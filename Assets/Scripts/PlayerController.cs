using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    internal Rigidbody2D rb;
    private Animator animator;
    internal Camera mainCamera;
    private CardActionExecutor cardActionExecutor;
    private PlayerHealth playerHealth;

    [Header("Visual Settings")]
    public GameObject visualModel; // YENİ: Hiyerarşideki PF Skeleton objesini buraya sürükleyeceğiz!

    [Header("Jump Feel Settings")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Durumlar")]
    public bool isPeeking = false;

    [Header("Physics Checks")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public Transform ceilingCheck;
    public float ceilingCheckRadius = 0.2f;

    // Ground + Enemy. Enemies are in here on purpose so the player can land on their heads (see
    // Pogo Boots), but that makes it the WRONG mask for anything asking "is there terrain here".
    public LayerMask groundLayer;

    [Tooltip("Terrain only. Used where an enemy must NOT count as level geometry — the wall check.")]
    public LayerMask terrainLayer = 1 << 3;   // Ground

    [Header("Air Settings")]
    public int maxAirJumps = 1;
    private int currentAirJumps = 0;

    public GameObject platformPrefab;
    private bool isPhasing = false;
    private float verticalInput;

    [Header("Swim Settings")]
    [Tooltip("Horizontal/vertical move speed while swimming. Lower than moveSpeed to simulate water resistance.")]
    public float swimSpeed = 5f;
    [Tooltip("Upward impulse applied when pressing Jump while swimming, used to break the surface and leap out.")]
    public float swimExitJumpForce = 9f;
    [Tooltip("How far BELOW the surface the player settles when NOT actively swimming up/down, so they sit in the water instead of bobbing on top. Hold Up to swim above this and out of the water.")]
    public float swimSurfaceOffset = 1.2f;
    [Tooltip("How quickly the player sinks to the idle rest depth when not pressing up/down. Higher = snappier settle.")]
    [SerializeField] private float swimSettleStrength = 8f;
    [Tooltip("Seconds the upward swim-jump pop is preserved, to help breach the surface and leave the water.")]
    [SerializeField] private float swimExitDuration = 0.35f;
    private bool isSwimming = false;
    private float swimCachedGravityScale;
    private int swimZoneCount = 0; // refcount so overlapping swim zones don't exit early
    private SwimZone currentSwimZone;
    private float swimExitTimer;
    private Vector3 originalScale;
    internal Vector3 currentRoomEntryPoint;

    private float _headBounceCooldown;

    // Pogo Boots chain state, both cleared the moment the player touches the ground.
    // See TriggerHeadBounce for why these exist.
    private readonly HashSet<int> _bouncedThisAirtime = new HashSet<int>();
    private int _bounceChain;

    [Header("Pogo Boots (head bounce)")]
    [Tooltip("Upward impulse per bounce as a fraction of a normal jump, by position in the chain. " +
             "The last value repeats once the chain runs past the end of the array.")]
    public float[] pogoChainFalloff = { 0.70f, 0.55f, 0.42f, 0.32f };

    [Header("Gold Settings")]
    public int currentGold = 0;
    public event System.Action<int> OnGoldChanged;

    [Header("Scrap Settings")]
    [Tooltip("Card-maintenance currency. Earned from kills and from cards exhausting; " +
             "spent at a Scrap Forge to recharge cards and salvage them out of exhaust. " +
             "Tuning lives in ScrapEconomy.cs.")]
    public int currentScrap = 0;
    public event System.Action<int> OnScrapChanged;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip dashSound;
    public AudioClip fireballCastSound;
    public AudioClip cometDiveSound;
    public AudioClip phaseSound;
    public AudioClip adrenalineSound;
    public AudioClip createPlatformSound;
    public AudioClip vampireBiteSound;
    public AudioClip glassVailSound;
    public AudioClip glassParrySound;      // successful parry: chime + shatter
    public AudioClip freefallBladeSound;   // slash swipe
    public AudioClip jumpSound;
    public AudioClip leapSound;
    public AudioClip spendSound;
    public AudioClip warningSoundClip;
    public float soundVolume = 1f;

    [Header("Footsteps")]
    [Tooltip("Footstep clips — a random one plays each time the walk/run animation plants a foot. " +
             "Add a few variations so steps don't sound identical. Leave empty for no footstep sound.")]
    public AudioClip[] footstepClips;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;
    [Tooltip("Random pitch range per step so repeated footsteps feel natural (x = min, y = max).")]
    public Vector2 footstepPitchRange = new Vector2(0.92f, 1.08f);
    private AudioSource footstepSource;   // dedicated 2D source (built on first step) so per-step pitch doesn't touch other SFX

    [Header("VFX Settings")]
    public GameObject biteEffectPrefab;
    public GameObject leapEffectPrefab;
    public TrailRenderer diveTrail;
    public GameObject dashEffectPrefab;

    [Header("Dash Settings")]
    [SerializeField] internal float dashSpeed = 26f;            // driven horizontal speed during the dash
    [SerializeField] internal float dashDuration = 0.16f;       // how long the dash drives velocity
    [SerializeField] internal float dashEndSpeed = 9f;          // momentum carried out of the dash (no dead stop)
    [SerializeField] internal float dashIFrameDuration = 0.22f; // i-frames; keep >= dashDuration to stay safe through the dash
    [SerializeField] internal bool  dashAfterimages = true;     // procedural motion-blur ghosts (no art needed)
    [SerializeField] internal Color dashAfterimageTint = new Color(0.6f, 0.85f, 1f, 0.55f);

    [Header("Adrenaline VFX")]
    public GameObject ghostPrefab;
    public float adrenalineSpeedMult = 2f;
    public float ghostDelay = 0.05f;

    private float ghostTimer;
    private bool isAdrenalineActive = false;
    private float defaultMoveSpeed;

    [Header("Portal Settings")]
    public GameObject portalPrefab;

    // Portal used to be the biggest skip in the game and the only card whose reach depended on the
    // player's MONITOR: the first portal had no range limit at all, so it could go anywhere the
    // mouse reached — the camera's half-width, which is 9.3 units at 4:3 but 16.3 at 21:9. With the
    // link range on top, total reach was 24-31 units against a spawn-to-exit separation the level
    // laws set at ~20. Anchoring the first portal to the PLAYER fixes both at once: the apparatus is
    // now placed around you rather than painted across the room, and the numbers are the same on
    // every display.
    [Tooltip("How far from the PLAYER the first portal may be placed, in world units (1 unit = 1 tile). " +
             "This is the 'set it down near you' radius — it is what makes Portal's reach independent " +
             "of screen aspect ratio. Matches the Phase bubble so both cards read as the same idea.")]
    public float portalPlaceRange = 6f;

    [Tooltip("How far the SECOND portal may be placed from the first — i.e. the size of the hop. " +
             "This is the number that decides how much level Portal can skip; tune this one first.")]
    public float portalMaxRange = 15f;

    private Portal firstPortalInstance;
    // Read-only view for CardAimIndicator's portal ghost (first vs second placement preview).
    internal Portal FirstPortalInstance => firstPortalInstance;

    [Header("Return Anchor (Second Thoughts)")]
    // Deliberately has NO range limit, which is the whole difference from Portal. The return end is
    // always somewhere the player has already stood, so it can never carry them forward through a
    // room — it can only undo a trip they already paid for. That makes unlimited range safe by
    // construction, and it's what the card is for: commit to a detour, come back cheap.
    private Vector2 returnAnchorPos;
    private bool hasReturnAnchor;
    private ReturnAnchorVFX returnAnchorVfx;
    internal bool HasReturnAnchor => hasReturnAnchor;
    internal Vector2 ReturnAnchorPos => returnAnchorPos;

    [Header("Wall Settings")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.5f;
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpForce = new Vector2(10f, 15f);
    private bool isWallDetected;
    private bool isWallSliding;
    private bool wallSlideAnimActive;
    private float nextScrapeTime;

    // Wall sliding is a RELIC, not a base ability (designer 2026-08-11). The state machine, the
    // sensor and the tuning fields all existed but nothing ever entered the state, so wall-jumping
    // has never been in the game — which makes it free to hand out as a pickup instead.
    public const string WallSlideRelicID = "GeckoGloves";

    private bool CanWallSlide()
    {
        return RelicManager.instance != null && RelicManager.instance.HasRelic(WallSlideRelicID);
    }

    [Header("Quest Tracking")]
    private bool tookDamageThisRoom = false;
    public bool TookDamageThisRoom { get { return tookDamageThisRoom; } }

    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    private float moveInput;

    [Header("Locomotion Animation")]
    [Tooltip("TEST TOGGLE: play the Cainos pack's dedicated RUN animation (the Shift-to-run clip) while moving, via the pack's IsRunning bool. Uncheck to go back to the normal walk.")]
    [SerializeField] private bool useRunAnimation = true;
    [Tooltip("Base locomotion pose used when NOT running. 0 = idle, 1 = walk, 3 = run — blends in between. Default 1 (walk).")]
    [SerializeField] private float locomotionPose = 1f;
    [Tooltip("How strongly the leg cadence follows actual ground speed. Higher = feet cycle faster. Tune this until the feet stop sliding at your move speed. At speed 8 a value of 0.16 gives ~1.28x cadence.")]
    [SerializeField] private float animCadenceScale = 0.16f;
    [Tooltip("Lower clamp on the cadence multiplier (keeps slow-nudge steps from crawling).")]
    [SerializeField] private float minCadence = 0.7f;
    [Tooltip("Upper clamp on the cadence multiplier (keeps top speed / dash from looking frantic).")]
    [SerializeField] private float maxCadence = 2.2f;

    // --- Temporary movement slow (acid drag / sticky goo) ---
    // Kept as a separate multiplier ON TOP of moveSpeed rather than mutating moveSpeed itself,
    // so it composes cleanly with Adrenaline's speed boost (which mutates moveSpeed) instead of
    // corrupting its save/restore snapshot. Refreshed each frame by hazard zones the player
    // stands in, and auto-clears shortly after they leave.
    private float slowFactor = 1f;       // 1 = normal, <1 = slowed
    private float slowExpireTime = 0f;

    /// <summary>
    /// Applies a temporary movement slow. Called repeatedly by hazard zones while the player is
    /// inside them; the strongest (lowest) active multiplier wins, and it fades <paramref name="duration"/>
    /// seconds after the last call.
    /// </summary>
    public void ApplySlow(float multiplier, float duration)
    {
        multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        // If a slow is already active this frame, keep whichever is stronger.
        if (Time.time < slowExpireTime) multiplier = Mathf.Min(multiplier, slowFactor);
        slowFactor = multiplier;
        slowExpireTime = Time.time + Mathf.Max(0.02f, duration);
    }

    [Header("Health Settings")]
    public float CurrentHealth => playerHealth.CurrentHealth;
    public float MaxHealth => playerHealth.MaxHealth;
    public bool IsDead => playerHealth.IsDead;
    public bool isInvincible
    {
        get => playerHealth.isInvincible;
        set => playerHealth.isInvincible = value;
    }

    [Header("Jump Settings")]
    public float defaultJumpForce = 10f;
    private bool freeAirJumpUsed = false;

    [Header("Shift Settings")]
    public int maxShift = 3;
    public int currentShift;
    public int GetCurrentShift() { return currentShift; }

    [Header("Comet Dive Settings")]
    public float cometSpeed = 25f;
    public float cometDamage = 40f;
    [Tooltip("Blast radius on landing. CometDiveVFX telegraphs exactly this value on the ground while falling.")]
    public float cometRadius = 3f;
    // The comet, its ground telegraph and the landing burst are all built procedurally by
    // CometDiveVFX — no prefabs to assign.
    private CometDiveVFX cometVfx;

    [Header("Adrenaline Card Settings")]
    public float adrenalineDuration = 3f;
    public float slowMotionFactor = 0.4f;
    public float speedBoostMultiplier = 1.5f;
    public GameObject adrenalineAuraEffect;   // looping energy aura (AdrenalineAuraVFX), played for the buff duration

    public PlayerState currentState;
    internal bool isGrounded;

    [Header("Jump Forgiveness")]
    // Two standard platformer affordances. Neither is a mechanic the player learns — when they work
    // you don't notice them, you just stop having moments where you swear you pressed jump and the
    // character dropped anyway.
    //
    // ⚠️ THEY MATTER MORE HERE THAN IN AN ORDINARY PLATFORMER, because in this game a jump that
    // fails on a timing gap does not just cost you a retry — it costs SHIFT, twice: once for the
    // failed input if it fired at all, and again for the re-attempt. That is a run-long resource
    // being spent on input latency rather than on decisions.
    [Tooltip("Seconds after walking off a ledge during which a jump still counts as grounded.")]
    public float coyoteTime = 0.10f;
    [Tooltip("Seconds a jump press is remembered for, so pressing just before landing still fires.")]
    public float jumpBufferTime = 0.12f;

    private float coyoteTimer;
    private float jumpBufferTimer;

    [Header("Character")]
    // The played character: the run's starting deck and one passive trait. Left null, the game
    // plays exactly as it did before characters existed (DeckManager falls back to its own
    // startingDeck), which is what makes this safe to ship without a select screen yet.
    public CharacterData character;

    [Header("Combat Settings")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    [SerializeField] private AudioClip shurikenThrowSound;
    // The pack's own shuriken art, so the thrown star is the same object that was in the hand.
    // Left empty, Shuriken falls back to its procedural star.
    [SerializeField] private Sprite shurikenSprite;
    public float wailRange = 10f;
    [SerializeField] internal float fireballCastDelay = 0.12f;
    public float biteRange = 1.5f;
    public float biteHealAmount = 10f;
    // Vampiric Bite is a regular circle around the BODY center (designer 2026-07-17) — it used
    // to be centered on the wand-side firePoint, which read as lopsided. Derived from the
    // capsule's serialized offset so it's valid in both play mode and editor gizmos.
    private CapsuleCollider2D bodyCapsule;
    internal Vector2 BiteCenter
    {
        get
        {
            if (bodyCapsule == null) bodyCapsule = GetComponent<CapsuleCollider2D>();
            return bodyCapsule != null
                ? (Vector2)transform.position + bodyCapsule.offset
                : (Vector2)transform.position;
        }
    }
    public LayerMask enemyLayer;
    public GameObject glassWailEffect;   // Glass Wail shockwave VFX (world-space ShockwaveVFX prefab; size set on the prefab)
    public float freefallBladeRange = 1.7f;   // radius of the ")" arc slash (front + below)
    public float freefallBladeFallingRangeMul = 1.4f;   // falling slash swings BIGGER (designer 2026-07-17)
    public float parryRiposteRange = 2.5f;    // shard burst radius on a successful Glass Parry

    [Header("Meteor Greaves (relic)")]
    // ⚠️ THIS MUST STAY ABOVE THE JUMP APEX. A max-height jump rises ~4.9 units (measured, see
    // LevelValidator), so at the old 4.0 the shockwave fired on EVERY full jump — the relic was
    // free, constant, and stopped being a decision. 6.5 clears the apex with margin, so it takes a
    // real DROP (a ledge, a shaft) to trigger, which is what the item is about.
    [Tooltip("Minimum fall (world units, apex→landing) before the landing shockwave triggers. " +
             "Keep ABOVE the ~4.9-unit jump apex or it procs on every jump.")]
    public float meteorMinFall = 6.5f;
    [Tooltip("Fall height at which the shockwave's size/damage max out.")]
    public float meteorMaxFall = 14f;
    [Tooltip("Seconds before it can trigger again — stops a repeated hop off the same ledge from " +
             "chaining shockwaves into a stun-lock.")]
    public float meteorCooldown = 1.5f;
    public float meteorMinRadius = 2.5f;
    public float meteorMaxRadius = 5.5f;
    public float meteorMinDamage = 12f;
    public float meteorMaxDamage = 55f;
    [Tooltip("Impact volume at the biggest fall. Scales down with drop height.")]
    [Range(0f, 2f)] public float meteorSfxVolume = 1.0f;
    private float meteorReadyAt;   // Time.time before which the greaves stay quiet

    // Fall tracking for Meteor Greaves: record the highest point reached while airborne,
    // so the landing knows how far the player dropped. Reset on teleports (respawn / room
    // enter) and consumed by comet-dive landings so they don't also meteor.
    private bool trackingFall = false;
    private float fallApexY = 0f;

    [Header("Interaction Settings")]
    public float interactionRange = 2f;
    public LayerMask interactableLayer;

    // --- Stagger: buy Shift with blood --------------------------------------------------------
    // Stagger is no longer a three-strikes death sentence. It is the pump of last resort: it hands
    // back a little Shift and charges HP for it, and the price goes UP every single time, for the
    // whole run. There is no cap and no reset — the run ends when you can no longer afford the next
    // one. That escalation is what keeps it a last resort instead of a Shift printer.
    [Header("Stagger Settings")]
    [Tooltip("How many times Stagger has been played this RUN. The price is derived from it, so it must not reset per room.")]
    public int staggerCount = 0;
    [Tooltip("HP the FIRST Stagger costs. Every play adds another step on top: 8, 16, 24, 32...")]
    public float staggerHealthStep = 8f;
    [Tooltip("Shift handed back per play. This is what the HP is buying.")]
    public int staggerShiftGain = 2;
    public float staggerJumpForce = 5f;
    [Tooltip("Incidental damage to enemies caught in the flail. Unrelated to what Stagger costs YOU.")]
    public float staggerDamage = 5f;
    public float staggerRadius = 2f;
    public GameObject staggerEffect;

    // What the NEXT Stagger will cost. The card face and its hover text both read this, so the
    // player is never surprised by the price — see CardUI.
    //
    // Iron Lung shallows the climb to 6 per step instead of 8. Read through here rather than
    // written into staggerHealthStep, for the same reason HandCapacity is read rather than
    // mirrored: selling the relic must restore the real price instantly, with no stale copy left
    // on the player. The card face reads this property, so the drawn cost follows automatically.
    // Debt Collector's exchange rate: gold charged per point of health Stagger would have cost.
    public const float DebtCollectorGoldPerHealth = 20f;

    public float StaggerStep =>
        (RelicManager.instance != null && RelicManager.instance.HasRelic("IronLung")) ? 6f : staggerHealthStep;

    public float NextStaggerCost => StaggerStep * (staggerCount + 1);

    // Gravity reversal state
    internal bool isGravityReversed = false;
    private float originalGravityScale;
    private Coroutine gravityReversalCoroutine;
    private float visualRotationZ = 0f;
    private Vector3 originalVisualLocalPos;
    private float originalVisualScaleX;
    internal bool isFacingRight = true;

    [Header("Gravity Reversal")]
    // Tune in Play mode: feet should just touch the ceiling when flipped
    [SerializeField] private float visualFlipYOffset = 2.0f;
    public GameObject gravityAuraEffect;        // looping anti-gravity aura prefab (GravityAuraVFX), played for the reversal duration
    private GameObject gravityAuraInstance;

    [Header("Phase")]
    // Phase is bounded to a bubble anchored where it was cast. Without one it is the strongest
    // traversal tool in the game by a wide margin: 8-directional flight at full moveSpeed for the
    // card's 2s duration reaches ~16 world units through solid rock in any direction, which is more
    // than the 20-tile spawn-to-exit separation the level design laws are built on. The bubble keeps
    // Phase as "get through that wall into that pocket" and stops it being "skip the room".
    [Tooltip("How far the player may travel from the cast point while phasing, in world units " +
             "(1 unit = 1 tile). Anchored at the BODY CENTER the moment the card is played; it does " +
             "NOT follow the player. Pushing the edge slides along it rather than stopping dead. " +
             "⚠️ The Phase card's description names this number — update the card asset if you change it.")]
    public float phaseMaxRadius = 6f;
    private Vector2 phaseAnchor;
    private PhaseBoundary phaseBoundary;    // live only while phasing; OnTeleported re-anchors it
    internal bool IsPhasing => isPhasing;

    [Header("Phase Visual")]
    [SerializeField] internal SkinnedMeshRenderer[] phaseVisuals;
    private Coroutine phaseVisualCoroutine;
    private CapsuleCollider2D capsuleCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        mainCamera = Camera.main;
        cardActionExecutor = GetComponent<CardActionExecutor>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        playerHealth = GetComponent<PlayerHealth>();

        if (visualModel != null)
        {
            originalVisualLocalPos = visualModel.transform.localPosition;
            originalVisualScaleX = visualModel.transform.localScale.x;
        }

        // ⚠️ THE SELECT SCREEN'S PICK OVERRIDES THE PREFAB. The prefab's `character` field is the
        // fallback for entering play mode straight into SampleScene, which is how this project is
        // actually developed — but a run started from the main menu must play whoever was chosen
        // there, and that choice arrives a scene load away through CharacterSelection.
        if (CharacterSelection.Chosen != null) character = CharacterSelection.Chosen;

        // Re-dress the rig for the played character. In Awake so the player is never seen wearing
        // the wrong outfit for a frame; a no-op when no character or no preset is assigned.
        CharacterAppearance.Apply(this, character);
    }

    void Start()
    {
        originalScale = transform.localScale;
        currentShift = maxShift;
        ChangeState(PlayerState.Idle);

        playerHealth.OnDamaged += (dmg) =>
        {
            tookDamageThisRoom = true;
            RelicManager.instance?.OnPlayerTakeDamage();
            CameraShake.instance?.Shake(0.2f, 0.3f);
        };

        // Phase toggles the global layer-collision matrix, which survives scene loads.
        // Dying mid-Phase kills PhaseRoutine before its cleanup runs, so the death
        // path must restore the matrix itself (audit_report.md Critical #2).
        playerHealth.OnDied += RestorePhaseLayerCollisions;

        // A pending first portal / return anchor must not outlive the run that placed it.
        playerHealth.OnDied += CancelPendingPortal;
        playerHealth.OnDied += ClearReturnAnchor;

        if (visualModel != null)
        {
            var aer = visualModel.GetComponentInChildren<Cainos.CustomizablePixelCharacter.AnimationEventReceiver>(true);
            if (aer != null && aer.enabled)
            {
                aer.enabled = false;
                Debug.Log("[PlayerController] AnimationEventReceiver was enabled on startup, force-disabling.");
            }
        }
    }

    void Update()
    {
        if (isPeeking)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        if (playerHealth.IsDead) return;

        bool wasGrounded = isGrounded;
        isGrounded = IsGroundedCheck();
        if (isGrounded)
        {
            currentAirJumps = 0;
            freeAirJumpUsed = false;
            _bouncedThisAirtime.Clear();
            _bounceChain = 0;
        }
        isWallDetected = WallCheck();

        // Meteor Greaves fall tracking: remember the apex while airborne.
        if (!isGrounded)
        {
            if (!trackingFall) { trackingFall = true; fallApexY = transform.position.y; }
            else if (transform.position.y > fallApexY) fallApexY = transform.position.y;
        }

        if (isGrounded && !wasGrounded)
        {
            currentAirJumps = 0;

            if (trackingFall)
            {
                float fallDist = fallApexY - transform.position.y;
                trackingFall = false;
                TryMeteorGreavesLanding(fallDist);
            }
        }

        if (GameManager.instance != null && GameManager.instance.currentState == GameState.Paused)
        {
            moveInput = 0;
            verticalInput = 0;
            return;
        }

        HandleCardInput();

        if (Input.GetKeyDown(KeyCode.E))
        {
            CheckInteraction();
        }

        if (currentState == PlayerState.InCannon) return;

        if (isPhasing)
        {
            moveInput = Input.GetAxisRaw("Horizontal");
            verticalInput = Input.GetAxisRaw("Vertical");
        }
        else if (isSwimming)
        {
            // Free 8-directional swim. Jump kicks upward to break the surface.
            moveInput = Input.GetAxisRaw("Horizontal");
            verticalInput = Input.GetAxisRaw("Vertical");

            if (Input.GetButtonDown("Jump")) PerformSwimJump();

            if (moveInput > 0 && !isFacingRight) Flip();
            else if (moveInput < 0 && isFacingRight) Flip();
        }
        else
        {
            // Coyote time: refreshed while grounded, bleeds away once you step off.
            if (isGrounded) coyoteTimer = coyoteTime;
            else coyoteTimer -= Time.deltaTime;

            // Jump buffering: the press is remembered rather than consumed on the frame it arrives.
            if (Input.GetButtonDown("Jump")) jumpBufferTimer = jumpBufferTime;
            else jumpBufferTimer -= Time.deltaTime;

            // Retried every frame while the buffer is live, and cleared only when a jump ACTUALLY
            // happened — so a press made a moment too early survives until landing, and a press
            // that could not be paid for (0 Shift) simply expires instead of firing later.
            if (jumpBufferTimer > 0f && HandleJumpInput()) jumpBufferTimer = 0f;

            // --- BOSS RELIC INPUTS ------------------------------------------------------------
            // ⚠️ THE ONLY RELICS ALLOWED TO ADD A KEYBIND (designer, 2026-08-21). Everything below
            // boss tier reuses an existing input or is passive, which is why Crowbar breaks walls
            // by walking into them and Air Brake is a fall multiplier rather than a hold.
            HandleBossRelicInput();

            if (currentState == PlayerState.Idle || currentState == PlayerState.Running || currentState == PlayerState.Jumping)
                moveInput = Input.GetAxisRaw("Horizontal");
            else
                moveInput = 0;

            verticalInput = 0;
        }

        // --- BETTER JUMP MANTIĞI ---
        // gravitySign flips fall/low-jump-cut direction when gravity is reversed.
        // Fall check: velocity dot gravitySign < 0 means moving against gravity (falling).
        // Low-jump cut: velocity dot gravitySign > 0 means rising; multiplied force sign follows.
        // Skip Better Jump while swimming: it manually applies Physics2D.gravity
        // regardless of gravityScale, which would pull the swimmer down.
        if (!isSwimming)
        {
            float gravitySign = isGravityReversed ? -1f : 1f;
            if (rb.linearVelocity.y * gravitySign < 0)
            {
                // Air Brake and Weight Class both live on the FALL multiplier, in opposite
                // directions, so owning both is a wash rather than a stack — which is the honest
                // outcome for "you fall slower" plus "you fall faster".
                //
                // Scaling the multiplier (not gravity itself) keeps this out of the way of gravity
                // reversal and of Phase/swim, which cache and restore gravityScale.
                float fall = fallMultiplier;
                if (RelicManager.instance != null)
                {
                    if (RelicManager.instance.HasRelic("AirBrake")) fall /= 1.5f;
                    if (RelicManager.instance.HasRelic("WeightClass")) fall *= 1.5f;
                }
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fall - 1) * Time.deltaTime * gravitySign;
            }
            else if (rb.linearVelocity.y * gravitySign > 0 && !Input.GetKey(KeyCode.Space))
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime * gravitySign;
            }
        }

        // Swimming handles its own facing above and must not let ground-based
        // transitions (e.g. touching the water floor) knock it out of Swimming.
        if (!isPhasing && !isSwimming)
        {
            HandleStateTransitions();
            UpdateAnimations();
        }
        else if (isSwimming)
        {
            UpdateSwimAnimations();
        }
    }

    private void HandleCardInput()
    {
        if (DeckManager.instance == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) DeckManager.instance.SelectCard(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) DeckManager.instance.SelectCard(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) DeckManager.instance.SelectCard(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) DeckManager.instance.SelectCard(3);

        if (Input.GetMouseButtonDown(0))
        {
            DeckManager.instance.TryCastSelectedCard();
        }

        if (Input.GetMouseButtonDown(1))
        {
            DeckManager.instance.DeselectCard();
        }
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        OnGoldChanged?.Invoke(currentGold);

        if (QuestSystem.instance != null)
        {
            QuestSystem.instance.ReportEvent(QuestType.GoldAccumulate, amount);
        }
    }

    public bool TrySpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            OnGoldChanged?.Invoke(currentGold);
            SfxManager.PlayOn(audioSource, spendSound, soundVolume);
            return true;
        }
        return false;
    }

    // --- Scrap ---------------------------------------------------------------------------------
    // Scrap is the CARD-MAINTENANCE currency, deliberately separate from gold. The split
    // (designer-set 2026-08-03): gold comes from piles placed in levels (exploration) and buys NEW
    // power at the shop; scrap comes from kills and from your own cards wearing out, and only ever
    // SUSTAINS what you already own. They must never become interchangeable — if the shop ever
    // sells charges, or scrap ever buys cards, the two have merged and one of them is redundant.
    //
    // Scrap spending is NOT hub-exempt. The umbrella "free in hub" rule covers resources the
    // sandbox drains from you (jump Shift, card charges); a forge recharge is a purchase that
    // permanently improves the run, exactly like a shop buy, which the hub already charges for.
    // Making it free would let the player stand in the hub and refill their whole deck.

    public void AddScrap(int amount)
    {
        if (amount <= 0) return;
        currentScrap += amount;
        OnScrapChanged?.Invoke(currentScrap);
    }

    public bool TrySpendScrap(int amount)
    {
        if (currentScrap >= amount)
        {
            currentScrap -= amount;
            OnScrapChanged?.Invoke(currentScrap);
            SfxManager.PlayOn(audioSource, spendSound, soundVolume);
            return true;
        }
        return false;
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        float speed = Mathf.Abs(rb.linearVelocity.x);
        bool moving = Mathf.Abs(moveInput) > 0.1f && speed > 0.1f;

        // Pose: hold the walk (or chosen) locomotion pose while actually moving, idle otherwise.
        // MoveBlendX blends idle(0)/walk(1)/run(3) in the grounded locomotion blend tree.
        // Gated on real speed too, so pushing into a wall shows idle instead of walking in place.
        animator.SetFloat("MoveBlendX", moving ? locomotionPose : 0f);

        // Run animation: the Cainos pack reaches its dedicated RUN clip via the IsRunning bool,
        // normally flipped by the pack's own Shift-to-run input controller — which was removed
        // when the character was integrated, so nothing ever set it and the character was stuck
        // walking. Drive it ourselves. Toggle off (useRunAnimation) to revert to the walk.
        animator.SetBool("IsRunning", moving && useRunAnimation);

        // Cadence: scale the walk-cycle PLAYBACK to the actual ground speed so the feet grip the
        // ground instead of sliding. This drives the pack's built-in MoveSpeedMul (the "Movement
        // Blend" state's speed parameter), which was never set — that's why the walk always
        // played at a fixed cadence regardless of how fast the body travelled. Also scales down
        // naturally when acid/goo slows the player, keeping the feet locked.
        float cadence = moving ? Mathf.Clamp(speed * animCadenceScale, minCadence, maxCadence) : 1f;
        animator.SetFloat("MoveSpeedMul", cadence);

        animator.SetFloat("VelocityY", rb.linearVelocity.y);
        animator.SetBool("IsGrounded", isGrounded);

        UpdateWallSlideAnimation();
    }

    // The wall-slide pose, borrowed from the Cainos pack's LADDER CLIMB layer.
    //
    // There is no wall-slide clip in the pack and commissioning one isn't on the table, but the
    // ladder-climb pose is already a character pressed flat against a vertical surface with both
    // arms up — which is exactly the read we want. Setting ClimbingSpeedMul to 0 FREEZES it on a
    // single frame, turning a climb cycle into a hold. That one parameter is the difference between
    // "climbing an invisible ladder" and "gripping a wall".
    //
    // Facing already points into the wall: the slide can only start while pushing toward the wall
    // the sensor found, and the sensor casts along `isFacingRight`.
    private void UpdateWallSlideAnimation()
    {
        bool sliding = currentState == PlayerState.WallSliding;

        if (sliding != wallSlideAnimActive)
        {
            wallSlideAnimActive = sliding;
            animator.SetBool("IsClimbingLadder", sliding);
            animator.SetFloat("ClimbingSpeedMul", sliding ? 0f : 1f);
        }

        if (sliding) EmitWallScrape();
    }

    // Grit scraped off the wall. The frozen pose alone reads as being STUCK to the wall — nothing
    // says which way you're travelling — so this supplies the motion cue.
    private void EmitWallScrape()
    {
        if (Time.time < nextScrapeTime) return;
        nextScrapeTime = Time.time + 0.045f;

        float dirX = isFacingRight ? 1f : -1f;
        float halfWidth = capsuleCollider != null ? capsuleCollider.size.x * 0.5f : 0.25f;

        // On the wall face, at a random height up the body, so it looks like a contact patch rather
        // than a single emitter point.
        Vector2 at = (Vector2)transform.position
                   + new Vector2(dirX * halfWidth, Random.Range(0.25f, 1.45f));

        int layerID = 0, order = 5;
        SpriteRenderer any = GetComponentInChildren<SpriteRenderer>();
        if (any != null) { layerID = any.sortingLayerID; order = any.sortingOrder + 1; }

        WallScrapeVFX.Spawn(at, dirX, layerID, order);
    }

    // Feeds the Cainos swim blend tree so it picks the forward / backward / up / down
    // swim clip based on actual movement. IsInWater (set in EnterWater) gates entry.
    private void UpdateSwimAnimations()
    {
        if (animator == null) return;

        animator.SetFloat("MoveBlendX", Mathf.Abs(moveInput) > 0.1f ? 1f : 0f);
        // VelocityX is signed by facing so "forward" stays positive regardless of direction.
        animator.SetFloat("VelocityX", rb.linearVelocity.x * (isFacingRight ? 1f : -1f));
        animator.SetFloat("VelocityY", rb.linearVelocity.y);
    }

    // Returns true only if a jump actually happened, which is what lets the buffer above know
    // whether to clear itself or keep waiting.
    private bool HandleJumpInput()
    {
        if (currentState == PlayerState.WallSliding)
        {
            return PerformWallJump();
        }

        // ⚠️ `coyoteTimer > 0` is the ONLY change to the grounded test, and it must stay that way.
        // Do NOT "fix" isGrounded to clear itself on jumping while you are in here: PerformJump's
        // horizontal impulse is dead code precisely because the next FixedUpdate still sees
        // isGrounded == true and overwrites it with the walking speed. Make isGrounded honest and
        // every jump silently gains a large horizontal boost, and every gap in every level in the
        // game becomes trivially clearable.
        if (isGrounded || coyoteTimer > 0f)
        {
            if (!PerformJump(defaultJumpForce)) return false;
            // Spend the coyote window. Without this, the leftover timer would hand out a second
            // free jump immediately after the first — a double jump nobody asked for.
            coyoteTimer = 0f;
            return true;
        }

        bool hasWings = SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.SpectralWings);

        if (hasWings && !freeAirJumpUsed)
        {
            freeAirJumpUsed = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            rb.AddForce(new Vector2(0f, defaultJumpForce), ForceMode2D.Impulse);
            ChangeState(PlayerState.Jumping);

            SfxManager.PlayOn(audioSource, jumpSound);
            Debug.Log("SPECTRAL WINGS: Bedava Zıplama!");
            return true;
        }

        if (currentAirJumps < maxAirJumps && currentShift > 0)
        {
            if (!PerformJump(defaultJumpForce)) return false;
            currentAirJumps++;
            return true;
        }

        return false;
    }

    private void FixedUpdate()
    {
        // Expire the acid/goo slow once the player has been out of the hazard long enough.
        if (Time.time >= slowExpireTime) slowFactor = 1f;

        if (isPhasing)
        {
            rb.linearVelocity = ClampPhaseVelocity(
                new Vector2(moveInput * moveSpeed * slowFactor, verticalInput * moveSpeed * slowFactor));
        }
        else if (isSwimming && currentState != PlayerState.Dashing && currentState != PlayerState.KnockedBack && currentState != PlayerState.CometDiving)
        {
            // Direct velocity control with water resistance. Overwriting velocity each
            // step keeps swimming responsive even if gravityScale gets changed elsewhere.
            float vx = moveInput * swimSpeed;
            float vy;

            if (swimExitTimer > 0f)
            {
                // Swim-jump pop in progress: preserve the upward velocity to breach and exit.
                swimExitTimer -= Time.fixedDeltaTime;
                vy = rb.linearVelocity.y;
            }
            else if (Mathf.Abs(verticalInput) > 0.01f)
            {
                // Actively swimming up or down. Holding Up carries the player to the
                // surface and out of the water — this is the normal way to exit.
                vy = verticalInput * swimSpeed;
            }
            else
            {
                // Idle: settle DOWN to a rest depth below the surface so the player sits
                // in the water instead of floating on top. Never pushes up, so it can't trap.
                float restY = (currentSwimZone != null ? currentSwimZone.SurfaceY : transform.position.y) - swimSurfaceOffset;
                vy = transform.position.y > restY
                    ? Mathf.Max(-swimSpeed, (restY - transform.position.y) * swimSettleStrength)
                    : 0f;
            }

            rb.linearVelocity = new Vector2(vx, vy);
        }
        else if (currentState == PlayerState.WallSliding)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -wallSlideSpeed, float.MaxValue));
        }
        else if (currentState != PlayerState.Dashing && currentState != PlayerState.KnockedBack && currentState != PlayerState.CometDiving)
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector2(moveInput * moveSpeed * slowFactor, rb.linearVelocity.y);
            }
            else
            {
                // Havadayken yatay hızı koru, ama input ile biraz kontrol ver
                float airControl = 0.7f;
                float targetX = moveInput * moveSpeed * slowFactor;
                float newX = Mathf.Lerp(rb.linearVelocity.x, targetX, airControl * Time.fixedDeltaTime * 5f);
                rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
            }
        }
    }

    private void HandleStateTransitions()
    {
        // ---- wall slide ----------------------------------------------------------------------
        // ENTRY. This is what never existed: WallSliding was handled in three places and set in
        // none, so the whole mechanic was unreachable. Conditions, and why each is here:
        //   · the relic          — it's a pickup, not a base ability
        //   · airborne + falling — you catch a wall on the way DOWN, never on the way up
        //   · pushing into it    — holding away should drop you, or walls become flypaper
        //   · not mid-action     — a dash or a dive through a corridor must not snag on the wall
        bool fallingNow = isGravityReversed ? rb.linearVelocity.y > 0f : rb.linearVelocity.y < 0f;
        bool pushingIntoWall = moveInput != 0f && (moveInput > 0f) == isFacingRight;

        if (currentState != PlayerState.WallSliding
            && !isGrounded && isWallDetected && fallingNow && pushingIntoWall && CanWallSlide()
            && currentState != PlayerState.Dashing
            && currentState != PlayerState.CometDiving
            && currentState != PlayerState.KnockedBack)
        {
            ChangeState(PlayerState.WallSliding);
        }

        // EXIT. Note this tests `!pushingIntoWall`, not `moveInput == 0` as it originally did:
        // letting go drops you, but so does actively holding AWAY from the wall. Without that,
        // steering off a wall left you still stuck to it and walls behaved like flypaper.
        if (currentState == PlayerState.WallSliding && (!isWallDetected || !pushingIntoWall || !CanWallSlide()))
            ChangeState(PlayerState.Jumping);

        if (isGrounded && (currentState == PlayerState.Jumping || currentState == PlayerState.KnockedBack || currentState == PlayerState.WallSliding))
            ChangeState(PlayerState.Idle);

        if (isGrounded && currentState != PlayerState.Dashing && currentState != PlayerState.CometDiving)
        {
            if (moveInput != 0) { ChangeState(PlayerState.Running); }
            else { ChangeState(PlayerState.Idle); }
        }

        if (moveInput > 0 && !isFacingRight) { Flip(); }
        else if (moveInput < 0 && isFacingRight) { Flip(); }
    }

    internal void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }

    // Applies isFacingRight to visualModel.localScale.x, accounting for the 180° Z
    // rotation that reverses the visual X axis during gravity reversal.
    private void ApplyVisualFacing()
    {
        if (visualModel == null) return;
        float sign = (isFacingRight ? 1f : -1f) * (isGravityReversed ? -1f : 1f);
        visualModel.transform.localScale = new Vector3(
            originalVisualScaleX * sign,
            visualModel.transform.localScale.y,
            visualModel.transform.localScale.z);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        ApplyVisualFacing();
    }

    // === SWIMMING ===
    // Called by SwimZone when the player enters/exits swimmable water (Clear/Normal).
    // Hazard waters (Acid/Lava/Poison) use HazardZone and never call these.
    // Swimming is free (no Shift cost) and uses gravity-off, 8-directional control.
    public void EnterWater(SwimZone zone)
    {
        swimZoneCount++;
        if (isSwimming || playerHealth.IsDead || currentState == PlayerState.InCannon) return;

        isSwimming = true;
        currentSwimZone = zone;
        swimExitTimer = 0f;
        swimCachedGravityScale = rb.gravityScale; // restore exactly on exit (handles gravity reversal)
        rb.gravityScale = 0f;
        ChangeState(PlayerState.Swimming);

        // Drives the Cainos "Swim" state machine in AC Character.controller.
        if (animator != null) animator.SetBool("IsInWater", true);
    }

    public void ExitWater(SwimZone zone)
    {
        swimZoneCount = Mathf.Max(0, swimZoneCount - 1);
        if (!isSwimming || swimZoneCount > 0) return; // still inside another overlapping zone

        isSwimming = false;
        currentSwimZone = null;
        swimExitTimer = 0f;
        rb.gravityScale = swimCachedGravityScale;
        if (currentState == PlayerState.Swimming) ChangeState(PlayerState.Jumping);

        if (animator != null) animator.SetBool("IsInWater", false);
    }

    // Upward kick to break the surface and leap out of water. Free, no Shift cost.
    // Starts a brief window where the surface clamp is bypassed so the player can exit.
    private void PerformSwimJump()
    {
        swimExitTimer = swimExitDuration;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, swimExitJumpForce);
        SfxManager.PlayOn(audioSource, jumpSound, soundVolume);
    }

    // Detailed outcome of the most recent card play (Success / Failed / Blocked).
    // DeckManager only needs the bool; UI feedback for Blocked reads this later.
    public CardExecuteResult LastExecuteResult { get; private set; } = CardExecuteResult.Success;

    public bool ExecuteAction(CardActionType type, float value, out bool keepCardInHand)
    {
        LastExecuteResult = cardActionExecutor.TryExecute(type, value, out keepCardInHand);
        return LastExecuteResult == CardExecuteResult.Success;
    }

    // Returns true if the jump actually fired. The bool matters: at 0 Shift this refuses, and the
    // jump buffer must not treat a refusal as a jump or the press is silently swallowed.
    // Ghost Step's remaining free jumps this room. Reset in OnNewRoomEnter.
    [System.NonSerialized] public int freeJumpsLeft = 0;
    public const int GhostStepJumpsPerRoom = 3;

    private bool PerformJump(float jumpForce)
    {
        // Ghost Step: a few jumps each room cost no Shift. Resolved BEFORE the affordability gate,
        // so a free jump is still available at 0 Shift — otherwise the relic would switch off at
        // exactly the moment it is worth having.
        bool ghostFree = freeJumpsLeft > 0
                         && RelicManager.instance != null
                         && RelicManager.instance.HasRelic("GhostStep");

        if (currentShift > 0 || ghostFree)
        {
            if (audioSource != null && jumpSound != null)
            {
                // Live sound is back to the original clip while the procedural "shift" SFX is
                // paused (designer hunting the vibe). Re-wire to ProcSfx.Jump to resume the test.
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                SfxManager.PlayOn(audioSource, jumpSound);
                audioSource.pitch = 1f;
            }
            // ⚠️ Through SpendShift, NOT `currentShift--`. Jumping is the single largest Shift
            // expense in the game, and decrementing the field directly skipped the quest hook that
            // hangs off SpendShift — so the Featherweight oath ("spend 8 Shift or less in a room")
            // was silently not counting jumps at all.
            if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
            {
                // A Ghost Step jump spends the charge instead of the Shift. Consumed here rather
                // than at the check above so a jump that never happens cannot burn one.
                if (ghostFree) freeJumpsLeft--;
                else SpendShift(1);
            }
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            float jumpDir = isGravityReversed ? -1f : 1f;

            // Weight Class: heavier, so it does not go as high. Applied to the impulse rather than
            // to defaultJumpForce, which is serialized and would keep a stale value after a sell.
            if (RelicManager.instance != null && RelicManager.instance.HasRelic("WeightClass"))
                jumpForce *= 0.75f;

            // ⚠️ PURELY VERTICAL, AND THE HORIZONTAL TERM WAS REMOVED ON PURPOSE (2026-08-14).
            //
            // This used to add `moveInput * jumpForce` sideways as well. On a GROUNDED jump that was
            // dead code — isGrounded is still true on the next FixedUpdate, which hard-sets
            // horizontal velocity to moveInput * moveSpeed and erases it about 20ms later.
            //
            // Coyote time reaches this from the other side and would have revived it. A coyote jump
            // happens while isGrounded is FALSE, so FixedUpdate takes the AIR branch instead, which
            // only lerps toward moveSpeed at ~7% per step — the impulse would have survived for the
            // best part of a second. A coyote jump would have flown noticeably further than the
            // ordinary jump it is meant to be indistinguishable from, and every gap in the game
            // would have been clearable by deliberately stepping off the edge first.
            //
            // Safe to delete outright because maxAirJumps is 0, so the ground branch is the only
            // caller: verified no behaviour change for a normal jump.
            rb.AddForce(new Vector2(0f, jumpDir * jumpForce), ForceMode2D.Impulse);
            ChangeState(PlayerState.Jumping);
            return true;
        }
        return false;
    }

    internal IEnumerator DashIFrames(float duration) => playerHealth.GrantInvincibility(duration);

    // Driven dash. Enters the Dashing state — which both the movement FixedUpdate and
    // HandleStateTransitions deliberately leave alone — and HOLDS a flat horizontal velocity
    // for dashDuration, re-asserting it every physics step (with y forced to 0) so it stays
    // level and snappy and works identically on the ground and in the air. Gravity scale is
    // never touched, so the dash composes cleanly with Floor is Lava. On exit it carries a
    // little momentum out instead of dead-stopping. i-frames + procedural afterimages for feel.
    // The PlayerVelocity | Invincibility conflict flags are held by the executor's managed
    // coroutine for the full duration of this routine.
    internal IEnumerator DashRoutine()
    {
        float dir = isFacingRight ? 1f : -1f;

        ChangeState(PlayerState.Dashing);

        // Instant feedback.
        SfxManager.PlayOn(audioSource, dashSound);
        if (dashEffectPrefab != null)
            Instantiate(dashEffectPrefab, transform.position, Quaternion.identity);
        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.12f, 0.55f);

        // i-frames for the whole dash (the field's buffer keeps the momentum tail safe too).
        StartCoroutine(DashIFrames(dashIFrameDuration));

        float elapsed = 0f;
        float ghostTimer = 0f;
        while (elapsed < dashDuration)
        {
            if (playerHealth != null && playerHealth.IsDead) yield break;

            rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

            if (dashAfterimages && visualModel != null)
            {
                ghostTimer -= Time.fixedDeltaTime;
                if (ghostTimer <= 0f)
                {
                    DashAfterimage.Spawn(visualModel.transform, dashAfterimageTint);
                    ghostTimer = 0.03f;
                }
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Carry a bit of momentum out in the dash direction — feels better than a hard stop.
        rb.linearVelocity = new Vector2(dir * dashEndSpeed, rb.linearVelocity.y);

        // Hand control back to the state machine.
        if (currentState == PlayerState.Dashing)
            ChangeState(isGrounded ? PlayerState.Idle : PlayerState.Jumping);
    }

    public void ApplyKnockback(Vector2 knockbackForce) => playerHealth.ApplyKnockback(knockbackForce);

    public void TakeDamage(float damage) => playerHealth.TakeDamage(damage);

    public void OnNewRoomEnter()
    {
        tookDamageThisRoom = false;
        ResetFallTracking();

        // Ghost Step's free jumps refill. Topped up unconditionally rather than only when the relic
        // is held, so picking it up mid-room grants the full allowance immediately instead of
        // silently doing nothing until the next door.
        freeJumpsLeft = GhostStepJumpsPerRoom;

        // Nest Egg watches how much Shift this room costs. Reset here for the same reason.
        shiftSpentThisRoom = 0;

        // Stopgap is once per ROOM, not once per run.
        stopgapUsedThisRoom = false;

        // The portal object itself carries TemporaryObject and is destroyed with the room, but the
        // reference would survive as a Unity fake-null. Clearing it explicitly also takes the range
        // ring down and keeps the "pending portal" state a per-room thing by construction.
        CancelPendingPortal();

        // Same for the return anchor: a marker pointing into the previous room is worse than none.
        ClearReturnAnchor();

        // Starts the oath recorder for this room (no-cards / no-recall / low-shift / no-stagger).
        // Hooked here rather than in LevelManager so it can't be missed by any path that puts the
        // player into a room.
        if (QuestSystem.instance != null) QuestSystem.instance.BeginRoom();

        // Blompo blessings that pay out per room (Time Will Come, Only Child, Compound Interest's
        // counter, Teacher's Pet's opening-hand pull, Slow Burn's per-room tally). Hooked at the
        // same point and for the same reason as the oaths above.
        if (DeckManager.instance != null) DeckManager.instance.BeginRoomForEnhancements();
    }

    // Clears Meteor Greaves fall tracking so a teleport (fall-respawn, room spawn) isn't
    // read as an enormous drop on the next landing. Called from PlayerHealth.FallAndRespawn.
    public void ResetFallTracking() => trackingFall = false;

    // ============================================================================================
    // BOSS RELICS — the only tier that may add an input.
    // ============================================================================================

    [Header("Boss Relics")]
    public float deadDropSpeed = 34f;
    public float grapnelRange = 12f;
    public int grapnelShiftCost = 2;
    public float grapnelPullSpeed = 26f;
    public float stopgapDuration = 1.5f;

    [System.NonSerialized] public bool stopgapUsedThisRoom = false;
    private bool grapnelBusy = false;

    private void HandleBossRelicInput()
    {
        if (RelicManager.instance == null) return;
        // There is no PlayerState.Dead — death is tracked on PlayerHealth. Also gated while
        // cannoned or dashing, where a second movement verb would fight the one already running.
        if (playerHealth != null && playerHealth.IsDead) return;
        if (currentState == PlayerState.InCannon || currentState == PlayerState.Dashing) return;

        // DEAD DROP — S / Down in mid-air slams you straight down. Feeds Meteor Greaves (which
        // needs a 6.5-unit fall) and Freefall Blade (which doubles its damage while falling); both
        // of those previously only triggered by accident.
        if (RelicManager.instance.HasRelic("DeadDrop")
            && !isGrounded && !isSwimming
            && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)))
        {
            float dir = isGravityReversed ? 1f : -1f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.4f, deadDropSpeed * dir);
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.06f, 0.15f);
        }

        // STOPGAP — Q freezes the room for a moment. Once per room, so it is a panic button and not
        // a playstyle: the game charges nothing for TIME, and an unlimited freeze would let a player
        // simply wait out every encounter.
        if (RelicManager.instance.HasRelic("Stopgap")
            && !stopgapUsedThisRoom
            && Input.GetKeyDown(KeyCode.Q))
        {
            stopgapUsedThisRoom = true;
            StartCoroutine(StopgapRoutine());
        }

        // GRAPNEL — F fires a hook at whatever the cursor is pointing at and reels you in.
        if (RelicManager.instance.HasRelic("Grapnel")
            && !grapnelBusy
            && Input.GetKeyDown(KeyCode.F))
        {
            TryGrapnel();
        }
    }

    // Freezes every enemy and projectile in the room without touching Time.timeScale — the global
    // scale is HitStop's and the pause counter's, and borrowing it here would stop the PLAYER too,
    // which is the opposite of what a panic button is for.
    private IEnumerator StopgapRoutine()
    {
        var frozen = new List<Rigidbody2D>();
        var vel = new List<Vector2>();
        var behaviours = new List<MonoBehaviour>();

        foreach (EnemyHealth eh in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (eh == null) continue;
            foreach (MonoBehaviour mb in eh.GetComponentsInChildren<MonoBehaviour>())
            {
                // Freeze the AI, never the health component — a frozen EnemyHealth could not take
                // damage, which would make the panic button also a damage immunity for the enemy.
                if (mb == null || mb is EnemyHealth || !mb.enabled) continue;
                // Matched by TYPE NAME rather than by type, because MonsterController lives in the
                // Cainos.PixelArtMonster_Dungeon namespace and there are three unrelated Projectile
                // types in this project — naming them directly is how ambiguous references start.
                string tn = mb.GetType().Name;
                if (tn.EndsWith("AI") || tn == "MonsterController" || tn == "Turret")
                {
                    mb.enabled = false;
                    behaviours.Add(mb);
                }
            }
            Rigidbody2D erb = eh.GetComponent<Rigidbody2D>();
            if (erb != null) { frozen.Add(erb); vel.Add(erb.linearVelocity); erb.linearVelocity = Vector2.zero; }
        }

        foreach (Projectile p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            if (p == null) continue;
            Rigidbody2D prb = p.GetComponent<Rigidbody2D>();
            if (prb != null) { frozen.Add(prb); vel.Add(prb.linearVelocity); prb.linearVelocity = Vector2.zero; }
            p.enabled = false;
            behaviours.Add(p);
        }

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.2f, 0.35f);

        yield return new WaitForSeconds(stopgapDuration);

        // Everything is null-checked on the way back: an enemy can die to a spike, or the room can
        // change, while the freeze is running.
        for (int i = 0; i < frozen.Count; i++)
            if (frozen[i] != null) frozen[i].linearVelocity = vel[i];
        foreach (MonoBehaviour mb in behaviours)
            if (mb != null) mb.enabled = true;
    }

    private void TryGrapnel()
    {
        if (currentShift < grapnelShiftCost
            && (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.85f;   // chest, not feet
        Vector2 dir = ((Vector2)mouse - origin).normalized;
        if (dir.sqrMagnitude < 0.01f) return;

        // Ground only. Hooking an enemy is a different relic; hooking a trigger would let the player
        // reel themselves into a pickup volume.
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, grapnelRange, terrainLayer);
        if (hit.collider == null) return;

        if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
            SpendShift(grapnelShiftCost);

        StartCoroutine(GrapnelRoutine(hit.point));
    }

    private IEnumerator GrapnelRoutine(Vector2 anchor)
    {
        grapnelBusy = true;
        GrapnelVFX fx = GrapnelVFX.Play(transform, anchor);

        float cachedGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Stop just short of the surface, or the capsule ends up embedded in it.
        const float stopDistance = 0.9f;
        float timeout = Time.time + 1.4f;   // never strand the player if something moves or blocks

        while (Time.time < timeout
               && Vector2.Distance(transform.position, anchor) > stopDistance)
        {
            Vector2 toAnchor = (anchor - (Vector2)transform.position).normalized;
            rb.linearVelocity = toAnchor * grapnelPullSpeed;
            yield return new WaitForFixedUpdate();
        }

        rb.gravityScale = cachedGravity;
        // Keep a little of the momentum so the arrival flows into a jump rather than dead-stopping.
        rb.linearVelocity *= 0.25f;

        if (fx != null) fx.Finish();
        grapnelBusy = false;
    }

    // Meteor Greaves: landing after a fall of at least meteorMinFall stomps a shockwave whose
    // radius and damage scale with how far you dropped (capped at meteorMaxFall). Damage routes
    // through the relic damage hook like every other player source. No-op without the relic.
    private void TryMeteorGreavesLanding(float fallDist)
    {
        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("MeteorGreaves")) return;
        if (fallDist < meteorMinFall) return;
        if (Time.time < meteorReadyAt) return;
        meteorReadyAt = Time.time + meteorCooldown;

        float denom = Mathf.Max(0.01f, meteorMaxFall - meteorMinFall);
        float power01 = Mathf.Clamp01((fallDist - meteorMinFall) / denom);
        float radius = Mathf.Lerp(meteorMinRadius, meteorMaxRadius, power01);
        float damage = Mathf.Lerp(meteorMinDamage, meteorMaxDamage, power01);

        HashSet<IDamageable> struck = new HashSet<IDamageable>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, radius, ~0))
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !struck.Add(target)) continue;
            float dmg = RelicManager.instance.ModifyPlayerDamage(damage, target as EnemyHealth);
            target.TakeDamage(dmg);
        }

        MeteorGreavesVFX.Play(transform.position, radius, power01);
        if (CameraShake.instance != null)
            CameraShake.instance.Shake(Mathf.Lerp(0.12f, 0.4f, power01), Mathf.Lerp(0.25f, 0.6f, power01));

        // Impact sound, louder the further you fell. 2D one-shot rather than positional: the player
        // IS the listener here, and a landing this heavy should hit at full weight every time.
        if (audioSource != null)
            SfxManager.PlayOn(audioSource, ProcSfx.MeteorImpact, Mathf.Lerp(0.55f, 1f, power01) * meteorSfxVolume);
    }

    public bool IsGroundedCheck()
    {
        if (isGravityReversed)
        {
            if (ceilingCheck == null) return false;
            return Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);
        }
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    // ⚠️ A WALL JUMP COSTS SHIFT, exactly like an ordinary jump.
    //
    // The relic grants the SLIDE for free — that's the utility, and it only ever slows a fall. The
    // JUMP has to be paid for. Deckshift's whole thesis is that vertical movement is a resource, and
    // a free wall jump is an unlimited climb: exactly the hole Pogo Boots' Shift refund opened, and
    // a wall is far easier to find than an enemy to bounce on.
    private bool PerformWallJump()
    {
        if (currentShift <= 0) return false;

        if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
            SpendShift(1);

        Flip();
        float jumpDir = isGravityReversed ? -1f : 1f;
        rb.linearVelocity = new Vector2(wallJumpForce.x * (isFacingRight ? 1f : -1f),
                                        wallJumpForce.y * jumpDir);
        ChangeState(PlayerState.Jumping);

        if (audioSource != null && jumpSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.0f);   // a shade lower than a ground jump
            SfxManager.PlayOn(audioSource, jumpSound);
            audioSource.pitch = 1f;
        }
        return true;
    }

    // ⚠️ TERRAIN ONLY, and deliberately NOT `groundLayer`.
    //
    // `groundLayer` is Ground + Enemy, so this used to treat any Enemy-layer enemy as a wall — you
    // could wall-slide and wall-jump off some enemies and not others, purely by which layer that
    // prefab happened to be authored on. A wall is a wall.
    //
    // The origin also used to sit at local y = -0.0098, i.e. just BELOW the capsule's bottom. With
    // Physics2D.queriesStartInColliders on (it is, by default) the ray therefore started INSIDE the
    // floor tile the player was standing on and reported a hit at distance 0 — measured: WallCheck()
    // returned true while standing on open, flat ground. The sensor now sits at mid-body, where a
    // wall check belongs.
    private bool WallCheck()
    {
        if (wallCheck == null) return false;
        return Physics2D.Raycast(wallCheck.position, Vector2.right * (isFacingRight ? 1f : -1f),
                                 wallCheckDistance, terrainLayer);
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = IsGroundedCheck() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (ceilingCheck != null)
        {
            Gizmos.color = isGravityReversed ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(ceilingCheck.position, ceilingCheckRadius);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + (Vector3.right * (isFacingRight ? 1f : -1f) * wallCheckDistance));
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(BiteCenter, biteRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, cometRadius);
    }

    public void SetCurrentEntryPoint(Vector3 entryPoint)
    {
        currentRoomEntryPoint = entryPoint;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeathZone"))
        {
            playerHealth.FallAndRespawn();
            return;
        }

        if (currentState == PlayerState.CometDiving) return;

        bool fallingDown = isGravityReversed ? rb.linearVelocity.y > 0.1f : rb.linearVelocity.y < -0.1f;
        if (fallingDown)
        {
            if (RelicManager.instance == null || !RelicManager.instance.HasRelic("PogoBoots")) return;
            EnemyHealth eHealth = other.GetComponentInParent<EnemyHealth>();
            if (eHealth != null)
            {
                float enemyTopY = other.bounds.center.y + other.bounds.extents.y * 0.5f;
                float enemyBottomY = other.bounds.center.y - other.bounds.extents.y * 0.5f;
#if UNITY_EDITOR && DECKSHIFT_VERBOSE
                // Verbose only: this fires on EVERY enemy trigger overlap and the interpolated
                // string allocates each time. Define DECKSHIFT_VERBOSE to re-enable.
                Debug.Log($"[HeadBounce Trigger] {other.gameObject.name}, playerY: {transform.position.y:F2}, enemyTopY: {enemyTopY:F2}, velocity.y: {rb.linearVelocity.y:F2}");
#endif

                bool positionOk = isGravityReversed ? transform.position.y < enemyBottomY : transform.position.y > enemyTopY;
                if (positionOk)
                    TriggerHeadBounce(eHealth);
            }
        }
    }

    private void PerformFireball(float damageFromCard)
    {
        SfxManager.PlayOn(audioSource, fireballCastSound);
        if (fireballPrefab == null || firePoint == null) return;

        Quaternion fireballRotation = !isFacingRight ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        GameObject fireballInstance = Instantiate(fireballPrefab, firePoint.position, fireballRotation);

        Fireball fireballScript = fireballInstance.GetComponent<Fireball>();
        if (fireballScript != null)
        {
            fireballScript.damage = damageFromCard;
            // Stamp the card that fired it. The shot outlives the play, so this is the only way its
            // blessings (Finisher, Grudge, Toll Booth…) can still apply when it finally lands.
            if (DeckManager.instance != null)
                fireballScript.sourceCard = DeckManager.instance.AttributedCard;
        }
    }

    // The Ninja's aimed throw.
    //
    // ⚠️ AIM COMES FROM THE CURSOR, AND THE CURSOR IS ALREADY WHERE THE PLAYER WANTS IT. Cards are
    // cast with a LEFT CLICK, so the mouse at the moment of the cast IS the aim — this needs no
    // extra input mode, no charge-up and no confirm step. The aim indicator draws the same line
    // beforehand, so what you see before the click is what you get.
    public void ThrowShuriken(float damageFromCard)
    {
        if (mainCamera == null) return;

        Vector2 origin = ShurikenOrigin;
        Vector2 aim = (Vector2)mainCamera.ScreenToWorldPoint(Input.mousePosition) - origin;
        if (aim.sqrMagnitude < 0.0001f) aim = new Vector2(isFacingRight ? 1f : -1f, 0f);
        aim.Normalize();

        // Turn to the throw. Throwing left while facing right reads as a bug, and facing is what
        // every other system reads for direction anyway.
        if (aim.x > 0.01f && !isFacingRight) Flip();
        else if (aim.x < -0.01f && isFacingRight) Flip();

        RuntimeCard src = DeckManager.instance != null ? DeckManager.instance.AttributedCard : null;

        if (throwRoutine != null) StopCoroutine(throwRoutine);
        throwRoutine = StartCoroutine(ThrowRoutine(aim, damageFromCard, src));
    }

    private Coroutine throwRoutine;

    // ⚠️ THE STAR LEAVES ON THE ARM'S SNAP, NOT ON THE KEYPRESS. Spawning it immediately made the
    // shuriken outrun the animation — it was already gone before he moved. The aim is captured at
    // the press (so it is still exactly where the cursor was), and only the release waits.
    private IEnumerator ThrowRoutine(Vector2 aim, float damage, RuntimeCard src)
    {
        BeginThrowPose();
        yield return new WaitForSeconds(THROW_RELEASE);

        // Re-read the hand: it has moved during the wind-up, and throwing from where it WAS looks
        // like the star spawned beside him.
        Vector2 origin = ShurikenOrigin;

        SfxManager.PlayOn(audioSource, shurikenThrowSound);
        HideHeldWeapon(0.28f);
        Shuriken.Spawn(origin + aim * 0.3f, aim, damage, src, shurikenSprite);

        EndThrowPose();
        throwRoutine = null;
    }

    // ⚠️ THE STAR IN HIS HAND IS THE STAR THAT FLIES. The held weapon is hidden for the length of
    // the throw and comes back after — so the projectile reads as the object he was holding rather
    // than as a second one conjured out of nowhere. Cosmetic only; nothing else looks at it.
    private Coroutine heldWeaponHide;

    private void HideHeldWeapon(float seconds)
    {
        SpriteRenderer[] parts = HeldWeaponRenderers();
        if (parts == null || parts.Length == 0) return;

        if (heldWeaponHide != null) StopCoroutine(heldWeaponHide);
        heldWeaponHide = StartCoroutine(HideHeldWeaponRoutine(parts, seconds));
    }

    private IEnumerator HideHeldWeaponRoutine(SpriteRenderer[] parts, float seconds)
    {
        foreach (SpriteRenderer sr in parts) if (sr != null) sr.enabled = false;
        yield return new WaitForSeconds(seconds);
        // Re-shown unconditionally: a throw interrupted by death or a room change must never leave
        // the character permanently empty-handed.
        foreach (SpriteRenderer sr in parts) if (sr != null) sr.enabled = true;
        heldWeaponHide = null;
    }

    private SpriteRenderer[] HeldWeaponRenderers()
    {
        Transform slot = HeldWeaponSlot;
        return slot != null ? slot.GetComponentsInChildren<SpriteRenderer>(true) : null;
    }

    private Transform HeldWeaponSlot
    {
        get
        {
            if (cachedWeaponSlot != null) return cachedWeaponSlot;
            if (visualModel == null) return null;
            foreach (Transform t in visualModel.GetComponentsInChildren<Transform>(true))
                if (t.name == "Weapon Slot") { cachedWeaponSlot = t; break; }
            return cachedWeaponSlot;
        }
    }
    private Transform cachedWeaponSlot;

    // Thrown from the HAND, so the star leaves from where the player can see it leave. Falls back
    // to the body centre if the rig has no weapon slot.
    //
    // ⚠️ NOT `firePoint`. That sits out at the wand hand on the facing side — right for a forward
    // cast, wrong for a 360° throw, where aiming straight up or down launches the star off to one
    // side of the player. The aim indicator reads this SAME property, so the preview and the throw
    // start from one origin and can never disagree.
    internal Vector2 ShurikenOrigin
    {
        get
        {
            Transform slot = HeldWeaponSlot;
            if (slot != null) return slot.position;

            if (bodyCapsule == null) bodyCapsule = GetComponent<CapsuleCollider2D>();
            return bodyCapsule != null
                ? (Vector2)transform.position + bodyCapsule.offset
                : (Vector2)transform.position;
        }
    }

    // ⚠️ ATTACK ACTION 13 IS **THROW**, NOT 14. 14 is Cast — the wizard's two-handed spell pose,
    // which is what this used before and is exactly why it looked wrong AND ran long: Cast is a 1.0s
    // clip that self-exits at 80%, so every star cost nearly a second of standing still.
    //
    // Throw is a wind-up / hold / release set (Throw Start -> Throw Loop -> Throw End), driven by
    // IsAttacking: true winds up and holds, false releases. So a quick throw is simply a very short
    // hold. `AttackSpeedMul` scales both halves, and at 2.2 the whole action lands near 0.45s
    // against Cast's ~1.3s.
    private const int THROW_ACTION = 13;
    private const float THROW_SPEED = 2.2f;
    private const float THROW_RELEASE = 0.13f;   // hold before the arm snaps forward

    private void BeginThrowPose()
    {
        if (animator == null) return;
        animator.SetFloat("AttackSpeedMul", THROW_SPEED);
        animator.SetInteger("AttackAction", THROW_ACTION);
        animator.SetBool("IsAttacking", true);
    }

    private void EndThrowPose()
    {
        if (animator == null) return;
        // Dropping IsAttacking is what fires Throw End — the release itself, not a tidy-up.
        animator.SetBool("IsAttacking", false);
        // ⚠️ Put the multiplier back. It is a GLOBAL animator parameter shared by every attack
        // state, so leaving it at 2.2 would quietly double-speed the Fireball cast for any
        // character who picked up a Shuriken.
        animator.SetFloat("AttackSpeedMul", 1f);
    }

    internal IEnumerator FireballCastRoutine(float damageFromCard)
    {
        if (animator != null)
        {
            animator.SetInteger("AttackAction", 14);
            animator.SetBool("IsAttacking", true);
        }

        yield return new WaitForSeconds(fireballCastDelay);

        PerformFireball(damageFromCard);

        yield return new WaitForSeconds(0.39f);

        if (animator != null)
            animator.SetBool("IsAttacking", false);
    }

    public void AddShift(int amount)
    {
        currentShift = Mathf.Min(currentShift + amount, maxShift);
    }

    public void ResetShiftToMax()
    {
        currentShift = maxShift;
    }

    public void SpendShift(int amount)
    {
        if (amount <= 0) return;
        currentShift = Mathf.Max(0, currentShift - amount);

        // Every Shift cost in the game funnels through here — jumps, cards, recall, portals — so
        // the Featherweight oath gets a complete per-room total from one hook. Callers already gate
        // this on the hub rule, so sandbox spending is never counted.
        if (QuestSystem.instance != null) QuestSystem.instance.NoteShiftSpent(amount);

        // Nest Egg reads the same total, for the same reason: one funnel, so no Shift cost added
        // later can forget to be counted.
        shiftSpentThisRoom += amount;
    }

    // How much Shift this room has cost so far. Nest Egg is scored against it when the room is
    // LEFT (see ExitDoor), never mid-room — a frugal room is only frugal once it's over.
    [System.NonSerialized] public int shiftSpentThisRoom = 0;
    public const int NestEggShiftCeiling = 5;
    public const int NestEggReward = 2;

    /// <summary>
    /// Scored on leaving a room. Nest Egg pays permanent max Shift for a room crossed cheaply,
    /// which is the Featherweight oath's shape as a relic.
    /// </summary>
    public void ScoreRoomRelics()
    {
        if (LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub()) return;
        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("NestEgg")) return;
        if (shiftSpentThisRoom > NestEggShiftCeiling) return;

        IncreaseMaxShift(NestEggReward);
        nestEggRoomsBanked++;
        Debug.Log($"🥚 Nest Egg: room crossed on {shiftSpentThisRoom} Shift. +{NestEggReward} max Shift (now {maxShift}).");
    }

    // Purely for the relic's live readout — how many rooms Nest Egg has actually paid out on.
    [System.NonSerialized] public int nestEggRoomsBanked = 0;

    // Would the player's capsule fit standing with its FEET at `feetPos`? Shared by every card that
    // puts the player somewhere: a portal and a return anchor are both places you ARRIVE at, so the
    // honest test is the player's own body, not a point sample. Nothing else would stop you
    // teleporting into the middle of a wall, and unlike Phase (which has EjectFromGeometry) neither
    // card has any recovery, so that was a dead run.
    //
    // Uses terrainLayer, NOT groundLayer: groundLayer deliberately contains Enemy so the player can
    // land on heads, and an enemy wandering past should not veto a placement.
    internal bool PlayerFitsAt(Vector2 feetPos)
    {
        if (capsuleCollider == null) return true;
        Vector2 center = feetPos + capsuleCollider.offset;
        return !Physics2D.OverlapBox(center, capsuleCollider.size * 0.9f, 0f, terrainLayer);
    }

    // Every teleport in the game should funnel through here. Currently: Portal traversal (via
    // Teleportable), Second Thoughts' return, and the out-of-bounds fall respawn.
    //
    // ⚠️ THE PHASE RE-ANCHOR IS LOAD-BEARING. The Phase bubble is anchored in WORLD space, so a
    // teleport taken mid-Phase would drop the player outside it and ClampPhaseVelocity would haul
    // them straight back — silently undoing the trip they just paid for. Portalling while phasing
    // did exactly that until this existed. Moving the bubble with them is the honest reading: the
    // limit is "how far you may travel under your own power", not "where you happen to be".
    internal void OnTeleported()
    {
        ResetFallTracking();   // a teleport is not a fall — don't Meteor Greaves on the next landing

        if (!isPhasing) return;
        phaseAnchor = BiteCenter;
        if (phaseBoundary != null) phaseBoundary.Reanchor(phaseAnchor);
    }

    // Second Thoughts. First play drops the anchor and keeps the card; the second snaps back to it
    // from anywhere in the room and spends the card. Mirrors Portal's two-stage shape on purpose —
    // one card teaching the pattern makes the other easier to read.
    internal bool TryReturnAnchor(out bool keepCard)
    {
        keepCard = false;

        if (!hasReturnAnchor)
        {
            returnAnchorPos = rb.position;             // the FEET: exactly where the player will be put back
            hasReturnAnchor = true;
            returnAnchorVfx = ReturnAnchorVFX.Spawn(returnAnchorPos);
            keepCard = true;                           // dropping the marker is free; the trip costs
            return true;
        }

        // The spot the player stood on can stop being standable — a gate closing over it is the
        // realistic case. Refuse rather than teleport them into it: a refused play costs nothing and
        // keeps the card, so the anchor stays available once whatever it is has moved.
        if (!PlayerFitsAt(returnAnchorPos))
        {
            keepCard = true;
            return false;
        }

        // ⚠️ BOTH, NOT JUST THE TRANSFORM. Physics2D.autoSyncTransforms is OFF in this project, so a
        // transform write leaves rb.position reporting the OLD spot until the next physics step.
        // ClampPhaseVelocity reads rb.position, so returning mid-Phase would spend that step
        // believing the player was far outside their bubble and drag them to its edge.
        rb.position = returnAnchorPos;
        transform.position = returnAnchorPos;
        rb.linearVelocity = Vector2.zero;              // otherwise you arrive still falling at speed
        OnTeleported();
        ClearReturnAnchor();
        return true;
    }

    // Drops the marker without taking the trip. Called when the card leaves the hand unspent
    // (Recall), and on room change / death, for the same reasons as CancelPendingPortal.
    internal void ClearReturnAnchor()
    {
        hasReturnAnchor = false;
        if (returnAnchorVfx != null) { returnAnchorVfx.Dismiss(); returnAnchorVfx = null; }
    }

    // The single source of truth for "may a portal go here right now?". CardAimIndicator calls this
    // exact method for its preview, so the ghost can never disagree with what the click will do.
    internal bool IsPortalPlacementValid(Vector2 spot)
    {
        if (!PlayerFitsAt(spot)) return false;

        return firstPortalInstance == null
            ? Vector2.Distance(BiteCenter, spot) <= portalPlaceRange       // first: near the PLAYER
            : Vector2.Distance(firstPortalInstance.transform.position, spot) <= portalMaxRange;
    }

    // Throws away a first portal that never got its pair. Without this the pending portal and its
    // range ring sat in the room forever and firstPortalInstance stayed set, so the NEXT Portal play
    // in that room placed the second half and charged for it — the card silently did something other
    // than what it looked like it was doing. Called on deselect, Recall, room change and death.
    internal void CancelPendingPortal()
    {
        if (firstPortalInstance == null) return;
        Destroy(firstPortalInstance.gameObject);
        firstPortalInstance = null;
    }

    // ⚠️ THIS NO LONGER SPENDS SHIFT. It used to charge its own cost, which meant it only knew about
    // the Kinetic discount and silently ignored Blompo's "On the House" and the First One's Free
    // relic — the latter being consumed on the second placement while the player was charged anyway.
    // DeckManager.PlayCard now pays for Portal exactly like every other card (see the spend inside
    // its `success && !keepInHand` block), so every discount applies for free and the cost lives in
    // ONE place. That also retires the old trap where Portal's price was stored twice, in `shiftCost`
    // (what the card face and the affordability gate used) and `actionValue` (what was really
    // charged) — two fields that had to agree and were never checked against each other.
    internal bool TryPlacePortal(out bool keepCard)
    {
        keepCard = false;
        if (portalPrefab == null) return false;
        if (mainCamera == null) return false;

        Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);

        // Out of range or inside rock: refuse without cost and keep the card. The aim indicator has
        // already been showing this spot as invalid, so a refusal here is never a surprise.
        if (!IsPortalPlacementValid(mousePos))
        {
            keepCard = true;
            return false;
        }

        if (firstPortalInstance == null)
        {
            GameObject p1 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
            firstPortalInstance = p1.GetComponent<Portal>();
            firstPortalInstance.spriteRenderer.color = Color.gray;
            firstPortalInstance.ShowRangeCircle(portalMaxRange);

            keepCard = true;      // first placement is free; the card is spent on the second
            return true;
        }

        GameObject p2 = Instantiate(portalPrefab, mousePos, Quaternion.identity);
        firstPortalInstance.Link(p2.GetComponent<Portal>());
        firstPortalInstance = null;

        keepCard = false;
        return true;
    }

    // Returns true if the bite landed on a target. Returns false when nothing damageable is in
    // range, so the play is refused upstream (no Shift/charge spent, card stays in hand) instead
    // of whiffing into empty air.
    internal bool PerformVampiricBite(float damageAmount)
    {
        // ~0 = all layers: avoids the AeroBat/MeleeEnemy Default-layer miss from enemyLayer mask.
        // GetComponentInParent finds EnemyHealth even when the collider is on a child.
        // Centered on the body (BiteCenter), not the wand — a regular circle around the player.
        Collider2D[] hits = Physics2D.OverlapCircleAll(BiteCenter, biteRange, ~0);
        foreach (Collider2D hit in hits)
        {
            IDamageable targetHealth = hit.GetComponentInParent<IDamageable>();
            if (targetHealth == null) continue;

            SfxManager.PlayOn(audioSource, vampireBiteSound);   // only chomp when there's a real target
            float biteDamage = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(damageAmount, targetHealth as EnemyHealth)
                : damageAmount;
            targetHealth.TakeDamage(biteDamage);
            if (targetHealth is EnemyHealth) Heal(biteHealAmount);
            if (biteEffectPrefab != null)
                Instantiate(biteEffectPrefab, hit.transform.position, Quaternion.identity);  // BiteVFX self-destroys

            if (CameraShake.instance != null)
                CameraShake.instance.Shake(0.08f, 0.25f);   // chomp impact

            return true; // one bite, one target
        }
        return false; // nothing in range — play refused, card retained
    }

    public void Heal(float amount) => playerHealth.Heal(amount);

    // Snap back to where this room started. Used by the DeathZone trigger and by LevelManager's
    // out-of-bounds net, which is what catches a player who has Phased clean out of the level.
    public void ReturnToEntryPoint() => playerHealth.FallAndRespawn();

    internal void PerformGlassWail(float stunDuration)
    {
        SfxManager.PlayOn(audioSource, glassVailSound);

        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

        if (CameraShake.instance != null)
            CameraShake.instance.Shake(0.5f, 0.5f);

        // Wail origin: a shockwave ripples out from the player so the screen-wide stun reads visually.
        // World-space prefab (self-destroys), so spawn it directly rather than via the UI-canvas helper.
        if (glassWailEffect != null)
            Instantiate(glassWailEffect, transform.position, Quaternion.identity);

        foreach (EnemyHealth enemy in allEnemies)
        {
            enemy.Stun(stunDuration);
        }
    }

    // Glass Parry: a 0.5s window. Eat a hit inside it and the hit is negated, the card's
    // charge is refunded (mastery = self-sustaining), and glass shards riposte everything
    // close for actionValue damage. Let the window close on nothing and the charge is
    // simply gone — Glass identity: brutal on failure, brilliant on success.
    private const float ParryWindowDuration = 0.5f;

    internal IEnumerator GlassParryRoutine(float riposteDamage, RuntimeCard playedCard)
    {
        GlassParryVFX windowVfx = GlassParryVFX.SpawnWindow(transform, ParryWindowDuration);
        playerHealth.BeginParryWindow();

        float elapsed = 0f;
        while (elapsed < ParryWindowDuration && !playerHealth.ParryTriggered)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        bool success = playerHealth.ParryTriggered;   // must read BEFORE EndParryWindow clears it
        playerHealth.EndParryWindow();

        if (!success)
        {
            // Window closed on nothing. The charge was already spent — that's the cost.
            yield break;
        }

        if (windowVfx != null) windowVfx.CutShort();

        if (DeckManager.instance != null) DeckManager.instance.RefundCharge(playedCard);

        SfxManager.PlayOn(audioSource, glassParrySound);
        GlassParryVFX.SpawnShatter(transform.position);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.25f, 0.45f);

        // Brief mercy so the parried enemy's lingering hitbox can't clip you next frame.
        StartCoroutine(playerHealth.GrantInvincibility(0.35f));

        // The riposte: shards bite everyone close. Same all-layers + parent-lookup pattern
        // as Vampiric Bite (enemy colliders aren't reliably on the Enemy layer).
        HashSet<IDamageable> struck = new HashSet<IDamageable>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, parryRiposteRange, ~0))
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || struck.Contains(target)) continue;
            struck.Add(target);

            float finalDamage = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(riposteDamage, target as EnemyHealth)
                : riposteDamage;
            target.TakeDamage(finalDamage);
        }
    }

    // Freefall Blade: a ")" arc slash — out in front and wrapping down below the feet.
    // Playable grounded or airborne, but while actually falling it hits for DOUBLE damage
    // AND swings a BIGGER arc (freefallBladeFallingRangeMul, designer 2026-07-17) —
    // Momentum: the fall you already paid for becomes a weapon. Always plays, even
    // into empty air (designer 2026-07-15) — the swing itself costs the charge.
    internal bool PerformFreefallBlade(float damageAmount)
    {
        bool falling = !isGrounded && rb.linearVelocity.y < -0.01f;
        float damage = falling ? damageAmount * 2f : damageAmount;
        float range = falling ? freefallBladeRange * freefallBladeFallingRangeMul : freefallBladeRange;
        float facing = isFacingRight ? 1f : -1f;

        // One circle seated forward-and-low covers the bracket: its top edge reaches
        // chest height in front, its bottom wraps under the feet.
        Vector2 center = (Vector2)transform.position
                       + new Vector2(facing * range * 0.55f, -range * 0.35f);

        HashSet<IDamageable> struck = new HashSet<IDamageable>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, range, ~0))
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || struck.Contains(target)) continue;
            struck.Add(target);

            float finalDamage = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(damage, target as EnemyHealth)
                : damage;
            target.TakeDamage(finalDamage);
        }

        SfxManager.PlayOn(audioSource, freefallBladeSound);
        FreefallBladeVFX.Spawn(transform.position, isFacingRight, falling, range);
        if (struck.Count > 0 && CameraShake.instance != null)
            CameraShake.instance.Shake(falling ? 0.15f : 0.08f, falling ? 0.35f : 0.2f);

        return true;
    }

    // Keeps a phasing player inside the bubble anchored at the cast point (see phaseMaxRadius).
    //
    // Two halves, and both are needed. The VELOCITY projection strips the outward component so
    // pressing the edge SLIDES along it: a dead stop reads as the controls breaking, and the player
    // still has to be able to travel around the inside of the bubble to find somewhere solid to
    // land before the timer runs out. The POSITION backstop exists because a tangential slide cuts
    // a chord across the circle, creeping a fraction of a unit outward every step — small per step,
    // but it compounds across the whole cast, and anything else that moves the player (knockback,
    // a hazard) would otherwise leave them permanently outside with no way back in.
    private Vector2 ClampPhaseVelocity(Vector2 desired)
    {
        if (phaseMaxRadius <= 0f) return desired;

        Vector2 bodyOffset = capsuleCollider != null ? capsuleCollider.offset : Vector2.zero;
        Vector2 body = rb.position + bodyOffset;
        Vector2 outward = body - phaseAnchor;
        float dist = outward.magnitude;

        if (dist < 0.0001f) return desired;             // sitting on the anchor; no outward axis yet

        Vector2 n = outward / dist;

        if (dist > phaseMaxRadius)
            rb.position = phaseAnchor + n * phaseMaxRadius - bodyOffset;

        float radial = Vector2.Dot(desired, n);
        if (radial <= 0f) return desired;               // heading back inward is always allowed

        // Don't interfere at all unless this step would actually leave the bubble.
        if (((body + desired * Time.fixedDeltaTime) - phaseAnchor).sqrMagnitude
            <= phaseMaxRadius * phaseMaxRadius) return desired;

        return desired - n * radial;                    // tangent only: slide along the boundary
    }

    internal IEnumerator PhaseRoutine(float duration)
    {
        isPhasing = true;

        // Anchor the bubble on the BODY CENTER, not the transform (which sits at the feet), so the
        // drawn boundary is centred on the player and matches what ClampPhaseVelocity enforces.
        phaseAnchor = BiteCenter;
        phaseBoundary = PhaseBoundary.Spawn(phaseAnchor, phaseMaxRadius, this);

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayerIndex, true);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, true);

        // Pass duration + 1.5f so the pulse covers the maximum possible extension time.
        // PhaseRoutine always stops it explicitly; the extra headroom just prevents a
        // static mid-pulse tint if wall-stuck extension runs to the full 1s cap.
        phaseVisualCoroutine = StartCoroutine(PhaseVisualRoutine(duration + 1.5f));

        yield return new WaitForSeconds(duration);

        float extensionTime = 0f;
        while (IsCollidingWithGround() && extensionTime < 1f)
        {
            yield return new WaitForSeconds(0.1f);
            extensionTime += 0.1f;
        }
        if (IsCollidingWithGround()) EjectFromGeometry();

        if (phaseVisualCoroutine != null) { StopCoroutine(phaseVisualCoroutine); phaseVisualCoroutine = null; }
        RestorePhaseVisuals();

        // Normal exit. The boundary also self-collapses if it sees isPhasing go false without this
        // running (dying mid-Phase kills the coroutine before it reaches here).
        if (phaseBoundary != null) { phaseBoundary.Collapse(); phaseBoundary = null; }

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayerIndex, false);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, false);

        rb.gravityScale = originalGravity;

        isPhasing = false;
    }

    private IEnumerator PhaseVisualRoutine(float duration)
    {
        if (phaseVisuals == null || phaseVisuals.Length == 0) yield break;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float pulse = (Mathf.Sin(Time.time * Mathf.PI * 4f) + 1f) * 0.5f;
            block.SetFloat("_Alpha", Mathf.Lerp(0.3f, 0.6f, pulse));
            for (int i = 0; i < phaseVisuals.Length; i++)
            {
                if (phaseVisuals[i] == null) continue;
                phaseVisuals[i].SetPropertyBlock(block);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Safety net: restore if PhaseRoutine doesn't stop this coroutine first.
        block.SetFloat("_Alpha", 1f);
        for (int i = 0; i < phaseVisuals.Length; i++)
        {
            if (phaseVisuals[i] == null) continue;
            phaseVisuals[i].SetPropertyBlock(block);
        }
    }

    private void RestorePhaseVisuals()
    {
        if (phaseVisuals == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetFloat("_Alpha", 1f);
        for (int i = 0; i < phaseVisuals.Length; i++)
        {
            if (phaseVisuals[i] == null) continue;
            phaseVisuals[i].SetPropertyBlock(block);
        }
    }

    // Getting the player OUT of solid geometry when Phase expires inside a wall.
    //
    // ⚠️ THE OLD VERSION NUDGED 0.5 UNITS UP AND HOPED. It never checked that the destination was
    // free, and half a unit does not clear a 2-thick wall, so a player who ran Phase out deep inside
    // rock stayed embedded and could not move at all — a dead run with no way to recover. The
    // designer hit exactly that.
    //
    // This searches outward in rings for a position the capsule actually FITS in, nearest first,
    // and only then moves. Because it verifies the destination, it cannot leave the player somewhere
    // still solid; because it fans out in every direction it handles walls and ceilings, not just
    // floors; and because it ends in a guaranteed fallback the player can never be stuck for good.
    private void EjectFromGeometry()
    {
        Vector3 safe;
        if (TryFindSafePosition(out safe))
        {
            transform.position = safe;
        }
        else
        {
            // Nothing within the search radius — a pocket sealed on every side, which shouldn't
            // happen but must not be a lost run if it does. The room's entry point is the one
            // position guaranteed to be standable, and it's the same recovery a fall uses.
            transform.position = currentRoomEntryPoint;
            Debug.LogWarning("[Phase] No free space near the player; recovered to the room entry point.");
        }

        // Kill the velocity that carried them in. Ejecting upward while still travelling downward
        // fast enough can tunnel straight back into the same geometry on the next physics step.
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    // Rings outward from the player, nearest first. Within each ring the directions are ordered by
    // how close they are to "up" (or down, under reversed gravity), so given two equally near exits
    // the player surfaces ON TOP of the geometry, which is what they expect.
    private bool TryFindSafePosition(out Vector3 result)
    {
        result = transform.position;

        const float STEP = 0.25f;
        const int RINGS = 28;                       // reaches 7 units — wider than any wall in the pool
        float flip = isGravityReversed ? -1f : 1f;

        for (int r = 1; r <= RINGS; r++)
        {
            float radius = r * STEP;
            for (int i = 0; i < EjectDirections.Length; i++)
            {
                Vector2 d = EjectDirections[i];
                Vector3 candidate = transform.position
                                  + new Vector3(d.x * radius, d.y * radius * flip, 0f);
                if (IsPositionClear(candidate))
                {
                    result = candidate;
                    return true;
                }
            }
        }
        return false;
    }

    // Straight up first, then fanning symmetrically out to the sides, with straight down last.
    private static readonly Vector2[] EjectDirections = BuildEjectDirections();

    private static Vector2[] BuildEjectDirections()
    {
        const float STEP_DEG = 22.5f;
        List<Vector2> dirs = new List<Vector2> { Vector2.up };
        for (float a = STEP_DEG; a < 180f; a += STEP_DEG)
        {
            float rad = a * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
            dirs.Add(new Vector2(s, c));    // lean right
            dirs.Add(new Vector2(-s, c));   // mirror left
        }
        dirs.Add(Vector2.down);
        return dirs.ToArray();
    }

    // Would the capsule be free of solid geometry if it stood here? Deliberately uses the SAME box
    // and mask as IsCollidingWithGround, so a position this call approves is one that call agrees is
    // not stuck — otherwise the search could "succeed" somewhere still considered embedded.
    private bool IsPositionClear(Vector3 worldPos)
    {
        if (capsuleCollider == null) return true;
        return !Physics2D.OverlapBox(CapsuleCenterAt(worldPos), capsuleCollider.size * 0.9f, 0f, groundLayer);
    }

    private bool IsCollidingWithGround()
    {
        if (capsuleCollider == null) return false;
        // 0.9f shrink avoids a false positive from the player barely touching the floor normally
        return Physics2D.OverlapBox(CapsuleCenterAt(transform.position), capsuleCollider.size * 0.9f, 0f, groundLayer);
    }

    // Where the player's capsule would sit if the transform were at `worldPos`.
    //
    // ⚠️ DERIVED FROM THE TRANSFORM, NOT FROM `capsuleCollider.bounds`. Unity's
    // `Physics2D.autoSyncTransforms` is OFF by default, so a collider's `bounds` still report the
    // player's PREVIOUS position until the next physics step — and the whole point of the eject is
    // to test positions the player has not moved to yet, then move and re-check. Reading `bounds`
    // made the search measure every candidate from a stale origin, so it "found" a clear spot,
    // teleported there, and left the player just as embedded as before.
    //
    // This is exact because the player root is guaranteed to be scale (1,1,1) — a hard project rule
    // (see the Facing System: facing is applied to visualModel, never to the root).
    private Vector2 CapsuleCenterAt(Vector3 worldPos)
    {
        return (Vector2)worldPos + capsuleCollider.offset;
    }

    // Re-enables the layer pairs PhaseRoutine ignores. Runs unconditionally on death:
    // ignoring is only ever set by Phase, and clearing an already-clear pair is a no-op,
    // so this is safe whether or not a Phase was active when the player died.
    private void RestorePhaseLayerCollisions()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        Physics2D.IgnoreLayerCollision(playerLayer, LayerMask.NameToLayer("Ground"), false);
        Physics2D.IgnoreLayerCollision(playerLayer, LayerMask.NameToLayer("Enemy"), false);
    }

    public void IncreaseMaxShift(int amount)
    {
        maxShift += amount;
        currentShift += amount;
    }

    public void EnterCannon(Transform cannonTransform)
    {
        ChangeState(PlayerState.InCannon);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // GÜNCELLENDİ: Artık SpriteRenderer yerine yeni görsel modeli (visualModel) açıp kapatıyoruz.
        if (visualModel != null) visualModel.SetActive(false);

        transform.position = cannonTransform.position;
        transform.SetParent(cannonTransform);
    }

    // Mirrors the dive's PlayerVelocity flag: true from StartCometDive until the first
    // EndCometDive. Needed because the fall-respawn path can call EndCometDive twice
    // for one dive (once at respawn, once when the still-diving state lands at spawn).
    private bool cometDiveWindowActive;

    internal void StartCometDive()
    {
        ChangeState(PlayerState.CometDiving);
        cometDiveWindowActive = true;
        // Manual flag: the dive holds PlayerVelocity until it lands (or is interrupted)
        // — an open-ended window the executor can't see via ManagedCoroutine.
        // ConflictFlags has no player-state flag, so PlayerVelocity is the whole claim.
        cardActionExecutor?.SetManualFlag(ConflictFlags.PlayerVelocity, true);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -cometSpeed);
        if (diveTrail != null) diveTrail.emitting = true;
        SfxManager.PlayOn(audioSource, cometDiveSound);

        if (cometVfx != null) cometVfx.Cancel();   // defensive: never leave an orphan telegraph behind
        cometVfx = CometDiveVFX.Begin(this, cometRadius);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == PlayerState.CometDiving)
        {
            bool isGround = (groundLayer.value & (1 << collision.gameObject.layer)) > 0;
            if (isGround)
                LandCometDive();
            return;
        }

        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("PogoBoots")) return;
        EnemyHealth eHealth = collision.gameObject.GetComponentInParent<EnemyHealth>();
        if (eHealth != null)
        {
            ContactPoint2D contact = collision.GetContact(0);
#if UNITY_EDITOR && DECKSHIFT_VERBOSE
            // Verbose only: fires on EVERY enemy collision (see the trigger path above).
            Debug.Log($"[HeadBounce] Collision: {collision.gameObject.name}, normal.y: {contact.normal.y:F2}, velocity.y: {rb.linearVelocity.y:F2}, canBounce: {eHealth.canBeHeadBounced}");
#endif

            bool normalFromBelow = isGravityReversed ? contact.normal.y < -0.7f : contact.normal.y > 0.7f;
            if (normalFromBelow)
                TriggerHeadBounce(eHealth);
        }
    }

    // Pogo Boots. REBALANCED 2026-08-10 — read this before touching the numbers.
    //
    // The old version granted `AddShift(1)` on every bounce with only a 0.3s cooldown, which made
    // this the ONLY free Shift regeneration in the game. In a game whose stated identity is that
    // Shift does not regenerate on its own and carries over for the whole run, that quietly turned
    // every room containing enemies into a refuelling station: a 40 HP melee enemy is five bounces
    // at 8 damage each, so a room of six was worth roughly half a full Shift bar for nothing.
    //
    // Three changes, and they are meant to work together:
    //   NO SHIFT REFUND    — the hole in the core resource, closed. The boots are a movement toy;
    //                        movement is what they pay in.
    //   ONE BOUNCE PER ENEMY PER AIRTIME — camping a single slime until it dies was the degenerate
    //                        line and it was also the boring one. Chaining ACROSS several enemies
    //                        is the trick worth rewarding, so that is the only thing still allowed.
    //   DECAYING CHAIN     — each successive bounce before touching the ground lifts less, so a
    //                        chain can't sustain itself indefinitely across a dense room.
    private void TriggerHeadBounce(EnemyHealth eHealth)
    {
        if (!eHealth.canBeHeadBounced) return;
        if (Time.time < _headBounceCooldown) return;

        // HashSet.Add returns false when the enemy is already in the set, so this is both the
        // "have I bounced this one already?" test and the record of it.
        if (!_bouncedThisAirtime.Add(eHealth.GetInstanceID())) return;

        _headBounceCooldown = Time.time + 0.3f;
        eHealth.TakeDamage(8f);

        float lift = 0.70f;
        if (pogoChainFalloff != null && pogoChainFalloff.Length > 0)
            lift = pogoChainFalloff[Mathf.Min(_bounceChain, pogoChainFalloff.Length - 1)];
        _bounceChain++;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        float bounceDir = isGravityReversed ? -1f : 1f;
        rb.AddForce(Vector2.up * defaultJumpForce * lift * bounceDir, ForceMode2D.Impulse);

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.2f);
    }

    // Single choke point for ending the dive window — every exit path (LandCometDive,
    // knockback, death, fall-respawn) calls this. The guard clears the flag exactly
    // once per dive even when a path calls EndCometDive twice (fall-respawn does).
    internal void EndCometDive()
    {
        if (cometDiveWindowActive)
        {
            cometDiveWindowActive = false;
            cardActionExecutor?.SetManualFlag(ConflictFlags.PlayerVelocity, false);
        }
        if (diveTrail != null) diveTrail.emitting = false;

        // A dive that ends without landing (knockback, death, fall-respawn) fades the comet out.
        // LandCometDive hands the VFX off first, so this can't cancel the landing burst.
        if (cometVfx != null) { cometVfx.Cancel(); cometVfx = null; }
    }

    private void LandCometDive()
    {
        // The blast is centred on the player's root (the feet). CometDiveVFX telegraphs and draws
        // its rings around this same point — keep the two in sync if either ever moves.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, cometRadius, ~0);
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && damaged.Add(target))
            {
                float diveDamage = RelicManager.instance != null
                    ? RelicManager.instance.ModifyPlayerDamage(cometDamage, target as EnemyHealth)
                    : cometDamage;
                target.TakeDamage(diveDamage);
            }
        }

        // Hand off before EndCometDive so its Cancel() sees nothing to cancel. Camera shake and
        // hit-stop live inside the burst.
        if (cometVfx != null) { cometVfx.Land(transform.position); cometVfx = null; }
        else CometDiveVFX.PlayImpact(transform.position, cometRadius);

        // Consume the fall so the normal-landing detector doesn't ALSO fire Meteor Greaves —
        // the dive is its own landing burst.
        trackingFall = false;

        EndCometDive();
        ChangeState(PlayerState.Idle);
    }

    public void FlashCardPlay()
    {
        StartCoroutine(CardPlayFlashRoutine());
    }

    // Called by the walk/run animation's footstep event, relayed from PlayerAnimEventSink on the
    // Animator child. Clips/volume live here on the main player object.
    // ⚠️ MIGRATED TO THE SOUND BANK (2026-08-20) — this is the worked example for the other call
    // sites. Everything this method used to do by hand (pick a clip, jitter the pitch, own an
    // AudioSource) is now data on the "Player.Footstep" event, and the bank does it better: a
    // shuffle bag instead of Random.Range, so the same step can never play twice in a row.
    //
    // The serialized `footstepClips` / `footstepVolume` / `footstepPitchRange` fields are kept as a
    // FALLBACK, not as the live path — if the bank or the event is missing, footsteps still work.
    // A silent player would be a bad way to find out an asset failed to load.
    public void PlayFootstep()
    {
        if (Sfx.Bank != null && Sfx.Bank.Find("Player.Footstep") != null)
        {
            Sfx.Play("Player.Footstep");
            return;
        }

        // ---- fallback: the pre-bank behaviour ----
        if (footstepClips == null || footstepClips.Length == 0) return;
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null) return;
        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 0f;
        }
        footstepSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
        SfxManager.PlayOn(footstepSource, clip, footstepVolume);
    }

    private IEnumerator CardPlayFlashRoutine()
    {
        SpriteRenderer[] renderers = visualModel != null
            ? visualModel.GetComponentsInChildren<SpriteRenderer>()
            : GetComponentsInChildren<SpriteRenderer>();

        Color flashColor = new Color(1f, 0.85f, 0.2f); // gold
        Color[] original = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            original[i] = renderers[i].color;
            renderers[i].color = flashColor;
        }

        yield return new WaitForSecondsRealtime(0.08f);

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = original[i];
    }

    internal void UseAdrenaline(float value)
    {
        SfxManager.PlayOn(audioSource, adrenalineSound);

        float healthPercentage = playerHealth.HealthPercent;

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.5f);

        // Energy aura for the whole buff (both branches last adrenalineDuration). Parent to the
        // player root (identity rotation) so sparks rise in world space; it self-destroys.
        if (adrenalineAuraEffect != null)
        {
            GameObject aura = Instantiate(adrenalineAuraEffect, transform);
            aura.transform.localPosition = Vector3.zero;
            AdrenalineAuraVFX auraVfx = aura.GetComponent<AdrenalineAuraVFX>();
            if (auraVfx != null) auraVfx.duration = adrenalineDuration;
        }

        if (healthPercentage > 0.5f)
        {
            StartCoroutine(AdrenalineSlowMoRoutine());
        }
        else
        {
            Heal(value);
            StartCoroutine(AdrenalineSpeedBoostRoutine());
        }
    }

    private IEnumerator AdrenalineSlowMoRoutine()
    {
        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, true);
        Time.timeScale = slowMotionFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(adrenalineDuration);

        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator AdrenalineSpeedBoostRoutine()
    {
        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, true);
        isAdrenalineActive = true;

        // GÜNCELLENDİ: SkinnedMesh rengi şimdilik değiştirilmiyor
        // if (spriteRenderer != null) spriteRenderer.color = Color.red;

        float originalSpeed = moveSpeed;
        moveSpeed *= speedBoostMultiplier;

        yield return new WaitForSeconds(adrenalineDuration);

        cardActionExecutor?.SetManualFlag(ConflictFlags.TimeScale | ConflictFlags.MoveSpeed, false);
        moveSpeed = originalSpeed;

        // if (spriteRenderer != null) spriteRenderer.color = Color.white;
        isAdrenalineActive = false;
    }

    public void LaunchFromCannon(Vector2 forceVector)
    {
        transform.SetParent(null);
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        // GÜNCELLENDİ: Görsel modeli geri aç
        if (visualModel != null) visualModel.SetActive(true);

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.AddForce(forceVector, ForceMode2D.Impulse);
        ChangeState(PlayerState.Jumping);
    }

    internal void PerformStagger()
    {
        // The flail: a free scrap of height plus a shove at anything standing on top of you, so
        // Stagger also unsticks a player who has jumped themselves into a corner.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        float jumpDir = isGravityReversed ? -1f : 1f;
        rb.AddForce(Vector2.up * staggerJumpForce * jumpDir, ForceMode2D.Impulse);

        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, staggerRadius, ~0);
        foreach (Collider2D enemy in enemies)
        {
            IDamageable target = enemy.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(staggerDamage);
            }
        }

        if (staggerEffect != null) Instantiate(staggerEffect, transform.position, Quaternion.identity);

        // The trade itself is a resource change, so the umbrella hub rule covers it: in the sandbox
        // the flail happens, nothing is bought and nothing is paid — including the escalation, which
        // is permanent run state and so must not advance there either.
        if (LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub()) return;

        float cost = NextStaggerCost;
        staggerCount++;

        // Pay out BEFORE charging: PayHealthCost can kill, and a lethal Stagger that silently
        // skipped its own payout would make the last one in a run behave differently from the rest.
        AddShift(staggerShiftGain);

        // Debt Collector: the bill goes on the tab instead of into your ribs — 20 gold per point of
        // health it would have cost. It converts the run's death clock into an economy problem, so
        // being rich and reckless and being broke and careful are both real ways to play.
        //
        // ⚠️ IT IS NOT A GET-OUT. Short of gold, you pay what you cannot cover in health, so the
        // clock still runs — it just runs on a resource you can go and earn. Silently skipping the
        // cost when broke would delete the only pressure in the run.
        bool onTheTab = RelicManager.instance != null && RelicManager.instance.HasRelic("DebtCollector");
        if (onTheTab)
        {
            int owed = Mathf.RoundToInt(cost * DebtCollectorGoldPerHealth);
            int paid = Mathf.Min(owed, currentGold);
            currentGold -= paid;
            OnGoldChanged?.Invoke(currentGold);

            float shortfallHealth = (owed - paid) / DebtCollectorGoldPerHealth;
            if (shortfallHealth > 0f) playerHealth.PayHealthCost(shortfallHealth);

            Debug.Log($"STAGGER #{staggerCount}: billed {paid} gold" +
                      (shortfallHealth > 0f ? $" and {shortfallHealth:0.#} HP (short)" : "") + ".");
        }
        else
        {
            playerHealth.PayHealthCost(cost);
            Debug.Log($"STAGGER #{staggerCount}: +{staggerShiftGain} Shift for {cost} HP. Next one costs {NextStaggerCost}.");
        }
    }

    private void CheckInteraction()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactableLayer);

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = hit.GetComponent<IInteractable>();

            if (interactable != null)
            {
                interactable.Interact();
                return;
            }
        }
    }

    // --- Floor is Lava card (ReverseGravity) ---

    internal void StartGravityReversal()
    {
        // Stop any existing effect so re-plays refresh the timer instead of stacking.
        // Flags are cleared BEFORE StopCoroutine: stopping skips the old routine's
        // tail (its normal clear point), and the new routine re-sets them
        // synchronously inside StartCoroutine below — so there is never a window
        // with flags set but no live routine, and the clear can't stomp the new set.
        if (gravityReversalCoroutine != null)
        {
            cardActionExecutor?.SetManualFlag(ConflictFlags.GravityScale | ConflictFlags.VisualTransform, false);
            StopCoroutine(gravityReversalCoroutine);
            DestroyGravityAura();   // stopping the routine skips its normal cleanup; clear the aura too
        }
        gravityReversalCoroutine = StartCoroutine(GravityReversalRoutine());
    }

    private IEnumerator GravityReversalRoutine()
    {
        // Manual-flag pattern (same as Adrenaline): flags live exactly as long as this
        // routine. This line runs synchronously inside StartCoroutine, so on a replay
        // the clear in StartGravityReversal and this re-set happen in one call stack.
        cardActionExecutor?.SetManualFlag(ConflictFlags.GravityScale | ConflictFlags.VisualTransform, true);

        bool wasAlreadyReversed = isGravityReversed;

        if (!wasAlreadyReversed)
        {
            // First activation: flip gravity, rotate visual upside-down
            isGravityReversed = true;
            ApplyVisualFacing();
            originalGravityScale = rb.gravityScale;
            rb.gravityScale = -originalGravityScale;

            // Anti-gravity aura: parent to the player root (identity rotation) so motes
            // rise in world space rather than inheriting the visual's 180 deg flip.
            if (gravityAuraEffect != null)
            {
                gravityAuraInstance = Instantiate(gravityAuraEffect, transform);
                gravityAuraInstance.transform.localPosition = Vector3.zero;
            }

            yield return StartCoroutine(LerpVisualTransform(0f, 180f, originalVisualLocalPos.y, originalVisualLocalPos.y + visualFlipYOffset, 0.15f));
            // Wait until 0.5s before the 5s mark (5.0 - 0.5 - 0.15 initial rotation = 4.35s)
            yield return new WaitForSeconds(4.35f);
        }
        else
        {
            // Re-play while already reversed: gravity and visual already set, just restart timer.
            // UNREACHABLE since Block enforcement (CardActionExecutor.TryExecute): a
            // ReverseGravity play while the effect is active is refused upstream because its
            // GravityScale|VisualTransform flags overlap activeFlags. Kept deliberately in
            // case the policy later changes to allow same-card timer refresh.
            yield return new WaitForSeconds(4.5f);
        }

        // Warning at t=4.5s: sound + visual strobe
        SfxManager.PlayOn(audioSource, warningSoundClip, soundVolume);
        yield return StartCoroutine(WarningFlashRoutine());

        // t=5.0s: gravity snaps back instantly, visual lerps back
        rb.gravityScale = originalGravityScale;
        isGravityReversed = false;
        ApplyVisualFacing();
        yield return StartCoroutine(LerpVisualTransform(180f, 0f, originalVisualLocalPos.y + visualFlipYOffset, originalVisualLocalPos.y, 0.15f));

        DestroyGravityAura();

        // Cleared only after the visual lerp-back so VisualTransform stays honest
        // for the full effect, mirroring the set at the top of this routine.
        cardActionExecutor?.SetManualFlag(ConflictFlags.GravityScale | ConflictFlags.VisualTransform, false);
        gravityReversalCoroutine = null;
    }

    private void DestroyGravityAura()
    {
        if (gravityAuraInstance != null)
        {
            Destroy(gravityAuraInstance);
            gravityAuraInstance = null;
        }
    }

    private IEnumerator LerpVisualTransform(float fromZ, float toZ, float fromY, float toY, float duration)
    {
        if (visualModel == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            visualRotationZ = Mathf.LerpAngle(fromZ, toZ, t);
            float newY = Mathf.Lerp(fromY, toY, t);
            visualModel.transform.localRotation = Quaternion.Euler(0f, 0f, visualRotationZ);
            visualModel.transform.localPosition = new Vector3(originalVisualLocalPos.x, newY, originalVisualLocalPos.z);
            yield return null;
        }
        // Snap to exact targets to avoid float drift
        visualRotationZ = toZ;
        visualModel.transform.localRotation = Quaternion.Euler(0f, 0f, visualRotationZ);
        visualModel.transform.localPosition = new Vector3(originalVisualLocalPos.x, toY, originalVisualLocalPos.z);
    }

    private IEnumerator WarningFlashRoutine()
    {
        // The gravity-reversal warning must read on the WHOLE character. The Mage M rig is
        // 16 SkinnedMeshRenderers (body + outfit) plus 1 SpriteRenderer (the staff). A prior
        // version flashed only SpriteRenderers, so it tinted the staff alone and the body
        // never reacted — the warning was effectively invisible. A red tint can't fix it
        // either: the Cainos "Alpha Cut" shader on most outfit parts exposes no color
        // property. But EVERY Cainos rig shader shares "_Alpha" (the same handle Phase
        // strobes), so we blink the whole body's alpha — a blink reads as "effect expiring"
        // — and still red-tint the staff, which does support color.
        SkinnedMeshRenderer[] bodyParts = visualModel != null
            ? visualModel.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            : new SkinnedMeshRenderer[0];
        SpriteRenderer[] sprites = visualModel != null
            ? visualModel.GetComponentsInChildren<SpriteRenderer>(true)
            : new SpriteRenderer[0];
        Color[] originals = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            originals[i] = sprites[i].color;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color warnColor = new Color(1f, 0.3f, 0.3f);   // red danger tint (staff only)
        const float dimAlpha = 0.2f;                   // blinked-down body alpha

        // 3 rapid on/off cycles ≈ 0.5s total
        for (int c = 0; c < 3; c++)
        {
            SetBodyAlpha(bodyParts, block, dimAlpha);
            SetSpriteColors(sprites, warnColor, null);
            yield return new WaitForSeconds(0.083f);
            SetBodyAlpha(bodyParts, block, 1f);
            SetSpriteColors(sprites, default, originals);
            yield return new WaitForSeconds(0.083f);
        }

        // Guarantee restoration regardless of where the loop ended.
        SetBodyAlpha(bodyParts, block, 1f);
        SetSpriteColors(sprites, default, originals);
    }

    // Strobe the "_Alpha" property every Cainos rig shader exposes. Mirrors
    // PhaseVisualRoutine — MaterialPropertyBlocks apply cleanly to these SkinnedMeshRenderers,
    // and _Alpha == 1 is the confirmed "normal" value (Phase restores to 1 the same way).
    private void SetBodyAlpha(SkinnedMeshRenderer[] parts, MaterialPropertyBlock block, float alpha)
    {
        block.SetFloat("_Alpha", alpha);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            parts[i].SetPropertyBlock(block);
        }
    }

    // Tint all sprites to a single color, or restore each to its captured original
    // when 'restore' is supplied.
    private void SetSpriteColors(SpriteRenderer[] sprites, Color flat, Color[] restore)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            sprites[i].color = restore != null ? restore[i] : flat;
        }
    }
}