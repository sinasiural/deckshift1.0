using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The run map — press M. A chart of the whole act, so the player plans a ROUTE rather than picking
// one door at a time.
//
// THEME: Cartograph. The screen is a folded sheet of parchment the player has just opened out.
//
// ⚠️ THIS IS THE THIRD ATTEMPT, AND THE TWO FAILURES ARE THE USEFUL PART. It was first a flat slate
// panel, then an acid-etched copper plate. Both were given a MATERIAL, both were carefully lit, and
// both still read as a diagram. The lesson: a material is not enough, because a map does not feel
// like a map on account of what it is made of. It feels like a map because it is a DOCUMENT —
// something printed, folded, carried, and then scribbled on. Four things carry that, and if any
// future pass strips one it will slide back to being a node graph:
//
//   1. PAPER, NOT A PANEL. The sheet IS the window — there is no frame around it and its edge is
//      torn, not chamfered. Every other screen in the game is a plate you look AT; this one is an
//      object you are holding.
//   2. FOLDS. Two vertical creases and one horizontal, with a lit and a shadowed side each. This is
//      the cheapest possible signal that the thing was in a pocket a second ago, and it is the
//      single detail that most says "I just opened this".
//   3. DASHED TRAILS. A solid line between two points is a graph edge. A dashed line is a ROUTE.
//      Nothing else on the screen changes the read as much for as little.
//   4. THE PLAYER'S PROGRESS IS ANNOTATION. The chart is printed in brown ink; where you have BEEN
//      and where you may go next is marked over the top in RED PEN. That is why the printed trails
//      are neat and mechanically tiled while the red ones are hand-drawn with per-stroke wobble —
//      the difference is the fiction, not an inconsistency. It also means every state on the map is
//      signalled without a colour key.
//
// It inverts the whole rest of the UI on VALUE — this is a light ground with dark ink, where every
// other screen is a dark plate with light text. Bulletin (the quest board) is the only relative, and
// the two are still separable at a glance: Bulletin is small pale slips pinned to a DARK board, so
// its dominant field is dark, while here the entire field is paper.
//
// House pattern: entirely procedural, self-instantiating under the root Canvas, no prefab, no art.
public class RunMapScreen : MonoBehaviour
{
    private static RunMapScreen instance;

    private const float WIN_W = 1560f;
    private const float WIN_H = 980f;

    private const float AREA_TOP = 132f;
    // ⚠️ Deep enough for the HUB's label. Every node's label hangs BELOW it, and the hub sits on the
    // chart's bottom edge — so its label reaches this far down into the margin and, at 96, landed
    // right on top of the footer line.
    private const float AREA_BOTTOM = 118f;
    private const float AREA_SIDE = 92f;

    // Printed trails. Period is dash + gap; fill is how much of that the dash occupies.
    private const float TRAIL_W = 5f;
    // ⚠️ The pen needs a SHORTER period and MORE overlap than feels necessary. A trail between two
    // adjacent floors is only ~60px after trimming, so at a 19px period it was three strokes with
    // wobble between them — which read as a faint dotted line, weaker than the printed trails it is
    // supposed to be drawn on top of. Your own route must be the boldest thing on the sheet.
    private const float PEN_PERIOD = 12f;
    private const float PEN_FILL = 1.40f;   // > 1 so the strokes overlap into a continuous line

    private RectTransform window, area;

    // ---- scrolling ------------------------------------------------------------------------------
    // ⚠️ THE CHART NO LONGER FITS THE SHEET, AND THAT IS THE POINT (designer, 2026-08-21: "make the
    // map scrollable, it can exceed the normal screen amount ... if the player can scroll up or
    // down on the map, itd be a better feeling").
    //
    // Before this the layout divided the visible height by the floor count, so every extra floor
    // squeezed the whole chart. At 20 floors that put roughly 40px between rows while the final
    // boss glyph alone is 88px across — the marks would have overlapped their own survey lines.
    // Floors now have a FIXED pitch and the chart is as tall as it needs to be; `area` clips it.
    private RectTransform chart;
    private float scrollY, scrollTarget, scrollMax;
    private bool recentreOnRefresh;

    // Pitch between floors, in canvas px. Set by the biggest glyph (the finale, 88px) plus room for
    // a trail to read as a trail rather than as two marks touching.
    private const float FLOOR_PITCH = 118f;

    // Padding above the top floor and below the hub, so neither sits jammed against the clip edge.
    private const float CHART_PAD = 90f;
    private CanvasGroup group;
    private TMP_FontAsset font;
    private TextMeshProUGUI footer, sub;

    private bool mustChoose;
    private System.Action onChosen;

    private bool isOpen;
    private GameObject cachedHud;
    private bool hudWasActive;
    private GameState prevState;

    private readonly List<Image> penMarks = new List<Image>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    // ---- entry points ---------------------------------------------------------------------------

    public static void Toggle()
    {
        EnsureInstance();
        if (instance == null) return;
        if (instance.isOpen)
        {
            if (instance.mustChoose) return;   // can't M your way out of a required choice
            instance.Hide();
        }
        else instance.Show();
    }

    // Opens the map because the run needs a branch before it can continue. `onChosen` runs once the
    // player commits — that is what actually spawns the next room.
    //
    // If the screen cannot be created, onChosen is invoked immediately rather than dropped. A
    // missing Canvas must not strand the run in a room with no way forward.
    public static void OpenForChoice(System.Action onChosen)
    {
        EnsureInstance();
        if (instance == null)
        {
            Debug.LogWarning("RunMapScreen: no Canvas, continuing without a route choice.");
            onChosen?.Invoke();
            return;
        }

        instance.mustChoose = true;
        instance.onChosen = onChosen;

        if (instance.isOpen) instance.Refresh();
        else instance.Show();
    }

    public static void Open()
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.Show();
    }

    public static void Close()
    {
        if (instance != null && instance.isOpen) instance.Hide();
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    private static void EnsureInstance()
    {
        if (instance != null) return;

        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("RunMapScreen: no Canvas found in scene."); return; }

        GameObject go = new GameObject("RunMapScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<RunMapScreen>();
        instance.Build();
        go.SetActive(false);
    }

    private static Canvas FindRootCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas c in all)
        {
            if (c == null) continue;
            if (fallback == null) fallback = c;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return fallback;
    }

    // ---- construction ---------------------------------------------------------------------------

    private void Build()
    {
        font = FlatUI.UIFont();

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        group = gameObject.AddComponent<CanvasGroup>();

        // Near-black behind, so the lit sheet reads as being held up in a dark room. This is also
        // what keeps the value inversion legible: paper only looks like paper against something.
        Image backdrop = AddImage(transform, "Backdrop", null, new Color(0.02f, 0.017f, 0.013f, 0.955f), true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(DismissIfAllowed);

        // A drop shadow under the sheet. Paper has thickness and sits ON something.
        Image shadow = AddPoint(transform, "SheetShadow", new Vector2(0.5f, 0.5f),
            new Vector2(6f, -10f), new Vector2(WIN_W + 44f, WIN_H + 44f)).gameObject.AddComponent<Image>();
        shadow.sprite = Parchment.Vignette();
        shadow.color = new Color(0f, 0f, 0f, 0.55f);
        shadow.raycastTarget = false;

        // THE SHEET. No panel sprite, no outline, no chamfer — the torn deckle in the paper texture
        // is the edge of the screen.
        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image paper = window.gameObject.AddComponent<Image>();
        paper.sprite = Parchment.Sheet();
        paper.color = Parchment.Paper;
        paper.raycastTarget = true;

        // Fibre at native resolution. The sheet sprite is blown up ~2.4x and softens; this puts the
        // tooth back. ⚠️ Must be Tiled — Simple would stretch a 64px noise tile across the whole
        // sheet and produce visible blobs instead of grain.
        Image grain = AddImage(window, "Grain", Parchment.Grain(), new Color(0.30f, 0.22f, 0.13f, 0.30f), false);
        grain.type = Image.Type.Tiled;
        Stretch(grain.rectTransform);

        Image vig = AddImage(window, "Vignette", Parchment.Vignette(), new Color(0.24f, 0.17f, 0.09f, 0.32f), false);
        Stretch(vig.rectTransform);

        BuildFolds();

        // Compass rose, in the header margin where a chart conventionally puts one.
        //
        // ⚠️ It was first drawn large and faint (0.115) as a watermark UNDER the chart and was
        // simply invisible — on a light ground a low-alpha dark mark washes out instead of reading
        // as subtle, which is the exact inverse of how the dark screens behave. Smaller, in clear
        // space, and roughly three times the alpha: now it is a printed detail rather than a stain.
        Image rose = AddImage(window, "Compass", Parchment.Compass(), Fade(Parchment.InkSoft, 0.34f), false);
        rose.rectTransform.anchorMin = rose.rectTransform.anchorMax = new Vector2(0f, 1f);
        rose.rectTransform.anchoredPosition = new Vector2(148f, -96f);
        rose.rectTransform.sizeDelta = new Vector2(132f, 132f);

        BuildCartouche();

        area = AddPoint(window, "Area", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        area.anchorMin = new Vector2(0f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.offsetMin = new Vector2(AREA_SIDE, AREA_BOTTOM);
        area.offsetMax = new Vector2(-AREA_SIDE, -AREA_TOP);

        // `area` is now a VIEWPORT: it clips, and `chart` slides inside it.
        //
        // ⚠️ RectMask2D, NOT Mask. Mask needs a Graphic to cut its stencil from, which would put an
        // opaque quad over the paper — and RectMask2D is a rectangular clip with no extra draw call,
        // which is exactly what a rectangular viewport wants. Same choice the quest board's beam
        // frame made for the same reason.
        area.gameObject.AddComponent<RectMask2D>();

        // ⚠️ A transparent Image so the viewport can CATCH the scroll wheel and drags. Without a
        // raycast target the pointer falls straight through to the sheet and nothing scrolls — and
        // it must sit BEHIND the nodes (built into `chart` afterwards) or it eats their clicks.
        Image catcher = area.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        chart = AddPoint(area, "Chart", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        chart.anchorMin = chart.anchorMax = chart.pivot = new Vector2(0.5f, 0.5f);

        // Wheel and drag both land on the viewport and drive the same value.
        MapScrollInput input = area.gameObject.AddComponent<MapScrollInput>();
        input.Bind(this);

        footer = AddText(window, "Footer", "", 15f, Parchment.InkSoft, TextAlignmentOptions.Center);
        footer.rectTransform.anchorMin = new Vector2(0f, 0f);
        footer.rectTransform.anchorMax = new Vector2(1f, 0f);
        footer.rectTransform.pivot = new Vector2(0.5f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        footer.rectTransform.sizeDelta = new Vector2(-120f, 24f);
        footer.characterSpacing = 4f;
    }

    // Two vertical creases and one horizontal — the way a pocket map is actually folded.
    //
    // ⚠️ EACH CREASE IS A PAIR: a shadow line and a highlight line, side by side. One line alone
    // reads as a drawn rule on the paper; it is the pairing that reads as the paper being bent,
    // because that is what a fold does to light. Getting the order wrong (light on the far side)
    // makes the sheet look embossed outward instead of folded.
    private void BuildFolds()
    {
        // ⚠️ THE HIGHLIGHT MUST BE NEARLY NOTHING. First pass used near-white at 0.20 and the sheet
        // came out with three glowing lines ruled across it — they read as laser guides, not folds.
        // This is the LIGHT-GROUND inversion of the usual linear-space trap: on the dark screens a
        // small bright alpha blooms, but here the ground is already bright, so a bright line has
        // almost no headroom above it and any visible value instantly looks drawn-on. The SHADOW is
        // what a viewer actually reads as a crease; the highlight only has to keep it from looking
        // like a pencil rule. Judge both on screen, never on paper.
        //
        // FadedRule so each crease dies out before the torn edge — a fold does not reach the deckle.
        foreach (float x in new[] { -WIN_W / 6f, WIN_W / 6f })
        {
            Image dark = AddImage(window, "FoldV", FlatUI.FadedRule(), Fade(Parchment.PaperShade, 0.30f), false);
            dark.rectTransform.anchoredPosition = new Vector2(x, 0f);
            dark.rectTransform.sizeDelta = new Vector2(WIN_H - 70f, 3f);
            dark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Image lit = AddImage(window, "FoldVLit", FlatUI.FadedRule(), new Color(1f, 0.98f, 0.92f, 0.055f), false);
            lit.rectTransform.anchoredPosition = new Vector2(x + 3f, 0f);
            lit.rectTransform.sizeDelta = new Vector2(WIN_H - 70f, 3f);
            lit.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        Image hDark = AddImage(window, "FoldH", FlatUI.FadedRule(), Fade(Parchment.PaperShade, 0.26f), false);
        hDark.rectTransform.sizeDelta = new Vector2(WIN_W - 70f, 3f);
        Image hLit = AddImage(window, "FoldHLit", FlatUI.FadedRule(), new Color(1f, 0.98f, 0.92f, 0.05f), false);
        hLit.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        hLit.rectTransform.sizeDelta = new Vector2(WIN_W - 70f, 3f);
    }

    // The title block, drawn the way a chart labels itself: rules above and below, with the sheet's
    // own name between them.
    private void BuildCartouche()
    {
        TextMeshProUGUI title = AddText(window, "Title", "THE OXIDATION DISTRICT", 34f, Parchment.Ink,
            TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        title.rectTransform.sizeDelta = new Vector2(-120f, 42f);
        title.characterSpacing = 9f;

        sub = AddText(window, "Sub", "", 16.5f, Parchment.InkSoft, TextAlignmentOptions.Center);
        sub.rectTransform.anchorMin = new Vector2(0f, 1f);
        sub.rectTransform.anchorMax = new Vector2(1f, 1f);
        sub.rectTransform.pivot = new Vector2(0.5f, 1f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, -74f);
        sub.rectTransform.sizeDelta = new Vector2(-120f, 22f);
        sub.characterSpacing = 6f;

        // Double rule under the title block, thick over thin — an engraver's convention, and the
        // asymmetry is what stops it reading as a UI divider.
        //
        // ⚠️ TWO SEGMENTS WITH A GAP, not one line. The boss mark sits dead centre on the chart's
        // top row and reaches up past this height, so a single centred rule ran straight through
        // it — the node's paper disc masked the middle and the rule appeared to be interrupted by
        // the boss. Leaving the centre empty is also just what a cartouche does.
        foreach (float s in new[] { -1f, 1f })
        {
            Rule(s * 176f, -104f, 210f, 2.5f, 0.85f);
            Rule(s * 176f, -110f, 210f, 1.5f, 0.55f);

            // A small lozenge capping the outer end. Without it the line just stops.
            Image dot = AddImage(window, "RuleCap", Parchment.Blot(), Fade(Parchment.Ink, 0.8f), false);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            dot.rectTransform.anchoredPosition = new Vector2(s * 281f, -107f);
            dot.rectTransform.sizeDelta = new Vector2(9f, 9f);
        }
    }

    private void Rule(float x, float y, float w, float h, float a)
    {
        Image r = AddImage(window, "Rule", FlatUI.FadedRule(), Fade(Parchment.Ink, a), false);
        r.rectTransform.anchorMin = r.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        r.rectTransform.anchoredPosition = new Vector2(x, y);
        r.rectTransform.sizeDelta = new Vector2(w, h);
    }

    // ---- open / close ---------------------------------------------------------------------------

    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (GameManager.instance != null)
        {
            prevState = GameManager.instance.currentState;
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }

        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        FitWindowToCanvas();

        // ⚠️ OPENING A 20-FLOOR MAP MUST SHOW YOU WHERE YOU ARE, not the bottom of the sheet. Only
        // on OPEN, though — Refresh also runs when the player commits to a branch, and re-centring
        // there would yank the view out from under someone who had just scrolled ahead to plan.
        recentreOnRefresh = true;
        Refresh();

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(prevState);
        }

        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        gameObject.SetActive(false);
    }

    // Shrinks the sheet if the canvas is narrower or shorter than its design size.
    //
    // ⚠️ This is the WIDEST window in the game (1560), so it is the first thing to run off the edge
    // on a narrow display. With the canvas matching HEIGHT, its logical width is 1080 * aspect —
    // 1920 at 16:9 and 2560 at 21:9, but only 1440 at 4:3.
    //
    // Only the map RESIZES. Its chart lives in `area`, anchored to the sheet's corners with insets,
    // so it genuinely reflows — LayOutNodes reads `area.rect` fresh every Refresh and re-solves the
    // lattice for the smaller width. The other screens position content at fixed offsets from their
    // centre and must SCALE instead.
    private void FitWindowToCanvas()
    {
        RectTransform parent = transform as RectTransform;
        if (parent == null) return;

        Rect r = parent.rect;
        if (r.width <= 1f || r.height <= 1f) return;   // not laid out yet

        const float MARGIN = 40f;
        float w = Mathf.Min(WIN_W, r.width - MARGIN);
        float h = Mathf.Min(WIN_H, r.height - MARGIN);
        window.sizeDelta = new Vector2(w, h);
    }

    // Unfolding, not fading in. The sheet arrives slightly small and settles, which is the closest a
    // 0.2s open can get to "this was in your pocket a moment ago".
    private IEnumerator OpenAnim()
    {
        const float dur = 0.22f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // the screen pauses the game; scaled time is frozen
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);
            group.alpha = Mathf.Clamp01(k * 1.4f);
            window.localScale = Vector3.one * Mathf.Lerp(0.965f, 1f, e);
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one;
    }

    private void Update()
    {
        if (!isOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { DismissIfAllowed(); return; }

        // Keyboard scrolling, so the map is navigable without a wheel. Held rather than tapped —
        // 20 floors is a long way to travel one keypress at a time.
        float key = 0f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) key -= 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) key += 1f;
        if (key != 0f) Scroll(key * 900f * Time.unscaledDeltaTime);

        // Ease toward the target. Unscaled, because this screen freezes the game.
        if (!Mathf.Approximately(scrollY, scrollTarget))
        {
            scrollY = Mathf.Lerp(scrollY, scrollTarget, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (Mathf.Abs(scrollTarget - scrollY) < 0.5f) scrollY = scrollTarget;
            ApplyScroll();
        }

        TickMotion();
    }

    private void DismissIfAllowed()
    {
        if (mustChoose) return;
        Hide();
    }

    private void ConfirmChoice()
    {
        System.Action cb = onChosen;
        onChosen = null;
        mustChoose = false;

        Hide();
        cb?.Invoke();
    }

    // Paper does not move, so almost nothing here does. The single exception is the red pen around
    // the branches you may actually take: it breathes, because that is the one question the screen
    // exists to answer and it should be answerable without reading a word.
    private void TickMotion()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f);
        Color c = Color.Lerp(Parchment.RedSoft, Parchment.Red, k);
        for (int i = 0; i < penMarks.Count; i++)
            if (penMarks[i] != null) penMarks[i].color = c;
    }

    // ---- drawing --------------------------------------------------------------------------------

    private void Refresh()
    {
        // Deactivate before Destroy: Unity's Destroy is deferred to end of frame, so the old chart
        // would otherwise render over the new one for a frame — and its buttons would still answer
        // GetComponentsInChildren.
        foreach (GameObject go in spawned)
        {
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        spawned.Clear();
        penMarks.Clear();

        RunMapManager mgr = RunMapManager.instance;
        RunMap map = mgr != null ? mgr.Map : null;

        if (map == null || map.nodes.Count == 0)
        {
            SetFooter("No act in progress.");
            return;
        }

        Dictionary<int, Vector2> pos = LayOutNodes(map);

        if (recentreOnRefresh)
        {
            recentreOnRefresh = false;
            CentreOnFloor(map, map.Current != null ? map.Current.floor : 0);
        }
        else ApplyScroll();   // the chart was just resized; put it back where the player left it

        DrawSurveyLines(map);

        foreach (MapNode n in map.nodes)
            foreach (int id in n.next)
            {
                MapNode m = map.Get(id);
                if (m == null) continue;
                DrawTrail(map, mgr, n, m, pos[n.id], pos[m.id]);
            }

        foreach (MapNode n in map.nodes) DrawNode(map, mgr, n, pos[n.id]);

        if (sub != null) sub.text = mustChoose ? "CHOOSE YOUR ROUTE" : "PRESS ESC TO CLOSE";

        int floorsLeft = (map.floors - 1) - (map.Current != null ? map.Current.floor : 0);
        if (mustChoose)
            SetFooter($"PICK A BRANCH TO CONTINUE   ·   {floorsLeft} TO THE BOSS");
        else if (mgr != null && mgr.HasChosenNext)
        {
            MapNode chosen = map.Get(mgr.ChosenNextId);
            SetFooter($"NEXT: {MapGlyphs.LabelFor(chosen.type)}{RechargeSuffix(chosen)}   ·   {floorsLeft} TO THE BOSS");
        }
        else if (map.AvailableNext().Count > 0)
            SetFooter($"PICK A BRANCH   ·   {floorsLeft} TO THE BOSS");
        else
            SetFooter("ACT COMPLETE");
    }

    private string RechargeSuffix(MapNode n)
        => n.recharge == RechargeType.None ? "" : $" + {MapGlyphs.LabelFor(n.recharge)}";

    private void SetFooter(string s)
    {
        if (footer != null) footer.text = s;
    }

    // Faint ruled lines across the sheet, one per floor, with a depth mark in the margin.
    //
    // They do the job the etched version's floor bands did — turning scattered marks into levels and
    // making the distance to the boss visible rather than a number in the footer — but drawn as
    // survey lines, which is a thing charts genuinely have. Tiled rather than built from individual
    // strokes: a band spans the whole sheet, and at ~65 strokes each that would be 500 objects for
    // the guide lines alone.
    private void DrawSurveyLines(RunMap map)
    {
        // Measured against the CHART, like the nodes — see LayOutNodes. A survey line placed
        // against the viewport would stay put while the marks it is meant to rule scrolled past it.
        Rect r = chart.rect;
        float h = r.height, w = r.width;
        float step = map.floors > 1 ? (h - CHART_PAD * 2f) / (map.floors - 1) : 0f;
        int curFloor = map.Current != null ? map.Current.floor : 0;

        for (int f = 0; f < map.floors; f++)
        {
            float y = -h * 0.5f + CHART_PAD + step * f;
            bool here = f == curFloor;
            bool bossLine = f == map.floors - 1;

            GameObject go = new GameObject($"Survey{f}", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(chart, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(w + 60f, 7f);

            Image img = go.AddComponent<Image>();
            img.sprite = Parchment.Stroke();
            img.type = Image.Type.Tiled;
            img.color = here ? Fade(Parchment.Red, 0.30f)
                      : bossLine ? Fade(Parchment.Ink, 0.34f)
                                 : Fade(Parchment.InkPale, 0.30f);
            img.raycastTarget = false;
            spawned.Add(go);

            string mark = f == 0 ? "HUB" : bossLine ? "BOSS" : f.ToString("00");
            TextMeshProUGUI t = AddText(chart, $"Depth{f}", mark, 12f,
                here ? Parchment.Red : Fade(Parchment.InkSoft, 0.85f), TextAlignmentOptions.Left);
            // ⚠️ INSIDE the chart's left edge, not 24px outside it. The gutter used to hang into the
            // sheet's margin, which was fine while nothing clipped — but `area` is a RectMask2D
            // viewport now, so anything past the chart's own width is cut away and the entire floor
            // numbering silently disappeared. There is ~137px between the chart edge and the first
            // column's centre, which is enough for a 70px label to sit clear of the marks.
            // ⚠️ anchoredPosition places the BOX CENTRE, and the box is 70 wide — so "just inside
            // the edge" has to account for its own half-width or the left half (and the
            // left-aligned text with it) still lands outside the mask. +41 puts the box's left edge
            // 6px inside the chart.
            t.rectTransform.anchoredPosition = new Vector2(-w * 0.5f + 41f, y + 12f);
            t.rectTransform.sizeDelta = new Vector2(70f, 15f);
            t.characterSpacing = 3f;
            spawned.Add(t.gameObject);
        }
    }

    // Every node sits on a FIXED COLUMN LATTICE: column decides x, floor decides y, nothing else
    // moves it. Combined with the survey lines that gives the chart a real grid, which is what lets
    // the eye compare one floor against another.
    //
    // ⚠️ DO NOT REINTRODUCE BARYCENTRIC RELAXATION HERE. It was tried and reverted. Pulling each
    // node toward the mean X of its neighbours and re-centring the row does straighten the trails —
    // measured, average sideways travel per edge falls 214px -> 86px — but it computes a DIFFERENT
    // spread for every row, so a floor with three nodes ends up sharing no column with a floor that
    // has five. The lattice disappears, and the designer read the result immediately as "the nodes
    // are off, they are not where they are meant to be". A grid you can scan beats trails that lean
    // less. Measured over 300 acts, edge crossings are zero either way, so nothing is lost.
    private Dictionary<int, Vector2> LayOutNodes(RunMap map)
    {
        // ⚠️ SIZE THE CHART FIRST. Everything below measures against `chart.rect`, not `area.rect`,
        // and the chart is deliberately TALLER than the viewport — that is what makes the map
        // scroll. Reading `area` here (as this did before) silently re-compresses the whole thing
        // back into one screen and the scrolling becomes a no-op with nothing to scroll to.
        SizeChart(map);

        Rect r = chart.rect;
        float w = r.width, h = r.height;
        float halfW = w * 0.5f;
        float step = map.floors > 1 ? (h - CHART_PAD * 2f) / (map.floors - 1) : 0f;

        int maxCol = 0;
        foreach (MapNode n in map.nodes) if (n.column > maxCol) maxCol = n.column;
        float colStep = maxCol > 0 ? w / (maxCol + 1) : w;

        Dictionary<int, Vector2> pos = new Dictionary<int, Vector2>();
        foreach (MapNode n in map.nodes)
        {
            // The run's spine. Start and the FINAL boss are single nodes and belong dead centre;
            // letting them take a lattice slot makes the whole chart look tipped over.
            //
            // ⚠️ MID-MAP BOSSES ARE NOT CENTRED, AND MUST NOT BE. They stand in a column like every
            // other node — that is the entire point of them being optional. Centring them (which
            // the old `type == Boss` test would now do) would draw every boss on the spine and make
            // them look mandatory.
            float x = (n.type == MapNodeType.Start || n.type == MapNodeType.FinalBoss || maxCol == 0)
                    ? 0f
                    : -halfW + colStep * (n.column + 0.5f);

            pos[n.id] = new Vector2(x, -h * 0.5f + CHART_PAD + step * n.floor);
        }
        return pos;
    }

    /// <summary>
    /// Makes the chart as tall as the run actually is, and works out how far it may slide.
    ///
    /// The width still tracks the viewport — only the vertical axis scrolls, because the map's
    /// columns are a fixed lattice at most five wide and there is nothing to find sideways.
    /// </summary>
    private void SizeChart(RunMap map)
    {
        // ⚠️ THE VIEWPORT'S RECT IS STALE UNTIL THE LAYOUT PASS RUNS. Show() resizes the window
        // (FitWindowToCanvas) and then Refreshes in the SAME frame, so `area.rect.height` still
        // reports the pre-resize value — the chart got sized against the wrong viewport and the
        // open-on-your-position scroll landed four floors off. Measured: centring on floor 6 put
        // the view on floor 2.
        Canvas.ForceUpdateCanvases();

        float viewH = area.rect.height;
        float needed = (map.floors - 1) * FLOOR_PITCH + CHART_PAD * 2f;

        // Never SHORTER than the viewport: a three-floor map would otherwise float in the middle of
        // a clipped box with its survey lines ending in mid-air.
        float h = Mathf.Max(viewH, needed);

        chart.sizeDelta = new Vector2(area.rect.width, h);

        // ⚠️ THE TRAVEL IS SYMMETRIC ABOUT ZERO, NOT [0 .. h - viewH]. The chart's pivot is its
        // CENTRE, so scrollY 0 shows the MIDDLE of the map — negative scrolls toward the finale,
        // positive back toward the hub. Clamping to a non-negative range therefore locked the view
        // at the middle and the top of the map was unreachable: measured, floors 6.4 to 12.6 were
        // the only ones that could ever be seen on a 20-floor chart, which is exactly what the
        // designer reported ("i can only go to floor 12, and cant see the top ever").
        //
        // At +halfRange the chart's bottom edge meets the viewport's bottom; at -halfRange its top
        // edge meets the top. Anything outside that is blank paper.
        scrollMax = Mathf.Max(0f, (h - viewH) * 0.5f);
        scrollY = Mathf.Clamp(scrollY, -scrollMax, scrollMax);
        scrollTarget = Mathf.Clamp(scrollTarget, -scrollMax, scrollMax);
    }

    /// <summary>Nudge the view. Positive scrolls UP the map (toward the finale).</summary>
    public void Scroll(float delta)
    {
        scrollTarget = Mathf.Clamp(scrollTarget + delta, -scrollMax, scrollMax);
    }

    /// <summary>Jump the view so a floor sits in the middle of the viewport. No easing.</summary>
    private void CentreOnFloor(RunMap map, int floor)
    {
        if (map == null || map.floors <= 1) return;
        float h = chart.rect.height;
        float step = (h - CHART_PAD * 2f) / (map.floors - 1);
        float yInChart = -h * 0.5f + CHART_PAD + step * floor;

        // Chart y offset that puts yInChart at the viewport's centre, clamped to the real travel.
        scrollTarget = Mathf.Clamp(-yInChart, -scrollMax, scrollMax);
        scrollY = scrollTarget;
        ApplyScroll();
    }

    private void ApplyScroll()
    {
        if (chart != null) chart.anchoredPosition = new Vector2(0f, scrollY);
    }

    // A trail between two marks.
    //
    // ⚠️ TWO DIFFERENT HANDS DRAW THESE, AND THAT IS DELIBERATE. Printed trails (everything the
    // player has not touched) are a mechanically tiled dash — neat, because a press printed them.
    // The player's own route and their live options are laid over the top in RED PEN, built from
    // individual strokes with per-stroke wobble and rotation, because a hand drew those. Making
    // both the same would throw away the annotation fiction that carries every state on this map.
    private void DrawTrail(RunMap map, RunMapManager mgr, MapNode from, MapNode to, Vector2 a, Vector2 b)
    {
        bool travelled = map.visited.Contains(from.id) && map.visited.Contains(to.id);
        bool open = map.currentNodeId == from.id && map.CanTravelTo(to.id);
        bool committed = open && mgr != null && mgr.ChosenNextId == to.id;

        // ⚠️ ONCE A BRANCH IS CHOSEN, THE OTHERS STOP BEING DRAWN IN PEN. Every open branch used to
        // stay in red no matter what you picked, so committing changed one line's thickness by two
        // pixels and left four rival red routes fanning out of the same node. Collapsing the pen to
        // the single route you actually drew is what makes the choice readable from across the
        // screen — and it is what a person marking up a map would have done.
        bool rejected = open && !committed && mgr != null && mgr.ChosenNextId >= 0;
        if (rejected) open = false;

        Vector2 delta = b - a;
        float len = delta.magnitude;
        if (len < 0.01f) return;

        // Trim to the marks at both ends: a trail runs BETWEEN two symbols, and drawn centre to
        // centre it runs through them and through the label hanging under the far one.
        Vector2 dir = delta / len;
        float ra = MapGlyphs.SizeFor(from.type) * 0.5f + 16f;
        float rb = MapGlyphs.SizeFor(to.type) * 0.5f + 16f;
        if (ra + rb >= len - 6f) return;

        a += dir * ra;
        b -= dir * rb;
        delta = b - a;
        len = delta.magnitude;

        int seed = from.id * 733 + to.id;

        if (travelled || committed || open)
        {
            Color pen = travelled || committed ? Parchment.Red : Parchment.RedSoft;
            float thick = travelled ? TRAIL_W + 3.5f : committed ? TRAIL_W + 4f : TRAIL_W + 0.5f;
            PenLine(a, delta, len, pen, thick, PEN_FILL, seed, open && !committed);
        }
        else
        {
            // Printed: one tiled dash run, straight and even.
            GameObject go = new GameObject($"Trail{from.id}_{to.id}", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(chart, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = a + delta * 0.5f;
            rt.sizeDelta = new Vector2(len, TRAIL_W + 2f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            Image img = go.AddComponent<Image>();
            img.sprite = Parchment.Stroke();
            img.type = Image.Type.Tiled;
            img.color = Fade(Parchment.InkSoft, 0.72f);
            img.raycastTarget = false;
            spawned.Add(go);
        }
    }

    // Hand-drawn line: short strokes along the run, each nudged sideways and rotated a little.
    // `pulse` registers the strokes for the breathing tick on live branches.
    private void PenLine(Vector2 a, Vector2 delta, float len, Color c, float thick, float fill, int seed, bool pulse)
    {
        Vector2 dir = delta / len;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float baseAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        int n = Mathf.Max(2, Mathf.RoundToInt(len / PEN_PERIOD));
        float seg = len / n;

        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) * seg;
            float wob = (Rand(seed, i) - 0.5f) * 2.6f;
            float rot = baseAng + (Rand(seed, i + 500) - 0.5f) * 7f;

            GameObject go = new GameObject("Pen", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(chart, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = a + dir * t + perp * wob;
            rt.sizeDelta = new Vector2(seg * fill, thick);
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);

            Image img = go.AddComponent<Image>();
            img.sprite = Parchment.Stroke();
            img.color = c;
            img.raycastTarget = false;
            spawned.Add(go);
            if (pulse) penMarks.Add(img);
        }
    }

    private static float Rand(int seed, int i)
    {
        unchecked
        {
            int h = seed * 374761393 + i * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    private void DrawNode(RunMap map, RunMapManager mgr, MapNode n, Vector2 p)
    {
        bool isCurrent = map.currentNodeId == n.id;
        bool visited = map.visited.Contains(n.id);
        bool reachable = map.CanTravelTo(n.id);
        bool committed = mgr != null && mgr.ChosenNextId == n.id;
        bool anyChosen = mgr != null && mgr.ChosenNextId >= 0;

        float size = MapGlyphs.SizeFor(n.type);
        float mark = size + 22f;

        GameObject go = new GameObject($"Node{n.id}", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(chart, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = p;
        rt.sizeDelta = new Vector2(mark + 10f, mark + 10f);
        spawned.Add(go);

        // A paper-coloured disc first, so the survey line and any trail behind the mark do not run
        // through the symbol. Invisible on paper — it only shows by what it hides.
        Image clear = AddImage(rt, "Clear", Parchment.Blot(), Fade(Parchment.Paper, 0.92f), false);
        clear.rectTransform.sizeDelta = new Vector2(mark + 4f, mark + 4f);

        // Printed: a light ink wash under the symbol, then the symbol. The wash is what makes a mark
        // look absorbed into the paper rather than laid on top of it.
        Image wash = AddImage(rt, "Wash", Parchment.Blot(), Fade(Parchment.InkPale, 0.20f), false);
        wash.rectTransform.sizeDelta = new Vector2(mark - 4f, mark - 4f);

        Image ring = AddImage(rt, "Ring", Parchment.InkRing(false), Fade(Parchment.Ink, 0.80f), false);
        ring.rectTransform.sizeDelta = new Vector2(mark, mark);

        // Visited marks fade — they are behind you. FUTURE marks (further off than the branches you
        // can take now) are pulled back too, so the live row is the strongest printed thing on the
        // sheet and the eye lands on the decision without being told where to look. Held at 0.72
        // rather than pushed further: every node is labelled because the screen exists for ROUTE
        // PLANNING, and a plan you have to squint at is not one.
        bool future = !visited && !isCurrent && !reachable;
        Color glyphCol = visited && !isCurrent ? Fade(Parchment.Ink, 0.45f)
                       : future ? Fade(Parchment.Ink, 0.72f)
                                : Parchment.Ink;
        ring.color = Fade(Parchment.Ink, future ? 0.58f : 0.80f);

        Image glyph = AddImage(rt, "Glyph", MapGlyphs.ForNode(n.type), glyphCol, false);
        glyph.rectTransform.sizeDelta = new Vector2(size, size);

        // --- the annotation, in red pen -----------------------------------------------------------
        //
        // ⚠️ THE RING SPRITE IS NOT ROTATIONALLY SYMMETRIC — its radius wanders and the nib lifts at
        // one point — so drawing the same sprite twice at different angles genuinely reads as two
        // separate hand-drawn circles. At the same angle it reads as one thick printed ring.
        Image penRing = null;
        if (isCurrent)
        {
            // Ringed twice, the way you'd circle where you are on a real map.
            Image r1 = AddImage(rt, "PenRing", Parchment.InkRing(true), Parchment.Red, false);
            r1.rectTransform.sizeDelta = new Vector2(mark + 12f, mark + 12f);
            Image r2 = AddImage(rt, "PenRing2", Parchment.InkRing(false), Fade(Parchment.Red, 0.75f), false);
            r2.rectTransform.sizeDelta = new Vector2(mark + 24f, mark + 24f);
            r2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 17f);
        }
        else if (committed)
        {
            // The chosen branch is scribbled over HARD: a red wash soaked into the paper under the
            // mark, two heavy rings at different angles, a faint outer sweep, and the label
            // underlined. It has to survive being one mark among twenty-odd on a busy sheet — the
            // old single ring differed from an unchosen branch by two pixels of radius and a
            // slightly deeper red, which is not a difference.
            Image soak = AddImage(rt, "PenSoak", Parchment.Blot(), Fade(Parchment.Red, 0.14f), false);
            soak.rectTransform.sizeDelta = new Vector2(mark + 20f, mark + 20f);
            soak.rectTransform.SetSiblingIndex(1);   // above the paper mask, under the printed mark

            Image r1 = AddImage(rt, "PenRing", Parchment.InkRing(true), Parchment.Red, false);
            r1.rectTransform.sizeDelta = new Vector2(mark + 13f, mark + 13f);
            r1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);

            Image r2 = AddImage(rt, "PenRing2", Parchment.InkRing(true), Parchment.Red, false);
            r2.rectTransform.sizeDelta = new Vector2(mark + 26f, mark + 26f);
            r2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 121f);

            Image r3 = AddImage(rt, "PenRing3", Parchment.InkRing(false), Fade(Parchment.Red, 0.45f), false);
            r3.rectTransform.sizeDelta = new Vector2(mark + 37f, mark + 37f);
            r3.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 244f);

            penRing = r1;
        }
        else if (reachable)
        {
            // ⚠️ ONCE A BRANCH IS CHOSEN THE RIVALS STEP BACK, and this is what finally made the
            // choice readable. Four siblings wearing the same red ring as the chosen one is why it
            // "wasn't clear which I picked" — the mark was fine, the CONTEXT was competing with it.
            // They stay faintly ringed rather than going bare, and hover brings one straight back to
            // full, so it is still obvious you may change your mind.
            bool sidelined = anyChosen;
            Image r1 = AddImage(rt, "PenRing", Parchment.InkRing(true),
                                sidelined ? Fade(Parchment.RedSoft, 0.34f) : Parchment.RedSoft, false);
            r1.rectTransform.sizeDelta = new Vector2(mark + 12f, mark + 12f);
            penRing = r1;
        }
        else if (visited)
        {
            Image r1 = AddImage(rt, "PenRing", Parchment.InkRing(false), Fade(Parchment.Red, 0.6f), false);
            r1.rectTransform.sizeDelta = new Vector2(mark + 10f, mark + 10f);
        }

        if (n.recharge != RechargeType.None)
        {
            float badge = 21f;
            RectTransform bt = AddPoint(rt, "Recharge", new Vector2(0.5f, 0.5f),
                new Vector2(mark * 0.46f, mark * 0.46f), new Vector2(badge + 12f, badge + 12f));

            Image disc = bt.gameObject.AddComponent<Image>();
            disc.sprite = Parchment.Blot();
            disc.color = Fade(Parchment.Paper, 0.95f);
            disc.raycastTarget = false;

            Image bring = AddImage(bt, "BadgeRing", Parchment.InkRing(false), Fade(Parchment.Ink, 0.75f), false);
            bring.rectTransform.sizeDelta = new Vector2(badge + 12f, badge + 12f);

            Image bm = AddImage(bt, "Mark", MapGlyphs.ForRecharge(n.recharge), Parchment.Ink, false);
            bm.rectTransform.sizeDelta = new Vector2(badge, badge);
        }

        TextMeshProUGUI labelText = AddLabel(rt, n, mark, isCurrent, visited, reachable,
                                             committed, anyChosen && reachable && !committed);

        // Only reachable nodes are clickable. Choosing is a commitment, not navigation: the click
        // sets the branch and the run travels there when the player reaches the exit door.
        if (reachable)
        {
            Button btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            // ⚠️ The hit target must cover the GROWN mark, not the resting one. Sized to the
            // resting +10 it would fall out from under the cursor at the edge of a hovered node,
            // firing exit/enter repeatedly and leaving the mark flickering.
            Image hit = AddImage(rt, "Hit", FlatUI.Pixel(), new Color(0f, 0f, 0f, 0f), true);
            hit.rectTransform.sizeDelta = new Vector2(mark + 34f, mark + 34f);
            btn.targetGraphic = hit;

            MapNodeMark hover = go.AddComponent<MapNodeMark>();
            hover.body = rt;
            hover.ring = penRing;
            hover.label = labelText;
            // Nothing breathes once a choice exists. Before you pick, the pulse is the screen
            // asking a question; after you pick it has been answered, and five marks still
            // twitching at you is exactly the competition that made the choice hard to see.
            hover.pulses = !committed && !anyChosen;
            hover.restCol = penRing != null ? penRing.color : Parchment.RedSoft;
            hover.hotCol = Parchment.Red;
            hover.labelRest = labelText != null ? labelText.color : Parchment.Red;
            hover.labelHot = Parchment.Red;

            int id = n.id;
            btn.onClick.AddListener(() =>
            {
                if (RunMapManager.instance == null || !RunMapManager.instance.ChooseNext(id)) return;
                // Opened by the exit: committing IS leaving, so go. Opened with M: this is planning,
                // so mark the branch and let the player keep reading the act.
                if (mustChoose) ConfirmChoice();
                else Refresh();
            });
        }
    }

    // ⚠️ EVERY node is labelled, not just the reachable ones. The whole point of showing the act is
    // planning a route through it, and you cannot plan through marks that have no names.
    //
    // The backing is PAPER-COLOURED, not a dark plate. On a light ground the mask can be the ground
    // itself, so it is invisible except for the dashes it hides — which is the whole trick the
    // etched version could not pull off, where the equivalent had to be a visible dark nameplate.
    private TextMeshProUGUI AddLabel(RectTransform parent, MapNode n, float mark,
                                     bool isCurrent, bool visited, bool reachable,
                                     bool committed, bool sidelined)
    {
        string text = isCurrent ? "YOU ARE HERE" : MapGlyphs.LabelFor(n.type);
        if (n.recharge != RechargeType.None && !isCurrent) text += "\n" + MapGlyphs.LabelFor(n.recharge);

        string[] parts = text.Split('\n');
        int widest = 0;
        foreach (string p in parts) if (p.Length > widest) widest = p.Length;

        float fs = reachable || isCurrent ? 13f : 12f;
        float y = -(mark * 0.5f + 6f);

        Image back = AddImage(parent, "LabelBack", Parchment.Blot(), Fade(Parchment.Paper, 0.88f), false);
        back.rectTransform.pivot = new Vector2(0.5f, 1f);
        back.rectTransform.anchoredPosition = new Vector2(0f, y + 7f);
        back.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(widest * fs * 0.82f + 26f, 66f, 190f),
                                                  14f + parts.Length * 16f);

        Color c = isCurrent || committed ? Parchment.Red
                : reachable ? (sidelined ? Fade(Parchment.Red, 0.48f) : Parchment.Red)
                : visited ? Fade(Parchment.Red, 0.72f)
                          : Parchment.InkSoft;

        TextMeshProUGUI label = AddText(parent, "Label", text, fs, c, TextAlignmentOptions.Top);
        // Pivot at the TOP so the offset places the text's first line, not its box centre. With a
        // centred pivot a 34px-tall box put its first line back on top of the symbol.
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        label.rectTransform.sizeDelta = new Vector2(150f, 36f);
        label.characterSpacing = 2f;
        label.lineSpacing = -12f;

        // The chosen branch gets its name underlined in the same pen as its rings. Cheap, and it
        // means the mark is identifiable from the label alone when the rings are behind the cursor.
        if (committed)
        {
            float w = Mathf.Clamp(widest * fs * 0.74f, 44f, 150f);
            Image rule = AddImage(parent, "LabelRule", Parchment.Stroke(), Parchment.Red, false);
            rule.type = Image.Type.Tiled;
            rule.rectTransform.sizeDelta = new Vector2(w, 4f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, y - 15f - (parts.Length - 1) * 16f);
            rule.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.1f);
        }

        return label;
    }

    // ---- small builders -------------------------------------------------------------------------

    private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color,
                                    TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
