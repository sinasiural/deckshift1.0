using System;
using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// F8, anywhere in the game: save a bug report a playtester can send back.
///
/// Each press writes one folder under <c>Documents/Deckshift Bug Reports/</c>:
///   · screenshot.png — the frame as it was when F8 went down (taken BEFORE the toast appears)
///   · report.txt     — a space at the top for the player to say what happened, then the machine,
///                      the run (character, floor, HP, Shift, deck, relics) and the last few dozen
///                      things that happened
///   · Player.log     — Unity's own log for this session, which carries any exceptions
///
/// ⚠️ WHY DOCUMENTS AND NOT THE LOG FOLDER: Unity's log lives in AppData\LocalLow\…, a hidden folder
/// a friend will never find. The whole point is that the person sending it can find it.
///
/// ⚠️ SCENE-LOCAL, via SceneBootstrap like every other self-bootstrapping piece here. The
/// confirmation toast goes on the scene's root canvas, raised above whatever screen is open.
/// </summary>
public class BugReport : MonoBehaviour
{
    public const KeyCode Key = KeyCode.F8;

    private static BugReport instance;

    public static string ReportsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Deckshift Bug Reports");

    private CanvasGroup toastGroup;
    private TextMeshProUGUI toastText;
    private float lastPress = -10f;
    private Coroutine toastRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneBootstrap.Register(Create); }

    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("BugReport");
        instance = go.AddComponent<BugReport>();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void Update()
    {
        // Unscaled: every menu in the game pauses time, and a report taken from a menu is still wanted.
        if (!Input.GetKeyDown(Key) || Time.unscaledTime - lastPress < 1f) return;
        lastPress = Time.unscaledTime;
        StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        string folder;
        try
        {
            folder = Path.Combine(ReportsFolder, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(folder);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BugReport] could not create the report folder: " + e.Message);
            ShowToast("COULD NOT SAVE A BUG REPORT");
            yield break;
        }

        // The screenshot is written at the end of THIS frame, so the toast must not exist until the
        // next one — or every report would be a picture of the words "bug report saved".
        try { ScreenCapture.CaptureScreenshot(Path.Combine(folder, "screenshot.png")); }
        catch (Exception e) { Debug.LogWarning("[BugReport] screenshot failed: " + e.Message); }
        yield return new WaitForEndOfFrame();
        yield return null;

        try { File.WriteAllText(Path.Combine(folder, "report.txt"), BuildReport(), Encoding.UTF8); }
        catch (Exception e) { Debug.LogWarning("[BugReport] report.txt failed: " + e.Message); }

#if !UNITY_EDITOR
        // ⚠️ The log is open for writing by the player itself, so it must be opened with a sharing
        // mode that tolerates that; a plain File.Copy fails on a locked file.
        try
        {
            string log = Application.consoleLogPath;
            if (!string.IsNullOrEmpty(log) && File.Exists(log))
            {
                using (var src = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dst = File.Create(Path.Combine(folder, "Player.log")))
                    src.CopyTo(dst);
            }
        }
        catch (Exception e) { Debug.LogWarning("[BugReport] log copy failed: " + e.Message); }
#endif

        ShowToast("BUG REPORT SAVED TO  DOCUMENTS \\ DECKSHIFT BUG REPORTS");
    }

    // ---- the report ------------------------------------------------------------------------------

    private static string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("DECKSHIFT BUG REPORT");
        sb.AppendLine("Version " + Application.version + "   ·   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();
        sb.AppendLine("=== WHAT HAPPENED? (please write here before sending) ===");
        sb.AppendLine("What were you doing?");
        sb.AppendLine();
        sb.AppendLine("What did you expect to happen?");
        sb.AppendLine();
        sb.AppendLine("What happened instead?");
        sb.AppendLine();
        sb.AppendLine();

        sb.AppendLine("=== MACHINE ===");
        sb.AppendLine("OS:         " + SystemInfo.operatingSystem);
        sb.AppendLine("CPU:        " + SystemInfo.processorType + " (" + SystemInfo.processorCount + " threads)");
        sb.AppendLine("RAM:        " + SystemInfo.systemMemorySize + " MB");
        sb.AppendLine("GPU:        " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsMemorySize + " MB, " + SystemInfo.graphicsDeviceType + ")");
        sb.AppendLine("Screen:     " + Screen.width + "x" + Screen.height + " @ " + Screen.currentResolution.refreshRateRatio.value.ToString("0") + "Hz, " + Screen.fullScreenMode);
        sb.AppendLine("Scene:      " + SceneManager.GetActiveScene().name);
        sb.AppendLine();

        AppendRun(sb);

        sb.AppendLine("=== LAST EVENTS (newest last) ===");
        foreach (string line in RunStats.Trail) sb.AppendLine(line);
        return sb.ToString();
    }

    private static void AppendRun(StringBuilder sb)
    {
        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        if (p == null) { sb.AppendLine("=== RUN ===\n(not in a run)\n"); return; }

        sb.AppendLine("=== RUN ===");
        sb.AppendLine("Character:  " + (CharacterSelection.Chosen != null ? CharacterSelection.Chosen.characterName : "?"));

        RunMapManager map = RunMapManager.instance;
        MapNode node = map != null ? map.CurrentNode : null;
        if (node != null && map.Map != null)
            sb.AppendLine("Floor:      " + node.floor + " / " + (map.Map.floors - 1) + "  (" + node.type + ")");

        PlayerHealth ph = p.GetComponent<PlayerHealth>();
        if (ph != null)
            sb.AppendLine("HP:         " + Mathf.CeilToInt(ph.CurrentHealth) + " / " + Mathf.CeilToInt(ph.MaxHealth)
                          + (ph.Armour > 0f ? "  (+" + Mathf.CeilToInt(ph.Armour) + " armour)" : ""));
        sb.AppendLine("Shift:      " + p.GetCurrentShift() + " / " + p.maxShift);
        sb.AppendLine("Gold/Scrap: " + p.currentGold + " / " + p.currentScrap);
        sb.AppendLine("Play time:  " + RunStats.FormatTime(RunStats.PlaySeconds)
                      + "   rooms " + RunStats.RoomsCleared + "   kills " + RunStats.Kills
                      + "   cards played " + RunStats.CardsPlayed);

        DeckManager dm = DeckManager.instance;
        if (dm != null)
        {
            sb.AppendLine("Recall cost: " + dm.currentRecallCost);
            AppendPile(sb, "Hand", dm.GetCurrentHand());
            AppendPile(sb, "Draw", dm.GetDrawPile());
            AppendPile(sb, "Discard", dm.GetDiscardPile());
            AppendPile(sb, "Exhaust", dm.GetExhaustPile());
        }

        RelicManager rm = RelicManager.instance;
        if (rm != null)
        {
            sb.Append("Relics:     ");
            for (int i = 0; i < rm.OwnedRelics.Count; i++)
                sb.Append((i > 0 ? ", " : "") + (rm.OwnedRelics[i] != null ? rm.OwnedRelics[i].relicName : "?"));
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private static void AppendPile(StringBuilder sb, string label, System.Collections.Generic.List<RuntimeCard> pile)
    {
        sb.Append((label + ":").PadRight(12));
        if (pile == null || pile.Count == 0) { sb.AppendLine("-"); return; }
        for (int i = 0; i < pile.Count; i++)
        {
            RuntimeCard c = pile[i];
            if (c == null || c.cardData == null) continue;
            sb.Append((i > 0 ? ", " : "") + c.cardData.cardName + " ["
                      + (c.isInfinite ? "inf" : c.currentUses.ToString()) + "]"
                      + (c.enhancement != CardEnhancement.None ? " {" + c.enhancement + "}" : ""));
        }
        sb.AppendLine();
    }

    // ---- the toast -------------------------------------------------------------------------------

    // ⚠️ ON THE SCENE'S OWN CANVAS, NOT A NEW ONE. A second root overlay canvas would be a candidate
    // for GameScreen.FindRootCanvas — the lookup every procedural screen uses to decide where to
    // build itself — and whichever canvas it happened to return first would win. Built lazily at
    // show time and raised to the top, so it draws over whatever screen is open.
    private bool EnsureToast()
    {
        if (toastGroup != null) { toastGroup.transform.SetAsLastSibling(); return true; }

        Canvas canvas = GameScreen.FindRootCanvas();
        if (canvas == null) return false;

        GameObject t = new GameObject("BugReportToast", typeof(RectTransform));
        t.transform.SetParent(canvas.transform, false);
        t.transform.SetAsLastSibling();
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -150f);
        rt.sizeDelta = new Vector2(1100f, 52f);
        toastGroup = t.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;

        Image plate = t.AddComponent<Image>();
        plate.color = new Color(0.03f, 0.025f, 0.02f, 0.82f);
        plate.raycastTarget = false;

        GameObject txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(t.transform, false);
        RectTransform trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        toastText = txt.AddComponent<TextMeshProUGUI>();
        UIType.Apply(toastText, TextRole.Caption);
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.color = Salvage.Chalk;
        toastText.characterSpacing = 4f;
        toastText.raycastTarget = false;
        return true;
    }

    private void ShowToast(string message)
    {
        if (!EnsureToast()) return;
        toastText.text = message;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; toastGroup.alpha = t / 0.15f; yield return null; }
        toastGroup.alpha = 1f;
        float until = Time.unscaledTime + 3.2f;
        while (Time.unscaledTime < until) yield return null;
        t = 0f;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; toastGroup.alpha = 1f - t / 0.5f; yield return null; }
        toastGroup.alpha = 0f;
        toastRoutine = null;
    }
}
