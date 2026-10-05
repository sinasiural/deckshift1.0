using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A Shift-infused enemy (Hard map nodes; chosen by <see cref="ShiftInfusion"/>). Tougher than the
/// same enemy elsewhere, and it drops Shift crystals when it dies.
///
/// ⚠️ A BUFFED ENEMY MUST LOOK DIFFERENT (run-map design rule): a Shambler that quietly has 18 HP
/// instead of 12 reads as "my Fireball is broken" and corrodes the CardAnchors.md rule that fodder
/// dies to one Fireball. Nothing on the HUD says so (designer: players learn it by playing), so the
/// body has to say all of it.
///
/// THE LOOK (rebuilt 2026-10-04; the designer found the first version, a tint, a soft aura and some
/// motes, "pretty simple"). Everything is drawn in Shift's own colour and says what Shift IS:
///   - The body is TRACED: a crisp one-pixel Shift outline with a softer two-pixel halo behind it,
///     taken from the enemy's real silhouette every frame, so it follows every limb.
///   - A band of light keeps SWEEPING UP through the body and its outline: charge, never at rest.
///   - Every few seconds it PHASE-SLIPS: two copies of it tear out sideways and snap back in, with a
///     flash, crackle and a spike of light. Shift is the movement resource, so the energy in it
///     visibly wants to move.
///   - It leaves ECHOES of itself where it has been.
///   - The crystals it will drop ORBIT it, one per crystal, so the player can count the prize
///     before the fight. On death they fly out and become the real pickups.
///   - Bolts of pixel lightning crackle over it, more when it is hit.
///
/// ⚠️ NO TINT ON THE BODY ANY MORE. The first version multiplied a cyan into the enemy's own colours
/// and read as a muddy teal zombie. The enemy keeps its art; the Shift is drawn around and over it.
/// That also keeps a Glass Wail / Glass Moon stun (pure blue body) unmistakable.
///
/// ⚠️ THE SILHOUETTE IS BAKED WITH BakeMesh(mesh, false) AND DRAWN AT THE RENDERER'S POSITION AND
/// ROTATION WITH SCALE 1. Measured against hand-skinned vertices on every enemy rig: that is exact
/// for all of them, while BakeMesh(mesh, true) drawn the same way loses the root's scale (a Rotbrute
/// outline came out 15% small) and the bat's flip (off by 1.6 units).
///
/// ⚠️ THE EFFECT LIVES ON ITS OWN WORLD-SPACE OBJECT, not under the enemy. The rigs carry rotations
/// and z-scale flips that would distort anything parented inside them. It is destroyed with the enemy
/// (OnDestroy) and stamped TemporaryObject so a room change sweeps it too.
///
/// The death runs on its own object (<see cref="InfusedDeathVFX"/>) because EnemyHealth.Die fires
/// OnDied and destroys the enemy in the same frame.
/// </summary>
public class ShiftInfused : MonoBehaviour
{
    // The look. Every alpha here was picked by screenshot: the project renders in linear colour
    // space, where a faint bright colour composites far brighter than its number (CLAUDE.md).
    private const float HaloAlpha = 0.34f;
    private const float BandSpeed = 2.1f;            // world units per second, upward
    private const float BandWidthPx = 2f;
    private const float SlipEveryMin = 2.4f, SlipEveryMax = 4.2f;
    private const float SlipDuration = 0.22f;
    private const float SlipTearPx = 7f;              // how far the torn copies jump out, in body pixels
    private const float FlashDecay = 12f;             // per second; a flash is a beat, not a state
    private const float EchoEvery = 0.3f;             // world units travelled between echoes
    private const float EchoLife = 0.45f;             // a walking zombie trails about two
    private const int EchoPool = 5;
    private const float OrbitSpeed = 2.6f;            // radians per second
    private const int MaxShards = 6;
    private const float LightBase = 1.3f;

    // Sorting, relative to the body's own slot. The body's shader is transparent, so it sorts by
    // order: below it is behind the body, above it is over the body.
    private const int EchoOrder = -5, BackShardOrder = -4, HaloOrder = -3, RimOrder = -2, SlipOrder = -1;
    private const int ChargeOrder = 1, FrontShardOrder = 2, SparkOrder = 3;

    private static readonly Vector2[] HaloOffsets =
        { new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 2), new Vector2(0, -2),
          new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };
    private static readonly Vector2[] RimOffsets =
        { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) };

    private EnemyHealth health;
    private int shiftDrop;
    private GameObject crystalPrefab;

    private Renderer body;
    private SkinnedMeshRenderer skinned;      // every pool enemy is one SkinnedMeshRenderer
    private SpriteRenderer spriteBody;        // fallback for the few sprite enemies
    private Texture bodyTexture;
    private Mesh pose;                        // the body's pose this frame, shared by the live layers
    private float px = 1f / 32f;              // one of the body's pixels, in world units
    private int baseOrder, sortingLayer;

    private class Layer { public GameObject go; public Renderer r; public Vector2 offsetPx; public Mesh ownMesh; }
    private class Echo { public Layer layer; public float age, life; public bool live; public Vector3 vel; }
    private class Shard { public SpriteRenderer sr; public SpriteRenderer[] tail; }
    private class Spark { public SpriteRenderer sr; public Vector3 pos; public Vector2 vel; public float age, life, drag; public bool bolt; public Color col; }

    private Transform fx;
    private Layer[] halo, rim, slip;
    private Layer charge;
    private readonly List<Echo> echoes = new List<Echo>();
    private readonly List<Shard> shards = new List<Shard>();
    private readonly List<Spark> sparks = new List<Spark>();
    private Light2D glow;
    private MaterialPropertyBlock block;

    private Vector3 centreOffset;
    private float bodyWidth = 1f, bodyHeight = 1.5f;
    private float phase, clock, orbitClock;
    private float flash;                      // 0..1: the whole silhouette lit white-hot, decaying
    private float lightKick;
    private float nextSlip, slipAge = -1f;
    private Vector2 tearA, tearB;
    private float nextBolt;
    private float moteClock;
    private Vector3 lastEchoPos;
    private bool shown = true;

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
            health.OnDamaged += OnHit;
        }

        FindBody();
        if (body == null) { Debug.LogWarning("[ShiftInfused] " + name + " has no body renderer to trace."); return; }
        MeasureBody();

        block = new MaterialPropertyBlock();
        fx = new GameObject("ShiftInfusion (" + name + ")").transform;
        fx.gameObject.AddComponent<TemporaryObject>();

        halo = new Layer[HaloOffsets.Length];
        for (int i = 0; i < halo.Length; i++) halo[i] = NewLayer("Halo", HaloOrder, HaloOffsets[i], false);
        rim = new Layer[RimOffsets.Length];
        for (int i = 0; i < rim.Length; i++) rim[i] = NewLayer("Rim", RimOrder, RimOffsets[i], false);
        charge = NewLayer("Charge", ChargeOrder, Vector2.zero, false);
        slip = new[] { NewLayer("Slip", SlipOrder, Vector2.zero, false), NewLayer("Slip", SlipOrder, Vector2.zero, false) };
        foreach (Layer l in slip) l.r.enabled = false;
        for (int i = 0; i < EchoPool; i++)
        {
            var e = new Echo { layer = NewLayer("Echo", EchoOrder, Vector2.zero, true) };
            e.layer.r.enabled = false;
            echoes.Add(e);
        }

        BuildShards();
        BuildGlow();

        lastEchoPos = body.transform.position;
        nextSlip = Random.Range(0.6f, SlipEveryMax);
        nextBolt = Random.Range(0.3f, 1.2f);
    }

    // ---------------------------------------------------------------------------------------------
    // Setup
    // ---------------------------------------------------------------------------------------------

    private void FindBody()
    {
        skinned = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skinned != null)
        {
            body = skinned;
            bodyTexture = skinned.sharedMaterial != null ? skinned.sharedMaterial.mainTexture : null;
            pose = new Mesh { name = "InfusedPose" };
            pose.MarkDynamic();
            skinned.BakeMesh(pose, false);
            px = MeasurePixel(pose, skinned.transform.rotation, bodyTexture, Mathf.Abs(skinned.transform.lossyScale.y) / 32f);
        }
        else
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.sprite != null && sr.enabled) { spriteBody = sr; break; }
            if (spriteBody == null) return;
            body = spriteBody;
            px = Mathf.Abs(spriteBody.transform.lossyScale.y) / spriteBody.sprite.pixelsPerUnit;
        }
        baseOrder = body.sortingOrder;
        sortingLayer = body.sortingLayerID;
    }

    // One texel of the body's art in world units, read off the baked mesh itself (edge length in
    // the world over edge length in texels), so a scaled enemy gets a matching outline.
    private static float MeasurePixel(Mesh m, Quaternion rot, Texture tex, float fallback)
    {
        if (m == null || tex == null) return fallback;
        Vector3[] v = m.vertices; Vector2[] uv = m.uv; int[] t = m.triangles;
        var ratios = new List<float>();
        for (int i = 0; i + 2 < t.Length && ratios.Count < 64; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], b = t[i + (k + 1) % 3];
                Vector3 d = rot * (v[a] - v[b]);
                float texels = new Vector2((uv[a].x - uv[b].x) * tex.width, (uv[a].y - uv[b].y) * tex.height).magnitude;
                if (texels > 3f) ratios.Add(new Vector2(d.x, d.y).magnitude / texels);
            }
        if (ratios.Count == 0) return fallback;
        ratios.Sort();
        float r = ratios[ratios.Count / 2];
        return r > 0.005f ? r : fallback;
    }

    private void MeasureBody()
    {
        Bounds b = body.bounds;
        centreOffset = b.center - transform.position;
        centreOffset.z = 0f;
        bodyWidth = Mathf.Max(0.5f, b.size.x);
        bodyHeight = Mathf.Max(0.6f, b.size.y);
    }

    private Layer NewLayer(string label, int order, Vector2 offsetPx, bool ownMesh)
    {
        var l = new Layer { offsetPx = offsetPx, go = new GameObject(label) };
        l.go.transform.SetParent(fx, false);
        Material mat = ShiftInfusionArt.Silhouette;
        if (skinned != null)
        {
            var mf = l.go.AddComponent<MeshFilter>();
            if (ownMesh) { l.ownMesh = new Mesh { name = "InfusedEcho" }; mf.sharedMesh = l.ownMesh; }
            else mf.sharedMesh = pose;
            var mr = l.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            l.r = mr;
        }
        else
        {
            var sr = l.go.AddComponent<SpriteRenderer>();
            if (mat != null) sr.sharedMaterial = mat;
            l.r = sr;
        }
        l.r.sortingLayerID = sortingLayer;
        l.r.sortingOrder = baseOrder + order;
        if (mat == null) l.r.enabled = false;
        return l;
    }

    private void BuildShards()
    {
        int n = Mathf.Clamp(shiftDrop, 0, MaxShards);
        for (int i = 0; i < n; i++)
        {
            var s = new Shard { sr = NewSprite("Shard", ShiftInfusionArt.Shard(0)), tail = new SpriteRenderer[3] };
            for (int k = 0; k < 3; k++)
                s.tail[k] = NewSprite("Tail", k == 0 ? ShiftInfusionArt.DotBig : ShiftInfusionArt.DotSmall);
            shards.Add(s);
        }
    }

    private SpriteRenderer NewSprite(string label, Sprite sprite)
    {
        var go = new GameObject(label);
        go.transform.SetParent(fx, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (ShiftInfusionArt.AtlasMaterial != null) sr.sharedMaterial = ShiftInfusionArt.AtlasMaterial;
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = baseOrder + SparkOrder;
        return sr;
    }

    private void BuildGlow()
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(fx, false);
        go.transform.position = Centre();
        glow = go.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = Salvage.Shift;
        glow.intensity = LightBase;
        glow.pointLightInnerRadius = 0.3f;
        glow.pointLightOuterRadius = Mathf.Max(2.4f, bodyHeight * 1.8f);
        glow.falloffIntensity = 0.6f;
    }

    private Vector3 Centre() => transform.position + centreOffset;

    // ---------------------------------------------------------------------------------------------
    // Every frame
    // ---------------------------------------------------------------------------------------------

    // LateUpdate, so the Animator has already posed the body for this frame before it is traced.
    private void LateUpdate()
    {
        if (body == null || fx == null) return;
        bool show = body.enabled && body.gameObject.activeInHierarchy;
        if (show != shown) { fx.gameObject.SetActive(show); shown = show; }
        if (!show) return;

        float dt = Time.deltaTime;
        clock += dt;
        bool stunned = health != null && health.IsStunned;
        // A stun freezes the Shift too: the orbit crawls and nothing slips.
        orbitClock += dt * (stunned ? 0.2f : 1f);

        if (skinned != null) skinned.BakeMesh(pose, false);
        Vector3 bp = body.transform.position;
        Quaternion br = body.transform.rotation;
        Vector3 c = Centre();

        flash *= Mathf.Exp(-dt * FlashDecay);
        lightKick *= Mathf.Exp(-dt * 9f);
        float pulse = Mathf.Sin(clock * 2.4f + phase);

        UpdateSlip(dt, stunned);

        // The band of charge rising through the body, its spacing set so one band crosses at a time.
        float spacing = bodyHeight * 1.6f + 0.6f;
        float bandOffset = clock * BandSpeed + phase;

        // Halo: soft, breathing, and it jumps a pixel wider when the body flashes.
        float haloGrow = flash > 0.4f ? 1.5f : 1f;
        Color haloCol = Salvage.Shift; haloCol.a = HaloAlpha * (0.85f + 0.15f * pulse) + flash * 0.3f;
        SetBlock(haloCol, Color.white, 0f, 0f, spacing, bandOffset, bp, 0f);
        foreach (Layer l in halo) Place(l, bp, br, l.offsetPx * haloGrow);

        // Rim: the crisp outline, lit white where the band crosses it and when the body flashes.
        Color rimCol = Color.Lerp(Salvage.Shift, ShiftInfusionArt.Hot, flash);
        SetBlock(rimCol, Color.white, 0f, 1f, spacing, bandOffset, bp, 0f);
        foreach (Layer l in rim) Place(l, bp, br, l.offsetPx);

        // Charge: drawn OVER the body, invisible except for the band and the flash. The flash is
        // squared so it is a spike: linearly faded, it whited the zombie out for a third of a
        // second and read as a ghost, not a hit (linear colour space makes a low alpha of a bright
        // colour far stronger than its number).
        Color chargeCol = ShiftInfusionArt.Hot; chargeCol.a = flash * flash * 0.6f;
        SetBlock(chargeCol, ShiftInfusionArt.Hot, 0.32f, 1f, spacing, bandOffset, bp, 0f);
        Place(charge, bp, br, Vector2.zero);

        UpdateEchoes(dt, bp, br, spacing, bandOffset);
        UpdateShards(c, bp.z, stunned);
        UpdateSparks(dt, c, bp.z, stunned);

        if (glow != null)
        {
            glow.transform.position = new Vector3(c.x, c.y, 0f);
            glow.intensity = LightBase + 0.2f * pulse + lightKick;
        }
    }

    private void SetBlock(Color col, Color bandCol, float bandAlpha, float bandMix, float spacing, float bandOffset, Vector3 origin, float dissolve)
    {
        block.Clear();
        block.SetColor("_Color", col);
        block.SetColor("_BandColor", bandCol);
        block.SetFloat("_BandAlpha", bandAlpha);
        block.SetFloat("_BandMix", bandMix);
        block.SetFloat("_BandSpacing", spacing);
        block.SetFloat("_BandWidth", BandWidthPx * px);
        block.SetFloat("_BandOffset", bandOffset);
        block.SetVector("_Origin", origin);
        block.SetFloat("_PixelSize", px);
        block.SetFloat("_Dissolve", dissolve);
        Texture tex = skinned != null ? bodyTexture : (spriteBody != null && spriteBody.sprite != null ? spriteBody.sprite.texture : null);
        if (tex != null) block.SetTexture("_MainTex", tex);
    }

    // Puts a layer on the body, offset by whole body pixels, carrying the current block.
    private void Place(Layer l, Vector3 bp, Quaternion br, Vector2 offsetPx)
    {
        Vector3 p = bp + new Vector3(Mathf.Round(offsetPx.x) * px, Mathf.Round(offsetPx.y) * px, 0f);
        l.go.transform.SetPositionAndRotation(p, br);
        if (spriteBody != null && l.r is SpriteRenderer sr)
        {
            sr.sprite = spriteBody.sprite;
            sr.flipX = spriteBody.flipX;
            sr.flipY = spriteBody.flipY;
            l.go.transform.localScale = spriteBody.transform.lossyScale;
        }
        l.r.SetPropertyBlock(block);
    }

    private void UpdateSlip(float dt, bool stunned)
    {
        if (slipAge < 0f)
        {
            if (!stunned) nextSlip -= dt;
            if (nextSlip <= 0f) StartSlip();
            return;
        }

        slipAge += dt;
        float k = slipAge / SlipDuration;
        if (k >= 1f)
        {
            // The snap back in: a second, smaller flash, and the energy spits out of it.
            foreach (Layer l in slip) l.r.enabled = false;
            slipAge = -1f;
            nextSlip = Random.Range(SlipEveryMin, SlipEveryMax);
            flash = Mathf.Max(flash, 0.45f);
            lightKick += 0.8f;
            SpawnBolt();
            for (int i = 0; i < 8; i++) SpawnBurstMote(Centre(), 1.4f, 2.8f);
            return;
        }

        // A TEAR, not a slide: the two copies sit BEHIND the body, so only a sliver of each shows
        // past its edge (the split-colour glitch look), and they jitter by whole pixels every frame
        // before collapsing home over the last third. As solid copies sliding 0.4 units out and
        // back, the first version read as a big cyan block either side of the enemy.
        float reach = k < 0.66f ? 1f : 1f - (k - 0.66f) / 0.34f;
        Color col = Salvage.Shift; col.a = 0.8f;
        SetBlock(col, ShiftInfusionArt.Hot, 0f, 0f, 1f, 0f, body.transform.position, 0f);
        Vector3 bp = body.transform.position; Quaternion br = body.transform.rotation;
        // Re-rolled only while time runs, or a paused game would keep a torn enemy twitching.
        if (dt > 0f)
        {
            tearA = new Vector2(SlipTearPx * reach * Random.Range(0.55f, 1.15f), Random.Range(-1, 2));
            tearB = new Vector2(-SlipTearPx * reach * Random.Range(0.55f, 1.15f), Random.Range(-1, 2));
        }
        Place(slip[0], bp, br, tearA);
        Place(slip[1], bp, br, tearB);
    }

    private void StartSlip()
    {
        slipAge = 0f;
        flash = 1f;
        lightKick = 1.6f;
        foreach (Layer l in slip) l.r.enabled = ShiftInfusionArt.Silhouette != null;
        // And one copy of it slides off sideways and comes apart: the Shift trying to MOVE it.
        SpawnEcho(new Vector3(Random.value < 0.5f ? -1.8f : 1.8f, 0f, 0f), 0.4f);
        SpawnBolt();
        SpawnBolt();
    }

    private void OnHit()
    {
        if (body == null) return;
        flash = Mathf.Max(flash, 0.55f);
        lightKick += 1.0f;
        SpawnBolt();
        SpawnBolt();
        for (int i = 0; i < 5; i++) SpawnBurstMote(Centre(), 1.2f, 2.4f);
    }

    private void UpdateEchoes(float dt, Vector3 bp, Quaternion br, float spacing, float bandOffset)
    {
        // A new echo for every stretch of ground covered, so it trails a mover and leaves a
        // standing enemy alone.
        if ((bp - lastEchoPos).sqrMagnitude >= EchoEvery * EchoEvery)
        {
            lastEchoPos = bp;
            SpawnEcho(Vector3.zero, EchoLife);
        }

        foreach (Echo e in echoes)
        {
            if (!e.live) continue;
            e.age += dt;
            float k = e.age / e.life;
            if (k >= 1f) { e.live = false; e.layer.r.enabled = false; continue; }
            if (e.vel != Vector3.zero)
            {
                // A drifting echo eases to a stop as it fades.
                Vector3 p = e.layer.go.transform.position + e.vel * dt * (1f - k);
                e.layer.go.transform.position = p;
            }
            Color col = Salvage.Shift; col.a = 0.42f * (1f - k) * (1f - k);
            SetBlock(col, Color.white, 0f, 0f, spacing, bandOffset, e.layer.go.transform.position, k * 0.8f);
            e.layer.r.SetPropertyBlock(block);
        }
    }

    // A snapshot of the body as it stands now, left in the world to fade (and drift, if given a
    // velocity). Reuses the oldest echo when all are in use.
    private void SpawnEcho(Vector3 vel, float life)
    {
        if (body == null || echoes.Count == 0) return;
        Echo free = null;
        foreach (Echo e in echoes) if (!e.live) { free = e; break; }
        if (free == null) { free = echoes[0]; foreach (Echo e in echoes) if (e.age > free.age) free = e; }
        free.live = true;
        free.age = 0f;
        free.life = life;
        free.vel = vel;
        if (skinned != null && free.layer.ownMesh != null) skinned.BakeMesh(free.layer.ownMesh, false);
        free.layer.go.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
        if (spriteBody != null && free.layer.r is SpriteRenderer esr)
        {
            esr.sprite = spriteBody.sprite; esr.flipX = spriteBody.flipX; esr.flipY = spriteBody.flipY;
            free.layer.go.transform.localScale = spriteBody.transform.lossyScale;
        }
        free.layer.r.enabled = ShiftInfusionArt.Silhouette != null;
    }

    // The crystals it carries, circling its waist on a tilted ring: in front of the body on the
    // near half, behind it on the far half, each with a short comet tail.
    private void UpdateShards(Vector3 c, float z, bool stunned)
    {
        int n = shards.Count;
        if (n == 0) return;
        Vector2 orbit = OrbitRadii();
        Vector3 hub = OrbitCentre(c);
        for (int i = 0; i < n; i++)
        {
            Shard s = shards[i];
            float theta = ShardAngle(i, n);
            bool front = Mathf.Sin(theta) < 0f;
            Vector3 p = OnOrbit(hub, orbit, theta, i);
            s.sr.transform.position = new Vector3(Snap(p.x), Snap(p.y), z - (front ? 0.02f : -0.001f));
            s.sr.sprite = ShiftInfusionArt.Shard(Mathf.FloorToInt(orbitClock * 10f + i * 2f));
            s.sr.sortingOrder = baseOrder + (front ? FrontShardOrder : BackShardOrder);
            float lit = front ? 1f : 0.6f;
            lit = Mathf.Lerp(lit, 1f, flash);
            s.sr.color = new Color(lit, lit, lit, front ? 1f : 0.85f);

            for (int k = 0; k < s.tail.Length; k++)
            {
                float t = theta - 0.2f * (k + 1);
                bool tf = Mathf.Sin(t) < 0f;
                Vector3 q = OnOrbit(hub, orbit, t, i);
                SpriteRenderer d = s.tail[k];
                d.transform.position = new Vector3(Snap(q.x), Snap(q.y), z - (tf ? 0.02f : -0.001f));
                d.sortingOrder = baseOrder + (tf ? FrontShardOrder : BackShardOrder);
                Color col = Salvage.Shift; col.a = (0.6f - 0.18f * k) * (tf ? 1f : 0.6f) * (stunned ? 0.3f : 1f);
                d.color = col;
            }
        }
    }

    private Vector2 OrbitRadii() => new Vector2(bodyWidth * 0.5f + 0.36f, 0.14f + bodyHeight * 0.05f);
    private Vector3 OrbitCentre(Vector3 c) => c + new Vector3(0f, -0.08f * bodyHeight, 0f);
    private float ShardAngle(int i, int n) => orbitClock * OrbitSpeed + i * Mathf.PI * 2f / n + phase;
    private Vector3 OnOrbit(Vector3 hub, Vector2 r, float theta, int i) =>
        hub + new Vector3(r.x * Mathf.Cos(theta), r.y * Mathf.Sin(theta) + 0.03f * Mathf.Sin(clock * 3f + i), 0f);
    private static float Snap(float v) => Mathf.Round(v * ShiftInfusionArt.PPU) / ShiftInfusionArt.PPU;

    private void UpdateSparks(float dt, Vector3 c, float z, bool stunned)
    {
        // Idle crackle, and a slow drift of motes rising off it.
        nextBolt -= dt * (stunned ? 0.3f : 1f);
        if (nextBolt <= 0f) { SpawnBolt(); nextBolt = Random.Range(0.5f, 1.4f); }
        moteClock += dt;
        while (moteClock >= 0.13f) { moteClock -= 0.13f; SpawnRisingMote(c, z); }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            s.age += dt;
            if (s.age >= s.life || s.sr == null)
            {
                if (s.sr != null) Destroy(s.sr.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            float k = s.age / s.life;
            if (s.bolt)
            {
                // Two beats, full then half: a strike, not a fade.
                Color col = Color.white; col.a = k < 0.5f ? 1f : 0.45f;
                s.sr.color = col;
                continue;
            }
            s.vel *= Mathf.Pow(s.drag, dt);
            s.pos += (Vector3)(s.vel * dt);
            s.sr.transform.position = new Vector3(Snap(s.pos.x), Snap(s.pos.y), s.pos.z);
            Color mc = s.col; mc.a = (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f) * 0.95f;
            s.sr.color = mc;
        }
    }

    private void SpawnBolt()
    {
        if (body == null || fx == null) return;
        Vector3 c = Centre();
        var sr = NewSprite("Bolt", ShiftInfusionArt.Arc(Random.Range(0, ShiftInfusionArt.ArcCount)));
        sr.flipX = Random.value < 0.5f;
        sr.flipY = Random.value < 0.5f;
        var p = new Vector3(c.x + Random.Range(-0.3f, 0.3f) * bodyWidth, c.y + Random.Range(-0.3f, 0.3f) * bodyHeight,
                            body.transform.position.z - 0.03f);
        sr.transform.position = new Vector3(Snap(p.x), Snap(p.y), p.z);
        sparks.Add(new Spark { sr = sr, pos = p, life = Random.Range(0.08f, 0.12f), bolt = true });
        lightKick += 0.5f;
    }

    private void SpawnRisingMote(Vector3 c, float z)
    {
        var sr = NewSprite("Mote", ShiftInfusionArt.Mote);
        var p = new Vector3(c.x + Random.Range(-0.45f, 0.45f) * bodyWidth, c.y + Random.Range(-0.45f, 0.2f) * bodyHeight, z - 0.05f);
        sr.transform.position = p;
        sparks.Add(new Spark
        {
            sr = sr, pos = p, vel = new Vector2(0f, Random.Range(0.6f, 1.1f)), drag = 1f,
            life = Random.Range(0.8f, 1.2f), col = Random.value < 0.3f ? ShiftInfusionArt.Hot : Salvage.Shift,
        });
    }

    private void SpawnBurstMote(Vector3 c, float minSpeed, float maxSpeed)
    {
        if (fx == null) return;
        var sr = NewSprite("Mote", ShiftInfusionArt.Mote);
        float a = Random.Range(0f, Mathf.PI * 2f);
        var p = new Vector3(c.x, c.y, body.transform.position.z - 0.05f);
        sr.transform.position = p;
        sparks.Add(new Spark
        {
            sr = sr, pos = p, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(minSpeed, maxSpeed), drag = 0.05f,
            life = Random.Range(0.3f, 0.55f), col = ShiftInfusionArt.Hot,
        });
    }

    // ---------------------------------------------------------------------------------------------
    // Death and teardown
    // ---------------------------------------------------------------------------------------------

    private void OnDied()
    {
        var snap = new InfusedDeathVFX.Snapshot
        {
            centre = Centre(),
            crystals = shiftDrop,
            crystalPrefab = crystalPrefab,
            px = px,
            sortingLayer = sortingLayer,
            baseOrder = baseOrder,
        };
        if (body != null)
        {
            snap.bodyPos = body.transform.position;
            snap.bodyRot = body.transform.rotation;
            if (skinned != null)
            {
                snap.mesh = new Mesh { name = "InfusedDeathPose" };
                skinned.BakeMesh(snap.mesh, false);
                snap.texture = bodyTexture;
            }
            else if (spriteBody != null)
            {
                snap.sprite = spriteBody.sprite;
                snap.flipX = spriteBody.flipX;
                snap.flipY = spriteBody.flipY;
                snap.spriteScale = spriteBody.transform.lossyScale;
            }
        }
        // The orbiting crystals are where the real ones start from.
        if (shards.Count > 0)
        {
            Vector3 hub = OrbitCentre(Centre());
            Vector2 r = OrbitRadii();
            for (int i = 0; i < shards.Count; i++)
                snap.shardPositions.Add(OnOrbit(hub, r, ShardAngle(i, shards.Count), i));
        }
        InfusedDeathVFX.Spawn(snap);
    }

    private void OnEnable() { if (fx != null) fx.gameObject.SetActive(shown); }
    private void OnDisable() { if (fx != null) fx.gameObject.SetActive(false); }

    private void OnDestroy()
    {
        if (health != null) { health.OnDied -= OnDied; health.OnDamaged -= OnHit; }
        // Everything here was made at runtime and is owned by nothing else.
        if (fx != null) Destroy(fx.gameObject);
        if (pose != null) Destroy(pose);
        foreach (Echo e in echoes) if (e.layer.ownMesh != null) Destroy(e.layer.ownMesh);
    }
}
