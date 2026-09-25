using System.Collections;
using UnityEngine;

// Procedural "boss defeated!" celebration, spawned at the boss's position the instant it dies.
// Fully self-building (no art assets, no prefab) and self-destroying, in the same house style as
// ChestOpenVFX / ShockwaveVFX — sprites are generated in code and cached statically.
//
// It runs on its OWN GameObject, NOT on the boss: EnemyHealth.Die() destroys the boss the same frame
// it fires OnDied, so anything parented to the boss would die with it. This object survives and directs
// the whole sequence: a freeze-frame + gold screen flash to mask the boss popping out, camera focus +
// a brief slow-mo, staggered eruption bursts, big acid-green shockwave rings, a light pillar, and a
// shower of REAL collectible gold + shift-crystal prefabs that erupt, arc out, and settle on the floor
// (or hover in mid-air if the boss died airborne) — all grabbable by the player.
public class BossDeathVFX : MonoBehaviour
{
    // Gold is the LOOT and never changes — it is the same gold the player picks up, in every boss
    // room, and recolouring it per boss would make the reward read as a different substance.
    private static readonly Color Gold = new Color(1f, 0.82f, 0.28f);

    // ⚠️ THE BURST COLOUR IS THE BOSS'S, NOT THIS CLASS'S. It was a hardcoded acid green, which is the
    // Moss Knight's identity — the whole point of that boss — and firing it out of a ninja would say
    // the wrong thing about what just died. It stays the default so the Moss Knight is untouched.
    private static readonly Color Acid = new Color(0.55f, 0.95f, 0.35f);
    private Color accent = Acid;

    const int SORT = 40;   // above the world + the chest burst

    private float groundY;
    private bool airborne;              // true = died mid-air → loot floats instead of falling to a floor
    private GameObject goldPrefab, shiftPrefab;
    private AudioClip sound;
    private float volume;
    private int goldCount = 14;
    private int crystalCount = 5;
    private bool started;

    private bool slowActive;   // guards the global time-scale so OnDestroy can always restore it

    private static Sprite glowSprite, ringSprite, beamSprite;

    // Called by the boss right after it dies (center = this object's spawn position).
    /// <param name="accent">
    /// The burst colour. Omit for the acid green this was written with; pass the boss's own colour
    /// for anything that is not made of moss.
    /// </param>
    public void Play(float groundY, bool airborne, GameObject goldPrefab, GameObject shiftPrefab,
                     AudioClip sound, float volume, int goldCount, int crystalCount, Color? accent = null)
    {
        if (accent.HasValue) this.accent = accent.Value;
        this.groundY = groundY;
        this.airborne = airborne;
        this.goldPrefab = goldPrefab;
        this.shiftPrefab = shiftPrefab;
        this.sound = sound;
        this.volume = volume;
        this.goldCount = Mathf.Max(0, goldCount);
        this.crystalCount = Mathf.Max(0, crystalCount);
        if (!started) { started = true; StartCoroutine(Run()); }
    }

    private IEnumerator Run()
    {
        Vector3 center = transform.position;

        // --- Beat 1: the death punch. Freeze-frame + gold flash mask the boss vanishing. ---
        // ⚠️ NEVER SILENT. All three bosses once passed an empty `deathSound` here, so every boss death
        // in the game played to no sound at all. This is the one place every boss death passes
        // through, so the fallback lives here and a boss added later cannot reintroduce the gap.
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;                 // 2D — always clearly audible across the arena
            SfxManager.PlayOn(src, sound != null ? sound : ProcSfx.BossDeath, volume);
        }

        if (HitStop.instance != null) HitStop.instance.Stop(0.12f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.6f, 1.6f);
        if (CameraFollow.instance != null) CameraFollow.instance.FocusOn(transform);
        StartCoroutine(ScreenFlashRoutine(new Color(1f, 0.9f, 0.5f, 0.7f), 0.4f));
        StartCoroutine(SlowMoRoutine());

        // Big central flash + a light pillar shooting up out of the collapse.
        StartCoroutine(FlashRoutine(center, 3.2f, Color.Lerp(Gold, Color.white, 0.3f), 0.35f));
        StartCoroutine(BeamRoutine(center, 5f));

        // --- Beat 2: staggered shockwave rings + secondary eruptions ripple out. ---
        StartCoroutine(RingRoutine(center, 0.0f, 4.5f, accent));
        StartCoroutine(RingRoutine(center, 0.12f, 3.2f, Gold));
        StartCoroutine(RingRoutine(center, 0.26f, 5.5f, accent));
        StartCoroutine(SecondaryBurstsRoutine(center));

        // --- Beat 3: the loot. Real collectible gold + shift crystals erupt, arc out, and settle. ---
        for (int i = 0; i < goldCount; i++) StartCoroutine(LootRoutine(goldPrefab));
        for (int i = 0; i < crystalCount; i++) StartCoroutine(LootRoutine(shiftPrefab));

        // A sparkle of rising motes on top of it all.
        for (int i = 0; i < 18; i++) StartCoroutine(MoteRoutine(center));

        // Let the camera linger on the kill, then ease back to the player.
        yield return new WaitForSecondsRealtime(1.5f);
        if (CameraFollow.instance != null) CameraFollow.instance.ReleaseFocus();

        // Keep alive while the loot settles (and any mid-air bob plays), then clean up. The dropped
        // pickups are their own root objects, so they persist and stay collectible after this dies.
        yield return new WaitForSeconds(4f);
        Destroy(gameObject);
    }

    // Brief, robust slow-mo for the kill. Uses unscaled time internally and always restores the
    // time-scale (also in OnDestroy) so a fire-and-forget object can never leave the game slowed.
    private IEnumerator SlowMoRoutine()
    {
        yield return new WaitForSecondsRealtime(0.14f);   // let the freeze-frame land first
        slowActive = true;
        Time.timeScale = 0.35f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(0.55f);   // savour the moment (real seconds)

        float t = 0f, ramp = 0.6f;
        while (t < ramp)
        {
            Time.timeScale = Mathf.Lerp(0.35f, 1f, t / ramp);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        slowActive = false;
    }

    private void OnDestroy()
    {
        if (slowActive) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }
    }

    // A few small delayed flash+ring puffs around the center — the boss cracking apart before it blows.
    private IEnumerator SecondaryBurstsRoutine(Vector3 center)
    {
        int bursts = 4;
        for (int i = 0; i < bursts; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.06f, 0.18f));
            Vector3 p = center + new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(-0.3f, 1.6f), 0f);
            StartCoroutine(FlashRoutine(p, Random.Range(1.1f, 1.9f), Color.Lerp(accent, Color.white, 0.4f), 0.25f));
            StartCoroutine(RingRoutine(p, 0f, Random.Range(1.2f, 2.2f), accent));
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.18f, 0.5f);
        }
    }

    // Full-screen colour flash that fades out. Self-contained ScreenSpaceOverlay canvas, unscaled so it
    // plays cleanly through the freeze-frame; destroys itself when done.
    private IEnumerator ScreenFlashRoutine(Color color, float duration)
    {
        GameObject canvasGO = new GameObject("BossDeathFlash");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        GameObject imgGO = new GameObject("Flash");
        imgGO.transform.SetParent(canvasGO.transform, false);
        UnityEngine.UI.Image img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        float startA = color.a, t = 0f;
        while (t < duration)
        {
            float n = 1f - (t / duration);
            Color c = color; c.a = startA * n * n; img.color = c;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(canvasGO);
    }

    // A bright radial pop of light.
    private IEnumerator FlashRoutine(Vector3 pos, float maxScale, Color color, float life)
    {
        SpriteRenderer sr = MakeSprite("Flash", GetGlowSprite(), color, pos, SORT + 3);
        float t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, maxScale, EaseOut(n));
            Color c = color; c.a = 1f - n; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // Expanding ground/air shockwave ring.
    private IEnumerator RingRoutine(Vector3 pos, float delay, float maxR, Color color)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SpriteRenderer sr = MakeSprite("Ring", GetRingSprite(), color, pos, SORT + 1);
        float life = 0.55f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, maxR, EaseOut(n)) * 2f;
            Color c = color; c.a = 1f - n; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // A tall pillar of light bursting up out of the collapse, then fading.
    private IEnumerator BeamRoutine(Vector3 pos, float height)
    {
        SpriteRenderer sr = MakeSprite("Beam", GetBeamSprite(), Color.Lerp(Gold, Color.white, 0.35f), pos, SORT);
        float life = 0.7f, t = 0f;
        while (t < life)
        {
            float n = t / life;
            float grow = EaseOut(Mathf.Min(1f, n * 2.5f));
            sr.transform.localScale = new Vector3(0.9f * (1f - n * 0.3f), height * grow, 1f);
            Color c = sr.color; c.a = (1f - n) * 0.9f; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // A single glowing mote flung up-and-out, arcing down while it twinkles out.
    private IEnumerator MoteRoutine(Vector3 center)
    {
        Color tint = Random.value < 0.6f ? Gold : accent;
        SpriteRenderer sr = MakeSprite("Mote", GetGlowSprite(), tint, center, SORT + 4);
        Transform tr = sr.transform;
        float ang = Mathf.Deg2Rad * Random.Range(40f, 140f);
        Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(3f, 8f);
        float baseScale = Random.Range(0.12f, 0.3f);
        float life = Random.Range(0.7f, 1.3f), t = 0f;
        float phase = Random.value * 6.28f;
        Color moteColor = Color.Lerp(tint, Color.white, 0.5f);
        while (t < life)
        {
            vel.y += Physics2D.gravity.y * 0.4f * Time.deltaTime;
            tr.position += (Vector3)(vel * Time.deltaTime);
            float n = t / life;
            float twinkle = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 18f + phase);
            tr.localScale = Vector3.one * baseScale * (1f - n * 0.5f) * twinkle;
            Color c = moteColor; c.a = (1f - n) * twinkle; sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // Spawns ONE real collectible prefab (gold or shift crystal) and erupts it out of the boss. It's a
    // live pickup the whole time — the routine only drives its transform, so the player can grab it at
    // any point (grabbing destroys it via its own OnTriggerEnter2D, which ends this loop). On a grounded
    // death it arcs under gravity, bounces, and settles on the floor; on a mid-air death it eases out,
    // decelerates, and hovers in place with a gentle bob (loot floating in the air).
    private IEnumerator LootRoutine(GameObject prefab)
    {
        if (prefab == null) yield break;

        Vector3 spawn = transform.position + (Vector3)(Random.insideUnitCircle * 0.15f);
        GameObject go = Instantiate(prefab, spawn, Quaternion.identity);
        Transform tr = go.transform;

        float ang = Mathf.Deg2Rad * Random.Range(55f, 125f);            // upward fan
        Vector2 vel = new Vector2(Mathf.Cos(ang) * Random.Range(0.7f, 1f), Mathf.Sin(ang)) * Random.Range(5f, 9f);
        float bobPhase = Random.value * 6.28f;
        bool resting = false;
        float restY = groundY;

        while (go != null)   // stop the instant the player collects it (prefab self-destroys)
        {
            float dt = Time.deltaTime;

            if (airborne)
            {
                // No floor below — ease outward, decelerate hard, then hover in mid-air.
                if (!resting)
                {
                    vel *= Mathf.Pow(0.04f, dt);                    // strong drag → glides to a stop
                    vel.y += Physics2D.gravity.y * 0.2f * dt;       // slight sag so it still feels weighty
                    tr.position += (Vector3)(vel * dt);
                    if (vel.magnitude < 0.7f) { resting = true; restY = tr.position.y; }
                }
                else
                {
                    Vector3 p = tr.position;
                    p.y = restY + Mathf.Sin(Time.time * 2f + bobPhase) * 0.12f;   // gentle floating bob
                    tr.position = p;
                }
            }
            else
            {
                // Arc under gravity and settle on the floor with a couple of bounces.
                vel.y += Physics2D.gravity.y * 1.3f * dt;
                Vector3 p = tr.position + (Vector3)(vel * dt);
                if (p.y <= groundY && vel.y < 0f)
                {
                    p.y = groundY;
                    vel.y = -vel.y * 0.4f;      // bounce
                    vel.x *= 0.55f;
                    if (Mathf.Abs(vel.y) < 1.2f) { tr.position = p; yield break; }   // rest on floor, done
                }
                tr.position = p;
            }
            yield return null;
        }
    }

    private SpriteRenderer MakeSprite(string n, Sprite sprite, Color color, Vector3 pos, int order)
    {
        GameObject go = new GameObject(n);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    // --- Procedural sprites (cached + shared). ---

    private static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, outer = c - 1f, band = s * 0.10f, inner = outer - band, feather = 2f;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float ao = Mathf.Clamp01((outer - d) / feather);
                float ai = Mathf.Clamp01((d - inner) / feather);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(ao, ai) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return ringSprite;
    }

    private static Sprite GetBeamSprite()
    {
        if (beamSprite != null) return beamSprite;
        int w = 64, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = (w - 1) * 0.5f;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float hx = Mathf.Clamp01(1f - Mathf.Abs(x - cx) / cx); hx *= hx;
                float vy = 1f - (y / (float)(h - 1));
                px[y * w + x] = new Color32(255, 255, 255, (byte)(hx * vy * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);   // bottom pivot
        return beamSprite;
    }
}
