
public enum PlayerState
{
    Idle,
    Running,
    Jumping,
    Dashing,
    KnockedBack,
    WallSliding,
    InCannon,
    CometDiving,
    Swimming,
}

public enum CardActionType
{
    Jump             = 0,
    Dash             = 1,
    // 2 (DashBackward) intentionally retired
    // 3 (WallCling) intentionally retired
    // 4 (DrawCards) intentionally retired
    // 5 (GainJumpCharges) intentionally retired
    PlatformCreate   = 6,
    Fireball         = 7,
    Portal           = 8,
    VampiricBite     = 9,
    GlassWail        = 10,
    Phase            = 11,
    CometDive        = 12,
    Adrenaline       = 13,
    Stagger          = 14,
    ReverseGravity   = 15,
    GlassParry       = 16,
    DeadWeight       = 17,
    FreefallBlade    = 18,
    // "Second Thoughts". Two-stage like Portal, but the return end is always somewhere the player
    // has ALREADY STOOD, so unlike Portal it can never advance them through a room — which is why
    // it needs no range limit at all.
    ReturnAnchor     = 19,
    // The Ninja's card. Unlike Fireball it is AIMED: the shot flies toward the cursor, in any
    // direction, rather than straight ahead along the player's facing.
    Shuriken         = 20,
}

public enum SkillType
{
    None,
    InfinitySeal,   // (Eski favorimiz)
    EchoChamber,    // %50 �ift Etki
    SpectralWings,  // Bedava Air Jump
    Overclock,
    KineticDiscount// Kill = Sonraki Kart Bedava
}
public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary,

    // ⚠️ BOSS IS NOT A HIGHER LEGENDARY — it is a separate ACQUISITION CHANNEL, and it is the only
    // tier allowed to add a new keybind (designer, 2026-08-21: "the only relics that will be able
    // to do that will be the boss relics, which are really hard to get").
    //
    // ⚠️ IT MUST NEVER APPEAR IN A CHEST OR A SHOP. RelicPool.Offerable filters it out; the
    // rarity-fallback loop in PickOfferable already stops at Legendary, so it cannot be reached by
    // stepping up either. Added LAST so no existing serialized rarity shifts value.
    Boss
}