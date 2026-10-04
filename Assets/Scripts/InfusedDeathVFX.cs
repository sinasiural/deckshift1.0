using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// What a Shift-infused enemy leaves behind: a flash of Shift light, a burst of motes, and its Shift
/// crystals, which pop out of the body and settle to hover at chest height above the floor.
///
/// Its own object because EnemyHealth.Die destroys the enemy in the same frame it fires OnDied.
/// Stamped TemporaryObject (and so are the crystals), so a room change sweeps it.
///
/// ⚠️ THE CRYSTALS SETTLE 1 UNIT ABOVE THE FLOOR, NOT WHERE THE BODY WAS. Picking them up must never
/// cost a jump: a jump costs Shift, and paying Shift to collect Shift would undo the point of the
/// drop. 1.0 sits inside the player's capsule (centre 0.84, height 1.68), so walking through
/// collects them. Over a pit (no floor within reach) they hover where they stopped.
///
/// The crystals are the real ShiftCrystal prefab, live pickups from the first frame; this only
/// drives their transforms and stops caring the moment one is collected (it destroys itself).
/// </summary>
public class InfusedDeathVFX : MonoBehaviour
{
    private const float RestAboveFloor = 1.0f;
    private const float FloorSearch = 6f;

    private class Drop { public Transform t; public Vector2 vel; public float age; public float restY; public bool hasFloor; public bool settled; }
    private class Mote { public SpriteRenderer sr; public Vector3 pos; public Vector2 vel; public float age, life; }

    private readonly List<Drop> drops = new List<Drop>();
    private readonly List<Mote> motes = new List<Mote>();
    private Light2D flash;
    private float age;
    private int groundMask;

    public static void Spawn(Vector3 centre, int crystals, GameObject crystalPrefab)
    {
        var go = new GameObject("InfusedDeathVFX");
        go.AddComponent<TemporaryObject>();
        go.transform.position = new Vector3(centre.x, centre.y, 0f);
        go.AddComponent<InfusedDeathVFX>().Begin(centre, crystals, crystalPrefab);
    }

    private void Begin(Vector3 centre, int crystals, GameObject crystalPrefab)
    {
        groundMask = LayerMask.GetMask("Ground");

        var lgo = new GameObject("Flash");
        lgo.transform.SetParent(transform, false);
        flash = lgo.AddComponent<Light2D>();
        flash.lightType = Light2D.LightType.Point;
        flash.color = Salvage.Shift;
        flash.intensity = 2.2f;
        flash.pointLightInnerRadius = 0.3f;
        flash.pointLightOuterRadius = 3.6f;
        flash.falloffIntensity = 0.6f;

        for (int i = 0; i < 18; i++)
        {
            float a = i * Mathf.PI * 2f / 18f + Random.Range(-0.15f, 0.15f);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 4.2f);
            var go = new GameObject("burst");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShiftInfused.MoteSprite;
            if (ShiftInfused.MoteMaterial != null) sr.sharedMaterial = ShiftInfused.MoteMaterial;
            sr.sortingOrder = 61;
            Vector3 p = new Vector3(centre.x, centre.y, PlayPlane.Z - 0.05f);
            go.transform.position = p;
            motes.Add(new Mote { sr = sr, pos = p, vel = v, life = Random.Range(0.4f, 0.7f) });
        }

        if (crystalPrefab == null || crystals <= 0) return;

        // Where the crystals come to rest: a hand's height above the floor under the body.
        RaycastHit2D floor = Physics2D.Raycast(centre, Vector2.down, FloorSearch, groundMask);
        for (int i = 0; i < crystals; i++)
        {
            // An upward fan, spread evenly so three crystals never stack on one spot.
            float t = crystals == 1 ? 0.5f : (float)i / (crystals - 1);
            float ang = Mathf.Deg2Rad * (Mathf.Lerp(40f, 140f, t) + Random.Range(-8f, 8f));
            GameObject c = Instantiate(crystalPrefab, new Vector3(centre.x, centre.y, PlayPlane.Z), Quaternion.identity);
            if (c.GetComponent<TemporaryObject>() == null) c.AddComponent<TemporaryObject>();
            drops.Add(new Drop
            {
                t = c.transform,
                vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(3.2f, 4.6f),
                hasFloor = floor.collider != null,
                restY = floor.collider != null ? floor.point.y + RestAboveFloor : centre.y,
            });
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (flash != null) flash.intensity = 2.2f * Mathf.Max(0f, 1f - age / 0.4f) * Mathf.Max(0f, 1f - age / 0.4f);

        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            m.age += dt;
            if (m.age >= m.life || m.sr == null) { if (m.sr != null) Destroy(m.sr.gameObject); motes.RemoveAt(i); continue; }
            m.vel *= Mathf.Pow(0.05f, dt);
            m.pos += (Vector3)(m.vel * dt);
            m.sr.transform.position = m.pos;
            Color c = Salvage.Shift; c.a = 1f - m.age / m.life; m.sr.color = c;
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

            // Then drift to its resting height (never into the floor or through a wall).
            if (d.hasFloor && d.age > 0.2f)
            {
                float y = Mathf.Lerp(next.y, d.restY, 1f - Mathf.Pow(0.01f, dt));
                Vector3 to = new Vector3(next.x, y, next.z);
                if (Physics2D.Linecast(next, to, groundMask).collider == null) next = to;
            }
            d.t.position = next;

            bool slow = d.vel.sqrMagnitude < 0.05f;
            bool home = !d.hasFloor || Mathf.Abs(next.y - d.restY) < 0.02f;
            if ((slow && home) || d.age > 2.5f) d.settled = true;
        }

        if (!anyMoving && motes.Count == 0 && age > 0.45f) Destroy(gameObject);
    }
}
