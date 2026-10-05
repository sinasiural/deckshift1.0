using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Turns any card-shaped UI element over on hover to show its CardBack.
//
// THE ONE IMPLEMENTATION. The hand's CardUI, the Scrap Forge's repair chips and Blompo's card picker
// all use this. It exists as a component rather than as code copied into each screen because the
// mechanism has three non-obvious requirements that have each already caused a shipped bug:
//
//   1. The back must be PRE-ROTATED 180 degrees, or past the halfway point it renders mirrored.
//   2. The hover target must COUNTER-ROTATE, or the raycast rect narrows with the card, the pointer
//      falls off it halfway through, and the card sits edge-on flapping under the cursor.
//   3. Showing the front again must restore ONLY WHAT THIS HID, or children that were deliberately
//      inactive get resurrected by the flip.
//
// A second hand-rolled copy of that would drift, and the failure modes are all "looks almost right".
public class CardHoverFlip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Seconds for the card to turn over.")]
    public float flipDuration = 0.22f;

    // Z-lean the owner wants on top of the flip, in degrees. The hand's fan uses it to splay its
    // cards. It lives here because this component writes localRotation every frame, so it has to be
    // the ONE writer — a second component setting the same transform would be resolved by script
    // execution order, which Unity does not define.
    [HideInInspector] public float ExtraRoll;

    private CardBack back;
    private RectTransform hoverTarget;
    private RectTransform geometry;     // the rect that matches what the player SEES (see Bind)
    private readonly List<Transform> hidden = new List<Transform>();

    private float t;                    // 0 = front, 1 = back
    private bool hovered;
    private bool showingBack;

    // 0..1, eased. Owners use it to drive their own extras — CardUI scales and lifts the card by it.
    public float Progress { get; private set; }
    public CardBack Back => back;

    // `geometrySource` is the rect that matches the visible card. Pass null when the host IS that
    // rect (the forge and Blompo build their chips at their real size).
    //
    // ⚠️ CardUI must pass its cardArtImage: a hand card's ROOT carries a LayoutElement and is
    // rewritten to 200x100 at runtime by the hand's layout group, nothing like the 200x300 it looks
    // like in the prefab. Sizing the back off the root put it over the card's bottom third.
    public static CardHoverFlip Attach(RectTransform host, RectTransform geometrySource = null)
    {
        CardHoverFlip flip = host.GetComponent<CardHoverFlip>();
        if (flip != null) return flip;

        flip = host.gameObject.AddComponent<CardHoverFlip>();
        flip.geometry = geometrySource != null ? geometrySource : host;
        flip.back = CardBack.Build(host);
        flip.BuildHoverTarget(host);
        return flip;
    }

    private void BuildHoverTarget(RectTransform host)
    {
        GameObject go = new GameObject("HoverTarget", typeof(RectTransform));
        hoverTarget = go.GetComponent<RectTransform>();
        hoverTarget.SetParent(host, false);

        Image hit = go.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);   // invisible, still raycast-hit
        hit.raycastTarget = true;

        hoverTarget.SetAsLastSibling();
    }

    // Re-reads the geometry and fills the back with this card. Call whenever the content changes.
    public void Bind(RuntimeCard card, string keyHint = "")
    {
        if (back == null) return;

        back.MatchTo(geometry);
        Match(hoverTarget, geometry);
        back.BindStandard(card, keyHint);
    }

    private static void Match(RectTransform target, RectTransform source)
    {
        if (target == null || source == null) return;
        if (target.parent == source)
        {
            // Nested inside the geometry: just fill it.
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = target.offsetMax = Vector2.zero;
            return;
        }
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition = source.anchoredPosition;
    }

    private void Update()
    {
        if (back == null) return;

        float target = hovered ? 1f : 0f;
        if (!Mathf.Approximately(t, target))
        {
            // Unscaled: every screen this appears on holds the game at timeScale 0.
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, flipDuration);
            t = Mathf.MoveTowards(t, target, step);
        }

        // Smoothstep: leaves and arrives softly, crosses the edge-on point quickly. A linear turn
        // reads mechanical, and lingering side-on reads as a glitch.
        Progress = t * t * (3f - 2f * t);

        float angle = Progress * 180f;
        transform.localRotation = Quaternion.Euler(0f, angle, ExtraRoll);
        // Counter-rotates the FLIP only. The roll is inherited deliberately: a Z-rotated rect still
        // covers its own area, so the raycast is unharmed — it is the Y turn that narrows the rect
        // to nothing and drops the pointer off the card.
        if (hoverTarget != null) hoverTarget.localRotation = Quaternion.Euler(0f, -angle, 0f);

        bool wantBack = Progress > 0.5f;
        if (wantBack == showingBack) return;

        showingBack = wantBack;
        SetFrontVisible(!wantBack);
        back.gameObject.SetActive(wantBack);
    }

    private void SetFrontVisible(bool visible)
    {
        if (visible)
        {
            for (int i = 0; i < hidden.Count; i++)
                if (hidden[i] != null) hidden[i].gameObject.SetActive(true);
            hidden.Clear();
            return;
        }

        hidden.Clear();
        Transform backT = back != null ? back.transform : null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == backT || child == hoverTarget) continue;
            if (!child.gameObject.activeSelf) continue;   // already off — leave it off
            child.gameObject.SetActive(false);
            hidden.Add(child);
        }
    }

    // Puts the card face-up again immediately, without animating. Screens call this when they tear
    // a row down and rebuild it, so a chip can't be left mid-turn.
    public void ResetFace()
    {
        hovered = false;
        t = 0f;
        Progress = 0f;
        // Keeps the owner's lean — this resets the FACE, not the pose.
        transform.localRotation = Quaternion.Euler(0f, 0f, ExtraRoll);
        if (hoverTarget != null) hoverTarget.localRotation = Quaternion.identity;
        if (showingBack) { showingBack = false; SetFrontVisible(true); }
        if (back != null) back.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
}
