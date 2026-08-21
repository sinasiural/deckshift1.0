public class RuntimeCard
{
    public CardData cardData;
    public int currentUses;
    public bool isInfinite = false;
    public bool isSelected = false;

    // Blompo's blessing. At most ONE per card (enforced in CardEnhancements.CanApplyTo).
    // Deliberately lives here and NOT on CardData — CardData is a shared asset, so writing an
    // enhancement there would upgrade the card in every future run and dirty the asset on disk.
    public CardEnhancement enhancement = CardEnhancement.None;

    // ---- per-blessing runtime state --------------------------------------------------------------
    // Several blessings accumulate across the run, so they need somewhere to keep score. It lives
    // here for the same reason the enhancement does: it is per-copy and per-run. With two Fireballs
    // in the deck, one can be a Grudge that has killed forty things and the other untouched.

    // Grudge: flat damage this card has earned by killing. Grows forever.
    public int grudgeBonus;

    // Time Will Come (gains a charge per room unplayed) and Compound Interest (pays Shift equal to
    // the wait) are both driven by this. One counter serves both — they ask the same question.
    public int roomsSincePlayed;

    // Last Call: the rescue is once per run.
    public bool lastCallUsed;

    // Teacher's Pet: its FIRST play each room spends no charge.
    public bool playedThisRoom;

    // Toll Booth refunds what this play actually cost, which is not recomputable later (First One's
    // Free, Kinetic Discount and the hub rule can all have zeroed it), so it is recorded on play.
    public int lastCostPaid;

    // Understudy: the card this one is bound to. Playing this draws that one.
    // ⚠️ A reference to another RuntimeCard, not a CardData — the bond is to that specific COPY, so
    // binding to one of your two Fireballs does not summon the other.
    public RuntimeCard understudyPartner;

    /// <summary>
    /// ⚠️ THE ONE PLACE A CARD'S MAXIMUM CHARGE COUNT IS DECIDED. Every read — the card face, the
    /// forge's repair maths, Blompo's picker, the shop tile — goes through here, for the same
    /// reason CardEnhancements.EffectiveCost exists: the eleven sites that used to read
    /// cardData.maxUses directly would each have needed their own copy of the Paper Skin rule.
    ///
    /// Computed rather than written into the card, so selling Paper Skin reverses exactly and
    /// cannot leave a stale +1 behind. Same principle as HandCapacity and StaggerStep.
    /// </summary>
    public int MaxUses
    {
        get
        {
            if (cardData == null) return 0;
            int max = cardData.maxUses;
            if (RelicManager.instance != null && RelicManager.instance.HasRelic("PaperSkin")) max += 1;
            return max;
        }
    }

    public RuntimeCard(CardData data)
    {
        cardData = data;
        currentUses = data.maxUses;
        isInfinite = false;
        isSelected = false;
        enhancement = CardEnhancement.None;
    }

    // Used by Twin, which puts a second copy of this card in the deck.
    public RuntimeCard Clone()
    {
        RuntimeCard c = new RuntimeCard(cardData);
        c.currentUses = currentUses;
        c.isInfinite = isInfinite;
        c.enhancement = enhancement;
        c.grudgeBonus = grudgeBonus;
        return c;
    }
}
