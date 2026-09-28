using UnityEngine;
using Cainos.PixelArtMonster_Dungeon;

public class RangedEnemyAI : MonoBehaviour
{
    [Header("Yapay Zeka Ayarları")]
    public float aggroRange = 10f;
    public float attackRange = 7f;
    public float fireRate = 2.5f;

    [Header("Nişan Ayarları (YENİ)")]
    [Tooltip("Oyuncu okçudan en fazla ne kadar yukarıda/aşağıda olursa ateş etsin? (Y ekseni farkı)")]
    public float yTolerance = 1.5f;

    [Header("Line of Sight")]
    [Tooltip("What blocks sight. Left empty it falls back to the Ground layer — see EnemySenses.")]
    public LayerMask sightBlockers;

    // ⚠️ Every other walking enemy checks for floor before it steps; the archer did not, so an
    // archer on a ledge walked straight off it whenever the player was 7-10 tiles away (it closes
    // to attack range). 0.6 rather than the 0.5 the others use: from the centre of a ledge's last
    // cell, 0.5 probes EXACTLY the tile boundary and can read as floor (the Long Jump's spitter
    // walked off that way). 2026-09-28.
    [Header("Edges")]
    public float edgeCheckOffsetX = 0.6f;
    public float edgeCheckDepth = 1f;

    [Header("Ses")]
    // Played when the archer fires. PlayClipAtPoint requires no AudioSource component.
    [SerializeField] private AudioClip shootSound;
    [SerializeField, Range(0f, 1f)] private float shootVolume = 1f;

    private MonsterController controller;
    private EnemyHealth health;
    private PixelMonster pm; // Yüzünü dönmesi için eklendi
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
        float yDifference = Mathf.Abs(player.position.y - transform.position.y); // Yükseklik farkı

        // ⚠️ Line of sight with a short memory. Without it the archer drew and fired through solid
        // rock at a player it could not see. See EnemySenses.
        if (distance < aggroRange
            && EnemySenses.IsAware(transform, player, sightBlockers, ref lastSeen))
        {
            // YENİ: Motor dursa bile yüzünü her karede oyuncuya dön (Arkaya sıkma sorununu çözer)
            if (player.position.x > transform.position.x)
                pm.Facing = PixelMonster.FacingType.Right;
            else
                pm.Facing = PixelMonster.FacingType.Left;

            // MENZİLE GİRDİ Mİ?
            if (distance <= attackRange)
            {
                controller.inputMove.x = 0f; // Yürümeyi kes

                // YENİ: Yükseklik olarak oyuncuyla aynı hizada mıyız? (Boşa sıkmaması için)
                if (yDifference <= yTolerance)
                {
                    if (Time.time >= lastAttackTime + fireRate)
                    {
                        controller.inputAttack = true;
                        lastAttackTime = Time.time;

                        // Play the bow-shot sound when the archer fires.
                        SfxManager.PlayAtPoint(shootSound, transform.position, shootVolume);
                    }
                }
                // (Eğer menzilde ama yukarıdaysa sadece bekler ve oyuncuya bakar)
            }
            else
            {
                // MENZİLDE DEĞİL, YAKLAŞ! (but never off a ledge)
                float dir = player.position.x > transform.position.x ? 1f : -1f;
                if (!IsEdgeAhead(dir))
                    controller.inputMove.x = dir;
            }
        }
    }

    private bool IsEdgeAhead(float dirX)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(dirX * edgeCheckOffsetX, -0.1f);
        // Ground is also what blocks its sight, and ResolveBlockers falls back to it, so this needs
        // no Inspector slot that could be left empty (an empty mask would read every step as an edge).
        LayerMask ground = EnemySenses.ResolveBlockers(0);
        return Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDepth, ground).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}