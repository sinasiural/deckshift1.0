using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Ninja boss's thrown star — and the arena's answer to "how do I fight this thing with an empty
/// deck?". It flies, it hurts, and when it MISSES it sticks in whatever it hit and becomes something
/// the player can pick up and throw back. See BossDesign_Ninja.md §4.
///
/// House pattern: built entirely in code, sprite included (like Shuriken, ScrapPickup and SpitGlob),
/// so there is no prefab to wire and nothing to lose out of a scene.
///
/// ⚠️ THE BOSS RECALLS THE ONES YOU DON'T TAKE. That is not decoration — without it the degenerate
/// line is to ignore the fight and farm the floor, and the volley → scramble → recall rhythm is the
/// loop the whole encounter is built on.
/// </summary>
public class BossShuriken : MonoBehaviour
{
    // ---- tuning ---------------------------------------------------------------------------------
    private const float SPEED       = 15f;    // slower than the player's 22 — this one must be dodgeable
    private const float SPIN        = 1440f;
    private const float LIFE        = 4f;     // in flight only; a stuck star waits for the recall
    private const float WORLD_SIZE  = 0.62f;
    private const float PICKUP_R    = 0.85f;  // forgiving: collecting under fire must not need precision
    private const float EMBED       = 0.12f;  // how far into the surface it buries, so it reads as stuck

    private static readonly Color Steel = new Color(0.82f, 0.86f, 0.92f, 1f);

    // ⚠️ THE STAR CHANGES SIDES, AND ITS COLOUR SAYS SO. This is the only object in the game that is
    // an ATTACK and then becomes AMMO, and the player has to read which it currently is at a glance,
    // mid-fight, from across an arena. So it carries two states and nothing else:
    //
    //     in flight   Wound red   — his. dodge it.
    //     on the floor  Torch gold — yours. take it.
    //
    // Both are already the game's own accents (Salvage §4: Torch and Shift are the only two accents,
    // and Wound is the warning), so this spends no new hue. Red also matches his dash lane, which is
    // the other thing on screen that means "he is about to hurt you there".
    private static readonly Color ThreatKey = new Color(1f, 0.34f, 0.32f, 0.60f);
    private static readonly Color ThreatHot = new Color(1f, 0.50f, 0.44f, 1f);
    private static readonly Color ThreatCool = new Color(0.42f, 0.12f, 0.13f, 1f);
    private static readonly Color LootKey = new Color(Salvage.Torch.r, Salvage.Torch.g, Salvage.Torch.b, 0.85f);

    private SpriteRenderer keyline;

    // ⚠️ EVERY LIVE STAR IS REGISTERED so the boss can recall them without holding references that
    // could go stale when one is collected or swept on a room change. Entries remove themselves in
    // OnDestroy, and every read prunes nulls anyway — a static list that outlives a domain reload is
    // a known trap in this project.
    private static readonly List<BossShuriken> live = new List<BossShuriken>();

    private float damage;
    private bool stuck;
    private bool spent;              // hit something or been collected; ignore everything after
    private Vector2 lastPos;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float bobPhase;

    /// <summary>Stars currently embedded in the arena and available to pick up.</summary>
    public static int StuckCount
    {
        get
        {
            int n = 0;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i] == null) { live.RemoveAt(i); continue; }
                if (live[i].stuck) n++;
            }
            return n;
        }
    }

    /// <param name="art">
    /// The pack's own shuriken sprite when the boss has one, so the star that flies is the star he
    /// was holding. Null falls back to the shared procedural star.
    /// </param>
    public static BossShuriken Spawn(Vector3 pos, Vector2 dir, float damage, Sprite art = null)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        var go = new GameObject("BossShuriken");
        go.transform.position = pos;

        // ⚠️ The Projectile layer is what lets the trigger reach the player — the same requirement
        // SpitGlob documents. Terrain is NOT detected by trigger (see FixedUpdate), so this does not
        // depend on how Projectile is set up against Ground in the collision matrix.
        int layer = LayerMask.NameToLayer("Projectile");
        if (layer >= 0) go.layer = layer;

        // Swept on a room change with every other runtime spawn. Without it this is a scene-root
        // object that outlives the room that threw it — the bug ClearRuntimeSpawns exists for.
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art != null ? art : Shuriken.StarSprite;
        sr.color = Color.white;
        sr.sortingOrder = 5;

        // Sized from the sprite's own bounds, never a hard-coded scale — the pack's shuriken and the
        // procedural fallback have different resolutions and PPU. Same rule as Shuriken.Spawn.
        float native = sr.sprite.bounds.size.x;
        go.transform.localScale = Vector3.one * (native > 0.001f ? WORLD_SIZE / native : 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearVelocity = dir * SPEED;

        var col = go.AddComponent<CircleCollider2D>();
        float scale = go.transform.localScale.x;
        col.radius = scale > 0.001f ? 0.26f / scale : 0.26f;
        col.isTrigger = true;

        // ⚠️ SNAPPED ONTO THE PLAY PLANE. Actors live at PlayPlane.Z and everything else is behind
        // them; a star left at whatever depth its thrower happened to occupy renders behind the
        // arena's props. Physics2D ignores Z entirely, so this costs nothing but correct sorting.
        Vector3 onPlane = go.transform.position;
        onPlane.z = PlayPlane.Z;
        go.transform.position = onPlane;

        var s = go.AddComponent<BossShuriken>();
        s.damage = damage;
        s.rb = rb;
        s.sr = sr;
        s.lastPos = onPlane;

        // Both shared with the player's Shuriken so the two can never drift apart visually — it is
        // literally the same object changing hands, and one implementation is what guarantees that.
        s.keyline = Shuriken.AttachKeyline(go.transform, sr, ThreatKey);
        Shuriken.AttachStreak(go.transform, sr.sortingOrder, ThreatHot, ThreatCool, 0.75f);

        return s;
    }

    private void OnEnable()  { if (!live.Contains(this)) live.Add(this); }
    private void OnDestroy() { live.Remove(this); }

    private void Start()
    {
        // Only the flight is time-limited. A stuck star has no expiry of its own — the boss's recall
        // is what clears the floor, so the player always knows why their ammo left.
        StartCoroutine(ExpireInFlight());
    }

    private IEnumerator ExpireInFlight()
    {
        yield return new WaitForSeconds(LIFE);
        if (!stuck && !spent) Destroy(gameObject);
    }

    private void Update()
    {
        if (stuck)
        {
            // A slow glint so an embedded star reads as loot rather than as scenery. Deliberately
            // brightness, not motion: a bobbing star would look like it had NOT stuck in anything.
            //
            // ⚠️ THE PULSE IS ON THE KEYLINE, NOT THE STAR. Brightening the sprite itself was all
            // this used to do, and against dark stone that is a dark object getting slightly less
            // dark — the change was real and invisible. Pulsing the lit edge behind it is the same
            // idea applied where there is actually contrast to gain.
            bobPhase += Time.deltaTime * 3.2f;
            float k = 0.78f + 0.22f * Mathf.Sin(bobPhase);
            sr.color = new Color(Steel.r * k + (1f - k), Steel.g * k + (1f - k), Steel.b * k + (1f - k), 1f);
            if (keyline != null)
                keyline.color = new Color(LootKey.r, LootKey.g, LootKey.b, LootKey.a * (0.55f + 0.45f * k));
            return;
        }
        transform.Rotate(0f, 0f, SPIN * Time.deltaTime);
    }

    // ⚠️ TERRAIN IS FOUND BY RAYCAST, NOT BY TRIGGER. At 15 u/s a fixed step covers ~0.3 units, and a
    // trigger against a 1-tile wall can be missed entirely if a step straddles it — a star that
    // tunnels through the arena wall is ammo the player can never reach. Sweeping from the previous
    // position also gives the exact impact point and normal, which is what lets it embed properly
    // instead of stopping in mid-air near a wall.
    private void FixedUpdate()
    {
        if (stuck || spent) return;

        Vector2 now = transform.position;
        Vector2 delta = now - lastPos;
        if (delta.sqrMagnitude > 0.000001f)
        {
            RaycastHit2D hit = Physics2D.Raycast(lastPos, delta.normalized, delta.magnitude,
                                                 LayerMask.GetMask("Ground"));
            if (hit.collider != null) { Stick(hit.point, hit.normal); return; }
        }
        lastPos = now;
    }

    private void Stick(Vector2 point, Vector2 normal)
    {
        stuck = true;

        Vector2 flight = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : Vector2.right;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        // Buried a little way into the surface along its own flight line — a star resting exactly on
        // the contact point looks like it is balancing on the wall rather than driven into it.
        //
        // ⚠️ Z IS CARRIED THROUGH BY HAND. `point` is a Vector2 from the raycast, and assigning a
        // Vector2 to transform.position implicitly sets z = 0 — which yanked the star off the play
        // plane (-2) the instant it embedded, so it rendered behind the arena's props. Caught in
        // play-mode testing, not by the compiler: the implicit conversion is perfectly legal.
        Vector2 rest = point + flight * EMBED;
        transform.position = new Vector3(rest.x, rest.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flight.y, flight.x) * Mathf.Rad2Deg);

        // Widen the trigger so collecting it under fire doesn't demand precision. The player has
        // enough to think about crossing this arena.
        var col = GetComponent<CircleCollider2D>();
        float scale = Mathf.Max(0.001f, transform.localScale.x);
        if (col != null) col.radius = PICKUP_R / scale;

        // It has changed sides. Red meant "his"; gold means "yours".
        //
        // ⚠️ THE RIM GROWS AS WELL AS CHANGING COLOUR. Photographed side by side, red-in-flight and
        // gold-on-the-floor are both "a dark star with a warm edge" and are only about as different
        // as two warm hues ever are at 30 pixels across. Widening the lit edge makes a planted star
        // read as something GLOWING rather than something outlined — two signals instead of one, and
        // the size difference survives at a glance across an arena where the hue alone might not.
        if (keyline != null)
        {
            keyline.color = LootKey;
            keyline.transform.localScale = Vector3.one * 1.55f;
        }

        // ⚠️ POSITIONAL, unlike the boss's own ability sounds. Where a star landed is information —
        // it is where the ammo now is — so it should get quieter across the arena rather than being
        // flat 2D. Quiet, because a volley plants four to six of these within a second.
        SfxManager.PlayAtPoint(ProcSfx.ShurikenStick, transform.position, 0.55f);

        Impact(transform.position, 4, 0.7f);
    }

    private void OnTriggerEnter2D(Collider2D other) { Touch(other); }
    private void OnTriggerStay2D(Collider2D other)  { Touch(other); }

    private void Touch(Collider2D other)
    {
        if (spent) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        if (stuck) { Collect(player); return; }

        // In flight: it bites, and it is gone. A star that hit you is not one you get to throw back.
        spent = true;
        player.TakeDamage(damage);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.08f, 0.20f);
        Impact(transform.position, 7, 1f);
        Destroy(gameObject);
    }

    private void Collect(PlayerController player)
    {
        spent = true;
        if (DeckManager.instance != null) DeckManager.instance.AddSalvagedShuriken(1);
        SfxManager.PlayAtPoint(ProcSfx.ShurikenCatch, transform.position, 0.9f);
        Impact(transform.position, 5, 0.8f);
        Destroy(gameObject);
    }

    // ---- recall ---------------------------------------------------------------------------------

    /// <summary>
    /// Every star still on the floor tears back to the boss. Stars still IN FLIGHT are left alone —
    /// yanking a star out of the air mid-throw reads as the game deleting an attack.
    /// </summary>
    public static int RecallAll(Transform to)
    {
        int n = 0;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (live[i] == null) { live.RemoveAt(i); continue; }
            if (!live[i].stuck || live[i].spent) continue;
            live[i].BeginRecall(to);
            n++;
        }
        return n;
    }

    private void BeginRecall(Transform to)
    {
        spent = true;                       // no longer collectible the moment it starts moving
        StartCoroutine(RecallRoutine(to));
    }

    private IEnumerator RecallRoutine(Transform to)
    {
        // A beat of hesitation before it moves, so the player can SEE the recall happen and learn
        // the rule. Snapping every star home on one frame just looks like they despawned.
        yield return new WaitForSeconds(Random.Range(0f, 0.18f));

        var col = GetComponent<CircleCollider2D>();
        if (col != null) col.enabled = false;
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        stuck = false;
        // Back to being his. The colour flip is the clearest possible statement that this one is no
        // longer collectable, and it happens on the frame it stops being collectable.
        if (keyline != null)
        {
            keyline.color = ThreatKey;
            keyline.transform.localScale = Vector3.one * 1.30f;
        }

        Vector3 from = transform.position;
        float t = 0f;
        const float DUR = 0.42f;

        while (t < DUR)
        {
            if (to == null) { Destroy(gameObject); yield break; }
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DUR);
            // Ease IN: it is being pulled, so it starts reluctantly and arrives fast.
            transform.position = Vector3.Lerp(from, to.position + Vector3.up * 1f, k * k);
            transform.Rotate(0f, 0f, SPIN * 1.4f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Short radial streaks. Its own object, because this star is destroyed on the same frame — the
    // rule EnemyHealth.Die's VFX has to follow too.
    private static void Impact(Vector3 pos, int count, float scale)
    {
        var root = new GameObject("ShurikenSparks");
        root.transform.position = pos;
        root.AddComponent<TemporaryObject>();
        root.AddComponent<SparkFade>();

        for (int i = 0; i < count; i++)
        {
            var s = new GameObject("Spark");
            s.transform.SetParent(root.transform, false);

            float ang = Random.Range(0f, 360f);
            float len = Random.Range(0.16f, 0.34f) * scale;
            s.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            s.transform.localPosition = Quaternion.Euler(0f, 0f, ang) * Vector3.right * (len * 0.6f);
            s.transform.localScale = new Vector3(len, Random.Range(0.03f, 0.055f) * scale, 1f);

            var sr = s.AddComponent<SpriteRenderer>();
            sr.sprite = FlatUI.Pixel();
            sr.color = new Color(1f, 0.97f, 0.88f, 1f);
            sr.sortingOrder = 7;
        }
    }
}
