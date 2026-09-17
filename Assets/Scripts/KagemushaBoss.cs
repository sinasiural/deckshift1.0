using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kagemusha — the Samurai's body double. A floor boss, and the Samurai character's mirror finale.
/// Design doc: BossDesign_Samurai.md. Written standalone (designer 2026-09-17: "a new and clean
/// boss script… they are not really correlated" with the Ninja).
///
/// THE ONE IDEA: he makes hollow copies of himself. Run into a copy before it swings and it bursts,
/// which hurts HIM and drops a Shift crystal. Everything else is arranged around that (§4).
///
/// ⚠️ HE NEVER TELEPORTS. Teleport-to-marker is the Ninja's identity. When this boss is suddenly
/// somewhere else it is because he and a double CHANGED PLACES, in view — the body-double trick,
/// performed on you. That is the only "blink" in this file and it is a swap.
///
/// Four attacks (§5): DRAW (a telegraphed lane cut), OVERHEAD (a leap onto you with a floor
/// shockwave — the Shift tax), SPLIT (doubles fan out and all cut together — the Shift income), and
/// SHEATHE (a counter-stance: hurt him while the blade is low and he swaps with a double and cuts).
///
/// He animates on the Cainos *Customizable Pixel Character* rig, so the animator handles are the
/// player's: `AttackAction` (INT), `IsAttacking`, `IsCrouching`, `IsDashing`, `MoveBlendX`.
///
/// Generic lessons kept from the two bosses before him, each paid for with a real bug:
///   - gravity is captured ONCE in Awake and every restore uses that value (a move that begins
///     while gravity is already 0 would otherwise "restore" it to 0 and he floats forever);
///   - every latch (gravity, player collision, animator bools) is released in a `finally` AND in
///     OnBossDied AND in OnDestroy;
///   - positions derive from transform + collider offset, never `collider.bounds` (autoSyncTransforms
///     is off, so bounds lag a physics step — fatal for anything that moves in a single frame);
///   - a stuck watchdog with NO mid-move exemption flag (a flag is a latch);
///   - the exit is sealed on Start and unsealed from OnDestroy as well as death — fail passable.
/// </summary>
/// <summary>
/// A boss that is some character's mirror. LevelManager calls SetFinale(true) on it when its room
/// is spawned as the played character's OWN finale, so the same prefab can be a lesser mid-map cut
/// of itself for everyone else.
/// </summary>
public interface IMirrorBoss
{
    void SetFinale(bool isFinale);
}

[RequireComponent(typeof(EnemyHealth))]
public class KagemushaBoss : MonoBehaviour, IBossFight, IMirrorBoss
{
    public void SetFinale(bool isFinale) => finale = isFinale;

    // Read out of AC Character's own transitions: 1=Swipe, 2=Stab, 11=Point, 12=Summon, 13=Throw,
    // 14=Cast. Swipe plays on BOTH the Arm and Body layers and reads as a committed slash.
    public const int SWIPE_ACTION = 1;

    [Header("Fight Start")]
    [Tooltip("Kneel with the doubles until a BossAwakenTrigger calls StartFight().")]
    public bool startDormant = true;

    [Header("Identity")]
    public string bossName = "Kagemusha";
    [Tooltip("Big screen bar for this boss. Empty = no boss bar.")]
    public GameObject bossHealthBarPrefab;

    [Header("Rig")]
    [Tooltip("The visual child that gets flipped for facing. Empty = the first child.")]
    public Transform visualModel;
    [Tooltip("PF Weapon - Katana. Equipped at runtime through the pack's AddWeapon.")]
    public GameObject weaponPrefab;

    [Header("The doubles")]
    [Tooltip("The stripped copies of his rig that ship as children of this prefab. Two is the finale count.")]
    public ShadowDouble[] doubles;
    [Tooltip("How see-through a double is. ⚠️ The rig cannot be tinted, only faded — see ShadowDouble.")]
    [Range(0f, 1f)] public float doubleAlpha = 0.5f;
    [Tooltip("TRUE when this arena is the Samurai's own finale. Two doubles instead of one, and the " +
             "40% twist. LevelManager sets this when the room is spawned as the finale.")]
    public bool finale = false;
    public int doublesMidMap = 1;
    public int doublesFinale = 2;
    [Tooltip("Finale only: below this health fraction the doubles turn SOLID and gain his contact " +
             "mark, so nobody in the room can tell which is real. Costs one number.")]
    [Range(0f, 1f)] public float twistAtFraction = 0.4f;
    [Tooltip("The warm mark under the real one's feet. Doubles carry none — a shadow casts no shadow.")]
    public Color markColour = new Color(0.980f, 0.706f, 0.365f, 1f);   // Salvage.Torch

    [Header("Between attacks (TUNE BY EYE)")]
    public float betweenAttacks = 1.0f;
    public float walkSpeed = 4.5f;
    public float preferredRange = 6f;
    public float repositionJitter = 2.5f;

    [Header("Draw — the lane cut (TUNE BY EYE)")]
    [Tooltip("Beyond this horizontal distance he will not Draw; he closes with Overhead instead.")]
    public float drawRange = 13f;
    [Tooltip("How long he stands still with his hand on the hilt. The lane is drawn for this whole time.")]
    public float drawWindup = 0.7f;
    [Tooltip("A beat of complete stillness at full load, before he goes.")]
    public float drawHold = 0.12f;
    public float drawSpeed = 28f;
    [Tooltip("How far PAST the player he commits to. Small — he ends beside you, not across the room.")]
    public float drawOvershoot = 3f;
    public float drawMaxLength = 12f;
    public float drawDamage = 16f;
    public float drawKnockback = 7f;
    [Tooltip("Height of the hit box. ~2 covers a standing player; a jump clears it.")]
    public float drawHeight = 2f;
    [Tooltip("Recovery after a clean Draw — the player's damage window.")]
    public float drawRecovery = 0.6f;
    [Tooltip("Recovery after ending against a wall. Longer: overshooting is the mistake the player baits.")]
    public float drawWallRecovery = 1.1f;
    // ⚠️ NOT the Ninja's red, and no premonition ghost. His telegraph is the same lane structure in
    // TORCH GOLD — lit steel — and his travel leaves the line the edge took (CutStreak) rather than
    // afterimages of his body. Body ghosts are the Ninja's vocabulary; the samurai's is the cut.
    public Color laneColor = new Color(0.980f, 0.706f, 0.365f, 1f);
    [Tooltip("The streak he and his doubles leave when they cut. Cold steel against the player's warm gold.")]
    public Color streakColour = new Color(0.78f, 0.86f, 1f, 1f);

    [Header("Overhead — the leap and the shockwave (TUNE BY EYE)")]
    public float overheadCooldown = 7f;
    [Tooltip("Seconds in the air. He lands exactly where you were standing when he jumped.")]
    public float overheadFlightTime = 0.9f;
    [Tooltip("Gravity multiplier for the leap only. At base gravity a 0.9s flight peaks under a " +
             "unit — a hop, not a leap. Heavier gravity buys height for the same flight time, and " +
             "the arc is solved against it so he still lands on the mark.")]
    public float overheadGravityMul = 2.5f;
    public float overheadDamage = 18f;
    public float overheadRadius = 1.6f;
    public float overheadKnockback = 8f;
    [Tooltip("The wave runs along the floor BOTH ways from the landing. Jump it — that costs Shift, " +
             "which is the point: the Split pays it back.")]
    public float shockwaveSpeed = 14f;
    public float shockwaveLength = 6f;
    public float shockwaveDamage = 12f;
    [Tooltip("Low on purpose. A standing player is hit; a jumping one clears it.")]
    public float shockwaveHeight = 1.1f;
    public float overheadRecovery = 0.8f;
    public Color shockwaveColour = new Color(0.95f, 0.85f, 0.65f, 1f);

    [Header("Split — the doubles (TUNE BY EYE)")]
    [Tooltip("⚠️ SECONDS BETWEEN GUARANTEED SPLITS — the fight's economy. The doubles are how a " +
             "cardless player hurts him and how everyone earns Shift. It has first refusal in the " +
             "loop, on a timer, precisely so a range check can never starve it (the Ninja's volley " +
             "fired ZERO times in 2000 attacks for exactly that reason).")]
    public float splitInterval = 9f;
    [Tooltip("How long all of them stand armed before the unison cut. THIS IS THE WINDOW to run into " +
             "a double and break it.")]
    public float splitWindup = 0.9f;
    [Tooltip("How far to each side the doubles peel off.")]
    public float splitSpread = 3.5f;
    [Tooltip("How long a double stands around after the unison cut before fading.")]
    public float doubleLifetime = 8f;
    [Tooltip("Damage HE takes when a double is broken. ⚠️ 12 is 12 — a body-check worth a little " +
             "less than a Fireball, never a secret boss-only multiplier.")]
    public float shatterDamage = 12f;
    [Tooltip("Assign Prefabs/ShiftCrystal. One per broken double is the single most sensitive number " +
             "in the encounter — keep it ONE tunable.")]
    public GameObject shiftCrystalPrefab;
    public int crystalsPerShatter = 1;

    [Header("Sheathe — the counter-stance (TUNE BY EYE)")]
    public float sheatheCooldown = 8f;
    [Tooltip("How long the blade stays low. ⚠️ Keep it longer than a Fireball's flight across the " +
             "arena — a shot fired BEFORE the stance that lands DURING it triggers the counter, and " +
             "that is only fair because the stance is long and loud.")]
    public float sheatheDuration = 1.6f;
    public float counterDamage = 18f;
    public float counterRadius = 2.4f;
    public float counterKnockback = 9f;
    [Tooltip("Recovery after the counter — the punish window for a player who baited it on purpose.")]
    public float counterRecovery = 0.7f;

    [Header("Death")]
    public bool playDeathEffect = true;
    public AudioClip deathSound;
    [Range(0f, 2f)] public float deathVolume = 1.4f;
    public GameObject deathGoldPrefab;
    public GameObject deathShiftCrystalPrefab;
    public int deathGoldCount = 14;
    public int deathCrystalCount = 5;
    [Tooltip("He comes apart into steel-grey — the doubles' colour, not the Ninja's blue or the " +
             "Moss Knight's green.")]
    public Color deathBurstColor = new Color(0.80f, 0.84f, 0.90f);
    public bool offerBossRelic = true;
    [Range(1, 4)] public int bossRelicChoices = 2;
    public float rewardDelay = 2.6f;

    // ⚠️ EVERY SLOT HERE IS AN OVERRIDE. Every sound plays procedurally whether or not a clip is
    // dragged in, because an empty AudioClip field is a silent no-op and that is where this
    // project's silence has always lived. The procedural defaults are BORROWED from other families
    // for now — a SAMURAI family (the ring axis, doc §8) is the follow-up.
    [Header("Audio (leave empty — procedural by default)")]
    public AudioClip drawSound;
    public AudioClip sheatheSound;
    public AudioClip counterSound;
    public AudioClip splitSound;
    public AudioClip shatterSound;
    public AudioClip landSound;
    [Range(0f, 2f)] public float sfxVolume = 1f;

    private AudioClip DrawClip    => drawSound    != null ? drawSound    : ProcSfx.FreefallBlade;
    private AudioClip SheatheClip => sheatheSound != null ? sheatheSound : ProcSfx.GateSeat;
    private AudioClip CounterClip => counterSound != null ? counterSound : ProcSfx.KatanaPlant;
    private AudioClip SplitClip   => splitSound   != null ? splitSound   : ProcSfx.NinjaBlink;
    private AudioClip ShatterClip => shatterSound != null ? shatterSound : ProcSfx.WallBreak;
    private AudioClip LandClip    => landSound    != null ? landSound    : ProcSfx.MeteorImpact;

    // ---- runtime ---------------------------------------------------------------------------------
    private EnemyHealth health;
    private Animator animator;
    private AudioSource sfx;
    private Transform player;
    private Collider2D body;
    private Rigidbody2D rb;
    private ExitDoor exit;
    private SpriteRenderer mark;

    private bool fightStarted;
    private bool facingRight = true;
    private float visualScaleX = 1f;
    private float baseGravityScale = 1f;
    private bool sheathed, counterTriggered;
    private bool twisted;
    private float nextSplit, nextOverhead, nextSheathe, doublesExpire, nextStuckCheck;
    private readonly List<ShadowDouble> live = new List<ShadowDouble>();

    private void RestoreGravity() { if (rb != null) rb.gravityScale = baseGravityScale; }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) baseGravityScale = rb.gravityScale;

        if (visualModel == null && transform.childCount > 0) visualModel = transform.GetChild(0);
        if (visualModel != null) visualScaleX = Mathf.Abs(visualModel.localScale.x);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;      // 2D: audible across the whole arena, slider can exceed 1

        EquipWeapon();

        if (doubles != null)
            foreach (var d in doubles)
                if (d != null) { d.Bind(this); d.Vanish(); }

        BuildMark();
    }

    // ⚠️ Through the pack's own AddWeapon, never hand-parented — it syncs the weapon to the rig
    // bone and pushes sorting/alpha onto the new renderers. Wrapped so a cosmetic failure inside
    // Awake can never disable the whole boss (Unity disables a MonoBehaviour whose Awake throws).
    private void EquipWeapon()
    {
        if (weaponPrefab == null || visualModel == null) return;
        var pixel = visualModel.GetComponent<Cainos.CustomizablePixelCharacter.PixelCharacter>();
        if (pixel == null) return;
        try { pixel.AddWeapon(weaponPrefab, true); }
        catch (System.Exception e) { Debug.LogWarning($"[KagemushaBoss] weapon setup failed: {e.Message}"); }
    }

    private void BuildMark()
    {
        var go = new GameObject("Mark");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.08f, 0.1f);
        go.transform.localScale = new Vector3(1.9f, 0.55f, 1f);
        mark = go.AddComponent<SpriteRenderer>();
        mark.sprite = FlatUI.SoftGlow();
        mark.sortingOrder = -1;
        mark.color = new Color(markColour.r, markColour.g, markColour.b, 0.55f);
        mark.enabled = false;    // lit when the fight starts — three identical kneeling figures first
    }

    private void Start()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
            player = GameManager.instance.player.transform;

        if (health != null)
        {
            health.OnDamaged += OnDamaged;
            health.OnDied += OnBossDied;
        }

        // Sealed from the moment the room exists — a door that seals in front of you is worse than
        // one that was always shut.
        SealExit();

        if (startDormant) Kneel();
        else StartFight();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= OnDamaged;
            health.OnDied -= OnBossDied;
        }
        ClearAnimatorState();
        RestoreGravity();
        SetPlayerCollision(true);
        // ⚠️ FAIL TOWARD PASSABLE. However he leaves the world, the exit must not stay sealed.
        if (exit != null) exit.SetLocked(false);
    }

    // ---- the opening beat --------------------------------------------------------------------------
    // Three of him kneel at the far end, identical and solid. Cross the line and two dissolve; the one
    // left stands and draws. He shows you the trick before he uses it (§10).
    private void Kneel()
    {
        if (animator != null) animator.SetBool("IsCrouching", true);
        FaceDirection(-1f);   // the spawn is to his left in the Hall

        int n = Mathf.Min(2, doubles != null ? doubles.Length : 0);
        for (int i = 0; i < n; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Vector3 pos = transform.position + new Vector3(side * 1.6f, 0f, 0f);
            doubles[i].Appear(pos, facingRight, 1f);          // SOLID — indistinguishable
            doubles[i].SetState(ShadowDouble.State.Kneeling);
            doubles[i].Pose(true, false, 0, false);
        }
    }

    public void StartFight()
    {
        if (fightStarted) return;
        fightStarted = true;

        if (MusicManager.instance != null) MusicManager.instance.PlayBossMusic();

        if (bossHealthBarPrefab != null && health != null)
        {
            GameObject barGO = Instantiate(bossHealthBarPrefab);
            BossHealthBar bar = barGO.GetComponent<BossHealthBar>();
            if (bar != null) bar.Initialize(health, bossName);
        }

        StartCoroutine(AwakenThenFight());
    }

    private IEnumerator AwakenThenFight()
    {
        // The two fakes go first, then he rises. A beat between so it reads as a sequence.
        SfxManager.PlayOn(sfx, SplitClip, sfxVolume);
        if (doubles != null)
            foreach (var d in doubles)
                if (d != null && d.Current == ShadowDouble.State.Kneeling) d.StartCoroutine(d.Dissolve(0.6f));
        yield return new WaitForSeconds(0.7f);

        if (animator != null) animator.SetBool("IsCrouching", false);
        if (mark != null) mark.enabled = true;
        FaceTowardPlayer();
        SfxManager.PlayOn(sfx, DrawClip, sfxVolume);
        yield return new WaitForSeconds(0.6f);

        nextSplit = Time.time + 3f;          // first split comes early: teach the loop
        nextOverhead = Time.time + 4f;
        nextSheathe = Time.time + 6f;
        StartCoroutine(FightLoop());
    }

    // ---- the loop ----------------------------------------------------------------------------------
    private IEnumerator FightLoop()
    {
        while (health != null && health.CurrentHealth > 0f)
        {
            // Resolved lazily: a reference captured once goes stale on respawn.
            if (player == null && GameManager.instance != null && GameManager.instance.player != null)
                player = GameManager.instance.player.transform;
            if (player == null) { yield return null; continue; }

            float dx = Mathf.Abs(player.position.x - transform.position.x);
            float dy = player.position.y - transform.position.y;
            bool level = Mathf.Abs(dy) < 2.5f;
            bool playerAbove = dy > 2.5f;

            if (Time.time >= nextSplit && IsGrounded())
            {
                nextSplit = Time.time + splitInterval;
                yield return StartCoroutine(SplitRoutine());
            }
            else if (Time.time >= nextSheathe && dx < 10f && level && IsGrounded())
            {
                nextSheathe = Time.time + sheatheCooldown;
                yield return StartCoroutine(SheatheRoutine());
            }
            else if (Time.time >= nextOverhead && IsGrounded() && (playerAbove || dx > drawRange))
            {
                nextOverhead = Time.time + overheadCooldown;
                yield return StartCoroutine(OverheadRoutine());
            }
            else if (level && dx <= drawRange && IsGrounded())
            {
                yield return StartCoroutine(DrawRoutine());
            }
            else if (IsGrounded() && Time.time >= nextOverhead)
            {
                nextOverhead = Time.time + overheadCooldown;
                yield return StartCoroutine(OverheadRoutine());
            }

            yield return StartCoroutine(RepositionRoutine(betweenAttacks));
        }
    }

    // He walks — the pack's run blend — to a jittered preferred distance. Never a statue.
    private IEnumerator RepositionRoutine(float duration)
    {
        if (player == null || rb == null) { yield return new WaitForSeconds(duration); yield break; }

        float side = Mathf.Sign(transform.position.x - player.position.x);
        if (Mathf.Approximately(side, 0f)) side = 1f;
        float want = player.position.x + side * (preferredRange + Random.Range(-repositionJitter, repositionJitter));

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (player == null) break;
            float ddx = want - transform.position.x;
            float step = Mathf.Sign(ddx);
            bool moving = Mathf.Abs(ddx) > 0.4f && !WallAhead(step) && IsGrounded();
            if (moving) { rb.linearVelocity = new Vector2(step * walkSpeed, rb.linearVelocity.y); FaceDirection(step); }
            else rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (animator != null)
            {
                animator.SetBool("IsMoving", moving);
                animator.SetFloat("MoveBlendX", moving ? 1f : 0f);   // walk pose — a samurai does not sprint
                animator.SetFloat("MoveSpeedMul", 1f);
            }
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) { animator.SetBool("IsMoving", false); animator.SetFloat("MoveBlendX", 0f); }
        FaceTowardPlayer();
    }

    // ---- DRAW ---------------------------------------------------------------------------------------
    private IEnumerator DrawRoutine()
    {
        FaceTowardPlayer();
        float dir = facingRight ? 1f : -1f;
        Vector2 terminus = ChestPoint + new Vector2(dir * MeasureLane(dir), 0f);

        LaneTelegraph tel = LaneTelegraph.Build(ChestPoint, terminus, drawHeight, LaneTelegraph.Style.Default(laneColor));
        yield return StartCoroutine(DrawWindup(terminus, tel, drawWindup));
        yield return StartCoroutine(DrawTravel(dir, terminus));
    }

    // The stillness IS the telegraph, alongside the lane. Gravity is off for the whole move so the
    // line he was shown is the line he travels — the telegraph is the contract.
    private IEnumerator DrawWindup(Vector2 terminus, LaneTelegraph tel, float windup)
    {
        if (rb != null) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; }
        if (animator != null) animator.SetBool("IsCrouching", true);

        float t = 0f, w = Mathf.Max(0.01f, windup);
        while (t < w)
        {
            t += Time.deltaTime;
            if (tel != null) { tel.Place(ChestPoint, terminus, drawHeight); tel.SetIntensity(Mathf.Clamp01(t / w)); }
            yield return null;
        }
        float hold = 0f;
        while (hold < drawHold)
        {
            hold += Time.deltaTime;
            if (tel != null) { tel.Place(ChestPoint, terminus, drawHeight); tel.SetIntensity(1f); }
            yield return null;
        }
        if (tel != null) tel.Clear();
    }

    private const float TRAVEL_TIMEOUT = 1.4f;
    private const float TRAVEL_STALL = 0.10f;

    private IEnumerator DrawTravel(float dir, Vector2 terminus)
    {
        if (animator != null)
        {
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsDashing", true);
            animator.SetInteger("AttackAction", SWIPE_ACTION);
            animator.SetBool("IsAttacking", true);
        }
        SfxManager.PlayOn(sfx, DrawClip, sfxVolume);
        Puff(ChestPoint - new Vector2(dir * 0.3f, 0.9f), 7, 1f, -dir);

        SetPlayerCollision(false);
        bool hitWall = false;
        // The line the edge takes, extended as he travels and left hanging when he stops. His
        // vocabulary, not the Ninja's afterimages.
        CutStreak streak = CutStreak.Begin(ChestPoint, streakColour, 0.12f);
        try
        {
            bool struck = false;
            float elapsed = 0f, stalled = 0f, lastX = transform.position.x;

            // Ends on reaching the drawn terminus, on NO PROGRESS, or on the hard timeout — a single
            // wall ray is not enough (a ledge above or below it pins him and the loop never ends).
            while ((terminus.x - transform.position.x) * dir > 0.05f)
            {
                elapsed += Time.fixedDeltaTime;
                if (elapsed > TRAVEL_TIMEOUT) { hitWall = true; break; }
                float moved = Mathf.Abs(transform.position.x - lastX);
                lastX = transform.position.x;
                stalled = moved < 0.01f ? stalled + Time.fixedDeltaTime : 0f;
                if (stalled > TRAVEL_STALL) { hitWall = true; break; }

                rb.linearVelocity = new Vector2(dir * drawSpeed, 0f);
                streak.SetEnd(ChestPoint);

                if (!struck && EnemyMelee.TryHit(transform, dir, 1.5f, drawDamage, drawKnockback, drawHeight))
                {
                    struck = true;
                    if (player != null) CutMark.Spawn((Vector2)player.position + Vector2.up * 0.9f, streakColour, 1.3f);
                }

                if (WallAhead(dir)) { hitWall = true; break; }
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            SetPlayerCollision(true);
            RestoreGravity();
            if (streak != null) streak.Release(0.4f);
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) { animator.SetBool("IsDashing", false); animator.SetBool("IsAttacking", false); }
        Puff(ChestPoint - new Vector2(0f, 0.9f), hitWall ? 12 : 8, hitWall ? 1.4f : 1f, -dir);
        if (hitWall && CameraShake.instance != null) CameraShake.instance.Shake(0.18f, 0.30f);

        // The recovery is the point: it is where a cardless player does real damage.
        yield return new WaitForSeconds(hitWall ? drawWallRecovery : drawRecovery);
    }

    private float MeasureLane(float dir)
    {
        float want = drawMaxLength;
        if (player != null) want = Mathf.Abs(player.position.x - transform.position.x) + drawOvershoot;
        want = Mathf.Min(want, drawMaxLength);
        var hit = Physics2D.Raycast(ChestPoint, new Vector2(dir, 0f), want, LayerMask.GetMask("Ground"));
        return hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.6f) : want;
    }

    // ---- SPLIT --------------------------------------------------------------------------------------
    // Doubles peel off to either side, all of them stand ARMED for the windup — the window to break
    // one — then everyone cuts together along their own lane.
    private IEnumerator SplitRoutine()
    {
        FaceTowardPlayer();
        float dir = facingRight ? 1f : -1f;
        int n = Mathf.Clamp(finale ? doublesFinale : doublesMidMap, 0, doubles != null ? doubles.Length : 0);

        SfxManager.PlayOn(sfx, SplitClip, sfxVolume);
        live.Clear();
        List<Vector3> spots = SplitSpots(n);
        for (int i = 0; i < n; i++)
        {
            var d = doubles[i];
            if (d == null) continue;
            Vector3 pos = i < spots.Count ? spots[i] : transform.position;
            d.Appear(pos, facingRight, twisted ? 1f : doubleAlpha);
            d.SetState(ShadowDouble.State.Armed);
            d.ShowMark(twisted, markColour);
            live.Add(d);
        }

        // Everyone winds up together. Each figure draws its own lane.
        Vector2 myTerminus = ChestPoint + new Vector2(dir * MeasureLane(dir), 0f);
        var myTel = LaneTelegraph.Build(ChestPoint, myTerminus, drawHeight, LaneTelegraph.Style.Default(laneColor));
        var tels = new List<LaneTelegraph>();
        foreach (var d in live)
        {
            d.Pose(true, false, 0, false);
            Vector2 from = (Vector2)d.transform.position + Vector2.up * 1.05f;
            Vector2 to = from + new Vector2(dir * LaneFrom(from, dir, drawMaxLength), 0f);
            tels.Add(LaneTelegraph.Build(from, to, drawHeight, LaneTelegraph.Style.Default(laneColor)));
        }

        if (rb != null) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; }
        if (animator != null) animator.SetBool("IsCrouching", true);
        float t = 0f;
        while (t < splitWindup + drawHold)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / splitWindup);
            myTel.Place(ChestPoint, myTerminus, drawHeight); myTel.SetIntensity(k);
            for (int i = 0; i < live.Count; i++)
            {
                // A broken double takes its lane with it.
                if (live[i].Current != ShadowDouble.State.Armed) { tels[i].Clear(); continue; }
                Vector2 from = (Vector2)live[i].transform.position + Vector2.up * 1.05f;
                tels[i].Place(from, from + new Vector2(dir * LaneFrom(from, dir, drawMaxLength), 0f), drawHeight);
                tels[i].SetIntensity(k);
            }
            yield return null;
        }
        myTel.Clear();
        foreach (var tl in tels) tl.Clear();

        // The unison cut. Survivors strike on their own coroutines; he strikes on this one.
        foreach (var d in live)
            if (d.Current == ShadowDouble.State.Armed)
                d.StartCoroutine(d.Draw(dir, drawMaxLength, drawSpeed, drawDamage, drawKnockback, drawHeight, streakColour));

        doublesExpire = Time.time + doubleLifetime;
        yield return StartCoroutine(DrawTravel(dir, myTerminus));
    }

    private float LaneFrom(Vector2 from, float dir, float max)
    {
        var hit = Physics2D.Raycast(from, new Vector2(dir, 0f), max, LayerMask.GetMask("Ground"));
        return hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.6f) : max;
    }

    // Where `count` doubles stand: alternating sides at splitSpread, then 2x splitSpread, and so on —
    // skipping any slot that would put a copy inside a wall. ⚠️ Slots are allocated from ONE list,
    // not computed per double: the first version flipped a walled double to the other side and it
    // landed exactly on top of its sibling. A copy stood half inside the arena's rock frame — or two
    // copies in one place — is exactly the "sometimes it looks broken" a split cannot afford.
    private List<Vector3> SplitSpots(int count)
    {
        var spots = new List<Vector3>();
        if (count <= 0) return spots;

        Vector2 chest = ChestPoint;
        float margin = CapsuleSize.x * 0.5f + 0.35f;
        int ground = LayerMask.GetMask("Ground");

        // Clear distance to each side, measured once.
        float[] clear = new float[2];
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f;
            var wall = Physics2D.Raycast(chest, new Vector2(side, 0f), 40f, ground);
            clear[s] = wall.collider != null ? wall.distance - margin : 40f;
        }

        for (int ring = 1; spots.Count < count && ring <= 4; ring++)
            for (int s = 0; s < 2 && spots.Count < count; s++)
            {
                float side = s == 0 ? -1f : 1f;
                float dx = splitSpread * ring;
                if (dx > clear[s]) continue;          // that slot is in the wall — skip it
                Vector2 foot = (Vector2)transform.position + new Vector2(side * dx, 0f);
                var down = Physics2D.Raycast(foot + Vector2.up * 1.0f, Vector2.down, 6f, ground);
                if (down.collider != null) foot.y = down.point.y + 0.02f;
                spots.Add(new Vector3(foot.x, foot.y, PlayPlane.Z));
            }

        // Boxed in on both sides: stand them on him rather than not at all.
        while (spots.Count < count) spots.Add(transform.position);
        return spots;
    }

    /// <summary>Called by a double the player touched while it was ARMED. The shard goes home.</summary>
    public void OnDoubleShattered(ShadowDouble d)
    {
        if (d == null || d.Current != ShadowDouble.State.Armed) return;
        Vector3 at = d.transform.position;
        d.Vanish();

        SfxManager.PlayOn(sfx, ShatterClip, sfxVolume);
        Puff(at + Vector3.up * 0.9f, 10, 1.3f, 1f);
        Puff(at + Vector3.up * 0.9f, 10, 1.3f, -1f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.12f, 0.22f);
        if (HitStop.instance != null) HitStop.instance.Stop(0.05f);

        if (health != null) health.TakeDamage(shatterDamage);

        if (shiftCrystalPrefab != null)
            for (int i = 0; i < crystalsPerShatter; i++)
            {
                GameObject c = Instantiate(shiftCrystalPrefab, at + Vector3.up * 1.1f + (Vector3)(Random.insideUnitCircle * 0.3f), Quaternion.identity);
                if (c.GetComponent<TemporaryObject>() == null) c.AddComponent<TemporaryObject>();
            }
    }

    // ---- SHEATHE ------------------------------------------------------------------------------------
    // Blade low, still, loud. Hurt him now and he ANSWERS: swaps with his nearest double and cuts.
    // Nothing happens if you simply wait — the stance exists so that "play a card now" is sometimes
    // the wrong answer, which is a real decision in a deckbuilder and nothing else creates it.
    private IEnumerator SheatheRoutine()
    {
        FaceTowardPlayer();
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) animator.SetBool("IsCrouching", true);
        SfxManager.PlayOn(sfx, SheatheClip, sfxVolume);

        // A slow pulse on the mark — the only motion on him while he waits.
        sheathed = true; counterTriggered = false;
        float t = 0f;
        while (t < sheatheDuration && !counterTriggered)
        {
            t += Time.deltaTime;
            if (mark != null) mark.color = new Color(markColour.r, markColour.g, markColour.b, 0.35f + 0.35f * Mathf.PingPong(t * 3f, 1f));
            yield return null;
        }
        sheathed = false;
        if (mark != null) mark.color = new Color(markColour.r, markColour.g, markColour.b, 0.55f);
        if (animator != null) animator.SetBool("IsCrouching", false);

        if (!counterTriggered) yield break;

        // THE ANSWER. Swap with the nearest standing double, if any — the body-double trick.
        ShadowDouble nearest = null; float best = float.MaxValue;
        foreach (var d in live)
        {
            if (d == null || d.Current != ShadowDouble.State.Standing) continue;
            float dist = Vector2.Distance(d.transform.position, transform.position);
            if (dist < best) { best = dist; nearest = d; }
        }
        if (nearest != null && Fits(nearest.transform.position))
        {
            Vector3 mine = transform.position, theirs = nearest.transform.position;
            Puff(mine + Vector3.up * 0.9f, 8, 1.1f, 1f);
            Puff(theirs + Vector3.up * 0.9f, 8, 1.1f, -1f);
            transform.position = new Vector3(theirs.x, theirs.y, PlayPlane.Z);
            nearest.transform.position = new Vector3(mine.x, mine.y, PlayPlane.Z);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        FaceTowardPlayer();
        SfxManager.PlayOn(sfx, CounterClip, sfxVolume);
        if (animator != null) { animator.SetInteger("AttackAction", SWIPE_ACTION); animator.SetBool("IsAttacking", true); }
        yield return new WaitForSeconds(0.12f);
        CircleHit(ChestPoint, counterRadius, counterDamage, counterKnockback);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.2f, 0.3f);
        yield return new WaitForSeconds(0.2f);
        if (animator != null) animator.SetBool("IsAttacking", false);

        yield return new WaitForSeconds(counterRecovery);
    }

    // ---- OVERHEAD -----------------------------------------------------------------------------------
    // A ballistic leap onto where you were standing, a vertical cut on landing, and a shockwave that
    // runs along the floor both ways. Ledges beat the wave; the floor does not. The opposite of Draw.
    private IEnumerator OverheadRoutine()
    {
        if (player == null || rb == null) yield break;
        FaceTowardPlayer();

        Vector2 target = player.position;
        Vector2 from = transform.position;
        float T = Mathf.Max(0.25f, overheadFlightTime);
        float leapGravity = baseGravityScale * Mathf.Max(0.1f, overheadGravityMul);
        float g = Physics2D.gravity.y * leapGravity;
        float vx = (target.x - from.x) / T;
        float vy = (target.y - from.y - 0.5f * g * T * T) / T;

        // Heavier for the leap only, and put back in a finally — a boss killed mid-air would
        // otherwise keep 2.5x gravity for whatever came next.
        rb.gravityScale = leapGravity;
        try
        {
            rb.linearVelocity = new Vector2(vx, vy);
            if (animator != null) { animator.SetInteger("AttackAction", SWIPE_ACTION); }
            SfxManager.PlayOn(sfx, DrawClip, sfxVolume * 0.7f);

            float t = 0f;
            yield return new WaitForSeconds(0.12f);
            while (!IsGrounded() && t < 1.8f) { t += Time.deltaTime; yield return null; }
        }
        finally { RestoreGravity(); }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) animator.SetBool("IsAttacking", true);
        SfxManager.PlayOn(sfx, LandClip, sfxVolume);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.28f, 0.35f);
        Puff(ChestPoint - new Vector2(0f, 0.9f), 10, 1.4f, 1f);
        Puff(ChestPoint - new Vector2(0f, 0.9f), 10, 1.4f, -1f);

        CircleHit(ChestPoint, overheadRadius, overheadDamage, overheadKnockback);
        StartCoroutine(Shockwave(1f));
        StartCoroutine(Shockwave(-1f));

        yield return new WaitForSeconds(0.25f);
        if (animator != null) animator.SetBool("IsAttacking", false);
        yield return new WaitForSeconds(overheadRecovery);
    }

    // A travelling box at floor level, drawn as a bright strip. Low enough that a jump clears it.
    private IEnumerator Shockwave(float dir)
    {
        var go = new GameObject("Shockwave");
        go.AddComponent<TemporaryObject>();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FlatUI.Pixel();
        sr.sortingOrder = 5;
        Vector2 native = sr.sprite.bounds.size;
        float w = 0.9f, h = shockwaveHeight;
        go.transform.localScale = new Vector3(w / native.x, h / native.y, 1f);

        float floorY = transform.position.y;
        float x = transform.position.x + dir * 0.8f;
        float travelled = 0f;
        bool struck = false;
        while (travelled < shockwaveLength)
        {
            float step = shockwaveSpeed * Time.fixedDeltaTime;
            if (Physics2D.Raycast(new Vector2(x, floorY + 0.5f), new Vector2(dir, 0f), step + 0.5f, LayerMask.GetMask("Ground")).collider != null) break;
            x += dir * step; travelled += step;
            Vector2 centre = new Vector2(x, floorY + h * 0.5f);
            go.transform.position = new Vector3(centre.x, centre.y, PlayPlane.Z + 0.05f);
            float fade = 1f - travelled / shockwaveLength;
            sr.color = new Color(shockwaveColour.r, shockwaveColour.g, shockwaveColour.b, 0.25f + 0.55f * fade);

            if (!struck)
            {
                var hit = Physics2D.OverlapBox(centre, new Vector2(w, h), 0f, LayerMask.GetMask("Player"));
                var pc = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                if (pc != null)
                {
                    struck = true;
                    pc.TakeDamage(shockwaveDamage);
                    pc.ApplyKnockback(new Vector2(dir * overheadKnockback * 0.7f, overheadKnockback * 0.6f));
                }
            }
            yield return new WaitForFixedUpdate();
        }
        Destroy(go);
    }

    private void CircleHit(Vector2 centre, float radius, float damage, float knockback)
    {
        var hit = Physics2D.OverlapCircle(centre, radius, LayerMask.GetMask("Player"));
        var pc = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
        if (pc == null) return;
        float dirX = Mathf.Sign(pc.transform.position.x - transform.position.x);
        if (Mathf.Approximately(dirX, 0f)) dirX = facingRight ? 1f : -1f;
        pc.TakeDamage(damage);
        pc.ApplyKnockback(new Vector2(dirX * knockback, knockback * 0.6f));
        if (HitStop.instance != null) HitStop.instance.Stop(0.06f);
    }

    // ---- per frame ----------------------------------------------------------------------------------
    private void Update()
    {
        if (!fightStarted) return;
        EnsureNotStuck();

        bool grounded = IsGrounded();
        if (animator != null)
        {
            animator.SetBool("IsGrounded", grounded);
            animator.SetFloat("VelocityY", rb != null ? rb.linearVelocity.y : 0f);
        }

        // Doubles that are just standing there mirror him; striking ones drive themselves.
        if (doubles != null)
            foreach (var d in doubles)
            {
                if (d == null || d.Current == ShadowDouble.State.Hidden) continue;
                if (d.Current == ShadowDouble.State.Standing && Time.time >= doublesExpire) { d.StartCoroutine(d.Dissolve(0.5f)); continue; }
                if (d.Current == ShadowDouble.State.Standing) d.Mirror(animator);
            }

        // The finale twist. One number: the doubles turn solid and marked, and nobody can tell.
        if (finale && !twisted && health != null && health.maxHealth > 0f &&
            health.CurrentHealth / health.maxHealth <= twistAtFraction)
        {
            twisted = true;
            SfxManager.PlayOn(sfx, SplitClip, sfxVolume);
            if (doubles != null)
                foreach (var d in doubles)
                    if (d != null) { d.SetAlpha(1f); d.ShowMark(true, markColour); }
        }
    }

    private void OnDamaged()
    {
        if (sheathed) counterTriggered = true;
    }

    private void OnBossDied()
    {
        StopAllCoroutines();
        ClearAnimatorState();
        SetPlayerCollision(true);
        RestoreGravity();
        OpenExit();
        if (MusicManager.instance != null) MusicManager.instance.StopBossMusic();

        // He comes apart into two of himself: the doubles stand up either side and fade.
        if (doubles != null)
            for (int i = 0; i < Mathf.Min(2, doubles.Length); i++)
            {
                var d = doubles[i]; if (d == null) continue;
                d.Appear(transform.position + new Vector3(i == 0 ? -1.4f : 1.4f, 0f, 0f), facingRight, 0.8f);
                d.SetState(ShadowDouble.State.Kneeling);   // untouchable — the fight is over
                d.StartCoroutine(d.Dissolve(1.2f));
            }

        // The celebration runs on its OWN object: EnemyHealth.Die destroys this one this frame.
        if (playDeathEffect)
        {
            bool airborne; float groundY = ResolveDeathGroundY(out airborne);
            var go = new GameObject("BossDeathVFX");
            go.transform.position = transform.position + Vector3.up * 0.9f;
            go.AddComponent<BossDeathVFX>().Play(groundY, airborne, deathGoldPrefab, deathShiftCrystalPrefab,
                                                 deathSound, deathVolume, deathGoldCount, deathCrystalCount, deathBurstColor);
        }
        if (offerBossRelic) BossRewardCue.Schedule(bossRelicChoices, rewardDelay);
    }

    private float ResolveDeathGroundY(out bool airborne)
    {
        Vector3 feet = transform.position; airborne = true; float groundY = feet.y;
        float nearest = float.MaxValue;
        foreach (var h in Physics2D.RaycastAll(feet + Vector3.up * 0.3f, Vector2.down, 2.2f))
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.CompareTag("Player")) continue;
            if (h.distance < nearest) { nearest = h.distance; groundY = h.point.y; airborne = false; }
        }
        return groundY;
    }

    // ---- the exit -----------------------------------------------------------------------------------
    private void SealExit()
    {
        Transform root = transform; while (root.parent != null) root = root.parent;
        exit = root.GetComponentInChildren<ExitDoor>(true);
        if (exit != null) exit.SetLocked(true);
    }
    private void OpenExit() { if (exit != null) exit.SetLocked(false); }

    // ---- geometry -----------------------------------------------------------------------------------
    private Vector2 ChestPoint => (Vector2)transform.position + (body != null ? body.offset : Vector2.up);
    private Vector2 CapsuleSize => body is CapsuleCollider2D c ? c.size : new Vector2(0.63f, 2.1f);

    private bool IsGrounded()
    {
        var cap = body as CapsuleCollider2D;
        float halfHeight = cap != null ? cap.size.y * 0.5f : 1f;
        return Physics2D.Raycast(ChestPoint, Vector2.down, halfHeight + 0.25f, LayerMask.GetMask("Ground")).collider != null;
    }

    // Three heights, not one — a single chest ray walks over a low ledge and under an overhang.
    private bool WallAhead(float dir)
    {
        float halfW = CapsuleSize.x * 0.5f, halfH = CapsuleSize.y * 0.5f, probe = halfW + 0.25f;
        Vector2 c = ChestPoint, d = new Vector2(dir, 0f);
        int ground = LayerMask.GetMask("Ground");
        return Physics2D.Raycast(c, d, probe, ground).collider != null
            || Physics2D.Raycast(c + Vector2.up * (halfH * 0.75f), d, probe, ground).collider != null
            || Physics2D.Raycast(c - Vector2.up * (halfH * 0.75f), d, probe, ground).collider != null;
    }

    private static bool Blocked(Vector2 centre, Vector2 size)
    {
        foreach (var h in Physics2D.OverlapBoxAll(centre, size, 0f, LayerMask.GetMask("Ground")))
            if (h != null && !h.isTrigger) return true;
        return false;
    }

    private bool Fits(Vector3 feet)
    {
        Vector2 offset = body != null ? body.offset : Vector2.up;
        return !Blocked((Vector2)feet + offset, CapsuleSize * 0.95f);
    }

    // The watchdog. If he is ever inside terrain, ring-search outward for somewhere his capsule
    // fits — inside the room's CameraBounds first, anywhere second — and put him there. No
    // mid-move exemption flag: that flag is a latch, and the detection box (0.8x) is smaller than
    // the fit test (0.95x), so being pressed against a wall at speed does not trip it.
    private void EnsureNotStuck()
    {
        if (body == null || Time.time < nextStuckCheck) return;
        nextStuckCheck = Time.time + 0.25f;
        if (!Blocked(ChestPoint, CapsuleSize * 0.8f)) return;

        Vector2 offset = body.offset;
        Vector2 size = CapsuleSize;
        for (float r = 0.3f; r <= 16f; r += 0.3f)
            for (int i = 0; i < 24; i++)
            {
                float ang = -90f + ((i + 1) / 2) * 15f * ((i % 2 == 0) ? 1f : -1f);
                Vector2 foot = (Vector2)transform.position + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * r;
                if (Blocked(foot + offset, size * 0.95f)) continue;
                var down = Physics2D.Raycast(foot + offset, Vector2.down, 3f, LayerMask.GetMask("Ground"));
                if (down.collider != null)
                {
                    Vector2 g = new Vector2(foot.x, down.point.y + 0.02f);
                    if (!Blocked(g + offset, size * 0.95f)) foot = g;
                }
                transform.position = new Vector3(foot.x, foot.y, PlayPlane.Z);
                if (rb != null) rb.linearVelocity = Vector2.zero;
                Puff(ChestPoint - Vector2.up * 0.9f, 8, 1.1f, 1f);
                return;
            }
    }

    private void SetPlayerCollision(bool enabled)
    {
        if (body == null || GameManager.instance == null || GameManager.instance.player == null) return;
        var pc = GameManager.instance.player.GetComponent<Collider2D>();
        if (pc != null) Physics2D.IgnoreCollision(body, pc, !enabled);
    }

    // ⚠️ Called from death and OnDestroy too. These bools live on a controller the PLAYER also
    // uses; a boss killed mid-swing must not leave any of them hot.
    private void ClearAnimatorState()
    {
        if (animator == null) return;
        animator.SetBool("IsCrouching", false);
        animator.SetBool("IsDashing", false);
        animator.SetBool("IsAttacking", false);
        animator.SetFloat("AttackSpeedMul", 1f);
        animator.SetBool("IsMoving", false);
        animator.SetFloat("MoveBlendX", 0f);
    }

    private void FaceDirection(float dir)
    {
        facingRight = dir > 0f;
        if (visualModel == null) return;
        Vector3 s = visualModel.localScale; s.x = visualScaleX * (facingRight ? 1f : -1f); visualModel.localScale = s;
    }

    private void FaceTowardPlayer()
    {
        if (player == null) return;
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return;
        FaceDirection(dx);
    }

    // Ground dust, biased along `bias` so a launch throws it back and a skid throws it forward.
    private void Puff(Vector2 pos, int count, float scale, float bias)
    {
        var root = new GameObject("Dust");
        root.transform.position = new Vector3(pos.x, pos.y, PlayPlane.Z);
        root.AddComponent<TemporaryObject>();
        for (int i = 0; i < count; i++)
        {
            var s = new GameObject("Mote");
            s.transform.SetParent(root.transform, false);
            float ang = Random.Range(-35f, 35f) + (bias >= 0f ? 0f : 180f);
            float len = Random.Range(0.14f, 0.32f) * scale;
            s.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            s.transform.localPosition = Quaternion.Euler(0f, 0f, ang) * Vector3.right * (len * 0.6f);
            s.transform.localScale = new Vector3(len, Random.Range(0.05f, 0.10f) * scale, 1f);
            var sr = s.AddComponent<SpriteRenderer>();
            sr.sprite = FlatUI.Pixel();
            sr.color = new Color(0.72f, 0.68f, 0.62f, 0.85f);
            sr.sortingOrder = 6;
        }
        root.AddComponent<SparkFade>();   // AFTER the motes exist — it gathers them in Awake
    }
}
