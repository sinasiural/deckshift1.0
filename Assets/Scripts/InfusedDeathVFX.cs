using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// How a Shift-infused enemy dies (rebuilt 2026-10-04 with the living look in ShiftInfused): its
/// last silhouette flashes white-hot where the body was, splits sideways out of phase, and rises
/// apart pixel by pixel while pixel shock rings and bolts go off. The crystals that were orbiting it
/// fly out and become the real pickups.
///
/// Its own object because EnemyHealth.Die destroys the enemy in the same frame it fires OnDied.
/// Stamped TemporaryObject (and so are the crystals), so a room change sweeps it.
///
/// ⚠️ THE CRYSTALS SETTLE 1 UNIT ABOVE THE FLOOR, NOT WHERE THE BODY WAS. Picking them up must never
/// cost a jump: a jump costs Shift, and paying Shift to collect Shift would undo the point of the
/// drop. 1.0 sits inside the player's capsule (centre 0.84, height 1.68), so walking through
/// collects them. Over a pit (no floor within reach) they hover where they stopped.
///
/// ⚠️ A CRYSTAL STARTS AT ITS ORBITING SHARD ONLY IF THE BODY HAS A CLEAR LINE TO IT. An enemy
/// hugging a wall has half its orbit inside the rock, and a crystal born in rock can never be picked
/// up; those start from the body's centre instead.
///
/// The crystals are the real ShiftCrystal prefab, live pickups from the first frame; this only
/// drives their transforms and stops caring the moment one is collected (it destroys itself).
/// </summary>
public class InfusedDeathVFX : MonoBehaviour
{
    /// <summary>Everything the death needs from the enemy, captured on the frame it died.</summary>
    public class Snapshot
    {
        public Vector3 centre;
        public int crystals;
        public GameObject crystalPrefab;
        public float px = 1f / 32f;
        public int sortingLayer, baseOrder;
        public Vector3 bodyPos;
        public Quaternion bodyRot = Quaternion.identity;
        public Mesh mesh;                 // skinned bodies: the last pose; the VFX owns it from here
        public Texture texture;
        public Sprite sprite;             // sprite bodies
        public bool flipX, flipY;
        public Vector3 spriteScale = Vector3.one;
        public readonly List<Vector3> shardPositions = new List<Vector3>();
    }

    private const float RestAboveFloor = 1.0f;
    private const float FloorSearch = 6f;
    private const float CrystalSpacing = 0.8f;      // a crystal is ~0.47 wide; this keeps clear gaps
    private const float GhostLife = 0.62f;
    private const float SplitLife = 0.36f;
    private const float RingLife = 0.42f;

    private class Drop { public Transform t; public Vector2 vel; public float age; public float restX, restY; public bool hasFloor; public bool settled; }
    private class Mote { public SpriteRenderer sr; public Vector3 pos; public Vector2 vel; public float age, life; public bool bolt; }
    private class Ring { public SpriteRenderer sr; public float delay; public int lastRadius = -1; }

    private readonly List<Drop> drops = new List<Drop>();
    private readonly List<Mote> motes = new List<Mote>();
    private readonly List<Ring> rings = new List<Ring>();
    private Snapshot snap;
    private Renderer ghost;
    private Renderer[] split;
    private MaterialPropertyBlock block;
    private Light2D flash;
    private float age;
    private int groundMask;
    private bool secondVolley;

    public static void Spawn(Snapshot snap)
    {
        var go = new GameObject("InfusedDeathVFX");
        go.AddComponent<TemporaryObject>();
        go.transform.position = new Vector3(snap.centre.x, snap.centre.y, 0f);
        go.AddComponent<InfusedDeathVFX>().Begin(snap);
    }

    private void Begin(Snapshot s)
    {
        snap = s;
        groundMask = LayerMask.GetMask("Ground");
        block = new MaterialPropertyBlock();
        Vector3 centre = s.centre;

        var lgo = new GameObject("Flash");
        lgo.transform.SetParent(transform, false);
        flash = lgo.AddComponent<Light2D>();
        flash.lightType = Light2D.LightType.Point;
        flash.color = Salvage.Shift;
        flash.intensity = 2.8f;
        flash.pointLightInnerRadius = 0.3f;
        flash.pointLightOuterRadius = 4.2f;
        flash.falloffIntensity = 0.6f;

        // The body's last silhouette, and the two copies that split out of it.
        ghost = BodyCopy("Ghost", s.baseOrder + 3);
        split = new[] { BodyCopy("Split", s.baseOrder + 2), BodyCopy("Split", s.baseOrder + 2) };

        for (int i = 0; i < 2; i++)
        {
            var rgo = new GameObject("Ring");
            rgo.transform.SetParent(transform, false);
            var sr = rgo.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = s.sortingLayer;
            sr.sortingOrder = s.baseOrder + 4;
            sr.enabled = false;
            rings.Add(new Ring { sr = sr, delay = i * 0.08f });
        }

        for (int i = 0; i < 20; i++)
        {
            float a = i * Mathf.PI * 2f / 20f + Random.Range(-0.15f, 0.15f);
            AddMote(centre, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 4.4f), Random.Range(0.4f, 0.75f));
        }
        for (int i = 0; i < 3; i++) AddBolt(centre, 0.55f);

        if (s.crystalPrefab == null || s.crystals <= 0) return;

        // Where the crystals come to rest: a hand's height above the floor under the body.
        // Each crystal starts at one of the orbiting shards and pops out along an upward fan (the
        // left-most shard taking the left-most direction), then glides to its own resting spot.
        var starts = new List<Vector3>();
        for (int i = 0; i < s.crystals; i++)
        {
            Vector3 p = i < s.shardPositions.Count ? s.shardPositions[i] : centre;
            if (Physics2D.Linecast(centre, p, groundMask).collider != null) p = centre;
            starts.Add(p);
        }
        starts.Sort((a, b) => a.x.CompareTo(b.x));
        PlanRestSpots(centre, starts.Count, out float[] restX, out float[] restY, out bool[] hasFloor);
        for (int i = 0; i < starts.Count; i++)
        {
            float t = starts.Count == 1 ? 0.5f : (float)i / (starts.Count - 1);
            float ang = Mathf.Deg2Rad * (Mathf.Lerp(140f, 40f, t) + Random.Range(-8f, 8f));
            GameObject c = Instantiate(s.crystalPrefab, new Vector3(starts[i].x, starts[i].y, PlayPlane.Z), Quaternion.identity);
            if (c.GetComponent<TemporaryObject>() == null) c.AddComponent<TemporaryObject>();
            drops.Add(new Drop
            {
                t = c.transform,
                vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(3.2f, 4.6f),
                hasFloor = hasFloor[i],
                restX = hasFloor[i] ? restX[i] : starts[i].x,
                restY = hasFloor[i] ? restY[i] : starts[i].y,
            });
        }
    }

    // Where the crystals come to rest: a row, CrystalSpacing apart, centred under the body, each a
    // hand's height above the same floor the body stood on.
    //
    // ⚠️ IF THE ROW WOULD HANG OVER A DROP OR RUN INTO A WALL, THE WHOLE ROW SLIDES IN until every
    // spot has that floor under it. Killed at a ledge's end, the outer crystal used to land over the
    // pit, where taking it meant stepping off; pulling single crystals back instead stacked two on
    // the ledge's edge, so the player saw two where there were three. A spot that still has no
    // floor (the body died over a pit) hovers where its crystal stops.
    private void PlanRestSpots(Vector3 centre, int n, out float[] xs, out float[] ys, out bool[] floors)
    {
        xs = new float[n]; ys = new float[n]; floors = new bool[n];
        RaycastHit2D under = Physics2D.Raycast(centre, Vector2.down, FloorSearch, groundMask);
        float floorY = under.collider != null ? under.point.y : float.NaN;

        float offset = 0f;
        if (!float.IsNaN(floorY))
            for (int step = 1; step <= 12 && !RowFits(centre, n, offset, floorY); step++)
            {
                if (RowFits(centre, n, step * 0.25f, floorY)) { offset = step * 0.25f; break; }
                if (RowFits(centre, n, -step * 0.25f, floorY)) { offset = -step * 0.25f; break; }
            }

        for (int i = 0; i < n; i++)
        {
            xs[i] = RowX(centre, n, i, offset);
            floors[i] = !float.IsNaN(floorY) && FloorAt(centre, xs[i], floorY);
            ys[i] = floors[i] ? floorY + RestAboveFloor : centre.y;
        }
    }

    private static float RowX(Vector3 centre, int n, int i, float offset) => centre.x + offset + (i - (n - 1) * 0.5f) * CrystalSpacing;

    private bool RowFits(Vector3 centre, int n, float offset, float floorY)
    {
        for (int i = 0; i < n; i++)
            if (!FloorAt(centre, RowX(centre, n, i, offset), floorY)) return false;
        return true;
    }

    // True when the body's own floor runs under x, with no wall between the body and that spot.
    private bool FloorAt(Vector3 centre, float x, float floorY)
    {
        var from = new Vector2(x, centre.y);
        if (Physics2D.Linecast(centre, from, groundMask).collider != null) return false;
        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, FloorSearch, groundMask);
        return hit.collider != null && Mathf.Abs(hit.point.y - floorY) < 0.3f;
    }

    private Renderer BodyCopy(string label, int order)
    {
        Material mat = ShiftInfusionArt.Silhouette;
        if (mat == null) return null;
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(snap.bodyPos, snap.bodyRot);
        Renderer r;
        if (snap.mesh != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = snap.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r = mr;
        }
        else if (snap.sprite != null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sprite = snap.sprite;
            sr.flipX = snap.flipX;
            sr.flipY = snap.flipY;
            go.transform.localScale = snap.spriteScale;
            r = sr;
        }
        else { Destroy(go); return null; }
        r.sortingLayerID = snap.sortingLayer;
        r.sortingOrder = order;
        return r;
    }

    private void Paint(Renderer r, Color col, float dissolve)
    {
        block.Clear();
        block.SetColor("_Color", col);
        block.SetFloat("_BandAlpha", 0f);
        block.SetFloat("_BandMix", 0f);
        block.SetVector("_Origin", snap.bodyPos);
        block.SetFloat("_PixelSize", snap.px);
        block.SetFloat("_Dissolve", dissolve);
        Texture tex = snap.mesh != null ? snap.texture : (snap.sprite != null ? snap.sprite.texture : null);
        if (tex != null) block.SetTexture("_MainTex", tex);
        r.SetPropertyBlock(block);
    }

    private void AddMote(Vector3 at, Vector2 vel, float life)
    {
        var go = new GameObject("burst");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ShiftInfusionArt.Mote;
        if (ShiftInfusionArt.AtlasMaterial != null) sr.sharedMaterial = ShiftInfusionArt.AtlasMaterial;
        sr.sortingLayerID = snap.sortingLayer;
        sr.sortingOrder = snap.baseOrder + 5;
        Vector3 p = new Vector3(at.x, at.y, PlayPlane.Z - 0.05f);
        go.transform.position = p;
        motes.Add(new Mote { sr = sr, pos = p, vel = vel, life = life });
    }

    private void AddBolt(Vector3 at, float spread)
    {
        var go = new GameObject("bolt");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ShiftInfusionArt.Arc(Random.Range(0, ShiftInfusionArt.ArcCount));
        if (ShiftInfusionArt.AtlasMaterial != null) sr.sharedMaterial = ShiftInfusionArt.AtlasMaterial;
        sr.flipX = Random.value < 0.5f;
        sr.flipY = Random.value < 0.5f;
        sr.sortingLayerID = snap.sortingLayer;
        sr.sortingOrder = snap.baseOrder + 5;
        Vector3 p = new Vector3(at.x + Random.Range(-spread, spread), at.y + Random.Range(-spread, spread), PlayPlane.Z - 0.06f);
        go.transform.position = new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, p.z);
        motes.Add(new Mote { sr = sr, pos = go.transform.position, life = Random.Range(0.09f, 0.14f), bolt = true });
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (flash != null) { float f = Mathf.Max(0f, 1f - age / 0.45f); flash.intensity = 2.8f * f * f; }

        if (!secondVolley && age >= 0.1f)
        {
            secondVolley = true;
            for (int i = 0; i < 2; i++) AddBolt(snap.centre, 0.9f);
        }

        // The ghost: white-hot for a beat, then Shift, rising and stretching as it comes apart.
        if (ghost != null)
        {
            float k = age / GhostLife;
            if (k >= 1f) { Destroy(ghost.gameObject); ghost = null; }
            else
            {
                float ease = 1f - (1f - k) * (1f - k);
                ghost.transform.position = snap.bodyPos + new Vector3(0f, 0.55f * ease, 0f);
                Vector3 baseScale = snap.mesh != null ? Vector3.one : snap.spriteScale;
                ghost.transform.localScale = Vector3.Scale(baseScale, new Vector3(1f, 1f + 0.3f * ease, 1f));
                Color col = age < 0.07f ? Color.white : Color.Lerp(ShiftInfusionArt.Hot, Salvage.Shift, Mathf.Clamp01((age - 0.07f) / 0.2f));
                Paint(ghost, col, Mathf.Clamp01((age - 0.12f) / (GhostLife - 0.12f)));
            }
        }

        // The split: two copies thrown sideways out of phase, thinning as they go.
        if (split != null)
        {
            float k = age / SplitLife;
            if (k >= 1f) { foreach (Renderer r in split) if (r != null) Destroy(r.gameObject); split = null; }
            else
            {
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                for (int i = 0; i < split.Length; i++)
                {
                    if (split[i] == null) continue;
                    float side = i == 0 ? 1f : -1f;
                    float d = Mathf.Round(0.75f * ease / snap.px) * snap.px;
                    split[i].transform.position = snap.bodyPos + new Vector3(side * d, side * snap.px, 0f);
                    Color col = Salvage.Shift; col.a = 0.65f * (1f - k);
                    Paint(split[i], col, k * 0.7f);
                }
            }
        }

        // The shock rings: whole-pixel rings, redrawn at each new radius rather than scaled, so the
        // line stays one pixel of the world thick.
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            Ring ring = rings[i];
            float k = (age - ring.delay) / RingLife;
            if (k < 0f) continue;
            if (k >= 1f) { Destroy(ring.sr.gameObject); rings.RemoveAt(i); continue; }
            int radius = Mathf.RoundToInt(Mathf.Lerp(4f, 70f, 1f - (1f - k) * (1f - k)));
            if (radius != ring.lastRadius)
            {
                ring.sr.sprite = ShiftInfusionArt.Ring(radius, out Material m);
                if (m != null) ring.sr.sharedMaterial = m;
                ring.lastRadius = radius;
            }
            ring.sr.enabled = true;
            ring.sr.transform.position = new Vector3(Mathf.Round(snap.centre.x * 32f) / 32f, Mathf.Round(snap.centre.y * 32f) / 32f, PlayPlane.Z - 0.06f);
            Color rc = Color.Lerp(ShiftInfusionArt.Hot, Salvage.Shift, k); rc.a = 1f - k;
            ring.sr.color = rc;
        }

        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.age += dt;
            if (m.age >= m.life || m.sr == null) { if (m.sr != null) Destroy(m.sr.gameObject); motes.RemoveAt(i); continue; }
            if (m.bolt) { Color bc = Color.white; bc.a = m.age < m.life * 0.5f ? 1f : 0.45f; m.sr.color = bc; continue; }
            m.vel *= Mathf.Pow(0.05f, dt);
            m.pos += (Vector3)(m.vel * dt);
            m.sr.transform.position = m.pos;
            Color c = Color.Lerp(ShiftInfusionArt.Hot, Salvage.Shift, m.age / m.life); c.a = 1f - m.age / m.life;
            m.sr.color = c;
        }

        bool anyMoving = false;
        foreach (Drop d in drops)
        {
            if (d.settled || d.t == null) continue;      // collected (destroyed) or at rest
            anyMoving = true;
            d.age += dt;
            Vector3 p = d.t.position;

            // The pop: out along the fan, dragged to a stop.
            d.vel *= Mathf.Pow(0.02f, dt);
            Vector3 next = p + (Vector3)(d.vel * dt);
            if (Physics2D.Linecast(p, next, groundMask).collider != null) { d.vel = Vector2.zero; next = p; }

            // Then glide to its own resting spot (never into the floor or through a wall).
            if (d.hasFloor && d.age > 0.2f)
            {
                float k = 1f - Mathf.Pow(0.01f, dt);
                Vector3 to = new Vector3(Mathf.Lerp(next.x, d.restX, k), Mathf.Lerp(next.y, d.restY, k), next.z);
                if (Physics2D.Linecast(next, to, groundMask).collider == null) next = to;
            }
            d.t.position = next;

            bool slow = d.vel.sqrMagnitude < 0.05f;
            bool home = !d.hasFloor || (Mathf.Abs(next.x - d.restX) < 0.02f && Mathf.Abs(next.y - d.restY) < 0.02f);
            if ((slow && home) || d.age > 2.5f) d.settled = true;
        }

        if (!anyMoving && motes.Count == 0 && rings.Count == 0 && ghost == null && split == null && age > 0.5f)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // The last pose was baked for this effect alone.
        if (snap != null && snap.mesh != null) Destroy(snap.mesh);
    }
}
