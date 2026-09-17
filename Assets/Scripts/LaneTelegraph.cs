using UnityEngine;

/// <summary>
/// A boss's "I am about to travel along THIS line" warning, drawn in the world before the attack.
///
/// Built from the lessons the Ninja's dash telegraph paid for (BossDesign_Ninja.md §7d), as a
/// standalone class so the next boss does not have to copy a nested one:
///
///   CUT      a thin bright hairline at blade height — the path the EDGE takes. It reads because
///            it is thin and bright, not because it is big and dim.
///   STREAKS  four hairline outriggers thinning toward the edges. They state the hit volume the
///            way a filled band would, but read as SPEED LINES rather than as a red box ruled
///            across the room (which is what a solid band photographs as, however faint).
///   NOTCH    a tick at the terminus. Where he STOPS is the single most useful thing a player can
///            know about a charge; it is what makes baiting him into a wall a real play.
///   GHOST    optional premonition of the attacker standing at the end, fading IN as they commit.
///
/// Nothing GROWS: the geometry is full length on frame one — a telegraph's job is done in its
/// first frame — and only the intensity ramps as the attacker loads.
///
/// ⚠️ SCALE *TO* A SIZE, NEVER *BY* ONE. `FlatUI.Pixel()` is a ONE-PIXEL sprite whose native world
/// size is a fraction of a unit. Setting localScale to (length, height) directly produced a lane
/// measuring 0.08 x 0.02 world units — a speck, invisible, with every value in code reading
/// correct. Two rounds of "the alpha must be too low" were spent before the bounds were measured.
///
/// ⚠️ THE ALPHA FLOOR IS A REAL FLOOR. It ramps linearly from a value that is already readable;
/// an ease-in from ~0 makes the lane appear only in the last instant of the wind-up, which is the
/// one moment it is too late to help. Both ends are picked by screenshot (linear colour space).
/// </summary>
public class LaneTelegraph
{
    public struct Style
    {
        public Color color;
        public float cutThickness;      // world units — the EDGE's path, thin
        public float alphaStart;        // readable on frame one
        public float alphaEnd;          // what it reaches at full commitment
        public float streakAlpha;       // the outriggers' base alpha, kept low: context, not warning

        public static Style Default(Color c) => new Style
        {
            color = c, cutThickness = 0.11f, alphaStart = 0.55f, alphaEnd = 0.95f, streakAlpha = 0.11f
        };
    }

    private GameObject root;
    private SpriteRenderer cut, notch;
    private SpriteRenderer[] streaks;
    private Style style;

    // Where the outriggers sit, as a fraction of the volume's half-height. The outer pair marks the
    // real extent of the hit box; the notch confirms it at the far end.
    private static readonly float[] StreakAt = { -0.86f, -0.44f, 0.44f, 0.86f };

    /// <param name="premonitionRig">If given, a fading-in ghost of this rig is stood at `to`.</param>
    public static LaneTelegraph Build(Vector2 from, Vector2 to, float height, Style style,
                                      Transform premonitionRig = null, Color? premonitionTint = null,
                                      float premonitionLife = 0.5f)
    {
        var t = new LaneTelegraph { style = style };
        t.root = new GameObject("LaneTelegraph");
        t.root.AddComponent<TemporaryObject>();

        t.streaks = new SpriteRenderer[StreakAt.Length];
        for (int i = 0; i < StreakAt.Length; i++) t.streaks[i] = Strip(t.root.transform, 2);
        t.cut = Strip(t.root.transform, 3);
        t.notch = Strip(t.root.transform, 3);

        if (premonitionRig != null)
        {
            // Positioned by moving the ghost's ROOT after Snapshot — its children keep local
            // offsets, so the whole figure travels together. Its lifetime ends on its own at the
            // moment of launch, so it lands as the attacker goes rather than blinking out first.
            var ghost = GhostTrail.Snapshot(premonitionRig,
                                            premonitionTint ?? new Color(style.color.r, style.color.g, style.color.b, 0.42f),
                                            Mathf.Max(0.05f, premonitionLife), 3, true);
            if (ghost != null) ghost.transform.position += (Vector3)(to - from);
        }

        t.Place(from, to, height);
        t.SetIntensity(0f);
        return t;
    }

    private static SpriteRenderer Strip(Transform parent, int order)
    {
        var go = new GameObject("Strip");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FlatUI.Pixel();
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>Re-aim. Call every frame of the wind-up so the start stays welded to the attacker.</summary>
    public void Place(Vector2 from, Vector2 to, float height)
    {
        if (root == null) return;

        Vector2 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f) { delta = Vector2.right; len = 0.01f; }
        Vector2 dir = delta / len;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 mid = from + dir * len * 0.5f;
        Vector2 up = new Vector2(-dir.y, dir.x);   // perpendicular, so a diagonal lane works too

        for (int i = 0; i < streaks.Length; i++)
        {
            float off = StreakAt[i] * height * 0.5f;
            float inset = Mathf.Abs(StreakAt[i]) * len * 0.10f;   // tapers toward the ends
            Size(streaks[i], Mathf.Max(0.1f, len - inset * 2f), style.cutThickness * 0.55f);
            Aim(streaks[i], mid + up * off, ang, 0.05f);
        }

        Size(cut, len, style.cutThickness);
        Aim(cut, mid, ang, 0.06f);

        Size(notch, style.cutThickness * 1.6f, height * 0.85f);
        Aim(notch, to, ang, 0.06f);
    }

    private static void Size(SpriteRenderer sr, float length, float thickness)
    {
        Vector2 native = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(
            native.x > 0.0001f ? length / native.x : length,
            native.y > 0.0001f ? thickness / native.y : thickness, 1f);
    }

    // Scale is applied in LOCAL space, so rotation is set after it or the strip shears.
    private static void Aim(SpriteRenderer sr, Vector2 centre, float angle, float zLift)
    {
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        sr.transform.position = new Vector3(centre.x, centre.y, PlayPlane.Z + zLift);
    }

    /// <param name="k">0 on the first frame of the wind-up, 1 at full commitment.</param>
    public void SetIntensity(float k)
    {
        if (root == null) return;
        Color c = style.color;
        float a = Mathf.Lerp(style.alphaStart, style.alphaEnd, k);
        cut.color = new Color(c.r, c.g, c.b, a);
        notch.color = new Color(c.r, c.g, c.b, a * 0.85f);

        // Densest along the blade's own line, merely present at the extremes — which is true.
        for (int i = 0; i < streaks.Length; i++)
        {
            float fade = 1f - Mathf.Abs(StreakAt[i]) * 0.55f;
            streaks[i].color = new Color(c.r, c.g, c.b, style.streakAlpha * fade * Mathf.Lerp(1.4f, 2.6f, k));
        }
    }

    public void Clear()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}
