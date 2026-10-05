using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The boss reward — **THE BANNER**. The second Salvage screen.
///
/// ⚠️ WHAT THE OBJECT IS: the boss's own banner, cut down and hung, with the spoils pinned to it.
/// Under Salvage a screen is separated by WHAT IT IS MADE OF rather than by claiming a colour, and
/// three things separate this one from the pause screen without spending a hue:
///
///   pause  planks and iron, DROPS in from above, is HAULED AWAY, sheds falling dust
///   banner CLOTH, UNFURLS downward, STAYS (a trophy you claimed does not leave), no particles
///
/// ⚠️ THE CLOTH IS DELIBERATE, NOT LEFTOVER. `SalvageSurfaces.Sheet` was built for the pause screen
/// and rejected there — the designer's verdict was that planks were better, and the skill records it
/// as "good cloth and something else may want it". A banner is what wants it: cloth is exactly the
/// wrong material for a menu you read every session and exactly the right one for a thing you took
/// off a corpse once.
///
/// ⚠️ IT MAKES NO ASSUMPTION ABOUT WHEN IT FIRES. The caller decides which relics to offer and how
/// often — first kill only, every kill, or something else entirely. The designer has a run-structure
/// decision pending (2026-08-21), so nothing here encodes one.
/// </summary>
public class BossRewardScreen : GameScreen
{
    private static BossRewardScreen instance;

    // ⚠️ SIZED TO THE CONTENT, NOT THE SCREEN. The first pass was 760 tall with everything in the
    // upper half, and the bare strip underneath made the banner read as oversized rather than full
    // — the same fault the pause board had at 130px of dead space.
    private const float BannerW = 1040f;
    private const float BannerH = 620f;
    private const float RestY = -10f;

    private RectTransform content, banner;
    private CanvasGroup group;
    private SalvageScreen.Hang hang;
    private float bannerRestY;

    private readonly List<RelicData> offers = new List<RelicData>();
    private readonly List<RectTransform> plates = new List<RectTransform>();
    private readonly List<Image> pins = new List<Image>();
    private int index;
    private int lastShown = -1;
    private int openedFrame;
    private float inputLiveAt;        // nothing is accepted before the banner has unrolled
    private bool confirmReleased;     // the confirm key must be seen UP before a press counts
    private System.Action<RelicData> onTaken;

    // Long enough for the unfurl (0.45s) plus a beat to read what is on the cloth.
    private const float SettleWindow = 0.85f;

    private TextMeshProUGUI nameLabel, descLabel, hintLabel;

    /// <summary>
    /// Show the spoils. `pool` is whatever the caller decided to offer; a single entry is fine and
    /// an empty list is a no-op rather than an empty screen.
    /// </summary>
    public static void Open(IList<RelicData> pool, System.Action<RelicData> onTaken = null)
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[BossReward] nothing to offer — skipping the screen.");
            if (onTaken != null) onTaken(null);
            return;
        }

        // ⚠️ ADOPT-THEN-VERIFY. A domain reload clears statics while leaving the scene object alive,
        // so building unconditionally stacks a second banner. But it also wipes non-serialized
        // fields, so an adopted screen can be a husk with its lists empty and its children intact —
        // check it is really built before trusting it.
        if (instance == null)
        {
            Canvas canvas = FindRootCanvas();
            if (canvas == null) { Debug.LogWarning("[BossReward] no canvas."); return; }

            Transform found = canvas.transform.Find("BossRewardScreen");
            instance = found != null ? found.GetComponent<BossRewardScreen>() : null;

            if (instance == null)
            {
                var go = new GameObject("BossRewardScreen", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                instance = go.AddComponent<BossRewardScreen>();
            }
        }

        instance.Show(pool, onTaken);
    }

    public static bool IsOpen { get { return instance != null && instance.isOpen; } }

    private bool IsBuilt { get { return content != null && plates.Count > 0; } }

    private void Show(IList<RelicData> pool, System.Action<RelicData> taken)
    {
        onTaken = taken;
        offers.Clear();
        foreach (RelicData r in pool) if (r != null) offers.Add(r);
        if (offers.Count == 0) { if (onTaken != null) onTaken(null); return; }

        Build();

        index = 0;
        lastShown = -1;
        openedFrame = Time.frameCount;
        inputLiveAt = Time.unscaledTime + SettleWindow;
        confirmReleased = false;
        isOpen = true;

        AcquireDisplay();
        content.gameObject.SetActive(true);
        group.alpha = 1f;
        group.blocksRaycasts = true;

        // ⚠️ DELIBERATELY NOT hang.Release(). That is the pause board's arrival — a rigid thing
        // FALLING IN from off screen — and using it here made the banner drop away from its own
        // rail, which is the one place cloth is nailed. The banner is already hung; what arrives is
        // its LENGTH, unrolling downward. Knock() supplies the sway that unrolling would cause.
        hang.Knock(1.6f);
        StartCoroutine(Unfurl());
    }

    // The banner drops as a rolled bundle and opens downward. Cloth does not swing like a board, so
    // the height eases open on its own curve while Hang supplies the sway.
    private IEnumerator Unfurl()
    {
        const float dur = 0.45f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            banner.sizeDelta = new Vector2(BannerW, BannerH * Mathf.Lerp(0.06f, 1f, k));
            yield return null;
        }
        banner.sizeDelta = new Vector2(BannerW, BannerH);
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        group.blocksRaycasts = false;
        content.gameObject.SetActive(false);
        ReleaseDisplay();
    }

    private void Update()
    {
        TickUIPauseMemory();
        if (!isOpen || !IsBuilt) return;

        hang.Tick(banner, bannerRestY);

        // ⚠️⚠️ THIS SCREEN APPEARS WHILE THE PLAYER IS STILL PLAYING, AND THAT CHANGES THE INPUT
        // RULES COMPLETELY. Every other screen is opened BY the player pressing something, so the
        // one-frame `openedFrame` guard is enough. This one arrives on its own, a couple of seconds
        // after a boss dies — while they are still mashing jump and interact from the fight.
        //
        // Measured: the first build accepted Space and E as confirm, and a boss relic was taken
        // before the banner had finished unfurling, with the description never read. Three guards,
        // and all three are needed:
        //
        //   1. SPACE AND E ARE GONE. They are jump and interact. A confirm on an unprompted screen
        //      must never share a key with what the player was doing a second ago.
        //   2. A SETTLE WINDOW — no input at all until the banner has unrolled.
        //   3. A FRESH PRESS — the key must be seen UP once before a down counts, so a key already
        //      held when the banner arrives cannot confirm on release-and-retap.
        if (Time.frameCount > openedFrame && Time.unscaledTime >= inputLiveAt)
        {
            int prev = index;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) index++;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) index--;
            index = Mathf.Clamp(index, 0, offers.Count - 1);
            if (index != prev) { PlayMove(); hang.Knock(0.8f); }

            bool confirmDown = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
            if (!confirmDown) confirmReleased = true;          // seen up at least once
            if (confirmReleased && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Take();
        }

        // Derived from index every frame, never pushed at it — the pattern that had already rotted
        // once on the character select when a click handler set index without refreshing.
        if (index != lastShown) Refresh();
    }

    private void Refresh()
    {
        lastShown = index;
        RelicData r = offers[index];

        nameLabel.text = r.relicName;
        descLabel.text = r.description;

        for (int i = 0; i < plates.Count; i++)
        {
            bool sel = i == index;
            // Selection is LIGHT, not a plate: the chosen spoil is the one the torch is on. Salvage
            // allows exactly two accents and this is Torch — nothing new is spent.
            plates[i].localScale = Vector3.one * (sel ? 1.06f : 0.94f);
            if (i < pins.Count && pins[i] != null)
                pins[i].color = sel ? Salvage.Torch : Salvage.Lit(Salvage.Ramp("iron").Sample(0.35f), 0.7f);

            CanvasGroup cg = plates[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = sel ? 1f : 0.62f;
        }
    }

    private void Take()
    {
        if (!isOpen || offers.Count == 0) return;
        // The settle window guards the CLICK path too — a stray click carried over from gameplay is
        // just as capable of taking a relic nobody read as a stray keypress.
        if (Time.unscaledTime < inputLiveAt) return;

        RelicData chosen = offers[index];

        PlayConfirm();

        // Routed through TryGrantRelic so a full loadout raises the swap screen exactly as a chest
        // does. The banner hides first, or two screens fight over the display.
        Hide();

        if (RelicManager.instance != null)
            RelicManager.instance.TryGrantRelic(chosen, () => { if (onTaken != null) onTaken(chosen); });
        else if (onTaken != null) onTaken(chosen);
    }

    // ============================================================================================
    // BUILD
    // ============================================================================================

    private void Build()
    {
        if (IsBuilt && plates.Count == offers.Count) return;

        // A husk from a domain reload, or an offer count that changed: start clean.
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        plates.Clear(); pins.Clear();

        RectTransform self = GetComponent<RectTransform>();
        SalvageScreen.Stretch(self);

        content = SalvageScreen.Point(transform, "Content", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        SalvageScreen.Stretch(content);
        group = content.gameObject.AddComponent<CanvasGroup>();

        // The dungeon wall behind it — the room you are standing in, not an invented void.
        SalvageScreen.BuildWall(content);

        BuildBanner();
        BuildPlates();
        BuildText();

        // ⚠️ Hang.Tick WRITES anchoredPosition every frame, so the rest height must be the position
        // the banner actually hangs at — its TOP edge on the rail, since the pivot is (0.5, 1).
        // Passing the centre offset instead silently yanked it half a banner downward on frame one.
        bannerRestY = RestY + BannerH * 0.5f;
    }

    private void BuildBanner()
    {
        // The rail it hangs from. Built BEFORE the cloth so it draws behind the banner's top edge,
        // the same ordering the pause screen's chains need.
        Image rail = SalvageScreen.Img(content, "Rail", SalvageSurfaces.Chain(Salvage.Tex(40f)),
                                       Salvage.Lit(Salvage.Ramp("iron").Sample(0.30f), 0.85f));
        rail.type = Image.Type.Tiled;
        rail.rectTransform.anchorMin = rail.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rail.rectTransform.sizeDelta = new Vector2(BannerW + 120f, Salvage.Px(6f));
        rail.rectTransform.anchoredPosition = new Vector2(0f, RestY + BannerH * 0.5f + 10f);

        banner = SalvageScreen.Point(content, "Banner", new Vector2(0.5f, 0.5f),
                                     new Vector2(0f, RestY), new Vector2(BannerW, BannerH));
        // ⚠️ THE PIVOT IS WHERE IT HANGS FROM. A banner rotating about its centre reads as a card
        // spinning in space; about its rail it reads as cloth on a pole. Same lesson as the quest
        // slips' tack pivot and the pause board's.
        banner.pivot = new Vector2(0.5f, 1f);
        banner.anchoredPosition = new Vector2(0f, RestY + BannerH * 0.5f);

        Image cloth = SalvageScreen.Img(banner, "Cloth",
            SalvageSurfaces.Sheet(Salvage.Tex(BannerW), Salvage.Tex(BannerH), new[] { 0.06f, 0.5f, 0.94f }),
            Color.white);
        SalvageScreen.Stretch(cloth.rectTransform);

        // Three iron pegs across the rail, matching the sheet's own pin points — the cloth is drawn
        // gathered at those fractions, so a peg anywhere else reads as floating.
        foreach (float u in new[] { 0.06f, 0.5f, 0.94f })
        {
            Image peg = SalvageScreen.Img(banner, "Peg", SalvageSurfaces.Peg(),
                                          Salvage.Lit(Salvage.Ramp("iron").Sample(0.45f), 0.9f));
            peg.rectTransform.anchorMin = peg.rectTransform.anchorMax = new Vector2(u, 1f);
            peg.rectTransform.sizeDelta = new Vector2(Salvage.Px(9f), Salvage.Px(9f));
            peg.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        }
    }

    private void BuildPlates()
    {
        // Spread across the banner. Two is the expected count; the layout holds for one or three.
        int n = offers.Count;
        float span = BannerW * 0.66f;
        float step = n > 1 ? span / (n - 1) : 0f;
        float x0 = n > 1 ? -span * 0.5f : 0f;

        for (int i = 0; i < n; i++)
        {
            RelicData r = offers[i];

            RectTransform plate = SalvageScreen.Point(banner, "Spoil" + i, new Vector2(0.5f, 1f),
                                                      new Vector2(x0 + step * i, -BannerH * 0.30f),
                                                      new Vector2(300f, 250f));
            plate.gameObject.AddComponent<CanvasGroup>();
            // ⚠️ MUST be recorded, not just created. `IsBuilt` is `plates.Count > 0`, so forgetting
            // this leaves Update() returning early forever: no Refresh (the name and description
            // stay blank) and no sway, while the plates themselves render perfectly. It looked like
            // a layout problem and was a missing line.
            plates.Add(plate);

            // The pin holding this spoil to the cloth. It is also the selection light (see Refresh).
            Image pin = SalvageScreen.Img(plate, "Pin", SalvageSurfaces.Peg(), Color.white, true);
            pin.rectTransform.anchorMin = pin.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            pin.rectTransform.sizeDelta = new Vector2(Salvage.Px(10f), Salvage.Px(10f));
            pin.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            pins.Add(pin);

            // The relic's own icon at world scale. ⚠️ Salvage.SpritePPU keeps a 32px pack sprite at
            // the size it is in the game (~77 canvas px); blowing pack pixel art past ~2x stops it
            // reading as the thing it depicts, which is what wrecked Vigil's grime.
            if (r.relicArt != null)
            {
                Image icon = SalvageScreen.Img(plate, "Icon", r.relicArt, Color.white);
                float s = Salvage.Px(r.relicArt.rect.width) * 2.6f;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                icon.rectTransform.sizeDelta = new Vector2(s, s);
                icon.rectTransform.anchoredPosition = new Vector2(0f, -78f);
            }

            // The name on the cloth. Ink on linen, not a light-on-dark label — this is the one
            // Salvage screen whose ground is pale, so the value structure inverts with it.
            TextMeshProUGUI cap = AddLabel(plate, "Cap", r.relicName, TextRole.Label, ChalkOnCloth());
            cap.rectTransform.anchorMin = new Vector2(0f, 1f);
            cap.rectTransform.anchorMax = new Vector2(1f, 1f);
            cap.rectTransform.pivot = new Vector2(0.5f, 1f);
            cap.rectTransform.sizeDelta = new Vector2(0f, 44f);
            cap.rectTransform.anchoredPosition = new Vector2(0f, -196f);

            int captured = i;
            AddClick(plate, () =>
            {
                if (index == captured) { Take(); return; }
                index = captured;
                PlayMove();
                hang.Knock(0.8f);
            });
        }
    }

    private void BuildText()
    {
        // The banner is the boss's, so the heading is what happened to the boss — not "REWARD",
        // which is a menu word and would say the same thing on any screen in any game.
        TextMeshProUGUI title = AddLabel(banner, "Title", "SPOILS", TextRole.Title, ChalkOnCloth());
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 62f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -46f);

        // Torch at full value — it is the accent AND the only warm thing on the cloth, so dimming it
        // to 0.55 (as the first pass did) put it at 0.39 luminance against a 0.185 ground.
        nameLabel = AddLabel(banner, "Name", "", TextRole.Heading, Salvage.Torch);
        nameLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        nameLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameLabel.rectTransform.sizeDelta = new Vector2(0f, 44f);
        nameLabel.rectTransform.anchoredPosition = new Vector2(0f, 148f);

        // A real sentence, so the PROSE face — the display face has essentially no lowercase and
        // renders a description as a wall of capitals.
        descLabel = AddLabel(banner, "Desc", "", TextRole.Body, ChalkOnCloth(0.82f));
        UIType.ApplyProse(descLabel, TextRole.Body);
        descLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        descLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        descLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        descLabel.rectTransform.sizeDelta = new Vector2(BannerW * 0.68f, 70f);
        descLabel.rectTransform.anchoredPosition = new Vector2(0f, 80f);

        // ⚠️ NO ARROW GLYPHS. CCBattleScarred is a display face with no guarantee of carrying "←"
        // or "→", and a missing glyph renders as a blank or a box — the character select shipped
        // that once. Words, or a drawn sprite; never a typed arrow.
        hintLabel = AddLabel(banner, "Hint", "LEFT / RIGHT  CHOOSE       ENTER  TAKE IT",
                             TextRole.Caption, ChalkOnCloth(0.50f));
        hintLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        hintLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        hintLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        hintLabel.rectTransform.sizeDelta = new Vector2(0f, 30f);
        hintLabel.rectTransform.anchoredPosition = new Vector2(0f, 38f);
    }

    /// <summary>
    /// ⚠️ THE CLOTH IS DARK, NOT PALE — measured, not assumed, and the first pass got it backwards.
    ///
    /// The linen RAMP samples 0.57–0.62, so "linen is pale like the quest board's paper" is a
    /// perfectly reasonable inference and it is wrong: `SalvageSurfaces.Sheet` shades that ramp down
    /// hard, and the rendered cloth measures **luminance 0.185**. Dark ink on it came out at 0.226 —
    /// four hundredths above its own background, i.e. invisible.
    ///
    /// So this screen is NOT the value inversion the quest board is. It is an ordinary dark ground
    /// and takes light text. Chalk at 0.90 gives about 5:1 against the cloth.
    /// </summary>
    private static Color ChalkOnCloth(float strength = 1f)
    {
        Color c = Salvage.Chalk;
        return new Color(c.r, c.g, c.b, strength);
    }

    private static void AddClick(RectTransform target, System.Action onClick)
    {
        Image hit = target.gameObject.GetComponent<Image>();
        if (hit == null) hit = target.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        Button b = target.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
    }
}
