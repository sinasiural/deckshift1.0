using UnityEngine;
using System.Collections.Generic;

public class Shopkeeper : MonoBehaviour
{
    private bool playerInRange = false;
    public KeyCode interactKey = KeyCode.E;

    [Header("G�rsel Referans")]
    public GameObject interactionPopup;

    [Header("Shop Screen")]
    [Tooltip("Portrait shown on the shop screen. Leave empty to use this shopkeeper's own world " +
             "sprite, so a placed stall gets a face with no wiring.")]
    public Sprite portrait;

    // The face the shop screen puts on the counter. Falls back to whatever this shopkeeper looks
    // like in the world, which is almost always the right answer and needs no Inspector step.
    public Sprite ResolvePortrait()
    {
        if (portrait != null) return portrait;
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

    [Header("Bu D�kkan�n ��eri�i")]
    public List<CardData> specificCardPool;
    public List<RelicData> specificRelicPool;

    // D�kkan�n Haf�zas�
    public List<ShopSlotData> myInventory = new List<ShopSlotData>();

    private bool isInitialized = false;

    // Blank Cheque: one free item at EVERY distinct shop, so it shapes how the map is routed rather
    // than being a single moment. Tracked per shopkeeper, which is what makes "distinct shop" mean
    // something — the flag lives and dies with this instance.
    [System.NonSerialized] public bool blankChequeUsed = false;

    private void Start()
    {
        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (!isInitialized)
        {
            GenerateShopContent();
            isInitialized = true;
        }
    }

    private void GenerateShopContent()
    {
        // NOTE: ShopManager.allCardsPool is deliberately NOT consulted any more — it held 10 of the
        // project's 15 cards, the same hand-maintained drift that hid relics. `specificCardPool`
        // survives as a genuine per-shop restriction; empty means the whole roster.
        //
        // NOTE: ShopManager.allRelicsPool is deliberately NOT consulted any more. It was a
        // hand-maintained list holding 3 of the project's 18 relics, and copying it into
        // specificRelicPool is exactly what capped the shop's stock. Relics now come from
        // RelicPool (see below), which derives from the assets themselves.

        // Cards — up to 5 DISTINCT offers, drawn from every card in the project (CardPool /
        // CardCatalogue). Stagger is excluded there: it is the fail-state card, and selling the
        // player a way to lose is not a shop.
        foreach (CardData card in CardPool.DrawDistinct(5, specificCardPool))
        {
            myInventory.Add(new ShopSlotData
            {
                itemType = ShopItemType.Card, cardReference = card, itemName = card.cardName,
                price = ShopPricing.ForCard(card), isSold = false
            });
        }

        // Relics — up to 3 DISTINCT offers, drawn from EVERY relic in the project (RelicPool /
        // RelicCatalogue) rather than a hand-kept list. ShopManager.allRelicsPool had drifted to 3
        // of the 18 relics, so five sixths of the roster was simply unbuyable and nothing in the
        // game could show that. `specificRelicPool` still works as a deliberate per-shop
        // restriction; empty means "the whole roster".
        //
        // Already-owned relics are excluded: shelf space spent on something you are wearing is a
        // wasted offer, and since ownership is read when the shop stocks, a relic you SELL becomes
        // buyable again in the next shop.
        {
            List<RelicData> picked = RelicPool.DrawDistinct(3, specificRelicPool);
            foreach (RelicData relic in picked)
            {
                myInventory.Add(new ShopSlotData
                {
                    itemType = ShopItemType.Relic, relicReference = relic, itemName = relic.relicName,
                    price = ShopPricing.ForRelic(relic), isSold = false
                });
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactionPopup != null) interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactionPopup != null) interactionPopup.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            if (interactionPopup != null) interactionPopup.SetActive(false);

            if (ShopManager.instance != null)
            {
                ShopManager.instance.OpenShop(this);
            }
        }
    }
}

public enum ShopItemType
{
    Card,
    Relic,
    Service
}

[System.Serializable]
public class ShopSlotData
{
    public string itemName;
    public int price;
    public bool isSold;

    public ShopItemType itemType;
    public CardData cardReference;
    public RelicData relicReference;
}