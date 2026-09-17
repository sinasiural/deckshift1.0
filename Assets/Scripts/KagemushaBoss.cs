using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A boss that is some character's mirror. LevelManager calls SetFinale(true) on it when its room
/// is spawned as the played character's OWN finale, so the same prefab can be a lesser mid-map cut
/// of itself for everyone else.
/// </summary>
public interface IMirrorBoss
{
    void SetFinale(bool isFinale);
}

/// <summary>
/// Kagemusha — the Samurai's body double. A floor boss, and the Samurai character's mirror finale.
/// Design doc: BossDesign_Samurai.md. Written standalone (designer 2026-09-17: "a new and clean
/// boss script… they are not really correlated" with the Ninja).
///
/// KIT v2 (rewritten with the designer 2026-09-17 — the first kit was "not cool enough"):
///
///   THE CROSSING   he blurs straight THROUGH you and stops on the far side with his back turned,
///                  leaving a SHADOW of himself standing where he started. Nothing happens… until
///                  he sheathes. Break the shadow before the click and the cut is cancelled — HE
///                  takes it, and a Shift crystal drops. Don't, and you take it. This is the
///                  card-free damage route, the Shift income and the dodge-or-punish decision in
///                  one mechanic. It is also Through and Through further gone: your card leaves
///                  afterimages, his leaves bodies.
///   HUNDRED CUTS   he sheathes, the room dims, and cut-lines flash into existence one by one
///                  across the whole hall, hanging there like cracks in glass. Then a click, and
///                  they all land at once. There is always a gap — and always one ON THE FLOOR, so
///                  a player at 0 Shift who cannot jump still has an answer. Reaches every ledge,
///                  so no tier is a refuge.
///   THE REVEAL     finale only, at 40% health: his shadows stop fading — a Crossing leaves a SOLID
///                  him behind, marked like the real one — and Hundred Cuts fires twice. He
///                  doesn't pay Shift.
///
/// ⚠️ HE NEVER TELEPORTS. Teleport-to-marker is the Ninja's identity. The Crossing is a dash you
/// can watch; the shadow is where he WAS.
///
/// He animates on the Cainos *Customizable Pixel Character* rig, so the animator handles are the
/// player's: `AttackAction` (INT), `IsAttacking`, `IsCrouching`, `IsDashing`, `MoveBlendX`.
///
/// Generic lessons kept from the two bosses before him, each paid for with a real bug: gravity
/// captured ONCE in Awake and every restore uses that value; every latch (gravity, player
/// collision, animator bools) released in a `finally` AND in OnBossDied AND in OnDestroy; positions
/// from transform + collider offset, never `collider.bounds`; a stuck watchdog with NO mid-move
/// exemption flag; the exit sealed on Start and unsealed from OnDestroy as well as death.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class KagemushaBoss : MonoBehaviour, IBossFight, IMirrorBoss
{
    public void SetFinale(bool isFinale) => finale = isFinale;

    // Read out of AC Character's own transitions: 1=Swipe, 2=Stab, 13=Throw, 14=Cast. Swipe plays
    // on BOTH the Arm and Body layers and reads as a committed slash.
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
    [Tooltip("Left EMPTY on purpose — the Samurai preset already holds PF Weapon - Katana.")]
    public GameObject weaponPrefab;

    [Header("The doubles")]
    [Tooltip("The stripped copies of his rig that ship as children of this prefab. Two is the finale count.")]
    public ShadowDouble[] doubles;
    [Tooltip("How see-through a shadow is. ⚠️ The rig cannot be tinted, only faded — see ShadowDouble.")]
    [Range(0f, 1f)] public float doubleAlpha = 0.5f;
    [Tooltip("TRUE when this arena is the Samurai's own finale. LevelManager sets it when the room is " +
             "spawned as the finale; the test menu can force it.")]
    public bool finale = false;
    [Tooltip("Finale only: below this health fraction the Reveal happens — shadows turn SOLID and " +
             "marked, Hundred Cuts fires twice, the Crossing chains three.")]
    [Range(0f, 1f)] public float twistAtFraction = 0.4f;
    [Tooltip("How long a shadow stands around after the click before fading.")]
    public float doubleLifetime = 7f;
    [Tooltip("The warm mark under the real one's feet. Shadows carry none — until the Reveal.")]
    public Color markColour = new Color(0.980f, 0.706f, 0.365f, 1f);   // Salvage.Torch

    [Header("Between attacks (TUNE BY EYE)")]
    public float betweenAttacks = 1.1f;
    public float walkSpeed = 4.5f;
    public float preferredRange = 6f;
    public float repositionJitter = 2.5f;

    [Header("The Crossing (TUNE BY EYE)")]
    [Tooltip("How long he stands still, hand on the hilt, before he goes. The lane is drawn for all of it.")]
    public float crossWindup = 0.55f;
    public float crossSpeed = 34f;
    [Tooltip("How far PAST the player he stops. He ends with his back to you.")]
    public float crossOvershoot = 3.2f;
    public float crossMaxLength = 14f;
    [Tooltip("⚠️ THE WINDOW. After he crosses, this long passes before the click. It is the time you " +
             "have to reach the shadow he left and break it — or to accept the cut. It is the fight.")]
    public float crossSheatheDelay = 1.1f;
    public float crossDamage = 20f;
    public float crossKnockback = 8f;
    [Tooltip("Height of the pass-through hit box. ~2 covers a standing player; a jump clears it.")]
    public float crossHeight = 2f;
    [Tooltip("Recovery after the click — the punish window.")]
    public float crossRecovery = 0.7f;
    [Tooltip("Damage HE takes when a shadow is broken. ⚠️ 12 is 12 — worth a little less than a " +
             "Fireball, never a secret boss-only multiplier.")]
    public float shatterDamage = 12f;
    [Tooltip("Assign Prefabs/ShiftCrystal. One per broken shadow is the single most sensitive number " +
             "in the encounter — keep it ONE tunable.")]
    public GameObject shiftCrystalPrefab;
    public int crystalsPerShatter = 1;
    public Color laneColor = new Color(0.980f, 0.706f, 0.365f, 1f);       // Torch gold, not the Ninja's red
    [Tooltip("The streak he leaves when he crosses. Cold steel against the player's warm gold.")]
    public Color streakColour = new Color(0.78f, 0.86f, 1f, 1f);

    [Header("Hundred Cuts (TUNE BY EYE)")]
    [Tooltip("Seconds between Hundred Cuts. It has first refusal in the loop, on a timer.")]
    public float cutsInterval = 11f;
    public int cutsCount = 12;
    [Tooltip("Seconds between one line appearing and the next. The whole pattern takes count x this.")]
    public float cutsLineInterval = 0.09f;
    [Tooltip("How long the finished pattern hangs before the click. This is the READ time.")]
    public float cutsHang = 0.55f;
    [Tooltip("Half-width of a line's hit. A line is a blade; the player is hit if their body is within this of it.")]
    public float cutsHalfWidth = 0.5f;
    [Tooltip("How far every line must stay from a safe pocket. Bigger = easier to read, easier to stand in.")]
    public float cutsSafeRadius = 1.6f;
    [Tooltip("How many safe pockets the pattern guarantees. ⚠️ ONE OF THEM IS ALWAYS ON THE FLOOR.")]
    public int cutsSafePockets = 3;
    public float cutsDamage = 24f;
    public float cutsKnockback = 6f;
    [Tooltip("How dark the room goes. World-space quad behind the actors, so figures and lines stay bright.")]
    [Range(0f, 1f)] public float cutsDim = 0.55f;
    [Tooltip("Recovery after the cuts land, blade still out. THE punish window of the fight.")]
    public float cutsRecovery = 1.2f;
    public Color cutsColour = new Color(0.980f, 0.706f, 0.365f, 1f);

    [Header("Death")]
    public bool playDeathEffect = true;
    public AudioClip deathSound;
    [Range(0f, 2f)] public float deathVolume = 1.4f;
    public GameObject deathGoldPrefab;
    public GameObject deathShiftCrystalPrefab;
    public int deathGoldCount = 14;
    public int deathCrystalCount = 5;
    [Tooltip("He comes apart into steel-grey — the shadows' colour.")]
    public Color deathBurstColor = new Color(0.80f, 0.84f, 0.90f);
    public bool offerBossRelic = true;
    [Range(1, 4)] public int bossRelicChoices = 2;
    public float rewardDelay = 2.6f;

    // ⚠️ EVERY SLOT HERE IS AN OVERRIDE. Every sound plays procedurally whether or not a clip is
    // dragged in — an empty AudioClip field is a silent no-op, and that is where this project's
    // silence has always lived. The defaults are BORROWED clips; a SAMURAI family is the follow-up.
    [Header("Audio (leave empty — procedural by default)")]
    public AudioClip drawSound;
    public AudioClip sheatheSound;
    public AudioClip lineSound;
    public AudioClip landSound;
    public AudioClip shatterSound;
    public AudioClip splitSound;
    [Range(0f, 2f)] public float sfxVolume = 1f;

    private AudioClip DrawClip    => drawSound    != null ? drawSound    : ProcSfx.FreefallBlade;
    private AudioClip SheatheClip => sheatheSound != null ? sheatheSound : ProcSfx.KatanaPlant;
    private AudioClip LineClip    => lineSound    != null ? lineSound    : ProcSfx.ShurikenStick;
    private AudioClip LandClip    => landSound    != null ? landSound    : ProcSfx.MeteorImpact;
    private AudioClip ShatterClip => shatterSound != null ? shatterSound : ProcSfx.WallBreak;
    private AudioClip SplitClip   => splitSound   != null ? splitSound   : ProcSfx.NinjaBlink;

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
    private bool twisted;
    private float nextCuts, doublesExpire, nextStuckCheck;
    private int nextShadow;

    // The Crossing's pending cut. The shadow that can cancel it is `crossingShadow`.
    private ShadowDouble crossingShadow;
    private bool crossingCancelled;

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

    // ⚠️ Through the pack's own AddWeapon, never hand-parented. Wrapped so a cosmetic failure inside
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

        if (health != null) health.OnDied += OnBossDied;

        // Sealed from the moment the room exists — a door that seals in front of you is worse than
        // one that was always shut.
        SealExit();

        if (startDormant) Kneel();
        else StartFight();
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= OnBossDied;
        ClearAnimatorState();
        RestoreGravity();
        SetPlayerCollision(true);
        if (dim != null) Destroy(dim.gameObject);
        // ⚠️ FAIL TOWARD PASSABLE. However he leaves the world, the exit must not stay sealed.
        if (exit != null) exit.SetLocked(false);
    }

    // ---- the opening beat --------------------------------------------------------------------------
    // Three of him kneel at the far end, identical and solid. Cross the line and two dissolve; the one
    // left stands. He shows you the trick before he uses it.
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

        nextCuts = Time.time + 7f;      // the first thing he does is cross you — teach the shadow first
        StartCoroutine(FightLoop());
    }

    // ---- the loop ----------------------------------------------------------------------------------
    // Two attacks. Hundred Cuts has first refusal on its timer; otherwise he crosses. That is the
    // rhythm: cross, cross, the room goes dark, cross, cross…
    private IEnumerator FightLoop()
    {
        while (health != null && health.CurrentHealth > 0f)
        {
            if (player == null && GameManager.instance != null && GameManager.instance.player != null)
                player = GameManager.instance.player.transform;
            if (player == null) { yield return null; continue; }

            if (Time.time >= nextCuts && IsGrounded())
            {
                nextCuts = Time.time + cutsInterval;
                yield return StartCoroutine(HundredCutsRoutine());
                if (twisted) yield return StartCoroutine(HundredCutsRoutine());   // the Reveal: twice
            }
            else if (IsGrounded())
            {
                int chain = twisted ? 3 : (finale ? 2 : 1);
                yield return StartCoroutine(CrossingRoutine(chain));
            }

            yield return StartCoroutine(RepositionRoutine(betweenAttacks));
        }
    }

    // He walks — the pack's walk blend — to a jittered preferred distance. Never a statue.
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
                animator.SetFloat("MoveBlendX", moving ? 1f : 0f);
                animator.SetFloat("MoveSpeedMul", 1f);
            }
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) { animator.SetBool("IsMoving", false); animator.SetFloat("MoveBlendX", 0f); }
        FaceTowardPlayer();
    }

    // ================================================================================================
    // THE CROSSING
    // ================================================================================================
    // Windup (lane drawn, hand on hilt) → the blur through you, leaving a shadow where he stood →
    // he stands on the far side, back turned, for crossSheatheDelay → the click.
    //
    // `chain` crossings run back to back, each leaving its own shadow, ONE click at the end for all
    // of them. Breaking ANY shadow he left in the chain cancels the whole cut — the finale's three
    // crossings are three chances, not three sentences.
    private IEnumerator CrossingRoutine(int chain)
    {
        crossingCancelled = false;
        bool crossedPlayer = false;
        var shadows = new List<ShadowDouble>();

        for (int c = 0; c < Mathf.Max(1, chain); c++)
        {
            FaceTowardPlayer();
            float dir = facingRight ? 1f : -1f;
            float lane = MeasureLane(dir);
            Vector2 terminus = ChestPoint + new Vector2(dir * lane, 0f);

            // ---- windup: the lane, and the stillness -------------------------------------------
            var tel = LaneTelegraph.Build(ChestPoint, terminus, crossHeight, LaneTelegraph.Style.Default(laneColor));
            if (rb != null) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; }
            if (animator != null) animator.SetBool("IsCrouching", true);
            float windup = c == 0 ? crossWindup : crossWindup * 0.5f;   // later links load faster
            float t = 0f;
            while (t < windup)
            {
                t += Time.deltaTime;
                tel.Place(ChestPoint, terminus, crossHeight);
                tel.SetIntensity(Mathf.Clamp01(t / windup));
                yield return null;
            }
            tel.Clear();

            // ---- the shadow: him, as he was, where he was ----------------------------------------
            ShadowDouble shadow = NextShadow();
            if (shadow != null)
            {
                shadow.Appear(transform.position, facingRight, twisted ? 1f : doubleAlpha);
                shadow.SetState(ShadowDouble.State.Armed);
                shadow.ShowMark(twisted, markColour);
                shadow.Pose(true, false, 0, false);          // crouched, hand on hilt — as he was
                shadows.Add(shadow);
                crossingShadow = shadow;
            }

            // ---- the blur ------------------------------------------------------------------------
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
            CutStreak streak = CutStreak.Begin(ChestPoint, streakColour, 0.12f);
            try
            {
                float elapsed = 0f, stalled = 0f, lastX = transform.position.x;
                while ((terminus.x - transform.position.x) * dir > 0.05f)
                {
                    elapsed += Time.fixedDeltaTime;
                    if (elapsed > TRAVEL_TIMEOUT) break;
                    float moved = Mathf.Abs(transform.position.x - lastX);
                    lastX = transform.position.x;
                    stalled = moved < 0.01f ? stalled + Time.fixedDeltaTime : 0f;
                    if (stalled > TRAVEL_STALL) break;

                    rb.linearVelocity = new Vector2(dir * crossSpeed, 0f);
                    streak.SetEnd(ChestPoint);

                    // Crossed, not cut. A spark says "that counted"; the damage waits for the click.
                    if (!crossedPlayer && PlayerInBox(ChestPoint, new Vector2(1.2f, crossHeight)))
                    {
                        crossedPlayer = true;
                        if (player != null) Puff((Vector2)player.position + Vector2.up * 0.9f, 6, 1.2f, dir);
                        if (HitStop.instance != null) HitStop.instance.Stop(0.03f);
                    }

                    if (WallAhead(dir)) break;
                    yield return new WaitForFixedUpdate();
                }
            }
            finally
            {
                SetPlayerCollision(true);
                RestoreGravity();
                if (streak != null) streak.Release(0.45f);
            }

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (animator != null) { animator.SetBool("IsDashing", false); animator.SetBool("IsAttacking", false); }
            Puff(ChestPoint - new Vector2(0f, 0.9f), 6, 0.9f, -dir);
            // He does NOT turn round. Back to you, blade out. That is the tell that the cut is
            // still in the air.
        }

        // ---- the window -------------------------------------------------------------------------
        // He stands. You have crossSheatheDelay to reach a shadow. The mark pulses on him — the
        // only motion on him while the cut hangs.
        float wait = 0f;
        while (wait < crossSheatheDelay && !crossingCancelled)
        {
            wait += Time.deltaTime;
            if (rb != null && IsGrounded()) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (mark != null) mark.color = new Color(markColour.r, markColour.g, markColour.b, 0.35f + 0.35f * Mathf.PingPong(wait * 4f, 1f));
            yield return null;
        }
        if (mark != null) mark.color = new Color(markColour.r, markColour.g, markColour.b, 0.55f);
        crossingShadow = null;

        // ---- the click ----------------------------------------------------------------------------
        foreach (var s in shadows)
            if (s != null && s.Current == ShadowDouble.State.Armed) s.SetState(ShadowDouble.State.Standing);
        doublesExpire = Time.time + doubleLifetime;

        if (!crossingCancelled)
        {
            SfxManager.PlayOn(sfx, SheatheClip, sfxVolume);
            FaceTowardPlayer();
            if (crossedPlayer && player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    float kdir = Mathf.Sign(player.position.x - transform.position.x); if (kdir == 0f) kdir = 1f;
                    CutMark.Spawn((Vector2)player.position + Vector2.up * 0.9f, streakColour, 1.5f);
                    if (HitStop.instance != null) HitStop.instance.Stop(0.09f);
                    if (CameraShake.instance != null) CameraShake.instance.Shake(0.22f, 0.35f);
                    pc.TakeDamage(crossDamage);
                    pc.ApplyKnockback(new Vector2(kdir * crossKnockback, crossKnockback * 0.5f));
                }
            }
        }
        else
        {
            // Cancelled: the click never comes. He flinches instead — the shard went home.
            FaceTowardPlayer();
        }

        yield return new WaitForSeconds(crossRecovery);
    }

    private const float TRAVEL_TIMEOUT = 1.4f;
    private const float TRAVEL_STALL = 0.10f;

    private float MeasureLane(float dir)
    {
        float want = crossMaxLength;
        if (player != null) want = Mathf.Abs(player.position.x - transform.position.x) + crossOvershoot;
        want = Mathf.Min(want, crossMaxLength);
        var hit = Physics2D.Raycast(ChestPoint, new Vector2(dir, 0f), want, LayerMask.GetMask("Ground"));
        return hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.6f) : want;
    }

    private ShadowDouble NextShadow()
    {
        if (doubles == null || doubles.Length == 0) return null;
        // Prefer a hidden one; otherwise recycle the oldest standing shadow.
        for (int i = 0; i < doubles.Length; i++)
        {
            var d = doubles[(nextShadow + i) % doubles.Length];
            if (d != null && d.Current == ShadowDouble.State.Hidden) { nextShadow = (nextShadow + i + 1) % doubles.Length; return d; }
        }
        var recycled = doubles[nextShadow % doubles.Length];
        nextShadow = (nextShadow + 1) % doubles.Length;
        return recycled;
    }

    /// <summary>Called by a shadow the player touched while it was ARMED. The shard goes home.</summary>
    public void OnDoubleShattered(ShadowDouble d)
    {
        if (d == null || d.Current != ShadowDouble.State.Armed) return;
        Vector3 at = d.transform.position;
        d.Vanish();

        SfxManager.PlayOn(sfx, ShatterClip, sfxVolume);
        Puff(at + Vector3.up * 0.9f, 10, 1.3f, 1f);
        Puff(at + Vector3.up * 0.9f, 10, 1.3f, -1f);
        CutMark.Spawn(ChestPoint, streakColour, 1.4f);     // the cut lands on HIM
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.14f, 0.25f);
        if (HitStop.instance != null) HitStop.instance.Stop(0.06f);

        crossingCancelled = true;
        if (health != null) health.TakeDamage(shatterDamage);

        if (shiftCrystalPrefab != null)
            for (int i = 0; i < crystalsPerShatter; i++)
            {
                GameObject c = Instantiate(shiftCrystalPrefab, at + Vector3.up * 1.1f + (Vector3)(Random.insideUnitCircle * 0.3f), Quaternion.identity);
                if (c.GetComponent<TemporaryObject>() == null) c.AddComponent<TemporaryObject>();
            }
    }

    // ================================================================================================
    // HUNDRED CUTS
    // ================================================================================================
    // He sheathes. The room dims. Lines flash in one by one. They hang. Click.
    //
    // ⚠️ THE PATTERN IS BUILT AROUND ITS SAFE POCKETS, NOT CHECKED FOR THEM AFTERWARDS. Pockets are
    // chosen first from spots the player can actually stand on, and every line is rejected if it
    // comes within cutsSafeRadius of one. That is what makes "find the gap" a promise rather than a
    // probability — and one pocket is always on the FLOOR, because a player at 0 Shift cannot jump
    // and this is the room where ending at 0 is likely.
    private struct Cut { public Vector2 a, b; }
    private SpriteRenderer dim;

    private IEnumerator HundredCutsRoutine()
    {
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        FaceTowardPlayer();
        if (animator != null) animator.SetBool("IsCrouching", true);
        SfxManager.PlayOn(sfx, SheatheClip, sfxVolume * 0.8f);

        // The room goes dark. A quad behind the actors, so he, his shadows, the player and the
        // lines all stay lit — the world does not.
        Bounds area = PlayArea ?? new Bounds(transform.position, new Vector3(60f, 30f, 1f));
        if (dim == null)
        {
            var go = new GameObject("HundredCutsDim");
            go.AddComponent<TemporaryObject>();
            dim = go.AddComponent<SpriteRenderer>();
            dim.sprite = FlatUI.Pixel();
            dim.sortingOrder = 40;
        }
        Vector2 native = dim.sprite.bounds.size;
        dim.transform.position = new Vector3(area.center.x, area.center.y, PlayPlane.Z + 0.3f);
        dim.transform.localScale = new Vector3((area.size.x + 30f) / native.x, (area.size.y + 20f) / native.y, 1f);
        dim.gameObject.SetActive(true);
        float d0 = 0f;
        while (d0 < 0.25f) { d0 += Time.deltaTime; dim.color = new Color(0f, 0f, 0f, cutsDim * (d0 / 0.25f)); yield return null; }
        dim.color = new Color(0f, 0f, 0f, cutsDim);

        // ---- the pattern ----------------------------------------------------------------------
        List<Vector2> pockets = ChoosePockets(area, cutsSafePockets);
        List<Cut> cuts = new List<Cut>();
        List<CutStreak> lines = new List<CutStreak>();
        int tries = 0;
        while (cuts.Count < cutsCount && tries < cutsCount * 30)
        {
            tries++;
            // Mostly shallow, a few steep. Through a random point in the room.
            float ang = Random.value < 0.7f ? Random.Range(-28f, 28f) : Random.Range(-70f, 70f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            Vector2 p = new Vector2(Random.Range(area.min.x + 1f, area.max.x - 1f), Random.Range(area.min.y + 0.8f, area.max.y - 0.8f));
            Cut cut = ClipToArea(p, dir, area);

            bool ok = true;
            foreach (var s in pockets) if (DistanceToSegment(s, cut.a, cut.b) < cutsSafeRadius) { ok = false; break; }
            if (!ok) continue;
            foreach (var other in cuts)
                if (DistanceToSegment((cut.a + cut.b) * 0.5f, other.a, other.b) < 0.7f) { ok = false; break; }
            if (!ok) continue;
            cuts.Add(cut);
        }

        // ---- they appear, one by one --------------------------------------------------------
        foreach (var cut in cuts)
        {
            var line = CutStreak.Begin(cut.a, cutsColour, 0.07f, 45);
            line.SetEnd(cut.b);
            lines.Add(line);
            SfxManager.PlayOn(sfx, LineClip, sfxVolume * 0.35f);
            yield return new WaitForSeconds(cutsLineInterval);
        }

        // ---- they hang --------------------------------------------------------------------------
        yield return new WaitForSeconds(cutsHang);

        // ---- the click --------------------------------------------------------------------------
        SfxManager.PlayOn(sfx, LandClip, sfxVolume);
        if (HitStop.instance != null) HitStop.instance.Stop(0.10f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.35f, 0.45f);
        if (animator != null) { animator.SetBool("IsCrouching", false); animator.SetInteger("AttackAction", SWIPE_ACTION); animator.SetBool("IsAttacking", true); }

        foreach (var line in lines) if (line != null) line.Release(0.30f);
        foreach (var cut in cuts)
        {
            // A white flash along each line as it lands.
            var flash = CutStreak.Begin(cut.a, Color.white, 0.16f, 46);
            flash.SetEnd(cut.b);
            flash.Release(0.22f);
        }

        // One hit, however many lines you were standing in. Tested against three points up the
        // player's body so a line through the chest counts and one over the head does not.
        if (player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            bool hit = false;
            Vector2 feet = player.position;
            Vector2[] probes = { feet + Vector2.up * 0.35f, feet + Vector2.up * 0.9f, feet + Vector2.up * 1.45f };
            foreach (var cut in cuts)
            {
                foreach (var pr in probes)
                    if (DistanceToSegment(pr, cut.a, cut.b) <= cutsHalfWidth) { hit = true; break; }
                if (hit) break;
            }
            if (hit && pc != null)
            {
                CutMark.Spawn(feet + Vector2.up * 0.9f, Color.white, 1.8f);
                pc.TakeDamage(cutsDamage);
                pc.ApplyKnockback(new Vector2(Random.value < 0.5f ? -cutsKnockback : cutsKnockback, cutsKnockback * 0.7f));
            }
        }

        // ---- the room comes back, and he is open -----------------------------------------------
        float d1 = 0f;
        while (d1 < 0.35f) { d1 += Time.deltaTime; dim.color = new Color(0f, 0f, 0f, cutsDim * (1f - d1 / 0.35f)); yield return null; }
        dim.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.2f);
        if (animator != null) animator.SetBool("IsAttacking", false);
        yield return new WaitForSeconds(cutsRecovery);
    }

    // Safe pockets: real standing spots, spread apart, the first always on the floor.
    private List<Vector2> ChoosePockets(Bounds area, int count)
    {
        var spots = new List<Vector2>();
        int ground = LayerMask.GetMask("Ground");
        Vector2 offset = body != null ? body.offset : Vector2.up;
        Vector2 size = CapsuleSize;

        // Sample the interior: every 0.5 in x, every 1 in y; a spot stands if the capsule fits there
        // and there is ground within a third of a unit below the feet.
        for (float x = area.min.x + 0.6f; x <= area.max.x - 0.6f; x += 0.5f)
            for (float y = area.min.y + 0.1f; y <= area.max.y - 2f; y += 1f)
            {
                Vector2 foot = new Vector2(x, y);
                if (Blocked(foot + offset, size * 0.95f)) continue;
                var down = Physics2D.Raycast(foot + Vector2.up * 0.1f, Vector2.down, 0.35f, ground);
                if (down.collider == null) continue;
                spots.Add(new Vector2(x, down.point.y));
            }

        var chosen = new List<Vector2>();
        if (spots.Count == 0) { chosen.Add(player != null ? (Vector2)player.position : (Vector2)transform.position); return chosen; }

        float floorY = float.MaxValue;
        foreach (var s in spots) floorY = Mathf.Min(floorY, s.y);

        // Floor pocket first — never one the boss is standing in, and away from him so the answer
        // is "move", not "stand next to him".
        var floor = spots.FindAll(s => Mathf.Abs(s.y - floorY) < 0.2f && Mathf.Abs(s.x - transform.position.x) > 2.5f);
        if (floor.Count == 0) floor = spots.FindAll(s => Mathf.Abs(s.y - floorY) < 0.2f);
        if (floor.Count > 0) chosen.Add(floor[Random.Range(0, floor.Count)] + Vector2.up * 0.9f);

        // Then spread the rest as far from each other as the room allows.
        int guard = 0;
        while (chosen.Count < count && guard++ < 200)
        {
            Vector2 best = spots[Random.Range(0, spots.Count)];
            float bestScore = -1f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 cand = spots[Random.Range(0, spots.Count)] + Vector2.up * 0.9f;
                float score = float.MaxValue;
                foreach (var c in chosen) score = Mathf.Min(score, Vector2.Distance(c, cand));
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            chosen.Add(best);
        }
        return chosen;
    }

    private static Cut ClipToArea(Vector2 p, Vector2 dir, Bounds area)
    {
        // Walk out both ways until the room edge.
        float tMin = -1000f, tMax = 1000f;
        void Clip(float p0, float d, float lo, float hi)
        {
            if (Mathf.Abs(d) < 1e-5f) return;
            float t0 = (lo - p0) / d, t1 = (hi - p0) / d;
            if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }
            tMin = Mathf.Max(tMin, t0); tMax = Mathf.Min(tMax, t1);
        }
        Clip(p.x, dir.x, area.min.x, area.max.x);
        Clip(p.y, dir.y, area.min.y, area.max.y);
        return new Cut { a = p + dir * tMin, b = p + dir * tMax };
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
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

        // Standing shadows fade out on their clock. Armed ones (a cut in the air) never expire early.
        if (doubles != null)
            foreach (var d in doubles)
                if (d != null && d.Current == ShadowDouble.State.Standing && Time.time >= doublesExpire)
                    d.StartCoroutine(d.Dissolve(0.5f));

        // THE REVEAL. One number: the shadows turn solid and marked, and nobody can tell.
        if (finale && !twisted && health != null && health.maxHealth > 0f &&
            health.CurrentHealth / health.maxHealth <= twistAtFraction)
        {
            twisted = true;
            SfxManager.PlayOn(sfx, SplitClip, sfxVolume);
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.2f, 0.5f);
            if (doubles != null)
                foreach (var d in doubles)
                    if (d != null) { d.SetAlpha(1f); d.ShowMark(true, markColour); }
        }
    }

    private void OnBossDied()
    {
        StopAllCoroutines();
        ClearAnimatorState();
        SetPlayerCollision(true);
        RestoreGravity();
        OpenExit();
        if (dim != null) dim.gameObject.SetActive(false);
        if (MusicManager.instance != null) MusicManager.instance.StopBossMusic();

        // He comes apart into two of himself: the shadows stand up either side and fade.
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

    private bool PlayerInBox(Vector2 centre, Vector2 size)
    {
        var hit = Physics2D.OverlapBox(centre, size, 0f, LayerMask.GetMask("Player"));
        return hit != null && hit.GetComponentInParent<PlayerController>() != null;
    }

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

    /// <summary>
    /// The room's own idea of where the fight happens: the CameraBounds zones every room must have.
    /// Cached including the null result.
    /// </summary>
    private Bounds? PlayArea
    {
        get
        {
            if (playAreaResolved) return playArea;
            playAreaResolved = true;
            Transform root = transform; while (root.parent != null) root = root.parent;
            Transform cb = root.Find("CameraBounds");
            if (cb == null) return playArea = null;
            Bounds b = new Bounds(); bool any = false;
            foreach (var c in cb.GetComponentsInChildren<Collider2D>(true))
            {
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            playArea = any ? (Bounds?)b : null;
            return playArea;
        }
    }
    private Bounds? playArea;
    private bool playAreaResolved;

    // The watchdog. If he is ever inside terrain, ring-search outward for somewhere his capsule
    // fits and put him there. No mid-move exemption flag: that flag is a latch, and the detection
    // box (0.8x) is smaller than the fit test (0.95x), so being pressed against a wall does not trip it.
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
