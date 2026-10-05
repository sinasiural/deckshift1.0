using System.Collections;

// "Bloodlust" — for a few seconds, part of all damage you deal heals you. The effect lives in
// PlayerBloodlust; the heal itself happens at RelicManager.ModifyPlayerDamage.
public class BloodlustAction : CardAction
{
    public override CardActionType ActionType => CardActionType.Bloodlust;
    public override bool IsCoroutine => true;

    // Its own flag only: a second Bloodlust is refused while one runs (refused plays cost nothing),
    // while every attack card stays playable — attacking is the point of the window.
    public override ConflictFlags ModifiedState => ConflictFlags.Bloodlust;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        return player != null;
    }

    // `value` is the card's actionValue: the share of damage returned, in percent.
    public override IEnumerator ExecuteCoroutine(PlayerController player, float value)
    {
        return PlayerBloodlust.For(player).Run(value);
    }
}
