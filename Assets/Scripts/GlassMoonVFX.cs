using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Glass Moon's look and timeline. It also OWNS the burst: GlassMoon.Strike is called from here on
/// the frame the moon shatters, so a moon removed early (a room change) never hits anything.
///
///   SUMMON  (0)        a ring pulses out from the chest; the room starts to darken
///   RISE    (0-0.42)   the moon climbs to its burst point, growing, trailing glints; it carries a
///                      real Light2D, so the walls and floor catch moonlight as the room dims
///   CRACK   (0.42-0.7) three crack stages, a glass tick and a few chips each; the moon trembles
///                      harder and a ring collapses into it just before
///   BURST   (0.7)      white flash and a moment of full light; a shock ring grows to EXACTLY the
///                      damage radius (so the hit area is visible); 34 shards fly out on trails,
///                      spin and fall; glitter drifts down; shards tinkle as they land
///
/// ⚠️ ALL THE ART IS GENERATED, AT THE WORLD'S 32 PX PER UNIT. Moon, shards, glints and halo are
/// pixel textures built once; rings and trails are LineRenderers/TrailRenderers so they keep one
/// thin width at any radius (BraceVFX learned that scaling a ring sprite makes its pixels blocky).
/// `MoonPixels` is public because the card's placeholder face is painted with the same moon.
///
/// ⚠️ UNLIT, AND ONE MATERIAL PER IMAGE (both from BraceVFX): the scene's global Light2D is 0.5 and
/// would halve a lit sprite, and sprites sharing one material were once drawn with each other's
/// texture.
///
/// ⚠️ THE GLOBAL LIGHT IS SHARED, SO THE DIMMING IS REFERENCE-COUNTED. Every live moon states how
/// dark it wants the room; the darkest wins, a burst's flash outshines them, and the original
/// intensity is restored when the last moon goes (OnDestroy, so a room change cannot leave the
/// room dark). Nothing else in the game writes the global light (checked 2026-10-03).
///
/// The Cainos player rig ignores 2D lights (see RestWell), so the character stays bright while the
/// room dims around them. That reads as a spotlight, and it is fine.
/// </summary>
public class GlassMoonVFX : MonoBehaviour
{
    private const float PPU = 32f;
    private const float LINE = 2f / PPU;          // two world pixels
    private const int RING_SEGMENTS = 64;

    public const float RiseEnd = 0.42f;
    public const float BurstAt = 0.70f;
    private const float LightFade = 0.55f;
    private const float DimReturn = 0.7f;
    private const float NightDim = 0.6f;          // the room at 60% while the moon is up
    private const float BurstBright = 1.35f;      // and 135% for a moment when it breaks

    private static float FRONT_Z => PlayPlane.Z - 0.3f;

    // Ice-glass palette, cool to match the Shift-crystal family rather than a warm torch.
    private static readonly Color32 COutline = new Color32(34, 48, 88, 255);
    private static readonly Color32 CDeep = new Color32(78, 104, 168, 255);
    private static readonly Color32 CShadow = new Color32(126, 160, 218, 255);
    private static readonly Color32 CMid = new Color32(182, 210, 246, 255);
    private static readonly Color32 CLight = new Color32(226, 240, 255, 255);
    private static readonly Color32 CShine = new Color32(255, 255, 255, 255);
    private static readonly Color32 CCrack = new Color32(240, 255, 255, 255);
    private static readonly Color Ice = new Color(0.72f, 0.86f, 1f, 1f);
    private static readonly Color Pale = new Color(0.93f, 0.97f, 1f, 1f);
    private static readonly Color MoonLight = new Color(0.70f, 0.84f, 1f, 1f);

    private class Bit
    {
        public Transform t;
        public SpriteRenderer sr;
        public LineRenderer lr;
        public Vector3 pos;
        public Vector2 vel;
        public float age, life;
        public float drag, gravity, gravityDelay, spin, angle;
        public float s0, s1;            // scale for sprites, radius for rings
        public float alpha, hold;       // hold = share of life spent at full alpha
        public Color from, to;
        public bool twinkle;
        public float phase;
    }

    // ---- generated art, built once ----
    private static Sprite[] moonStages;
    private static Sprite halo, flash, glint;
    private static Sprite[] shards;
    private static Shader unlit;
    private static Material lineMat;
    private static readonly Dictionary<Sprite, Material> mats = new Dictionary<Sprite, Material>();

    // ---- the shared global light ----
    private static readonly List<GlassMoonVFX> alive = new List<GlassMoonVFX>();
    private static Light2D globalLight;
    private static float globalBase;

    // ---- this moon ----
    private Vector2 start, burst;
    private float damage;
    private RuntimeCard source;
    private float t;
    private bool burstDone, imploded;
    private int crackStage;
    private float nextGlint;
    private float dim = 1f;                       // this moon's wish for the room's light
    private SpriteRenderer moon, haloSR;
    private Light2D light2D;
    private float lightPeak;
    private readonly List<Bit> bits = new List<Bit>();
    private readonly List<float> tinkles = new List<float>();

    // ---- API ------------------------------------------------------------------------------------

    public static GlassMoonVFX Spawn(Vector2 from, Vector2 at, float damage, RuntimeCard source)
    {
        EnsureArt();
        var go = new GameObject("GlassMoon");
        go.AddComponent<TemporaryObject>();       // a room change takes the moon with it, unburst
        var v = go.AddComponent<GlassMoonVFX>();
        v.start = from;
        v.burst = at;
        v.damage = damage;
        v.source = source;
        v.Begin();
        return v;
    }

    private void Begin()
    {
        haloSR = NewSR(halo, new Color(Ice.r, Ice.g, Ice.b, 0f), 60);
        moon = NewSR(moonStages[0], Color.white, 62);
        SetMoon(start, 0.35f);

        var lgo = new GameObject("MoonLight");
        lgo.transform.SetParent(transform, false);
        light2D = lgo.AddComponent<Light2D>();
        light2D.lightType = Light2D.LightType.Point;
        light2D.color = MoonLight;
        light2D.intensity = 0f;
        light2D.pointLightInnerRadius = 0.4f;
        light2D.pointLightOuterRadius = 7.5f;
        light2D.falloffIntensity = 0.55f;
        light2D.transform.position = new Vector3(start.x, start.y, 0f);

        AddRing(start, 0.15f, 1.2f, 0.38f, Pale, Ice, 0.85f, LINE * 1.5f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f + Random.Range(-0.3f, 0.3f);
            AddSprite(glint, Pale, Ice, start, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3f),
                      Random.Range(0.3f, 0.45f), 1f, 1f, 1f, 0.3f);
        }

        Sfx.Play("Card.GlassMoon.Rise", start);
        AcquireGlobal();
    }

    // ---- timeline -------------------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;

        if (!burstDone)
        {
            if (t < RiseEnd) Rise(dt);
            else Hover();
            if (t >= BurstAt) Burst();
        }
        else
        {
            float k = Mathf.Clamp01((t - BurstAt) / LightFade);
            light2D.intensity = lightPeak * (1f - k) * (1f - k);
            float r = Mathf.Clamp01((t - BurstAt) / DimReturn);
            dim = 1f + (BurstBright - 1f) * (1f - r) * (1f - r) * (1f - r);
        }

        UpdateBits(dt);
        for (int i = tinkles.Count - 1; i >= 0; i--)
        {
            if (t < tinkles[i]) continue;
            tinkles.RemoveAt(i);
            Sfx.Play("Card.GlassMoon.Tinkle", burst);
        }
        ApplyGlobalDim();

        if (burstDone && t > BurstAt + DimReturn && bits.Count == 0 && tinkles.Count == 0)
            Destroy(gameObject);
    }

    private void Rise(float dt)
    {
        float k = Mathf.Clamp01(t / RiseEnd);
        float e = 1f - (1f - k) * (1f - k) * (1f - k);      // shoots up, then settles into place
        Vector2 p = Vector2.Lerp(start, burst, e);
        SetMoon(p, Mathf.Lerp(0.35f, 1f, e));
        haloSR.color = new Color(Ice.r, Ice.g, Ice.b, 0.9f * e);
        light2D.intensity = 1.3f * e;
        dim = Mathf.Lerp(1f, NightDim, e);

        nextGlint -= dt;
        if (nextGlint <= 0f)
        {
            nextGlint = 0.022f;
            Vector2 at = p + Random.insideUnitCircle * 0.35f;
            AddSprite(glint, Pale, Ice, at, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-1.1f, -0.3f)),
                      Random.Range(0.35f, 0.55f), 1f, 1f, 0.95f, 0.35f).twinkle = true;
        }
    }

    private void Hover()
    {
        float k = Mathf.Clamp01((t - RiseEnd) / (BurstAt - RiseEnd));

        int want = t >= RiseEnd + 0.20f ? 3 : t >= RiseEnd + 0.10f ? 2 : 1;
        if (want > crackStage)
        {
            crackStage = want;
            moon.sprite = moonStages[crackStage];
            moon.sharedMaterial = MaterialFor(moon.sprite);
            Crack();
        }

        // It trembles harder the closer it gets to breaking: whole-pixel jitter, so it stays crisp.
        int j = Mathf.RoundToInt(Mathf.Lerp(0f, 2f, k));
        Vector2 jitter = new Vector2(Random.Range(-j, j + 1), Random.Range(-j, j + 1)) / PPU;
        Vector2 bob = new Vector2(0f, Mathf.Sin(t * 9f) * 0.03f);
        SetMoon(burst + bob + jitter, 1f);

        haloSR.transform.localScale = Vector3.one * (1f + 0.18f * k * Mathf.Sin(t * 34f));
        light2D.intensity = Mathf.Lerp(1.3f, 2.3f, k) * (0.88f + 0.12f * Mathf.Sin(t * 57f));
        dim = NightDim;

        if (!imploded && t >= BurstAt - 0.16f)
        {
            imploded = true;
            AddRing(burst, 2.4f, 0.55f, 0.16f, Ice, Pale, 0.75f, LINE * 1.5f);
        }
    }

    private void Crack()
    {
        Sfx.Play("Card.GlassMoon.Crack", burst);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.08f, 0.05f + 0.05f * crackStage);
        light2D.intensity *= 1.4f;

        for (int i = 0; i < 2 + crackStage; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Bit b = AddSprite(shards[Random.Range(0, shards.Length)], Color.white, Color.white,
                              burst + dir * GlassMoon.MoonRadius, dir * Random.Range(2f, 4f),
                              Random.Range(0.4f, 0.6f), 0.7f, 0.7f, 1f, 0.5f);
            b.gravity = 10f;
            b.spin = Random.Range(-540f, 540f);
        }
    }

    private void Burst()
    {
        burstDone = true;
        moon.enabled = false;

        Sfx.Play("Card.GlassMoon", burst);
        Sfx.Play("Card.GlassMoon.Body", burst);
        Sfx.Play("Card.GlassMoon.Tail", burst);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.35f, 0.75f);

        // ⚠️ Tuned by screenshot: at 4.2 the light turned the wall behind the burst into a white blob
        // for a third of a second, and the shards read as a lamp rather than glass.
        lightPeak = 2.8f;
        light2D.intensity = lightPeak;
        dim = BurstBright;

        AddSprite(flash, Color.white, Pale, burst, Vector2.zero, 0.12f, 1f, 1.7f, 1f, 0f);
        haloSR.enabled = false;
        AddSprite(halo, Pale, Ice, burst, Vector2.zero, 0.35f, 1.1f, 2.0f, 0.32f, 0f).pos.z = FRONT_Z + 0.02f;

        // The outer ring stops at EXACTLY the damage radius: the hit area, drawn for one moment.
        AddRing(burst, 0.6f, GlassMoon.Radius, 0.32f, Pale, Ice, 0.95f, LINE * 1.5f);
        AddRing(burst, 0.4f, GlassMoon.Radius * 0.7f, 0.45f, Ice, Ice, 0.45f, LINE);

        const int N = 34;
        for (int i = 0; i < N; i++)
        {
            float a = i * Mathf.PI * 2f / N + Random.Range(-0.08f, 0.08f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Bit b = AddSprite(shards[Random.Range(0, shards.Length)], Color.white, Color.white,
                              burst + dir * 0.3f, dir * Random.Range(8f, 14f),
                              Random.Range(0.75f, 1.2f), 1f, 1f, 1f, 0.55f);
            b.drag = 2.8f;
            b.gravity = 16f;
            b.gravityDelay = 0.16f;
            b.spin = Random.Range(-720f, 720f);
            if (i % 3 == 0) AddTrail(b);
        }

        for (int i = 0; i < 26; i++)
        {
            Vector2 at = burst + Random.insideUnitCircle * 1.2f;
            Bit g = AddSprite(glint, Pale, Ice, at, Random.insideUnitCircle * 1.2f + new Vector2(0f, -0.3f),
                              Random.Range(0.9f, 1.7f), 1f, 1f, 1f, 0.4f);
            g.gravity = 1.2f;
            g.drag = 1.5f;
            g.twinkle = true;
        }

        foreach (float d in new[] { 0.20f, 0.33f, 0.47f, 0.64f })
            tinkles.Add(t + d + Random.Range(-0.03f, 0.03f));

        // Last, so everything above is already on screen during the hit-stop the hits trigger.
        GlassMoon.Strike(burst, damage, source);
    }

    private void SetMoon(Vector2 p, float scale)
    {
        moon.transform.position = new Vector3(p.x, p.y, FRONT_Z);
        moon.transform.localScale = Vector3.one * scale;
        haloSR.transform.position = new Vector3(p.x, p.y, FRONT_Z + 0.02f);
        if (light2D != null) light2D.transform.position = new Vector3(p.x, p.y, 0f);
    }

    // ---- particles ------------------------------------------------------------------------------

    private void UpdateBits(float dt)
    {
        for (int i = bits.Count - 1; i >= 0; i--)
        {
            Bit b = bits[i];
            b.age += dt;
            if (b.age >= b.life || b.t == null)
            {
                if (b.t != null) Destroy(b.t.gameObject);
                bits.RemoveAt(i);
                continue;
            }

            float k = b.age / b.life;
            float grow = 1f - (1f - k) * (1f - k);
            float f = k < b.hold ? 1f : 1f - (k - b.hold) / Mathf.Max(0.0001f, 1f - b.hold);
            Color c = Color.Lerp(b.from, b.to, k);
            c.a = b.alpha * f * f;
            if (b.twinkle) c.a *= 0.55f + 0.45f * Mathf.Sin(b.age * 38f + b.phase);

            if (b.lr != null)
            {
                float r = Mathf.Lerp(b.s0, b.s1, grow);
                for (int s = 0; s < RING_SEGMENTS; s++)
                {
                    float ang = s * Mathf.PI * 2f / RING_SEGMENTS;
                    b.lr.SetPosition(s, new Vector3(b.pos.x + Mathf.Cos(ang) * r, b.pos.y + Mathf.Sin(ang) * r, b.pos.z));
                }
                b.lr.startColor = b.lr.endColor = c;
                continue;
            }

            b.vel *= Mathf.Max(0f, 1f - b.drag * dt);
            if (b.age > b.gravityDelay) b.vel.y -= b.gravity * dt;
            b.pos += (Vector3)(b.vel * dt);
            b.angle += b.spin * dt;
            b.t.position = b.pos;
            b.t.rotation = Quaternion.Euler(0f, 0f, b.angle);
            b.t.localScale = Vector3.one * Mathf.Lerp(b.s0, b.s1, grow);
            b.sr.color = c;
        }
    }

    private Bit AddSprite(Sprite sprite, Color from, Color to, Vector2 at, Vector2 vel, float life,
                          float scale0, float scale1, float alpha, float hold)
    {
        SpriteRenderer sr = NewSR(sprite, from, 64);
        var b = new Bit
        {
            t = sr.transform, sr = sr, pos = new Vector3(at.x, at.y, FRONT_Z - 0.01f), vel = vel,
            life = life, s0 = scale0, s1 = scale1, alpha = alpha, hold = hold, from = from, to = to,
            phase = Random.Range(0f, 6.3f),
        };
        sr.transform.position = b.pos;
        sr.transform.localScale = Vector3.one * scale0;
        bits.Add(b);
        return b;
    }

    // A ring centred on `at` that goes from radius r0 to r1 (either way), always `width` thick.
    private void AddRing(Vector2 at, float r0, float r1, float life, Color from, Color to, float alpha, float width)
    {
        var go = new GameObject("moon ring");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = RING_SEGMENTS;
        lr.widthMultiplier = width;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 0;
        lr.sortingOrder = 63;
        if (lineMat != null) lr.sharedMaterial = lineMat;
        lr.startColor = lr.endColor = from;
        bits.Add(new Bit
        {
            t = go.transform, lr = lr, pos = new Vector3(at.x, at.y, FRONT_Z), life = life,
            s0 = r0, s1 = r1, alpha = alpha, hold = 0f, from = from, to = to,
        });
    }

    private void AddTrail(Bit b)
    {
        var tr = b.t.gameObject.AddComponent<TrailRenderer>();
        tr.time = 0.14f;
        tr.minVertexDistance = 0.04f;
        tr.widthMultiplier = LINE * 1.2f;
        tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        tr.numCapVertices = 0;
        tr.sortingOrder = 63;
        if (lineMat != null) tr.sharedMaterial = lineMat;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Pale, 0f), new GradientColorKey(Ice, 1f) },
                  new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
    }

    private SpriteRenderer NewSR(Sprite sprite, Color color, int order)
    {
        var go = new GameObject("moon bit");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        Material m = MaterialFor(sprite);
        if (m != null) sr.sharedMaterial = m;
        return sr;
    }

    private static Material MaterialFor(Sprite sprite)
    {
        if (sprite == null || unlit == null) return null;
        if (!mats.TryGetValue(sprite, out Material m) || m == null)
        {
            m = new Material(unlit) { name = "GlassMoon " + sprite.name, mainTexture = sprite.texture };
            mats[sprite] = m;
        }
        return m;
    }

    // ---- the room's light -----------------------------------------------------------------------

    private void AcquireGlobal()
    {
        alive.RemoveAll(m => m == null);
        if (alive.Count == 0 || globalLight == null)
        {
            globalLight = null;
            foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global && l.isActiveAndEnabled) { globalLight = l; break; }
            if (globalLight != null) globalBase = globalLight.intensity;
        }
        alive.Add(this);
    }

    private static void ApplyGlobalDim()
    {
        alive.RemoveAll(m => m == null);
        if (globalLight == null) return;
        if (alive.Count == 0) { globalLight.intensity = globalBase; return; }

        float darkest = float.MaxValue, brightest = 0f;
        foreach (GlassMoonVFX m in alive)
        {
            darkest = Mathf.Min(darkest, m.dim);
            brightest = Mathf.Max(brightest, m.dim);
        }
        // A burst's flash outshines another moon's night; otherwise the darkest wish wins.
        globalLight.intensity = globalBase * (brightest > 1f ? brightest : darkest);
    }

    private void OnDestroy()
    {
        alive.Remove(this);
        if (alive.Count == 0) { if (globalLight != null) globalLight.intensity = globalBase; }
        else ApplyGlobalDim();
    }

    // ---- generated pixel art --------------------------------------------------------------------

    private static void EnsureArt()
    {
        if (moonStages != null && moonStages[0] != null) return;

        unlit = Shader.Find("Sprites/Default");
        mats.Clear();
        if (unlit != null) lineMat = new Material(unlit) { name = "GlassMoon lines" };

        moonStages = new Sprite[4];
        for (int s = 0; s < 4; s++) moonStages[s] = MakeSprite("GlassMoon " + s, 36, 36, MoonPixels(36, s));

        // The halo is posterised in three steps, like the rest of the pixel art, not a smooth glow.
        halo = MakeSprite("GlassMoon halo", 76, 76, Disk(76, d => d < 0.42f ? 0.30f : d < 0.68f ? 0.15f : d < 1f ? 0.06f : 0f));
        flash = MakeSprite("GlassMoon flash", 44, 44, Disk(44, d => d < 0.6f ? 1f : d < 1f ? 0.5f : 0f));
        glint = MakeSprite("GlassMoon glint", 3, 3, new[]
        {
            new Color32(255, 255, 255, 0),   new Color32(255, 255, 255, 140), new Color32(255, 255, 255, 0),
            new Color32(255, 255, 255, 140), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 140),
            new Color32(255, 255, 255, 0),   new Color32(255, 255, 255, 140), new Color32(255, 255, 255, 0),
        });

        var rng = new System.Random(11);
        shards = new Sprite[6];
        for (int i = 0; i < shards.Length; i++) shards[i] = ShardSprite("GlassMoon shard " + i, rng);
    }

    /// <summary>
    /// The moon, size x size, lit from the upper left, with craters, a glassy rim and a specular
    /// spot. crackStage 0-3 adds glowing fissures; each stage is a strict superset of the one before.
    /// </summary>
    public static Color32[] MoonPixels(int size, int crackStage, int seed = 7)
    {
        var px = new Color32[size * size];
        float r = size * 0.5f;
        Vector3 L = new Vector3(-0.55f, 0.62f, 0.56f).normalized;
        Color32[] tone = { CDeep, CShadow, CMid, CLight };
        Vector3[] craters =
        {
            new Vector3(0.28f, -0.18f, 0.20f), new Vector3(-0.22f, -0.42f, 0.13f),
            new Vector3(0.10f, 0.36f, 0.11f), new Vector3(-0.42f, 0.06f, 0.09f), new Vector3(0.50f, 0.30f, 0.07f),
        };

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int i = y * size + x;
                if (d > r - 0.5f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                if (d > r - 1.5f) { px[i] = COutline; continue; }

                float nx = dx / r, ny = dy / r, nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float lam = nx * L.x + ny * L.y + nz * L.z;
                int k = lam > 0.82f ? 3 : lam > 0.5f ? 2 : lam > 0.12f ? 1 : 0;

                foreach (Vector3 c in craters)
                {
                    float ddx = dx - c.x * r, ddy = dy - c.y * r, cr = c.z * r;
                    float cd = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                    if (cd > cr) continue;
                    // The floor sits a tone darker; the far rim (away from the light) catches it.
                    bool litRim = cd > cr - 1.2f && (ddx * L.x + ddy * L.y) < 0f;
                    k = litRim ? Mathf.Min(3, k + 1) : Mathf.Max(0, k - 1);
                }

                // Glass: light refracted through the body shows as a band inside the lower-right rim.
                if (d > r - 3.2f && (dx * L.x + dy * L.y) < -0.3f * r) k = Mathf.Max(k, 2);
                px[i] = tone[k];
            }

        // Specular spot up-left, and one stray glint.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                if (Mathf.Sqrt(dx * dx + dy * dy) > r - 1.5f) continue;
                float ex = (dx + 0.36f * r) / (0.16f * r), ey = (dy - 0.40f * r) / (0.10f * r);
                if (ex * ex + ey * ey <= 1f) px[y * size + x] = CShine;
            }
        int gx = Mathf.RoundToInt(r - 0.12f * r), gy = Mathf.RoundToInt(r + 0.58f * r);
        if (gx >= 0 && gy >= 0 && gx < size && gy < size && px[gy * size + gx].a > 0) px[gy * size + gx] = CShine;

        if (crackStage > 0) DrawCracks(px, size, r, crackStage, seed);
        return px;
    }

    private static void DrawCracks(Color32[] px, int size, float r, int stage, int seed)
    {
        var rng = new System.Random(seed);
        var crack = new bool[size * size];
        int rays = stage == 1 ? 2 : stage == 2 ? 4 : 7;
        float frac = stage == 1 ? 0.45f : stage == 2 ? 0.75f : 1f;
        Vector2 origin = new Vector2(r + 0.16f * r, r + 0.10f * r);
        float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;

        bool Inside(Vector2 p) => (p - new Vector2(r, r)).magnitude <= r - 2f;

        // Every ray (and its branch) is generated in full on every stage, so the random stream is
        // the same and each stage only REVEALS more of the same cracks.
        for (int ri = 0; ri < 7; ri++)
        {
            float ang = a0 + ri * Mathf.PI * 2f / 7f + ((float)rng.NextDouble() - 0.5f) * 0.5f;
            List<Vector2Int> path = Walk(origin, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), rng, Inside, 64);
            int branchFrom = path.Count / 2;
            float bang = ang + (rng.NextDouble() < 0.5 ? 0.9f : -0.9f);
            List<Vector2Int> branch = path.Count > 4
                ? Walk(path[branchFrom], new Vector2(Mathf.Cos(bang), Mathf.Sin(bang)), rng, Inside, 9)
                : new List<Vector2Int>();

            if (ri >= rays) continue;
            int n = Mathf.CeilToInt(path.Count * frac);
            for (int s = 0; s < n; s++) Mark(crack, size, path[s]);
            if (stage == 3) foreach (Vector2Int p in branch) Mark(crack, size, p);
        }

        // The fissure's lower-right lip is in shadow (light from the upper left); the crack glows.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!crack[y * size + x]) continue;
                int sx = x + 1, sy = y - 1;
                if (sx < size && sy >= 0 && !crack[sy * size + sx] && px[sy * size + sx].a > 0
                    && !px[sy * size + sx].Equals(COutline))
                    px[sy * size + sx] = CDeep;
            }
        for (int i = 0; i < crack.Length; i++) if (crack[i]) px[i] = CCrack;
    }

    private static List<Vector2Int> Walk(Vector2 from, Vector2 dir, System.Random rng,
                                         System.Func<Vector2, bool> inside, int maxSteps)
    {
        var path = new List<Vector2Int>();
        Vector2 p = from;
        for (int step = 0; step < maxSteps; step++)
        {
            if (step % 3 == 2)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                dir = (dir + perp * ((float)rng.NextDouble() - 0.5f) * 0.9f).normalized;
            }
            p += dir;
            if (!inside(p)) break;
            path.Add(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)));
        }
        return path;
    }

    private static void Mark(bool[] crack, int size, Vector2Int p)
    {
        if (p.x >= 0 && p.y >= 0 && p.x < size && p.y < size) crack[p.y * size + p.x] = true;
    }

    // A white disk whose alpha is alphaAt(distance from centre, 0..1).
    private static Color32[] Disk(int size, System.Func<float, float> alphaAt)
    {
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(alphaAt(Mathf.Sqrt(dx * dx + dy * dy) / r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        return px;
    }

    // A splinter: a thin triangle, lit edge on the left, shadowed base, one bright pixel at the tip.
    private static Sprite ShardSprite(string name, System.Random rng)
    {
        int w = rng.Next(4, 8), h = rng.Next(5, 10);
        Vector2 a = new Vector2(rng.Next(0, w) + 0.5f, h - 0.5f);
        Vector2 b = new Vector2(0.5f, 0.5f);
        Vector2 c = new Vector2(w - 0.5f, rng.Next(0, Mathf.Max(1, h / 2)) + 0.5f);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                if (!InTriangle(p, a, b, c)) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                float dAB = EdgeDistance(p, a, b), dBC = EdgeDistance(p, b, c);
                px[y * w + x] = dAB < 1f ? CLight : dBC < 1f ? CShadow : CMid;
            }
        int tx = Mathf.Clamp(Mathf.FloorToInt(a.x), 0, w - 1);
        px[(h - 1) * w + tx] = CShine;
        return MakeSprite(name, w, h, px);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

    private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return (a + ab * k - p).magnitude;
    }

    private static Sprite MakeSprite(string name, int w, int h, Color32[] px)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        s.name = name;
        return s;
    }
}
