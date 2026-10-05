using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The tutorial's Stagger lesson: a crack in the floor that drinks Shift.
//
// Stagger only ever appears at ZERO Shift, and a tutorial player starts with 40 and spends maybe ten,
// so without help nobody would meet it before their first real run, where it arrives mid-fight as
// a card they have never seen. This puts the player in a pit they need a jump to leave, takes every
// point of Shift they carry, and lets the real Stagger rule do the rest: DeckManager conjures the card
// the moment Shift reads 0, and playing it pays the +2 Shift they need to jump out.
//
// THE LANGUAGE is the one the gate and the altar already speak: Shift is the cyan light, and a seam
// of that light in stone means "Shift lives here". The gate's seam LOCKS with it; this one DRINKS it.
// So it breathes, idle motes sink into it, and when it drinks, the motes are pulled out of the player
// and down into the floor while the counter ticks to zero. Not a label changing: an event.
//
// Fires once. A respawn afterwards (TutorialRoom tops Shift up to 10) does not re-drain, so a player
// who dies in the pit is not trapped in a loop.
public class TutorialShiftDrain : MonoBehaviour
{
    [Tooltip("Width of floor the crack spans, in world units.")]
    public float width = 6f;

    [Tooltip("How far either side of the crack's centre the player may land and still set it off.")]
    public float reach = 5f;

    private const float LandBeat = 0.25f;     // a held breath between landing and the drink
    private const float DrinkTime = 1.1f;
    private const float IdleAlpha = 0.34f;    // screenshot-calibrated: unlit sprites read bright
    private const float SpentAlpha = 0.10f;

    private static readonly Color Cyan = Salvage.Shift;

    private SpriteRenderer crack;
    private SpriteRenderer glow;
    private AudioSource sfx;
    private PlayerController player;
    private Rigidbody2D playerBody;

    private bool spent;
    private float flare;          // 0..1 extra brightness, eased back down
    private float idleClock;

    private class Mote { public SpriteRenderer sr; public Vector3 from; public float t, dur, size; }
    private readonly List<Mote> motes = new List<Mote>();

    private static Sprite crackSprite, glowSprite, dotSprite;

    private void Awake()
    {
        BuildVisuals();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;   // 2D: the drink must be heard wherever the camera sits
    }

    private void Start()
    {
        player = GameManager.instance != null ? GameManager.instance.player : FindFirstObjectByType<PlayerController>();
        if (player != null) playerBody = player.GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Breathing, dimmer once it has fed.
        float breathe = 1f + Mathf.Sin(Time.time * 1.6f) * 0.25f;
        float baseAlpha = spent ? SpentAlpha : IdleAlpha;
        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 1.4f);
        SetAlpha(crack, Mathf.Clamp01(baseAlpha * breathe + flare));
        SetAlpha(glow, Mathf.Clamp01((baseAlpha * 0.5f) * breathe + flare * 0.6f));

        // While hungry it is always drinking a little: loose motes sink into the seam. This is what
        // tells a player peeking down from above that the thing in the pit takes Shift, before it
        // ever touches theirs.
        if (!spent)
        {
            idleClock += Time.deltaTime;
            while (idleClock > 0.35f)
            {
                idleClock -= 0.35f;
                Vector3 from = transform.position + new Vector3(Random.Range(-width / 2f, width / 2f),
                                                                Random.Range(0.8f, 2.2f), 0f);
                SpawnMote(from, 1.4f, 0.10f);
            }
        }
        StepMotes();

        if (!spent && PlayerHasLanded()) StartCoroutine(Drink());
    }

    private bool PlayerHasLanded()
    {
        if (player == null) return false;
        Vector3 p = player.transform.position;
        Vector3 here = transform.position;
        if (Mathf.Abs(p.x - here.x) > reach) return false;
        if (p.y > here.y + 0.4f || p.y < here.y - 1f) return false;    // on the pit floor, not above it
        return playerBody == null || Mathf.Abs(playerBody.linearVelocity.y) < 0.1f;
    }

    private IEnumerator Drink()
    {
        spent = true;

        // BEAT: it notices you.
        flare = 0.35f;
        yield return new WaitForSeconds(LandBeat);

        // DRINK: motes are pulled out of the player and down into the seam while the counter falls.
        if (sfx != null) SfxManager.PlayOn(sfx, ProcSfx.WellDraw, 0.9f);
        int start = player != null ? player.currentShift : 0;
        float spawnClock = 0f;
        for (float t = 0f; t < DrinkTime; t += Time.deltaTime)
        {
            float k = t / DrinkTime;
            if (player != null)
            {
                // Direct write, not SpendShift: this is a scripted beat, not a cost the player chose,
                // so it must not count toward oaths or Nest Egg bookkeeping.
                player.currentShift = Mathf.RoundToInt(start * (1f - k));

                spawnClock += Time.deltaTime;
                while (spawnClock > 0.03f)
                {
                    spawnClock -= 0.03f;
                    Vector3 chest = player.transform.position + new Vector3(Random.Range(-0.3f, 0.3f),
                                                                            Random.Range(0.6f, 1.4f), 0f);
                    SpawnMote(chest, Random.Range(0.35f, 0.55f), 0.14f);
                }
            }
            flare = Mathf.Max(flare, 0.25f + 0.4f * k);
            yield return null;
        }
        if (player != null) player.currentShift = 0;   // Stagger appears on the next DeckManager tick

        // SATED: one last flare, a thud, then it goes dull.
        flare = 0.9f;
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.18f, 0.25f);
    }

    // ---- motes -----------------------------------------------------------------------------------

    private void SpawnMote(Vector3 from, float dur, float size)
    {
        var go = new GameObject("DrainMote");
        go.transform.SetParent(transform, true);
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DotSprite();
        sr.sortingOrder = 32;   // over the actors' sprites, like the Well's motes
        sr.color = Cyan;
        go.transform.localScale = Vector3.one * size;
        motes.Add(new Mote { sr = sr, from = from, t = 0f, dur = dur, size = size });
    }

    private void StepMotes()
    {
        Vector3 sink = transform.position + Vector3.up * 0.05f;
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.t += Time.deltaTime;
            float k = Mathf.Clamp01(m.t / m.dur);
            if (m.sr == null || k >= 1f)
            {
                if (m.sr != null) Destroy(m.sr.gameObject);
                motes.RemoveAt(i);
                continue;
            }
            // Ease IN: sucked, not floated — it accelerates the whole way down.
            float e = k * k;
            Vector3 target = new Vector3(Mathf.Lerp(m.from.x, sink.x, 0.35f), sink.y, sink.z);
            m.sr.transform.position = Vector3.Lerp(m.from, target, e);
            m.sr.transform.localScale = Vector3.one * m.size * (1f - 0.6f * e);
            SetAlpha(m.sr, Mathf.Sin(k * Mathf.PI) * 0.9f);
        }
    }

    // ---- visuals ---------------------------------------------------------------------------------

    private void BuildVisuals()
    {
        crack = MakeRenderer("Crack", CrackSprite(), 3);
        // ⚠️ Scale TO a size, never BY it: the procedural sprite is not one unit wide.
        float native = crack.sprite.bounds.size.x;
        crack.transform.localScale = new Vector3(width / native, width / native, 1f);
        crack.transform.localPosition = new Vector3(0f, 0.04f, 0f);

        glow = MakeRenderer("Glow", GlowSprite(), 2);
        Vector3 gb = glow.sprite.bounds.size;
        glow.transform.localScale = new Vector3(width * 1.1f / gb.x, 2.4f / gb.y, 1f);
        glow.transform.localPosition = new Vector3(0f, 0.05f, 0f);   // pivot is its bottom edge: rises off the floor
    }

    private SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;   // above the Ground tilemap (Default, order 1)
        sr.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
        return sr;
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    // A jagged hairline: bright core, soft edges, broken in places like a real fracture.
    private static Sprite CrackSprite()
    {
        if (crackSprite != null) return crackSprite;
        const int W = 192, H = 16;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var clear = new Color[W * H];
        tex.SetPixels(clear);
        float y = H / 2f;
        for (int x = 0; x < W; x++)
        {
            // A random walk, pulled back toward the middle so it never leaves the texture.
            y += (Mathf.PerlinNoise(x * 0.09f, 1.7f) - 0.5f) * 1.6f + (H / 2f - y) * 0.08f;
            float edgeFade = Mathf.Clamp01(Mathf.Min(x, W - 1 - x) / 14f);
            float broken = Mathf.PerlinNoise(x * 0.05f + 9f, 4.2f) < 0.3f ? 0.35f : 1f;
            for (int py = 0; py < H; py++)
            {
                float d = Mathf.Abs(py + 0.5f - y);
                float a = Mathf.Clamp01(1.6f - d) + Mathf.Clamp01(1f - d / 3.5f) * 0.35f;
                if (a <= 0f) continue;
                tex.SetPixel(x, py, new Color(1f, 1f, 1f, Mathf.Clamp01(a) * edgeFade * broken));
            }
        }
        tex.Apply();
        crackSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);
        return crackSprite;
    }

    // Soft light rising off the seam. ⚠️ Falls off on each axis INDEPENDENTLY: a round radial
    // stretched this wide becomes a streak with a hot core rather than a band of light.
    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int W = 64, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float fx = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(x, W - 1 - x) / 22f));
                // Cubed, over a taller texture: at fy² the faint top rows still composited to a
                // visible hard line (linear colour space lifts low alphas), measured on screen.
                float fy = 1f - Mathf.Clamp01(y / (float)(H - 1));   // brightest at the floor
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, fx * fy * fy * fy));
            }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 32f);
        return glowSprite;
    }

    private static Sprite DotSprite()
    {
        if (dotSprite != null) return dotSprite;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return dotSprite;
    }
}
