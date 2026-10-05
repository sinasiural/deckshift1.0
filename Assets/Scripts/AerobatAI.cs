using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

public class AeroBatAI : MonoBehaviour
{
    [Header("Referanslar")]
    public PixelMonster pixelMonster; // PF Bat - Black objesini sürükle (animasyon için)

    [Header("Algılama")]
    public float detectRange = 9f;
    public LayerMask obstacleLayer;

    [Header("Hareket")]
    public float moveSpeed = 3f;
    public float diveSpeedMultiplier = 2f;

    [Header("Saldırı")]
    public float windUpTime = 0.8f;
    public float diveDuration = 1.5f;
    public float damage = 15f;
    public float knockbackForce = 5f;

    [Header("Hover")]
    public float hoverHeight = 0.3f;
    public float hoverSpeed = 2f;

    [Header("Wind-up telegraph")]
    // ⚠️ THE TELEGRAPH USED TO BE A PLAIN RED `Square` SPRITE floating over the bat — and it sat at
    // almost exactly the height of the enemy health bar, which is ALSO a red bar. The two overlapped
    // and were indistinguishable, so in practice the dive had no readable warning at all.
    //
    // It is now the bat itself: it REARS BACK away from you and flushes hot before it commits. That
    // is anticipation, the oldest and most readable telegraph there is, and it needs no icon, no new
    // art and nothing that could ever be confused with a health bar. It also says something a red
    // box cannot — which way it is about to go, because it winds up in the opposite direction.
    [Tooltip("How far it pulls back before lunging. This IS the tell — do not set it to 0.")]
    public float windUpRecoil = 0.55f;
    [Tooltip("Colour it flushes to while winding up. Its own eyes are this orange.")]
    public Color windUpTint = new Color(1f, 0.42f, 0.30f, 1f);
    [Tooltip("Breathing room after a dive before it may pick you again.")]
    public float diveCooldown = 1.1f;

    [Header("Görsel")]
    [Tooltip("OBSOLETE — the red square telegraph this drove is gone. Left only so old prefabs that " +
             "still carry one can be found and switched off; nothing shows it any more.")]
    public GameObject alertIcon;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private Transform player;
    private Vector3 startPos;
    private Renderer body;
    private MaterialPropertyBlock mpb;
    private float diveReadyAt;
    private Vector2 prepFrom, prepBack;
    private float prepK;

    private enum State { Idle, Preparing, Diving, Returning }
    private State currentState = State.Idle;

    private Vector3 diveTarget;
    private float diveTimer;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        startPos = transform.position;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // The Cainos monster shader exposes _Color (verified — unlike the PLAYER rig's "Alpha Cut",
        // which exposes no colour property at all and silently swallows tint writes).
        body = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (body == null) body = GetComponentInChildren<SpriteRenderer>(true);
        mpb = new MaterialPropertyBlock();
        SetTint(Color.white);

        // Any leftover red square from an old prefab: off, permanently.
        if (alertIcon != null) alertIcon.SetActive(false);
    }

    private void SetTint(Color c)
    {
        if (body == null || mpb == null) return;
        body.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", c);
        body.SetPropertyBlock(mpb);
    }

    private void FixedUpdate()
    {
        if (player == null) return;
        if (health != null && health.IsStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (currentState)
        {
            case State.Idle:
                IdleBehavior();
                break;
            case State.Preparing:
                // ⚠️ The recoil is applied HERE, not in the coroutine that times it. This is a
                // Kinematic body, so MovePosition belongs on the physics step; driven from a
                // coroutine (which runs on the Update timing) it stutters against FixedUpdate.
                rb.linearVelocity = Vector2.zero;
                rb.MovePosition(Vector2.Lerp(prepFrom, prepBack, prepK));
                break;
            case State.Diving:
                DiveBehavior();
                break;
            case State.Returning:
                ReturnBehavior();
                break;
        }
    }

    private void Update()
    {
        if (player == null) return;
        if (health != null && health.IsStunned) return;

        // Idle'dayken oyuncuyu kontrol et
        if (currentState == State.Idle)
        {
            CheckForPlayer();
        }

        // Yön değişimi
        if (currentState == State.Diving)
            FaceTarget(diveTarget);
        else if (currentState == State.Returning)
            FaceTarget(startPos);
        else if (player != null)
            FaceTarget(player.position);
    }

    void IdleBehavior()
    {
        // Yumuşak hover hareketi
        float hoverY = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        Vector2 targetPos = (Vector2)startPos + Vector2.up * hoverY;
        rb.MovePosition(Vector2.Lerp(rb.position, targetPos, Time.fixedDeltaTime * 5f));
    }

    void CheckForPlayer()
    {
        if (Time.time < diveReadyAt) return;    // still catching its breath after the last dive

        Vector2 playerCenter = (Vector2)player.position + Vector2.up * 0.5f; // Oyuncunun göğsüne nişan al
        float dist = Vector2.Distance(transform.position, playerCenter);
        if (dist > detectRange) return;

        // ⚠️ Routed through EnemySenses rather than a local raycast, and that fixed a real bug: this
        // used to cast from `transform.position`, and `Physics2D.queriesStartInColliders` is ON — so
        // a bat hovering against a ceiling or ledge (which is where bats hang) started its ray INSIDE
        // that tile, got "blocked by Ground at distance 0.00", and never attacked once in its life.
        // EnemySenses skips the first StartSkip units for exactly this reason.
        if (!EnemySenses.CanSee(transform, player, obstacleLayer, 0f)) return;

        StartCoroutine(PrepareAttackRoutine());
    }

    // REAR BACK -> FLUSH -> COMMIT. The recoil is the telegraph: it pulls away from you along the
    // exact line it is about to come down, so the wind-up shows both THAT it is coming and WHERE.
    IEnumerator PrepareAttackRoutine()
    {
        currentState = State.Preparing;

        Vector2 aim = (Vector2)player.position + Vector2.up * 0.5f;
        Vector2 away = ((Vector2)transform.position - aim).normalized;
        prepFrom = rb.position;
        prepBack = prepFrom + away * windUpRecoil;
        prepK = 0f;

        float t = 0f;
        while (t < windUpTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windUpTime);

            // Pull back fast, then hang at the top of the wind-up — the hang is what makes the
            // release read as a release rather than as a drift. FixedUpdate consumes prepK.
            prepK = 1f - (1f - k) * (1f - k);

            // ...and flush hot over the same beat.
            SetTint(Color.Lerp(Color.white, windUpTint, prepK));

            // Stunned mid-wind-up: abort rather than launching a ghost dive.
            if (health != null && health.IsStunned)
            {
                SetTint(Color.white);
                currentState = State.Idle;
                diveReadyAt = Time.time + diveCooldown;
                yield break;
            }
            yield return null;
        }

        diveTarget = player.position;
        diveTimer = 0f;
        currentState = State.Diving;
    }

    void DiveBehavior()
    {
        diveTimer += Time.fixedDeltaTime;

        // Bleed the flush off over the first part of the lunge — the heat is spent on the strike.
        SetTint(Color.Lerp(windUpTint, Color.white, Mathf.Clamp01(diveTimer / 0.18f)));

        Vector2 dir = ((Vector2)diveTarget - rb.position).normalized;
        float speed = moveSpeed * diveSpeedMultiplier;
        rb.linearVelocity = dir * speed;

        if (Vector2.Distance(rb.position, diveTarget) < 0.5f || diveTimer > diveDuration)
        {
            currentState = State.Returning;
        }
    }

    void ReturnBehavior()
    {
        Vector2 dir = ((Vector2)startPos - rb.position).normalized;
        rb.linearVelocity = dir * moveSpeed;

        if (Vector2.Distance(rb.position, startPos) < 0.3f)
        {
            rb.linearVelocity = Vector2.zero;
            SetTint(Color.white);
            currentState = State.Idle;
            // ⚠️ Breathing room. Without it the bat re-acquires on the frame it arrives home and
            // dives again immediately — measured, that killed a full-health player in a few seconds
            // while the test was still being set up.
            diveReadyAt = Time.time + diveCooldown;
        }
    }

    void FaceTarget(Vector3 target)
    {
        if (pixelMonster == null) return;

        if (target.x < transform.position.x)
            pixelMonster.Facing = PixelMonster.FacingType.Left;
        else
            pixelMonster.Facing = PixelMonster.FacingType.Right;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && currentState == State.Diving)
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(damage);
                pc.ApplyKnockback((other.transform.position - transform.position).normalized * knockbackForce);
            }
            currentState = State.Returning;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}