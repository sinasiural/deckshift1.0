using UnityEngine;

/// <summary>
/// The Ninja boss's thrown katana. It is a weapon, but its real job is to be a TELEGRAPH: it sticks
/// where it lands and sits there for a beat before he blinks to it, so the player gets to see where
/// he will be *before he is there*. Look at the sword, not the man. See BossDesign_Ninja.md §6.
///
/// House pattern: built entirely in code, no prefab. Terrain is found by raycast for the same reason
/// BossShuriken does it — a trigger test at this speed can straddle a 1-tile wall and tunnel through.
/// </summary>
public class BossKatana : MonoBehaviour
{
    private const float SPEED = 19f;
    private const float LIFE = 3f;          // in flight only
    private const float WORLD_LEN = 1.15f;
    private const float EMBED = 0.22f;

    private float damage;
    private bool stuck;
    // ⚠️ THIS ONLY MEANS "HAS ALREADY HURT THE PLAYER". It must NEVER gate the terrain check below.
    // It originally did, and the consequence was severe: a blade that clipped the player on the way
    // past stopped looking for walls, flew off the map forever, and the boss then BLINKED TO IT —
    // teleporting himself out of the arena. Passing through someone is not the end of its flight.
    private bool hasHurtPlayer;
    private Vector2 lastPos;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float pulse;

    /// <summary>Where it came to rest, for the boss to blink to. Only meaningful once Stuck.</summary>
    public bool Stuck => stuck;

    public static BossKatana Spawn(Vector3 pos, Vector2 dir, float damage, Sprite art)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        var go = new GameObject("BossKatana");
        go.transform.position = new Vector3(pos.x, pos.y, PlayPlane.Z);

        int layer = LayerMask.NameToLayer("Projectile");
        if (layer >= 0) go.layer = layer;
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art;
        sr.color = Color.white;
        sr.sortingOrder = 5;

        // Sized from the sprite's own bounds, never a fixed scale — the same rule the dash lane
        // violated and the shuriken gets right. A generated or re-imported sprite is not 1x1.
        if (art != null)
        {
            float native = Mathf.Max(art.bounds.size.x, art.bounds.size.y);
            if (native > 0.001f) go.transform.localScale = Vector3.one * (WORLD_LEN / native);
        }
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearVelocity = dir * SPEED;

        var col = go.AddComponent<CircleCollider2D>();
        float s = go.transform.localScale.x;
        col.radius = s > 0.001f ? 0.30f / s : 0.30f;
        col.isTrigger = true;

        var k = go.AddComponent<BossKatana>();
        k.damage = damage;
        k.rb = rb;
        k.sr = sr;
        k.lastPos = go.transform.position;
        return k;
    }

    private void Update()
    {
        if (!stuck) return;
        // A slow shine so the landed blade reads as "he is coming HERE" rather than as scenery.
        pulse += Time.deltaTime * 4f;
        float g = 0.82f + 0.18f * Mathf.Sin(pulse);
        sr.color = new Color(g, g, g, 1f);
    }

    private void FixedUpdate()
    {
        if (stuck) return;

        Vector2 now = transform.position;
        Vector2 delta = now - lastPos;
        if (delta.sqrMagnitude > 0.000001f)
        {
            var hit = Physics2D.Raycast(lastPos, delta.normalized, delta.magnitude, LayerMask.GetMask("Ground"));
            if (hit.collider != null) { Stick(hit.point); return; }
        }
        lastPos = now;

        if ((Time.time - spawnTime) > LIFE) Stick(now);   // never leave the boss with nowhere to blink
    }

    private float spawnTime;
    private void Start() { spawnTime = Time.time; }

    private void Stick(Vector2 point)
    {
        stuck = true;
        Vector2 flight = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.right;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // ⚠️ Z carried through by hand. `point` is a Vector2 and assigning one to transform.position
        // implicitly zeroes Z, which pulls the blade off the play plane and behind the props.
        Vector2 rest = point + flight * EMBED;
        transform.position = new Vector3(rest.x, rest.y, PlayPlane.Z);

        var col = GetComponent<CircleCollider2D>();
        if (col != null) col.enabled = false;   // landed: it is an anchor now, not a weapon
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHurtPlayer || stuck) return;
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        hasHurtPlayer = true;
        pc.TakeDamage(damage);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.10f, 0.22f);
        // It keeps going — a thrown sword that stops dead in someone reads as a pin, and the boss
        // still needs it to land somewhere he can blink to.
    }

    /// <summary>He has arrived and picked it back up.</summary>
    public void Reclaim() { Destroy(gameObject); }
}
