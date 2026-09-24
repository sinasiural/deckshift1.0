using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// "Movement is a Resource" made physical: an altar that eats Shift.
// Idle: cyan glow aura pulses behind the altar, the price tag bobs.
// Pay (E): motes of Shift are RIPPED out of the air and absorbed into the
// altar, a ring shockwave pops, and a glowing signal orb flies from the altar
// to its gate — the gate opens the moment the orb arrives, so the player SEES
// what they bought. Refusal: the tag shakes red and sheds sparks.
// Free in the hub (umbrella rule). All VFX procedural (house style).
public class ShiftAltar : MonoBehaviour, IInteractable
{
    [SerializeField] private int shiftCost = 3;
    [Tooltip("Fired when the price is paid (after the signal orb arrives). Wired to Gate.Open by the importer.")]
    public UnityEvent OnPaid = new UnityEvent();
    [Tooltip("Where the signal orb flies (the gate). Set by the importer; optional.")]
    public Transform signalTarget;
    [SerializeField] private AudioClip paySound;
    [SerializeField] private AudioClip refuseSound;
    [SerializeField] private float labelHeight = 2.0f;

    private static readonly Color ShiftCyan = new Color(0.45f, 0.9f, 1f, 1f);
    private static readonly Color RefuseRed = new Color(1f, 0.35f, 0.3f, 1f);

    private bool used;
    private TextMeshPro label;
    private SpriteRenderer altarSprite;
    private SpriteRenderer aura;
    private Coroutine flasher;
    private float idleT;

    // ---- procedural motes (house style: generated sprites, Update-driven) ----
    private class Mote
    {
        public SpriteRenderer sr;
        public Vector2 vel;
        public Vector3? homeTo;      // if set, steer toward this point instead of drifting
        public float life, maxLife, size;
        public Color color;
    }
    private readonly List<Mote> motes = new List<Mote>();
    private static Sprite dotSprite, ringSprite;

    void Start()
    {
        altarSprite = GetComponent<SpriteRenderer>();
        BuildAura();
        BuildLabel();
    }

    void Update()
    {
        idleT += Time.deltaTime;

        // idle breathing: aura pulse + label bob (calms down after payment)
        if (aura != null)
        {
            float pulse = used ? 0.06f : 0.22f + 0.10f * Mathf.Sin(idleT * 2.2f);
            aura.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, pulse);
            aura.transform.localScale = Vector3.one * (2.1f + 0.12f * Mathf.Sin(idleT * 2.2f));
        }
        if (label != null && !used && flasher == null)
            label.transform.localPosition = new Vector3(0f, labelHeight + 0.06f * Mathf.Sin(idleT * 2.6f), 0f);

        // animate motes
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
            if (m.homeTo.HasValue)
            {
                // absorbed: accelerate hard toward the altar's heart
                Vector3 dir = (m.homeTo.Value - m.sr.transform.position);
                m.vel += (Vector2)(dir.normalized * 26f * Time.deltaTime);
                m.vel *= 1f - 1.5f * Time.deltaTime;
            }
            else
            {
                m.vel += Vector2.down * 2.5f * Time.deltaTime;
                m.vel *= 1f - 1.8f * Time.deltaTime;
            }
            m.sr.transform.position += (Vector3)(m.vel * Time.deltaTime);
            float fade = 1f - t * t;
            m.sr.color = new Color(m.color.r, m.color.g, m.color.b, m.color.a * fade);
            m.sr.transform.localScale = Vector3.one * m.size * (1f - 0.4f * t);
        }
    }

    public void Interact()
    {
        if (used) return;

        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) return;

        bool free = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomSandbox();

        if (!free && player.GetCurrentShift() < shiftCost)
        {
            if (flasher != null) StopCoroutine(flasher);
            flasher = StartCoroutine(FlashRefusal());
            if (refuseSound != null) SfxManager.PlayAtPoint(refuseSound, transform.position);
            SpawnBurst(transform.position + new Vector3(0f, labelHeight, 0f), 4, RefuseRed, 1.6f, null);
            return;
        }

        if (!free) player.SpendShift(shiftCost);
        used = true;
        if (paySound != null) SfxManager.PlayAtPoint(paySound, transform.position);
        StartCoroutine(PaySequence());
    }

    public string GetInteractText()
    {
        return used ? "" : $"Pay {shiftCost} Shift";
    }

    // absorption -> flash + ring + shake -> signal orb flies to the gate -> OnPaid
    private IEnumerator PaySequence()
    {
        Vector3 heart = transform.position + Vector3.up * 0.8f;

        // 1. shift motes ripped out of the air, converging into the altar
        for (int i = 0; i < 12; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(1.1f, 1.8f);
            Vector3 pos = heart + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.8f, 0f);
            SpawnMote(pos, Vector2.zero, ShiftCyan, Random.Range(0.14f, 0.24f), Random.Range(0.3f, 0.45f), heart);
        }
        if (label != null) label.text = "PAID";
        yield return new WaitForSeconds(0.32f);

        // 2. the altar drinks: white flash + expanding ring + thump
        if (altarSprite != null) StartCoroutine(FlashSprite());
        StartCoroutine(RingBurst(heart));
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.15f, 0.14f);
        if (label != null) StartCoroutine(FadeLabelOut());

        // 3. signal orb carries the payment to the gate; it opens on arrival
        if (signalTarget != null)
        {
            yield return SignalOrb(heart, signalTarget.position);
        }
        OnPaid.Invoke();
    }

    private IEnumerator SignalOrb(Vector3 from, Vector3 to)
    {
        var go = new GameObject("ShiftSignal");
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.color = ShiftCyan;
        sr.sortingOrder = 35;
        go.transform.localScale = Vector3.one * 0.45f;

        float dist = Vector3.Distance(from, to);
        float dur = Mathf.Max(0.28f, dist / 14f);
        float trailTimer = 0f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = k * k * (3f - 2f * k); // smoothstep
            Vector3 pos = Vector3.Lerp(from, to, k);
            pos.y += Mathf.Sin(k * Mathf.PI) * Mathf.Min(1.2f, dist * 0.12f); // gentle arc
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (0.45f + 0.08f * Mathf.Sin(t * 30f)); // sizzle

            trailTimer += Time.deltaTime;
            if (trailTimer > 0.035f)
            {
                trailTimer = 0f;
                SpawnMote(pos, Vector2.zero, ShiftCyan, Random.Range(0.10f, 0.16f), 0.3f, null);
            }
            yield return null;
        }
        SpawnBurst(to, 7, ShiftCyan, 2.4f, null);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.08f, 0.07f);
        Destroy(go);
    }

    private IEnumerator FlashSprite()
    {
        Color orig = altarSprite.color;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.35f;
            altarSprite.color = Color.Lerp(orig, new Color(1.6f * ShiftCyan.r, 1.6f * ShiftCyan.g, 1.6f * ShiftCyan.b, 1f), k * 0.9f);
            yield return null;
        }
        altarSprite.color = orig;
    }

    private IEnumerator RingBurst(Vector3 at)
    {
        var go = new GameObject("AltarRing");
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        sr.sortingOrder = 30;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            float ease = 1f - (1f - k) * (1f - k);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3.2f, ease);
            sr.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.85f * (1f - k));
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator FlashRefusal()
    {
        if (label == null) yield break;
        label.color = RefuseRed;
        Vector3 basePos = new Vector3(0f, labelHeight, 0f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float x = Mathf.Sin(t * 60f) * 0.07f * (1f - t / 0.35f);
            label.transform.localPosition = basePos + new Vector3(x, 0f, 0f);
            yield return null;
        }
        label.transform.localPosition = basePos;
        label.color = ShiftCyan;
        flasher = null;
    }

    private IEnumerator FadeLabelOut()
    {
        yield return new WaitForSeconds(0.8f);
        float dur = 0.6f;
        Color start = label.color;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Color c = start;
            c.a = Mathf.Lerp(start.a, 0f, t / dur);
            label.color = c;
            label.transform.localPosition = new Vector3(0f, labelHeight + (t / dur) * 0.5f, 0f);
            yield return null;
        }
        label.gameObject.SetActive(false);
    }

    // ---- construction ----

    private void BuildAura()
    {
        var go = new GameObject("Aura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        aura = go.AddComponent<SpriteRenderer>();
        aura.sprite = GetDotSprite();
        aura.sortingOrder = 1; // right behind the altar sprite (2)
        aura.color = new Color(ShiftCyan.r, ShiftCyan.g, ShiftCyan.b, 0.25f);
        go.transform.localScale = Vector3.one * 2.1f;
    }

    private void BuildLabel()
    {
        var go = new GameObject("CostLabel");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, labelHeight, 0f);

        label = go.AddComponent<TextMeshPro>();
        label.text = $"{shiftCost} SHIFT";
        label.fontSize = 3f;
        label.color = ShiftCyan;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(4f, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 60;
    }

    private void SpawnBurst(Vector3 at, int count, Color color, float speed, Vector3? homeTo)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(speed * 0.5f, speed);
            SpawnMote(at, vel, color, Random.Range(0.12f, 0.2f), Random.Range(0.3f, 0.5f), homeTo);
        }
    }

    private void SpawnMote(Vector3 pos, Vector2 vel, Color color, float size, float life, Vector3? homeTo)
    {
        var go = new GameObject("AltarMote");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDotSprite();
        sr.color = color;
        sr.sortingOrder = 32;
        go.transform.localScale = Vector3.one * size;
        motes.Add(new Mote { sr = sr, vel = vel, homeTo = homeTo, life = 0f, maxLife = life, size = size, color = color });
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
