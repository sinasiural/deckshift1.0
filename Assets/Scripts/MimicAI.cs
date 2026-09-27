using System.Collections;
using UnityEngine;
using Cainos.PixelArtMonster_Dungeon;

public class MimicAI : MonoBehaviour
{
    private enum MimicState { Hidden, Revealing, Chasing, Dead }

    [Header("Reveal Settings")]
    [SerializeField] private float revealRadius = 2.5f;
    [SerializeField] private float revealStunDuration = 0.7f;

    [Header("AI Settings")]
    [SerializeField] private float aggroRange = 8f;
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Damage Settings")]
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float damageDelay = 0.3f;
    [SerializeField] private float knockbackPower = 5f;
    [Tooltip("How tall the bite reaches. A chest is low to the ground.")]
    [SerializeField] private float attackHeight = 1.3f;

    [Header("Edge Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckOffsetX = 0.5f;
    [SerializeField] private float edgeCheckDepth = 1f;

    // A chest is low to the ground, so it looks out from lower down than a humanoid does.
    private const float EyeHeight = 0.6f;

    private MimicState state = MimicState.Hidden;
    private float lastAttackTime;
    private float lastSeen = -999f;

    private MonsterController controller;
    private PixelMonster pixelMonster;
    private EnemyHealth enemyHealth;
    private PlayerController playerController;
    private Transform player;

    private void Awake()
    {
        controller = GetComponent<MonsterController>();
        pixelMonster = GetComponent<PixelMonster>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (pixelMonster != null)
            pixelMonster.IsHiding = true;

        // A chest with a health bar over it is not a disguise.
        if (enemyHealth != null)
            enemyHealth.SetBarConcealed(true);
    }

    private void Start()
    {
        if (controller == null || pixelMonster == null || enemyHealth == null)
        {
            Debug.LogWarning($"[MimicAI] '{name}': missing required component (MonsterController, PixelMonster, or EnemyHealth) — MimicAI disabled.");
            enabled = false;
            return;
        }

        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            playerController = GameManager.instance.player.GetComponent<PlayerController>();
            player = playerController != null ? playerController.transform : null;
        }

        if (player == null)
        {
            Debug.LogWarning($"[MimicAI] '{name}': could not find player via GameManager — MimicAI disabled.");
            enabled = false;
            return;
        }

        enemyHealth.OnDamaged += OnEnemyDamaged;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDamaged -= OnEnemyDamaged;
    }

    private void Update()
    {
        if (player == null || controller == null || controller.IsDead)
        {
            if (controller != null) controller.inputMove = Vector2.zero;
            return;
        }

        controller.inputMove = Vector2.zero;
        controller.inputAttack = false;

        switch (state)
        {
            case MimicState.Hidden:
                UpdateHidden();
                break;
            case MimicState.Revealing:
                // RevealRoutine coroutine owns this transition — nothing to drive here.
                break;
            case MimicState.Chasing:
                UpdateChasing();
                break;
            case MimicState.Dead:
                // controller.IsDead check above already returns early; this branch is never reached.
                break;
        }
    }

    private void UpdateHidden()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= revealRadius)
            Reveal();
    }

    private void UpdateChasing()
    {
        if (enemyHealth.IsStunned)
        {
            controller.inputMove = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        // ⚠️ Line of sight with a short memory. Deliberately applied only to CHASING — the reveal
        // in UpdateHidden is a proximity trap and must still spring when you walk onto the chest,
        // wall or no wall. See EnemySenses.
        if (distance >= aggroRange
            || !EnemySenses.IsAware(transform, player, groundLayer, ref lastSeen, EyeHeight))
            return;

        // ⚠️ FACE THE PLAYER EVERY FRAME. MonsterController only updates facing while inputMove.x
        // is non-zero (MonsterController.cs:226), and the attack branch below sets it to ZERO — so
        // without this the mimic visibly bites BACKWARDS at a player who got behind it, while
        // EnemyMelee still resolves the hit on the player's real side.
        if (pixelMonster != null)
            pixelMonster.Facing = player.position.x > transform.position.x
                ? PixelMonster.FacingType.Right
                : PixelMonster.FacingType.Left;

        if (distance <= attackRange)
        {
            controller.inputMove.x = 0f;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                controller.inputAttack = true;
                lastAttackTime = Time.time;
                StartCoroutine(DealDamageRoutine(player.position.x >= transform.position.x ? 1f : -1f));
            }
        }
        else
        {
            float dir = player.position.x > transform.position.x ? 1f : -1f;
            if (!IsEdgeAhead(dir))
                controller.inputMove.x = dir;
        }
    }

    private void Reveal()
    {
        if (state != MimicState.Hidden) return;
        state = MimicState.Revealing;
        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        pixelMonster.IsHiding = false;
        enemyHealth.SetBarConcealed(false);
        controller.inputMove = Vector2.zero;
        controller.inputAttack = false;
        yield return new WaitForSeconds(revealStunDuration);
        state = MimicState.Chasing;
    }

    private void OnEnemyDamaged()
    {
        if (state == MimicState.Hidden)
            Reveal();
    }

    private IEnumerator DealDamageRoutine(float swingDir)
    {
        yield return new WaitForSeconds(damageDelay);
        if (player == null || controller.IsDead) yield break;

        // A box in front, not a circle around the mimic's feet with a hidden +0.5. See EnemyMelee.
        EnemyMelee.TryHit(transform, swingDir, attackRange, attackDamage, knockbackPower, attackHeight);
    }

    private bool IsEdgeAhead(float dirX)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(dirX * edgeCheckOffsetX, -0.1f);
        return Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDepth, groundLayer).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, revealRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        EnemyMelee.DrawGizmo(transform, attackRange, attackHeight);
    }
}
