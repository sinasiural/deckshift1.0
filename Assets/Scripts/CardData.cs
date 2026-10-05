using UnityEngine;

// 'GameEnums.cs' dosyam�zda oldu�u i�in bu sat�ra gerek yok
// public enum CardActionType { ... } 

// A card's rarity. Cards have Uncommon where relics do not, and no Legendary, so this is its own
// enum rather than the relics' Rarity.
public enum CardRarity { Common, Uncommon, Rare, Epic }

[CreateAssetMenu(fileName = "New CardData", menuName = "Deckshift/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Card Info")]
    public string cardName;
    [TextArea]
    public string description;
    public Sprite cardArt;

    // Whether the card's TITLE is already drawn into cardArt.
    //
    // Going forward the answer is NO and CardUI types the name into the plate itself (designer,
    // 2026-08-09) — which is why the default is false. New art should ship with an EMPTY plate; the
    // name then comes from cardName, so renaming a card no longer means repainting a texture.
    //
    // ⚠️ The 14 pre-2026-08-09 cards have their names PAINTED IN and therefore set this to true.
    // Clear the flag on each one as its art is replaced — leaving it set just means the plate keeps
    // showing the old painted title, and clearing it too early prints the name on top of itself.
    [Tooltip("ON for legacy art with the title painted into the texture. OFF (the default) lets the UI type cardName into the plate.")]
    public bool nameIsPaintedIntoArt = false;

    // Which of the four card frames this card uses. The FRONT's frame colour is painted into the
    // art (dark grey Common, light grey Uncommon, yellow Rare, purple Epic); this field is the same
    // fact as data, so code can act on it: the hover back picks its frame from it (CardBack).
    // Keep it matching the art. There are no Legendary cards.
    [Tooltip("Must match the frame colour painted into the card art. The hover back uses the frame of this rarity.")]
    public CardRarity rarity = CardRarity.Common;

    [Header("Card Action")]
    public CardActionType actionType;
    public float actionValue;

    [Header("Card Behavior")]
    public bool singleUse = false;

    // --- YEN� EKLENEN KISIM ---
    [Header("Game Mechanics")]

    [Tooltip("Bu kart� oynaman�n 'Shift' maliyeti")]
    public int shiftCost = 0; // Varsay�lan maliyet 0 (yani bedava)

    [Tooltip("Bu kart�n desteye eklendi�inde sahip olaca�� maks. kullan�m hakk�")]
    public int maxUses = 3; // Varsay�lan kullan�m hakk� 3
    // --- B�T�� ---
}