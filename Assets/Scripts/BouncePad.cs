using UnityEngine;

/// <summary>
/// Bounce House (relic): an enemy you kill leaves a springy pad behind for a few seconds.
///
/// The relic's job is to pay MOVEMENT for combat — the game's stated thesis is that movement is a
/// resource, and nothing else in the roster turns a fight into terrain.
///
/// ⚠️ BUILT ENTIRELY IN CODE, NO PREFAB. Same house pattern as DashAfterimage, ShockwaveVFX,
/// SpitGlob and ScrapPickup: there is no bounce-pad art in either Cainos pack, and a procedural one
/// costs nothing, cannot be lost from a scene, and needs no wiring.
///
/// ⚠️ THE BOUNCE IS FREE — IT COSTS NO SHIFT, AND THAT IS DELIBERATE. It is not a jump: you cannot
/// summon a pad on demand, it needs a corpse, it expires, and it only fires when you are already
/// falling onto it. Charging Shift would make it strictly worse than the jump it replaces.
/// </summary>
public class BouncePad : MonoBehaviour
{
    public const float Lifetime = 5f;
    public const float BounceForce = 15f;
    private const float Radius = 0.85f;

    private float born;
    private float lastBounce = -1f;
    private SpriteRenderer sr;
    private static Sprite cachedSprite;

    public static void Spawn(Vector3 position)
    {
        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("BounceHouse")) return;

        GameObject go = new GameObject("BouncePad");
        go.transform.position = position;
        // Actors live on the play plane; the pad is something the player touches, so it belongs there.
        go.transform.position = new Vector3(position.x, position.y, PlayPlane.Z);
        go.AddComponent<TemporaryObject>();   // swept on room change like every runtime spawn

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Radius;

        go.AddComponent<BouncePad>();
    }

    private void Awake()
    {
        born = Time.time;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PadSprite();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 50;
        sr.color = new Color(0.45f, 0.95f, 0.55f, 0.9f);
        transform.localScale = Vector3.one * (Radius * 2f);
    }

    private void Update()
    {
        float age = Time.time - born;
        if (age >= Lifetime) { Destroy(gameObject); return; }

        // Idle breathing, plus a fade over the last second so its expiry is visible rather than
        // sprung on the player mid-leap.
        float pulse = 1f + Mathf.Sin(age * 7f) * 0.08f;
        transform.localScale = Vector3.one * (Radius * 2f) * pulse;

        float fade = Mathf.Clamp01((Lifetime - age) / 1f);
        if (sr != null) sr.color = new Color(0.45f, 0.95f, 0.55f, 0.9f * fade);
    }

    private void OnTriggerEnter2D(Collider2D other) { TryBounce(other); }
    private void OnTriggerStay2D(Collider2D other) { TryBounce(other); }

    private void TryBounce(Collider2D other)
    {
        if (Time.time - lastBounce < 0.25f) return;

        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // Only when moving INTO the pad. Without this the player is launched while walking across
        // it on the ground, which reads as the pad grabbing them rather than as a bounce.
        // Flipped under gravity reversal, exactly like the head bounce.
        bool falling = pc.isGravityReversed ? rb.linearVelocity.y > 0.1f : rb.linearVelocity.y < -0.1f;
        if (!falling) return;

        lastBounce = Time.time;
        float dir = pc.isGravityReversed ? -1f : 1f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * BounceForce * dir, ForceMode2D.Impulse);

        if (CameraShake.instance != null) CameraShake.instance.Shake(0.06f, 0.12f);
    }

    // A soft filled disc with a brighter rim — reads as springy rather than as a flat marker.
    private static Sprite PadSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        const int S = 64;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (S * 0.5f);
                float a = d > 1f ? 0f : Mathf.SmoothStep(1f, 0f, Mathf.Max(0f, (d - 0.55f) / 0.45f));
                float rim = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.10f, 2f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a * 0.55f + rim * 0.9f)));
            }
        tex.Apply();

        cachedSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedSprite;
    }
}
