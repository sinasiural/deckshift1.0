using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A hanging press activated by a Lever: the Moss Knight's arena weapon, and the level press
// (Assets/Prefabs/CrusherTrap.prefab, the 'P' in room texts). The two are separate objects that share
// this script; everything the level press added (hanging chains, wind-up, dust, bounce) is off by
// default, so the boss room's press behaves exactly as it always has.
// Slams down, crushes anything in the impact zone (enemies AND the player),
// holds, winches back up, then needs a rearm period before it can fire again.
// Wire Lever.OnFlippedOn / OnFlippedOff to Activate() — the lever is a toggle,
// so both directions should trigger a slam.
public class CrusherTrap : MonoBehaviour
{
    [Header("Parts (assigned by setup)")]
    [SerializeField] private Rigidbody2D pressHead;          // kinematic body that moves
    [SerializeField] private SpriteRenderer chainRenderer;   // tiled sprite stretched between anchor and head
    [SerializeField] private Transform chainAnchor;          // fixed point at the ceiling

    [Header("Hanging chains (the level press)")]
    [Tooltip("Tiled chains that hang from a FIXED point (their own transform, sprite pivot at the top) " +
             "down to the head. Only their length changes, the way the Cainos Elevator's chains work. " +
             "Separate from the single chain above, which the Moss Knight's press still uses.")]
    [SerializeField] private SpriteRenderer[] hangingChains;
    [Tooltip("Head-local height where the hanging chains hook into the head.")]
    [SerializeField] private float chainHookHeight;
    [Tooltip("The head's sprite(s). Shaken during the wind-up and bounced on impact; the collider never moves for it.")]
    [SerializeField] private Transform headVisual;

    [Header("Wind-up (0 = slam the moment it is pulled)")]
    [Tooltip("A shudder before the drop: the catch releasing. It telegraphs the slam, and it means a pull has to be TIMED.")]
    [SerializeField] private float windupTime = 0f;
    [SerializeField] private float windupShake = 0.03125f;   // one pixel
    [SerializeField] private AudioClip releaseSound;
    [SerializeField, Range(0f, 2f)] private float releaseVolume = 0.8f;

    [Header("Impact dust (0 = none)")]
    [SerializeField] private int dustChips = 0;
    // The pack's lightest steel, not its stone: the floor stone is exactly the colour of the floor the
    // grit flies across, and measured on screen it vanished into it.
    [SerializeField] private Color dustColor = new Color32(0x9C, 0x95, 0x89, 255);
    [SerializeField] private Color dustColorDark = new Color32(0x83, 0x7A, 0x6D, 255);
    [Tooltip("Half the width of the head's crushing face, where the dust bursts out from under it.")]
    [SerializeField] private float dustHalfWidth = 1f;

    [Header("Motion")]
    [SerializeField] private float travelDistance = 16f;     // how far the head descends from idle
    [SerializeField] private float slamSpeed = 40f;
    [SerializeField] private float raiseSpeed = 3.5f;
    [SerializeField] private float holdTime = 0.7f;          // stays down before winching up
    [SerializeField] private float rearmTime = 6f;           // extra cooldown after fully raised

    [Header("Damage")]
    [SerializeField] private float crushDamage = 80f;
    [SerializeField] private float playerDamage = 20f;
    [SerializeField] private Vector2 damagePadding = new Vector2(0.3f, 0.6f); // extends impact box below/around head

    [Header("Feedback")]
    [SerializeField] private AudioClip slamSound;
    [Tooltip("Loudness of the slam. Plays as a 2D sound (no distance falloff); above 1 boosts it.")]
    [SerializeField, Range(0f, 2f)] private float slamVolume = 1.5f;
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeIntensity = 0.35f;

    private AudioSource sfxSource;   // 2D one-shot source built at runtime so the slam is always audible

    [Header("Shift Reward (only when it crushes the boss)")]
    [Tooltip("Shift crystal spawned at each point below when this crushes the boss. Assign Prefabs/ShiftCrystal.")]
    [SerializeField] private GameObject shiftCrystalPrefab;
    [Tooltip("Empty GameObjects placed where crystals should drop (e.g. one per platform, two on the ground sides).")]
    [SerializeField] private Transform[] crystalSpawnPoints;
    [Tooltip("How high the crystals arc as they burst from the boss toward their points.")]
    [SerializeField] private float crystalArcHeight = 3f;
    [Tooltip("Flight time from the boss to each point.")]
    [SerializeField] private float crystalFlightTime = 0.6f;

    private Vector2 idleHeadPos;   // world position cached at Start
    private Vector3 headVisualRest;
    private bool isBusy;           // mid-slam or winching up
    private bool isArmed = true;

    private float cooldownStartTime;
    private float cooldownTotal;

    public bool IsReady => isArmed && !isBusy;

    // 1 right after firing, easing to 0 as it rearms. For a cooldown clock on the lever.
    public float CooldownRemaining01
    {
        get
        {
            if (IsReady || cooldownTotal <= 0f) return 0f;
            return Mathf.Clamp01((cooldownStartTime + cooldownTotal - Time.time) / cooldownTotal);
        }
    }

    private void Start()
    {
        if (pressHead != null) idleHeadPos = pressHead.position;
        if (headVisual != null) headVisualRest = headVisual.localPosition;

        // 2D source so the slam plays at a flat, controllable volume regardless of how far the
        // press is from the camera (PlayClipAtPoint was 3D-attenuated, which made it too quiet).
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    // Called by the Lever's UnityEvents. Ignores the pull if the press isn't rearmed yet.
    public void Activate()
    {
        if (!IsReady || pressHead == null) return;
        StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        isBusy = true;
        isArmed = false;

        // Estimate the full not-ready window so the lever's clock can count it down.
        float slamTime = travelDistance / Mathf.Max(0.01f, slamSpeed);
        float raiseTime = travelDistance / Mathf.Max(0.01f, raiseSpeed);
        cooldownStartTime = Time.time;
        cooldownTotal = windupTime + slamTime + holdTime + raiseTime + rearmTime;

        if (windupTime > 0f) yield return WindupRoutine();

        Vector2 target = idleHeadPos + Vector2.down * travelDistance;

        // Slam down fast.
        while (pressHead.position.y > target.y + 0.01f)
        {
            float step = slamSpeed * Time.fixedDeltaTime;
            Vector2 next = Vector2.MoveTowards(pressHead.position, target, step);
            pressHead.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        ApplyImpactDamage();

        if (CameraShake.instance != null) CameraShake.instance.Shake(shakeDuration, shakeIntensity);
        SfxManager.PlayOn(sfxSource, slamSound, slamVolume);
        if (dustChips > 0) SpawnDust();
        if (headVisual != null) StartCoroutine(ImpactBounceRoutine());

        yield return new WaitForSeconds(holdTime);

        // Winch back up slowly — this is the window where the kill zone is safe.
        while (pressHead.position.y < idleHeadPos.y - 0.01f)
        {
            float step = raiseSpeed * Time.fixedDeltaTime;
            Vector2 next = Vector2.MoveTowards(pressHead.position, idleHeadPos, step);
            pressHead.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        isBusy = false;
        yield return new WaitForSeconds(rearmTime);
        isArmed = true;
    }

    private void ApplyImpactDamage()
    {
        Collider2D headCol = pressHead.GetComponent<Collider2D>();
        if (headCol == null) return;

        Bounds b = headCol.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y);
        Vector2 size = new Vector2(b.size.x + damagePadding.x * 2f, damagePadding.y * 2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        var damagedEnemies = new HashSet<EnemyHealth>();
        bool playerHit = false;
        bool bossHit = false;
        Vector2 bossPos = center;

        foreach (Collider2D hit in hits)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null && damagedEnemies.Add(enemy))
            {
                enemy.TakeDamage(crushDamage);
                if (enemy.GetComponent<MossKnightBoss>() != null)
                {
                    bossHit = true;
                    bossPos = enemy.transform.position;
                }
                continue;
            }

            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player != null && !playerHit)
            {
                playerHit = true;
                player.TakeDamage(playerDamage);
            }
        }

        // Landing a crusher hit on the boss bursts Shift out of him toward the arena — a resource lifeline.
        if (bossHit) DropShiftCrystals(bossPos);
    }

    private void DropShiftCrystals(Vector2 bossPos)
    {
        if (shiftCrystalPrefab == null || crystalSpawnPoints == null) return;

        Vector2 origin = bossPos + Vector2.up * 1.5f;   // erupt from the boss's body, not his feet
        foreach (Transform p in crystalSpawnPoints)
        {
            if (p == null) continue;
            GameObject crystal = Instantiate(shiftCrystalPrefab, origin, Quaternion.identity);
            StartCoroutine(ArcCrystal(crystal, crystal.transform.localScale, origin, p.position));
        }
    }

    // Pops the crystal into existence at the boss and arcs it to its resting point. It stays a live
    // pickup the whole time, so the player can even snag it out of the air.
    private IEnumerator ArcCrystal(GameObject crystal, Vector3 baseScale, Vector2 from, Vector2 to)
    {
        float time = Mathf.Max(0.05f, crystalFlightTime);
        float arcH = crystalArcHeight * Random.Range(0.8f, 1.2f);
        float t = 0f;
        while (t < time)
        {
            if (crystal == null) yield break;   // collected mid-flight
            float k = t / time;
            float x = Mathf.Lerp(from.x, to.x, k);
            float y = Mathf.Lerp(from.y, to.y, k) + arcH * 4f * k * (1f - k);
            crystal.transform.position = new Vector3(x, y, crystal.transform.position.z);
            crystal.transform.localScale = baseScale * Mathf.Clamp01(k / 0.15f);   // pop out as it launches
            t += Time.deltaTime;
            yield return null;
        }
        if (crystal != null)
        {
            crystal.transform.position = new Vector3(to.x, to.y, crystal.transform.position.z);
            crystal.transform.localScale = baseScale;
        }
    }

    // The catch lets go: the head shudders a pixel either way, sags a pixel, and clanks.
    private IEnumerator WindupRoutine()
    {
        SfxManager.PlayOn(sfxSource, releaseSound, releaseVolume);
        float t = 0f;
        while (t < windupTime)
        {
            if (headVisual != null)
            {
                float side = Mathf.FloorToInt(t / 0.04f) % 2 == 0 ? 1f : -1f;
                headVisual.localPosition = headVisualRest + new Vector3(side * windupShake, -windupShake, 0f);
            }
            t += Time.deltaTime;
            yield return null;
        }
        if (headVisual != null) headVisual.localPosition = headVisualRest;
    }

    // A heavy weight does not stop dead: it kicks back up two pixels off the floor and settles.
    private IEnumerator ImpactBounceRoutine()
    {
        foreach (float px in new[] { 2f, 1f })
        {
            headVisual.localPosition = headVisualRest + Vector3.up * (px / 32f);
            yield return new WaitForSeconds(0.045f);
        }
        headVisual.localPosition = headVisualRest;
    }

    // Grit bursting out from under both edges of the head. Plain pixel chips on the room's own stone
    // colours, snapped to the pixel grid so they read as part of the art rather than as particles.
    private static Sprite pixelSprite;

    private void SpawnDust()
    {
        if (pixelSprite == null)
            pixelSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 128f); // 1 game pixel
        StartCoroutine(DustRoutine(pressHead.transform.position));
    }

    private IEnumerator DustRoutine(Vector3 floor)
    {
        const float Gravity = -14f, Px = 32f;
        int n = dustChips;
        var root = new GameObject("CrusherDust").transform;
        root.SetParent(transform, false);   // goes with the room
        var chips = new SpriteRenderer[n];
        var pos = new Vector2[n];
        var vel = new Vector2[n];
        var life = new float[n];
        var baseColor = new Color[n];

        for (int i = 0; i < n; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            pos[i] = new Vector2(floor.x + side * (dustHalfWidth + Random.Range(-0.2f, 0.1f)), floor.y + Random.Range(0f, 0.12f));
            vel[i] = new Vector2(side * Random.Range(1.5f, 5f), Random.Range(1.2f, 4.4f));
            life[i] = Random.Range(0.35f, 0.75f);
            baseColor[i] = Color.Lerp(dustColor, dustColorDark, Random.value);

            var go = new GameObject("Chip");
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * Random.Range(2, 5);   // 2-4 pixels
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.color = baseColor[i];
            sr.sortingOrder = 3;
            chips[i] = sr;
        }

        float t = 0f;
        while (t < 0.8f)
        {
            float dt = Time.deltaTime;
            t += dt;
            for (int i = 0; i < n; i++)
            {
                if (chips[i] == null) continue;
                if (t >= life[i]) { chips[i].enabled = false; continue; }
                vel[i].y += Gravity * dt;
                vel[i].x *= 1f - Mathf.Min(1f, 3f * dt);
                pos[i] += vel[i] * dt;
                if (pos[i].y < floor.y) { pos[i].y = floor.y; vel[i].y = 0f; vel[i].x *= 0.5f; }   // settles on the floor
                chips[i].transform.position = new Vector3(Mathf.Round(pos[i].x * Px) / Px, Mathf.Round(pos[i].y * Px) / Px, floor.z);
                float k = t / life[i];
                var c = baseColor[i];
                c.a = 1f - k * k;
                chips[i].color = c;
            }
            yield return null;
        }
        Destroy(root.gameObject);
    }

    // Keep the chain sprites stretched between their fixed tops and the head.
    private void LateUpdate()
    {
        if (hangingChains != null && pressHead != null)
        {
            float hookY = pressHead.transform.position.y + chainHookHeight;
            foreach (var chain in hangingChains)
            {
                if (chain == null) continue;
                chain.size = new Vector2(chain.size.x, Mathf.Max(0.01f, chain.transform.position.y - hookY));
            }
        }

        if (chainRenderer == null || chainAnchor == null || pressHead == null) return;

        float top = chainAnchor.position.y;
        float bottom = pressHead.position.y;
        float length = Mathf.Max(0.01f, top - bottom);

        chainRenderer.size = new Vector2(chainRenderer.size.x, length);
        chainRenderer.transform.position = new Vector3(chainAnchor.position.x, bottom + length * 0.5f, chainRenderer.transform.position.z);
    }
}
