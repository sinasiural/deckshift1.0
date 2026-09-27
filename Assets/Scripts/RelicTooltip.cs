using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A single shared tooltip for the relic loadout bar: name + description + sell value,
// shown on hover under the hovered slot. Built procedurally in the FlatUI LOADOUT theme and
// reused across every slot — one instance, repositioned per hover.
//
// Its border is tinted by RARITY (see Show). With the gem gone from the bar, the tooltip's border
// and name colour are what confirm the meaning of the coloured strip on the slot above it.
public class RelicTooltip : MonoBehaviour
{
    private CanvasGroup group;
    private RectTransform rt;
    private Image frame;
    private TMP_Text nameText, descText, liveText, valueText;

    private const float WIDTH = 244f;
    private const float GAP = 10f;   // distance below the hovered slot

    public void Build(TMP_FontAsset font)
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 1f);              // hangs down from an anchor point
        rt.sizeDelta = new Vector2(WIDTH, 100f);

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Flat chamfered plate, matching the loadout bar it hangs off.
        Image bg = gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(6);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.055f, 0.053f, 0.050f, 0.98f);   // darker than the bar so it sits on top
        bg.raycastTarget = false;

        // Vertical content layout, height driven by a ContentSizeFitter. Padding clears the gold border.
        VerticalLayoutGroup vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 14, 14);
        vlg.spacing = 5f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        nameText  = MakeText(font, 19f, FontStyles.Bold, FlatUI.Loadout.TextBright);
        descText  = MakeText(font, 15f, FontStyles.Normal, FlatUI.Loadout.TextBody);
        // What the relic is doing RIGHT NOW (RelicManager.LiveReadout). Brighter than the rule above
        // it and no new hue — Loadout is the colourless theme, and it sits over gameplay.
        liveText  = MakeText(font, 15f, FontStyles.Bold, FlatUI.Loadout.TextBright);
        valueText = MakeText(font, 14f, FontStyles.Bold, new Color(0.85f, 0.72f, 0.36f));

        // Outline on top (ignored by the layout, stretched to fill). Unlike the old gold frame this
        // one is TINTED BY RARITY in Show() — with the gem gone from the bar, the tooltip border and
        // the name colour are what confirm what the slot's coloured strip was telling you.
        GameObject frameGo = new GameObject("Frame", typeof(RectTransform));
        RectTransform frt = frameGo.GetComponent<RectTransform>();
        frt.SetParent(transform, false);
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        LayoutElement le = frameGo.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        frame = frameGo.AddComponent<Image>();
        frame.sprite = FlatUI.Outline(6, 1);
        frame.type = Image.Type.Sliced;
        frame.color = FlatUI.Loadout.Border;
        frame.raycastTarget = false;

        gameObject.SetActive(false);
    }

    private TMP_Text MakeText(TMP_FontAsset font, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    // Positions the tooltip just below the given slot and fills it with the relic's info.
    public void Show(RelicData relic, RectTransform slot)
    {
        if (relic == null || slot == null) { Hide(); return; }

        Color rarityCol = FlatUI.RarityColor(relic.rarity);

        nameText.text = string.IsNullOrEmpty(relic.relicName) ? relic.relicID : relic.relicName;
        nameText.color = rarityCol;
        descText.text = string.IsNullOrEmpty(relic.description) ? "-" : relic.description;

        // Hidden entirely when there is nothing live to say, so a plain relic's tooltip is unchanged.
        string live = RelicManager.instance != null ? RelicManager.instance.LiveReadout(relic) : null;
        liveText.gameObject.SetActive(!string.IsNullOrEmpty(live));
        if (!string.IsNullOrEmpty(live)) liveText.text = live;

        int value = RelicManager.instance != null ? RelicManager.instance.SellValueFor(relic) : 0;
        valueText.text = $"Sell: {value} gold";

        // Rarity carried by the name AND the border, matching the strip on the slot above.
        if (frame != null) frame.color = Color.Lerp(FlatUI.Loadout.Border, rarityCol, 0.55f);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Anchor to the slot's bottom-centre (canvas-space agnostic — corners and
        // transform.position share the same space).
        Vector3[] corners = new Vector3[4];
        slot.GetWorldCorners(corners); // 0=BL 1=TL 2=TR 3=BR
        Vector3 bottomCentre = (corners[0] + corners[3]) * 0.5f;
        transform.position = bottomCentre + Vector3.down * GAP;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
