using UnityEngine;

/// <summary>
/// The samurai's visual language, shared by the playable Samurai (Through and Through) and the
/// Kagemusha: not afterimages of the BODY — those are the Ninja's — but the LINE THE EDGE TOOK,
/// left hanging in the air for a beat after the cut, and a mark on whatever it went through.
///
/// Two pieces, both procedural (FlatUI's one-pixel and soft-glow sprites, no art):
///
///   CutStreak  a hot hairline with a soft wider glow, drawn from the launch point and EXTENDED
///              as the blade travels, then released to fade and thin. Different colour per owner:
///              the player's is warm gold-white, the boss's cold steel-white.
///   CutMark    an X at the point of contact, snapping open and fading. The "it went through
///              you" stamp — placed on each enemy the lunge crossed, at the moment the cut lands.
///
/// ⚠️ SCALE *TO* A SIZE, NEVER *BY* ONE — FlatUI.Pixel() is one pixel, so localScale is divided by
/// the sprite's native world size or the streak comes out as an invisible speck (the Ninja lane
/// telegraph lost two rounds of debugging to exactly that).
/// </summary>
public class CutStreak : MonoBehaviour
{
    private SpriteRenderer core, glow;
    private Vector2 from, to;
    private Color colour;
    private float thickness;
    private float life, t;
    private bool released;

    /// <summary>Start a streak at `from`. Call SetEnd every step while travelling, then Release.</summary>
    public static CutStreak Begin(Vector2 from, Color colour, float thickness = 0.10f, int sortingOrder = 8)
    {
        var go = new GameObject("CutStreak");
        go.AddComponent<TemporaryObject>();
        var s = go.AddComponent<CutStreak>();
        s.from = from; s.to = from; s.colour = colour; s.thickness = thickness;

        s.glow = Strip(go.transform, "Glow", sortingOrder - 1);
        s.core = Strip(go.transform, "Core", sortingOrder);
        s.Redraw(1f);
        return s;
    }

    public void SetEnd(Vector2 end) { if (!released) { to = end; Redraw(1f); } }

    /// <summary>Let go: it hangs for `seconds`, thinning and fading, then destroys itself.</summary>
    public void Release(float seconds = 0.35f) { released = true; life = Mathf.Max(0.05f, seconds); t = 0f; }

    private void Update()
    {
        if (!released) return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        Redraw(1f - k);
        if (k >= 1f) Destroy(gameObject);
    }

    private void Redraw(float k)
    {
        Vector2 d = to - from;
        float len = Mathf.Max(0.05f, d.magnitude);
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Vector2 mid = from + d * 0.5f;

        // Eased: the line stays hot for the first half of its life and goes in the second — a
        // short bright hold reads as a cut; a linear fade reads as smoke.
        float a = Mathf.SmoothStep(0f, 1f, k);
        Size(core, len, thickness * Mathf.Lerp(0.35f, 1f, a));
        Aim(core, mid, ang, 0.04f);
        core.color = new Color(1f, 1f, 1f, a);                       // white-hot centre, always

        Size(glow, len * 1.02f, thickness * 3.2f * Mathf.Lerp(0.5f, 1f, a));
        Aim(glow, mid, ang, 0.05f);
        glow.color = new Color(colour.r, colour.g, colour.b, 0.55f * a);
    }

    internal static SpriteRenderer Strip(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FlatUI.Pixel();
        sr.sortingOrder = order;
        return sr;
    }

    internal static void Size(SpriteRenderer sr, float length, float thickness)
    {
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(
            native.x > 0.0001f ? length / native.x : length,
            native.y > 0.0001f ? thickness / native.y : thickness, 1f);
    }

    internal static void Aim(SpriteRenderer sr, Vector2 centre, float angle, float zLift)
    {
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        sr.transform.position = new Vector3(centre.x, centre.y, PlayPlane.Z - zLift);   // in FRONT of actors
    }
}

/// <summary>An X snapping open at a point of contact and fading. See CutStreak.</summary>
public class CutMark : MonoBehaviour
{
    private SpriteRenderer a, b, flash;
    private Color colour;
    private float size, t;
    private const float OPEN = 0.07f, LIFE = 0.28f;

    public static void Spawn(Vector2 at, Color colour, float size = 1.4f)
    {
        var go = new GameObject("CutMark");
        go.transform.position = new Vector3(at.x, at.y, PlayPlane.Z - 0.06f);
        go.AddComponent<TemporaryObject>();
        var m = go.AddComponent<CutMark>();
        m.colour = colour; m.size = size;

        m.flash = new GameObject("Flash").AddComponent<SpriteRenderer>();
        m.flash.transform.SetParent(go.transform, false);
        m.flash.sprite = FlatUI.SoftGlow();
        m.flash.sortingOrder = 7;

        m.a = CutStreak.Strip(go.transform, "A", 9);
        m.b = CutStreak.Strip(go.transform, "B", 9);
        m.Redraw(0f);
    }

    private void Update()
    {
        t += Time.deltaTime;
        Redraw(t);
        if (t >= LIFE) Destroy(gameObject);
    }

    private void Redraw(float time)
    {
        float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / OPEN));       // snaps open
        float fade = 1f - Mathf.Clamp01((time - OPEN) / (LIFE - OPEN));           // then goes
        float len = size * open;
        float thick = 0.09f * Mathf.Lerp(0.3f, 1f, fade);
        Vector2 c = transform.position;

        CutStreak.Size(a, len, thick); CutStreak.Aim(a, c, 32f, 0.06f);
        CutStreak.Size(b, len * 0.8f, thick); CutStreak.Aim(b, c, -48f, 0.06f);
        a.color = new Color(1f, 1f, 1f, fade);
        b.color = new Color(colour.r, colour.g, colour.b, fade);

        float glow = Mathf.Lerp(size * 0.9f, size * 1.6f, 1f - fade);
        flash.transform.localScale = new Vector3(glow, glow * 0.6f, 1f);
        flash.color = new Color(colour.r, colour.g, colour.b, 0.5f * fade * open);
    }
}
