using UnityEngine;

/// <summary>
/// The four blank card frames the hover back is printed on, one per rarity (designer art,
/// 2026-09-27: Assets/Art/bgcommon, bguncommon, bgrare, bgepic).
///
/// Lives at Assets/Resources/CardBackArt.asset so CardBack, which is built in code, can find the
/// frames without a scene reference. Swap a frame by dragging a new sprite into its slot.
/// </summary>
[CreateAssetMenu(fileName = "CardBackArt", menuName = "Deckshift/Card Back Art")]
public class CardBackArt : ScriptableObject
{
    public Sprite common;
    public Sprite uncommon;
    public Sprite rare;
    public Sprite epic;

    public Sprite For(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Uncommon: return uncommon != null ? uncommon : common;
            case CardRarity.Rare:     return rare != null ? rare : common;
            case CardRarity.Epic:     return epic != null ? epic : common;
            default:                  return common;
        }
    }

    private static CardBackArt cached;
    private static bool searched;

    /// <summary>The project's frames, or null if the asset is missing (CardBack then draws a plain back).</summary>
    public static CardBackArt Get()
    {
        if (cached != null || searched) return cached;
        searched = true;
        cached = Resources.Load<CardBackArt>("CardBackArt");
        if (cached == null)
            Debug.LogWarning("CardBackArt: no Assets/Resources/CardBackArt.asset — card backs fall back to a plain frame.");
        return cached;
    }
}
