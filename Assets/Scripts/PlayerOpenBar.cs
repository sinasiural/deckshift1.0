using System.Collections;
using UnityEngine;

/// <summary>
/// The Open Bar card's effect, on the player: for a few seconds, part of every point of damage you
/// deal comes back to you as health.
///
/// Design: CardIdeas.md #7 (designer verdict 2026-09-08: liked) — "For 6 seconds, 40% of all damage
/// you deal heals you." The decision it creates is that it has to be played BEFORE you commit to a
/// fight, not after you're hurt, which is the opposite of how players reach for healing.
///
/// ⚠️ HEALED AT RelicManager.ModifyPlayerDamage, the one chokepoint every point of player damage
/// passes through and the only place the target is known. So it works on every attack card and on
/// any added later, with nothing to wire per card.
///
/// ⚠️ IT HEALS ON DAMAGE THAT LANDS, NOT DAMAGE THAT WAS SWUNG. Two caps: overkill does not count
/// (a 60-point hit on a 12 HP zombie heals for 12's share, not 60's), and a hit a shield blocks heals
/// nothing. Breakable walls are not EnemyHealth, so hitting scenery never heals.
///
/// ⚠️ A BUFF MUST LOOK DIFFERENT (project rule): wine-red drops circle the player while it runs and
/// blink in the last second and a half, and every heal sends drops flying from the enemy into you.
///
/// Lives on the player (added on first use), like PlayerBrace.
/// </summary>
public class PlayerOpenBar : MonoBehaviour
{
    public const float Duration = 6f;

    // At most one window runs (ConflictFlags.OpenBar refuses a second play), so the chokepoint can
    // ask one static rather than looking the component up on every hit.
    private static PlayerOpenBar running;

    private PlayerController player;
    private PlayerHealth health;
    private float fraction;
    private OpenBarVFX vfx;

    // ⚠️ THE WINDOW IS A DEADLINE, NOT "WHILE THE COROUTINE LIVES". A coroutine stopped from outside
    // does not reliably run its finally, so a flag cleared at the end of Run could stay set forever
    // and the bar would never close. A timestamp closes itself.
    private float openUntil;
    public bool IsOpen => Time.time < openUntil;

    public static PlayerOpenBar For(PlayerController p)
    {
        PlayerOpenBar b = p.GetComponent<PlayerOpenBar>();
        return b != null ? b : p.gameObject.AddComponent<PlayerOpenBar>();
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
    }

    /// <param name="percent">The card's actionValue: the share of damage returned, in percent.</param>
    public IEnumerator Run(float percent)
    {
        fraction = Mathf.Clamp01(percent / 100f);
        openUntil = Time.time + Duration;
        running = this;
        vfx = OpenBarVFX.Spawn(player, Duration);   // times itself out; End() below just cuts it short
        Sfx.Play("Card.OpenBar", transform.position);

        while (IsOpen && health != null && !health.IsDead)
            yield return null;

        openUntil = 0f;
        if (vfx != null) vfx.End();
        vfx = null;
    }

    /// <summary>Called from RelicManager.ModifyPlayerDamage with the final damage of a hit about to land.</summary>
    public static void NoteDamage(float damage, EnemyHealth target)
    {
        PlayerOpenBar bar = running;
        if (bar == null || !bar.IsOpen || target == null || damage <= 0f) return;
        if (bar.health == null || bar.health.IsDead) return;
        if (target.CurrentHealth <= 0f) return;

        ShieldEnemy shield = target.GetComponent<ShieldEnemy>();
        if (shield != null && shield.IsBlocking()) return;

        float landed = Mathf.Min(damage, target.CurrentHealth);
        float heal = landed * bar.fraction;
        if (heal <= 0f) return;

        bar.health.Heal(heal);
        if (bar.vfx != null) bar.vfx.Draw(target.transform.position, heal);
    }
}
