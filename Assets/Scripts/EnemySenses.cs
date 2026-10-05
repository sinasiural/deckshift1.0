using UnityEngine;

// The one place an enemy decides whether it can actually SEE the player.
//
// ⚠️ WHY THIS EXISTS. Before it, no ground AI checked line of sight at all. Every one of them
// aggroed, chased and fired through solid rock:
//
//   · Spitters (aggro 11, range 8) lobbed acid through walls at a player they could not see.
//   · Melee enemies walked into the wall between them and the player and stayed there.
//   · Turrets fired forever with no range check whatsoever — see Turret.cs.
//
// AeroBat was the only enemy that ever raycast (its `obstacleLayer`), and it is not a coincidence
// that it is also the one that reads as competent.
//
// ── SIGHT MEMORY, and why a raw LOS check is not enough on its own ────────────────────────────
// Gating the chase on "can see right now" produces a WORSE enemy, not a better one: it freezes the
// moment the player steps behind a pillar, then unfreezes, which reads as a stutter rather than as
// awareness. So sight ACQUIRES a target and a short memory keeps it. An enemy that has seen you
// keeps coming for `Memory` seconds after losing you, which is how a person behaves and is enough
// to cross a pillar without noticing anything at all.
public static class EnemySenses
{
    // Cast from roughly the enemy's chest, not its feet: enemy transforms are grounded at floor
    // level by the level importer, so a ray from the origin starts inside the floor tile and can
    // report "blocked" while standing on open ground. Same class of bug as the player's wallCheck,
    // which sat below the capsule and returned true on flat ground.
    public const float DefaultEyeHeight = 1.0f;

    // Aim at the player's chest rather than their feet, for the same reason in reverse — a ray to
    // the feet clips the lip of whatever they are standing on.
    public const float PlayerChest = 0.8f;

    // How long an enemy keeps coming after losing sight.
    public const float Memory = 2.5f;

    // ⚠️ THE RAY STARTS THIS FAR ALONG, AND THAT IS LOAD-BEARING. `Physics2D.queriesStartInColliders`
    // is ON by default, so a ray whose ORIGIN sits inside any collider returns a hit at distance
    // 0.000 — permanently blind. It is not hypothetical: the bat hovers against ceilings and ledges,
    // and a bat overlapping a tile reported "blocked by Ground at distance 0.00" and never attacked
    // once, for as long as it lived. Exactly the bug that made the player's old wallCheck return true
    // while standing on flat open ground.
    //
    // 0.35 is safe against seeing THROUGH anything: level geometry is on a 1-unit grid, so a wall is
    // at least a full unit thick and the caster would have to be buried inside it for this to skip
    // past the far face.
    public const float StartSkip = 0.35f;

    // ⚠️ A LayerMask left unset serializes as 0, which as a raycast mask means "hit nothing" — so an
    // unconfigured field would silently disable line of sight entirely and restore exactly the bug
    // this file exists to fix. Falling back to Ground means a forgotten Inspector slot degrades to
    // CORRECT behaviour instead of to no behaviour. (This project's most expensive bug shape is code
    // that runs without error and does nothing; a guarded skip is still that bug.)
    public static LayerMask ResolveBlockers(LayerMask configured)
    {
        if (configured.value != 0) return configured;
        int ground = LayerMask.NameToLayer("Ground");
        return ground >= 0 ? (1 << ground) : 0;
    }

    public static bool CanSee(Transform self, Transform target, LayerMask blockers,
                              float eyeHeight = DefaultEyeHeight)
    {
        if (self == null || target == null) return false;
        if (PlayerHealth.IsHidden(target)) return false;   // the Ninja's Old Log Trick

        Vector2 eye = (Vector2)self.position + Vector2.up * eyeHeight;
        Vector2 aim = (Vector2)target.position + Vector2.up * PlayerChest;
        Vector2 delta = aim - eye;
        float dist = delta.magnitude;
        if (dist < 0.05f) return true;

        LayerMask mask = ResolveBlockers(blockers);
        if (mask.value == 0) return true;   // nothing can block; do not pretend to be blind

        Vector2 dir = delta / dist;
        float skip = Mathf.Min(StartSkip, dist * 0.5f);   // never skip past the target itself
        return Physics2D.Raycast(eye + dir * skip, dir, dist - skip, mask).collider == null;
    }

    // Convenience for the common shape: "am I allowed to act on the player right now?" — true if
    // visible, or if seen recently enough to still be pursuing. Callers stamp `lastSeen` themselves
    // so they can also use it for their own state (giving up, returning to patrol, and so on).
    public static bool IsAware(Transform self, Transform target, LayerMask blockers,
                               ref float lastSeen, float eyeHeight = DefaultEyeHeight)
    {
        // ⚠️ A VANISHED PLAYER IS FORGOTTEN, not merely unseen. Blocking sight alone would leave
        // every enemy coming for another `Memory` seconds, longer than the trick lasts, so the
        // vanish would change nothing. Wiping the memory means each enemy has to spot the player
        // again once the trick ends.
        if (PlayerHealth.IsHidden(target))
        {
            lastSeen = -1000f;
            return false;
        }

        if (CanSee(self, target, blockers, eyeHeight))
        {
            lastSeen = Time.time;
            return true;
        }
        return Time.time - lastSeen < Memory;
    }

    public static void DrawGizmo(Transform self, Transform target, LayerMask blockers,
                                 float eyeHeight = DefaultEyeHeight)
    {
        if (self == null || target == null) return;
        Vector2 eye = (Vector2)self.position + Vector2.up * eyeHeight;
        Vector2 aim = (Vector2)target.position + Vector2.up * PlayerChest;
        Gizmos.color = CanSee(self, target, blockers, eyeHeight)
            ? new Color(0.4f, 1f, 0.4f, 0.8f)
            : new Color(1f, 0.35f, 0.3f, 0.8f);
        Gizmos.DrawLine(eye, aim);
    }
}
