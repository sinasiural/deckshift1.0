using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Brace's visuals, drawn from the card's own art: a figure hunched and planted, concentric grey
/// rings spreading behind it, a pale rim of light over its shoulders.
///
///   PLANT    white flash at the chest, three rings thrown out, a kick of dust at the feet
///   HOLD     the pale rim arched over the shoulders (brighter the more block is left), faint rings
///            pulsing out BEHIND the body like the art's backdrop, a footing mark under the feet;
///            the rim flickers through the last second so the end is never a surprise
///   HIT      the rim flashes, a shock ring and sparks burst, "+1" rises in Shift's colour
///   RELEASE  the rim lifts away and fades
///
/// ⚠️ RINGS ARE LINES, NOT SCALED SPRITES. The first pass grew a 64px Point-filtered ring sprite up to
/// 2.9x, and its pixels grew with it — thick, blocky bands three times the size of the world's pixels.
/// A LineRenderer keeps one constant width (about two world pixels) at every radius. The pieces that
/// never scale (the arch, the footing, sparks, dust) stay pixel sprites at the world's 32 px per unit.
///
/// ⚠️ UNLIT. The scene's global Light2D is 0.5, which halves any lit sprite; these are light, so
/// they use Sprites/Default.
///
/// ⚠️ ONE MATERIAL PER IMAGE, never one shared by every sprite. With a single shared Sprites/Default
/// material the 2D renderer merged the footing mark and the shoulder arch (drawn back to back) into
/// one draw with the FOOTING's texture: the arch came out as the footing ellipse stretched to the
/// arch's size, chunky brackets around the head. It happened in some frames and not others, which
/// is what made it hard to see. Renderers that share a material now always share its image too.
///
/// ⚠️ BEHIND vs IN FRONT is done with Z, not sortingOrder: the player's body is opaque and writes
/// depth. "Behind" is BEHIND_Z — a hair behind the play plane, because PlayPlane leaves props that
/// were already behind the plane where they are, as close as 0.001 behind it; anything further back
/// is hidden by the nearest doorway (measured: the arch lost its top to the entry door's frame).
/// "In front" is FRONT_Z, clear of the rig's frontmost parts (bounds reach ~0.16 in front of the plane).
/// </summary>
public class BraceVFX : MonoBehaviour
{
    private const float PPU = 32f;
    private const float CHEST = 0.95f;       // above the player's feet (the root sits at the feet)
    private const float HEAD = 2.05f;
    private const float ARCH_DROP = 14f;     // px from the arch texture's middle down to its ring centre
    // Where the arch sprite sits so its ring is centred on the chest: the arc then runs from the
    // shoulders up over the head, like the pale rim over the figure in the card's art.
    private static float ArchY => CHEST + ARCH_DROP / PPU;

    private static float BEHIND_Z => PlayPlane.Z + 0.0005f;
    private static float FRONT_Z => PlayPlane.Z - 0.3f;

    private const float LINE = 2f / PPU;     // two world pixels
    private const int RING_SEGMENTS = 48;

    private static readonly Color Stone = new Color(0.80f, 0.80f, 0.83f, 1f);
    private static readonly Color Pale = new Color(0.95f, 0.95f, 0.97f, 1f);

    private class Mote
    {
        public Transform t;
        public SpriteRenderer sr;       // a sprite mote...
        public LineRenderer lr;         // ...or a ring
        public Vector2 vel;
        public float life, maxLife;
        public Color from, to;
        public float size0, size1;      // scale for sprites, radius for rings
        public float alpha0;
        public bool followPlayer;
        public Vector3 offset;          // from the player (following) or a world position (free)
    }

    private static Sprite arch, disk, shard, dust, footing;
    private static Shader unlitShader;
    private static Material lineMat;
    private static readonly Dictionary<Sprite, Material> spriteMats = new Dictionary<Sprite, Material>();

    private readonly List<Mote> motes = new List<Mote>();
    private readonly List<TextMeshPro> pops = new List<TextMeshPro>();
    private readonly List<float> popLife = new List<float>();

    private Transform follow;
    private SpriteRenderer rim, foot;
    private float block01 = 1f, timeLeft01 = 1f;
    private float flash;          // seconds of hit flash left
    private float nextRipple;
    private bool releasing;
    private float releaseT;

    // ---- API -----------------------------------------------------------------------------------

    public static BraceVFX Spawn(Transform follow)
    {
        EnsureSprites();
        var vfx = new GameObject("BraceVFX").AddComponent<BraceVFX>();
        vfx.follow = follow;
        vfx.transform.position = follow.position;
        vfx.Plant();
        return vfx;
    }

    public void SetBlock01(float v) { block01 = Mathf.Clamp01(v); }
    public void SetTimeLeft01(float v) { timeLeft01 = Mathf.Clamp01(v); }

    public void Hit(int shiftGained)
    {
        if (releasing) return;
        flash = 0.14f;

        AddRing(FRONT_Z, 0.60f, 2.6f, 0.30f, Pale, Stone, 0.95f, LINE * 1.6f);
        Vector3 c = Chest(FRONT_Z);
        for (int i = 0; i < 9; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(5f, 9f);
            AddSprite(shard, Pale, Stone, c, v, Random.Range(0.22f, 0.38f), 1f, 1f, 1f, false);
        }
        if (shiftGained > 0) Pop("+" + shiftGained);
    }

    public void Release()
    {
        if (releasing) return;
        releasing = true;
        releaseT = 0f;
        AddRing(BEHIND_Z, 0.9f, 2.3f, 0.5f, Pale, Stone, 0.55f, LINE);
    }

    // ---- the plant -----------------------------------------------------------------------------

    private void Plant()
    {
        AddSprite(disk, Color.white, Pale, Chest(FRONT_Z), Vector2.zero, 0.12f, 1.0f, 1.25f, 0.9f, true);

        // Three rings thrown out from the chest, each starting a little further in.
        AddRing(FRONT_Z, 0.45f, 2.5f, 0.42f, Pale, Stone, 1.0f, LINE * 1.5f);
        AddRing(FRONT_Z, 0.28f, 2.1f, 0.50f, Pale, Stone, 0.8f, LINE);
        AddRing(FRONT_Z, 0.14f, 1.75f, 0.58f, Stone, Stone, 0.6f, LINE);

        // Dust kicked sideways along the ground by the stomp.
        Vector3 feet = new Vector3(follow.position.x, follow.position.y + 0.05f, FRONT_Z);
        for (int i = 0; i < 12; i++)
        {
            float dir = i % 2 == 0 ? 1f : -1f;
            Vector2 v = new Vector2(dir * Random.Range(2.0f, 5.0f), Random.Range(0.3f, 1.4f));
            AddSprite(dust, Stone, new Color(0.55f, 0.53f, 0.52f, 1f), feet + new Vector3(dir * 0.15f, 0f, 0f),
                      v, Random.Range(0.3f, 0.55f), 1f, 1f, 0.9f, false);
        }

        // The standing pieces, just behind the body.
        rim = NewSR(arch, Pale, 62);
        foot = NewSR(footing, Stone, 61);
        PlaceStanding(0f);

        nextRipple = 0.35f;
    }

    private void PlaceStanding(float lift)
    {
        Vector3 p = transform.position;
        if (rim != null) rim.transform.position = new Vector3(p.x, p.y + ArchY + lift, BEHIND_Z);
        if (foot != null) foot.transform.position = new Vector3(p.x, p.y + 0.02f, BEHIND_Z);
    }

    // ---- update --------------------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.deltaTime;
        if (follow == null) { Destroy(gameObject); return; }
        transform.position = follow.position;

        if (!releasing) UpdateHold(dt);
        else UpdateRelease(dt);

        UpdateMotes(dt);
        UpdatePops(dt);

        if (releasing && releaseT >= 0.35f && motes.Count == 0 && pops.Count == 0) Destroy(gameObject);
    }

    private void UpdateHold(float dt)
    {
        PlaceStanding(0f);

        float breathe = 1f + Mathf.Sin(Time.time * 3.2f) * 0.06f;
        float a = Mathf.Lerp(0.25f, 0.85f, block01);

        // The last second of the brace flickers, so its end is never a surprise.
        float remaining = timeLeft01 * PlayerBrace.Duration;
        if (remaining < 1f && Mathf.Sin(Time.time * 38f) < 0f) a *= 0.35f;

        Color c = Pale;
        float s = breathe;
        if (flash > 0f)
        {
            flash -= dt;
            c = Color.white;
            a = 1f;
            s *= 1.12f;
        }
        c.a = a;
        rim.color = c;
        rim.transform.localScale = Vector3.one * s;

        Color f = Stone; f.a = 0.25f + 0.25f * block01;
        foot.color = f;

        // Rings pulsing out behind the body, like the backdrop of the card's art.
        nextRipple -= dt;
        if (nextRipple <= 0f)
        {
            nextRipple = 0.85f;
            AddRing(BEHIND_Z, 0.7f, 2.1f, 0.8f, Stone, Stone, 0.22f + 0.15f * block01, LINE);
        }
    }

    private void UpdateRelease(float dt)
    {
        releaseT += dt;
        float k = Mathf.Clamp01(releaseT / 0.35f);
        PlaceStanding(0.3f * k);
        if (rim != null)
        {
            Color c = Pale; c.a = 0.85f * (1f - k) * (1f - k);
            rim.color = c;
            rim.transform.localScale = Vector3.one * (1f + 0.5f * k);
        }
        if (foot != null) { Color f = Stone; f.a = 0.4f * (1f - k); foot.color = f; }
    }

    private void UpdateMotes(float dt)
    {
        Vector3 here = transform.position;
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.life -= dt;
            if (m.life <= 0f || m.t == null)
            {
                if (m.t != null) Destroy(m.t.gameObject);
                motes.RemoveAt(i);
                continue;
            }
            float k = 1f - m.life / m.maxLife;
            float grow = 1f - (1f - k) * (1f - k);     // fast out, easing
            Color c = Color.Lerp(m.from, m.to, k);
            c.a = m.alpha0 * (1f - k) * (1f - k * 0.3f);

            Vector3 at;
            if (m.followPlayer) at = new Vector3(here.x + m.offset.x, here.y + m.offset.y, m.offset.z);
            else
            {
                m.offset += (Vector3)(m.vel * dt);
                m.vel *= 1f - 5f * dt;
                at = m.offset;
            }

            if (m.lr != null)
            {
                float r = Mathf.Lerp(m.size0, m.size1, grow);
                for (int s = 0; s < RING_SEGMENTS; s++)
                {
                    float ang = s * Mathf.PI * 2f / RING_SEGMENTS;
                    m.lr.SetPosition(s, new Vector3(at.x + Mathf.Cos(ang) * r, at.y + Mathf.Sin(ang) * r, at.z));
                }
                m.lr.startColor = m.lr.endColor = c;
            }
            else
            {
                m.t.position = at;
                m.t.localScale = Vector3.one * Mathf.Lerp(m.size0, m.size1, grow);
                m.sr.color = c;
            }
        }
    }

    private void UpdatePops(float dt)
    {
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            popLife[i] -= dt;
            TextMeshPro t = pops[i];
            if (popLife[i] <= 0f || t == null)
            {
                if (t != null) Destroy(t.gameObject);
                pops.RemoveAt(i); popLife.RemoveAt(i);
                continue;
            }
            float k = 1f - popLife[i] / 0.85f;
            Vector3 p = transform.position;
            t.transform.position = new Vector3(p.x, p.y + HEAD + 0.9f * (1f - (1f - k) * (1f - k)), FRONT_Z);
            Color c = t.color; c.a = k < 0.6f ? 1f : Mathf.Clamp01(1f - (k - 0.6f) / 0.4f); t.color = c;
        }
    }

    // ---- building blocks -----------------------------------------------------------------------

    private Vector3 Chest(float z) { return new Vector3(transform.position.x, transform.position.y + CHEST, z); }

    private void AddSprite(Sprite sprite, Color from, Color to, Vector3 pos, Vector2 vel, float life,
                           float scale0, float scale1, float alpha, bool followPlayer)
    {
        SpriteRenderer sr = NewSR(sprite, from, 63);
        sr.transform.SetParent(null, true);   // free in the world; positioned every frame
        sr.transform.position = pos;
        sr.transform.localScale = Vector3.one * scale0;
        Vector3 here = transform.position;
        motes.Add(new Mote
        {
            t = sr.transform, sr = sr, vel = vel, life = life, maxLife = life,
            from = from, to = to, size0 = scale0, size1 = scale1, alpha0 = alpha,
            followPlayer = followPlayer,
            offset = followPlayer ? new Vector3(pos.x - here.x, pos.y - here.y, pos.z) : pos,
        });
    }

    // A ring centred on the chest that grows from r0 to r1, always `width` thick.
    private void AddRing(float z, float r0, float r1, float life, Color from, Color to, float alpha, float width)
    {
        var go = new GameObject("brace ring");
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

        motes.Add(new Mote
        {
            t = go.transform, lr = lr, life = life, maxLife = life,
            from = from, to = to, size0 = r0, size1 = r1, alpha0 = alpha,
            followPlayer = true, offset = new Vector3(0f, CHEST, z),
        });
    }

    private void Pop(string text)
    {
        GameObject go = new GameObject("BracePop");
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        TMP_FontAsset f = UIType.Display();
        if (f != null) t.font = f;
        t.text = text;
        t.fontSize = 6f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Salvage.Shift;              // the Shift colour, everywhere in the game
        t.outlineColor = new Color32(10, 20, 28, 255);
        t.outlineWidth = 0.25f;
        t.sortingOrder = 70;
        t.rectTransform.sizeDelta = new Vector2(3f, 1.2f);
        Vector3 p = transform.position;
        go.transform.position = new Vector3(p.x, p.y + HEAD, FRONT_Z);
        pops.Add(t);
        popLife.Add(0.85f);
    }

    private SpriteRenderer NewSR(Sprite sprite, Color color, int order)
    {
        var go = new GameObject("brace");
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
        if (sprite == null || unlitShader == null) return null;
        if (!spriteMats.TryGetValue(sprite, out Material m) || m == null)
        {
            m = new Material(unlitShader) { name = "BraceVFX " + sprite.texture.width + "x" + sprite.texture.height, mainTexture = sprite.texture };
            spriteMats[sprite] = m;
        }
        return m;
    }

    private void OnDestroy()
    {
        // Free-standing motes, rings and pops are not children; take them with us.
        foreach (Mote m in motes) if (m.t != null) Destroy(m.t.gameObject);
        foreach (TextMeshPro t in pops) if (t != null) Destroy(t.gameObject);
    }

    // ---- pixel sprites, generated once -----------------------------------------------------------

    private static void EnsureSprites()
    {
        if (arch != null) return;

        unlitShader = Shader.Find("Sprites/Default");
        spriteMats.Clear();
        if (unlitShader != null) lineMat = new Material(unlitShader) { name = "BraceVFX lines" };

        // The rim over the shoulders: the upper part of a ring whose centre sits ARCH_DROP px below
        // the texture's middle, fading out toward its ends. A 2px bright band with a 1px dim band
        // inside it — the stepped, posterised look of the card's art rather than a smooth glow.
        arch = Pixel(56, 34, (x, y) =>
        {
            float cy = y + ARCH_DROP;                              // height above the ring's centre
            if (cy < 0f) return 0f;                                // only the upper half
            float d = Mathf.Sqrt(x * x + cy * cy);
            float band = d > 25f && d <= 27.5f ? 1f : (d > 23f && d <= 25f ? 0.4f : 0f);
            float along = Mathf.Clamp01(cy / 14f);                 // ends fade into nothing
            return band * along;
        });

        // Drawn at close to its real size (it only grows 25%), so its pixels stay the world's pixels.
        disk = Pixel(28, 28, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y) / 14f;
            return d > 1f ? 0f : (d > 0.6f ? 0.45f : 1f);
        });

        shard = Pixel(5, 5, (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 2f ? 1f : 0f);
        dust = Pixel(3, 3, (x, y) => 1f);

        // A flat mark under the feet: the footing the stance is planted on.
        footing = Pixel(48, 8, (x, y) =>
        {
            float e = (x * x) / (23f * 23f) + (y * y) / (3.5f * 3.5f);
            return e > 1f ? 0f : (e > 0.55f ? 1f : 0f);
        });
    }

    // Texture of w x h pixels; alphaAt receives coordinates relative to the centre, in pixels.
    private static Sprite Pixel(int w, int h, System.Func<float, float, float> alphaAt)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                float x = px + 0.5f - w * 0.5f, y = py + 0.5f - h * 0.5f;
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, Mathf.Clamp01(alphaAt(x, y))));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
    }
}
