using System.Collections;

// "Brace" — plant your feet for a few seconds behind a block of armour; every hit you take while
// braced pays Shift. The whole effect (timer, block, Shift, stance, visuals) lives in PlayerBrace.
public class BraceAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Brace;
    public override bool IsCoroutine => true;

    // Its own flag only: a second Brace is refused while one runs (blocked plays cost nothing), so
    // the block can never stack, while every other card stays playable during the brace.
    public override ConflictFlags ModifiedState => ConflictFlags.Brace;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return player != null;
    }

    // `value` is the card's actionValue: the size of the block.
    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        return PlayerBrace.For(player).Run(value);
    }
}
