using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Glass Moon" — playable only below 30 HP. A small glass moon rises over you, cracks, and bursts:
/// everything within reach of the burst takes the card's damage and is staggered.
///
/// History: built 2026-10-03 as "Break Glass" (CardIdeas.md #6, a half-disc of shards in front of
/// you). The designer kept the card's rules and replaced its fiction the same day: a moon that
/// shatters, which is also why the area is now a CIRCLE around the burst rather than a half-disc.
///
/// ⚠️ THE DAMAGE LANDS AT THE BURST, NOT ON THE KEYPRESS (BurstAt seconds later). Two consequences:
///   · The card that cast it is captured at cast and re-installed around the hits, exactly like
///     Fireball.sourceCard, or every damage blessing on it would silently do nothing.
///   · The moon stays where it was summoned. Dash away after casting and it still bursts there.
///     The aim preview shows that spot, so what you see is what you get at the moment you cast.
///
/// ⚠️ THE LOCK IS A REFUSED PLAY, NOT A COST. Above the threshold the action returns false: no
/// Shift, no charge, the card stays put. A refusal sound plays and the preview is red.
///
/// ⚠️ STAGGER, NOT KNOCKBACK. The game has no enemy knockback (bodies are mass 500 and several AIs
/// zero their own velocity every frame), so survivors get EnemyHealth.Stun, Glass Wail's freeze.
///
/// Geometry lives here ONCE: the cast, the burst and CardAimIndicator's preview all call it.
/// The look and the timeline live in GlassMoonVFX.
/// </summary>
public static class GlassMoon
{
    /// <summary>Playable strictly below this much HP.</summary>
    public const float HealthThreshold = 30f;
    /// <summary>Reach of the burst, from the moon's centre.</summary>
    public const float Radius = 4.4f;
    /// <summary>How high above the chest the moon rises (less under a low ceiling).</summary>
    public const float RiseHeight = 2.6f;
    /// <summary>A little ahead of you, on your facing.</summary>
    public const float ForwardNudge = 0.5f;
    /// <summary>The moon's own radius in world units (its sprite is 36 px at 32 px per unit).</summary>
    public const float MoonRadius = 0.56f;
    public const float StaggerSeconds = 0.6f;

    public static bool IsUnlocked(PlayerHealth health)
    {
        return health != null && health.CurrentHealth < HealthThreshold;
    }

    /// <summary>The middle of the player's capsule, not the feet.</summary>
    public static Vector2 Chest(PlayerController player)
    {
        CapsuleCollider2D cap = player.GetComponent<CapsuleCollider2D>();
        Vector2 offset = cap != null ? cap.offset : new Vector2(0f, 0.84f);
        return (Vector2)player.transform.position + offset;
    }

    /// <summary>Where the moon will burst if cast right now.</summary>
    public static Vector2 BurstPoint(PlayerController player)
    {
        Vector2 chest = Chest(player);
        // "Up" is away from whatever you are standing on, so it still rises off a reversed floor.
        Vector2 up = player.isGravityReversed ? Vector2.down : Vector2.up;
        float facing = player.isFacingRight ? 1f : -1f;

        // Under a low ceiling the moon stops short of the rock instead of bursting inside it.
        float height = RiseHeight;
        RaycastHit2D roof = Physics2D.Raycast(chest, up, RiseHeight + MoonRadius, LayerMask.GetMask("Ground"));
        if (roof.collider != null) height = Mathf.Max(0.9f, roof.distance - MoonRadius - 0.1f);

        return chest + up * height + new Vector2(facing * ForwardNudge, 0f);
    }

    /// <summary>Every damageable thing a burst at <paramref name="at"/> would hit.</summary>
    public static List<IDamageable> Targets(Vector2 at)
    {
        var struck = new List<IDamageable>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(at, Radius, ~0))
        {
            if (hit.GetComponentInParent<PlayerController>() != null) continue;
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || struck.Contains(target)) continue;
            struck.Add(target);
        }
        return struck;
    }

    /// <summary>The play. Returns false (a refused play) while the player is above the threshold.</summary>
    public static bool Cast(PlayerController player, float damage)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (!IsUnlocked(health))
        {
            SfxManager.PlayOn(player.audioSource, ProcSfx.UIRefuse, 0.7f);
            return false;
        }

        RuntimeCard source = DeckManager.instance != null ? DeckManager.instance.AttributedCard : null;
        GlassMoonVFX.Spawn(Chest(player), BurstPoint(player), damage, source);
        return true;
    }

    /// <summary>The burst itself. Called by GlassMoonVFX on the frame the moon shatters.</summary>
    public static void Strike(Vector2 at, float damage, RuntimeCard source)
    {
        DeckManager deck = DeckManager.instance;
        RuntimeCard prev = deck != null ? deck.AttributedCard : null;
        if (deck != null) deck.AttributedCard = source;
        try
        {
            foreach (IDamageable target in Targets(at))
            {
                EnemyHealth enemy = target as EnemyHealth;
                float dealt = RelicManager.instance != null
                    ? RelicManager.instance.ModifyPlayerDamage(damage, enemy)
                    : damage;
                target.TakeDamage(dealt);

                // ⚠️ Stun RESTARTS its timer, so re-stunning an enemy already frozen by Glass Wail
                // would cut that longer freeze down to this one.
                if (enemy != null && enemy.CurrentHealth > 0f && !enemy.IsStunned) enemy.Stun(StaggerSeconds);
            }
        }
        finally
        {
            // A stale attribution would hand the next spike or pogo bounce a blessing it never earned.
            if (deck != null) deck.AttributedCard = prev;
        }
    }
}
