using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deckshift → Screen Gallery.
///
/// Walks every full-screen UI in the project, captures each one at 4:3 / 16:9 / 21:9, and writes a
/// contact sheet you can open in a browser. Two jobs:
///
///   1. A **baseline**. Before touching typography or the shared screen base class, this is what the
///      UI looked like. Re-run it afterwards and the two folders diff by eye.
///   2. A **regression net**. Every screen is built procedurally at runtime, so nothing catches a
///      screen that silently stopped opening, dropped out of the font system, or overflows a narrow
///      aspect. The sheet shows all of them side by side at the three aspects that matter.
///
/// ── Why it works the way it does ─────────────────────────────────────────────────────────────
///
/// ⚠️ **It requires Play mode and will not start one.** Entering play mode triggers a domain reload,
///    which would wipe this class's state mid-run. Every screen here is also procedural — none of
///    them exist in the scene at edit time — so there is nothing to photograph outside play mode.
///
/// ⚠️ **`ScreenCapture.CaptureScreenshot` is the only honest capture.** It grabs the real
///    framebuffer after the next frame renders, which is the only way to get Screen-Space-Overlay
///    UI at all. A manual `camera.Render()` into a RenderTexture sees no overlay UI whatsoever, and
///    CLAUDE.md already records it sorting differently from the real pipeline. Because it is async,
///    every capture here waits for the file to appear on disk rather than assuming.
///
/// ⚠️ **Aspect is changed by driving the Game View's own size dropdown** (reflection into
///    `UnityEditor.GameView.selectedSizeIndex`). That makes the capture genuinely 2560x1080 rather
///    than a letterboxed 16:9, which is the entire point — the canvas is `ScaleWithScreenSize` with
///    `matchWidthOrHeight = 1` (HEIGHT), so width is what flexes and width is what breaks screens.
///    The original size is restored when the run finishes or aborts.
///
/// ⚠️ **Waits are WALL CLOCK, never `Time.deltaTime`.** Every screen in this project pauses the game
///    (`GameManager.RequestPause` → `timeScale = 0`) and animates itself on unscaled time. A
///    scaled-time wait would hang forever on the first modal.
///
/// ⚠️ **It shoots after a settle delay, not on the build frame.** A screenshot taken on the frame a
///    UI is built shows the AUTHORED colours, not the animated ones — that trap is already recorded
///    in the project's docs, and it is exactly what a gallery would fall into by default.
///
/// ⚠️ **A screen that fails to open records the failure and the run continues.** A regression net
///    that aborts on the first broken screen tells you about one problem per run.
///
/// The run stages the save state first (gold, scrap, relics, a damaged card, an exhausted card) so
/// screens render populated. Empty screens are a useless baseline — and several of them deliberately
/// collapse to a single explanatory line when they have no content. All of it is play-mode state,
/// which Unity discards when you press Stop.
/// </summary>
public static class ScreenGallery
{
    // ---- timing (wall clock, seconds) ----
    private const float RelayoutWait = 0.60f;  // canvas reflow after an aspect change
    private const float BuildWait    = 0.30f;  // screen instantiates itself + first layout pass
    private const float SettleWait   = 0.90f;  // entry animations finish before the shutter
    private const float TeardownWait = 0.35f;  // close animation + pause release land
    private const float FileTimeout  = 8.00f;  // give up waiting for a capture to hit disk

    private static readonly Aspect[] Aspects =
    {
        new Aspect("4-3",  1440, 1080),
        new Aspect("16-9", 1920, 1080),
        new Aspect("21-9", 2560, 1080),
    };

    private struct Aspect
    {
        public readonly string Label;
        public readonly int W, H;
        public Aspect(string label, int w, int h) { Label = label; W = w; H = h; }
        public override string ToString() { return Label + " (" + W + "x" + H + ")"; }
    }

    private class ScreenDef
    {
        public string Name;
        public string Note;
        public Func<bool> Open;
        public Action Close;

        /// <summary>
        /// Extra settle time for a screen that needs longer than the default before it is worth
        /// photographing (instantiating character rigs, building RenderTextures, …).
        /// </summary>
        public float ExtraSettle;

        /// <summary>
        /// Destroy the screen's GameObject after capture instead of trusting its close.
        ///
        /// ⚠️ Some screens' `Hide()` only deactivates their UI content and leaves live objects
        /// behind. That is invisible in the game and *poisons a gallery run*, because everything
        /// captured afterwards inherits the leftovers. Destroying the object runs the screen's own
        /// `OnDestroy`, which is where the real teardown lives.
        /// </summary>
        public bool DestroyOnClose;

        /// <summary>The type whose static `instance` field the close/destroy helpers reach for.</summary>
        public Type Owner;
    }

    private class Shot
    {
        public string Screen;
        public string Aspect;
        public string File;   // relative filename, null if it failed
        public string Error;  // null if it worked
    }

    // ---- run state ----
    private static IEnumerator driver;
    private static double resumeAt;
    private static int restoreSizeIndex = -1;
    private static GameObject stagedShopkeeper;

    // =====================================================================================
    // Menu
    // =====================================================================================

    [MenuItem("Deckshift/Screen Gallery")]
    private static void RunMenu()
    {
        if (driver != null)
        {
            EditorUtility.DisplayDialog("Screen Gallery", "A gallery run is already in progress.", "OK");
            return;
        }

        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Screen Gallery",
                "The gallery needs Play mode.\n\n" +
                "Every screen in Deckshift is built procedurally at runtime — none of them exist in " +
                "the scene at edit time, so there is nothing to photograph until the game is running.\n\n" +
                "Press Play, then run this again.",
                "OK");
            return;
        }

        string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ScreenGallery");
        string outDir = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(outDir);

        Debug.Log("[ScreenGallery] starting — output: " + outDir);

        driver = Sequence(outDir);
        resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Deckshift/Screen Gallery", validate = true)]
    private static bool RunMenuValidate() { return driver == null; }

    // =====================================================================================
    // Driver
    //
    // A hand-rolled coroutine pump. The gallery cannot be a MonoBehaviour coroutine (it has to
    // outlive and drive editor-side things like the Game View size), and it cannot block, because
    // blocking the editor stops the game rendering and there would be nothing to capture.
    // `yield return <float>` waits that many seconds of WALL CLOCK.
    // =====================================================================================

    private static void Tick()
    {
        if (driver == null) { EditorApplication.update -= Tick; return; }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ScreenGallery] play mode exited — run aborted.");
            Finish();
            return;
        }

        if (EditorApplication.timeSinceStartup < resumeAt) return;

        bool more;
        try
        {
            more = driver.MoveNext();
        }
        catch (Exception e)
        {
            Debug.LogError("[ScreenGallery] run failed: " + e);
            Finish();
            return;
        }

        if (!more) { Finish(); return; }

        if (driver.Current is float wait) resumeAt = EditorApplication.timeSinceStartup + wait;
        else resumeAt = 0;
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        driver = null;
        resumeAt = 0;

        // Always put the editor back the way we found it, even on an abort.
        if (restoreSizeIndex >= 0)
        {
            try { GameViewSizer.Select(restoreSizeIndex); } catch { /* editor teardown */ }
            restoreSizeIndex = -1;
        }

        if (stagedShopkeeper != null)
        {
            UnityEngine.Object.Destroy(stagedShopkeeper);
            stagedShopkeeper = null;
        }

        ReleaseLeakedPause();
    }

    // =====================================================================================
    // The run
    // =====================================================================================

    private static IEnumerator Sequence(string outDir)
    {
        var shots = new List<Shot>();

        restoreSizeIndex = GameViewSizer.Current();

        string stageLog = Stage();
        Debug.Log("[ScreenGallery] staged: " + stageLog);
        yield return 0.5f;

        HashSet<int> cameraBaseline = CameraCensus();

        List<ScreenDef> screens = BuildRegistry();

        int total = screens.Count * Aspects.Length;
        int done = 0;

        foreach (Aspect aspect in Aspects)
        {
            GameViewSizer.Select(GameViewSizer.Ensure(aspect.W, aspect.H, "Deckshift Gallery"));
            yield return RelayoutWait;

            foreach (ScreenDef screen in screens)
            {
                var shot = new Shot { Screen = screen.Name, Aspect = aspect.Label };
                bool opened = false;

                try
                {
                    opened = screen.Open();
                }
                catch (Exception e)
                {
                    shot.Error = e.GetBaseException().Message;
                }

                if (shot.Error == null && !opened)
                    shot.Error = "screen did not open (missing instance or could not be staged)";

                if (shot.Error == null)
                {
                    yield return BuildWait;
                    yield return SettleWait + screen.ExtraSettle;

                    string file = Sanitize(screen.Name) + "__" + aspect.Label + ".png";
                    string path = Path.Combine(outDir, file);

                    ScreenCapture.CaptureScreenshot(path);

                    // Async: wait for the file rather than guessing at a frame count.
                    double deadline = EditorApplication.timeSinceStartup + FileTimeout;
                    while (!File.Exists(path) && EditorApplication.timeSinceStartup < deadline)
                        yield return 0.1f;

                    if (File.Exists(path)) shot.File = file;
                    else shot.Error = "capture did not reach disk within " + FileTimeout + "s";
                }

                try
                {
                    screen.Close();
                    if (screen.DestroyOnClose) DestroyInstance(screen.Owner);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ScreenGallery] close failed for " + screen.Name + ": " + e.Message);
                }

                yield return TeardownWait;

                // A screen that failed to close would leave the game paused and the HUD hidden,
                // silently poisoning every capture after it.
                ReleaseLeakedPause();
                AssertNoLeakedCameras(screen.Name, cameraBaseline);
                yield return 0.15f;

                shots.Add(shot);
                done++;
                Debug.Log("[ScreenGallery] " + done + "/" + total + "  " + screen.Name + " @ " + aspect.Label +
                          (shot.Error == null ? "  ok" : "  FAILED: " + shot.Error));
            }
        }

        GameViewSizer.Select(restoreSizeIndex);
        restoreSizeIndex = -1;
        yield return 0.2f;

        string indexPath = Path.Combine(outDir, "index.html");
        File.WriteAllText(indexPath, BuildContactSheet(shots, screens, stageLog), new UTF8Encoding(false));

        int failed = 0;
        foreach (Shot s in shots) if (s.Error != null) failed++;

        Debug.Log("[ScreenGallery] done — " + (shots.Count - failed) + "/" + shots.Count +
                  " captured" + (failed > 0 ? ", " + failed + " FAILED" : "") + "\n" + indexPath);

        EditorUtility.RevealInFinder(indexPath);
    }

    // =====================================================================================
    // Staging — make the screens have something to show
    // =====================================================================================

    private static string Stage()
    {
        var notes = new List<string>();

        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.AddGold(1200);
            player.AddScrap(140);
            notes.Add("1200 gold, 140 scrap");
        }

        // Relics — fills the loadout bar, the manage panel and the swap screen's "current loadout"
        // column. Pulled straight off disk so this does not depend on RelicPool's signature.
        if (RelicManager.instance != null)
        {
            var relics = LoadAssets<RelicData>("t:RelicData");
            int granted = 0;
            foreach (RelicData r in relics)
            {
                if (granted >= 3) break;
                if (RelicManager.instance.HasRelic(r.relicID)) continue;
                RelicManager.instance.AddRelic(r);
                granted++;
            }
            notes.Add(granted + " relics");
        }

        // Cards — a damaged one and an exhausted one, so the Scrap Forge has both of its sections
        // populated. Its empty state deliberately collapses to one line, which would be a
        // misleading thing to photograph as "the forge".
        DeckManager deck = DeckManager.instance;
        if (deck != null)
        {
            List<RuntimeCard> hand = deck.GetCurrentHand();
            if (hand != null && hand.Count > 0)
            {
                RuntimeCard damaged = hand[0];
                if (!DeckManager.IsStagger(damaged) && damaged.cardData != null)
                {
                    damaged.currentUses = 1;
                    notes.Add("1 damaged card");
                }

                // One blessed card, so CardUI's blessing mark appears somewhere in the sheet.
                if (hand.Count > 1 && !DeckManager.IsStagger(hand[1]))
                {
                    hand[1].enhancement = CardEnhancement.Ritual;
                    notes.Add("1 blessed card");
                }
            }

            // An exhausted card, so the forge's salvage section has something in it. Prefer moving one
            // out of the draw pile; early in a run that pile is often empty (the whole deck is in
            // hand), so fall back to a clone rather than leaving the section collapsed — a forge with
            // nothing to repair is a misleading thing to photograph as "the forge".
            List<RuntimeCard> exhaust = deck.GetExhaustPile();
            List<RuntimeCard> draw = deck.GetDrawPile();
            if (exhaust != null && exhaust.Count == 0)
            {
                RuntimeCard spent = null;

                if (draw != null && draw.Count > 0)
                {
                    spent = draw[draw.Count - 1];
                    draw.RemoveAt(draw.Count - 1);
                }
                else if (hand != null && hand.Count > 0)
                {
                    spent = hand[hand.Count - 1].Clone();
                }

                if (spent != null && !DeckManager.IsStagger(spent))
                {
                    spent.currentUses = 0;
                    exhaust.Add(spent);
                    notes.Add("1 exhausted card");
                }
            }

            deck.RefreshHandUI();
        }

        // The shop needs a live Shopkeeper. Built here rather than at open time because
        // `Shopkeeper.Start()` is what stocks the shelf, and Start does not run until the next frame.
        if (stagedShopkeeper == null)
        {
            stagedShopkeeper = new GameObject("~ScreenGalleryShopkeeper");
            stagedShopkeeper.AddComponent<Shopkeeper>();
            notes.Add("a shopkeeper");
        }

        return notes.Count > 0 ? string.Join(", ", notes.ToArray()) : "nothing (no player/deck found)";
    }

    // =====================================================================================
    // Registry
    // =====================================================================================

    private static List<ScreenDef> BuildRegistry()
    {
        var list = new List<ScreenDef>();

        list.Add(new ScreenDef
        {
            Name = "Gameplay HUD",
            Note = "no modal open — the baseline every other shot covers up",
            Open = () => true,
            Close = () => { },
        });

        list.Add(Simple("Pause", typeof(PauseScreen), "Halt", () => InvokeInstance(typeof(PauseScreen), "Open")));

        list.Add(Simple("Settings", typeof(SettingsScreen), "Apparatus", () =>
        {
            SettingsScreen.Open(null);
            return true;
        }));

        list.Add(Simple("Run Map", typeof(RunMapScreen), "Cartograph", () =>
        {
            RunMapScreen.Open();
            return true;
        }));

        list.Add(Simple("Quest Board", typeof(QuestBoardScreen), "Bulletin", () =>
        {
            QuestBoardScreen.Open();
            return true;
        }));

        list.Add(Simple("Scrap Forge", typeof(ScrapForgeScreen), "Iron", () =>
        {
            ScrapForgeScreen.Open();
            return true;
        }));

        list.Add(Simple("Blompo", typeof(BlompoScreen), "Arcane", () =>
        {
            BlompoScreen.Open(null, new List<CardEnhancement>
            {
                CardEnhancement.Ritual, CardEnhancement.Grudge, CardEnhancement.Echo
            }, null);
            return true;
        }));

        list.Add(Simple("Relic Manage", typeof(RelicManagePanel), "Loadout", () =>
        {
            RelicManagePanel.Open();
            return true;
        }));

        list.Add(Simple("Relic Swap", typeof(RelicSwapScreen), "Loadout — the forced full-slot decision", () =>
        {
            RelicData incoming = FirstUnownedRelic();
            if (incoming == null) return false;
            RelicSwapScreen.Open(incoming, null, null);
            return true;
        }));

        list.Add(Simple("Shop", typeof(ShopScreenUI), "Marketplace", () =>
        {
            if (stagedShopkeeper == null) return false;
            ShopScreenUI.Open(stagedShopkeeper.GetComponent<Shopkeeper>());
            return true;
        }));

        list.Add(Simple("Card Chest", typeof(CardChestScreen), "the card reward pick", () =>
        {
            CardChestScreen.Open(null);
            return true;
        }));

        list.Add(new ScreenDef
        {
            Name = "Deck View",
            Note = "the scrollable pile inspector",
            Open = () =>
            {
                if (DeckViewUI.instance == null) return false;
                DeckViewUI.instance.ShowFullDeck();
                return true;
            },
            Close = () => { if (DeckViewUI.instance != null) DeckViewUI.instance.CloseView(); },
        });

        // ⚠️ Destroyed rather than hidden, and given extra settle. `Hide()` now deactivates the
        // character plots too, so the old failure — portrait cameras surviving a resolution change
        // with dead RenderTextures and drawing the stage straight into every later capture — can no
        // longer happen. Destroying is kept anyway: it is the only path that RELEASES those
        // RenderTextures, and this tool changes resolution three times per screen.
        list.Add(Simple("Character Select", typeof(CharacterSelectScreen),
            "Marquee — a main-menu screen, so it may not stage correctly in a gameplay scene", () =>
        {
            CharacterSelectScreen.Open(null);
            return true;
        }, extraSettle: 0.8f, destroyOnClose: true));

        return list;
    }

    private static ScreenDef Simple(string name, Type type, string note, Func<bool> open,
                                    float extraSettle = 0f, bool destroyOnClose = false)
    {
        return new ScreenDef
        {
            Name = name,
            Note = note,
            Owner = type,
            Open = open,
            Close = () => CloseAny(type),
            ExtraSettle = extraSettle,
            DestroyOnClose = destroyOnClose,
        };
    }

    // =====================================================================================
    // Reflection helpers
    //
    // Every screen's close is a PRIVATE `Hide()` or `Close()` on a static `instance`. That is fine
    // for the game (each screen owns its own dismissal) but leaves a tool with no public way to put
    // a screen away, so the gallery reaches in. If a shared screen base class ever lands, this
    // whole section collapses into one virtual call.
    // =====================================================================================

    private static UnityEngine.Object InstanceOf(Type t)
    {
        FieldInfo f = t.GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return null;
        return f.GetValue(null) as UnityEngine.Object;
    }

    private static bool InvokeInstance(Type t, string method)
    {
        UnityEngine.Object inst = InstanceOf(t);
        if (inst == null) return false;   // Unity's == null: also catches destroyed objects
        MethodInfo m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                   null, Type.EmptyTypes, null);
        if (m == null) return false;
        m.Invoke(inst, null);
        return true;
    }

    private static void CloseAny(Type t)
    {
        if (InvokeInstance(t, "Hide")) return;
        if (InvokeInstance(t, "Close")) return;

        MethodInfo sm = t.GetMethod("Close", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                    null, Type.EmptyTypes, null);
        if (sm != null) sm.Invoke(null, null);
    }

    /// <summary>
    /// A screen that fails to close leaves the pause counter held and the HUD hidden, which would
    /// silently corrupt every capture that follows it. Cheaper to assert than to debug later.
    /// </summary>
    private static void ReleaseLeakedPause()
    {
        GameManager gm = GameManager.instance;
        if (gm == null) return;

        int released = 0;
        while (gm.IsUIPaused && released < 16) { gm.ReleasePause(); released++; }
        if (released > 0)
            Debug.LogWarning("[ScreenGallery] a screen leaked " + released + " pause(s) — released them so " +
                             "the following captures are not taken through a paused, HUD-hidden game.");

        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
    }

    private static void DestroyInstance(Type owner)
    {
        if (owner == null) return;
        UnityEngine.Object inst = InstanceOf(owner);
        if (inst == null) return;
        var mb = inst as MonoBehaviour;
        UnityEngine.Object.Destroy(mb != null ? (UnityEngine.Object)mb.gameObject : inst);
    }

    /// <summary>
    /// Every enabled camera that draws to the screen (rather than into a RenderTexture).
    ///
    /// This is the census that catches a leaking screen. A screen whose close leaves something
    /// *rendering* behind does not fail loudly — it quietly composites itself into every capture
    /// that follows, and the run finishes "successfully" with wrong pictures. That is precisely how
    /// the first run went: Character Select left its portrait cameras alive, a resolution change
    /// invalidated their RenderTextures, and they began drawing the stage over the game.
    /// </summary>
    private static HashSet<int> CameraCensus()
    {
        var set = new HashSet<int>();
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c != null && c.isActiveAndEnabled && c.targetTexture == null)
                set.Add(c.GetInstanceID());
        return set;
    }

    private static void AssertNoLeakedCameras(string screenName, HashSet<int> baseline)
    {
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (c == null || !c.isActiveAndEnabled || c.targetTexture != null) continue;
            if (baseline.Contains(c.GetInstanceID())) continue;

            Debug.LogWarning("[ScreenGallery] '" + screenName + "' left a camera drawing to the screen ('" +
                             c.name + "'). Every capture after this one is contaminated — fix the screen's " +
                             "teardown, or mark it DestroyOnClose in the registry.");
            baseline.Add(c.GetInstanceID());   // report once, not on every subsequent screen
        }
    }

    private static RelicData FirstUnownedRelic()
    {
        foreach (RelicData r in LoadAssets<RelicData>("t:RelicData"))
        {
            if (r == null) continue;
            if (RelicManager.instance != null && RelicManager.instance.HasRelic(r.relicID)) continue;
            return r;
        }
        return null;
    }

    private static List<T> LoadAssets<T>(string filter) where T : UnityEngine.Object
    {
        var result = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets(filter))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) result.Add(a);
        }
        return result;
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString();
    }

    // =====================================================================================
    // Game View sizing
    // =====================================================================================

    // Internal (not private): TrailerWindow drives the Game View to 1920x1080 through it too.
    internal static class GameViewSizer
    {
        private static readonly Assembly EdAsm = typeof(Editor).Assembly;
        private static Type TSizes { get { return EdAsm.GetType("UnityEditor.GameViewSizes"); } }
        private static Type TGroup { get { return EdAsm.GetType("UnityEditor.GameViewSizeGroup"); } }
        private static Type TSize { get { return EdAsm.GetType("UnityEditor.GameViewSize"); } }
        private static Type TSizeType { get { return EdAsm.GetType("UnityEditor.GameViewSizeType"); } }
        private static Type TGameView { get { return EdAsm.GetType("UnityEditor.GameView"); } }

        private const int FixedResolution = 1;   // UnityEditor.GameViewSizeType.FixedResolution

        private static object Group()
        {
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(TSizes);
            object inst = singleton.GetProperty("instance").GetValue(null, null);
            object groupType = TSizes.GetProperty("currentGroupType").GetValue(inst, null);
            return TSizes.GetMethod("GetGroup").Invoke(inst, new object[] { (int)groupType });
        }

        private static EditorWindow Window()
        {
            return EditorWindow.GetWindow(TGameView, false, null, false);
        }

        private static PropertyInfo SelectedIndexProp()
        {
            return TGameView.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        /// <summary>Index of an existing fixed-resolution entry of this size, adding one if absent.</summary>
        internal static int Ensure(int w, int h, string label)
        {
            object group = Group();
            int total = (int)TGroup.GetMethod("GetTotalCount").Invoke(group, null);

            for (int i = 0; i < total; i++)
            {
                object s = TGroup.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)TSize.GetProperty("sizeType").GetValue(s, null) != FixedResolution) continue;
                if ((int)TSize.GetProperty("width").GetValue(s, null) != w) continue;
                if ((int)TSize.GetProperty("height").GetValue(s, null) != h) continue;
                return i;
            }

            object size = Activator.CreateInstance(TSize, new object[]
            {
                Enum.ToObject(TSizeType, FixedResolution), w, h, label
            });
            TGroup.GetMethod("AddCustomSize").Invoke(group, new object[] { size });
            return (int)TGroup.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }

        internal static int Current()
        {
            EditorWindow win = Window();
            if (win == null) return -1;
            return (int)SelectedIndexProp().GetValue(win, null);
        }

        internal static void Select(int index)
        {
            if (index < 0) return;
            EditorWindow win = Window();
            if (win == null) return;
            SelectedIndexProp().SetValue(win, index, null);
            win.Repaint();
        }
    }

    // =====================================================================================
    // Contact sheet
    //
    // HTML rather than a stitched PNG montage: it labels every shot, marks failures, and lets a
    // click open the full-resolution capture. Rendering readable labels into a texture would mean
    // rasterising a font by hand for no gain.
    //
    // The three aspects are shown at EQUAL HEIGHT, which is the honest comparison — all three are
    // 1080 tall and the canvas matches on height, so equal height makes the extra width that 21:9
    // hands you (and the width 4:3 takes away) immediately visible.
    // =====================================================================================

    private static string BuildContactSheet(List<Shot> shots, List<ScreenDef> screens, string stageLog)
    {
        int failed = 0;
        foreach (Shot s in shots) if (s.Error != null) failed++;

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Deckshift — Screen Gallery</title>");
        sb.AppendLine(@"<style>
:root { color-scheme: dark; }
body { margin:0; padding:32px 40px 80px; background:#141210; color:#d8d2c8;
       font:15px/1.55 -apple-system,Segoe UI,Roboto,sans-serif; }
h1 { margin:0 0 4px; font-size:26px; letter-spacing:.04em; font-weight:600; color:#f0e9dd; }
.meta { color:#8b8378; font-size:13px; margin-bottom:6px; }
.meta b { color:#c9a227; font-weight:600; }
.fail-count { color:#d4574a; font-weight:600; }
hr { border:0; border-top:1px solid #2c2823; margin:26px 0; }
section { margin:0 0 40px; }
h2 { font-size:18px; margin:0 0 2px; color:#f0e9dd; font-weight:600; }
.note { color:#8b8378; font-size:13px; margin:0 0 12px; }
.row { display:flex; gap:16px; align-items:flex-start; flex-wrap:wrap; }
figure { margin:0; }
figcaption { font-size:12px; color:#8b8378; margin-top:6px; letter-spacing:.06em; text-transform:uppercase; }
img { height:250px; width:auto; display:block; border:1px solid #2c2823; background:#000; }
a:hover img { border-color:#c9a227; }
.err { height:250px; display:flex; align-items:center; justify-content:center; text-align:center;
       padding:0 18px; border:1px dashed #5a3230; background:#1e1513; color:#d4574a;
       font-size:13px; box-sizing:border-box; }
</style></head><body>");

        sb.AppendLine("<h1>Deckshift — Screen Gallery</h1>");
        sb.Append("<div class=\"meta\">")
          .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
          .Append(" &nbsp;·&nbsp; Unity ").Append(Esc(Application.unityVersion))
          .Append(" &nbsp;·&nbsp; scene <b>").Append(Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)).Append("</b>")
          .Append(" &nbsp;·&nbsp; ").Append(screens.Count).Append(" screens × ").Append(Aspects.Length).Append(" aspects")
          .AppendLine("</div>");

        sb.Append("<div class=\"meta\">Staged: ").Append(Esc(stageLog)).AppendLine("</div>");

        sb.Append("<div class=\"meta\">")
          .Append(shots.Count - failed).Append(" of ").Append(shots.Count).Append(" captured");
        if (failed > 0) sb.Append(" &nbsp;·&nbsp; <span class=\"fail-count\">").Append(failed).Append(" failed</span>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"meta\">Shown at equal height — the canvas matches on HEIGHT, so width is what flexes. Click any shot for full resolution.</div>");
        sb.AppendLine("<hr>");

        foreach (ScreenDef screen in screens)
        {
            sb.Append("<section><h2>").Append(Esc(screen.Name)).AppendLine("</h2>");
            if (!string.IsNullOrEmpty(screen.Note))
                sb.Append("<p class=\"note\">").Append(Esc(screen.Note)).AppendLine("</p>");
            sb.AppendLine("<div class=\"row\">");

            foreach (Aspect aspect in Aspects)
            {
                Shot shot = shots.Find(s => s.Screen == screen.Name && s.Aspect == aspect.Label);
                sb.AppendLine("<figure>");

                if (shot != null && shot.File != null)
                {
                    sb.Append("<a href=\"").Append(Esc(shot.File)).Append("\" target=\"_blank\"><img src=\"")
                      .Append(Esc(shot.File)).Append("\" alt=\"").Append(Esc(screen.Name)).AppendLine("\"></a>");
                }
                else
                {
                    string msg = shot != null && shot.Error != null ? shot.Error : "not captured";
                    sb.Append("<div class=\"err\" style=\"width:")
                      .Append(Mathf.RoundToInt(250f * aspect.W / aspect.H)).Append("px\">")
                      .Append(Esc(msg)).AppendLine("</div>");
                }

                sb.Append("<figcaption>").Append(Esc(aspect.ToString())).AppendLine("</figcaption>");
                sb.AppendLine("</figure>");
            }

            sb.AppendLine("</div></section>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
