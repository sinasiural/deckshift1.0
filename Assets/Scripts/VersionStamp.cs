using TMPro;
using UnityEngine;

/// <summary>
/// "DEMO v0.1" in the main menu's bottom-right corner, with the bug-report key beside it.
///
/// The version exists so a report can say which build it came from — without it, "the door didn't
/// open" from a friend could be about a build three fixes ago. It reads Application.version, which
/// is Player Settings → Version, so bumping that one field is the whole job for a new build.
///
/// Main menu only: in a run the corner belongs to the HUD, and the pause board already carries the
/// run's own readout.
/// </summary>
public static class VersionStamp
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneBootstrap.Register(Create); }

    private static void Create()
    {
        if (Object.FindFirstObjectByType<MainMenuController>() == null) return;

        Canvas canvas = GameScreen.FindRootCanvas();
        if (canvas == null || canvas.transform.Find("VersionStamp") != null) return;

        GameObject go = new GameObject("VersionStamp", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = (RectTransform)go.transform;
        // ⚠️ Anchored to the corner it sits in: the canvas width flexes with the aspect ratio.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 22f);
        rt.sizeDelta = new Vector2(700f, 26f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        UIType.Apply(t, TextRole.Caption);
        t.text = "DEMO  V" + Application.version + "     F8  BUG REPORT";
        t.alignment = TextAlignmentOptions.BottomRight;
        t.color = new Color(Salvage.TextMuted.r, Salvage.TextMuted.g, Salvage.TextMuted.b, 0.85f);
        t.characterSpacing = 5f;
        t.raycastTarget = false;
    }
}
