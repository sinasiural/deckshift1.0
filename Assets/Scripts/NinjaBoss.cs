using System.Collections;
using UnityEngine;

/// <summary>
/// The Ninja — a floor boss, and the Ninja character's mirror finale. Design doc: BossDesign_Ninja.md
///
/// GREYBOX SCOPE (2026-08-22): dormant start, the shuriken volley, the recall, and the airborne Shift
/// drop. The dash slash and the katana flight are NOT built yet — see the doc §6.
///
/// ⚠️ HE ANIMATES LIKE THE PLAYER, NOT LIKE A MONSTER. He is built on the Cainos *Customizable Pixel
/// Character* rig (`AC Character.controller`), the same one PlayerController drives — not the monster
/// pack the Moss Knight uses. So the animator handles here are `AttackAction` (INT), `IsAttacking`
/// and `AttackSpeedMul`, and facing is a localScale flip on the visual child.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class NinjaBoss : MonoBehaviour, IBossFight
{
    [Header("Fight Start")]
    [Tooltip("Stay an idle statue until a BossAwakenTrigger calls StartFight().")]
    public bool startDormant = true;

    [Header("Identity")]
    public string bossName = "The Ninja";
    [Tooltip("Big screen bar for this boss (assign BossHealthBar). Empty = no boss bar.")]
    public GameObject bossHealthBarPrefab;

    [Header("Rig")]
    [Tooltip("The visual child that gets flipped for facing. Empty = the first child.")]
    public Transform visualModel;
    [Tooltip("The pack's shuriken sprite, so the stars he throws match the ones the player picks up. " +
             "Empty falls back to the shared procedural star.")]
    public Sprite shurikenSprite;
    [Tooltip("Weapon he holds. Assign PF Weapon - Katana — it is the silhouette separating him from " +
             "the playable Ninja, who holds a shuriken.")]
    public GameObject weaponPrefab;

    [Header("Movement Between Attacks (TUNE BY EYE)")]
    [Tooltip("Gap between attacks. ⚠️ He does not STAND during it — he repositions. It used to be a " +
             "flat 4.5s WaitForSeconds after every attack, which is what made him feel like a statue " +
             "that occasionally lunged.")]
    public float betweenAttacks = 1.1f;
    public float repositionSpeed = 7.5f;
    [Tooltip("Distance he tries to hold from the player while circling.")]
    public float preferredRange = 8f;
    [Tooltip("Random spread on that distance, so he does not settle into the same spot every time.")]
    public float repositionJitter = 3.5f;
    [Tooltip("⚠️ SECONDS BETWEEN GUARANTEED VOLLEYS — the single most important number in the fight. " +
             "It is not flavour: the stars are the player's ammo, so this is how fast the arena " +
             "resupplies them. Lower it and a card-less player can win; raise it too far and they " +
             "cannot. It is a floor, not a cap — the volley still also fires as the fallback when " +
             "nothing else applies.")]
    public float volleyInterval = 5.5f;
    public int starsMin = 4;
    public int starsMax = 6;
    [Tooltip("Total spread of the fan, in degrees, centred on the player.")]
    public float spreadDegrees = 26f;
    [Tooltip("Damage per star. Matches the player's Shuriken so a star thrown back is the same object.")]
    public float starDamage = 8f;
    [Tooltip("Seconds between stars within one volley.")]
    public float starInterval = 0.09f;

    [Header("Recall")]
    [Tooltip("Seconds after a volley before uncollected stars tear back to him. THIS IS THE CLOCK " +
             "THE FIGHT RUNS ON — it is what stops the player ignoring the boss and farming the floor.")]
    public float recallDelay = 8f;

    [Header("Dash Slash")]
    [Tooltip("Within this horizontal distance he dashes instead of throwing. Beyond it, he throws.")]
    public float dashRange = 15f;
    // ⚠️ ALL OF THE ANTICIPATION VALUES BELOW ARE MEANT TO BE TUNED BY EYE, IN PLAY MODE, BY THE
    // DESIGNER. They are feel, not logic — nothing here can be judged by measuring it.
    public enum Anticipation { Crouch, DodgeBack, None }

    [Header("Dash Slash — anticipation (TUNE BY EYE)")]
    [Tooltip("Crouch = the pack's authored crouch, he drops low and coils. DodgeBack = the authored " +
             "back-dodge. Swap freely and compare; neither needs new art.")]
    public Anticipation anticipation = Anticipation.Crouch;
    [Tooltip("How long he coils before the launch. The LANE is telegraphed for this whole time.")]
    public float coilTime = 0.40f;
    [Tooltip("⚠️ A beat of COMPLETE STILLNESS at full coil, after the drift stops and before he goes. " +
             "The gate rebuild found this does more work than any other single value: weight and " +
             "intent are read from the PAUSE BEFORE a movement, not from the movement.")]
    public float holdTime = 0.14f;
    [Tooltip("How far he drifts BACKWARD while coiling — the wind-up of the spring. Classic " +
             "anticipation: he moves away from where he is about to go.")]
    public float leanBack = 0.5f;
    [Tooltip("Only used by DodgeBack. The pack's DodgeDir convention is unverified, so if the dodge " +
             "plays facing the wrong way, try -1.")]
    public int dodgeDirValue = 1;
    public float dashSpeed = 26f;
    [Tooltip("How far PAST the player he commits to travelling. This is the overshoot the player " +
             "baits — small enough that he ends up beside them, not on the far side of the arena.")]
    public float dashOvershoot = 4.5f;
    [Tooltip("Hard ceiling on a single dash. He stops early at a wall, or once the overshoot is spent.")]
    public float dashMaxDistance = 18f;
    public float dashDamage = 14f;
    public float dashKnockback = 7f;
    [Tooltip("Depth of the damage box in front of him while dashing.")]
    public float dashReach = 1.5f;
    [Tooltip("Height of that box. ~2 covers a standing player; a jump clears it.")]
    public float dashHeight = 2.0f;
    [Tooltip("Recovery after a clean dash. THIS IS THE PLAYER'S DAMAGE WINDOW.")]
    public float dashRecovery = 0.65f;
    [Tooltip("Recovery after burying the katana in a wall. Longer on purpose — overshooting is the " +
             "mistake the player baits him into, so it has to pay better than a clean dash.")]
    public float dashWallRecovery = 1.35f;
    public Color laneColor = new Color(0.95f, 0.25f, 0.30f, 1f);
    [Tooltip("The faint wide band showing the VOLUME the strike sweeps — how high it reaches. Kept " +
             "low: this is context, not the warning.")]
    [Range(0f, 0.6f)] public float bandAlpha = 0.11f;
    [Tooltip("The cut line's alpha when the telegraph appears. ⚠️ It must be READABLE THE INSTANT " +
             "IT APPEARS — that is the warning. Measured on screen, never reasoned about.")]
    [Range(0f, 1f)] public float cutAlphaStart = 0.55f;
    [Tooltip("What the cut line reaches as he commits.")]
    [Range(0f, 1f)] public float cutAlphaEnd = 0.95f;
    [Tooltip("Thickness of the cut line itself, in world units. It is the EDGE's path — thin.")]
    public float cutThickness = 0.11f;
    [Tooltip("A premonition of him standing where the dash ENDS, resolving as he loads. Blue ghosts " +
             "are where he WAS; this red one is where he is about to be.")]
    public bool showPremonition = true;
    public Color premonitionTint = new Color(1f, 0.32f, 0.30f, 0.42f);

    [Header("Dash Slash — afterimages (TUNE BY EYE)")]
    [Tooltip("Frozen copies of him left along the dash. This is where the ninja reads from — the " +
             "trail says 'moved too fast to be one person'.")]
    public bool leaveGhosts = true;
    [Tooltip("Seconds between ghosts. Smaller = denser trail.")]
    public float ghostInterval = 0.035f;
    [Tooltip("How long each ghost lingers.")]
    public float ghostLife = 0.24f;
    public Color ghostTint = new Color(0.55f, 0.70f, 1f, 0.55f);
    [Tooltip("A ghost left at the launch point too, so the dash reads as leaving something behind.")]
    public bool ghostOnLaunch = true;
    public AudioClip dashSound;
    [Range(0f, 2f)] public float dashVolume = 1f;

    [Header("Katana Flight (TUNE BY EYE)")]
    [Tooltip("Seconds between katana flights. It is his signature, not his filler.")]
    public float katanaCooldown = 9f;
    public float leapForce = 15f;
    [Tooltip("Horizontal drift toward the player during the leap.")]
    public float leapDrift = 2.5f;
    [Tooltip("⚠️ HANGTIME AT THE APEX. He stops dead in the air to throw. This beat is most of why " +
             "the move reads as deliberate rather than as a hop — try 0 and watch it fall apart.")]
    public float hangTime = 0.55f;
    [Tooltip("How long the LANDED blade just sits there before he blinks. This is the telegraph: it " +
             "is the window the player has to read where he is about to be.")]
    public float katanaWatchTime = 0.45f;
    public float katanaDamage = 12f;
    [Tooltip("Beat after he arrives, before he comes at you.")]
    public float blinkPause = 0.14f;
    [Tooltip("Blade sprite. Left empty it is pulled from weaponPrefab at Awake.")]
    public Sprite katanaSprite;

    [Header("Wall-Run Dive (TUNE BY EYE)")]
    [Tooltip("Seconds between wall dives.")]
    public float wallDiveCooldown = 11f;
    [Tooltip("Speed he crosses to the wall at.")]
    public float wallApproachSpeed = 15f;
    public float wallRunSpeed = 13f;
    [Tooltip("How far UP the wall he runs before clinging.")]
    public float wallRunHeight = 7f;
    [Tooltip("⚠️ THE TELEGRAPH. He hangs on the wall, perfectly still, aiming. This beat is the " +
             "player's entire warning — and it is also a long airborne window, so it is their best " +
             "chance to shoot him for Shift.")]
    public float clingTime = 0.6f;
    public float diveSpeed = 30f;
    public float diveDamage = 16f;
    [Tooltip("Radius of the body-slam hit during the dive. A diagonal dive is a body, not a swing, " +
             "so it uses a circle rather than EnemyMelee's forward box.")]
    public float diveRadius = 1.1f;
    public float diveRecovery = 0.85f;

    // ---- death ------------------------------------------------------------------------------------
    // ⚠️ HE PAID NOTHING FOR KILLING HIM. Until now `OnBossDied` opened the exit and that was all —
    // no loot, no celebration, no relic, and the boss music (which he never started either) kept
    // playing over a corpse. A floor boss is the biggest fight on the map and it was the only enemy in
    // the game whose death was quieter than a zombie's.
    //
    // Reuses `BossDeathVFX` and `BossRewardCue` verbatim from the Moss Knight. Both were written
    // generic and neither needed changing — except that the burst colour was hardcoded acid green,
    // which is the Moss Knight's whole identity, so it is now a parameter.

    [Header("Death — the celebration")]
    public bool playDeathEffect = true;
    public AudioClip deathSound;
    [Range(0f, 2f)] public float deathVolume = 1.4f;
    [Tooltip("REAL collectible gold dropped on death — assign the 'Gold New' prefab. Empty = no gold.")]
    public GameObject deathGoldPrefab;
    [Tooltip("REAL collectible shift crystals dropped on death — assign ShiftCrystal. Empty = none.")]
    public GameObject deathShiftCrystalPrefab;
    public int deathGoldCount = 14;
    public int deathCrystalCount = 5;
    [Tooltip("⚠️ NOT the Moss Knight's acid green. His afterimages are cold blue, so that is what he " +
             "comes apart into — the same colour the fight taught you means 'where he was'.")]
    public Color deathBurstColor = new Color(0.55f, 0.72f, 1f);

    [Header("Death — the spoils")]
    public bool offerBossRelic = true;
    [Range(1, 4)] public int bossRelicChoices = 2;
    [Tooltip("Seconds after the kill before the reward banner drops — long enough for the celebration " +
             "to land, short enough that it reads as one moment.")]
    public float rewardDelay = 2.6f;

    [Header("Shift Payout")]
    [Tooltip("Hit him while he is AIRBORNE and he sheds one of these. Assign Prefabs/ShiftCrystal.")]
    public GameObject shiftCrystalPrefab;
    [Tooltip("Cap per airborne period. ⚠️ Per AIRTIME, not per hit — uncapped, one fast card during " +
             "a single leap prints a fistful. Same rule as Pogo Boots' _bouncedThisAirtime.")]
    public int crystalsPerAirtime = 2;
    public float groundCheckDistance = 0.25f;

    // ⚠️ NO GATE FIELDS HERE ANY MORE. The boss used to instantiate the pack's whole
    // `PF Dungeon Props - Door Iron Fence 01` over the exit — which brought its own stone frame, its
    // own shadow, a cyan sky panel, a light shaft, a Light2D and a dust emitter, and produced a
    // second, smaller, differently-lit archway sitting in front of the real one. The bars now belong
    // to `ExitDoor` and follow its lock; sealing the room is one call and nothing to dress.

    // ---- audio ----------------------------------------------------------------------------------
    // ⚠️ EVERY SLOT HERE IS AN OVERRIDE, NOT A REQUIREMENT. All eight of his sounds are procedural
    // (`ProcSfx`, the NINJA family) and play whether or not anything is dragged in — because an empty
    // AudioClip field is a SILENT NO-OP, and 75 of those across the project are where the game's
    // silence actually lives. A boss must not be able to ship mute because someone forgot a slot.
    //
    // ⚠️ AND THE KATANA HAS ITS OWN THROW SOUND. It used to reuse `throwSound`, so hurling a
    // four-foot sword sounded exactly like flicking a star. The family separates them by flutter
    // rate — 62 Hz for the star, 11 Hz for the blade tumbling end over end — which is the whole
    // reason those two clips exist as a pair. Do not point them back at one clip.

    [Header("Audio (leave empty — procedural by default)")]
    public AudioClip throwSound;
    [Range(0f, 2f)] public float throwVolume = 0.9f;
    public AudioClip recallSound;
    [Range(0f, 2f)] public float recallVolume = 1f;
    public AudioClip katanaThrowSound;
    [Range(0f, 2f)] public float katanaThrowVolume = 1f;
    [Tooltip("The blade planting. ⚠️ This is the TELEGRAPH's voice — it rings on while the player " +
             "reads where he is about to be, so it is the longest clip he has.")]
    public AudioClip katanaPlantSound;
    [Range(0f, 2f)] public float katanaPlantVolume = 1f;
    public AudioClip blinkSound;
    [Range(0f, 2f)] public float blinkVolume = 1f;

    private AudioClip Star      => throwSound       != null ? throwSound       : ProcSfx.ShurikenThrow;
    private AudioClip Recall    => recallSound      != null ? recallSound      : ProcSfx.ShurikenRecall;
    private AudioClip Dash      => dashSound        != null ? dashSound        : ProcSfx.NinjaDash;
    private AudioClip Katana    => katanaThrowSound != null ? katanaThrowSound : ProcSfx.KatanaThrow;
    private AudioClip Plant     => katanaPlantSound != null ? katanaPlantSound : ProcSfx.KatanaPlant;
    private AudioClip Blink     => blinkSound       != null ? blinkSound       : ProcSfx.NinjaBlink;

    // ---- animator handles (see the class header) ------------------------------------------------
    private const int   THROW_ACTION  = 13;     // ⚠️ 13 is THROW. 14 is CAST — the wizard's spell pose.
    // Read out of AC Character's own transitions rather than guessed: 1=Swipe, 2=Stab, 11=Point,
    // 12=Summon, 13=Throw, 14=Cast. Swipe is the only one that plays on BOTH the Arm and Body
    // layers and reads as a committed slash.
    private const int   SWIPE_ACTION  = 1;
    private const float THROW_SPEED   = 2.2f;   // ⚠️ GLOBAL parameter. Must be put back to 1.
    private const float THROW_RELEASE = 0.13f;  // the star leaves on the arm's snap, not on the decision

    private EnemyHealth health;
    private Animator animator;
    private AudioSource sfx;
    private Transform player;
    private Collider2D body;
    private Rigidbody2D rb;

    private bool fightStarted;
    private bool facingRight = true;
    private float visualScaleX = 1f;

    private int crystalsThisAirtime;
    private bool wasGrounded = true;
    private float baseGravityScale = 1f;

    /// <summary>
    /// Put weight back. Called from every interruption path as well as the normal ones — a boss left
    /// weightless is the quietest possible failure: he does not error, he just never falls again.
    /// </summary>
    private void RestoreGravity()
    {
        if (rb != null) rb.gravityScale = baseGravityScale;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();

        // One Animator in the hierarchy, on a child — same shape as the player's rig.
        animator = GetComponentInChildren<Animator>();

        if (visualModel == null && transform.childCount > 0) visualModel = transform.GetChild(0);
        if (visualModel != null) visualScaleX = Mathf.Abs(visualModel.localScale.x);

        body = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // ⚠️ CAPTURED ONCE, FROM THE PREFAB, AND EVERY RESTORE USES THIS — never a value read at the
        // top of a coroutine. Three of his moves switch gravity off (dash, wall-run, katana hang), and
        // reading "the current value" to put back later is a trap the moment two of them can overlap
        // or one is interrupted: a move that begins while gravity is already 0 faithfully restores it
        // to 0, and the boss floats for the rest of the fight with nothing in the log.
        if (rb != null) baseGravityScale = rb.gravityScale;

        // 2D so his stars are audible across the arena regardless of distance, and so the volume
        // slider can exceed 1 for headroom. Same reasoning as the Moss Knight's ability SFX.
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        EquipWeapon();

        // The blade that flies is the blade he is holding — taken from the same prefab so the two
        // can never drift apart.
        if (katanaSprite == null && weaponPrefab != null)
        {
            var wsr = weaponPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (wsr != null) katanaSprite = wsr.sprite;
        }
    }

    // ⚠️ THE WEAPON GOES THROUGH THE PACK'S OWN `AddWeapon`, NEVER HAND-PARENTED.
    // `PixelCharacter.Weapon` is read-only, and AddWeapon is what syncs the weapon to the rig bone
    // and pushes the sorting layer and alpha onto the new renderers. Hand-parenting looks correct
    // standing still and then sorts or fades wrong the moment he moves. Done at RUNTIME rather than
    // baked into the prefab for the same reason CharacterAppearance does it: the pack's setup is
    // fragile at edit time and this is one call.
    private void EquipWeapon()
    {
        if (weaponPrefab == null || visualModel == null) return;

        var pixel = visualModel.GetComponent<Cainos.CustomizablePixelCharacter.PixelCharacter>();
        if (pixel == null) return;

        // Never allowed to brick the boss. A cosmetic step that throws inside Awake would leave
        // Unity disabling this MonoBehaviour — no AI, no volleys, a statue in an arena — which is
        // exactly the failure CharacterAppearance.Apply is wrapped against.
        try { pixel.AddWeapon(weaponPrefab, true); }
        catch (System.Exception e) { Debug.LogWarning($"[NinjaBoss] weapon setup failed: {e.Message}"); }
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

        // Sealed from the moment the room exists, not from the moment the fight starts — the trigger
        // is crossed long before the exit is reachable, but a door that seals in front of you is
        // worse than one that was always shut.
        SealExit();

        if (!startDormant) StartFight();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= OnDamaged;
            health.OnDied -= OnBossDied;
        }
        // ⚠️ Never leave global animator state hot. AttackSpeedMul, IsDashing and IsCrouching all
        // live on a controller the PLAYER also uses, so a boss destroyed mid-attack could otherwise
        // hand every character a double-speed swing or a latched dash.
        ClearAnimatorState();
        RestoreGravity();

        // ⚠️ FAIL TOWARD PASSABLE. However this boss leaves the world — killed, swept on a room
        // change, or destroyed by something nobody has written yet — the exit must not stay sealed.
        // An open door in a boss room costs nothing; a locked one ends the run.
        if (exit != null) exit.SetLocked(false);
    }

    public void StartFight()
    {
        if (fightStarted) return;
        fightStarted = true;

        // ⚠️ He was fighting to the LEVEL's music. `StopBossMusic` was already being called on his
        // death, stopping something that had never started — which reads as correct in a diff and is
        // silent in play.
        if (MusicManager.instance != null) MusicManager.instance.PlayBossMusic();

        if (bossHealthBarPrefab != null && health != null)
        {
            GameObject barGO = Instantiate(bossHealthBarPrefab);
            BossHealthBar bar = barGO.GetComponent<BossHealthBar>();
            if (bar != null) bar.Initialize(health, bossName);
        }

        StartCoroutine(FightLoop());
    }

    // ---- the loop -------------------------------------------------------------------------------
    // Chosen by range, on the MossKnightBoss pattern. Close and level with him → dash slash;
    // otherwise → volley. He has no walk cycle by design: a ninja who strolls across an arena reads
    // wrong, so the DASH is how he closes distance. That is why its range is generous.
    private IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(1f);   // a beat to read the room before the first attack

        while (health != null && health.CurrentHealth > 0f)
        {
            // ⚠️ RESOLVED LAZILY, NOT ONCE IN Start(). A reference captured at Start goes stale if the
            // player is ever respawned, and is simply NULL if anything drives this boss before Unity
            // has run Start on it — which silently degrades every attack to a default facing rather
            // than failing loudly. Cheap to re-check; expensive to debug.
            if (player == null && GameManager.instance != null && GameManager.instance.player != null)
                player = GameManager.instance.player.transform;
            if (player == null) { yield return null; continue; }

            float dx = Mathf.Abs(player.position.x - transform.position.x);
            float dy = Mathf.Abs(player.position.y - transform.position.y);

            // ⚠️ HEIGHT IS PART OF THE CHOICE. A dash at a player standing on a ledge is a guaranteed
            // whiff that also throws him across the room, and a boss that keeps doing it looks broken
            // rather than dangerous.
            bool level = dy < 2.5f;

            // ⚠️ THE WALL DIVE GETS FIRST REFUSAL WHEN THE PLAYER IS ABOVE HIM, because it is the
            // only answer he has to high ground. Without this branch the ledges stay a refuge no
            // matter what else is in the kit.
            bool playerAbove = player.position.y - transform.position.y > 2.5f;

            // ⚠️ THE VOLLEY GETS FIRST REFUSAL, ON A TIMER, AND IT USED TO BE THE LAST `else`.
            // Measured over 2000 attacks on flat ground: it fired **ZERO** times. He repositions to
            // `preferredRange` ± jitter — 4.5 to 11.5 units — and every one of those is inside
            // `dashRange` (15), so the dash branch accepted every single time and the fallback below
            // was unreachable. Stars only ever appeared when the player stood on a ledge (dy > 2.5)
            // or ran past 15 units, which is why the fight felt like it had no ammo in it.
            //
            // This is not a tuning number, it is the fight's ECONOMY. The whole premise is that HE
            // ARMS YOU (§4) — a player who entered with no damage cards has no other source — and the
            // recall is described in this file as the clock the fight runs on. A clock that depends
            // on losing a range check is not a clock. So the volley is now a GUARANTEED periodic
            // event and the rest of the kit fills the gaps between.
            if (Time.time >= nextVolleyTime)
            {
                nextVolleyTime = Time.time + volleyInterval;
                yield return StartCoroutine(VolleyRoutine());
            }
            else if (Time.time >= nextWallDiveTime && IsGrounded() && (playerAbove || dx > dashRange))
            {
                nextWallDiveTime = Time.time + wallDiveCooldown;
                yield return StartCoroutine(WallRunDiveRoutine());
            }
            // The katana flight is his signature and gets next refusal, on its own cooldown. Not used
            // at point-blank — leaping away to throw a sword back at someone standing next to you is
            // the one situation where it reads as him running away.
            else if (Time.time >= nextKatanaTime && dx > 4f && IsGrounded())
            {
                nextKatanaTime = Time.time + katanaCooldown;
                yield return StartCoroutine(KatanaFlightRoutine());
            }
            else if (dx <= dashRange && level && IsGrounded())
                yield return StartCoroutine(DashSlashRoutine());
            else
                yield return StartCoroutine(VolleyRoutine());

            yield return StartCoroutine(RepositionRoutine(betweenAttacks));
        }
    }

    /// <summary>
    /// What he does between attacks: MOVE. He has no idle standoff — a ninja who plants his feet and
    /// waits reads as a training dummy, which is exactly how the first build felt.
    ///
    /// He holds a preferred distance on whichever side of the player he is already on, with jitter so
    /// he never settles into the same spot twice.
    /// </summary>
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

            float dx = want - transform.position.x;
            float step = Mathf.Sign(dx);
            bool moving = Mathf.Abs(dx) > 0.4f && !WallAhead(step);

            if (moving)
            {
                rb.linearVelocity = new Vector2(step * repositionSpeed, rb.linearVelocity.y);
                FaceDirection(step);
            }
            else rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Drive the pack's locomotion blend so he actually RUNS rather than sliding in an idle
            // pose. MoveBlendX 3 is the run pose (0 idle / 1 walk / 3 run), same mapping the player uses.
            if (animator != null)
            {
                animator.SetBool("IsMoving", moving);
                animator.SetFloat("MoveBlendX", moving ? 3f : 0f);
                animator.SetFloat("MoveSpeedMul", 1f);
            }
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) { animator.SetBool("IsMoving", false); animator.SetFloat("MoveBlendX", 0f); }
        FaceTowardPlayer();
    }

    // ---- boss gate ------------------------------------------------------------------------------
    // ⚠️ THE EXIT WAS USABLE MID-FIGHT. Reported from play 2026-08-22: you could simply walk to the
    // door and leave, which makes the whole encounter optional in a room that is meant to be the
    // point. The exit is sealed while he lives.
    //
    // ⚠️ The bars are `Door Iron Fence 01`, which the levels doc records as REJECTED for the ordinary
    // exit — "bars read as blocked". That was a defect there and is precisely the meaning wanted
    // here. A rejected asset is rejected for a REASON, and the reason can invert.
    private ExitDoor exit;

    private void SealExit()
    {
        exit = FindExitInRoom();
        if (exit == null) return;
        exit.SetLocked(true);      // the door draws its own bars
    }

    private void OpenExit()
    {
        if (exit != null) exit.SetLocked(false);   // ...and raises them
    }

    private ExitDoor FindExitInRoom()
    {
        Transform root = transform;
        while (root.parent != null) root = root.parent;
        return root.GetComponentInChildren<ExitDoor>(true);
    }

    // ---- wall-run dive --------------------------------------------------------------------------
    // Run to a wall → climb it → cling and aim → dive diagonally at the player → land hard.
    //
    // ⚠️ THIS EXISTS TO DELETE THE REFUGE. Every other attack he has is grounded and horizontal —
    // the dash flatly refuses a target more than 2.5 units above him — so before this move the
    // ledges and the centre island were a free safe zone, which is the exact opposite of the arena's
    // stated thesis that no tier is correct for long. It is prioritised when the player is ABOVE him.
    private float nextWallDiveTime;
    private float nextVolleyTime;

    private IEnumerator WallRunDiveRoutine()
    {
        if (player == null && GameManager.instance != null && GameManager.instance.player != null)
            player = GameManager.instance.player.transform;
        if (player == null || rb == null) yield break;

        // Nearer wall wins. Found by raycast rather than by comparing against arena bounds, so this
        // works in any room without being told where the walls are.
        float leftDist = WallDistance(-1f);
        float rightDist = WallDistance(1f);
        float wallDir = leftDist <= rightDist ? -1f : 1f;
        if (Mathf.Min(leftDist, rightDist) > 30f) yield break;   // nothing to climb; pick another attack

        try
        {
            // ---- 1. CROSS TO THE WALL ----
            FaceDirection(wallDir);
            if (animator != null) animator.SetBool("IsDashing", true);
            float guard = 0f;
            while (!WallAhead(wallDir) && guard < 2.5f)
            {
                guard += Time.deltaTime;
                rb.linearVelocity = new Vector2(wallDir * wallApproachSpeed, rb.linearVelocity.y);
                if (leaveGhosts) MaybeGhost(ref guard, 0f);
                yield return null;
            }
            rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("IsDashing", false);

            // ---- 2. RUN UP IT ----
            // ⚠️ Borrowed animation, not authored: the pack has no wall-run, but Ladder Climb is
            // already a character pressed flat to a vertical surface. Same trick the Gecko Gloves
            // wall-slide uses — and there, freezing it with ClimbingSpeedMul = 0 turns the climb
            // into a grip. Both halves of that are used here.
            rb.gravityScale = 0f;
            if (animator != null)
            {
                animator.SetBool("IsClimbingLadder", true);
                animator.SetFloat("ClimbingSpeedMul", 1f);
            }

            float startY = transform.position.y;
            guard = 0f;
            while (transform.position.y - startY < wallRunHeight && guard < 2.5f && !CeilingAbove())
            {
                guard += Time.deltaTime;
                rb.linearVelocity = new Vector2(0f, wallRunSpeed);
                yield return null;
            }
            rb.linearVelocity = Vector2.zero;

            // ---- 3. CLING AND AIM ----
            if (animator != null) animator.SetFloat("ClimbingSpeedMul", 0f);   // freeze the climb into a grip
            FaceTowardPlayer();

            Vector2 aim = ((Vector2)player.position + Vector2.up * 0.8f - ChestPoint).normalized;
            float diveLen = MeasureRay(ChestPoint, aim, 26f);
            Vector2 diveEnd = ChestPoint + aim * diveLen;

            // The same telegraph the dash uses, on a diagonal — one language, one calibration, and no
            // second set of alphas to keep in sync. The premonition is skipped here: he is clinging
            // to a wall in a pose that would read as a second ninja stuck to the far side of the room.
            Telegraph tel = Telegraph.Build(this, ChestPoint, diveEnd, dashHeight * 0.9f, false);

            float t = 0f;
            while (t < clingTime)
            {
                t += Time.deltaTime;
                rb.linearVelocity = Vector2.zero;      // held against the wall, weightless
                if (tel != null)
                {
                    tel.Place(ChestPoint, diveEnd, dashHeight * 0.9f, this);
                    tel.SetIntensity(Mathf.Clamp01(t / clingTime), this);
                }
                yield return null;
            }
            if (tel != null) tel.Clear();

            // ---- 4. DIVE ----
            if (animator != null)
            {
                animator.SetBool("IsClimbingLadder", false);
                animator.SetFloat("ClimbingSpeedMul", 1f);
                animator.SetBool("IsDashing", true);
                animator.SetInteger("AttackAction", SWIPE_ACTION);
                animator.SetBool("IsAttacking", true);
            }
            SfxManager.PlayOn(sfx, Dash, dashVolume);
            GhostTrail.Snapshot(visualModel, ghostTint, ghostLife * 1.5f);

            SetPlayerCollision(false);
            bool struck = false;
            float ghostClock = 0f;
            Vector2 from = transform.position;
            guard = 0f;

            while (Vector2.Distance(transform.position, from) < diveLen && guard < 2f)
            {
                guard += Time.fixedDeltaTime;
                rb.linearVelocity = aim * diveSpeed;

                if (leaveGhosts)
                {
                    ghostClock += Time.fixedDeltaTime;
                    if (ghostClock >= ghostInterval) { ghostClock = 0f; GhostTrail.Snapshot(visualModel, ghostTint, ghostLife); }
                }

                // A body-slam, so a circle around him — not EnemyMelee's forward box, which only
                // understands ±X and would miss on a steep dive.
                if (!struck)
                {
                    var hit = Physics2D.OverlapCircle(ChestPoint, diveRadius, LayerMask.GetMask("Player"));
                    var pc = hit != null ? hit.GetComponentInParent<PlayerController>() : null;
                    if (pc != null) { pc.TakeDamage(diveDamage); struck = true; }
                }

                if (TerrainAhead(aim)) break;
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            // Every one of these is a latch that would break the boss if a StopCoroutine landed
            // mid-move: weightless forever, stuck to a wall, or intangible to the player.
            RestoreGravity();
            SetPlayerCollision(true);
            if (animator != null)
            {
                animator.SetBool("IsClimbingLadder", false);
                animator.SetFloat("ClimbingSpeedMul", 1f);
                animator.SetBool("IsDashing", false);
                animator.SetBool("IsAttacking", false);
            }
        }

        // ---- 5. LAND ----
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        Puff(ChestPoint - Vector2.up * 0.9f, 12, 1.3f, 1f);
        Puff(ChestPoint - Vector2.up * 0.9f, 12, 1.3f, -1f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.26f, 0.38f);
        if (HitStop.instance != null) HitStop.instance.Stop(0.05f);

        yield return new WaitForSeconds(diveRecovery);
    }

    private float WallDistance(float dir)
    {
        var hit = Physics2D.Raycast(ChestPoint, new Vector2(dir, 0f), 40f, LayerMask.GetMask("Ground"));
        return hit.collider != null ? hit.distance : float.MaxValue;
    }

    private bool CeilingAbove()
    {
        var cap = body as CapsuleCollider2D;
        float half = cap != null ? cap.size.y * 0.5f : 1f;
        return Physics2D.Raycast(ChestPoint, Vector2.up, half + 0.3f, LayerMask.GetMask("Ground")).collider != null;
    }

    private bool TerrainAhead(Vector2 dir)
    {
        float probe = (body is CapsuleCollider2D c ? c.size.x * 0.5f : 0.3f) + 0.35f;
        return Physics2D.Raycast(ChestPoint, dir, probe, LayerMask.GetMask("Ground")).collider != null;
    }

    private float MeasureRay(Vector2 origin, Vector2 dir, float max)
    {
        var hit = Physics2D.Raycast(origin, dir, max, LayerMask.GetMask("Ground"));
        return hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.4f) : max;
    }

    private void MaybeGhost(ref float clock, float _)
    {
        // Ghosts on the approach run too, on the same time accumulator the dash uses.
        if (Mathf.Repeat(clock, ghostInterval) < Time.deltaTime)
            GhostTrail.Snapshot(visualModel, ghostTint, ghostLife);
    }

    private void FaceDirection(float dir)
    {
        bool wantRight = dir > 0f;
        if (wantRight == facingRight) return;
        facingRight = wantRight;
        if (visualModel != null)
        {
            Vector3 s = visualModel.localScale;
            s.x = visualScaleX * (facingRight ? 1f : -1f);
            visualModel.localScale = s;
        }
    }

    // ---- katana flight --------------------------------------------------------------------------
    // Leap → hang → throw → the blade LANDS AND WAITS → blink to it → come at you.
    //
    // ⚠️ THE TELEGRAPH IS THE SWORD, NOT A MARKER. Where it sticks is where he will be, and it sits
    // there long enough to read. That is what makes this legible instead of a teleport that simply
    // happens to you: look at the sword, not the man. A player already moving when it lands can end
    // up behind him.
    //
    // ⚠️ HE IS UNARMED WHILE IT IS IN FLIGHT. The held blade is hidden from the throw until he
    // reclaims it, so the coolest-looking move is also the one that costs him something.
    private float nextKatanaTime;

    private IEnumerator KatanaFlightRoutine()
    {
        if (player == null && GameManager.instance != null && GameManager.instance.player != null)
            player = GameManager.instance.player.transform;
        if (player == null || rb == null) yield break;

        FaceTowardPlayer();
        float dir = facingRight ? 1f : -1f;

        // ---- LEAP ----
        rb.linearVelocity = new Vector2(dir * leapDrift, leapForce);
        while (rb != null && rb.linearVelocity.y > 0.5f) yield return null;

        // ---- HANG + THROW ----
        BossKatana blade = null;
        try
        {
            // ⚠️ gravityScale is restored in a finally. He is a dynamic body; a StopCoroutine here
            // (death, room change) would otherwise leave a boss hanging weightless in mid-air.
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            BeginThrowPose();
            yield return new WaitForSeconds(THROW_RELEASE);

            Vector2 origin = ThrowOrigin;
            Vector2 aim = ((Vector2)player.position + Vector2.up * 0.6f - origin).normalized;

            SetKatanaHeld(false);
            blade = BossKatana.Spawn(origin + aim * 0.45f, aim, katanaDamage, katanaSprite);
            SfxManager.PlayOn(sfx, Katana, katanaThrowVolume);
            EndThrowPose();

            float t = 0f;
            while (t < hangTime) { t += Time.deltaTime; yield return null; }
        }
        finally
        {
            RestoreGravity();
        }

        // Wait for it to land, but never forever — a blade that somehow never sticks must not strand
        // him mid-move with his weapon gone.
        float wait = 0f;
        while (blade != null && !blade.Stuck && wait < 1.5f) { wait += Time.deltaTime; yield return null; }
        if (blade != null && blade.Stuck) SfxManager.PlayOn(sfx, Plant, katanaPlantVolume);

        // ---- THE WATCH BEAT ---- the blade is planted and he has not moved yet. This is the read.
        float watch = 0f;
        while (watch < katanaWatchTime) { watch += Time.deltaTime; yield return null; }

        // ---- BLINK ----
        // ⚠️ ONLY TO A BLADE THAT ACTUALLY LANDED. Blinking to one still in flight teleports him to
        // wherever it happens to be — which, when a bug once let a blade fly on forever, meant
        // sending the boss to x = -40, outside the arena entirely. If it never stuck, he simply
        // stays put and keeps his sword.
        //
        // ⚠️ AND ONLY TO A SPOT HIS BODY ACTUALLY FITS IN. This is the second half of that same
        // lesson and it cost a run: reported from play 2026-08-22, he threw the blade into the rock
        // at the top right of the arena, teleported into it, and spent the rest of the fight buried
        // in the wall throwing stars, unable to move, dash or dive. If nothing near the blade is
        // clear he simply does not go — a boss that stays put is a worse boss for four seconds; a
        // boss inside a wall is a broken encounter for the rest of the fight.
        if (blade != null && blade.Stuck &&
            TryResolveStandingSpot(blade.transform.position, BLINK_SEARCH_RADIUS, out Vector3 landing))
        {
            GhostTrail.Snapshot(visualModel, ghostTint, ghostLife * 1.8f);
            Puff(ChestPoint - Vector2.up * 0.9f, 9, 1.1f, -dir);

            transform.position = landing;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            SfxManager.PlayOn(sfx, Blink, blinkVolume);
            GhostTrail.Snapshot(visualModel, ghostTint, ghostLife * 1.8f);
            Puff(ChestPoint - Vector2.up * 0.9f, 9, 1.1f, dir);
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.14f, 0.25f);
        }
        if (blade != null) blade.Reclaim();   // never leave a stray blade lying in the arena

        SetKatanaHeld(true);          // unconditional: an interrupted flight must never disarm him for good

        yield return new WaitForSeconds(blinkPause);

        // ...and he is already coming.
        yield return StartCoroutine(DashSlashRoutine());
    }

    /// <summary>
    /// Somewhere near the planted blade that his BODY ACTUALLY FITS IN.
    ///
    /// ⚠️ THE VERSION THIS REPLACES WAS ONE RAYCAST DOWN, AND IT BURIED HIM IN A WALL.
    /// It cast down from the blade and stood him on whatever it hit. The blade is deliberately
    /// embedded 0.22 units INTO the surface it struck — so that ray STARTS INSIDE A COLLIDER, and
    /// `Physics2D.queriesStartInColliders` is ON by default, which means it returns a hit at distance
    /// ZERO. The "floor beneath the blade" it found was the blade's own position, inside the rock. He
    /// teleported into the arena wall and could do nothing there but throw stars.
    ///
    /// This is the same class of bug, and the same fix, as the Phase card's `EjectFromGeometry`:
    /// searching a RING OUTWARD for a position the capsule genuinely fits in, nearest first, rather
    /// than trusting a single probe. It is also the same underlying trap as reading `collider.bounds`
    /// after a move — a query that looks authoritative and is answering a different question.
    ///
    /// Angles are walked from straight DOWN outward, so where several spots are equally near he ends
    /// up beneath his blade — which is what "he threw it and followed it" should look like.
    /// </summary>
    private bool TryResolveStandingSpot(Vector3 bladePos, float radius, out Vector3 spot)
    {
        // ⚠️ TWO PASSES, AND THE SECOND ONE IS THE SAFETY NET RATHER THAN THE ANSWER.
        // "Nearest clear spot" is not the same as "somewhere he should be". Burying him deep in the
        // arena's rock frame and searching outward surfaced him ON TOP OF THE ROOM — free, upright,
        // and standing outside the level. So the first pass only accepts spots inside the play area,
        // and only if that finds nothing does he take any clear spot at all. Out of the fight is bad;
        // stuck in a wall is worse, and that ordering is what the two passes encode.
        if (PlayArea.HasValue && SearchStandingSpot(bladePos, PlayArea.Value, radius, out spot)) return true;
        return SearchStandingSpot(bladePos, null, radius, out spot);
    }

    /// <summary>
    /// The room's own idea of where the fight happens, taken from the `CameraBounds` zones every room
    /// is contractually required to have. Nothing else in a room prefab states this, and inventing a
    /// radius around his spawn would be a number with no meaning behind it.
    ///
    /// Cached including the NULL result: a room without CameraBounds is already broken in louder ways
    /// and must not cost a hierarchy walk every time he blinks.
    /// </summary>
    private Bounds? PlayArea
    {
        get
        {
            if (playAreaResolved) return playArea;
            playAreaResolved = true;

            Transform root = transform;
            while (root.parent != null) root = root.parent;
            Transform cb = root.Find("CameraBounds");
            if (cb == null) return playArea = null;

            Bounds b = new Bounds();
            bool any = false;
            foreach (var c in cb.GetComponentsInChildren<Collider2D>(true))
            {
                if (!any) { b = c.bounds; any = true; }
                else b.Encapsulate(c.bounds);
            }
            playArea = any ? (Bounds?)b : null;
            return playArea;
        }
    }

    private Bounds? playArea;
    private bool playAreaResolved;

    private bool SearchStandingSpot(Vector3 bladePos, Bounds? area, float radius, out Vector3 spot)
    {
        spot = bladePos;
        Vector2 size = CapsuleSize;
        Vector2 offset = body != null ? body.offset : Vector2.up;

        for (float r = 0f; r <= radius; r += 0.3f)
        {
            // r == 0 is the blade's own position: one candidate, not sixteen.
            int steps = r < 0.01f ? 1 : 24;
            for (int i = 0; i < steps; i++)
            {
                // Alternating out from straight down: -90, then -90±15, ±30, ...
                float ang = -90f + ((i + 1) / 2) * 15f * ((i % 2 == 0) ? 1f : -1f);
                Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                Vector2 foot = (Vector2)bladePos + dir * r;

                if (!Inside(area, foot)) continue;

                // FIT is tested at 0.95 of the capsule — slightly generous, so a spot that passes is
                // genuinely clear rather than technically legal by a millimetre.
                if (Blocked(foot + offset, size * 0.95f)) continue;

                // Then drop him onto the floor under it. This ray starts in FREE SPACE (we just
                // proved it), which is exactly why the old one could not be trusted and this one can.
                //
                // ⚠️ SHORT REACH, DELIBERATELY. At 9 units this quietly became a second teleport: a
                // blade planted in the ceiling resolved to a clear spot beside it and was then
                // dropped to the floor far below — measured worst case 8.2 units away from the sword
                // the player was told to watch. He is a dynamic body with gravity, so a long drop
                // does not need doing here; gravity does it, and a ninja FALLING out of the ceiling
                // is both readable and better-looking than one who was never up there. This snap is
                // only to tidy away a small gap so short arrivals do not start with a visible hop.
                var down = Physics2D.Raycast(foot + offset, Vector2.down, 3f, LayerMask.GetMask("Ground"));
                if (down.collider != null)
                {
                    Vector2 grounded = new Vector2(foot.x, down.point.y + size.y * 0.5f + 0.04f - offset.y);
                    if (Inside(area, grounded) && !Blocked(grounded + offset, size * 0.95f)) foot = grounded;
                }

                spot = new Vector3(foot.x, foot.y, PlayPlane.Z);
                return true;
            }
        }
        return false;
    }

    // ⚠️ TWO RADII, AND THEY ARE DIFFERENT ON PURPOSE — they are answering different questions.
    //
    // The BLINK is a promise: the planted blade says "this is where he will be", and that is the only
    // reason a teleporting boss is legible instead of unfair. Letting it search a long way to find
    // somewhere to land would break the promise — he would arrive somewhere the player never looked.
    // Six units, and if nothing that close works he simply does not go.
    //
    // The RESCUE is a repair, and the telegraph is already irrelevant by the time it runs. Its only
    // job is to get him back into a fight he is currently buried outside of, so it looks much further.
    private const float BLINK_SEARCH_RADIUS = 6f;
    private const float RESCUE_SEARCH_RADIUS = 16f;

    // X/Y only. Z is the play plane for everything that matters here, and a camera zone's depth is
    // not something a room author thinks about — testing against it would reject perfectly good spots
    // for a reason nobody could see.
    private static bool Inside(Bounds? area, Vector2 p)
    {
        if (!area.HasValue) return true;
        Bounds b = area.Value;
        return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
    }

    private Vector2 CapsuleSize => body is CapsuleCollider2D c ? c.size : new Vector2(0.63f, 2.1f);

    // ⚠️ TRIGGERS ARE SKIPPED. Ground carries non-solid colliders too (breakable-wall probes, zone
    // volumes), and counting one as terrain would make a perfectly open spot look occupied — which
    // in the search above reads as "nowhere to land" and silently cancels the whole ability.
    private static bool Blocked(Vector2 centre, Vector2 size)
    {
        var hits = Physics2D.OverlapBoxAll(centre, size, 0f, LayerMask.GetMask("Ground"));
        foreach (var h in hits) if (h != null && !h.isTrigger) return true;
        return false;
    }

    /// <summary>
    /// The last-resort watchdog: if he is ever inside terrain, get him out.
    ///
    /// ⚠️ THIS IS THE HALF THAT MATTERS, and it is deliberately not specific to the katana. Fixing
    /// the blink fixes the one path somebody happened to report; a boss that can teleport, dash
    /// through walls with collision disabled, dive at 30 u/s and cling to walls with gravity switched
    /// off has other ways to end up embedded, including ones nobody has written yet. Same reasoning
    /// as `EnemyHealthBar` owning its own lifetime rather than trusting every future spawn site to
    /// remember to clean up: the general guard covers the cases nobody thought of.
    ///
    /// Detection is deliberately LOOSER (0.8) than the fit test (0.95) — a capsule has rounded ends,
    /// so a body legitimately wedged into a corner overlaps its own bounding box. Only a real burial
    /// trips this.
    /// </summary>
    private void EnsureNotStuck()
    {
        if (body == null || Time.time < nextStuckCheck) return;
        nextStuckCheck = Time.time + 0.25f;

        // ⚠️ NO "I AM MID-MOVE" EXEMPTION FLAG, DELIBERATELY. The obvious design is to switch this
        // off during the dash and the dive so it cannot interrupt them — and that flag is a LATCH,
        // which is the failure mode this file has been bitten by over and over: one StopCoroutine on
        // death or a room change leaves it set, and the watchdog is silently dead for the rest of the
        // session with nothing to show for it. It does not need the exemption anyway. The solver
        // never lets a dynamic body rest inside terrain, and the detection box is smaller than the
        // capsule, so merely being pressed against a wall at 30 u/s does not trip it. If he really is
        // buried mid-dash, that is the bug, not an exception to it.
        Vector2 centre = (Vector2)transform.position + body.offset;
        if (!Blocked(centre, CapsuleSize * 0.8f)) return;

        if (TryResolveStandingSpot(transform.position, RESCUE_SEARCH_RADIUS, out Vector3 free))
        {
            transform.position = free;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Puff(ChestPoint - Vector2.up * 0.9f, 8, 1.1f, 1f);
        }
    }

    private float nextStuckCheck;

    // Cosmetic only. Toggles the renderers under the rig's Weapon Slot, so the blade that flies is
    // the blade he was holding rather than a second one conjured out of nowhere.
    private void SetKatanaHeld(bool held)
    {
        if (visualModel == null) return;
        Transform slot = visualModel.Find("Weapon Slot");
        if (slot == null) return;
        foreach (var r in slot.GetComponentsInChildren<Renderer>(true)) r.enabled = held;
    }

    // ---- dash slash -----------------------------------------------------------------------------

    private IEnumerator DashSlashRoutine()
    {
        if (player == null && GameManager.instance != null && GameManager.instance.player != null)
            player = GameManager.instance.player.transform;
        if (player == null) yield break;

        FaceTowardPlayer();
        float dir = facingRight ? 1f : -1f;
        float lane = MeasureLane(dir);

        // ⚠️ THE TERMINUS IS A WORLD POSITION, FIXED BEFORE THE WIND-UP — not a distance measured
        // from wherever he happens to be when he launches. That distinction is the whole bug the
        // designer reported: *"it shows in one spot, but the ninja boss sometimes goes lower than
        // what that indication showed"*. Two separate lies were being told —
        //
        //   VERTICAL   he keeps falling. `rb.linearVelocity` was written as (dir * speed, keep Y),
        //              so gravity ran through the coil AND through the dash, while the strip stayed
        //              at the height it was drawn. Worse, `KatanaFlightRoutine` chains straight into
        //              a dash after the blink with no grounded check at all, so he could begin the
        //              whole move airborne.
        //   HORIZONTAL he drifts BACKWARD by `leanBack` while coiling, and the old loop then ran a
        //              full `lane` from that retreated position — overshooting the drawn end.
        //
        // Fixing the drawing would have been the wrong repair. **The telegraph is the contract**, so
        // the ATTACK is what gets corrected: Y is locked for the duration below, and the loop runs to
        // this exact X.
        Vector2 terminus = ChestPoint + new Vector2(dir * lane, 0f);

        // ⚠️ AND THE LANE IS TELEGRAPHED, NOT THE POSE. Drawing the ground he is about to occupy makes
        // leaving it a DECISION; a wind-up animation alone only tells you something is coming, not
        // where, which is the difference between "fast" and "unfair".
        Telegraph tel = Telegraph.Build(this, ChestPoint, terminus, dashHeight, true);

        // ---- COIL ------------------------------------------------------------------------------
        // He drops into the pack's authored crouch and drifts BACKWARD, away from where he is about
        // to go. That backward drift is the whole trick: anticipation is the oldest readability tool
        // there is, and it also tells the player which way the strike is coming from.
        // ⚠️ GRAVITY IS OFF FROM HERE, NOT FROM THE LAUNCH — and the difference was measured.
        // Holding it only for the travel made the dash itself perfectly straight (y drift across the
        // whole run: 0.000) while leaving a constant offset of nearly TWO UNITS, because he was still
        // falling through the 0.54s of coil and hold before it. The telegraph would then either lie
        // or have to tilt into a diagonal he does not actually travel.
        //
        // Switching it off for the whole move also agrees with what the coil is FOR: the hold beat is
        // "fully still, fully loaded", and a boss sinking through it says the opposite. On the ground
        // this is invisible; in the air — which is where `KatanaFlightRoutine` hands over — it is the
        // whole fix.
        if (rb != null) rb.gravityScale = 0f;

        EnterAnticipation();
        float coil = Mathf.Max(0.01f, coilTime);
        float leanSpeed = leanBack / coil;

        float t = 0f;
        while (t < coil)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / coil);

            // Velocity, not transform assignment — he is a dynamic body, and writing position
            // directly every frame fights the solver and reads as jitter. Eased so he loads
            // sharply and settles, rather than sliding at a constant rate.
            if (rb != null) rb.linearVelocity = new Vector2(-dir * leanSpeed * (1f - k), rb.linearVelocity.y);

            // Re-aimed from his LIVE chest to the fixed terminus every frame. The start of the line
            // therefore stays welded to him as he leans back and as gravity moves him, while the end
            // never moves — which is exactly the right way round: where he starts is obvious, where
            // he stops is the promise.
            //
            // ⚠️ LINEAR RAMP, NOT k*k, AND A REAL FLOOR. The first version started at 0.05 and eased
            // in on k², so the lane was measurably INVISIBLE for most of the wind-up and appeared
            // only just before he committed — the one moment it is too late to be useful. A
            // telegraph's job is done in its first frame; the ramp only builds pressure. Values
            // picked by screenshot, per the standing rule that alphas in linear space are measured.
            if (tel != null)
            {
                tel.Place(ChestPoint, terminus, dashHeight, this);
                tel.SetIntensity(k, this);
            }
            yield return null;
        }

        // ---- HOLD ------------------------------------------------------------------------------
        // Fully still, fully loaded. Nothing moves. This is the beat that sells the launch.
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        float hold = 0f;
        while (hold < holdTime)
        {
            hold += Time.deltaTime;
            if (tel != null) { tel.Place(ChestPoint, terminus, dashHeight, this); tel.SetIntensity(1f, this); }
            yield return null;
        }

        if (tel != null) tel.Clear();

        // ---- LAUNCH ----------------------------------------------------------------------------
        ExitAnticipation();
        SfxManager.PlayOn(sfx, Dash, dashVolume);
        if (ghostOnLaunch) GhostTrail.Snapshot(visualModel, ghostTint, ghostLife);
        Puff(ChestPoint - new Vector2(dir * 0.3f, 0.9f), 7, 1f, -dir);

        if (animator != null)
        {
            // ⚠️ Dash and the Attack layers are INDEPENDENT in AC Character, so these compose: the
            // body plays the pack's authored Dash while the arm plays Swipe. That is a genuine
            // dashing slash rather than a swipe animation played over a slide, which is what the
            // first version was and why it read as boring.
            animator.SetBool("IsDashing", true);
            animator.SetInteger("AttackAction", SWIPE_ACTION);
            animator.SetBool("IsAttacking", true);
        }

        // He passes THROUGH the player rather than shoving them — he is mass 500 against the
        // player's 1, so a body-check would fling them across the arena. Same call the Moss Knight's
        // charge makes. try/finally because a StopCoroutine (death, room change) must never leave
        // the player permanently able to walk through him.
        SetPlayerCollision(false);
        bool hitWall = false;

        try
        {
            bool struck = false;
            float ghostClock = 0f;

            // ⚠️ THREE EXIT CONDITIONS, AND THE LOOP HUNG WITH ONLY TWO OF THEM.
            // It used to end on distance-travelled or WallAhead. `WallAhead` casts ONE ray from chest
            // height, so a platform that blocks his body above or below that line satisfies neither:
            // he is pinned by physics, x stops changing, the distance never accumulates, and the dash
            // runs FOREVER — animation latched, IsDashing stuck on, player-collision permanently
            // ignored, and the once-per-dash strike flag never spent (which is why walking into a
            // "finished" dash still dealt damage). Reported from play, 2026-08-22.
            //
            // The honest fix is not a better ray. It is to notice he has STOPPED MOVING: whatever the
            // geometry is, if he is not making progress the dash is over. The hard timeout is a
            // second net under that.
            float elapsed = 0f;
            float stalled = 0f;
            float lastX = transform.position.x;

            // Runs to the TERMINUS THE TELEGRAPH DREW, not a distance from here. He retreated by
            // `leanBack` during the coil, so a relative distance overshot the drawn end every time.
            while ((terminus.x - transform.position.x) * dir > 0.05f)
            {
                elapsed += Time.fixedDeltaTime;
                if (elapsed > DASH_TIMEOUT) { hitWall = true; break; }

                float moved = Mathf.Abs(transform.position.x - lastX);
                lastX = transform.position.x;
                stalled = moved < 0.01f ? stalled + Time.fixedDeltaTime : 0f;
                if (stalled > DASH_STALL) { hitWall = true; break; }

                // Y velocity ZEROED, not preserved. With gravity already off, this holds the exact
                // line the player was shown.
                rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

                // Afterimages, spaced on a TIME ACCUMULATOR rather than one per physics step — a
                // per-step spawn is framerate-dependent and runs away completely in slow motion,
                // which is how the gate's spark burst once hit 109 live particles at timeScale 0.05.
                if (leaveGhosts)
                {
                    ghostClock += Time.fixedDeltaTime;
                    if (ghostClock >= ghostInterval)
                    {
                        ghostClock = 0f;
                        GhostTrail.Snapshot(visualModel, ghostTint, ghostLife);
                    }
                }

                // Once per dash. The box travels with him, so without the flag a single pass would
                // land a hit on every physics step it overlapped the player.
                if (!struck && EnemyMelee.TryHit(transform, dir, dashReach, dashDamage, dashKnockback, dashHeight))
                    struck = true;

                if (WallAhead(dir)) { hitWall = true; break; }
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            SetPlayerCollision(true);
            RestoreGravity();
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null)
        {
            animator.SetBool("IsDashing", false);
            animator.SetBool("IsAttacking", false);
        }

        // The skid. Dust kicks BACKWARD out of the stop, opposite his travel, so the arrival reads
        // as braking rather than as simply ceasing to exist.
        Puff(ChestPoint - new Vector2(0f, 0.9f), hitWall ? 12 : 8, hitWall ? 1.4f : 1f, -dir);
        if (hitWall && CameraShake.instance != null) CameraShake.instance.Shake(0.18f, 0.30f);

        // ⚠️ THE RECOVERY IS THE POINT OF THE WHOLE ATTACK. It is the window a player with no damage
        // cards uses to hurt him, and burying the blade in a wall costs him roughly twice as long —
        // so baiting the overshoot is a real, learnable play rather than a cosmetic flourish.
        yield return new WaitForSeconds(hitWall ? dashWallRecovery : dashRecovery);
    }

    /// <summary>
    /// How far this dash actually travels: to the player, plus a fixed overshoot, cut short by any
    /// wall in the way.
    ///
    /// ⚠️ IT IS NOT A FLAT DISTANCE, AND THE FIRST VERSION WAS. Dashing a constant 18 units in a
    /// 30-wide arena put him on the far side every time, so he never met a wall — which meant the
    /// long wall-recovery, the entire reason the attack has a punish window, could never fire. He
    /// also just ping-ponged from end to end. Committing to "reach you and a bit further" is what
    /// makes backing up to a wall a real play: the overshoot is then what buries his blade.
    /// </summary>
    private float MeasureLane(float dir)
    {
        float want = dashMaxDistance;
        if (player != null)
            want = Mathf.Abs(player.position.x - transform.position.x) + dashOvershoot;
        want = Mathf.Min(want, dashMaxDistance);

        var hit = Physics2D.Raycast(ChestPoint, new Vector2(dir, 0f), want, LayerMask.GetMask("Ground"));
        return hit.collider != null ? Mathf.Max(0.5f, hit.distance - 0.6f) : want;
    }

    // Hard limits on a single dash. Nothing legitimate comes close to these — they exist purely so a
    // dash can never become permanent.
    private const float DASH_TIMEOUT = 1.6f;
    private const float DASH_STALL   = 0.10f;

    // ⚠️ PROBED AT THREE HEIGHTS, NOT ONE. A single chest-height ray walks straight over a low ledge
    // and straight under an overhang, so the dash could be physically blocked by geometry the check
    // could not see. Cheap to cast three; expensive to debug the one it misses.
    private bool WallAhead(float dir)
    {
        var cap = body as CapsuleCollider2D;
        float halfW = cap != null ? cap.size.x * 0.5f : 0.3f;
        float halfH = cap != null ? cap.size.y * 0.5f : 1f;
        float probe = halfW + 0.25f;
        Vector2 centre = ChestPoint;
        Vector2 d = new Vector2(dir, 0f);
        int ground = LayerMask.GetMask("Ground");

        return Physics2D.Raycast(centre, d, probe, ground).collider != null
            || Physics2D.Raycast(centre + Vector2.up * (halfH * 0.75f), d, probe, ground).collider != null
            || Physics2D.Raycast(centre - Vector2.up * (halfH * 0.75f), d, probe, ground).collider != null;
    }

    // ⚠️ DERIVED FROM transform + collider OFFSET, NEVER FROM collider.bounds.
    // `Physics2D.autoSyncTransforms` is OFF by default, so bounds still report the PREVIOUS position
    // until the next physics step — which put the dash telegraph two units behind him whenever he had
    // just moved. Exact here because the boss root is guaranteed scale (1,1,1). Same trap that made
    // the Phase card's first eject fix silently fail.
    private Vector2 ChestPoint => (Vector2)transform.position + (body != null ? body.offset : Vector2.up);

    /// <summary>
    /// THE CUT — what he is about to do, drawn before he does it.
    ///
    /// ⚠️ IT REPLACED A FLAT RED RECTANGLE, and the designer's verdict on that was *"really ugly,
    /// its just red and pretty bland"*. The fault was not the colour. A solid slab is the **generic
    /// reveal** the UI doc warns about wearing a costume — a shape drawn around the danger, carrying
    /// no information beyond "something here", and equally at home in any game.
    ///
    /// The test that replaces it: **what does this object already mean in this game?** He is a blade
    /// travelling in a straight line, and his whole visual identity is AFTERIMAGES. So the telegraph
    /// is built from his own vocabulary, inverted:
    ///
    ///   BAND   the volume the strike sweeps — faint, wide, context only.
    ///   CUT    a thin bright line at blade height: the path the EDGE takes. This is the warning,
    ///          and it reads because it is thin and bright, not because it is big and dim.
    ///   NOTCH  a tick at the terminus. The slab never said where he STOPS, which is the single most
    ///          useful thing a player can know about a charge — it is what makes baiting the
    ///          overshoot into a wall a real play instead of a lucky one.
    ///   GHOST  a premonition of him standing at the end, resolving as he loads.
    ///          ⚠️ Blue ghosts are where he WAS. This one is red and it is where he WILL BE. Same
    ///          mechanism, opposite meaning, and it costs no new art because `GhostTrail` already
    ///          bakes his rig.
    ///
    /// Nothing GROWS: the geometry is full-length on frame one (a telegraph's job is done in its
    /// first frame) and only the intensity and the premonition change as he commits.
    /// </summary>
    private class Telegraph
    {
        public GameObject root;
        private SpriteRenderer cut, notch;
        private SpriteRenderer[] streaks;
        private GhostTrail ghost;

        // Where the outriggers sit, as a fraction of the volume's half-height. The outermost pair
        // marks the real extent of the hit box; the notch confirms it at the far end.
        private static readonly float[] StreakAt = { -0.86f, -0.44f, 0.44f, 0.86f };

        public static Telegraph Build(NinjaBoss b, Vector2 from, Vector2 to, float thickness, bool premonition)
        {
            var t = new Telegraph();
            t.root = new GameObject("DashTelegraph");
            t.root.AddComponent<TemporaryObject>();

            // ⚠️ NO SOLID BAND. The first rebuild kept one — a faint filled rectangle behind the cut
            // line, to show the volume the strike sweeps — and photographed, it was still a red BOX
            // with two crisp horizontal edges ruled across the room. It had the same fault as the slab
            // it replaced, just quieter: a rectangle drawn around danger is the generic reveal, and
            // hard parallel edges read as interface rather than as anything in the world.
            //
            // Four hairline outriggers instead, thinning toward the edges. They state exactly the same
            // volume — the outer pair IS the hit box's extent — but they read as SPEED LINES, which is
            // the one thing this attack is actually about and the same vocabulary as his afterimages.
            t.streaks = new SpriteRenderer[StreakAt.Length];
            for (int i = 0; i < StreakAt.Length; i++) t.streaks[i] = Strip(t.root.transform, 2);

            t.cut = Strip(t.root.transform, 3);
            t.notch = Strip(t.root.transform, 3);

            if (premonition && b.showPremonition && b.visualModel != null)
            {
                // Positioned by moving the ghost's ROOT after Snapshot — its children keep local
                // offsets, so the whole figure travels together.
                t.ghost = GhostTrail.Snapshot(b.visualModel, b.premonitionTint,
                                              Mathf.Max(0.05f, b.coilTime + b.holdTime), 3, true);
                if (t.ghost != null)
                    t.ghost.transform.position += (Vector3)(to - from);
            }

            t.Place(from, to, thickness, b);
            return t;
        }

        private static SpriteRenderer Strip(Transform parent, int order)
        {
            var go = new GameObject("Strip");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = FlatUI.Pixel();
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Re-aim it. Called every frame during the wind-up, so it can never go stale.</summary>
        public void Place(Vector2 from, Vector2 to, float thickness, NinjaBoss b)
        {
            Vector2 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.01f) { delta = Vector2.right; len = 0.01f; }
            Vector2 dir = delta / len;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 mid = from + dir * len * 0.5f;
            Vector2 up = new Vector2(-dir.y, dir.x);        // perpendicular, so this works on a diagonal

            // The outriggers. Shorter than the cut and offset from its ends, so the shape tapers to a
            // point rather than terminating as a blunt rectangle.
            for (int i = 0; i < streaks.Length; i++)
            {
                float off = StreakAt[i] * thickness * 0.5f;
                float inset = Mathf.Abs(StreakAt[i]) * len * 0.10f;
                Size(streaks[i], Mathf.Max(0.1f, len - inset * 2f), b.cutThickness * 0.55f);
                Aim(streaks[i], mid + up * off, ang, 0.05f);
            }

            Size(cut, len, b.cutThickness);
            Aim(cut, mid, ang, 0.06f);

            // A short perpendicular tick standing at the far end — the only element that states the
            // full height, and the only one that says where he STOPS.
            Size(notch, b.cutThickness * 1.6f, thickness * 0.85f);
            Aim(notch, to, ang, 0.06f);
        }

        // ⚠️ SCALE *TO* A SIZE, NEVER *BY* ONE. `FlatUI.Pixel()` is a ONE-PIXEL sprite, so its native
        // world size is a fraction of a unit — not 1×1. Setting localScale to (length, height)
        // directly, as the first version did, produced a lane measuring 0.08 × 0.02 world units: a
        // speck, perfectly invisible, while every value in code read correct. The bug survived two
        // rounds of "the alpha must be too low" before the renderer bounds were actually measured.
        private static void Size(SpriteRenderer sr, float length, float thickness)
        {
            Vector2 native = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(
                native.x > 0.0001f ? length / native.x : length,
                native.y > 0.0001f ? thickness / native.y : thickness, 1f);
        }

        // Scale is applied in LOCAL space, so the rotation must be set after it or the strip shears.
        private static void Aim(SpriteRenderer sr, Vector2 centre, float angle, float zLift)
        {
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            sr.transform.position = new Vector3(centre.x, centre.y, PlayPlane.Z + zLift);
        }

        public void SetIntensity(float k, NinjaBoss b)
        {
            Color c = b.laneColor;
            float a = Mathf.Lerp(b.cutAlphaStart, b.cutAlphaEnd, k);
            cut.color = new Color(c.r, c.g, c.b, a);
            notch.color = new Color(c.r, c.g, c.b, a * 0.85f);

            // Falling off toward the edges of the volume, so the danger reads as densest along the
            // blade's own line and merely present at the extremes — which is exactly true.
            for (int i = 0; i < streaks.Length; i++)
            {
                float fade = 1f - Mathf.Abs(StreakAt[i]) * 0.55f;
                streaks[i].color = new Color(c.r, c.g, c.b, b.bandAlpha * fade * Mathf.Lerp(1.4f, 2.6f, k));
            }
        }

        public void Clear()
        {
            if (root != null) Object.Destroy(root);
            // The premonition is deliberately NOT destroyed here — its lifetime ends on its own at
            // exactly the moment he launches, so it lands as he goes rather than blinking out first.
        }
    }

    private void SetPlayerCollision(bool enabled)
    {
        if (body == null || GameManager.instance == null || GameManager.instance.player == null) return;
        var pc = GameManager.instance.player.GetComponent<Collider2D>();
        if (pc != null) Physics2D.IgnoreCollision(body, pc, !enabled);
    }

    // ---- anticipation ---------------------------------------------------------------------------
    // Both poses are AUTHORED BY THE PACK — no procedural squash, no invented art. AC Character
    // carries a whole Crouch layer and a Dodge layer that nothing in this project had been using.

    private void EnterAnticipation()
    {
        if (animator == null) return;
        switch (anticipation)
        {
            case Anticipation.Crouch:
                animator.SetBool("IsCrouching", true);
                break;
            case Anticipation.DodgeBack:
                animator.SetInteger("DodgeDir", dodgeDirValue);
                animator.SetBool("IsDodging", true);
                break;
        }
    }

    private void ExitAnticipation()
    {
        if (animator == null) return;
        animator.SetBool("IsCrouching", false);
        animator.SetBool("IsDodging", false);
    }

    // ⚠️ Called from death and OnDestroy as well as the normal path. A boss killed mid-coil would
    // otherwise leave the rig stuck in a crouch, and — worse — leave IsDashing latched, which is a
    // BOOL ON A CONTROLLER THE PLAYER ALSO USES.
    private void ClearAnimatorState()
    {
        if (animator == null) return;
        animator.SetBool("IsCrouching", false);
        animator.SetBool("IsDodging", false);
        animator.SetBool("IsDashing", false);
        animator.SetBool("IsAttacking", false);
        animator.SetFloat("AttackSpeedMul", 1f);
        animator.SetBool("IsMoving", false);
        animator.SetFloat("MoveBlendX", 0f);
        animator.SetBool("IsClimbingLadder", false);
        animator.SetFloat("ClimbingSpeedMul", 1f);
    }

    // Ground dust. Biased along `bias` so a launch throws it backward and a skid throws it forward,
    // which is what makes the two read differently.
    private void Puff(Vector2 pos, int count, float scale, float bias)
    {
        var root = new GameObject("DashDust");
        root.transform.position = pos;
        root.AddComponent<TemporaryObject>();
        root.AddComponent<SparkFade>();

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
            // Pitched brighter than "dust" suggests: world sprites render through the scene's
            // 0.5-intensity global Light2D, and a plausible grey came back reading as dirt on the
            // lens. Same calibration the wall-scrape VFX needed.
            sr.color = new Color(0.72f, 0.68f, 0.62f, 0.85f);
            sr.sortingOrder = 6;
        }
    }

    private IEnumerator VolleyRoutine()
    {
        if (player == null) yield break;

        int count = Random.Range(starsMin, starsMax + 1);

        FaceTowardPlayer();
        BeginThrowPose();
        yield return new WaitForSeconds(THROW_RELEASE);

        for (int i = 0; i < count; i++)
        {
            if (player == null) break;

            // Re-aimed per star rather than fanned from one snapshot, so a moving player is tracked
            // across the volley instead of being led into a spread they already left.
            Vector2 origin = ThrowOrigin;
            Vector2 aim = ((Vector2)player.position + Vector2.up * 0.9f - origin).normalized;

            // Spread is centred on the aim: an odd star goes dead centre, the rest bracket it.
            float t = count <= 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
            Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, t * spreadDegrees) * aim);

            BossShuriken.Spawn(origin + dir * 0.35f, dir, starDamage, shurikenSprite);
            SfxManager.PlayOn(sfx, Star, throwVolume);

            yield return new WaitForSeconds(starInterval);
        }

        EndThrowPose();

        // The recall is scheduled from the END of the volley, so the window is the same length
        // however many stars went out.
        StartCoroutine(RecallAfterDelay());
    }

    private IEnumerator RecallAfterDelay()
    {
        yield return new WaitForSeconds(recallDelay);
        if (health == null || health.CurrentHealth <= 0f) yield break;

        int pulled = BossShuriken.RecallAll(transform);
        if (pulled > 0) SfxManager.PlayOn(sfx, Recall, recallVolume);
    }

    // ---- Shift payout ---------------------------------------------------------------------------

    private void Update()
    {
        if (!fightStarted) return;

        EnsureNotStuck();

        bool grounded = IsGrounded();
        // Landing closes the airborne period, so the next leap pays again.
        if (grounded && !wasGrounded) crystalsThisAirtime = 0;
        wasGrounded = grounded;

        // Feed the rig's Air layer so the leap actually plays Air Up / Air Down / Land instead of
        // sliding upward in an idle pose. Nothing was driving these before the katana flight existed.
        if (animator != null)
        {
            animator.SetBool("IsGrounded", grounded);
            animator.SetFloat("VelocityY", rb != null ? rb.linearVelocity.y : 0f);
        }
    }

    // ⚠️ DERIVED FROM transform + collider OFFSET/SIZE, NEVER FROM collider.bounds — the same reason
    // ChestPoint is. `Physics2D.autoSyncTransforms` is OFF, so bounds report the PREVIOUS position
    // until the next physics step, and this boss now TELEPORTS: right after a blink the old version
    // would have ground-checked wherever he used to be. Exact because the root is scale (1,1,1).
    private bool IsGrounded()
    {
        var cap = body as CapsuleCollider2D;
        Vector2 centre = (Vector2)transform.position + (body != null ? body.offset : Vector2.up);
        float halfHeight = cap != null ? cap.size.y * 0.5f : 1f;
        return Physics2D.Raycast(centre, Vector2.down, halfHeight + groundCheckDistance,
                                 LayerMask.GetMask("Ground")).collider != null;
    }

    /// <summary>
    /// ⚠️ HIS SCARIEST ATTACK IS ALSO THE PLAYER'S PAYDAY. Hitting him mid-air sheds Shift, which is
    /// what lets a player with no damage cards fund their own dodging: collect his stars, throw them
    /// back while he is in the air, spend the Shift staying alive. Do not gate this behind a card
    /// type or a damage threshold — it hangs off EnemyHealth.OnDamaged precisely so every damage
    /// source, including ones not written yet, pays out.
    /// </summary>
    private void OnDamaged()
    {
        if (!fightStarted) return;
        if (shiftCrystalPrefab == null) return;
        if (IsGrounded()) return;
        if (crystalsThisAirtime >= crystalsPerAirtime) return;

        crystalsThisAirtime++;

        // Parented to nothing by design (it is a pickup), so it needs the room sweep — the exact
        // class of bug ClearRuntimeSpawns and TemporaryObject exist for.
        GameObject c = Instantiate(shiftCrystalPrefab,
                                   transform.position + Vector3.up * 1.2f + (Vector3)(Random.insideUnitCircle * 0.4f),
                                   Quaternion.identity);
        if (c.GetComponent<TemporaryObject>() == null) c.AddComponent<TemporaryObject>();
    }

    private void OnBossDied()
    {
        // ⚠️ Stars already stuck in the arena are deliberately LEFT for the player to sweep up — the
        // fight is over and they are spoils. They are cleared with everything else on the room change.
        StopAllCoroutines();
        ClearAnimatorState();
        SetPlayerCollision(true);   // dying mid-dash must not leave the player able to walk through him
        RestoreGravity();           // ...nor a weightless corpse hanging where he was killed
        OpenExit();

        if (MusicManager.instance != null) MusicManager.instance.StopBossMusic();

        // ⚠️ THE CELEBRATION RUNS ON ITS OWN OBJECT, NOT ON HIM. `EnemyHealth.Die()` destroys this
        // GameObject on the SAME FRAME it fires OnDied, so a coroutine started here — or anything
        // parented to him — dies before it draws a single frame. Same rule BossDeathVFX's own header
        // states and the same one BossShuriken's impact sparks follow.
        if (playDeathEffect)
        {
            bool airborne;
            float groundY = ResolveDeathGroundY(out airborne);

            var go = new GameObject("BossDeathVFX");
            go.transform.position = transform.position + Vector3.up * 0.9f;
            go.AddComponent<BossDeathVFX>().Play(groundY, airborne,
                                                 deathGoldPrefab, deathShiftCrystalPrefab,
                                                 deathSound, deathVolume,
                                                 deathGoldCount, deathCrystalCount,
                                                 deathBurstColor);
        }

        if (offerBossRelic) BossRewardCue.Schedule(bossRelicChoices, rewardDelay);
    }

    /// <summary>
    /// The floor the loot should land on. `airborne` comes back true when there is nothing close
    /// below — he can be killed mid-leap or mid-wall-cling — and the drops then hover instead of
    /// falling out of the world.
    /// </summary>
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
            if (h.collider.CompareTag("Player")) continue;      // never land loot on the player
            if (h.distance < nearest) { nearest = h.distance; groundY = h.point.y; airborne = false; }
        }
        return groundY;
    }

    // ---- rig ------------------------------------------------------------------------------------

    private Vector2 ThrowOrigin
    {
        get
        {
            if (body != null) return (Vector2)body.bounds.center + new Vector2(facingRight ? 0.35f : -0.35f, 0.15f);
            return (Vector2)transform.position + Vector2.up * 1.1f;
        }
    }

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
        animator.SetBool("IsAttacking", false);
        animator.SetFloat("AttackSpeedMul", 1f);
    }

    private void FaceTowardPlayer()
    {
        if (player == null) return;
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return;

        bool wantRight = dx > 0f;
        if (wantRight == facingRight) return;

        facingRight = wantRight;
        if (visualModel != null)
        {
            Vector3 s = visualModel.localScale;
            s.x = visualScaleX * (facingRight ? 1f : -1f);
            visualModel.localScale = s;
        }
    }
}
