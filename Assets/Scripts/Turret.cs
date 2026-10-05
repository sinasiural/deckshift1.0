using System.Collections;
using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Referanslar")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Ate� Ayarlar�")]
    public float fireRate = 2f;

    [Header("Targeting")]
    // ⚠️ THIS USED TO HAVE NEITHER. FireRoutine was a bare `while(true)` that fired every `fireRate`
    // seconds from the moment the room spawned, at ANY distance, THROUGH ANY AMOUNT OF ROCK. The
    // bolt travels Projectile.speed x Projectile.lifeTime = 10 x 3 = 30 world units, and the screen
    // is only about 25 across — so turrets were shooting the player from off screen, out of walls,
    // for the entire time the room was loaded. Of every enemy in the game this was the one most
    // likely to read as "the game is attacking me at random".
    [Tooltip("Don't fire at a player further away than this.")]
    public float range = 13f;
    [Tooltip("What blocks the shot. Left empty it falls back to the Ground layer — see EnemySenses.")]
    public LayerMask sightBlockers;

    private Transform playerTransform;
    private EnemyHealth health;
    private float lastSeen = -999f;

    // Reference lookups live in Awake so they exist before OnEnable's coroutine runs.
    // The firing loop is owned exclusively by OnEnable/OnDisable — starting it here too
    // ran two overlapping loops (double fire rate), per audit_report.md High 1.5.
    void Awake()
    {
        health = GetComponent<EnemyHealth>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
    }

    private IEnumerator FireRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireRate);
            if (health != null && health.IsStunned) continue;
            Fire();
        }
    }

    private void Fire()
    {
        if (playerTransform == null || projectilePrefab == null || firePoint == null) return;

        // Only shoot at something it can actually reach and actually see. The sight ray is cast
        // from the FIREPOINT, not the turret's origin: that is where the bolt leaves from, so it is
        // the only line whose being blocked means the shot would hit a wall.
        if (Vector2.Distance(firePoint.position, playerTransform.position) > range) return;
        if (!EnemySenses.IsAware(firePoint, playerTransform, sightBlockers, ref lastSeen, 0f)) return;

        Vector2 targetPosition = playerTransform.position;
        Vector2 fireDirection = (targetPosition - (Vector2)firePoint.position).normalized;

        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Launch(fireDirection);
        }
    }
    // Turret.cs i�ine ekle:
    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void OnEnable()
    {
        StartCoroutine(FireRoutine());
    }
}