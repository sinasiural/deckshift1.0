using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The Well — the recharge room that fixes BEING WORN DOWN. Walk up, press E, once per visit:
// heals a fixed amount and pays a fixed amount of Shift. It does nothing else on purpose; a room
// that fixes every problem is never a decision (see the recharge-room design in the levels skill).
//
// Both numbers are Inspector-tunable and deliberately NOT a full reset: a Well is roughly one
// combat room's worth of Shift supply (hand-made rooms average ~7) and a Stagger or two of health.
//
// ---- The drink is an EVENT, staged (rebuilt 2026-09-14 after "bland and not very immersive") ----
// The first version changed a label and floated sixteen dots. Nothing HAPPENED. This one speaks
// the game's own language — the altar rips motes from the air, the gate's seal shatters — so:
//
//   DRAW    the well holds its breath: the light dims, the water's glow contracts, loose motes are
//           sucked back IN. (Inversion first, so the surge has somewhere to come from.)
//   SURGE   the water erupts: a column of cyan light, ripple rings racing across the surface, the
//           light flares to fill the room, a small shake.
//   RECEIVE a fountain of motes arcs into the player. The payout lands in TICKS as they arrive, so
//           the player watches HP and Shift FILL rather than jump; an aura blooms behind them.
//   SETTLE  the light sinks to a spent ember, the ripples slow, the label rises and fades. A used
//           well is visibly used.
//
// ⚠️ THE LIGHT IS A REAL Light2D, and it is the biggest single lever here. Verified 2026-09-14: a
// point Light2D lights the well sprite, the floor tiles and the back wall — but NOT the player's
// Cainos rig (its shaders ignore 2D lights). So the world reacts through the light and the player
// is reached with sprites (motes, rings, the aura). Don't expect the character to catch the glow.
//
// Sound: two procedural clips (ProcSfx.WellDraw / WellSurge) so it is never silent; `drinkSound`
// is an OVERRIDE for a real file, not a requirement — an empty slot must not mute the event.
//
// Placement: Interactable layer (12) with a trigger Collider2D, like ShiftAltar / ScrapForge.
// It GIVES rather than spends, so the umbrella rule never gates it. Once-per-visit falls out of
// the room being destroyed on exit. VFX are procedural in the house style: generated sprites,
// Update-driven, TIME-ACCUMULATED spawning — never a per-frame probability.
public class RestWell : MonoBehaviour, IInteractable
{
    [Header("Payout (once per visit)")]
    [SerializeField] private float healAmount = 40f;
    [SerializeField] private int shiftAmount = 8;

    [Header("Wiring")]
    [Tooltip("Optional 'press E' hint (an InteractPrompt prefab instance), shown while in range.")]
    public GameObject prompt;
    [Tooltip("Where the water is — the well's mouth, in local space.")]
    [SerializeField] private Vector3 mouthOffset = new Vector3(0f, 1.0f, 0f);
    [SerializeField] private float labelHeight = 4.1f;

    [Header("Light")]
    [SerializeField] private float idleLight = 1.1f;
    [SerializeField] private float surgeLight = 4.5f;
    [SerializeField] private float spentLight = 0.35f;
    [SerializeField] private float lightRadius = 5.5f;

    [Header("Audio (overrides — procedural clips play when empty)")]
    [SerializeField] private AudioClip drinkSound;
    [SerializeField, Range(0f, 2f)] private float drinkVolume = 1f;

    private static readonly Color ShiftCyan = new Color(0.45f, 0.9f, 1f, 1f);
    private const float NearRadius = 5f;      // the water notices you from here
    private const int PayoutTicks = 8;

    private bool used, drinking;
    private TextMeshPro label;
    private SpriteRenderer glow;
    private Light2D light2D;
    private AudioSource sfx;
    private float idleT, trickleAccum, rippleAccum;
    private float lightTarget, lightNow, glowScale = 1f;
    private Transform playerT;

    private class Mote
    {
        public SpriteRenderer sr;
        public Vector2 vel;
        public Transform homeTo;      // steer toward this transform (the player) once homeDelay elapses
        public Vector3? homePoint;    // or toward a fixed point (the mouth, during DRAW)
        public float homeDelay;
        public float life, maxLife, size;
    }
    private class Ripple { public SpriteRenderer sr; public float life, maxLife, w; }

    private readonly List<Mote> motes = new List<Mote>();
    private readonly List<Ripple> ripples = new List<Ripple>();
    private static Sprite dotSprite, ringSprite;

    private Vector3 Mouth => transform.position + mouthOffset;

    void Start()
    {
        BuildLight();
        BuildGlow();
        BuildLabel();
        BuildAudio();
        lightNow = lightTarget = idleLight;
    }

    void Update()
    {
        idleT += Time.deltaTime;
        if (playerT == null && GameManager.instance != null && GameManager.instance.player != null)
            playerT = GameManager.instance.player.transform;

        // ---- light: breathes, brightens as you approach, follows lightTarget during the drink ----
        if (light2D != null)
        {
            float target = lightTarget;
            if (!used && !drinking)
            {
                float near = 0f;
                if (playerT != null)
                    near = 1f - Mathf.Clamp01((Vector2.Distance(playerT.position, Mouth) - 1.5f) / NearRadius);
                target = idleLight * (1f + 0.12f * Mathf.Sin(idleT * 1.8f)) + 0.6f * near;
            }
            lightNow = Mathf.Lerp(lightNow, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            light2D.intensity = lightNow;
            light2D.pointLightOuterRadius = lightRadius * (0.8f + 0.25f * Mathf.Clamp01(lightNow / surgeLight) * 3f);
        }

        // ---- the water's own glow ----
        if (glow != null)
        {
            float a = used ? 0.10f : 0.34f + 0.10f * Mathf.Sin(idleT * 1.8f);
            if (drinking) a = 0.34f;
            glow.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, a);
            glow.transform.localScale = new Vector3(2.2f, 1.3f, 1f) * glowScale * (1f + 0.06f * Mathf.Sin(idleT * 1.8f));
        }
        if (label != null && !used)
            label.transform.localPosition = new Vector3(0f, labelHeight + 0.06f * Mathf.Sin(idleT * 2.4f), 0f);

        // ---- idle trickle + surface ripples (accumulators, never per-frame probability) ----
        if (!used && !drinking)
        {
            trickleAccum += Time.deltaTime * 2.2f;
            while (trickleAccum >= 1f)
            {
                trickleAccum -= 1f;
                Vector3 p = Mouth + new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(-0.05f, 0.1f), 0f);
                SpawnMote(p, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 0.9f)),
                          Random.Range(0.14f, 0.22f), Random.Range(0.9f, 1.4f));
            }
        }
        rippleAccum += Time.deltaTime * (used ? 0.25f : 0.6f);
        while (rippleAccum >= 1f) { rippleAccum -= 1f; SpawnRipple(0.35f, 1.6f, 1.3f); }

        TickMotes();
        TickRipples();
    }

    // ---------------------------------------------------------------------------------------------

    public void Interact()
    {
        if (used) return;

        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) return;

        used = true;
        if (prompt != null) prompt.SetActive(false);
        StartCoroutine(DrinkSequence(player));
    }

    public string GetInteractText()
    {
        return used ? "" : $"Drink (+{healAmount:0} HP, +{shiftAmount} Shift)";
    }

    private IEnumerator DrinkSequence(PlayerController player)
    {
        drinking = true;
        Transform target = player.transform;

        // ---- DRAW: the well holds its breath ----
        Play(ProcSfx.WellDraw, 0.9f);
        lightTarget = 0.25f;
        foreach (Mote m in motes) { m.homePoint = Mouth; m.homeTo = null; m.homeDelay = 0f; }
        for (int i = 0; i < 12; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(1.4f, 2.4f);
            Vector3 pos = Mouth + new Vector3(Mathf.Cos(ang) * r, Mathf.Abs(Mathf.Sin(ang)) * r * 0.7f, 0f);
            Mote m = SpawnMote(pos, Vector2.zero, Random.Range(0.14f, 0.22f), 0.6f);
            m.homePoint = Mouth;
        }
        for (float t = 0f; t < 0.38f; t += Time.deltaTime)
        {
            glowScale = Mathf.Lerp(1f, 0.55f, t / 0.38f);
            yield return null;
        }

        // ---- SURGE: the water erupts ----
        if (drinkSound != null) Play(drinkSound, drinkVolume); else Play(ProcSfx.WellSurge, 1f);
        lightTarget = surgeLight;
        glowScale = 1.35f;
        StartCoroutine(Column());
        StartCoroutine(RippleBurst());
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.10f, 0.18f);
        for (int i = 0; i < 22; i++)
        {
            Vector3 p = Mouth + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
            Vector2 v = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(3.5f, 6.5f));
            Mote m = SpawnMote(p, v, Random.Range(0.22f, 0.36f), 1.6f);
            m.homeTo = target;
            m.homeDelay = Random.Range(0.12f, 0.30f);   // rises first, then bends toward the player
        }
        yield return new WaitForSeconds(0.42f);

        // ---- RECEIVE: the payout lands in ticks as the water reaches the player ----
        StartCoroutine(Aura(target));
        lightTarget = 1.8f;
        int shiftGiven = 0;
        for (int i = 1; i <= PayoutTicks; i++)
        {
            player.Heal(healAmount / PayoutTicks);
            int shiftDue = Mathf.RoundToInt(shiftAmount * (float)i / PayoutTicks) - shiftGiven;
            if (shiftDue > 0) { player.AddShift(shiftDue); shiftGiven += shiftDue; }
            StartCoroutine(Ring(target.position + Vector3.up * 0.9f, 0.9f + 0.05f * i, 0.32f, 0.55f));
            yield return new WaitForSeconds(0.085f);
        }
        Debug.Log($"[RestWell] drank: +{healAmount:0} HP, +{shiftGiven} Shift.");

        // ---- SETTLE ----
        if (label != null) { label.text = "RESTED"; StartCoroutine(FadeLabelOut()); }
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            lightTarget = Mathf.Lerp(1.8f, spentLight, t / 0.8f);
            glowScale = Mathf.Lerp(1.35f, 0.85f, t / 0.8f);
            yield return null;
        }
        lightTarget = spentLight;
        drinking = false;
    }

    // A column of light standing up out of the water for a moment, then gone.
    private IEnumerator Column()
    {
        var go = new GameObject("WellColumn");
        go.transform.position = Mouth;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.sortingOrder = 31;
        const float dur = 0.5f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            float rise = 1f - (1f - k) * (1f - k);               // shoots up fast, hangs
            float h = Mathf.Lerp(0.5f, 6.5f, rise);
            go.transform.localScale = new Vector3(1.6f * (1f - 0.35f * k), h, 1f);
            go.transform.position = Mouth + Vector3.up * (h * 0.5f - 0.3f);
            sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.55f * (1f - k * k));
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator RippleBurst()
    {
        for (int i = 0; i < 4; i++)
        {
            SpawnRipple(0.3f, 3.2f + i * 0.4f, 0.55f);
            yield return new WaitForSeconds(0.06f);
        }
    }

    // A soft cyan bloom BEHIND the character. Opaque rig pixels occlude it, so it reads as light
    // around them rather than a slab over them.
    private IEnumerator Aura(Transform target)
    {
        var go = new GameObject("WellAura");
        go.transform.SetParent(target, false);
        go.transform.localPosition = new Vector3(0f, 0.9f, 0.15f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.sortingOrder = 29;
        const float dur = 1.5f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            float env = Mathf.Sin(k * Mathf.PI);
            go.transform.localScale = new Vector3(2.6f, 3.4f, 1f) * (0.8f + 0.3f * env);
            sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.38f * env);
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator Ring(Vector3 at, float toScale, float dur, float alpha)
    {
        var go = new GameObject("WellRing");
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        sr.sortingOrder = 30;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            float ease = 1f - (1f - k) * (1f - k);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, toScale, ease);
            sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, alpha * (1f - k));
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator FadeLabelOut()
    {
        yield return new WaitForSeconds(0.6f);
        float dur = 0.7f;
        Color start = label.color;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Color c = start;
            c.a = Mathf.Lerp(start.a, 0f, t / dur);
            label.color = c;
            label.transform.localPosition = new Vector3(0f, labelHeight + (t / dur) * 0.6f, 0f);
            yield return null;
        }
        label.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null && !used) prompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && prompt != null) prompt.SetActive(false);
    }

    // ---- particles ----

    private void TickMotes()
    {
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

            Vector3? goal = null;
            if (m.homeTo != null)
            {
                m.homeDelay -= Time.deltaTime;
                if (m.homeDelay <= 0f) goal = m.homeTo.position + Vector3.up * 0.9f;
            }
            else if (m.homePoint.HasValue) goal = m.homePoint.Value;

            if (goal.HasValue)
            {
                Vector3 dir = goal.Value - m.sr.transform.position;
                if (dir.sqrMagnitude < 0.12f)                // arrived — absorbed, gone
                {
                    Destroy(m.sr.gameObject);
                    motes.RemoveAt(i);
                    continue;
                }
                m.vel += (Vector2)(dir.normalized * 34f * Time.deltaTime);
                m.vel *= 1f - 1.6f * Time.deltaTime;
            }
            else
            {
                m.vel *= 1f - 0.6f * Time.deltaTime;         // rising motes just slow and fade
            }
            m.sr.transform.position += (Vector3)(m.vel * Time.deltaTime);
            float fade = goal.HasValue ? 1f : 1f - t * t;
            m.sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.9f * fade);
            m.sr.transform.localScale = Vector3.one * m.size * (goal.HasValue ? 1f : 1f - 0.3f * t);
        }
    }

    private void TickRipples()
    {
        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Ripple r = ripples[i];
            r.life += Time.deltaTime;
            float k = r.life / r.maxLife;
            if (k >= 1f || r.sr == null)
            {
                if (r.sr != null) Destroy(r.sr.gameObject);
                ripples.RemoveAt(i);
                continue;
            }
            float ease = 1f - (1f - k) * (1f - k);
            float w = Mathf.Lerp(0.3f, r.w, ease);
            // Flattened: a ring lying on the water's surface, seen from the side.
            r.sr.transform.localScale = new Vector3(w, w * 0.28f, 1f);
            r.sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.55f * (1f - k));
        }
    }

    private Mote SpawnMote(Vector3 pos, Vector2 vel, float size, float life)
    {
        var go = new GameObject("WellMote");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.color = ShiftCyan;
        sr.sortingOrder = 32;
        go.transform.localScale = Vector3.one * size;
        var m = new Mote { sr = sr, vel = vel, life = 0f, maxLife = life, size = size };
        motes.Add(m);
        return m;
    }

    private void SpawnRipple(float alpha, float width, float life)
    {
        var go = new GameObject("WellRipple");
        go.transform.position = Mouth + Vector3.up * 0.05f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        sr.sortingOrder = 4;
        sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, alpha);
        ripples.Add(new Ripple { sr = sr, life = 0f, maxLife = life, w = width });
    }

    private void OnDestroy()
    {
        // Motes and ripples are scene-root objects; the room's destruction would leave them behind.
        foreach (Mote m in motes) if (m.sr != null) Destroy(m.sr.gameObject);
        foreach (Ripple r in ripples) if (r.sr != null) Destroy(r.sr.gameObject);
        motes.Clear();
        ripples.Clear();
    }

    // ---- construction ----

    private void BuildLight()
    {
        var go = new GameObject("WellLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = mouthOffset;
        light2D = go.AddComponent<Light2D>();
        light2D.lightType = Light2D.LightType.Point;
        light2D.color = ShiftCyan;
        light2D.intensity = idleLight;
        light2D.pointLightInnerRadius = 0.4f;
        light2D.pointLightOuterRadius = lightRadius;
        light2D.falloffIntensity = 0.6f;
    }

    private void BuildGlow()
    {
        var go = new GameObject("WaterGlow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = mouthOffset;
        glow = go.AddComponent<SpriteRenderer>();
        glow.sprite = GetDotSprite();
        glow.sortingOrder = 3;   // over the well's rim so the water itself reads as lit
        glow.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.34f);
        go.transform.localScale = new Vector3(2.2f, 1.3f, 1f);
    }

    private void BuildLabel()
    {
        var go = new GameObject("RestLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, labelHeight, 0f);

        label = go.AddComponent<TextMeshPro>();
        label.text = "REST";
        label.fontSize = 3f;
        label.color = ShiftCyan;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(4f, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 60;
    }

    private void BuildAudio()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;   // 2D: the event must be heard regardless of where the camera sits
    }

    private void Play(AudioClip clip, float vol)
    {
        if (clip == null || sfx == null) return;
        SfxManager.PlayOn(sfx, clip, vol);
    }

    // ---- procedural sprites (cached + shared, house pattern) ----

    private static Sprite GetDotSprite()
    {
        if (dotSprite != null) return dotSprite;
        int s = 96;
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

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, outer = c - 1f, band = s * 0.09f, inner = outer - band, feather = 2f;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float ao = Mathf.Clamp01((outer - d) / feather);
                float ai = Mathf.Clamp01((d - inner) / feather);
                float a = Mathf.Min(ao, ai);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return ringSprite;
    }
}
