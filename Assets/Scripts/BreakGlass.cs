using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Break Glass" — in case of emergency. Playable only below 30 HP; then a wide burst of shards in
/// front of you hits everything there for the card's damage and staggers it.
///
/// Design: CardIdeas.md #6 (designer verdict 2026-09-08: liked). Glass's identity is that being
/// nearly dead is where your power is, and this is the purest statement of it: dead weight in hand
/// while you're winning, your best card at the worst moment of the run.
///
/// ⚠️ THE LOCK IS A REFUSED PLAY, NOT A COST. Above the threshold the action returns false, which
/// rides DeckManager's "failed play" path: no Shift spent, no charge lost, the card stays put (same
/// route as Dead Weight). The refusal plays a sound so pressing it isn't silently ignored, and the
/// aim preview turns red while it is locked.
///
/// ⚠️ STAGGER, NOT KNOCKBACK. The design asked for knockback, but the game has no enemy knockback:
/// bodies are mass 500 and several AIs zero their own velocity every frame, so a shove is undone on
/// the next physics step. The stagger is EnemyHealth.Stun — the same freeze Glass Wail uses, which
/// suits the archetype and buys the same breather. Bosses are staggered too (Glass Wail does it).
///
/// The geometry lives here ONCE: the cast and CardAimIndicator's preview both call InArc, so the
/// preview cannot promise a hit the card won't land.
/// </summary>
public static class BreakGlass
{
    /// <summary>Playable strictly below this much HP.</summary>
    public const float HealthThreshold = 30f;
    /// <summary>Reach of the burst, measured from the body's centre.</summary>
    public const float Radius = 4.5f;
    /// <summary>How far BEHIND the body still counts, so an enemy overlapping you is not spared.</summary>
    public const float BehindSlack = 0.6f;
    public const float StaggerSeconds = 0.6f;

    public static bool IsUnlocked(PlayerHealth health)
    {
        return health != null && health.CurrentHealth < HealthThreshold;
    }

    /// <summary>Where the burst starts: the middle of the player's capsule, not the feet.</summary>
    public static Vector2 Origin(PlayerController player)
    {
        CapsuleCollider2D cap = player.GetComponent<CapsuleCollider2D>();
        Vector2 offset = cap != null ? cap.offset : new Vector2(0f, 0.84f);
        return (Vector2)player.transform.position + offset;
    }

    /// <summary>Is this collider inside the half-disc in front of the player?</summary>
    public static bool InArc(Vector2 origin, float facing, Collider2D col)
    {
        Vector2 p = col.ClosestPoint(origin);
        if ((p - origin).sqrMagnitude > Radius * Radius) return false;
        return (p.x - origin.x) * facing >= -BehindSlack;
    }

    /// <summary>Every damageable thing the burst would hit right now.</summary>
    public static List<IDamageable> Targets(PlayerController player, Vector2 origin, float facing)
    {
        var struck = new List<IDamageable>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(origin, Radius, ~0))
        {
            if (hit.GetComponentInParent<PlayerController>() != null) continue;
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || struck.Contains(target)) continue;
            if (!InArc(origin, facing, hit)) continue;
            struck.Add(target);
        }
        return struck;
    }

    /// <summary>The play. Returns false (a refused play) while the player is above the threshold.</summary>
    public static bool Perform(PlayerController player, float damage)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (!IsUnlocked(health))
        {
            SfxManager.PlayOn(player.audioSource, ProcSfx.UIRefuse, 0.7f);
            return false;
        }

        float facing = player.isFacingRight ? 1f : -1f;
        Vector2 origin = Origin(player);

        // Shards first, so the burst is on screen during the hit-stop the first hit triggers.
        // Two layers: one glass sample alone measured about -18 dB, too thin for the card's moment.
        Sfx.Play("Card.BreakGlass", origin);
        Sfx.Play("Card.BreakGlass.Body", origin);
        GlassParryVFX.SpawnShatter(origin + new Vector2(facing * 1.2f, 0f));
        GlassParryVFX.SpawnShatter(origin + new Vector2(facing * 2.8f, 0.4f));
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.3f, 0.6f);

        foreach (IDamageable target in Targets(player, origin, facing))
        {
            EnemyHealth enemy = target as EnemyHealth;
            float dealt = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(damage, enemy)
                : damage;
            target.TakeDamage(dealt);

            // TakeDamage may have killed it this frame. ⚠️ Stun RESTARTS the timer, so re-stunning
            // an enemy already frozen by Glass Wail would cut that longer freeze down to this one.
            if (enemy != null && enemy.CurrentHealth > 0f && !enemy.IsStunned) enemy.Stun(StaggerSeconds);
        }
        return true;
    }
}
