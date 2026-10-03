// "Break Glass" — see BreakGlass.cs. Instant, holds no state, so it declares no ConflictFlags.
public class BreakGlassAction : CardAction
{
    public override CardActionType ActionType => CardActionType.BreakGlass;

    // `value` is the card's actionValue: the burst's damage. Returning false above the HP threshold
    // is the lock — a failed play costs nothing and leaves the card in hand.
    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return player != null && BreakGlass.Perform(player, value);
    }
}
