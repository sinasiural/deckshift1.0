using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A Shift-infused enemy (Hard map nodes; chosen by <see cref="ShiftInfusion"/>). Tougher than the
/// same enemy elsewhere, and it drops Shift crystals when it dies.
///
/// ⚠️ A BUFFED ENEMY MUST LOOK DIFFERENT (run-map design rule): a Shambler that quietly has 18 HP
/// instead of 12 reads as "my Fireball is broken" and corrodes the CardAnchors.md rule that fodder
/// dies to one Fireball. So it wears Shift's colour three ways: a tint on the body, a real Light2D
/// that glows on the walls around it, and motes of Shift drifting up off it. Nothing on the HUD says
/// so (designer: players learn it by playing).
///
/// ⚠️ The tint is NOT the stun's blue. Glass Wail and Glass Moon stun enemies to pure Color.blue;
/// this is a pale cyan cast, so a stunned enemy and an infused one never read as each other.
///
/// The drop runs on its own object (<see cref="InfusedDeathVFX"/>) because EnemyHealth.Die fires
/// OnDied and destroys the enemy in the same frame.
/// </summary>
public class ShiftInfused : MonoBehaviour
{
    private const float PPU = 32f;
    private static readonly Color Tint = Color.Lerp(Color.white, Salvage.Shift, 0.55f);

    private EnemyHealth health;
    private int shiftDrop;
    private GameObject crystalPrefab;

    private Vector3 centreOffset;     // body centre relative to the transform
    private float bodyWidth = 1f, bodyHeight = 1.5f;
    private Light2D glow;
    private SpriteRenderer aura;
    private Transform moteRoot;
    private float moteClock;
    private float phase;

    private class Mote { public SpriteRenderer sr; public Vector3 pos; public float age, life, rise, sway; }
    private readonly List<Mote> motes = new List<Mote>();

    private static Sprite moteSprite;
    private static Material moteMat;
    private static Sprite auraSprite;
    private static Material auraMat;

    public void Begin(float healthMultiplier, int drop, GameObject crystal)
    {
        health = GetComponent<EnemyHealth>();
        shiftDrop = drop;
        crystalPrefab = crystal;
        phase = Random.Range(0f, 6.3f);

        if (health != null)
        {
            health.ScaleMaxHealth(healthMultiplier);
            // Scrap normally follows max health; an enemy with a hand-set drop scales that instead,
            // so "tougher" always pays more.
            if (health.scrapDropOverride > 0)
                health.scrapDropOverride = Mathf.RoundToInt(health.scrapDropOverride * healthMultiplier);
            health.OnDied += OnDied;
        }

        MeasureBody();
        TintBody();
        BuildGlow();
        moteRoot = new GameObject("InfusionMotes").transform;
        moteRoot.SetParent(transform, false);
        // Undo the enemy's own scale, so a mote is the same size on a Rotbrute as on a Shambler.
        Vector3 s = transform.lossyScale;
        moteRoot.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= OnDied;
    }

    private void MeasureBody()
    {
        bool any = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is SkinnedMeshRenderer) && !(r is SpriteRenderer)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;
        centreOffset = b.center - transform.position;
        centreOffset.z = 0f;
        bodyWidth = Mathf.Max(0.5f, b.size.x);
        bodyHeight = Mathf.Max(0.6f, b.size.y);
    }

    private void TintBody()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer sr) { sr.color *= Tint; continue; }
            if (!(r is SkinnedMeshRenderer)) continue;
            foreach (Material m in r.materials)              // per-enemy instances, on purpose
                if (m.HasProperty("_Color")) m.color *= Tint;
        }
    }

    // ⚠️ The tint alone read as a muddy teal on a green zombie, and a 1.0 light barely showed on the
    // dark brick (judged by screenshot, 2026-10-04). What sells "charged with Shift" is light AROUND
    // the body: a soft aura behind it, and a stronger light on the walls.
    private void BuildGlow()
    {
        var go = new GameObject("InfusionGlow");
        go.transform.SetParent(transform, false);
        go.transform.position = Centre();
        glow = go.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = Salvage.Shift;
        glow.intensity = 1.8f;
        glow.pointLightInnerRadius = 0.3f;
        glow.pointLightOuterRadius = Mathf.Max(2.6f, bodyHeight * 2f);
        glow.falloffIntensity = 0.6f;

        EnsureArt();
        var ago = new GameObject("InfusionAura");
        ago.transform.SetParent(transform, false);
        aura = ago.AddComponent<SpriteRenderer>();
        aura.sprite = auraSprite;
        if (auraMat != null) aura.sharedMaterial = auraMat;
        // The body's shader is transparent, so it sorts by order, not depth: the aura sits one step
        // under the body's own sorting slot to stay behind it.
        Renderer body = null;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r is SkinnedMeshRenderer || (r is SpriteRenderer && r != aura)) { body = r; break; }
        if (body != null) { aura.sortingLayerID = body.sortingLayerID; aura.sortingOrder = body.sortingOrder - 1; }
        else aura.sortingOrder = -1;
        // Sized TO the body, never BY it (the sprite is 1.5 x 2 units natively).
        Vector3 native = auraSprite.bounds.size;
        Vector3 parent = transform.lossyScale;
        ago.transform.localScale = new Vector3(bodyWidth * 1.7f / native.x / Mathf.Abs(parent.x),
                                               bodyHeight * 1.35f / native.y / Mathf.Abs(parent.y), 1f);
    }

    private Vector3 Centre() => transform.position + centreOffset;

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 c = Centre();

        float pulse = Mathf.Sin(Time.time * 2.6f + phase);
        if (glow != null)
        {
            glow.transform.position = new Vector3(c.x, c.y, 0f);
            glow.intensity = 1.8f + 0.4f * pulse;
        }
        if (aura != null)
        {
            // A hair behind the play plane, so the body draws over it (BraceVFX's BEHIND_Z rule).
            aura.transform.position = new Vector3(c.x, c.y, PlayPlane.Z + 0.0005f);
            Color a = Salvage.Shift; a.a = 0.85f + 0.15f * pulse; aura.color = a;
        }

        // Motes on a time accumulator, never a per-frame chance (framerate-independent).
        moteClock += dt;
        while (moteClock >= 0.09f)
        {
            moteClock -= 0.09f;
            SpawnMote(c);
        }

        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.age += dt;
            if (m.age >= m.life || m.sr == null)
            {
                if (m.sr != null) Destroy(m.sr.gameObject);
                motes.RemoveAt(i);
                continue;
            }
            m.pos.y += m.rise * dt;
            float k = m.age / m.life;
            m.sr.transform.position = m.pos + new Vector3(Mathf.Sin(m.age * 5f + m.sway) * 0.06f, 0f, 0f);
            Color col = Salvage.Shift;
            col.a = (k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f) * 0.9f;
            m.sr.color = col;
        }
    }

    private void SpawnMote(Vector3 c)
    {
        EnsureArt();
        var go = new GameObject("mote");
        go.transform.SetParent(moteRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = moteSprite;
        if (moteMat != null) sr.sharedMaterial = moteMat;
        sr.sortingOrder = 60;
        Vector3 p = new Vector3(c.x + Random.Range(-0.45f, 0.45f) * bodyWidth,
                                c.y + Random.Range(-0.45f, 0.2f) * bodyHeight,
                                PlayPlane.Z - 0.05f);
        motes.Add(new Mote { sr = sr, pos = p, life = Random.Range(0.8f, 1.2f), rise = Random.Range(0.6f, 1.1f), sway = Random.Range(0f, 6.3f) });
        sr.transform.position = p;
        sr.transform.localScale = Vector3.one * 2f;   // two world pixels per texel: one-pixel motes vanished on screen
    }

    private void OnDied()
    {
        InfusedDeathVFX.Spawn(Centre(), shiftDrop, crystalPrefab);
    }

    // A 3x3 plus of light, at the world's 32 px per unit; unlit, so the scene's 0.5 global light
    // does not halve it.
    private static void EnsureArt()
    {
        if (moteSprite != null) return;
        var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color o = new Color(1, 1, 1, 0), e = new Color(1, 1, 1, 0.55f), w = Color.white;
        tex.SetPixels(new[] { o, e, o, e, w, e, o, e, o });
        tex.Apply();
        moteSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        moteSprite.name = "InfusionMote";
        Shader unlit = Shader.Find("Sprites/Default");
        if (unlit != null) moteMat = new Material(unlit) { name = "InfusionMote", mainTexture = tex };

        // The aura: an upright ellipse in three flat steps of alpha, posterised like the rest of the
        // pixel art rather than a smooth gradient. Low alphas: the project renders in linear space,
        // where a faint bright colour composites far brighter than its number (CLAUDE.md).
        const int AW = 48, AH = 64;
        var at = new Texture2D(AW, AH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[AW * AH];
        for (int y = 0; y < AH; y++)
            for (int x = 0; x < AW; x++)
            {
                float dx = (x + 0.5f - AW * 0.5f) / (AW * 0.5f), dy = (y + 0.5f - AH * 0.5f) / (AH * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d < 0.45f ? 0.22f : d < 0.75f ? 0.12f : d < 1f ? 0.05f : 0f;
                px[y * AW + x] = new Color(1f, 1f, 1f, a);
            }
        at.SetPixels(px);
        at.Apply();
        auraSprite = Sprite.Create(at, new Rect(0, 0, AW, AH), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        auraSprite.name = "InfusionAura";
        if (unlit != null) auraMat = new Material(unlit) { name = "InfusionAura", mainTexture = at };
    }

    /// <summary>Shared with InfusedDeathVFX so both draw the same mote.</summary>
    public static Sprite MoteSprite { get { EnsureArt(); return moteSprite; } }
    public static Material MoteMaterial { get { EnsureArt(); return moteMat; } }
}
