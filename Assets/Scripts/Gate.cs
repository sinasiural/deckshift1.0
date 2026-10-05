using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A pair of heavy doors hung in a stone arch, held shut by a seal of Shift. Driven by
// Lever/ShiftAltar via Open()/Close()/Toggle(); the level importer wires those automatically.
//
// ── WHY THIS WAS REBUILT AGAIN (2026-08-20) ───────────────────────────────────────────────────
// The previous animation was competent and the designer rejected it as "not great … it just
// doesn't fit in". It didn't, and the reason was not the timing curve — it was that the gate was
// animated as a REALISTIC MEDIEVAL DOOR in a game where doors are opened by magic:
//
//   The ShiftAltar rips motes of Shift out of the air, absorbs them, flashes, and fires a glowing
//   CYAN ORB that flies across the room and BURSTS on the gate. Only then does the gate open.
//
// ...and the gate answered that with a brown, dusty, groaning, 1.6-second carpentry routine that
// acknowledged none of it. Cause and effect were in two different genres. It also spent 1.6
// seconds of a game whose entire thesis is momentum.
//
// So the gate now speaks the game's own language instead of a castle's:
//
//   1. A CLOSED GATE IS VISIBLY SEALED. A hairline of Shift-cyan light breathes in the seam where
//      the two leaves meet. This is information the gate never gave before — it says "locked, and
//      Shift is what locks it", which is what makes a player go looking for the altar or lever.
//   2. OPENING IS AN EVENT, NOT A PROCESS. The seal flares and breaks, a held beat, then the doors
//      are THROWN open and rebound off the jambs. ~0.5s, not ~1.6s.
//   3. THE PARTICLES INVERT. Breaking sheds cyan motes OUTWARD from the seam; re-sealing draws
//      them back INWARD. Stone grit is kept only where the leaves actually strike stone. (The
//      outward/inward inversion is the same trick Blompo's forging→binding rebuild turned on.)
//
// ⚠️ DO NOT re-add the groaning strain sequence, brown dust as the primary particle, or a
// multi-second open. Each was tried and each is what made it read as the wrong game.
//
// ── STILL TRUE FROM THE PREVIOUS REBUILDS ─────────────────────────────────────────────────────
// The art is a stone ARCH with double doors in it, not a portcullis, so the arch never moves and
// the two leaves open by narrowing toward their hinges (which is how this pack draws its own
// doors: "Door Wood 01" runs 37px wide down to 11px at constant height). Pieces are cut by
// Editor/GateArtBaker.
//
// ⚠️ THE COLLIDER MUST BE SWITCHED, NOT MOVED. An older version only translated the transform, so
// the solid box travelled below the floor and left an invisible wall in playable space in every
// room with a gate. Opening drops the collider FIRST, closing restores it LAST — the passage must
// never be solid at a moment the doors visibly are not.
public class Gate : MonoBehaviour
{
    [SerializeField] private bool startOpen = false;
    [SerializeField, Range(0f, 2f)] private float volume = 1f;

    // The seal's colour is the altar's orb colour, deliberately identical - that is the whole point.
    private static readonly Color Seal = new Color(0.45f, 0.9f, 1f, 1f);
    private static readonly Color Grit = new Color(0.52f, 0.45f, 0.38f, 0.85f);

    // --- movement shape (seconds) ---
    private const float BreakTime = 0.10f;   // the seal gives
    private const float HoldTime = 0.07f;    // the beat. the one thing the old version got right.
    private const float ThrowTime = 0.26f;   // the doors are flung
    private const float ReboundTime = 0.09f;
    private const float ShutTime = 0.34f;    // accelerating into the slam
    private const float ResealTime = 0.22f;

    private const float Overshoot = 1.06f;   // past fully-open, so they bounce off the jambs
    private const float LeafOpenScale = 0.07f;  // a door edge-on still shows its thickness
    private const float RestGlow = 0.12f;    // the sealed hairline at rest - deliberately faint

    private BoxCollider2D solid;
    private bool isOpen;
    private Coroutine mover;
    private AudioSource sfxSource;

    private GateArt art;
    private Transform visual;
    private SpriteRenderer archSr, passageSr, leafLSr, leafRSr, seamSr;
    private Transform leafL, leafR, seam;
    private float visScale = 1f;
    private Vector3 visualHome;
    private float sealGlow;                  // 0..1, drives the seam light

    private class Mote
    {
        public SpriteRenderer sr;
        public Vector2 vel;
        public float life, maxLife, size, drag, gravity;
        public Color tint;
        public Vector3 pullTo;               // non-zero = converge here (the re-seal)
        public bool pulls;
    }
    private readonly List<Mote> motes = new List<Mote>();
    private static Sprite dotSprite, seamSprite;

    void Awake()
    {
        solid = GetComponent<BoxCollider2D>();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        BuildVisual();
    }

    void Start()
    {
        sealGlow = startOpen ? 0f : 1f;
        if (startOpen)
        {
            isOpen = true;
            SetLeaves(1f);
            if (solid != null) solid.enabled = false;
        }
        else SetLeaves(0f);
    }

    // Re-dress the importer's single-sprite gate into arch + passage + two leaves + the seal.
    //
    // Done at RUNTIME rather than by changing the importer and re-importing the rooms: GenLevel7/8/9
    // carry hand edits a re-import would destroy, and a re-import also renumbers every fileID, which
    // drops the room out of LevelManager.roomPrefabs (see CLAUDE.md).
    private void BuildVisual()
    {
        visual = transform.Find("Visual");
        if (visual == null)
        {
            var anySr = GetComponentInChildren<SpriteRenderer>(true);
            if (anySr != null) visual = anySr.transform;
        }
        if (visual == null) return;

        archSr = visual.GetComponent<SpriteRenderer>();
        if (archSr == null) return;
        visScale = visual.localScale.x;
        visualHome = visual.localPosition;

        // ⚠️ A gate may carry MORE THAN ONE visual. GenLevel9 shipped with two identical "Visual"
        // children stacked exactly on top of each other. Only the first gets re-dressed, so the
        // survivor would go on drawing a CLOSED gate over the open one forever and the room would
        // look like the lever did nothing.
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c == visual) continue;
            var stray = c.GetComponent<SpriteRenderer>();
            if (stray == null) continue;
            stray.enabled = false;
            Debug.LogWarning("Gate '" + name + "': disabled a duplicate visual child '" + c.name +
                             "'. Remove it from the room prefab.");
        }

        art = Resources.Load<GateArt>("GateArt/gate01");
        // Graceful degradation: with no baked art the gate keeps its single sprite and still opens,
        // just by vanishing. A missing Resources folder must never leave a room unfinishable.
        if (art == null || art.arch == null || art.leafL == null || art.leafR == null)
        {
            Debug.LogWarning("Gate: Resources/GateArt/gate01 missing - falling back to hide-on-open. " +
                             "Run Deckshift -> Bake Gate Art.");
            art = null;
            return;
        }

        int baseOrder = archSr.sortingOrder;
        archSr.sprite = art.arch;

        // The stack goes UP from the sprite's original order, never down: the Ground tilemap draws
        // at Default order 1 and the gate art is wider than the 1-tile gap it stands in, so a
        // passage at order 0 gets swallowed by the floor tiles either side.
        passageSr = MakeLayer("Passage", art.passage, Vector2.zero, baseOrder, 0.02f);
        leafLSr = MakeLayer("LeafL", art.leafL, art.leafLOffset, baseOrder + 1, 0.01f);
        leafRSr = MakeLayer("LeafR", art.leafR, art.leafROffset, baseOrder + 1, 0.01f);
        leafL = leafLSr.transform;
        leafR = leafRSr.transform;

        // The seal: a hairline of light down the join between the leaves. Drawn ABOVE them, because
        // it is light escaping from between the doors rather than something painted on them.
        float cx = (art.openingLeft + art.openingRight) * 0.5f;
        float cy = (art.openingBottom + art.openingTop) * 0.5f;
        seamSr = MakeLayer("Seam", GetSeamSprite(), new Vector2(cx, cy), baseOrder + 3, 0.005f);
        seam = seamSr.transform;
        // ⚠️ Scale TO the opening, not BY it. The seam sprite is 32x128px at PPU 32, i.e. natively
        // 1 x 4 WORLD UNITS - so multiplying by the opening height made it 16 units tall and the
        // seal ran off the top and bottom of the screen as a full-height line. Always divide a
        // desired size by the sprite's own bounds; never assume a procedural sprite is 1x1.
        float openH = art.openingTop - art.openingBottom;
        Vector2 native = seamSr.sprite.bounds.size;
        seam.localScale = new Vector3(0.34f / Mathf.Max(0.001f, native.x),
                                      openH / Mathf.Max(0.001f, native.y), 1f);
        archSr.sortingOrder = baseOrder + 2;

        SetLeaves(0f);
    }

    private SpriteRenderer MakeLayer(string layerName, Sprite sprite, Vector2 offset, int order, float z)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(visual, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, z);
        go.transform.localScale = Vector3.one;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = archSr.sortingLayerID;
        sr.sortingOrder = order;
        return sr;
    }

    // openness: 0 = shut, 1 = fully open (values slightly above 1 are the rebound overshoot).
    private void SetLeaves(float openness)
    {
        if (art == null || leafL == null) return;
        float s = Mathf.Max(0.01f, Mathf.LerpUnclamped(1f, LeafOpenScale, openness));
        leafL.localScale = new Vector3(s, 1f, 1f);
        leafR.localScale = new Vector3(s, 1f, 1f);

        // A leaf turning away from the room catches less light. Deliberately shallow: a first pass
        // went to 0.52 and the leaves fell to roughly the value of the dark passage behind them, so
        // the doors stopped reading as wood. The scene's 0.5-intensity global Light2D halves it again.
        float shade = Mathf.Lerp(1f, 0.74f, Mathf.Clamp01(openness));
        var tint = new Color(shade, shade, shade, 1f);
        if (leafLSr != null) leafLSr.color = tint;
        if (leafRSr != null) leafRSr.color = tint;
    }

    void Update()
    {
        // the sealed hairline breathes, very slowly, so a locked gate is alive but not noisy
        if (seamSr != null)
        {
            float breathe = 1f + 0.30f * Mathf.Sin(Time.time * 1.5f);
            float a = sealGlow * RestGlow * breathe;
            // during a flare sealGlow runs past 1, which is what makes the break read as a burst
            if (sealGlow > 1f) a = RestGlow + (sealGlow - 1f) * 0.55f;
            seamSr.color = new Color(Seal.r, Seal.g, Seal.b, Mathf.Clamp01(a));
        }

        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.life += Time.deltaTime;
            float t = m.life / m.maxLife;
            if (t >= 1f || m.sr == null)
            {
                if (m.sr != null) Destroy(m.sr.gameObject);
                motes.RemoveAt(i);
                continue;
            }
            if (m.pulls)
            {
                // converge: the inverse of the break. Accelerate toward the seam and shrink.
                Vector3 to = m.pullTo - m.sr.transform.position;
                m.vel += (Vector2)to.normalized * 26f * Time.deltaTime;
            }
            else
            {
                m.vel += Vector2.down * m.gravity * Time.deltaTime;
            }
            m.vel *= 1f - m.drag * Time.deltaTime;
            m.sr.transform.position += (Vector3)(m.vel * Time.deltaTime);

            float fade = 1f - t;
            m.sr.color = new Color(m.tint.r, m.tint.g, m.tint.b, m.tint.a * fade * fade);
            float grow = m.pulls ? (1f - 0.6f * t) : (0.8f + 0.5f * t);
            m.sr.transform.localScale = Vector3.one * m.size * grow;
        }
    }

    public void Open() { Set(true); }
    public void Close() { Set(false); }
    public void Toggle() { Set(!isOpen); }

    private void Set(bool open)
    {
        if (open == isOpen) return;
        isOpen = open;
        if (mover != null) StopCoroutine(mover);
        if (visual != null) visual.localPosition = visualHome;
        mover = StartCoroutine(open ? OpenRoutine() : CloseRoutine());
    }

    // BREAK → beat → THROW → rebound. ~0.52s total.
    private IEnumerator OpenRoutine()
    {
        // Collider FIRST. This is also the fix for the old "the barricade is still there" bug:
        // from here nothing about this gate is solid, wherever its art happens to be mid-animation.
        if (solid != null) solid.enabled = false;

        if (art == null) { yield return FallbackOpen(); yield break; }

        // BREAK — the seal lets go. Sharp, and the only loud thing at this point.
        Play(ProcSfx.GateRelease, 1f, 1.16f);
        Shake(0.09f, 0.10f);
        SpawnSparks(SeamPoint(), 14, false);
        float t = 0f;
        while (t < BreakTime)
        {
            t += Time.deltaTime;
            sealGlow = 1f + 2.4f * Mathf.Clamp01(t / BreakTime);   // flare past full
            yield return null;
        }

        // BEAT — total stillness at the top of the flare. Weight is the pause before the movement,
        // not the movement; it is the one idea worth keeping from the version this replaced.
        yield return new WaitForSeconds(HoldTime);

        // THROW — the doors are flung. Ease OUT: all the speed is at the start, because they were
        // released rather than pushed. The seal dies during the first part of the travel.
        t = 0f;
        float sparkAccum = 0f;
        while (t < ThrowTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ThrowTime);
            float eased = 1f - (1f - k) * (1f - k);
            SetLeaves(eased * Overshoot);
            sealGlow = Mathf.Max(0f, (1f - k * 2.2f) * 3.4f);

            // ⚠️ Time-based, NOT a per-frame probability. `if (Random.value < 0.5f)` spawns twice as
            // much at 120fps as at 60, and in slow motion it runs away completely - measured 109
            // live sparks at timeScale 0.05 where normal speed produces about 8.
            sparkAccum += Time.deltaTime * 34f;
            while (sparkAccum >= 1f) { sparkAccum -= 1f; SpawnSparks(SeamPoint(), 1, false); }
            yield return null;
        }
        sealGlow = 0f;

        // REBOUND — they hit the jambs and settle back. This is the only stone in the sequence, so
        // it is the only place grit belongs.
        Play(ProcSfx.GateSeat, 0.95f, 1.14f);
        Shake(0.22f, 0.34f);
        SpawnGrit(JambPoint(-1f), 5);
        SpawnGrit(JambPoint(1f), 5);
        t = 0f;
        while (t < ReboundTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ReboundTime);
            SetLeaves(Mathf.Lerp(Overshoot, 1f, k * k));
            yield return null;
        }
        SetLeaves(1f);
        mover = null;
    }

    // Shut → SLAM → the seal knits back. Deliberately NOT a mirror: opening ends softly at the
    // jambs, closing ends loudly in the middle, so the two are distinguishable with your eyes shut.
    private IEnumerator CloseRoutine()
    {
        if (art == null) { yield return FallbackClose(); yield break; }

        float from = CurrentOpenness();
        float t = 0f;
        while (t < ShutTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ShutTime);
            SetLeaves(Mathf.Lerp(from, 0f, k * k));   // ease IN — accelerating into the slam
            yield return null;
        }
        SetLeaves(0f);

        // SLAM.
        Play(ProcSfx.GateSeat, 1f, 0.92f);
        Shake(0.28f, 0.42f);
        SpawnGrit(SeamPoint(), 9);

        // RE-SEAL — motes are drawn back INTO the seam and the hairline re-ignites. The inversion of
        // the break is the point: out means released, in means bound.
        SpawnSparks(SeamPoint(), 12, true);
        Play(ProcSfx.GateRelease, 0.5f, 0.86f);
        t = 0f;
        while (t < ResealTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ResealTime);
            sealGlow = Mathf.Lerp(0f, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        sealGlow = 1f;

        // Collider LAST.
        if (solid != null) solid.enabled = true;
        mover = null;
    }

    // Back out the current openness from the leaf scale, so an interrupted open closes from wherever
    // it actually got to rather than snapping wide first.
    private float CurrentOpenness()
    {
        if (leafL == null) return isOpen ? 0f : 1f;
        return Mathf.Clamp01(Mathf.InverseLerp(1f, LeafOpenScale, leafL.localScale.x));
    }

    // ---- fallbacks, used only if the baked art is missing ----
    private IEnumerator FallbackOpen()
    {
        float t = 0f;
        while (t < 0.3f) { t += Time.deltaTime; SetAlpha(1f - t / 0.3f); yield return null; }
        SetAlpha(0f);
        mover = null;
    }

    private IEnumerator FallbackClose()
    {
        float t = 0f;
        while (t < 0.3f) { t += Time.deltaTime; SetAlpha(t / 0.3f); yield return null; }
        SetAlpha(1f);
        if (solid != null) solid.enabled = true;
        mover = null;
    }

    private void SetAlpha(float a)
    {
        if (archSr == null) return;
        var c = archSr.color; c.a = a; archSr.color = c;
    }

    // ---- points of interest, in world space, from the baked geometry ----

    private Vector3 SeamPoint()
    {
        if (visual == null) return transform.position;
        if (art == null) return visual.position;
        float cx = (art.openingLeft + art.openingRight) * 0.5f;
        float cy = (art.openingBottom + art.openingTop) * 0.45f;
        return visual.TransformPoint(new Vector3(cx, cy, 0f));
    }

    private Vector3 JambPoint(float side)
    {
        if (visual == null) return transform.position;
        if (art == null) return visual.position;
        float x = side < 0f ? art.openingLeft : art.openingRight;
        float cy = (art.openingBottom + art.openingTop) * 0.45f;
        return visual.TransformPoint(new Vector3(x, cy, 0f));
    }

    private void Play(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        SfxManager.PlayOn(sfxSource, clip, vol * volume);
    }

    // CameraShake.Shake is (INTENSITY, DURATION) - every other caller in the project passes it that
    // way (boss death is 0.6 over 1.6s). An older gate had the two REVERSED, which is part of why a
    // three-tonne door registered as nothing.
    private void Shake(float intensity, float duration)
    {
        if (CameraShake.instance != null) CameraShake.instance.Shake(intensity, duration);
    }

    // Cyan motes of the broken seal. converge = the re-seal, where they are drawn back in.
    private void SpawnSparks(Vector3 pos, int count, bool converge)
    {
        float openH = art != null ? (art.openingTop - art.openingBottom) * visScale : 2f;
        for (int i = 0; i < count; i++)
        {
            float along = Random.Range(-0.42f, 0.42f) * openH;
            Vector3 start = converge
                ? pos + new Vector3(Random.Range(-1.1f, 1.1f), along * 0.6f + Random.Range(-0.4f, 0.4f), 0f)
                : pos + new Vector3(Random.Range(-0.06f, 0.06f), along, 0f);

            var go = new GameObject("GateSpark");
            go.transform.position = start;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDotSprite();
            sr.color = Seal;
            sr.sortingOrder = 14;
            float size = Random.Range(0.10f, 0.20f) * Mathf.Max(0.35f, visScale);
            go.transform.localScale = Vector3.one * size;

            motes.Add(new Mote
            {
                sr = sr,
                // breaking throws them sideways out of the join; sealing lets the pull do the work
                vel = converge ? Vector2.zero : new Vector2(Random.Range(-3.4f, 3.4f), Random.Range(-0.7f, 0.9f)),
                life = 0f,
                maxLife = converge ? Random.Range(0.18f, 0.30f) : Random.Range(0.30f, 0.55f),
                size = size,
                drag = converge ? 0.5f : 3.4f,
                gravity = 0f,                      // it is light, not debris
                tint = Seal,
                pullTo = pos,
                pulls = converge,
            });
        }
    }

    // Stone grit, only where a leaf actually strikes stone.
    private void SpawnGrit(Vector3 pos, int count)
    {
        float spread = 0.4f * Mathf.Max(0.2f, visScale);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("GateDust");
            go.transform.position = pos + new Vector3(Random.Range(-spread, spread), Random.Range(-0.5f, 0.5f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDotSprite();
            sr.color = Grit;
            sr.sortingOrder = 12;
            float size = Random.Range(0.14f, 0.30f) * Mathf.Max(0.35f, visScale);
            go.transform.localScale = Vector3.one * size;
            motes.Add(new Mote
            {
                sr = sr,
                vel = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(0.4f, 1.6f)),
                life = 0f,
                maxLife = Random.Range(0.30f, 0.55f),
                size = size,
                drag = 2.2f,
                gravity = 3.5f,
                tint = Grit,
                pulls = false,
            });
        }
    }

    // soft radial dot, generated once and shared (house pattern)
    private static Sprite GetDotSprite()
    {
        if (dotSprite != null) return dotSprite;
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return dotSprite;
    }

    // The seal itself: a vertical line of light, hottest along its centre and tapering out at both
    // ends so it does not stop dead at the lintel and the threshold.
    private static Sprite GetSeamSprite()
    {
        if (seamSprite != null) return seamSprite;
        int w = 32, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs(x / (float)(w - 1) - 0.5f) * 2f;   // 0 centre .. 1 edge
                float v = Mathf.Abs(y / (float)(h - 1) - 0.5f) * 2f;
                float across = Mathf.Clamp01(1f - u);
                across *= across * across;                             // tight core, soft bloom
                float along = Mathf.Clamp01(1f - Mathf.Pow(v, 4f));    // fades only near the ends
                float a = across * along;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        seamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        return seamSprite;
    }
}
