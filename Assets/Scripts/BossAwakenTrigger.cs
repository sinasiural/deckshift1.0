using UnityEngine;

/// <summary>
/// Anything that starts dormant and is woken by the player walking into a trigger volume.
/// Implemented by MossKnightBoss and NinjaBoss.
/// </summary>
public interface IBossFight
{
    void StartFight();
}

/// <summary>
/// Wakes any dormant <see cref="IBossFight"/> when the player enters this trigger zone. Place an
/// empty GameObject with a BoxCollider2D (Is Trigger ON) over the area that should start the fight,
/// add this script, and assign the boss.
///
/// One-shot: disables itself after firing so the fight can't "restart".
///
/// ⚠️ THIS EXISTS ALONGSIDE THE OLDER `BossFightTrigger`, WHICH IS TYPED TO `MossKnightBoss`
/// SPECIFICALLY. It was left untouched deliberately — widening its serialized field type risks the
/// one working boss encounter in the game for no gain today. When the shared boss base class lands
/// (the "all bosses are characters" direction needs one anyway), the two should merge into this one
/// and `BossFightTrigger` should be deleted.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossAwakenTrigger : MonoBehaviour
{
    [Tooltip("The boss this trigger wakes. Must be a component implementing IBossFight " +
             "(NinjaBoss, MossKnightBoss).")]
    public MonoBehaviour boss;

    private void Reset()
    {
        // When the component is first added, default the collider to a trigger.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var fight = boss as IBossFight;
        if (fight == null)
        {
            // Loud on purpose: a boss room whose trigger silently does nothing is a room the player
            // walks around in wondering why the fight never starts.
            Debug.LogError($"[BossAwakenTrigger] '{name}': assigned boss " +
                           $"{(boss == null ? "is EMPTY" : boss.GetType().Name + " does not implement IBossFight")}.");
            return;
        }

        fight.StartFight();
        gameObject.SetActive(false);   // one-shot
    }
}
