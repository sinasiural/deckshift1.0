using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

public class SlimeAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float aggroRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.2f;

    [Header("Damage Settings")]
    public float damage = 10f;
    public float damageDelay = 0.2f;
    public float knockbackPower = 4f;
    [Tooltip("How tall the lunge reaches. A slime is short, so this is lower than a humanoid's.")]
    public float attackHeight = 1.2f;

    [Header("Idle Patrol")]
    public float patrolFlipInterval = 2.5f;

    [Header("Edge Detection")]
    public LayerMask groundLayer;
    public float edgeCheckOffsetX = 0.5f;
    public float edgeCheckDepth = 1f;

    [Header("Ses")]
    // Played at the start of each attack. PlayClipAtPoint survives enemy destroy.
    [SerializeField] private AudioClip attackSound;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;

    // A slime is low to the ground, so it looks out from lower down than a humanoid does. Too high
    // and the ray leaves from above its own head; too low and it starts inside the floor tile.
    private const float EyeHeight = 0.5f;

    private MonsterController controller;
    private EnemyHealth health;
    private PixelMonster pm;
    private Transform player;
    private float lastAttackTime;
    private float patrolTimer;
    private float patrolDir = 1f;
    private float lastSeen = -999f;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        health = GetComponent<EnemyHealth>();
        pm = GetComponent<PixelMonster>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        patrolTimer = patrolFlipInterval;
    }

    void Update()
    {
        if (controller == null || controller.IsDead)
        {
            if (controller != null) controller.inputMove = Vector2.zero;
            return;
        }

        controller.inputMove = Vector2.zero;
        controller.inputJump = false;
        controller.inputAttack = false;

        if (health != null && health.IsStunned) return;

        if (player == null)
        {
            Patrol();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        // ⚠️ Line of sight with a short memory — a slime on the far side of a wall goes back to
        // patrolling instead of grinding into the rock. See EnemySenses.
        bool aware = distance < aggroRange
                     && EnemySenses.IsAware(transform, player, groundLayer, ref lastSeen, EyeHeight);

        if (aware)
        {
            // ⚠️ FACE THE PLAYER EVERY FRAME. MonsterController only updates facing while
            // inputMove.x is non-zero (MonsterController.cs:226), and the attack branch below sets
            // it to ZERO — so without this a slime visibly lunges BACKWARDS at a player who got
            // behind it, while EnemyMelee still resolves the hit on the player's real side.
            if (pm != null)
                pm.Facing = player.position.x > transform.position.x
                    ? PixelMonster.FacingType.Right
                    : PixelMonster.FacingType.Left;

            if (distance > attackRange)
            {
                float dir = player.position.x > transform.position.x ? 1f : -1f;
                if (!IsEdgeAhead(dir))
                    controller.inputMove.x = dir;
            }
            else
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    controller.inputAttack = true;
                    lastAttackTime = Time.time;

                    // Play the attack sound at the moment the attack starts.
                    SfxManager.PlayAtPoint(attackSound, transform.position, attackVolume);

                    StartCoroutine(DealDamageRoutine(player.position.x >= transform.position.x ? 1f : -1f));
                }
            }
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f || IsEdgeAhead(patrolDir))
        {
            patrolDir = -patrolDir;
            patrolTimer = patrolFlipInterval;
        }
        controller.inputMove.x = patrolDir;
    }

    IEnumerator DealDamageRoutine(float swingDir)
    {
        yield return new WaitForSeconds(damageDelay);
        if (player == null || controller.IsDead) yield break;

        // A box in front, not a circle around the slime's feet with a hidden +0.5. See EnemyMelee.
        EnemyMelee.TryHit(transform, swingDir, attackRange, damage, knockbackPower, attackHeight);
    }

    private bool IsEdgeAhead(float dirX)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(dirX * edgeCheckOffsetX, -0.1f);
        return Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDepth, groundLayer).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        EnemyMelee.DrawGizmo(transform, attackRange, attackHeight);
    }
}
