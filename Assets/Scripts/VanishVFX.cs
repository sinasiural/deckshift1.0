using UnityEngine;

// The Ninja's Old Log Trick, seen from outside: a puff of smoke where he stood, and a log dropping
// out of it onto the floor (see PlayerHealth.Vanish). House pattern, like SparkBurst and BraceVFX:
// built entirely in code, so there is no prefab to wire and nothing to lose out of a scene.
//
// ⚠️ THE SMOKE IS DRAWN AS PIXEL ART, not with the dungeon pack's own smoke texture. That one is a
// soft 256px particle blob, and a smooth gradient next to hard pixel art is a different drawing
// language (the same reason the shuriken's ribbon trail was replaced). Each puff is a 12px disc,
// point-filtered, lit from the upper left like everything in Salvage.
public static class VanishVFX
{
    private const int PUFFS = 8;
    private const float CHEST = 0.85f;          // the player's capsule is ~1.7 tall, pivot at the feet
    private const float BEHIND = 0.0005f;       // just behind the play plane: behind the player, in front of the room

    private static Sprite puffSprite;
    private static Material puffMaterial;
    private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> decoys =
        new System.Collections.Generic.Dictionary<Sprite, Sprite>();

    // The world-prop outline: the darkest brown in the log's own shading, so it reads as the same
    // object rather than as a stroke drawn round it.
    private static readonly Color32 PropOutline = new Color32(38, 26, 20, 255);

    public static void Play(Vector3 feet, Sprite decoy)
    {
        Vector3 chest = new Vector3(feet.x, feet.y + CHEST, PlayPlane.Z + BEHIND);

        for (int i = 0; i < PUFFS; i++)
        {
            // Spread round the body, a little wider than tall, so the puff swallows the silhouette.
            Vector2 off = Random.insideUnitCircle;
            Vector3 at = chest + new Vector3(off.x * 0.55f, off.y * 0.75f, 0f);
            Vector2 drift = new Vector2(off.x * 1.1f, 0.6f + Random.Range(0f, 0.6f));
            Puff(at, drift, Random.Range(0.32f, 0.5f), Random.Range(0.4f, 0.6f));
        }

        if (decoy != null) Log(chest, decoy);
    }

    private static void Puff(Vector3 at, Vector2 drift, float size, float life)
    {
        var go = new GameObject("VanishPuff");
        go.transform.position = at;
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PuffSprite();
        // Unlit, like the player rig (which Light2D does not light), so the smoke matches the body
        // it hides instead of sinking into a dark room. Grey, not white: white glows under bloom.
        sr.sharedMaterial = PuffMaterial();
        // Measured on screen: 0.78 grey still bloomed to near white, reading as glowing rather than
        // as smoke. Linear colour space again (see CLAUDE.md): pick these by screenshot.
        sr.color = new Color(0.58f, 0.58f, 0.61f, 0.85f);
        sr.sortingOrder = 8;   // over the player for the instant the swap happens

        go.AddComponent<VanishPuff>().Init(drift, size, life);
    }

    private static void Log(Vector3 chest, Sprite decoy)
    {
        var go = new GameObject("VanishLog");
        go.transform.position = chest;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AsWorldProp(decoy);
        sr.sortingOrder = 4;   // under the smoke, so it is revealed as the puff clears

        // Where it lands: the floor under the chest, or wherever it gives up falling.
        RaycastHit2D floor = Physics2D.Raycast(chest, Vector2.down, 8f, LayerMask.GetMask("Ground"));
        float rest = floor.collider != null
            ? floor.point.y + decoy.bounds.extents.y * 0.8f
            : chest.y - 8f;

        go.AddComponent<VanishLog>().Init(rest);
    }

    // ⚠️ THE LOG IS AN INVENTORY ICON, AND ICONS WEAR A WHITE RIM. The Cainos RPG icon pack draws a
    // 1px white outline round every icon so it reads on a UI panel. In the world that rim says "this
    // is an item" — and a thin white outline is exactly what now marks a shuriken you can pick up, so
    // a white-rimmed log would read as loot. The rim is repainted to a dark world-prop outline, once
    // per sprite: the same art, drawn the way the dungeon pack draws its props.
    private static Sprite AsWorldProp(Sprite icon)
    {
        if (decoys.TryGetValue(icon, out Sprite cached) && cached != null) return cached;

        Color32[] px = StarArt.ReadPixels(icon, out int w, out int h);
        if (px == null) return icon;

        var outPx = new Color32[px.Length];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color32 p = px[y * w + x];
                bool rim = p.a > 127 && (p.r + p.g + p.b) / 3 > 200 && TouchesClear(px, w, h, x, y);
                outPx[y * w + x] = rim ? PropOutline : p;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = icon.name + " (prop)" };
        tex.SetPixels32(outPx);
        tex.Apply();

        Sprite prop = Sprite.Create(tex, new Rect(0, 0, w, h),
                                    new Vector2(icon.pivot.x / w, icon.pivot.y / h), icon.pixelsPerUnit);
        decoys[icon] = prop;
        return prop;
    }

    private static bool TouchesClear(Color32[] px, int w, int h, int x, int y)
    {
        if (x == 0 || y == 0 || x == w - 1 || y == h - 1) return true;
        return px[y * w + x - 1].a <= 127 || px[y * w + x + 1].a <= 127
            || px[(y - 1) * w + x].a <= 127 || px[(y + 1) * w + x].a <= 127;
    }

    private static Sprite PuffSprite()
    {
        if (puffSprite != null) return puffSprite;

        const int S = 12;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Vanish puff" };

        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (S * 0.5f);
                if (d > 1f) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }

                // Light from the upper left (Salvage law 2): the lower-right crescent is shaded.
                float shade = (dx - dy) / S > 0.12f && d > 0.45f ? 0.72f : 1f;
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }
        }
        tex.Apply();

        puffSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return puffSprite;
    }

    private static Material PuffMaterial()
    {
        if (puffMaterial == null) puffMaterial = new Material(Shader.Find("Sprites/Default"));
        return puffMaterial;
    }
}

// One smoke puff: grows, drifts up and out, slows, fades. Scaled time, so a HitStop holds it too.
public class VanishPuff : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 drift;
    private float size;
    private float life;
    private float t;
    private Color start;

    public void Init(Vector2 driftVelocity, float worldSize, float lifetime)
    {
        sr = GetComponent<SpriteRenderer>();
        start = sr.color;
        drift = driftVelocity;
        size = worldSize;
        life = Mathf.Max(0.05f, lifetime);
        transform.localScale = Vector3.one * size * 0.5f;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);

        transform.position += (Vector3)(drift * (1f - k) * Time.deltaTime);
        transform.localScale = Vector3.one * size * Mathf.Lerp(0.5f, 1.35f, 1f - (1f - k) * (1f - k));
        sr.color = new Color(start.r, start.g, start.b, start.a * (1f - k * k));

        if (k >= 1f) Destroy(gameObject);
    }
}

// The decoy log: drops out of the smoke, thuds onto the floor, lies there a moment, then fades.
public class VanishLog : MonoBehaviour
{
    private const float GRAVITY = 30f;
    private const float LINGER = 1.4f;     // about as long as the vanish, so the joke outlasts the smoke
    private const float FADE = 0.35f;

    private SpriteRenderer sr;
    private float restY;
    private float fall;
    private float spin;
    private bool landed;
    private float sinceLanding;

    public void Init(float floorY)
    {
        sr = GetComponent<SpriteRenderer>();
        restY = floorY;
        fall = -1.5f;                      // a little hop up first, as if knocked out of the puff
        spin = Random.Range(-160f, 160f);
    }

    private void Update()
    {
        if (!landed)
        {
            fall += GRAVITY * Time.deltaTime;
            Vector3 p = transform.position;
            p.y -= fall * Time.deltaTime;
            transform.Rotate(0f, 0f, spin * Time.deltaTime);

            if (p.y <= restY)
            {
                p.y = restY;
                landed = true;
                // Settle flat-ish, at whatever angle it came down nearest to lying on its side.
                float z = transform.eulerAngles.z;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(z / 90f) * 90f + Random.Range(-8f, 8f));
            }
            transform.position = p;
            return;
        }

        sinceLanding += Time.deltaTime;
        if (sinceLanding > LINGER)
        {
            float k = Mathf.Clamp01((sinceLanding - LINGER) / FADE);
            sr.color = new Color(1f, 1f, 1f, 1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
