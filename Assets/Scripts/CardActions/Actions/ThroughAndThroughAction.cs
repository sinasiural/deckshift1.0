using System.Collections;
using UnityEngine;

// "Through and Through" — the Samurai's signature card. A Dash that cuts: the player is driven
// forward on their facing with full i-frames, passes THROUGH enemy bodies, and cuts every enemy
// crossed for the card's actionValue.
//
// ⚠️ ITS FLAGS ARE DASH'S FLAGS PLUS LayerCollisionMatrix, and every one of the three is load-bearing:
//   PlayerVelocity       — it drives rb.linearVelocity every physics step, like the dash.
//   Invincibility        — it grants i-frames, so it must not overlap anything else that does.
//   LayerCollisionMatrix — it suspends player<->enemy collision (per-collider, see LungeRoutine).
// Declaring the last one is what makes CardActionExecutor refuse it while Phase is live, and Phase
// while this is live, for free. Sharing the first two means it also cannot stack with Dash itself.
//
// The name is honest about the shape rather than clever: this really is the dash with a blade on it.
public class ThroughAndThroughAction : CardAction
{
    public override CardActionType ActionType => CardActionType.ThroughAndThrough;
    public override bool IsCoroutine => true;
    public override ConflictFlags ModifiedState =>
        ConflictFlags.PlayerVelocity | ConflictFlags.Invincibility | ConflictFlags.LayerCollisionMatrix;

    // Coroutine action: no gate. It is playable grounded or airborne, into empty air or into a
    // wall — like Freefall Blade, the swing itself is what the charge buys. Returning true simply
    // lets the coroutine start.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return true;
    }

    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        yield return player.StartCoroutine(player.LungeRoutine(value));
    }
}
