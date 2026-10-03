// "Glass Moon" — see GlassMoon.cs. The action only summons the moon; GlassMoonVFX runs its timeline
// and calls GlassMoon.Strike when it bursts. It holds no player state, so it declares no
// ConflictFlags: two moons in the air at once is allowed.
public class GlassMoonAction : CardAction
{
    public override CardActionType ActionType => CardActionType.GlassMoon;

    // `value` is the card's actionValue: the burst's damage. Returning false above the HP threshold
    // is the lock — a failed play costs nothing and leaves the card in hand.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return player != null && GlassMoon.Cast(player, value);
    }
}
