using UnityEngine;

// The Ninja boss's own stars, thrown back at him.
//
// Mechanically identical to ShurikenAction — same aimed throw, same animation, same damage — and
// that is deliberate: this is not a special boss-fight weapon, it is HIS shuriken in the player's
// hand. The design doc rejects both "shuriken hit the boss harder" and "boss-only cards" for the
// same reason: they lie to the player about what a shuriken is, and CardAnchors.md says 8 is 8.
//
// It exists as a separate CardActionType only so DeckManager can recognise it (see IsSalvagedShuriken).
public class SalvagedShurikenAction : CardAction
{
    public override CardActionType ActionType => CardActionType.SalvagedShuriken;

    // No declared flags, for the same reason ShurikenAction and FireballAction declare none: an
    // attack must never be refused by the conflict system, and claiming AnimatorAttackState would
    // Block rapid throws — which on a quiver holding a dozen stars is the whole way you play it.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;   // the quiver's retention is decided in DeckManager, not here
        player.ThrowShuriken(value);
        return true;
    }
}
