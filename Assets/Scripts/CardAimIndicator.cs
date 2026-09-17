using System.Collections.Generic;
using UnityEngine;

// House-style aim/targeting indicator (no prefab, no art — self-building, same pattern as
// DashAfterimage / EnemyHealthBar). Watches DeckManager's selected card each frame and shows
// an honest preview of what the card will actually do when cast:
//
//   Fireball      -> ember dots flowing along the TRUE flight line (capsule-cast with the real
//                    fireball collider, so short targets register) + a pulsing impact ring.
//                    Ring runs hot red when the impact would be an enemy, orange for walls.
//   Dash          -> afterimage trail: frozen silhouettes fading in along the dash path,
//                    strongest at the TRUE end point (dashSpeed * dashDuration, wall-clamped
//                    with the player capsule) — the look the real dash leaves, in advance.
//   VampiricBite  -> aura ring at the real bite radius around the BODY center; green when an
//                    enemy is inside (the play lands), dim red when it would be refused.
//   Portal        -> ghost portal following the cursor from the moment the card is selected;
//                    while the second placement is pending it tints by in-range validity.
//   PlatformCreate-> ghost of the platform prefab (true art, true size) following the cursor.
//   FreefallBlade -> the real ")" slash circle (forward-and-low); grows while falling
//                    (bigger empowered arc) and turns green when an enemy is inside.
//   GlassWail     -> expanding ripples from the player + a glint over every enemy that
//                    would be stunned (the wail is screen-wide).
//
// Every indicator dims when the player can't afford the card's EFFECTIVE Shift cost (mirrors
// DeckManager.PlayCard: KineticDiscount and First One's Free included). Hidden while paused,
// dead, or when a non-indicator card (or nothing) is selected.
//
// Lives on the Player prefab root. All visuals are children of this transform (root scale is
// a hard (1,1,1) so world-space positioning is safe).
public class CardAimIndicator : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private int sortingOrder = 40;                 // above ground tiles (order 1), below UI
    [Range(0.05f, 1f)]
    [SerializeField] private float unaffordableDim = 0.3f;          // alpha multiplier when Shift can't pay the card

    [Header("Fireball")]
    [SerializeField] private int emberCount = 10;
    [SerializeField] private float emberFlowSpeed = 7f;             // world units per second along the line
    [SerializeField] private float emberSize = 0.14f;
    [SerializeField] private Color emberColor = new Color(1f, 0.62f, 0.18f, 0.9f);
    [SerializeField] private float impactRingSize = 0.55f;
    [SerializeField] private Color impactWallColor = new Color(1f, 0.55f, 0.15f, 0.8f);
    [SerializeField] private Color impactEnemyColor = new Color(1f, 0.2f, 0.1f, 0.95f);

    [Header("Dash")]
    [SerializeField] private int dashTrailSteps = 4;                // silhouettes along the path (last = destination)

    [Header("Vampiric Bite")]
    [SerializeField] private Color biteValidColor = new Color(0.35f, 1f, 0.45f, 0.85f);
    [SerializeField] private Color biteInvalidColor = new Color(1f, 0.25f, 0.25f, 0.4f);
    [SerializeField] private float biteRingWidth = 0.05f;

    [Header("Portal")]
    [SerializeField] private Color portalFirstColor = new Color(0.85f, 0.85f, 0.85f, 0.6f);
    [SerializeField] private Color portalValidColor = new Color(0.3f, 0.95f, 1f, 0.65f);
    [SerializeField] private Color portalInvalidColor = new Color(1f, 0.25f, 0.25f, 0.6f);

    [Header("Platform Create")]
    [SerializeField] private Color platformGhostColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Freefall Blade")]
    [SerializeField] private Color freefallNeutralColor = new Color(0.8f, 0.9f, 1f, 0.55f);
    [SerializeField] private Color freefallFallingColor = new Color(1f, 0.55f, 0.15f, 0.85f);  // empowered: 2x dmg + bigger arc
    [SerializeField] private Color freefallHitColor = new Color(0.35f, 1f, 0.45f, 0.85f);      // an enemy is inside the arc
    [SerializeField] private float freefallRingWidth = 0.05f;

    [Header("Glass Wail")]
    [SerializeField] private Color wailColor = new Color(0.75f, 0.95f, 1f, 0.6f);
    [SerializeField] private float wailRippleMaxRadius = 6f;
    [SerializeField] private float wailRipplePeriod = 1.1f;

    [Header("Phase")]
    // Matches PhaseBoundary's violet so the preview and the live bubble read as the same thing.
    [SerializeField] private Color phaseColor = new Color(0.72f, 0.55f, 1f, 0.75f);
    [SerializeField] private float phaseRingWidth = 0.05f;

    [Header("Return Anchor")]
    // Amber, matching ReturnAnchorVFX — and deliberately clear of the violet Phase bubble and the
    // cyan portal ring, so the three world markers never read as each other.
    [SerializeField] private Color anchorColor = new Color(1f, 0.72f, 0.28f, 0.85f);

    [Header("Shuriken")]
    // Cold steel, deliberately clear of Fireball's orange: the two are both "a shot from here to
    // there" and the only difference is that this one goes where you point, so they must not read
    // as the same card.
    [SerializeField] private Color shurikenColor = new Color(0.82f, 0.88f, 0.96f, 0.8f);
    [SerializeField] private Color shurikenEnemyColor = new Color(1f, 0.28f, 0.2f, 0.95f);
    [SerializeField] private int shurikenDots = 9;

    private enum Kind { None, Fireball, Dash, Bite, Portal, Platform, Freefall, Wail, Phase, Anchor, Shuriken }
    private Kind activeKind = Kind.None;

    // All layers, triggers included — same as the game code's OverlapCircleAll(..., ~0).
    private static readonly ContactFilter2D NoFilter = new ContactFilter2D().NoFilter();

    private PlayerController player;
    private PlayerHealth playerHealth;
    private CapsuleCollider2D playerCapsule;
    private Camera cam;                                             // cached: Camera.main is slow and can be null

    // --- Fireball visuals + cached prefab facts ---
    private GameObject fireballRoot;
    private SpriteRenderer[] embers;
    private SpriteRenderer impactRing;
    private bool fireballParamsCached;
    private float fbMaxRange = 30f;
    private Vector2 fbCapsuleSize = new Vector2(0.3f, 1f);
    private Vector2 fbCapsuleOffset;
    private CapsuleDirection2D fbCapsuleDir = CapsuleDirection2D.Vertical;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];

    // --- Dash visuals ---
    // The Cainos pixel character body is SkinnedMeshRenderers (16 parts: Body, Hair, Hat...);
    // only the staff is a SpriteRenderer. Skinned parts are baked (BakeMesh) once per frame
    // into shared meshes; each trail step renders them via MeshRenderer ghost copies.
    private GameObject dashRoot;
    private readonly List<Renderer> ghostSources = new List<Renderer>();       // SpriteRenderer or SkinnedMeshRenderer
    private readonly List<Mesh> sourceBakes = new List<Mesh>();                // parallel; null for sprite sources
    private readonly List<Renderer[]> trailSteps = new List<Renderer[]>();     // [step][srcIndex] ghost copies
    private readonly List<Material[]> trailStepMats = new List<Material[]>();  // parallel; null for sprite ghosts

    // --- Bite visuals ---
    private GameObject biteRoot;
    private LineRenderer biteRing;
    private SpriteRenderer biteFill;
    private const int BITE_SEGMENTS = 48;
    private float biteScanTimer;
    private bool biteValid;
    private readonly Collider2D[] overlapHits = new Collider2D[16];

    // --- Portal visuals ---
    private GameObject portalRoot;
    private SpriteRenderer portalGhost;
    private LineRenderer portalBound;          // the bubble the next portal must land inside
    private bool portalBoundsBuilt;

    // --- Platform visuals ---
    private GameObject platformRoot;
    private readonly List<SpriteRenderer> platformGhostParts = new List<SpriteRenderer>();

    // --- Freefall Blade visuals ---
    private GameObject freefallRoot;
    private LineRenderer freefallRing;
    private SpriteRenderer freefallFill;
    private float freefallScanTimer;
    private bool freefallHit;

    // --- Phase visuals ---
    private GameObject phaseRoot;
    private LineRenderer phaseRing;
    private SpriteRenderer phaseFill;

    // --- Shuriken visuals ---
    private GameObject shurikenRoot;
    private SpriteRenderer[] shurikenDotsSR;
    private SpriteRenderer shurikenHitRing;

    // --- Return Anchor visuals ---
    private GameObject anchorRoot;
    private LineRenderer anchorTether;         // player -> anchor, so you can find it from anywhere
    private LineRenderer anchorDecal;          // the small flat spot on the ground
    private LineRenderer[] anchorRings;        // contracting rings — the "you come back here" motion
    private SpriteRenderer anchorCore;
    private const int ANCHOR_SEGMENTS = 40;
    // ⚠️ FLAT ENOUGH TO BE ON THE FLOOR. At 0.4 the outer ring reached the player's waist and read as
    // a hoop AROUND the character instead of a mark beneath them — a ground decal in a side-on game
    // has to be much flatter than the maths suggests before the eye puts it on the floor plane.
    private const float ANCHOR_FLATTEN = 0.24f;
    private const float ANCHOR_SPOT_R = 0.62f; // where the rings land, and the size of the decal
    private const float ANCHOR_RING_START = 1.8f;
    private const float ANCHOR_RING_PERIOD = 1.5f;

    // --- Glass Wail visuals ---
    private GameObject wailRoot;
    private SpriteRenderer[] wailRipples;
    private readonly List<SpriteRenderer> wailGlints = new List<SpriteRenderer>();
    private EnemyHealth[] wailTargets;
    private float wailScanTimer;

    // --- Shared procedural resources (built once, survive across instances) ---
    private static Sprite cachedDotSprite;
    private static Sprite cachedRingSprite;
    private static Material cachedLineMaterial;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerCapsule = GetComponent<CapsuleCollider2D>();
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        DeckManager deck = DeckManager.instance;
        if (deck == null || player == null) { SetKind(Kind.None); return; }
        if (!GameSettings.CardAimPreview) { SetKind(Kind.None); return; }               // player turned previews off
        if (Time.timeScale == 0f) { SetKind(Kind.None); return; }                       // paused / full-screen UI open
        if (playerHealth != null && playerHealth.IsDead) { SetKind(Kind.None); return; }

        int idx = deck.GetSelectedIndex();
        List<RuntimeCard> hand = deck.GetCurrentHand();
        if (idx < 0 || idx >= hand.Count) { SetKind(Kind.None); return; }

        RuntimeCard card = hand[idx];
        float dim = CanAfford(deck, card) ? 1f : unaffordableDim;

        switch (card.cardData.actionType)
        {
            case CardActionType.Fireball:       SetKind(Kind.Fireball); UpdateFireball(dim); break;
            case CardActionType.Dash:           SetKind(Kind.Dash);     UpdateDash(dim);     break;
            case CardActionType.VampiricBite:   SetKind(Kind.Bite);     UpdateBite(dim);     break;
            case CardActionType.Portal:         SetKind(Kind.Portal);   UpdatePortal(dim);   break;
            case CardActionType.PlatformCreate: SetKind(Kind.Platform); UpdatePlatform(dim); break;
            case CardActionType.FreefallBlade:  SetKind(Kind.Freefall); UpdateFreefall(dim); break;
            case CardActionType.GlassWail:      SetKind(Kind.Wail);     UpdateWail(dim);     break;
            case CardActionType.Phase:          SetKind(Kind.Phase);    UpdatePhase(dim);    break;
            case CardActionType.ReturnAnchor:   SetKind(Kind.Anchor);   UpdateAnchor(dim);   break;
            case CardActionType.Shuriken:       SetKind(Kind.Shuriken); UpdateShuriken(dim); break;
            case CardActionType.ThroughAndThrough:
                                                SetKind(Kind.Dash);     UpdateDash(dim, true); break;
            default:                            SetKind(Kind.None);                          break;
        }
    }

    // Mirrors DeckManager.PlayCard's affordability gate exactly (KineticDiscount, Blompo's
    // "On the House", First One's Free). Note the gate applies even in the hub — only the SPEND
    // is hub-exempt. If you change the cost maths in PlayCard, change it here too or the
    // indicator's affordability dimming becomes a lie.
    private bool CanAfford(DeckManager deck, RuntimeCard card)
    {
        int cost = card.cardData.shiftCost;
        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
            cost = Mathf.Max(0, cost - 1);
        // Same function PlayCard charges through, so the dimming cannot drift from the real price.
        cost = Mathf.Max(0, CardEnhancements.EffectiveCost(card, cost));
        if (deck.isNextCardFree) cost = 0;
        return player.GetCurrentShift() >= cost;
    }

    // Activates the container for `kind`, hides all others. Containers are built lazily.
    private void SetKind(Kind kind)
    {
        if (activeKind == kind) return;
        activeKind = kind;

        if (fireballRoot != null) fireballRoot.SetActive(kind == Kind.Fireball);
        if (dashRoot != null) dashRoot.SetActive(kind == Kind.Dash);
        if (biteRoot != null) biteRoot.SetActive(kind == Kind.Bite);
        if (portalRoot != null) portalRoot.SetActive(kind == Kind.Portal);
        if (platformRoot != null) platformRoot.SetActive(kind == Kind.Platform);
        if (freefallRoot != null) freefallRoot.SetActive(kind == Kind.Freefall);
        if (wailRoot != null) wailRoot.SetActive(kind == Kind.Wail);
        if (phaseRoot != null) phaseRoot.SetActive(kind == Kind.Phase);
        if (anchorRoot != null) anchorRoot.SetActive(kind == Kind.Anchor);
        if (shurikenRoot != null) shurikenRoot.SetActive(kind == Kind.Shuriken);

        switch (kind)
        {
            case Kind.Fireball: EnsureFireballVisuals(); fireballRoot.SetActive(true); break;
            case Kind.Dash:     EnsureDashVisuals();     RebuildDashTrail(); dashRoot.SetActive(true); break;
            case Kind.Bite:     EnsureBiteVisuals();     biteScanTimer = 0f; biteRoot.SetActive(true); break;
            case Kind.Portal:   EnsurePortalVisuals();   if (portalRoot != null) portalRoot.SetActive(true); break;
            case Kind.Platform: EnsurePlatformVisuals(); if (platformRoot != null) platformRoot.SetActive(true); break;
            case Kind.Freefall: EnsureFreefallVisuals(); freefallScanTimer = 0f; freefallRoot.SetActive(true); break;
            case Kind.Wail:     EnsureWailVisuals();     wailScanTimer = 0f; wailRoot.SetActive(true); break;
            case Kind.Phase:    EnsurePhaseVisuals();    phaseRoot.SetActive(true); break;
            case Kind.Anchor:   EnsureAnchorVisuals();   anchorRoot.SetActive(true); break;
            case Kind.Shuriken: EnsureShurikenVisuals(); shurikenRoot.SetActive(true); break;
        }
    }

    // ------------------------------------------------------------------ FIREBALL

    private void EnsureFireballVisuals()
    {
        if (fireballRoot != null) return;

        fireballRoot = MakeContainer("Aim_Fireball");

        embers = new SpriteRenderer[Mathf.Max(2, emberCount)];
        for (int i = 0; i < embers.Length; i++)
        {
            embers[i] = MakeSpriteChild(fireballRoot.transform, "Ember" + i, GetDotSprite(), sortingOrder);
            embers[i].transform.localScale = Vector3.one * emberSize;
        }

        impactRing = MakeSpriteChild(fireballRoot.transform, "ImpactRing", GetRingSprite(), sortingOrder + 1);
    }

    // The real flight facts live on the Fireball prefab; read them once.
    private void CacheFireballParams()
    {
        if (fireballParamsCached || player.fireballPrefab == null) return;
        fireballParamsCached = true;

        Fireball fb = player.fireballPrefab.GetComponent<Fireball>();
        if (fb != null) fbMaxRange = fb.speed * fb.lifeTime;

        CapsuleCollider2D col = player.fireballPrefab.GetComponent<CapsuleCollider2D>();
        if (col != null)
        {
            fbCapsuleSize = col.size;
            fbCapsuleOffset = col.offset;
            fbCapsuleDir = col.direction;
        }
    }

    private void UpdateFireball(float dim)
    {
        if (player.fireballPrefab == null || player.firePoint == null) { fireballRoot.SetActive(false); return; }
        if (!fireballRoot.activeSelf) fireballRoot.SetActive(true);
        CacheFireballParams();

        float dir = player.isFacingRight ? 1f : -1f;
        Vector2 start = player.firePoint.position;

        // Cast the true fireball capsule (yaw-180 flip mirrors the collider offset's X).
        Vector2 origin = start + new Vector2(fbCapsuleOffset.x * dir, fbCapsuleOffset.y);
        int hitCount = Physics2D.CapsuleCast(origin, fbCapsuleSize, fbCapsuleDir, 0f,
            new Vector2(dir, 0f), NoFilter, castHits, fbMaxRange);

        // Mirror Fireball.OnTriggerEnter2D's rules: skip own body and portals; damageables stop
        // it (even triggers); other triggers pass through; anything solid left is a wall.
        bool found = false;
        bool enemyHit = false;
        Vector2 end = start + new Vector2(dir * fbMaxRange, 0f);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = castHits[i].collider;
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (col.GetComponent<Portal>() != null) continue;
            IDamageable dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null && col.isTrigger) continue;

            found = true;
            enemyHit = dmg != null;
            end = castHits[i].point;
            break;
        }

        // Ember dots drifting from wand to impact at a constant world speed.
        float len = Mathf.Max(Vector2.Distance(start, end), 0.01f);
        float cyclesPerSecond = emberFlowSpeed / len;
        for (int i = 0; i < embers.Length; i++)
        {
            float phase = Mathf.Repeat(Time.unscaledTime * cyclesPerSecond + (float)i / embers.Length, 1f);
            Vector2 p = Vector2.Lerp(start, end, phase);
            embers[i].transform.position = new Vector3(p.x, p.y, 0f);

            Color c = emberColor;
            c.a *= (0.2f + 0.8f * Mathf.Sin(phase * Mathf.PI)) * dim;   // fade in from the wand, out at impact
            embers[i].color = c;
        }

        // Pulsing impact ring; hot red = the shot would land on an enemy.
        impactRing.enabled = found;
        if (found)
        {
            float t = Time.unscaledTime;
            impactRing.transform.position = new Vector3(end.x, end.y, 0f);
            impactRing.transform.localScale = Vector3.one * (impactRingSize * (1f + 0.15f * Mathf.Sin(t * 7f)));

            Color rc = enemyHit ? impactEnemyColor : impactWallColor;
            rc.a *= (0.75f + 0.25f * Mathf.Sin(t * 7f)) * dim;
            impactRing.color = rc;
        }
    }

    // ------------------------------------------------------------------ SHURIKEN

    private void EnsureShurikenVisuals()
    {
        if (shurikenRoot != null) return;

        shurikenRoot = MakeContainer("Aim_Shuriken");

        shurikenDotsSR = new SpriteRenderer[Mathf.Max(3, shurikenDots)];
        for (int i = 0; i < shurikenDotsSR.Length; i++)
            shurikenDotsSR[i] = MakeSpriteChild(shurikenRoot.transform, "Dot" + i, GetDotSprite(), sortingOrder);

        shurikenHitRing = MakeSpriteChild(shurikenRoot.transform, "HitRing", GetRingSprite(), sortingOrder + 1);
    }

    // ⚠️ THIS MUST MIRROR PlayerController.ThrowShuriken EXACTLY — same origin, same aim, same
    // range. It is the only promise the player gets before committing a charge, and an aimed card
    // whose preview disagrees with its shot is worse than no preview at all.
    private void UpdateShuriken(float dim)
    {
        Camera c = Camera.main;
        if (c == null) { shurikenRoot.SetActive(false); return; }
        if (!shurikenRoot.activeSelf) shurikenRoot.SetActive(true);

        Vector2 origin = player.ShurikenOrigin;
        Vector2 aim = (Vector2)c.ScreenToWorldPoint(Input.mousePosition) - origin;
        if (aim.sqrMagnitude < 0.0001f) aim = new Vector2(player.isFacingRight ? 1f : -1f, 0f);
        aim.Normalize();

        const float MAX_RANGE = 22f * 1.3f;   // Shuriken.SPEED * Shuriken.LIFE
        const float RADIUS = 0.42f * 0.55f;   // collider radius * spawn scale

        Vector2 start = origin + aim * 0.45f;
        Vector2 end = start + aim * MAX_RANGE;
        bool enemy = false;

        int n = Physics2D.CircleCast(start, RADIUS, aim, NoFilter, castHits, MAX_RANGE);
        for (int i = 0; i < n; i++)
        {
            Collider2D col = castHits[i].collider;
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (col.GetComponent<Portal>() != null) continue;
            IDamageable dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null && col.isTrigger) continue;      // pickups and zones are passed through

            enemy = dmg != null;
            end = castHits[i].point;
            break;
        }

        // Evenly spaced dots along the true flight line, thinning toward the far end so the line
        // reads as a throw rather than as a laser sight.
        float len = Vector2.Distance(start, end);
        Color line = enemy ? shurikenEnemyColor : shurikenColor;
        for (int i = 0; i < shurikenDotsSR.Length; i++)
        {
            float t = (i + 0.5f) / shurikenDotsSR.Length;
            Vector2 p = Vector2.Lerp(start, end, t);
            shurikenDotsSR[i].transform.position = new Vector3(p.x, p.y, 0f);
            shurikenDotsSR[i].transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.07f, t);

            Color dc = line;
            dc.a *= (1f - 0.55f * t) * dim;
            shurikenDotsSR[i].color = dc;
        }

        // The impact mark only appears when the throw actually meets something — an endpoint ring
        // hanging in empty air 28 units away would claim a hit that is not going to happen.
        bool lands = len < MAX_RANGE - 0.05f;
        shurikenHitRing.enabled = lands;
        if (lands)
        {
            float t = Time.unscaledTime;
            shurikenHitRing.transform.position = new Vector3(end.x, end.y, 0f);
            shurikenHitRing.transform.localScale = Vector3.one * (0.42f * (1f + 0.15f * Mathf.Sin(t * 8f)));
            Color rc = line;
            rc.a *= (0.75f + 0.25f * Mathf.Sin(t * 8f)) * dim;
            shurikenHitRing.color = rc;
        }
    }

    // ------------------------------------------------------------------ DASH

    private void EnsureDashVisuals()
    {
        if (dashRoot != null) return;
        dashRoot = MakeContainer("Aim_Dash");
    }

    // Builds `dashTrailSteps` silhouette sets — one ghost copy of every visual-model renderer
    // per step. Poses are refreshed every frame in UpdateDash; the alpha ramp along the path
    // makes them read as a motion streak (the dash's own afterimage look), not a twin.
    private void RebuildDashTrail()
    {
        ReleaseDashTrail();
        for (int i = dashRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(dashRoot.transform.GetChild(i).gameObject);

        if (player.visualModel == null) return;

        // Collect sources: skinned body parts + plain sprites (the staff). Particles skipped.
        foreach (Renderer r in player.visualModel.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SkinnedMeshRenderer)
            {
                ghostSources.Add(r);
                sourceBakes.Add(new Mesh());
            }
            else if (r is SpriteRenderer)
            {
                ghostSources.Add(r);
                sourceBakes.Add(null);
            }
        }

        int steps = Mathf.Max(2, dashTrailSteps);
        for (int s = 0; s < steps; s++)
        {
            var stepRoot = new GameObject("Trail" + s);
            stepRoot.transform.SetParent(dashRoot.transform, false);

            var copies = new Renderer[ghostSources.Count];
            var mats = new Material[ghostSources.Count];
            for (int i = 0; i < ghostSources.Count; i++)
            {
                var go = new GameObject(ghostSources[i].name + "_aimGhost");
                go.transform.SetParent(stepRoot.transform, false);

                if (ghostSources[i] is SkinnedMeshRenderer smr)
                {
                    // Mesh ghost: all steps share the per-frame bake; each step owns a
                    // Sprites/Default material carrying the part's texture + step alpha.
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = sourceBakes[i];

                    var mat = new Material(GetLineMaterial().shader);
                    if (smr.sharedMaterial != null) mat.mainTexture = smr.sharedMaterial.mainTexture;
                    mats[i] = mat;

                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.sortingLayerID = smr.sortingLayerID;
                    mr.sortingOrder = smr.sortingOrder - 1;   // just behind the real character
                    copies[i] = mr;
                }
                else
                {
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingLayerID = ghostSources[i].sortingLayerID;
                    sr.sortingOrder = ghostSources[i].sortingOrder - 1;
                    copies[i] = sr;
                }
            }
            trailSteps.Add(copies);
            trailStepMats.Add(mats);
        }
    }

    // Baked meshes and ghost materials are runtime-created assets — destroy them explicitly.
    private void ReleaseDashTrail()
    {
        foreach (Mesh m in sourceBakes) if (m != null) Destroy(m);
        foreach (Material[] mats in trailStepMats)
            foreach (Material m in mats) if (m != null) Destroy(m);
        ghostSources.Clear();
        sourceBakes.Clear();
        trailSteps.Clear();
        trailStepMats.Clear();
    }

    private void OnDestroy()
    {
        ReleaseDashTrail();
    }

    // `lunge` switches to Through and Through's travel, which is the same preview in every respect
    // except how far it reaches — that card IS a dash, so it gets the dash's ghost trail rather
    // than a second visual language for the same motion.
    private void UpdateDash(float dim, bool lunge = false)
    {
        float dir = player.isFacingRight ? 1f : -1f;
        float dist = lunge ? player.lungeSpeed * player.lungeDuration
                           : player.dashSpeed  * player.dashDuration;

        // Wall-clamp with the player's own capsule so the trail never previews standing in rock.
        if (playerCapsule != null)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(player.groundLayer);
            int n = playerCapsule.Cast(new Vector2(dir, 0f), filter, castHits, dist);
            for (int i = 0; i < n; i++)
                dist = Mathf.Min(dist, Mathf.Max(0f, castHits[i].distance - 0.02f));
        }

        // Bake each visible skinned part once; all trail steps share the result.
        for (int i = 0; i < ghostSources.Count; i++)
        {
            if (ghostSources[i] is SkinnedMeshRenderer smr && sourceBakes[i] != null
                && smr.enabled && smr.gameObject.activeInHierarchy)
                smr.BakeMesh(sourceBakes[i], true);   // includes transform scale (carries the facing flip)
        }

        // Silhouettes spaced along the path, faint near the player, strongest at the destination.
        Color tint = player.dashAfterimageTint;
        for (int s = 0; s < trailSteps.Count; s++)
        {
            float t = (s + 1f) / trailSteps.Count;
            Vector3 offset = new Vector3(dir * dist * t, 0f, 0f);
            Color c = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Lerp(0.25f, 0.85f, t) * dim);

            Renderer[] copies = trailSteps[s];
            Material[] mats = trailStepMats[s];
            for (int i = 0; i < ghostSources.Count && i < copies.Length; i++)
            {
                Renderer src = ghostSources[i];
                Renderer copy = copies[i];
                if (src == null || copy == null) continue;

                bool visible = src.enabled && src.gameObject.activeInHierarchy;
                if (src is SpriteRenderer srcSprite && srcSprite.sprite == null) visible = false;
                copy.enabled = visible;
                if (!visible) continue;

                if (copy is SpriteRenderer copySprite && src is SpriteRenderer srcSr)
                {
                    copySprite.sprite = srcSr.sprite;
                    copySprite.flipX = srcSr.flipX;
                    copySprite.flipY = srcSr.flipY;
                    copySprite.color = c;
                    copy.transform.localScale = src.transform.lossyScale;   // carries the facing flip
                }
                else if (mats[i] != null)
                {
                    mats[i].color = c;
                    copy.transform.localScale = Vector3.one;   // scale is baked into the mesh
                }

                copy.transform.position = src.transform.position + offset;
                copy.transform.rotation = src.transform.rotation;
            }
        }
    }

    // ------------------------------------------------------------------ VAMPIRIC BITE

    private void EnsureBiteVisuals()
    {
        if (biteRoot != null) return;

        biteRoot = MakeContainer("Aim_Bite");

        biteRing = MakeLineChild(biteRoot.transform, "Ring", biteRingWidth, sortingOrder);
        biteRing.loop = true;
        biteRing.positionCount = BITE_SEGMENTS;

        biteFill = MakeSpriteChild(biteRoot.transform, "Fill", GetDotSprite(), sortingOrder - 1);
    }

    private void UpdateBite(float dim)
    {
        // Same center as PerformVampiricBite: a regular circle around the body, not the wand.
        Vector2 center = player.BiteCenter;
        float radius = player.biteRange;

        // Revalidate on a short timer — mirrors PerformVampiricBite's exact filter
        // (all layers, IDamageable in parents; the player itself is not IDamageable).
        biteScanTimer -= Time.unscaledDeltaTime;
        if (biteScanTimer <= 0f)
        {
            biteScanTimer = 0.08f;
            biteValid = false;
            int n = Physics2D.OverlapCircle(center, radius, NoFilter, overlapHits);
            for (int i = 0; i < n; i++)
            {
                if (overlapHits[i].GetComponentInParent<IDamageable>() == null) continue;
                biteValid = true;
                break;
            }
        }

        // Gentle breathing only while the bite would land.
        float breath = biteValid ? 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
        float r = radius * breath;
        for (int i = 0; i < BITE_SEGMENTS; i++)
        {
            float a = (float)i / BITE_SEGMENTS * Mathf.PI * 2f;
            biteRing.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, 0f));
        }

        Color rc = biteValid ? biteValidColor : biteInvalidColor;
        biteRing.startColor = biteRing.endColor = new Color(rc.r, rc.g, rc.b, rc.a * dim);

        biteFill.transform.position = new Vector3(center.x, center.y, 0f);
        biteFill.transform.localScale = Vector3.one * (r * 2f);
        biteFill.color = new Color(rc.r, rc.g, rc.b, 0.07f * dim);
    }

    // ------------------------------------------------------------------ PORTAL

    private void EnsurePortalVisuals()
    {
        if (portalRoot != null) return;
        if (player.portalPrefab == null) return;

        portalBoundsBuilt = false;

        // Steal the portal's look straight from the prefab so the ghost always matches.
        Portal prefabPortal = player.portalPrefab.GetComponent<Portal>();
        SpriteRenderer srcSr = prefabPortal != null && prefabPortal.spriteRenderer != null
            ? prefabPortal.spriteRenderer
            : player.portalPrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (srcSr == null || srcSr.sprite == null) return;

        portalRoot = MakeContainer("Aim_Portal");

        portalGhost = MakeSpriteChild(portalRoot.transform, "PortalGhost", srcSr.sprite, sortingOrder);
        portalGhost.sortingLayerID = srcSr.sortingLayerID;
        portalGhost.sortingOrder = srcSr.sortingOrder + 5;
        portalGhost.transform.localScale = srcSr.transform.lossyScale;

        // The bubble the next portal must land inside. Same LineRenderer treatment as the Phase
        // boundary — a constant world-space width stays crisp at both the small place radius and
        // the much larger link radius, where a scaled sprite ring would go soft.
        portalBound = MakeLineChild(portalRoot.transform, "Bound", 0.06f, sortingOrder - 1);
        portalBound.loop = true;
        portalBound.positionCount = BITE_SEGMENTS;
        portalBoundsBuilt = true;
    }

    // Shows BOTH halves of the placement rule the player is currently subject to: the bubble the
    // portal must land inside, and whether the exact spot under the cursor would be accepted.
    //
    // ⚠️ Validity is asked of PlayerController.IsPortalPlacementValid — the same method
    // TryPlacePortal itself calls — rather than re-deriving the distance test here. This preview and
    // the click can therefore never disagree, which matters more for Portal than for any other card:
    // a refused placement costs nothing but looks identical to a bug if the ghost said it was fine.
    private void UpdatePortal(float dim)
    {
        if (portalRoot == null || portalGhost == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        portalGhost.transform.position = new Vector3(mouse.x, mouse.y, 0f);

        // Before the first placement the bubble is around the PLAYER (portalPlaceRange); once the
        // first portal is down it becomes the hop radius around THAT portal (portalMaxRange).
        Portal first = player.FirstPortalInstance;
        Vector2 center = first == null ? player.BiteCenter : (Vector2)first.transform.position;
        float radius = first == null ? player.portalPlaceRange : player.portalMaxRange;

        bool valid = player.IsPortalPlacementValid(mouse);

        Color c = valid ? (first == null ? portalFirstColor : portalValidColor) : portalInvalidColor;
        c.a *= (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)) * dim;
        portalGhost.color = c;

        if (portalBoundsBuilt && portalBound != null)
        {
            float breath = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 2.4f);
            float r = radius * breath;
            for (int i = 0; i < BITE_SEGMENTS; i++)
            {
                float a = (float)i / BITE_SEGMENTS * Mathf.PI * 2f;
                portalBound.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, 0f));
            }
            Color bc = valid ? portalValidColor : portalInvalidColor;
            portalBound.startColor = portalBound.endColor = new Color(bc.r, bc.g, bc.b, bc.a * 0.7f * dim);
        }
    }

    // ------------------------------------------------------------------ PLATFORM CREATE

    // Ghost copies of the platform prefab's sprites at their true relative offsets and
    // scales, so the preview is exactly the platform that will spawn.
    private void EnsurePlatformVisuals()
    {
        if (platformRoot != null) return;
        if (player.platformPrefab == null) return;

        Transform prefabRoot = player.platformPrefab.transform;
        SpriteRenderer[] sources = player.platformPrefab.GetComponentsInChildren<SpriteRenderer>(true);
        if (sources.Length == 0) return;

        platformRoot = MakeContainer("Aim_Platform");
        platformGhostParts.Clear();

        foreach (SpriteRenderer src in sources)
        {
            if (src.sprite == null) continue;

            var part = MakeSpriteChild(platformRoot.transform, src.name + "_ghost", src.sprite, sortingOrder);
            part.flipX = src.flipX;
            part.flipY = src.flipY;
            part.sortingLayerID = src.sortingLayerID;
            part.sortingOrder = src.sortingOrder + 5;
            part.drawMode = src.drawMode;
            if (src.drawMode != SpriteDrawMode.Simple) part.size = src.size;

            // Pose relative to the prefab root, reproduced under our container.
            part.transform.localPosition = prefabRoot.InverseTransformPoint(src.transform.position);
            part.transform.localRotation = Quaternion.Inverse(prefabRoot.rotation) * src.transform.rotation;
            part.transform.localScale = src.transform.lossyScale;

            platformGhostParts.Add(part);
        }
    }

    private void UpdatePlatform(float dim)
    {
        if (platformRoot == null || platformGhostParts.Count == 0) return;

        Camera c = player.mainCamera != null ? player.mainCamera : cam;
        if (c == null) return;

        Vector2 mouse = c.ScreenToWorldPoint(Input.mousePosition);
        platformRoot.transform.position = new Vector3(mouse.x, mouse.y, 0f);

        Color pc = platformGhostColor;
        pc.a *= (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f)) * dim;
        foreach (SpriteRenderer part in platformGhostParts)
            if (part != null) part.color = pc;
    }

    // ------------------------------------------------------------------ FREEFALL BLADE

    private void EnsureFreefallVisuals()
    {
        if (freefallRoot != null) return;

        freefallRoot = MakeContainer("Aim_Freefall");

        freefallRing = MakeLineChild(freefallRoot.transform, "Ring", freefallRingWidth, sortingOrder);
        freefallRing.loop = true;
        freefallRing.positionCount = BITE_SEGMENTS;

        freefallFill = MakeSpriteChild(freefallRoot.transform, "Fill", GetDotSprite(), sortingOrder - 1);
    }

    private void UpdateFreefall(float dim)
    {
        // Mirror PerformFreefallBlade exactly: forward-and-low circle, BIGGER while falling.
        bool falling = !player.isGrounded && player.rb != null && player.rb.linearVelocity.y < -0.01f;
        float range = falling ? player.freefallBladeRange * player.freefallBladeFallingRangeMul
                              : player.freefallBladeRange;
        float facing = player.isFacingRight ? 1f : -1f;
        Vector2 center = (Vector2)transform.position
                       + new Vector2(facing * range * 0.55f, -range * 0.35f);

        // Hit preview on a short timer (same IDamageable-in-parents filter as the slash).
        freefallScanTimer -= Time.unscaledDeltaTime;
        if (freefallScanTimer <= 0f)
        {
            freefallScanTimer = 0.08f;
            freefallHit = false;
            int n = Physics2D.OverlapCircle(center, range, NoFilter, overlapHits);
            for (int i = 0; i < n; i++)
            {
                if (overlapHits[i].GetComponentInParent<IDamageable>() == null) continue;
                freefallHit = true;
                break;
            }
        }

        for (int i = 0; i < BITE_SEGMENTS; i++)
        {
            float a = (float)i / BITE_SEGMENTS * Mathf.PI * 2f;
            freefallRing.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * range, center.y + Mathf.Sin(a) * range, 0f));
        }

        // Green = something's in the arc; orange = empowered falling slash; pale = neutral.
        Color rc = freefallHit ? freefallHitColor : (falling ? freefallFallingColor : freefallNeutralColor);
        freefallRing.startColor = freefallRing.endColor = new Color(rc.r, rc.g, rc.b, rc.a * dim);

        freefallFill.transform.position = new Vector3(center.x, center.y, 0f);
        freefallFill.transform.localScale = Vector3.one * (range * 2f);
        freefallFill.color = new Color(rc.r, rc.g, rc.b, 0.07f * dim);
    }

    // ------------------------------------------------------------------ GLASS WAIL

    private void EnsureWailVisuals()
    {
        if (wailRoot != null) return;

        wailRoot = MakeContainer("Aim_Wail");

        // Two staggered expanding ripples read as a continuous shockwave preview.
        wailRipples = new SpriteRenderer[2];
        for (int i = 0; i < wailRipples.Length; i++)
            wailRipples[i] = MakeSpriteChild(wailRoot.transform, "Ripple" + i, GetRingSprite(), sortingOrder);
    }

    private void UpdateWail(float dim)
    {
        // The wail stuns EVERY EnemyHealth in the scene — refresh the target list on a timer.
        wailScanTimer -= Time.unscaledDeltaTime;
        if (wailScanTimer <= 0f)
        {
            wailScanTimer = 0.25f;
            wailTargets = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        }

        Vector2 center = player.BiteCenter;

        // Expanding ripples from the body, fading as they travel.
        // The ring sprite's band sits at ~0.41 world units at scale 1.
        for (int i = 0; i < wailRipples.Length; i++)
        {
            float t = Mathf.Repeat(Time.unscaledTime / wailRipplePeriod + (float)i / wailRipples.Length, 1f);
            float r = Mathf.Max(t * wailRippleMaxRadius, 0.01f);
            wailRipples[i].transform.position = new Vector3(center.x, center.y, 0f);
            wailRipples[i].transform.localScale = Vector3.one * (r / 0.41f);
            wailRipples[i].color = new Color(wailColor.r, wailColor.g, wailColor.b, wailColor.a * (1f - t) * dim);
        }

        // A pulsing glint over every enemy that would be stunned.
        int targetCount = wailTargets != null ? wailTargets.Length : 0;
        while (wailGlints.Count < targetCount)
        {
            var glint = MakeSpriteChild(wailRoot.transform, "Glint" + wailGlints.Count, GetDotSprite(), sortingOrder + 1);
            glint.transform.localScale = Vector3.one * 0.35f;
            wailGlints.Add(glint);
        }

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
        for (int i = 0; i < wailGlints.Count; i++)
        {
            bool live = i < targetCount && wailTargets[i] != null;
            wailGlints[i].enabled = live;
            if (!live) continue;

            Vector3 p = wailTargets[i].transform.position + Vector3.up * 0.9f;
            wailGlints[i].transform.position = p;
            wailGlints[i].color = new Color(wailColor.r, wailColor.g, wailColor.b, pulse * dim);
        }
    }

    // ------------------------------------------------------------------ PHASE

    private void EnsurePhaseVisuals()
    {
        if (phaseRoot != null) return;

        phaseRoot = MakeContainer("Aim_Phase");

        phaseRing = MakeLineChild(phaseRoot.transform, "Ring", phaseRingWidth, sortingOrder);
        phaseRing.loop = true;
        phaseRing.positionCount = BITE_SEGMENTS;

        phaseFill = MakeSpriteChild(phaseRoot.transform, "Fill", GetDotSprite(), sortingOrder - 1);
    }

    // Where the phase bubble WILL be anchored: centred on the body, following the player until
    // they cast. Mirrors PlayerController.PhaseRoutine (anchor = BiteCenter) and the radius it
    // clamps to — if phaseMaxRadius or the anchor point changes there, change it here too or the
    // preview starts lying about where the player can reach.
    private void UpdatePhase(float dim)
    {
        Vector2 center = player.BiteCenter;
        float radius = player.phaseMaxRadius;

        float breath = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 2.4f);
        float r = radius * breath;

        for (int i = 0; i < BITE_SEGMENTS; i++)
        {
            float a = (float)i / BITE_SEGMENTS * Mathf.PI * 2f;
            phaseRing.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, 0f));
        }

        phaseRing.startColor = phaseRing.endColor = new Color(phaseColor.r, phaseColor.g, phaseColor.b, phaseColor.a * dim);

        phaseFill.transform.position = new Vector3(center.x, center.y, 0f);
        phaseFill.transform.localScale = Vector3.one * (r * 2f);
        phaseFill.color = new Color(phaseColor.r, phaseColor.g, phaseColor.b, 0.04f * dim);
    }

    // ------------------------------------------------------------------ RETURN ANCHOR

    private void EnsureAnchorVisuals()
    {
        if (anchorRoot != null) return;

        anchorRoot = MakeContainer("Aim_Anchor");

        anchorTether = MakeLineChild(anchorRoot.transform, "Tether", 0.045f, sortingOrder - 2);
        anchorTether.positionCount = 2;

        anchorCore = MakeSpriteChild(anchorRoot.transform, "Core", GetDotSprite(), sortingOrder - 1);

        anchorDecal = MakeLineChild(anchorRoot.transform, "Decal", 0.05f, sortingOrder + 1);
        anchorDecal.loop = true;
        anchorDecal.positionCount = ANCHOR_SEGMENTS;

        anchorRings = new LineRenderer[2];
        for (int i = 0; i < anchorRings.Length; i++)
        {
            anchorRings[i] = MakeLineChild(anchorRoot.transform, "Ring" + i, 0.055f, sortingOrder);
            anchorRings[i].loop = true;
            anchorRings[i].positionCount = ANCHOR_SEGMENTS;
        }
    }

    // ⚠️ THE MOTION IS THE DESCRIPTION. A first pass drew one fat soft ring around the player and the
    // designer called it out: an enclosing ring reads as an area-of-effect, which is the wrong idea
    // entirely — the card marks a POINT and later pulls you back to it. So the rings now CONTRACT
    // inward and snuff out on the spot, which is the card's whole sentence in one gesture, and it is
    // deliberately the inverse of Glass Wail's expanding ripples and of the Phase bubble's static
    // boundary. Nothing else in the game moves inward.
    //
    // It is also much smaller than before. The old ring was 3.4 units wide and sat around the
    // player's own legs, so it claimed an area AND was occluded by the character standing in it. A
    // tight decal says "this exact spot" and leaves the player readable.
    //
    // Two states, and the second matters most: once an anchor exists the decision is "do I want to
    // be back THERE?", which is unanswerable without seeing where "there" is. The tether covers that
    // — the card has no range limit, so the anchor is regularly off-screen.
    private void UpdateAnchor(float dim)
    {
        if (anchorRoot == null) return;

        bool placed = player.HasReturnAnchor;
        Vector2 target = placed ? player.ReturnAnchorPos : (Vector2)transform.position;
        Vector3 c = new Vector3(target.x, target.y + 0.12f, 0f);

        float breathe = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);

        SetEllipse(anchorDecal, c, ANCHOR_SPOT_R, ANCHOR_SPOT_R * ANCHOR_FLATTEN);
        Color dc = anchorColor;
        dc.a *= breathe * dim;
        anchorDecal.startColor = anchorDecal.endColor = dc;

        for (int i = 0; i < anchorRings.Length; i++)
        {
            float t = Mathf.Repeat(Time.unscaledTime / ANCHOR_RING_PERIOD + (float)i / anchorRings.Length, 1f);
            float r = Mathf.Lerp(ANCHOR_RING_START, ANCHOR_SPOT_R, t * t);   // accelerates as it arrives
            SetEllipse(anchorRings[i], c, r, r * ANCHOR_FLATTEN);

            // Zero at both ends so the loop point is invisible — a ring snapping back out would
            // undo the inward reading the whole effect depends on.
            float fade = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((1f - t) / 0.2f);
            Color rc = anchorColor;
            rc.a *= fade * 0.85f * dim;
            anchorRings[i].startColor = anchorRings[i].endColor = rc;
        }

        anchorCore.transform.position = c;
        anchorCore.transform.localScale = new Vector3(ANCHOR_SPOT_R * 2.4f, ANCHOR_SPOT_R * 2.4f * ANCHOR_FLATTEN, 1f);
        anchorCore.color = new Color(anchorColor.r, anchorColor.g, anchorColor.b, 0.12f * breathe * dim);

        // No tether before placement — the marker lands at the player's own feet, so a line from
        // them to themselves would be noise.
        anchorTether.enabled = placed;
        if (placed)
        {
            anchorTether.SetPosition(0, new Vector3(transform.position.x, transform.position.y + 0.5f, 0f));
            anchorTether.SetPosition(1, c);
            Color tc = anchorColor;
            tc.a *= 0.3f * breathe * dim;
            anchorTether.startColor = anchorTether.endColor = tc;
        }
    }

    // Flattened ring on the ground plane. Shared by the decal and the contracting rings so they
    // can never disagree about what "on the floor" looks like.
    private static void SetEllipse(LineRenderer lr, Vector3 center, float rx, float ry)
    {
        for (int i = 0; i < lr.positionCount; i++)
        {
            float a = (float)i / lr.positionCount * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * rx, center.y + Mathf.Sin(a) * ry, center.z));
        }
    }

    // ------------------------------------------------------------------ BUILD HELPERS

    private GameObject MakeContainer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        return go;
    }

    private static SpriteRenderer MakeSpriteChild(Transform parent, string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private static LineRenderer MakeLineChild(Transform parent, string name, float width, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = GetLineMaterial();
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.sortingOrder = order;
        return lr;
    }

    private static Material GetLineMaterial()
    {
        if (cachedLineMaterial == null)
            cachedLineMaterial = new Material(Shader.Find("Sprites/Default"));
        return cachedLineMaterial;
    }

    // Soft radial dot, 64px, 1 world unit at scale 1. Doubles as the bite aura's inner fill.
    private static Sprite GetDotSprite()
    {
        if (cachedDotSprite != null) return cachedDotSprite;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = S * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d);
                a *= a;   // soft falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        cachedDotSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedDotSprite;
    }

    // Soft ring band, 128px, 1 world unit outer diameter at scale 1.
    private static Sprite GetRingSprite()
    {
        if (cachedRingSprite != null) return cachedRingSprite;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        float half = S * 0.5f;
        const float bandCenter = 0.82f;   // normalized radius of the ring band
        const float bandWidth = 0.14f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - bandCenter) / bandWidth);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedRingSprite;
    }
}
