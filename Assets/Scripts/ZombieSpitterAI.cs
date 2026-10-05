using UnityEngine;
using System.Collections;
using Cainos.PixelArtMonster_Dungeon;

// A ranged "spitter" zombie. It approaches the player until in range, then lobs a projectile at a
// fixed cadence. Built on the Cainos zombie rig, which has NO ranged-attack animation of its own —
// so it reuses the melee "attack" gesture as the spit windup and spawns the projectile itself on a
// timed delay. Same trigger-then-delayed-payload shape as MeleeEnemyAI.DealDamageRoutine, but the
// payload is a Projectile instead of a melee hit. Facing comes from PixelMonster; because the root
// transform never flips (only the visual child does), the spit ORIGIN is mirrored in code from the
// facing sign rather than parented to a firepoint that would sit on the wrong side.
public class ZombieSpitterAI : MonoBehaviour
{
    [Header("Yapay Zeka Ayarları")]
    public float aggroRange = 11f;
    public float attackRange = 8f;
    public float fireCooldown = 2.8f;
    [Tooltip("Only spit when the player is within this vertical band (stops it firing at nothing).")]
    public float yTolerance = 2.5f;

    [Header("Tükürük (Projectile)")]
    public GameObject projectilePrefab;
    public float projectileDamage = 8f;
    [Tooltip("Local height the glob leaves from (roughly the mouth).")]
    public float mouthHeight = 1.2f;
    [Tooltip("How far in front of the spitter the glob spawns (mirrored by facing).")]
    public float mouthForward = 0.6f;
    [Tooltip("Seconds after the spit gesture starts before the glob actually leaves.")]
    public float windupDelay = 0.35f;

    [Header("Edge Detection")]
    public LayerMask groundLayer;
    public float edgeCheckOffsetX = 0.5f;
    public float edgeCheckDepth = 1f;

    [Header("Ses")]
    // PlayAtPoint needs no AudioSource and survives the spitter dying mid-spit.
    [SerializeField] private AudioClip spitSound;
    [SerializeField, Range(0f, 1f)] private float spitVolume = 1f;

    private MonsterController controller;
    private EnemyHealth health;
    private PixelMonster pm;
    private Transform player;
    private float lastAttackTime;
    private float lastSeen = -999f;

    void Start()
    {
        controller = GetComponent<MonsterController>();
        health = GetComponent<EnemyHealth>();
        pm = GetComponent<PixelMonster>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null || controller == null || controller.IsDead)
        {
            if (controller != null) controller.inputMove = Vector2.zero;
            return;
        }

        controller.inputMove = Vector2.zero;
        controller.inputAttack = false;

        if (health != null && health.IsStunned) return;

        float distance = Vector2.Distance(transform.position, player.position);
        float yDiff = Mathf.Abs(player.position.y - transform.position.y);

        // ⚠️ Line of sight with a short memory. Without it this lobbed acid through solid rock at a
        // player it could not possibly see — the single worst offender, because a projectile coming
        // out of a wall reads as a bug rather than as an enemy. See EnemySenses.
        if (distance >= aggroRange
            || !EnemySenses.IsAware(transform, player, groundLayer, ref lastSeen, mouthHeight))
            return;

        // Face the player every frame (keeps working even while standing still to spit).
        if (pm != null)
            pm.Facing = player.position.x > transform.position.x
                ? PixelMonster.FacingType.Right
                : PixelMonster.FacingType.Left;

        if (distance <= attackRange)
        {
            controller.inputMove.x = 0f; // stop and spit

            if (yDiff <= yTolerance && Time.time >= lastAttackTime + fireCooldown)
            {
                controller.inputAttack = true;
                lastAttackTime = Time.time;

                SfxManager.PlayAtPoint(spitSound, transform.position, spitVolume);
                StartCoroutine(SpitRoutine());
            }
            // (In range but too far above/below → just wait and keep facing the player.)
        }
        else
        {
            // Not in range yet — walk toward the player, but never off a ledge.
            float dir = player.position.x > transform.position.x ? 1f : -1f;
            if (!IsEdgeAhead(dir))
                controller.inputMove.x = dir;
        }
    }

    // Waits out the spit gesture, then launches the glob from the mouth toward the player's chest.
    IEnumerator SpitRoutine()
    {
        yield return new WaitForSeconds(windupDelay);

        if (player == null || controller == null || controller.IsDead) yield break;
        if (projectilePrefab == null) yield break;

        float sign = (pm != null && pm.Facing == PixelMonster.FacingType.Left) ? -1f : 1f;
        Vector2 origin = (Vector2)transform.position + new Vector2(sign * mouthForward, mouthHeight);
        Vector2 dir = ((Vector2)player.position + Vector2.up * 0.6f - origin).normalized;

        GameObject go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        Projectile proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.damage = projectileDamage;
            proj.Launch(dir);
        }
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
