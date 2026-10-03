using System;

[Flags]
public enum ConflictFlags
{
    None                 = 0,
    GravityScale         = 1,
    TimeScale            = 2,
    MoveSpeed            = 4,
    LayerCollisionMatrix = 8,
    VisualTransform      = 16,
    PlayerVelocity       = 32,
    Invincibility        = 64,
    AnimatorAttackState  = 128,
    // Held by a running Brace, so a second Brace cannot stack another block on top of the first.
    Brace                = 256,
    // Held by a running Open Bar, so a second one is refused rather than stacking the heal.
    OpenBar              = 512,
}
