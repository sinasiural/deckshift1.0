using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "First time here?" — asked once, on the very first PLAY, before the character select.
//
// THE OBJECT is a plank board hung on chains (SalvageScreen.BuildBoard), the pause screen's
// language, dropped over the main menu. Its two choices are CLONES of the menu's own PLAY plaque, not
// new buttons: the prompt is a question the menu is asking, so it should be made of the menu.
//
// Either answer is remembered (TutorialMode.MarkCompleted on skip, or on finishing the tutorial), so
// it never asks twice. Escape backs out without answering, and then it will ask again next PLAY.
public class FirstTimePrompt : MonoBehaviour
{
    private const float BoardWidth = 720f;
    private const float BoardTop = 250f;
    private const float BoardBottom = -230f;
    private const float DropTime = 0.5f;
    private const float DropFrom = 900f;     // canvas px above the resting position

    private static FirstTimePrompt open;

    private Action onTutorial, onSkip;
    private RectTransform board;
    private float t;
    private int openedFrame;

    public static bool IsOpen => open != null;

    /// <param name="template">the menu button to clone for the two choices (the PLAY plaque)</param>
    public static void Open(Button template, Action onTutorial, Action onSkip)
    {
        if (open != null) return;

        Canvas canvas = GameScreen.FindRootCanvas();
        if (canvas == null || template == null)
        {
            // Never leave PLAY dead: with nothing to draw the question on, just start the game.
            onSkip?.Invoke();
            return;
        }

        var go = new GameObject("FirstTimePrompt", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        SalvageScreen.Stretch((RectTransform)go.transform);
        var prompt = go.AddComponent<FirstTimePrompt>();
        prompt.onTutorial = onTutorial;
        prompt.onSkip = onSkip;
        prompt.Build(template);
        open = prompt;
    }

    private void Build(Button template)
    {
        // Dims the menu and swallows clicks meant for the buttons behind it.
        Image dim = SalvageScreen.Img(transform, "Dim", Salvage.Pixel(), new Color(0f, 0f, 0f, 0.6f), raycast: true);
        SalvageScreen.Stretch(dim.rectTransform);

        SalvageScreen.BuildBoard(transform, BoardWidth, BoardTop, BoardBottom, out board, out RectTransform printed);

        TextMeshProUGUI title = Text(printed, "Title", "FIRST TIME HERE?", false, TextRole.Title, Salvage.TextBright);
        Place(title.rectTransform, new Vector2(0f, 150f), new Vector2(620f, 70f));

        TextMeshProUGUI body = Text(printed, "Body",
            "Deckshift does not play like other platformers: every jump spends something you will not " +
            "get back. The tutorial takes about four minutes.",
            true, TextRole.Label, Salvage.TextBody);
        body.textWrappingMode = TextWrappingModes.Normal;
        Place(body.rectTransform, new Vector2(0f, 40f), new Vector2(560f, 130f));

        // Labels name the exact outcome (designer, 2026-09-24): "Show me how" / "Just play" were rejected
        // as vague. Skip marks the tutorial done and goes to the character select.
        Plaque(template, printed, "Play tutorial", new Vector2(-150f, -110f), () => Choose(onTutorial));
        Plaque(template, printed, "Skip tutorial", new Vector2(150f, -110f), () => Choose(onSkip));

        TextMeshProUGUI hint = Text(printed, "Hint", "ENTER  play tutorial       ESC  back", false, TextRole.Caption, Salvage.TextFaint);
        Place(hint.rectTransform, new Vector2(0f, -185f), new Vector2(560f, 30f));

        board.anchoredPosition = new Vector2(0f, BoardTop + DropFrom);
        openedFrame = Time.frameCount;
    }

    private void Update()
    {
        // The drop. Unscaled: nothing on the menu should depend on the game clock.
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / DropTime);
        board.anchoredPosition = new Vector2(0f, BoardTop + DropFrom * (1f - EaseOutBack(t)));

        // ⚠️ The key that pressed PLAY is still down on the first frame — without this guard an Enter
        // on PLAY would answer the question before it was drawn. Same trap CharacterSelectScreen guards.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Choose(onTutorial);
        else if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Choose(Action action)
    {
        Close();
        action?.Invoke();
    }

    private void Close()
    {
        if (open == this) open = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }

    // ---- builders ---------------------------------------------------------------------------------

    private static void Plaque(Button template, RectTransform parent, string label, Vector2 pos, Action onClick)
    {
        Button b = Instantiate(template, parent);
        b.name = "Choice_" + label;
        // ⚠️ A fresh event, not RemoveAllListeners: the clone carries the template's PERSISTENT PlayGame
        // call, which RemoveAllListeners does not touch — every choice would also start the game.
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(() => onClick());

        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(262f, rt.sizeDelta.y);   // wider than PLAY: longer labels
        // ⚠️ THE MENU'S PLAQUES ARE ROTATED 180° AND FLIPPED BACK BY A (-1.05) SCALE, which cancels
        // out on screen. Resetting only one of the two turns the label upside down (it did, on the
        // first build of this prompt). Reset both; the net picture is identical, minus the hack.
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.text = label;
            // The plaque is one Simple sprite with an ornament at each end and no 9-slice border, so it
            // cannot grow to fit a long label without smearing the ornaments. Keep the text between
            // them instead: inset the label and shrink the size just enough to fit — deterministically,
            // from a measurement, not with auto-size (which settles over several frames).
            RectTransform lrt = t.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            float inset = rt.sizeDelta.x * PlaqueInsetFraction;
            lrt.offsetMin = new Vector2(inset, 0f);
            lrt.offsetMax = new Vector2(-inset, 0f);
            t.enableAutoSizing = false;
            float room = rt.sizeDelta.x - 2f * inset;
            float needed = t.GetPreferredValues(label).x;
            if (needed > room) t.fontSize *= room / needed;
        }
    }

    // Clear of the end ornaments on the menu's plaque sprite (button2_0). A FRACTION of the width, not a
    // pixel count: the sprite is Simple, so the ornaments stretch with the plaque and each covers about
    // a quarter of it at any width. Measured at 1920x1080 — fixed insets of 34 and 52 px both left
    // "PLAY TUTORIAL" touching the arrow tips.
    private const float PlaqueInsetFraction = 0.30f;

    private static TextMeshProUGUI Text(Transform parent, string name, string content, bool prose, TextRole role, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (prose) UIType.ApplyProse(t, role);
        else UIType.Apply(t, role);
        t.text = content;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
