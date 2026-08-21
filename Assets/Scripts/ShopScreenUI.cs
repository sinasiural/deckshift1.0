using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

// Fully code-built shop — no image assets, no AI background. A cozy "merchant stall" themed
// to match the in-world shop (blue-grey striped awning, warm wood shelves, price plaques).
// Self-instantiating singleton under the root Canvas, same house pattern as RelicManagePanel.
// Driven by ShopManager, populated from the Shopkeeper's inventory + ShopManager's services.
public class ShopScreenUI : MonoBehaviour
{
    public static ShopScreenUI instance;

    private CanvasGroup group;
    private RectTransform window;
    private Transform cardsRow, relicsRow, servicesRow;
    private TMP_Text goldText, barkerText;
    private Image goldFlash;

    // The keeper: portrait at the counter, speech bubble beside him.
    private RectTransform keeperPortrait, bubble;
    private Image portraitImage;
    private CanvasGroup bubbleGroup;
    private Coroutine talkCo, moodCo;
    private string lastLine = "";
    private RectTransform tooltip;
    private TMP_Text tooltipText;
    private TMP_FontAsset font;
    private bool isOpen;
    private GameObject cachedHud;
    private Shopkeeper shop;
    private float fillScale = 1f;   // window scaled up to nearly fill the canvas (computed per open)
    private RectTransform canvasRT;

    // True while the stall is up. PauseMenu checks this so ESC closes the shop, not the pause menu.
    public static bool IsOpen => instance != null && instance.isOpen;

    private readonly List<Offer> offers = new List<Offer>();
    private readonly List<Tile> tiles = new List<Tile>();

    // Height grew from 680 to give the keeper his own band along the bottom — at 680 his portrait
    // clipped the relic tiles above him by a few pixels.
    private const float WIN_W = 1060f, WIN_H = 726f;
    private const float TILE_W = 186f, TILE_H = 190f;
    private const float SIDE_PAD = 28f;
    private const float ROW_SPACING = 18f;
    private const float ROW1_Y = -144f;

    // Warm-wood + steel-canopy palette, pulled from the in-world stall.
    private static readonly Color Stone      = new Color(0.04f, 0.05f, 0.07f, 0.90f);
    private static readonly Color WoodDark   = new Color(0.115f, 0.09f, 0.07f, 0.99f);
    private static readonly Color WoodBand   = new Color(0.22f, 0.17f, 0.12f, 1f);
    private static readonly Color WoodFrame  = new Color(0.86f, 0.64f, 0.34f, 1f);
    private static readonly Color TileBg     = new Color(0.30f, 0.24f, 0.18f, 1f);
    private static readonly Color ShelfWood  = new Color(0.36f, 0.27f, 0.18f, 1f);
    private static readonly Color PlaqueBg   = new Color(0.11f, 0.09f, 0.07f, 1f);
    private static readonly Color GoldCoin   = new Color(1f, 0.82f, 0.32f, 1f);
    private static readonly Color Cream      = new Color(0.90f, 0.86f, 0.74f);
    private static readonly Color AwningBlue = new Color(0.52f, 0.60f, 0.66f, 1f);

    // THE KEEPER TALKS BACK.
    //
    // These used to be one array, with a single line picked when the stall opened — decoration
    // that never changed no matter what you did. Splitting them by EVENT is what turns the shop
    // from a menu with a joke on it into someone standing behind a counter: he greets you, he
    // comments on what you're looking at, he thanks you, and he needles you when you can't pay.
    //
    // Keep them short. They're read in a speech bubble while the player's attention is on prices.
    private static readonly string[] Greetings =
    {
        "Everything's a steal. Some of it literally.",
        "Buy somethin' or admire the ambiance elsewhere.",
        "Prices set by a very reasonable goblin.",
        "Fresh scrap, barely cursed.",
        "Ah. A customer with money-shaped pockets.",
    };
    private static readonly string[] BrowseCard =
    {
        "Good eye. That one bites.",
        "Cheap for what it does. Don't ask what it does.",
        "Previous owner loved it. Right up 'til the end.",
    };
    private static readonly string[] BrowseRelic =
    {
        "Ohh, that one's got history.",
        "Hold it up to the light. Go on.",
        "Found it in a wall. Don't ask whose.",
    };
    private static readonly string[] BrowseService =
    {
        "Honest work, honest price. One of those is true.",
        "You look like you could use a favour.",
    };
    private static readonly string[] TooPoor =
    {
        "Come back when your purse rattles louder.",
        "That's a looking price. Buying price is the same.",
        "No coin, no cargo.",
    };
    private static readonly string[] Bought =
    {
        "Pleasure doing business.",
        "No refunds. No refunds. No... refunds.",
        "You break it, you bought it. You bought it? Also bought it.",
        "Spent well. Probably.",
    };
    private static readonly string[] AlreadySold =
    {
        "Gone. You snoozed.",
        "Sold. To someone quicker.",
    };
    private static readonly string[] Farewells =
    {
        "Mind the step.",
        "Don't die out there. Bad for repeat business.",
        "Come back richer.",
    };

    private class Offer
    {
        public ShopItemType type;
        public CardData card;
        public RelicData relic;
        public ShopSlotData slot;   // card/relic persist their sold flag here; null for services
        public string name, desc;
        public int price;
        public System.Action onService;
        public bool Sold => slot != null && slot.isSold;
    }

    private class Tile
    {
        public Offer offer;
        public RectTransform root;
        public GameObject soldStamp;
        public Image tileBg;
        public TMP_Text priceLabel;
        public CanvasGroup cg;
        public Button buyBtn;
        public Coroutine hoverCo;
        public bool topRow;
    }

    // ---- entry ----
    public static void Open(Shopkeeper shopkeeper)
    {
        EnsureInstance();
        if (instance != null) instance.Show(shopkeeper);
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("ShopScreenUI: no Canvas found in scene."); return; }
        GameObject go = new GameObject("ShopScreenUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<ShopScreenUI>();
        instance.Build();
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

    private void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape)) Hide();
        if (isOpen) RefreshAffordability();
        if (isOpen) TickKeeperIdle();
    }

    // ---- construction ----
    private void Build()
    {
        font = ResolveFont();
        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) canvasRT = c.rootCanvas.GetComponent<RectTransform>();

        // Dim stone backdrop; click outside window closes.
        Image backdrop = AddImage(transform, "Backdrop", null, Stone, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        // Window (the stall).
        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = PixelUI.Grain(); winBg.type = Image.Type.Tiled; winBg.color = WoodDark;
        winBg.raycastTarget = true;
        Image winFrame = AddImage(window, "Frame", PixelUI.Frame(), WoodFrame, false);
        winFrame.type = Image.Type.Sliced; Stretch(winFrame.rectTransform);

        BuildAwning();
        BuildHeaderBand();

        // Shop name. The barker line moved out of the header and down to the counter, next to the
        // keeper's face — a line of italic text under a title is a caption; a line coming out of
        // someone's mouth is a person.
        AddText(window, "Title", new Vector2(0f, 1f), new Vector2(SIDE_PAD + 6f, -60f), new Vector2(620f, 44f),
            "THE MARKETPLACE", 32f, FontStyles.Bold, new Color(0.98f, 0.90f, 0.64f), TextAlignmentOptions.Left);

        BuildGoldBox();

        // ---- Row 1: CARDS (full width) ----
        AddText(window, "CardsLabel", new Vector2(0f, 1f), new Vector2(SIDE_PAD, ROW1_Y + 24f), new Vector2(300f, 26f),
            "CARDS", 19f, FontStyles.Bold, Cream, TextAlignmentOptions.Left);
        cardsRow = BuildRow("CardsRow", new Vector2(0.5f, 1f), new Vector2(0f, ROW1_Y), WIN_W - SIDE_PAD * 2f, TextAnchor.UpperCenter);
        BuildShelf(new Vector2(0.5f, 1f), new Vector2(0f, ROW1_Y - TILE_H - 4f), WIN_W - SIDE_PAD * 2f);

        // ---- Row 2: RELICS (left) | SERVICES (right), clearly separated ----
        float relicsW = 3f * TILE_W + 2f * ROW_SPACING;
        float servicesW = 2f * TILE_W + ROW_SPACING;
        float row2Y = ROW1_Y - TILE_H - 40f;

        AddText(window, "RelicsLabel", new Vector2(0f, 1f), new Vector2(SIDE_PAD, row2Y + 24f), new Vector2(300f, 26f),
            "RELICS", 19f, FontStyles.Bold, Cream, TextAlignmentOptions.Left);
        relicsRow = BuildRow("RelicsRow", new Vector2(0f, 1f), new Vector2(SIDE_PAD, row2Y), relicsW, TextAnchor.UpperLeft);
        BuildShelf(new Vector2(0f, 1f), new Vector2(SIDE_PAD, row2Y - TILE_H - 4f), relicsW, false);

        AddText(window, "ServicesLabel", new Vector2(1f, 1f), new Vector2(-SIDE_PAD, row2Y + 24f), new Vector2(300f, 26f),
            "SERVICES", 19f, FontStyles.Bold, Cream, TextAlignmentOptions.Right);
        servicesRow = BuildRow("ServicesRow", new Vector2(1f, 1f), new Vector2(-SIDE_PAD, row2Y), servicesW, TextAnchor.UpperRight);
        BuildShelf(new Vector2(1f, 1f), new Vector2(-SIDE_PAD, row2Y - TILE_H - 4f), servicesW, false);

        // Vertical divider between the two segments.
        Image div = AddImage(window, "Divider", FlatUI.Pixel(), new Color(1f, 1f, 1f, 0.10f), false);
        RectTransform drt = div.rectTransform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f); drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(2f, TILE_H + 40f);
        drt.anchoredPosition = new Vector2((SIDE_PAD + relicsW) - WIN_W * 0.5f + 10f, row2Y + 30f);

        // The keeper leans on the counter along the bottom, where the stall already had dead space.
        BuildKeeper();

        // Leave button. Hovering it gets a farewell — said on the way out, where it's actually
        // read, rather than on Hide() where the screen is already gone.
        GameObject leave = BuildButton(window, "Leave", new Vector2(1f, 0f), new Vector2(-SIDE_PAD, 26f),
            new Vector2(210f, 52f), new Color(0.80f, 0.60f, 0.28f), "LEAVE  (Esc)", Hide);
        if (leave != null)
        {
            ShopTileHover bye = leave.AddComponent<ShopTileHover>();
            bye.onEnter = () => Say(Farewells, Mood.Nod);
        }

        BuildTooltip();

        // Lamplit dust drifting through the stall. Warm, slow and very faint — a shop is a place
        // with air in it, and stillness is what made the panel feel like a menu.
        UIEmberField.Attach(window, 20, new Color(1f, 0.88f, 0.66f, 1f), UIEmberField.Settings.Dust);

        gameObject.SetActive(false);
    }

    // ---- the keeper ----------------------------------------------------------------------------

    // Portrait + speech bubble along the bottom-left of the stall, the one area the tile grid
    // never used. Reads as him leaning on the counter while you browse.
    private void BuildKeeper()
    {
        const float PORTRAIT = 132f;

        keeperPortrait = AddPoint(window, "Keeper", new Vector2(0f, 0f), new Vector2(SIDE_PAD + 6f, 18f),
            new Vector2(PORTRAIT, PORTRAIT));
        keeperPortrait.pivot = new Vector2(0f, 0f);

        // Warm pool of lamplight behind him so he sits in the scene rather than on it.
        Image lamp = AddImage(keeperPortrait, "Lamp", FlatUI.SoftGlow(), new Color(1f, 0.78f, 0.42f, 0.16f), false);
        lamp.rectTransform.anchorMin = lamp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        lamp.rectTransform.sizeDelta = new Vector2(PORTRAIT * 1.9f, PORTRAIT * 1.9f);

        portraitImage = AddImage(keeperPortrait, "Face", null, Color.white, false);
        Stretch(portraitImage.rectTransform);
        portraitImage.preserveAspect = true;

        // Speech bubble to his right.
        bubble = AddPoint(window, "Bubble", new Vector2(0f, 0f), new Vector2(SIDE_PAD + PORTRAIT + 24f, 46f),
            new Vector2(540f, 78f));
        bubble.pivot = new Vector2(0f, 0f);

        Image bg = bubble.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(6);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.09f, 0.075f, 0.06f, 0.96f);
        bg.raycastTarget = false;

        Image ol = AddImage(bubble, "Outline", FlatUI.Outline(6, 2), new Color(0.42f, 0.32f, 0.20f, 1f), false);
        ol.type = Image.Type.Sliced;
        Stretch(ol.rectTransform);
        FlatUI.ApplySliceThickness(ol, 2f);

        // Little tail pointing back at his mouth, so the words are clearly HIS.
        Image tail = AddImage(bubble, "Tail", FlatUI.Pixel(), new Color(0.42f, 0.32f, 0.20f, 1f), false);
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        tail.rectTransform.pivot = new Vector2(1f, 0.5f);
        tail.rectTransform.sizeDelta = new Vector2(18f, 3f);
        tail.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        barkerText = AddText(bubble, "Barker", new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(508f, 52f),
            "", 17f, FontStyles.Italic, new Color(0.88f, 0.82f, 0.68f), TextAlignmentOptions.TopLeft);
        barkerText.enableWordWrapping = true;

        bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
    }

    // What the keeper is doing with his body. Kept tiny on purpose — a portrait that lurches
    // around pulls focus off the prices, which is what the player is actually here to read.
    private enum Mood { Idle, Lean, Nod, Slump }

    private void Say(string[] pool, Mood mood = Mood.Idle)
    {
        if (barkerText == null || pool == null || pool.Length == 0) return;

        // Never repeat the previous line back-to-back — repetition is what makes barks read as
        // canned. With small pools a plain Random.Range does it constantly.
        string line = pool[Random.Range(0, pool.Length)];
        if (pool.Length > 1 && line == lastLine)
            line = pool[(System.Array.IndexOf(pool, line) + 1) % pool.Length];
        lastLine = line;

        if (talkCo != null) StopCoroutine(talkCo);
        talkCo = StartCoroutine(TypeLine(line));

        if (moodCo != null) StopCoroutine(moodCo);
        moodCo = StartCoroutine(PlayMood(mood));
    }

    // Typewriter. Speech that appears a character at a time reads as SAID; a line that snaps in
    // whole reads as a label that changed.
    private IEnumerator TypeLine(string line)
    {
        if (bubbleGroup != null) bubbleGroup.alpha = 1f;
        barkerText.text = "";

        const float perChar = 0.018f;
        for (int i = 1; i <= line.Length; i++)
        {
            barkerText.text = line.Substring(0, i);
            float t = 0f;
            while (t < perChar) { t += Time.unscaledDeltaTime; yield return null; }
        }
        talkCo = null;
    }

    private IEnumerator PlayMood(Mood mood)
    {
        if (keeperPortrait == null) yield break;

        Vector2 home = new Vector2(SIDE_PAD + 6f, 18f);
        float dur = 0.5f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float n = Mathf.Clamp01(t / dur);
            float e = Mathf.Sin(n * Mathf.PI);          // out and back
            Vector2 off = Vector2.zero;
            float scale = 1f;

            switch (mood)
            {
                case Mood.Lean:  off = new Vector2(10f * e, 4f * e); scale = 1f + 0.05f * e; break;
                case Mood.Nod:   off = new Vector2(0f, -9f * e); break;
                case Mood.Slump: off = new Vector2(0f, -6f * e); scale = 1f - 0.04f * e; break;
            }

            keeperPortrait.anchoredPosition = home + off;
            keeperPortrait.localScale = Vector3.one * scale;
            yield return null;
        }
        keeperPortrait.anchoredPosition = home;
        keeperPortrait.localScale = Vector3.one;
        moodCo = null;
    }

    // Slow idle breathing. Skipped while a mood reaction owns the transform, or the two would
    // fight over anchoredPosition and the reaction would stutter.
    private void TickKeeperIdle()
    {
        if (keeperPortrait == null || moodCo != null) return;
        float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * 2.2f;
        keeperPortrait.anchoredPosition = new Vector2(SIDE_PAD + 6f, 18f + bob);
    }

    private void BuildAwning()
    {
        RectTransform awn = AddPoint(window, "Awning", new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(WIN_W - 14f, 34f));
        awn.pivot = new Vector2(0.5f, 1f);
        int stripes = 16;
        float sw = (WIN_W - 14f) / stripes;
        for (int i = 0; i < stripes; i++)
        {
            Image s = AddImage(awn, $"Stripe{i}", FlatUI.Pixel(), (i % 2 == 0) ? AwningBlue : Cream, false);
            RectTransform rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(sw, 34f);
            rt.anchoredPosition = new Vector2(i * sw, 0f);
        }
        // Thin wood trim just under the canopy.
        Image trim = AddImage(window, "AwningTrim", FlatUI.Pixel(), ShelfWood, false);
        RectTransform trt = trim.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(WIN_W - 14f, 4f);
        trt.anchoredPosition = new Vector2(0f, -39f);
    }

    // A lighter wood band behind the title/gold, with a gold divider line, so the header
    // reads as its own zone instead of dissolving into one flat brown slab.
    private void BuildHeaderBand()
    {
        RectTransform band = AddPoint(window, "HeaderBand", new Vector2(0.5f, 1f), new Vector2(0f, -43f), new Vector2(WIN_W - 14f, 74f));
        band.pivot = new Vector2(0.5f, 1f);
        Image b = band.gameObject.AddComponent<Image>();
        b.sprite = PixelUI.Grain(); b.type = Image.Type.Tiled; b.color = WoodBand;

        Image line = AddImage(window, "HeaderLine", FlatUI.Pixel(), WoodFrame, false);
        RectTransform lrt = line.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f); lrt.pivot = new Vector2(0.5f, 1f);
        lrt.sizeDelta = new Vector2(WIN_W - 14f, 3f);
        lrt.anchoredPosition = new Vector2(0f, -117f);
    }

    private void BuildGoldBox()
    {
        RectTransform goldBox = AddPoint(window, "GoldBox", new Vector2(1f, 1f), new Vector2(-SIDE_PAD, -54f), new Vector2(172f, 48f));
        goldBox.pivot = new Vector2(1f, 1f);
        Image gp = goldBox.gameObject.AddComponent<Image>();
        gp.sprite = PixelUI.Panel(); gp.type = Image.Type.Sliced; gp.color = PlaqueBg;
        Image coin = AddImage(goldBox, "Coin", Disc(), Color.white, false);
        coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        coin.rectTransform.pivot = new Vector2(0f, 0.5f);
        coin.rectTransform.anchoredPosition = new Vector2(14f, 0f);
        coin.rectTransform.sizeDelta = new Vector2(26f, 26f);
        goldText = AddText(goldBox, "Gold", new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(112f, 40f),
            "0", 26f, FontStyles.Bold, GoldCoin, TextAlignmentOptions.Left);
        goldFlash = AddImage(goldBox, "Flash", PixelUI.Panel(), new Color(1f, 1f, 1f, 0f), false);
        goldFlash.type = Image.Type.Sliced; Stretch(goldFlash.rectTransform);
    }

    private Transform BuildRow(string name, Vector2 anchor, Vector2 pos, float width, TextAnchor align)
    {
        RectTransform rt = AddPoint(window, name, anchor, pos, new Vector2(width, TILE_H));
        rt.pivot = new Vector2(anchor.x, 1f);
        HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = ROW_SPACING;
        h.childAlignment = align;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return rt;
    }

    private void BuildShelf(Vector2 anchor, Vector2 pos, float width, bool center = true)
    {
        Image shelf = AddImage(window, "Shelf", PixelUI.Grain(), ShelfWood, false);
        shelf.type = Image.Type.Tiled;
        RectTransform rt = shelf.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(anchor.x, 1f);
        rt.sizeDelta = new Vector2(width + 16f, 10f);
        rt.anchoredPosition = pos;
    }

    private void BuildTooltip()
    {
        tooltip = AddPoint(window, "Tooltip", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 96f));
        tooltip.pivot = new Vector2(0.5f, 0f);
        Image bg = tooltip.gameObject.AddComponent<Image>();
        bg.sprite = PixelUI.Panel(); bg.type = Image.Type.Sliced; bg.color = new Color(0.06f, 0.05f, 0.04f, 0.98f);
        bg.raycastTarget = false;
        Image fr = AddImage(tooltip, "TipFrame", PixelUI.Frame(), WoodFrame, false);
        fr.type = Image.Type.Sliced; Stretch(fr.rectTransform);
        tooltipText = AddText(tooltip, "TipText", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(276f, 84f),
            "", 16f, FontStyles.Normal, new Color(0.9f, 0.9f, 0.94f), TextAlignmentOptions.Center);
        tooltipText.enableWordWrapping = true;
        tooltip.gameObject.SetActive(false);
    }

    // ---- open / close ----
    private void Show(Shopkeeper shopkeeper)
    {
        if (isOpen) return;
        isOpen = true;
        shop = shopkeeper;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }
        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        if (cachedHud != null) cachedHud.SetActive(false);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Put this shopkeeper's own face on the counter.
        if (portraitImage != null)
        {
            Sprite face = shopkeeper != null ? shopkeeper.ResolvePortrait() : null;
            portraitImage.sprite = face;
            portraitImage.enabled = face != null;
        }
        if (barkerText != null) barkerText.text = "";
        if (bubbleGroup != null) bubbleGroup.alpha = 0f;

        if (tooltip != null) tooltip.gameObject.SetActive(false);
        BuildOffers();
        Populate();
        RefreshGold();

        // Scale the whole stall to nearly fill the canvas (computed from the real canvas size,
        // so it fills whatever resolution the player is at). Uniform scale keeps the tuned layout.
        Vector2 cs = canvasRT != null ? canvasRT.rect.size : ((RectTransform)transform).rect.size;
        if (cs.x > 1f && cs.y > 1f)
            fillScale = Mathf.Clamp(Mathf.Min(cs.x * 0.985f / WIN_W, cs.y * 0.965f / WIN_H), 0.5f, 4f);

        // StopAllCoroutines kills the talk/mood routines, so clear their handles before greeting —
        // a stale moodCo would make Update think a reaction is still playing and freeze the idle bob.
        StopAllCoroutines();
        talkCo = null; moodCo = null;
        StartCoroutine(OpenAnim());
        Say(Greetings, Mood.Lean);
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(GameState.Playing);
        }
        if (cachedHud != null) cachedHud.SetActive(true);
        if (HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);
        Cursor.lockState = CursorLockMode.Confined;

        gameObject.SetActive(false);
    }

    private IEnumerator OpenAnim()
    {
        group.alpha = 0f;

        // Start the tile pop-ins CONCURRENTLY with the window fade (each starts invisible).
        // Running them AFTER the fade caused the reported flash: tiles showed during the fade,
        // then reset to 0 and popped again.
        for (int i = 0; i < tiles.Count; i++)
            StartCoroutine(PopTile(tiles[i], 0.035f * i));

        float t = 0f; const float dur = 0.22f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            group.alpha = n;
            window.localScale = Vector3.one * fillScale * (0.95f + 0.05f * EaseOutBack(n));
            yield return null;
        }
        group.alpha = 1f; window.localScale = Vector3.one * fillScale;
    }

    private IEnumerator PopTile(Tile tile, float delay)
    {
        if (tile == null || tile.root == null) yield break;
        tile.cg.alpha = 0f;
        tile.root.localScale = Vector3.one * 0.82f;
        float e = 0f;
        while (e < delay) { e += Time.unscaledDeltaTime; yield return null; }
        float t = 0f; const float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            tile.cg.alpha = n;
            tile.root.localScale = Vector3.one * (0.82f + 0.18f * EaseOutBack(n));
            yield return null;
        }
        tile.cg.alpha = 1f; tile.root.localScale = Vector3.one;
    }

    // ---- offers ----
    private void BuildOffers()
    {
        offers.Clear();
        if (shop != null)
        {
            foreach (ShopSlotData d in shop.myInventory)
            {
                // Haggler is applied HERE, once, as the tile is built — so the number on the plaque
                // is the number the player is charged. `slot.price` stays the sticker so selling the
                // relic mid-shop cannot leave a discounted price quoted from a stale copy.
                if (d.itemType == ShopItemType.Card && d.cardReference != null)
                    offers.Add(new Offer { type = ShopItemType.Card, card = d.cardReference, slot = d,
                        name = d.cardReference.cardName, desc = d.cardReference.description,
                        price = ShopPricing.Effective(d.price) });
                else if (d.itemType == ShopItemType.Relic && d.relicReference != null)
                    offers.Add(new Offer { type = ShopItemType.Relic, relic = d.relicReference, slot = d,
                        name = d.relicReference.relicName, desc = d.relicReference.description,
                        price = ShopPricing.Effective(d.price) });
            }
        }
        ShopManager sm = ShopManager.instance;
        if (sm != null)
        {
            offers.Add(new Offer { type = ShopItemType.Service, name = "Medical Kit", price = sm.healCost,
                desc = $"Restores <color=#7CFF7C>{sm.healAmount} HP</color>.",
                onService = () => { if (GameManager.instance != null && GameManager.instance.player != null) GameManager.instance.player.Heal(sm.healAmount); } });
            offers.Add(new Offer { type = ShopItemType.Service, name = "Shift Battery", price = sm.shiftCost,
                desc = $"Grants <color=#7CC8FF>+{sm.shiftAmount} Shift</color>.",
                onService = () => { if (GameManager.instance != null && GameManager.instance.player != null) GameManager.instance.player.AddShift(sm.shiftAmount); } });
        }
    }

    private void Populate()
    {
        ClearRow(cardsRow); ClearRow(relicsRow); ClearRow(servicesRow);
        tiles.Clear();

        foreach (Offer o in offers)
        {
            Transform row = o.type == ShopItemType.Card ? cardsRow
                          : o.type == ShopItemType.Relic ? relicsRow : servicesRow;
            bool topRow = o.type == ShopItemType.Card;
            tiles.Add(BuildTile(row, o, topRow));
        }
    }

    private Tile BuildTile(Transform row, Offer o, bool topRow)
    {
        Tile tile = new Tile { offer = o, topRow = topRow };

        // ⚠️ A CARD TILE IS CARD-SHAPED. Relics and services keep the square-ish shelf tile, but a
        // card is drawn as the real 2:3 face (see below), so its tile is sized to that aspect at the
        // same height — otherwise the card floats in a wide box with dead space either side. The
        // row centres them, so a narrower tile just reads as more shelf between the cards.
        bool isCard = o.type == ShopItemType.Card && o.card != null;
        float tileW = isCard ? TILE_H / CardFace.ASPECT : TILE_W;

        RectTransform root = AddPoint(row, o.name, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(tileW, TILE_H));
        LayoutElement le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = tileW; le.preferredHeight = TILE_H;
        tile.root = root;
        tile.cg = root.gameObject.AddComponent<CanvasGroup>();
        tile.cg.alpha = 0f;   // invisible until PopTile fades it in (prevents a pre-anim flash)

        if (isCard)
        {
            // ⚠️ THE REAL CARD FACE, same as the Scrap Forge and Blompo. The shop used to draw the
            // art into a 68x68 SQUARE icon, which letterboxed the whole 2:3 card down to about
            // 45x68 — unreadable — and then re-printed the name and "N SHIFT  N CHARGES" as text
            // underneath to compensate. All of that is painted on the card. CardFace also stamps
            // the medallion digits, which are NOT in the art.
            //
            // No grain plate and no PixelUI frame here: the card carries its own painted border,
            // and a second frame around it read as a card inside a card.
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // keeps the whole tile clickable
            tile.tileBg = hit;

            CardFace.Build(root, new RuntimeCard(o.card));

            // Hovering turns it over for the effect text, exactly as in the forge and Blompo.
            CardHoverFlip.Attach(root).Bind(new RuntimeCard(o.card));
        }
        else
        {
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = PixelUI.Grain(); bg.type = Image.Type.Tiled; bg.color = TileBg;
            tile.tileBg = bg;

            Color frameCol = (o.type == ShopItemType.Relic) ? FlatUI.RarityColor(o.relic.rarity)
                                                            : new Color(0.5f, 0.42f, 0.3f, 1f);
            Image fr = AddImage(root, "Frame", PixelUI.Frame(), frameCol, false);
            fr.type = Image.Type.Sliced; Stretch(fr.rectTransform);

            // Icon.
            RectTransform iconRt = AddPoint(root, "Icon", new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(68f, 68f));
            Image ic = iconRt.gameObject.AddComponent<Image>();
            if (o.type == ShopItemType.Relic)
            {
                ic.preserveAspect = true;
                if (o.relic.relicArt != null) ic.sprite = o.relic.relicArt;
                else { ic.sprite = Gem(); ic.color = FlatUI.RarityColor(o.relic.rarity); }   // clean rarity gem until art exists
            }
            else
            {
                ic.sprite = (o.type == ShopItemType.Service) ? ServiceIcon(o.name) : PixelUI.Panel();
                ic.color = (o.type == ShopItemType.Service) ? Color.white : new Color(0.35f, 0.3f, 0.24f, 1f);
                if (o.type != ShopItemType.Service) ic.type = Image.Type.Sliced;
            }

            // Name (wraps to 2 lines).
            TMP_Text nm = AddText(root, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(TILE_W - 14f, 38f),
                o.name, 17f, FontStyles.Bold, Color.white, TextAlignmentOptions.Top);
            nm.enableWordWrapping = true;
        }

        // Clean price plaque along the bottom (no overlap, no tilt).
        //
        // ⚠️ On a card it is lifted clear of the card's own NAME PLATE, which occupies roughly the
        // bottom 10% of the face. Sitting it at the usual height covered the title, so every card in
        // the row was an unlabelled picture — the one thing a shopper scans by. It now reads as a
        // price sticker slapped across the lower artwork with the name still showing beneath.
        RectTransform plaque = AddPoint(root, "Plaque", new Vector2(0.5f, 0f),
            new Vector2(0f, isCard ? 24f : 10f), new Vector2(tileW - 24f, isCard ? 28f : 32f));
        Image pbg = plaque.gameObject.AddComponent<Image>();
        pbg.sprite = PixelUI.Panel(); pbg.type = Image.Type.Sliced; pbg.color = PlaqueBg;
        Image pcoin = AddImage(plaque, "PCoin", Disc(), Color.white, false);
        pcoin.rectTransform.anchorMin = pcoin.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        pcoin.rectTransform.pivot = new Vector2(0f, 0.5f);
        pcoin.rectTransform.anchoredPosition = new Vector2(14f, 0f);
        pcoin.rectTransform.sizeDelta = new Vector2(20f, 20f);
        tile.priceLabel = AddText(plaque, "Price", new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(tileW - 74f, 30f),
            o.price.ToString(), 21f, FontStyles.Bold, GoldCoin, TextAlignmentOptions.Left);

        // Whole tile buys (no default colour tint — we do our own hover).
        tile.buyBtn = root.gameObject.AddComponent<Button>();
        tile.buyBtn.transition = Selectable.Transition.None;
        Offer captured = o; Tile capturedTile = tile;
        tile.buyBtn.onClick.AddListener(() => TryBuy(capturedTile));

        // ⚠️ CARDS GET NO TOOLTIP. Hovering a card now turns it over, and its back already carries
        // the name, the effect text and the cost/charges — the tooltip was saying the same thing a
        // second time, in a box beside the card that was showing it. Relics and services keep it:
        // they have no back to turn to. The keeper's browse bark stays on both, because that is
        // flavour rather than information.
        bool tileIsCard = isCard;

        ShopTileHover hov = root.gameObject.AddComponent<ShopTileHover>();
        hov.onEnter = () =>
        {
            if (!tileIsCard) ShowTooltip(capturedTile, captured.desc);
            HoverScale(capturedTile, 1.05f);
            BarkOnBrowse(capturedTile);
        };
        hov.onExit  = () =>
        {
            if (!tileIsCard) HideTooltip();
            HoverScale(capturedTile, 1f);
        };

        tile.soldStamp = BuildSoldStamp(root);
        ApplySold(tile);
        return tile;
    }

    private GameObject BuildSoldStamp(RectTransform parent)
    {
        RectTransform st = AddPoint(parent, "Sold", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TILE_W, 54f));
        st.localRotation = Quaternion.Euler(0, 0, -13f);
        Image bg = st.gameObject.AddComponent<Image>();
        bg.sprite = PixelUI.Panel(); bg.type = Image.Type.Sliced; bg.color = new Color(0.7f, 0.18f, 0.16f, 0.92f);
        AddText(st, "SoldText", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TILE_W, 50f),
            "SOLD", 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        st.gameObject.SetActive(false);
        return st.gameObject;
    }

    // ---- tooltip ----
    private void ShowTooltip(Tile tile, string text)
    {
        if (tooltip == null || tile == null || tile.root == null) return;
        if (string.IsNullOrEmpty(text)) { HideTooltip(); return; }
        tooltipText.text = text;

        // Position above (bottom-row tiles) or below (top-row tiles) so it never leaves the window.
        Vector3 localCenter = window.InverseTransformPoint(tile.root.position);
        float half = TILE_H * 0.5f + 10f;
        if (tile.topRow)
        {
            tooltip.pivot = new Vector2(0.5f, 1f);   // hang below the tile
            tooltip.anchoredPosition = new Vector2(localCenter.x, localCenter.y - half);
        }
        else
        {
            tooltip.pivot = new Vector2(0.5f, 0f);   // sit above the tile
            tooltip.anchoredPosition = new Vector2(localCenter.x, localCenter.y + half);
        }
        tooltip.gameObject.SetActive(true);
        tooltip.SetAsLastSibling();
    }

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.gameObject.SetActive(false);
    }

    // What he says when you look at something. Affordability wins over item type — being told you
    // can't afford it is more useful than a joke about what it does, and it's the reaction a real
    // shopkeeper would have to you eyeing something out of your league.
    private void BarkOnBrowse(Tile tile)
    {
        Offer o = tile.offer;
        if (o.Sold) { Say(AlreadySold, Mood.Slump); return; }

        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player != null && player.currentGold < o.price) { Say(TooPoor, Mood.Slump); return; }

        switch (o.type)
        {
            case ShopItemType.Card:    Say(BrowseCard, Mood.Lean); break;
            case ShopItemType.Relic:   Say(BrowseRelic, Mood.Lean); break;
            default:                   Say(BrowseService, Mood.Lean); break;
        }
    }

    // ---- buying ----
    // Blank Cheque's bark. The relic has no UI of its own, so the shopkeeper saying it out loud is
    // how the player learns the free item was spent — diegetic, and it costs no screen furniture.
    private static readonly string[] ChequeBarks =
    {
        "That one's on the house. The rest aren't.",
        "Cheque cleared. Don't push it.",
        "Free of charge. Once.",
    };

    // Marks this shop's free item as taken.
    private void SpendBlankCheque()
    {
        if (shop == null) return;
        shop.blankChequeUsed = true;
        Say(ChequeBarks, Mood.Nod);
    }

    private void TryBuy(Tile tile)
    {
        Offer o = tile.offer;
        if (o.Sold) { Say(AlreadySold, Mood.Slump); return; }
        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) return;

        // Blank Cheque: the first thing you take at each shop is free. Resolved before the
        // affordability gate, or a broke player would be refused the item they weren't paying for.
        //
        // ⚠️ The flag is only consumed once the purchase actually COMPLETES. A relic buy can still
        // be declined at the swap screen, and burning the cheque on a decline would silently cost
        // the player their free item for nothing.
        bool freeGoing = shop != null && !shop.blankChequeUsed
                         && RelicManager.instance != null
                         && RelicManager.instance.HasRelic("BlankCheque");
        int due = freeGoing ? 0 : o.price;

        if (player.currentGold < due) { Say(TooPoor, Mood.Slump); StartCoroutine(DenyShake(tile)); return; }

        switch (o.type)
        {
            case ShopItemType.Card:
                if (player.TrySpendGold(due))
                {
                    DeckManager.instance.AddCardToDeck(o.card);
                    o.slot.isSold = true;
                    if (freeGoing) SpendBlankCheque();
                    OnBought(tile);
                }
                break;

            case ShopItemType.Relic:
                RelicManager.instance.TryGrantRelic(o.relic, () =>
                {
                    player.TrySpendGold(due);
                    o.slot.isSold = true;
                    if (freeGoing) SpendBlankCheque();
                    OnBought(tile);
                });
                break;

            // ⚠️ Services deliberately do NOT accept the cheque. It buys an ITEM — a card or a
            // relic — and letting it cover a heal would make the strongest use of a Legendary-tier
            // perk "get 75 gold of Shift", which is a poor trade dressed up as flexibility.
            case ShopItemType.Service:
                if (player.TrySpendGold(o.price))
                {
                    o.onService?.Invoke();
                    Say(Bought, Mood.Nod);
                    StartCoroutine(PunchTile(tile));
                    StartCoroutine(GoldFlash());
                    RefreshGold();
                }
                break;
        }
    }

    private void OnBought(Tile tile)
    {
        RefreshGold();
        HideTooltip();
        Say(Bought, Mood.Nod);
        StartCoroutine(GoldFlash());
        StartCoroutine(PunchTile(tile));
        StartCoroutine(SlamSold(tile));
    }

    private void ApplySold(Tile tile)
    {
        bool sold = tile.offer.Sold;
        if (tile.soldStamp != null) tile.soldStamp.SetActive(sold);
        if (tile.tileBg != null) tile.tileBg.color = sold ? new Color(0.12f, 0.10f, 0.09f, 1f) : TileBg;
        if (tile.cg != null) tile.cg.alpha = sold ? 0.6f : 1f;
        if (tile.buyBtn != null) tile.buyBtn.interactable = !sold;
    }

    // ---- refresh ----
    private void RefreshGold()
    {
        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        if (goldText != null && p != null) goldText.text = p.currentGold.ToString();
    }

    private void RefreshAffordability()
    {
        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        if (p == null) return;
        foreach (Tile t in tiles)
        {
            if (t.offer.Sold || t.priceLabel == null) continue;
            t.priceLabel.color = p.currentGold >= t.offer.price ? GoldCoin : new Color(0.85f, 0.35f, 0.3f);
        }
    }

    // ---- juice ----
    private void HoverScale(Tile tile, float target)
    {
        if (tile == null || tile.root == null || tile.offer.Sold) return;
        if (tile.hoverCo != null) StopCoroutine(tile.hoverCo);
        tile.hoverCo = StartCoroutine(ScaleTo(tile.root, target, 0.09f));
    }

    private IEnumerator ScaleTo(RectTransform rt, float target, float dur)
    {
        Vector3 from = rt.localScale, to = Vector3.one * target;
        float t = 0f;
        while (t < dur) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.Lerp(from, to, t / dur); yield return null; }
        rt.localScale = to;
    }

    private IEnumerator PunchTile(Tile tile)
    {
        if (tile?.root == null) yield break;
        float t = 0f; const float dur = 0.22f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float s = 1f + 0.14f * Mathf.Sin((t / dur) * Mathf.PI);
            tile.root.localScale = Vector3.one * s;
            yield return null;
        }
        tile.root.localScale = Vector3.one;
    }

    private IEnumerator SlamSold(Tile tile)
    {
        ApplySold(tile);
        if (tile.soldStamp == null) yield break;
        RectTransform st = tile.soldStamp.GetComponent<RectTransform>();
        float t = 0f; const float dur = 0.18f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            st.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, EaseOutBack(n));
            yield return null;
        }
        st.localScale = Vector3.one;
    }

    private IEnumerator GoldFlash()
    {
        if (goldFlash == null) yield break;
        float t = 0f; const float dur = 0.3f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            goldFlash.color = new Color(1f, 0.9f, 0.5f, 0.5f * (1f - t / dur));
            yield return null;
        }
        goldFlash.color = new Color(1f, 1f, 1f, 0f);
    }

    private IEnumerator DenyShake(Tile tile)
    {
        if (tile?.root == null) yield break;
        Vector3 baseScale = Vector3.one;
        float t = 0f; const float dur = 0.3f;
        RectTransform rt = tile.root;
        Vector2 basePos = rt.anchoredPosition;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 65f) * 5f, 0f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    // ---- pixel icons (low-res, Point-filtered, dark 1px outline; colours baked, used at white) ----
    private static Sprite discSprite, crossSprite, boltSprite;

    // A gold coin (kept named Disc for the existing call sites).
    private static Sprite Disc()
    {
        if (discSprite == null)
            discSprite = MakeIcon(16, (nx, ny) => { float dx = nx - 0.5f, dy = ny - 0.5f; return dx * dx + dy * dy < 0.17f; },
                new Color(1f, 0.82f, 0.30f), new Color(0.35f, 0.22f, 0.08f));
        return discSprite;
    }

    private Sprite ServiceIcon(string name)
    {
        string n = name.ToLower();
        return (n.Contains("medical") || n.Contains("heal") || n.Contains("kit")) ? Cross() : Bolt();
    }

    // Faceted pixel gem, grayscale so it tints to any rarity colour. A clean placeholder for
    // relics that don't have art yet — reads as "treasure" instead of a blank blob.
    private static Sprite gemSprite;
    private static Sprite Gem()
    {
        if (gemSprite != null) return gemSprite;
        const int s = 16;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s, ny = (y + 0.5f) / s;
                float d = Mathf.Abs(nx - 0.5f) + Mathf.Abs(ny - 0.5f);   // diamond
                Color32 c;
                if (d >= 0.46f) c = new Color32(0, 0, 0, 0);
                else if (d >= 0.38f) c = new Color32(70, 55, 30, 255);   // dark facet edge
                else
                {
                    float sh = (0.5f - nx) * 0.5f + (ny - 0.5f) * 0.5f;   // top-left brighter
                    float g = Mathf.Clamp(0.82f + sh * 0.9f, 0.45f, 1f);
                    if (ny > 0.58f && Mathf.Abs(nx - 0.4f) < 0.10f) g = 1f;   // sparkle facet
                    c = new Color32((byte)(g * 255), (byte)(g * 255), (byte)(g * 255), 255);
                }
                px[y * s + x] = c;
            }
        tex.SetPixels32(px); tex.Apply();
        gemSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return gemSprite;
    }

    private static Sprite Cross()
    {
        if (crossSprite == null)
            crossSprite = MakeIcon(16, (nx, ny) =>
            {
                float ax = Mathf.Abs(nx - 0.5f), ay = Mathf.Abs(ny - 0.5f);
                return (ax < 0.15f && ay < 0.38f) || (ay < 0.15f && ax < 0.38f);
            }, new Color(0.95f, 0.93f, 0.86f), new Color(0.45f, 0.16f, 0.14f));
        return crossSprite;
    }

    private static Sprite Bolt()
    {
        if (boltSprite == null)
            boltSprite = MakeIcon(16, (nx, ny) =>
            {
                bool top = ny > 0.5f && Mathf.Abs((nx - 0.62f) - (ny - 0.5f) * -0.7f) < 0.12f;
                bool bot = ny <= 0.5f && Mathf.Abs((nx - 0.38f) - (ny - 0.5f) * -0.7f) < 0.12f;
                return top || bot;
            }, new Color(1f, 0.85f, 0.25f), new Color(0.40f, 0.28f, 0.06f));
        return boltSprite;
    }

    // Builds a chunky pixel icon: body pixels where shape(nx,ny) is true, a 1px dark outline on
    // the body's border, transparent elsewhere. Point-filtered so it stays crisp when scaled.
    private static Sprite MakeIcon(int s, System.Func<float, float, bool> shape, Color body, Color outline)
    {
        bool[,] m = new bool[s, s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                m[x, y] = shape((x + 0.5f) / s, (y + 0.5f) / s);

        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                if (m[x, y]) { px[y * s + x] = body; continue; }
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < s && ny >= 0 && ny < s && m[nx, ny]) near = true;
                    }
                px[y * s + x] = near ? (Color32)outline : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }

    // ---- small UGUI builders ----
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void ClearRow(Transform row)
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text AddText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = fontSize; t.fontStyle = style; t.color = color; t.alignment = align;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    private GameObject BuildButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        Color color, string text, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = PixelUI.Panel(); bg.type = Image.Type.Sliced; bg.color = color;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.onClick.AddListener(onClick);
        AddText(rt, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size,
            text, 20f, FontStyles.Bold, new Color(0.14f, 0.1f, 0.05f), TextAlignmentOptions.Center);
        return rt.gameObject;
    }

    private TMP_FontAsset ResolveFont() => FlatUI.UIFont();

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

// Pointer relay for shop tiles (hover lift + tooltip). Top-level so AddComponent works.
public class ShopTileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onEnter, onExit;
    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
}
