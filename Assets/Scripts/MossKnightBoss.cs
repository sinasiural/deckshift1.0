using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

// Act 1 boss — the Moss Knight. Attack state machine driving the Cainos MonsterController.
// Relies on EnemyHealth for HP/damage/death, so cards AND the Crusher Trap damage it for free
// (both call TakeDamage) and Glass Wail stuns it for free (EnemyHealth.IsStunned).
//
// Implemented: pursue, Acid Cleave (melee), Charge (run-across dash), Leap Slam (+ green acid
// shockwave), Lob (acid blob → lingering puddle on the floor, or a Slime add lobbed onto a camped
// platform — same arc, different payload).
// Still to come: phase transitions, the full acid system (flank pools → P3 rise), boss death/reward.
[RequireComponent(typeof(EnemyHealth))]
// IBossFight is satisfied by the existing public StartFight() below — nothing else changed. It lets
// the generic BossAwakenTrigger wake this boss too, so the two triggers can merge later.
public class MossKnightBoss : MonoBehaviour, IBossFight
{
    [Header("Fight Start")]
    [Tooltip("Sleep until a BossFightTrigger wakes him (place one over the arena platform). " +
             "Uncheck to restore the old behavior: fight starts the moment the room loads.")]
    public bool startDormant = true;
    [Tooltip("Play the dramatic coil→roar awakening when the fight starts. The health bar and boss " +
             "music land on the roar. Uncheck for an instant, effect-free start.")]
    public bool playAwakenEffect = true;
    [Tooltip("Seconds he coils/rumbles before the roar burst.")]
    public float awakenWindup = 0.75f;
    [Tooltip("Green screen-flash colour on the roar.")]
    public Color awakenFlashColor = new Color(0.45f, 0.95f, 0.35f, 0.55f);
    [Tooltip("Scale multiplier on the awaken shockwave rings vs. the slam (reuses Slam Shockwave Effect).")]
    public float awakenShockwaveScale = 1.7f;
    [Tooltip("Gentle, non-damaging upward shove pushed onto the player by the roar. 0 = no shove.")]
    public float awakenPushback = 5f;
    [Tooltip("Number of hard ground-pound stomps during the awakening (0 = none).")]
    public int awakenPounds = 2;
    [Tooltip("Upward launch of each stomp hop — higher = bigger hop, lower/faster = harder slam.")]
    public float poundHopVelocity = 6f;

    [Header("Pursuit")]
    [Tooltip("Engage range. Default arena-wide so the boss always pursues.")]
    public float aggroRange = 40f;
    [Tooltip("Stop closing in once this near (so it doesn't shove into the player).")]
    public float moveStopRange = 1.8f;

    [Header("Acid Cleave (melee)")]
    public float cleaveRange = 2.2f;
    public float cleaveCooldown = 2.0f;
    public float cleaveDamage = 15f;
    [Tooltip("Fallback only: damage now lands on the animation's contact-frame event. This is the " +
             "safety timeout used if that event never fires.")]
    public float cleaveDamageDelay = 0.3f;
    public float cleaveKnockback = 6f;
    public float cleaveRecover = 0.3f;

    [Header("Charge (dash across)")]
    [Tooltip("Only charge when the player is at least this far.")]
    public float chargeMinRange = 4f;
    public float chargeMaxRange = 18f;
    public float chargeCooldown = 5f;
    [Tooltip("Wind-up pause before the dash (the player's cue to dodge).")]
    public float chargeTelegraph = 0.55f;
    [Tooltip("Safety cap on dash length; he normally stops once he passes the player or hits a wall.")]
    public float chargeDuration = 1.4f;
    [Tooltip("Vulnerable pause after the dash.")]
    public float chargeRecover = 0.7f;
    public float chargeDamage = 18f;
    public float chargeKnockback = 9f;
    [Tooltip("Contact range that connects during the dash.")]
    public float chargeHitRange = 1.6f;
    [Tooltip("Dash speed — overrides the run cap during the charge. Higher = more menacing.")]
    public float chargeSpeed = 14f;
    [Tooltip("Dash acceleration — high so he launches fast instead of ramping up.")]
    public float chargeAccel = 70f;
    [Tooltip("Stop this far past the player so he doesn't run to the far wall.")]
    public float chargeOvershoot = 2f;

    [Header("Leap Slam")]
    [Tooltip("Only leap when the player is at least this far.")]
    public float leapMinRange = 4f;
    public float leapCooldown = 7f;
    [Tooltip("Crouch wind-up before launching.")]
    public float leapTelegraph = 0.5f;
    [Tooltip("Arc apex height in world units.")]
    public float leapApexHeight = 4f;
    [Tooltip("Max horizontal distance of a leap (the leap clamps to this toward the player).")]
    public float maxLeapDistance = 12f;
    [Tooltip("Safety cap on air time before forcing the slam.")]
    public float leapMaxAirTime = 3f;
    public float slamRadius = 3f;
    public float slamDamage = 22f;
    public float slamKnockback = 11f;
    [Tooltip("Vulnerable pause after landing.")]
    public float leapRecover = 0.6f;
    [Tooltip("Green acid shockwave spawned on landing (assign AcidShockwaveVFX).")]
    public GameObject slamShockwaveEffect;
    [Tooltip("Gravity multiplier during the leap — higher = snappier, less floaty.")]
    public float leapGravityMul = 2.2f;
    [Tooltip("Extra downward pull while descending — makes the slam land with weight.")]
    public float leapFallMul = 1.4f;
    [Tooltip("Gel sprite flung out on slam impact for splatter (assign Slime Gel). Optional.")]
    public Sprite slamDebrisSprite;

    [Header("Lob (Acid Blob / Slime)")]
    [Tooltip("Carrier projectile arced onto the player/platform (assign AcidBlobProjectile).")]
    public GameObject lobProjectile;
    [Tooltip("Slime add lobbed up when the player camps a platform (assign SlimeEnemy). Empty = acid only.")]
    public GameObject slimeEnemy;
    [Tooltip("On the floor, only lob when the player is at least this far.")]
    public float lobMinRange = 6f;
    [Tooltip("Never lob past this range (the throw can't reach).")]
    public float lobMaxRange = 30f;
    public float lobCooldown = 6f;
    [Tooltip("Wind-up of the throw gesture (the player's cue).")]
    public float lobTelegraph = 0.45f;
    [Tooltip("Vulnerable pause after the throw.")]
    public float lobRecover = 0.5f;
    [Tooltip("How high the blob arcs — raise this to clear tall platforms.")]
    public float lobArcHeight = 6f;
    [Tooltip("Air time of the blob — longer is easier to read and dodge.")]
    public float lobTravelTime = 0.9f;
    [Tooltip("Player counts as 'camped on a platform' (priority lob target) when this far above the boss.")]
    public float lobAboveThreshold = 2f;

    [Header("Boss Health Bar")]
    [Tooltip("Big screen bar shown for this boss (assign BossHealthBar). Empty = no boss bar.")]
    public GameObject bossHealthBarPrefab;
    [Tooltip("Name shown on the boss bar.")]
    public string bossName = "The Moss Knight";

    [Header("Damage Reaction")]
    [Tooltip("A single hit above this plays the boss's injured/flinch animation (e.g. the Crusher's 80).")]
    public float hurtAnimThreshold = 50f;

    [Header("Death")]
    [Tooltip("Play the death celebration (freeze-frame, slow-mo, loot shower) when the boss dies. " +
             "Uncheck for a plain, instant despawn.")]
    public bool playDeathEffect = true;
    [Tooltip("One-shot played on death (2D, always audible). Optional.")]
    public AudioClip deathSound;
    [Range(0f, 2f)] public float deathVolume = 1.4f;
    [Tooltip("REAL collectible gold dropped on death — assign the 'Gold New' prefab (YeniLeveller). " +
             "Each piece gives its own gold amount when the player grabs it. Empty = no gold drops.")]
    public GameObject deathGoldPrefab;
    [Tooltip("REAL collectible shift crystals dropped on death — assign the ShiftCrystal prefab (Prefabs). " +
             "Empty = no crystal drops.")]
    public GameObject deathShiftCrystalPrefab;
    [Tooltip("How many gold pieces erupt and scatter on death.")]
    public int deathGoldCount = 14;
    [Tooltip("How many shift crystals erupt and scatter on death.")]
    public int deathCrystalCount = 5;

    [Header("Death — the spoils")]
    [Tooltip("Open the boss reward banner after the kill. Uncheck to drop loot only.")]
    public bool offerBossRelic = true;
    [Tooltip("How many boss relics the banner offers. Fewer than this are shown if fewer remain.")]
    [Range(1, 4)] public int bossRelicChoices = 2;
    [Tooltip("Seconds after the kill before the banner drops — long enough for the death " +
             "celebration to land, short enough that it reads as one moment.")]
    public float rewardDelay = 2.6f;

    [Header("Audio")]
    // All boss SFX play as 2D sound (always audible across the big arena); sliders go past 1 for headroom.
    [Tooltip("Roar on the awaken beat when the fight starts.")]
    public AudioClip roarSound;
    [Range(0f, 2f)] public float roarVolume = 1.4f;
    [Tooltip("Each ground-pound stomp during the awakening.")]
    public AudioClip poundSound;
    [Range(0f, 2f)] public float poundVolume = 1f;
    [Tooltip("Acid Cleave — the melee swing.")]
    public AudioClip cleaveSound;
    [Range(0f, 2f)] public float cleaveVolume = 1f;
    [Tooltip("Charge — the dash launch.")]
    public AudioClip chargeSound;
    [Range(0f, 2f)] public float chargeVolume = 1f;
    [Tooltip("Leap — the jump launch into the air.")]
    public AudioClip leapSound;
    [Range(0f, 2f)] public float leapVolume = 1f;
    [Tooltip("Leap Slam — the landing impact.")]
    public AudioClip slamSound;
    [Range(0f, 2f)] public float slamVolume = 1.3f;
    [Tooltip("Lob — spitting the acid blob / slime.")]
    public AudioClip lobSound;
    [Range(0f, 2f)] public float lobVolume = 1f;
    [Tooltip("Plays when the boss takes damage (on every landed hit).")]
    public AudioClip hurtSound;
    [Range(0f, 2f)] public float hurtVolume = 1f;

    [Header("Edge Detection")]
    [Tooltip("Stop at ledges. Requires Ground Layer to be set, or it is ignored.")]
    public bool avoidLedges = false;
    public LayerMask groundLayer;
    public float edgeCheckOffsetX = 0.6f;
    public float edgeCheckDepth = 1.2f;

    private MonsterController controller;
    private PixelMonster pm;
    private EnemyHealth health;
    private Transform player;
    private AudioSource bossSfx;        // 2D source built at runtime for all one-shot boss SFX
    private AudioSource chargeSource;   // separate 2D source for the charge, so it can be Stop()ped mid-dash
    private AnimationEventReceiver animEvents;   // Cainos monster event source; onAttack = the strike frame
    private System.Action pendingAttackHit;      // fired on the next onAttack event (set only during a cleave)

    private bool isActing;              // true while an attack coroutine drives the inputs
    private bool fightStarted;          // set by StartFight(); dormant (no AI, no music, no bar) until then
    private float cleaveReadyTime;
    private float chargeReadyTime;
    private float leapReadyTime;
    private float lobReadyTime;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        pm = GetComponent<PixelMonster>();
        health = GetComponent<EnemyHealth>();

        // 2D sources so boss SFX are always clearly audible regardless of distance across the arena.
        bossSfx = gameObject.AddComponent<AudioSource>();
        bossSfx.playOnAwake = false;
        bossSfx.spatialBlend = 0f;

        chargeSource = gameObject.AddComponent<AudioSource>();
        chargeSource.playOnAwake = false;
        chargeSource.spatialBlend = 0f;

        // The attack clip fires onAttack at its authored contact frame — drive cleave damage/SFX off it
        // so they stay in sync with the swing instead of a guessed delay.
        animEvents = GetComponentInChildren<AnimationEventReceiver>();
        if (animEvents != null) animEvents.onAttack.AddListener(OnAttackAnimHit);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // Pass THROUGH the player instead of physically shoving them — otherwise a charge
        // just bulldozes/drags the player along. Damage + knockback are still applied by our
        // own contact checks; only the solid-body collision is ignored (triggers untouched,
        // so the player's attacks and the crusher still register).
        if (player != null)
        {
            Collider2D[] bossCols = GetComponentsInChildren<Collider2D>();
            Collider2D[] playerCols = player.GetComponentsInChildren<Collider2D>();
            foreach (var bc in bossCols)
                foreach (var pc in playerCols)
                    if (bc != null && pc != null && !bc.isTrigger && !pc.isTrigger)
                        Physics2D.IgnoreCollision(bc, pc, true);
        }

        if (controller == null)
            Debug.LogWarning("MossKnightBoss: no MonsterController found — add this to the Moss Knight (Cainos) prefab.");

        // Flinch on big hits (Crusher, heavy cards).
        if (health != null) health.OnDamagedAmount += OnDamaged;
        if (health != null) health.OnDied += OnBossDied;

        // Dormant bosses wait for a BossFightTrigger (arena platform) to call StartFight().
        if (!startDormant) StartFight();
    }

    // Wakes the boss, so landing on the arena platform IS the fight starting. Called by
    // BossFightTrigger (or Start if not dormant). With the awaken effect on, the coil→roar
    // sequence plays and the bar/music land on the roar; otherwise the battle begins instantly.
    public void StartFight()
    {
        if (fightStarted) return;
        fightStarted = true;

        if (playAwakenEffect)
            StartCoroutine(AwakenRoutine());
        else
            BeginBattle();
    }

    // Health bar intro + boss music. Split out so the awaken sequence can land it on the roar beat.
    private void BeginBattle()
    {
        // Spawn the dedicated boss health bar and bind it to our EnemyHealth.
        if (bossHealthBarPrefab != null && health != null)
        {
            GameObject barGO = Instantiate(bossHealthBarPrefab);
            BossHealthBar bar = barGO.GetComponent<BossHealthBar>();
            if (bar != null) bar.Initialize(health, bossName);
        }

        // Boss music: start the boss theme now, and hand it back to the level track on death.
        if (MusicManager.instance != null) MusicManager.instance.PlayBossMusic();
    }

    // --- Awakening: coil (rumble) → roar (freeze-frame + shockwave + flash + shove) → battle. ---
    private IEnumerator AwakenRoutine()
    {
        if (controller == null) { BeginBattle(); yield break; }   // no rig to animate — just start

        isActing = true;                 // hold the AI off until the intro finishes
        ClearInputs();
        FaceTowardPlayer();

        // Cinematic: pan the camera onto the boss for the intro, roaring as it snaps to him.
        if (CameraFollow.instance != null) CameraFollow.instance.FocusOn(transform);
        PlayBossSfx(roarSound, roarVolume);

        // Beat 1 — ground pounds: a couple of hard stomps that shake the arena as he wakes.
        yield return GroundPound(awakenPounds);

        // Beat 1b — final coil: a short crouch + rising tremor for anticipation before the roar.
        if (pm != null) pm.IsInJumpPrepare = true;
        float t = 0f;
        float windup = Mathf.Max(0.1f, awakenWindup);
        while (t < windup)
        {
            float n = t / windup;
            if (CameraShake.instance != null)
                CameraShake.instance.Shake(0.08f, Mathf.Lerp(0.1f, 0.5f, n));   // escalating tremor
            t += Time.deltaTime;
            yield return null;
        }
        if (pm != null) pm.IsInJumpPrepare = false;

        // Beat 2 — ROAR: everything lands together. (The roar SFX already fired on the camera pan.)
        if (HitStop.instance != null) HitStop.instance.Stop(0.09f);             // freeze-frame punch
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.55f, 1.5f);

        controller.inputAttack = true;                                          // aggressive swing to announce
        StartCoroutine(ScreenFlashRoutine(awakenFlashColor, 0.35f));            // green screen flash

        // Green acid shockwave rings, scaled up for a boss-sized pulse (reuses the slam VFX).
        if (slamShockwaveEffect != null)
        {
            GameObject ring = Instantiate(slamShockwaveEffect, transform.position, Quaternion.identity);
            ring.transform.localScale *= Mathf.Max(0.1f, awakenShockwaveScale);
        }
        SpawnDebrisBurst(transform.position, 14, 1.4f);                          // gel eruption

        // A gentle, clearly non-damaging shove so the roar has physical weight.
        if (awakenPushback > 0.01f && player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                Vector2 away = (player.position - transform.position);
                away.x = Mathf.Approximately(away.x, 0f) ? 0f : Mathf.Sign(away.x);
                Vector2 shove = new Vector2(away.x * 0.7f, 1f).normalized * awakenPushback;
                pc.ApplyKnockback(shove);
            }
        }

        yield return new WaitForSecondsRealtime(0.02f);
        controller.inputAttack = false;

        // The bar sweeps in and the music drops on the roar, and the camera eases back to the player.
        BeginBattle();
        if (CameraFollow.instance != null) CameraFollow.instance.ReleaseFocus();

        // Beat 3 — brief settle (lets the camera finish panning back), then the AI takes over.
        yield return new WaitForSeconds(0.4f);
        isActing = false;
    }

    // A couple of hard downward stomps: quick crouch → snap up → slam back to the floor with an
    // impact (shake + gel burst + ground ring). Drives the Rigidbody directly (same proven pattern
    // as the Leap), so the controller doesn't brake the hop; control is always restored in finally.
    private IEnumerator GroundPound(int count)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null || pm == null || controller == null) yield break;

        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            // Quick crouch tell.
            pm.IsInJumpPrepare = true;
            yield return new WaitForSeconds(0.1f);
            pm.IsInJumpPrepare = false;

            float startY = transform.position.y;
            float savedGrav = rb.gravityScale;
            rb.gravityScale = savedGrav * 2.5f;     // heavy, so the slam is fast and decisive
            controller.enabled = false;             // take manual control of the hop
            try
            {
                rb.linearVelocity = new Vector2(0f, poundHopVelocity);
                float t = 0f;
                while (t < 1.2f)
                {
                    pm.IsGrounded = false;
                    pm.SpeedVertical = rb.linearVelocity.y;
                    bool descending = rb.linearVelocity.y <= 0.1f;
                    bool atFloor = transform.position.y <= startY + 0.1f;
                    if (descending && t > 0.08f && atFloor) break;   // landed
                    t += Time.deltaTime;
                    yield return null;
                }
                rb.linearVelocity = Vector2.zero;
                pm.IsGrounded = true;
                pm.SpeedVertical = 0f;
            }
            finally
            {
                rb.gravityScale = savedGrav;
                controller.enabled = true;
            }

            // Impact juice.
            PlayBossSfx(poundSound, poundVolume);
            if (HitStop.instance != null) HitStop.instance.Stop(0.05f);
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.28f, 0.8f);
            SpawnDebrisBurst(transform.position, 6, 1.1f);
            if (slamShockwaveEffect != null)
                Instantiate(slamShockwaveEffect, transform.position, Quaternion.identity);

            yield return new WaitForSeconds(0.1f);
        }
    }

    // Full-screen colour flash that fades out. Self-contained ScreenSpaceOverlay canvas (house style),
    // unscaled so it plays through the roar's freeze-frame; destroys itself when done.
    private IEnumerator ScreenFlashRoutine(Color color, float duration)
    {
        GameObject canvasGO = new GameObject("BossAwakenFlash");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;                    // above HUD, below nothing important

        GameObject imgGO = new GameObject("Flash");
        imgGO.transform.SetParent(canvasGO.transform, false);
        UnityEngine.UI.Image img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        float startA = color.a;
        float t = 0f;
        while (t < duration)
        {
            float n = 1f - (t / duration);
            Color c = color; c.a = startA * n * n;     // quick, punchy fade
            img.color = c;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(canvasGO);
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamagedAmount -= OnDamaged;
            health.OnDied -= OnBossDied;
        }
        if (animEvents != null) animEvents.onAttack.RemoveListener(OnAttackAnimHit);
    }

    // Attack animation reached its contact frame. Only a cleave arms pendingAttackHit, so lob/charge/
    // awaken swings (which reuse the same Attack anim) harmlessly no-op here.
    private void OnAttackAnimHit()
    {
        System.Action cb = pendingAttackHit;
        pendingAttackHit = null;
        cb?.Invoke();
    }

    // Return the soundtrack to the level's music once the boss is defeated, and fire the death
    // celebration. EnemyHealth destroys this GameObject the same frame it calls us, so the effect
    // must run on its OWN self-destroying object (BossDeathVFX) rather than a coroutine here.
    private void OnBossDied()
    {
        if (MusicManager.instance != null) MusicManager.instance.StopBossMusic();

        if (playDeathEffect)
        {
            // Resolve the loot's resting height: the real floor beneath the boss if there is one,
            // otherwise the boss stays "airborne" and the loot floats in mid-air where it died.
            bool airborne;
            float groundY = ResolveDeathGroundY(out airborne);

            Vector3 center = transform.position + Vector3.up * 0.9f;   // mid-body burst origin
            GameObject go = new GameObject("BossDeathVFX");
            go.transform.position = center;
            go.AddComponent<BossDeathVFX>().Play(groundY, airborne,
                                                 deathGoldPrefab, deathShiftCrystalPrefab,
                                                 deathSound, deathVolume,
                                                 deathGoldCount, deathCrystalCount);
        }

        if (offerBossRelic) BossRewardCue.Schedule(bossRelicChoices, rewardDelay);
    }

    // Casts down from the boss to find the floor loot should land on. Returns that floor's Y and sets
    // airborne=true when there's no ground close below (a mid-air death → the loot hovers instead).
    private float ResolveDeathGroundY(out bool airborne)
    {
        Vector3 feet = transform.position;
        airborne = true;
        float groundY = feet.y;

        RaycastHit2D[] hits = Physics2D.RaycastAll(feet + Vector3.up * 0.3f, Vector2.down, 2.2f);
        float nearest = float.MaxValue;
        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.CompareTag("Player")) continue;   // don't land loot on the player
            if (h.distance < nearest)
            {
                nearest = h.distance;
                groundY = h.point.y;
                airborne = false;
            }
        }
        return groundY;
    }

    // Plays the injured/flinch animation when a single hit is heavy enough. Front vs back is chosen
    // from where the player stands relative to the boss's facing — a crusher hit just uses the default.
    private void OnDamaged(float amount)
    {
        if (controller != null && controller.IsDead) return;   // no hurt reaction on/after death

        PlayBossSfx(hurtSound, hurtVolume);                    // every landed hit

        if (amount <= hurtAnimThreshold) return;               // flinch animation only on heavy hits
        if (pm == null || controller == null) return;

        bool fromFront = true;
        if (player != null)
        {
            bool playerOnRight = player.position.x >= transform.position.x;
            bool facingRight = pm.Facing == PixelMonster.FacingType.Right;
            fromFront = (playerOnRight == facingRight);
        }

        if (fromFront) pm.InjuredFront();
        else pm.InjuredBack();
    }

    void Update()
    {
        if (controller == null) return;

        // While an attack coroutine is running it owns the inputs — don't touch them.
        if (isActing) return;

        ClearInputs();

        if (!fightStarted) return;   // dormant — waiting for the arena trigger
        if (player == null || controller.IsDead) return;
        if (health != null && health.IsStunned) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > aggroRange) return;

        // Choose an action by range + cooldown; otherwise close the distance.
        bool canCleave = dist <= cleaveRange && Time.time >= cleaveReadyTime;
        bool canCharge = dist >= chargeMinRange && dist <= chargeMaxRange && Time.time >= chargeReadyTime;
        bool canLeap = dist >= leapMinRange && Time.time >= leapReadyTime;
        bool playerAbove = player.position.y > transform.position.y + lobAboveThreshold;
        // Lob reaches a camped-above player at any range, or pokes a far player on the floor.
        bool canLob = lobProjectile != null && Time.time >= lobReadyTime && dist <= lobMaxRange
                      && (playerAbove || dist >= lobMinRange);

        if (canCleave)
            StartCoroutine(CleaveRoutine());
        else if (canLob && playerAbove)            // contest platforms first — lob a slime up onto the perch
            StartCoroutine(LobRoutine(true));
        else if (canCharge)
            StartCoroutine(ChargeRoutine());
        else if (canLeap)
            StartCoroutine(LeapSlamRoutine());
        else if (canLob)                            // otherwise an acid poke while leap/charge cool
            StartCoroutine(LobRoutine(false));
        else
            Pursue(dist);
    }

    private void ClearInputs()
    {
        controller.inputMove = Vector2.zero;
        controller.inputAttack = false;
        controller.inputMoveModifier = false;
    }

    private void Pursue(float dist)
    {
        if (dist <= moveStopRange) return;
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        bool blockedByLedge = avoidLedges && groundLayer.value != 0 && IsLedgeAhead(dir);
        if (!blockedByLedge)
            controller.inputMove = new Vector2(dir, 0f);
    }

    // --- Acid Cleave ---
    private IEnumerator CleaveRoutine()
    {
        isActing = true;
        cleaveReadyTime = Time.time + cleaveCooldown;

        ClearInputs();
        FaceTowardPlayer();

        // Damage + sound land on the animation's OnAttack contact frame (in sync with the visible
        // swing), not a guessed delay. A timeout still forces the hit if the event never fires.
        bool hitDone = false;
        System.Action doHit = () =>
        {
            if (hitDone) return;
            hitDone = true;
            PlayBossSfx(cleaveSound, cleaveVolume);
            TryMeleeHit(cleaveRange + 0.5f, cleaveDamage, cleaveKnockback);
        };
        pendingAttackHit = doHit;

        controller.inputAttack = true;       // trigger the Attack anim
        yield return null;                    // hold it a frame so the controller reads it
        controller.inputAttack = false;

        // Proceed the instant the strike frame fires; otherwise fall back after a safety window.
        float timeout = Mathf.Max(0.15f, cleaveDamageDelay) + 0.4f;
        float waited = 0f;
        while (!hitDone && waited < timeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        if (pendingAttackHit == doHit) pendingAttackHit = null;   // don't let a stale callback linger
        doHit();                                                  // no-op if the event already hit

        yield return new WaitForSeconds(cleaveRecover);
        isActing = false;
    }

    // --- Charge ---
    private IEnumerator ChargeRoutine()
    {
        isActing = true;
        chargeReadyTime = Time.time + chargeCooldown;

        // Telegraph: face the player and wind up in place.
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        ClearInputs();
        FaceDirection(dir);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.1f, 0.15f);
        yield return new WaitForSeconds(chargeTelegraph);

        // Commit the direction at the moment of launch (re-aim to where the player is now),
        // then it's locked — dodge by getting to his other side.
        if (player != null) dir = player.position.x > transform.position.x ? 1f : -1f;
        FaceDirection(dir);
        // Sustained dash sound on its own source so it can be cut the moment the charge ends.
        if (chargeSound != null)
        {
            chargeSource.clip = chargeSound;
            chargeSource.volume = chargeVolume * SfxManager.Volume;
            chargeSource.Play();
        }

        // Temporarily override the controller's run cap/accel so this is a real dash, not a jog.
        float savedMax = controller.runSpeedMax;
        float savedAcc = controller.runAcc;
        controller.runSpeedMax = chargeSpeed;
        controller.runAcc = chargeAccel;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        bool hitPlayer = false;
        float t = 0f;
        while (t < chargeDuration)
        {
            if (controller.IsDead) break;
            if (health != null && health.IsStunned) break;   // Glass Wail interrupts the charge

            controller.inputMove = new Vector2(dir, 0f);
            controller.inputMoveModifier = true;             // run
            controller.inputAttack = false;

            if (!hitPlayer && player != null &&
                Vector2.Distance(transform.position, player.position) <= chargeHitRange)
            {
                hitPlayer = true;
                controller.inputAttack = true;               // slash on impact
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(chargeDamage);
                    pc.ApplyKnockback(new Vector2(dir, 0.35f).normalized * chargeKnockback);
                }
            }

            // Stop once he's run past the player (with overshoot) or run into a wall.
            bool passedPlayer = player != null &&
                ((dir > 0f && transform.position.x > player.position.x + chargeOvershoot) ||
                 (dir < 0f && transform.position.x < player.position.x - chargeOvershoot));
            bool hitWall = t > 0.15f && rb != null && Mathf.Abs(rb.linearVelocity.x) < 0.5f;
            if (passedPlayer || hitWall) break;

            t += Time.deltaTime;
            yield return null;
        }

        // Restore normal movement, then the vulnerable recovery window.
        controller.runSpeedMax = savedMax;
        controller.runAcc = savedAcc;
        if (chargeSource != null) chargeSource.Stop();   // cut the dash sound the instant he stops
        ClearInputs();
        yield return new WaitForSeconds(chargeRecover);
        isActing = false;
    }

    // --- Leap Slam ---
    private IEnumerator LeapSlamRoutine()
    {
        isActing = true;
        leapReadyTime = Time.time + leapCooldown;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { isActing = false; yield break; }

        // Telegraph: crouch (jump prepare), face the player.
        ClearInputs();
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        FaceDirection(dir);
        if (pm != null) pm.IsInJumpPrepare = true;
        yield return new WaitForSeconds(leapTelegraph);
        if (pm != null) pm.IsInJumpPrepare = false;

        // Aim: leap toward the player's X (clamped), landing back at floor height.
        float startY = transform.position.y;
        float targetX = player != null ? player.position.x : transform.position.x + dir * maxLeapDistance;
        float dx = Mathf.Clamp(targetX - transform.position.x, -maxLeapDistance, maxLeapDistance);
        if (Mathf.Abs(dx) > 0.01f) dir = Mathf.Sign(dx);
        FaceDirection(dir);

        // Boost gravity for the leap so the arc is snappy and weighty instead of floaty.
        float savedGrav = rb.gravityScale;
        rb.gravityScale = savedGrav * Mathf.Max(1f, leapGravityMul);

        // Parabolic launch velocity from the (boosted) gravity — apex height is preserved.
        float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.1f, rb.gravityScale);
        float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.5f, leapApexHeight));
        float airTime = 2f * vy / g;
        float vx = dx / Mathf.Max(0.05f, airTime);

        // Take manual control of the arc (the controller would brake the horizontal velocity).
        controller.enabled = false;
        PlayBossSfx(leapSound, leapVolume);   // launch grunt/whoosh
        try
        {
            rb.linearVelocity = new Vector2(vx, vy);

            float t = 0f;
            while (t < leapMaxAirTime)
            {
                if (pm != null) { pm.IsGrounded = false; pm.SpeedVertical = rb.linearVelocity.y; }

                // Extra downward pull on the way down for a weighty, decisive slam.
                if (rb.linearVelocity.y < 0f)
                    rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (leapFallMul - 1f) * rb.gravityScale * Time.deltaTime);

                bool descending = rb.linearVelocity.y <= 0.1f;
                bool atFloor = transform.position.y <= startY + 0.15f;
                bool stoppedVert = Mathf.Abs(rb.linearVelocity.y) < 0.05f;   // landed on ground/platform
                if (descending && t > 0.2f && (atFloor || stoppedVert)) break;

                t += Time.deltaTime;
                yield return null;
            }

            rb.linearVelocity = Vector2.zero;
            if (pm != null) { pm.IsGrounded = true; pm.SpeedVertical = 0f; }
        }
        finally
        {
            rb.gravityScale = savedGrav;   // restore normal gravity
            controller.enabled = true;      // always hand control back, even if interrupted
        }

        DoSlamImpact();

        yield return new WaitForSeconds(leapRecover);
        isActing = false;
    }

    private void DoSlamImpact()
    {
        if (slamShockwaveEffect != null)
            Instantiate(slamShockwaveEffect, transform.position, Quaternion.identity);

        PlayBossSfx(slamSound, slamVolume);

        // Punch: a brief freeze-frame + a heavy shake + a gel splatter burst.
        if (HitStop.instance != null) HitStop.instance.Stop(0.07f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.32f, 1.1f);
        SpawnSlamDebris(transform.position);

        if (player != null && Vector2.Distance(transform.position, player.position) <= slamRadius)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(slamDamage);
                Vector2 kb = player.position - transform.position;
                kb.y = Mathf.Abs(kb.y) + 1f;      // bias the knockback upward
                pc.ApplyKnockback(kb.normalized * slamKnockback);
            }
        }
        // TODO (acid system): spread a temporary acid pool at the landing point.
    }

    // A short-lived burst of gel chunks flung up and out from the impact — cheap, contained juice.
    private void SpawnSlamDebris(Vector2 origin) => SpawnDebrisBurst(origin, 7, 1f);

    // Parameterized so the awaken can throw a bigger, faster eruption than a slam.
    private void SpawnDebrisBurst(Vector2 origin, int count, float speedMul)
    {
        if (slamDebrisSprite == null) return;
        StartCoroutine(DebrisRoutine(origin, count, speedMul));
    }

    private IEnumerator DebrisRoutine(Vector2 origin, int count, float speedMul)
    {
        count = Mathf.Max(1, count);
        var gos = new GameObject[count];
        var srs = new SpriteRenderer[count];
        var vel = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("SlamDebris");
            go.transform.position = origin + Vector2.up * 0.2f;
            go.transform.localScale = Vector3.one * Random.Range(0.18f, 0.34f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = slamDebrisSprite;
            sr.color = new Color(0.55f, 0.8f, 0.32f, 1f);
            sr.sortingOrder = 12;
            gos[i] = go; srs[i] = sr;
            float ang = Mathf.Deg2Rad * Random.Range(35f, 145f);   // upward-biased fan
            vel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(4f, 9f) * speedMul;
        }

        float t = 0f; const float life = 0.6f;
        while (t < life)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                vel[i] += Vector2.up * (Physics2D.gravity.y * 1.2f * dt);   // arc back down
                gos[i].transform.position += (Vector3)(vel[i] * dt);
                gos[i].transform.Rotate(0f, 0f, 300f * dt);
                Color c = srs[i].color; c.a = 1f - t / life; srs[i].color = c;
            }
            t += dt;
            yield return null;
        }
        for (int i = 0; i < count; i++) if (gos[i] != null) Destroy(gos[i]);
    }

    // --- Lob (Acid Blob / Slime) ---
    // Reuses the single Attack anim as a throw gesture and arcs a payload onto the player's spot.
    // When the player is camped above (on a platform), it lobs a Slime add up to contest the perch;
    // otherwise it lobs an acid blob that bursts into a lingering puddle (see AcidBlobProjectile).
    // Either way, the platforms stop being a free refuge.
    private IEnumerator LobRoutine(bool aimHigh)
    {
        isActing = true;
        lobReadyTime = Time.time + lobCooldown;

        ClearInputs();
        FaceTowardPlayer();

        // Throw gesture (the Attack anim doubles as the overhead lob).
        PlayBossSfx(lobSound, lobVolume);
        controller.inputAttack = true;
        yield return null;
        controller.inputAttack = false;

        yield return new WaitForSeconds(lobTelegraph);   // wind-up doubles as the release cue

        if (lobProjectile != null && player != null)
        {
            // Lob a slime onto a camped platform; otherwise an acid blob.
            bool throwSlime = aimHigh && slimeEnemy != null;

            Vector2 origin = (Vector2)transform.position + Vector2.up * 1.2f;   // from the hands
            Vector2 landing = player.position;                                  // where they are now (dodgeable)
            GameObject blob = Instantiate(lobProjectile, origin, Quaternion.identity);
            AcidBlobProjectile proj = blob.GetComponent<AcidBlobProjectile>();
            if (proj != null)
            {
                proj.createPuddle = !throwSlime;
                proj.landPayload = throwSlime ? slimeEnemy : null;
                proj.Launch(origin, landing, lobArcHeight, lobTravelTime);
            }
        }

        yield return new WaitForSeconds(lobRecover);
        isActing = false;
    }

    // Sets facing without moving, via the PixelMonster component (MonsterController only
    // overrides facing while there is movement input, so this sticks during a wind-up).
    private void FaceDirection(float dir)
    {
        if (pm != null)
            pm.Facing = dir > 0f ? PixelMonster.FacingType.Right : PixelMonster.FacingType.Left;
    }

    private void FaceTowardPlayer()
    {
        if (player == null) return;
        FaceDirection(player.position.x > transform.position.x ? 1f : -1f);
    }

    // All boss SFX route through here: 2D, global-volume-aware, null-safe.
    private void PlayBossSfx(AudioClip clip, float volume) => SfxManager.PlayOn(bossSfx, clip, volume);

    private void TryMeleeHit(float range, float damage, float knockback)
    {
        if (player == null) return;
        if (Vector2.Distance(transform.position, player.position) > range) return;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TakeDamage(damage);
            Vector2 dir = (player.position - transform.position).normalized;
            pc.ApplyKnockback(dir * knockback);
        }
    }

    private bool IsLedgeAhead(float dirX)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(dirX * edgeCheckOffsetX, -0.1f);
        return Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDepth, groundLayer).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeMaxRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, cleaveRange);
    }
}
