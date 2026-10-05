using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bloodlust's look. Blood-red drops circle the player while it is active, passing behind the body
/// on the far side of the ring; they blink through the last second and a half (blinking is how this
/// game says "about to expire", see the gravity reversal warning). Every heal sends a few drops
/// arcing from the enemy that was hit into the player's chest, so you can SEE where the health came
/// from — an economy effect is invisible otherwise.
///
/// Self-building, no prefab or art (house pattern, like CardAimIndicator). Parented to the player,
/// so it carries through a room change exactly as long as the window does.
/// </summary>
public class BloodlustVFX : MonoBehaviour
{
    private static readonly Color Blood = new Color(0.62f, 0.05f, 0.15f, 1f);
    private static readonly Color BloodLight = new Color(0.95f, 0.38f, 0.45f, 1f);

    private const int Orbiters = 5;
    private const float OrbitRadius = 0.78f;
    private const float OrbitFlatten = 0.5f;       // a ring seen from the side
    private const float OrbitSpeed = 2.4f;         // radians per second
    private const float WarnSeconds = 1.5f;
    private const float FadeSeconds = 0.25f;
    private const int SortingOrder = 45;

    private Transform follow;
    private Vector2 chestOffset = new Vector2(0f, 0.84f);
    private float duration;
    private float age;
    private bool ending;
    private float fade = 1f;

    private SpriteRenderer[] orbit;
    private readonly List<Mote> motes = new List<Mote>();

    private class Mote
    {
        public SpriteRenderer sr;
        public Vector2 from, control;
        public float t, length;
    }

    private static Sprite dropSprite;

    public static BloodlustVFX Spawn(PlayerController player, float duration)
    {
        var go = new GameObject("BloodlustVFX");
        go.transform.SetParent(player.transform, false);
        var v = go.AddComponent<BloodlustVFX>();
        v.follow = player.transform;
        v.duration = duration;
        CapsuleCollider2D cap = player.GetComponent<CapsuleCollider2D>();
        if (cap != null) v.chestOffset = cap.offset;
        v.Build();
        return v;
    }

    /// <summary>End the window now (fades out, then removes itself once its drops have landed).</summary>
    public void End() { ending = true; }

    /// <summary>A heal landed: drops fly from <paramref name="from"/> into the player.</summary>
    public void Draw(Vector3 from, float heal)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(heal / 2f), 2, 6);
        for (int i = 0; i < n; i++)
        {
            Vector2 start = (Vector2)from + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.3f, 1.1f));
            Vector2 chest = Chest();
            // Lift the arc above the straight line so the drops are thrown, not slid.
            Vector2 control = Vector2.Lerp(start, chest, 0.5f) + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.0f, 2.0f));
            motes.Add(new Mote
            {
                sr = MakeDrop("Sip"),
                from = start,
                control = control,
                t = -i * 0.04f,                      // a short stagger so they arrive as a trickle
                length = Random.Range(0.32f, 0.46f),
            });
        }
    }

    private Vector2 Chest() => (Vector2)follow.position + chestOffset;

    private void Build()
    {
        orbit = new SpriteRenderer[Orbiters];
        for (int i = 0; i < Orbiters; i++) orbit[i] = MakeDrop("Orbit" + i);
    }

    private void Update()
    {
        if (follow == null) { Destroy(gameObject); return; }

        float dt = Time.deltaTime;
        age += dt;
        if (age >= duration) ending = true;
        if (ending) fade = Mathf.Max(0f, fade - dt / FadeSeconds);

        // --- the ring ---
        float left = duration - age;
        bool blinkOff = !ending && left < WarnSeconds && Mathf.Repeat(age * 8f, 1f) < 0.5f;
        Vector2 chest = Chest();
        for (int i = 0; i < orbit.Length; i++)
        {
            float a = age * OrbitSpeed + i * (Mathf.PI * 2f / Orbiters);
            float s = Mathf.Sin(a);
            // The far half of the ring sits just behind the play plane, so the body hides it.
            float z = s > 0f ? PlayPlane.Z + 0.0005f : PlayPlane.Z - 0.05f;
            float bob = Mathf.Sin(age * 3f + i) * 0.05f;
            orbit[i].transform.position = new Vector3(chest.x + Mathf.Cos(a) * OrbitRadius,
                                                      chest.y + s * OrbitRadius * OrbitFlatten + bob, z);
            orbit[i].color = new Color(1f, 1f, 1f, blinkOff ? 0f : fade);
        }

        // --- drops flying in ---
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.t += dt;
            if (m.t < 0f) { m.sr.enabled = false; continue; }
            m.sr.enabled = true;

            float k = Mathf.Clamp01(m.t / m.length);
            float e = k * k;                                        // ease in: thrown, then pulled home
            Vector2 target = Chest();
            Vector2 p = (1 - e) * (1 - e) * m.from + 2 * (1 - e) * e * m.control + e * e * target;
            m.sr.transform.position = new Vector3(p.x, p.y, PlayPlane.Z - 0.06f);
            m.sr.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
            if (k >= 1f)
            {
                Destroy(m.sr.gameObject);
                motes.RemoveAt(i);
            }
        }

        if (ending && fade <= 0f && motes.Count == 0) Destroy(gameObject);
    }

    private SpriteRenderer MakeDrop(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DropSprite();
        sr.sortingOrder = SortingOrder;
        return sr;
    }

    // A 3x4 drop, rounded at the bottom, with one lit pixel on the upper left (the game's light
    // comes from the upper left). 16 PPU, so each texel is two of the game's pixels.
    private static Sprite DropSprite()
    {
        if (dropSprite != null) return dropSprite;
        var tex = new Texture2D(3, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color clear = new Color(0, 0, 0, 0);
        // rows bottom (y = 0) to top (y = 3)
        string[] rows = { ".#.", "###", "+##", ".#." };
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 3; x++)
            {
                char c = rows[y][x];
                tex.SetPixel(x, y, c == '#' ? Blood : c == '+' ? BloodLight : clear);
            }
        tex.Apply();
        dropSprite = Sprite.Create(tex, new Rect(0, 0, 3, 4), new Vector2(0.5f, 0.5f), 16f);
        dropSprite.name = "BloodlustDrop";
        return dropSprite;
    }
}
