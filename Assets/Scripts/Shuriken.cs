using UnityEngine;

// The Ninja's thrown star. House pattern: built ENTIRELY in code, sprite included, like ScrapPickup
// and SpitGlob — so there is no prefab to wire and nothing to lose out of a scene.
//
// ⚠️ IT FLIES WHERE THE CURSOR WAS, NOT WHERE THE PLAYER FACES. That is the entire point of the
// card and the one thing that separates it from Fireball. Direction is handed in at spawn.
public class Shuriken : MonoBehaviour
{
    private float damage;
    private bool hasHit;

    private static Sprite cachedSprite;

    private const float SPEED = 22f;
    private const float LIFE = 1.3f;      // ~28 units of range
    private const float SPIN = 1440f;     // deg/sec — fast enough to read as a blur, not a shape

    private static readonly Color Steel = new Color(0.82f, 0.86f, 0.92f, 1f);

    // ⚠️ THE STREAK IS HOT AT THE HEAD AND DEAD AT THE TAIL, and it used to be neither.
    //
    // It was one flat dark grey (0.34, 0.36, 0.43) at alpha 0.40, chosen on the reasoning that a pale
    // streak would be "the loudest thing on screen". That reasoning was sound and the result was not:
    // the dungeon's own stone measures #444548, so a 0.35 grey at 40% over it composites to almost
    // exactly the background. The designer's report was that the traces are "almost unseeable"
    // (2026-08-22), and they were right — the trail was drawing itself in the wall's colour.
    //
    // The fix is not "brighter everywhere", which really would be a pale worm following the star
    // around. It is a GRADIENT: bright for the first few pixels behind the blade and gone by the end.
    // A short hot head reads as speed; a long even streak reads as an object. Combined with the 0.11s
    // lifetime, what is on screen at any moment is a stub, not a banner.
    // ⚠️ AND THE PLAYER'S STAR IS SHIFT CYAN, NOT WHITE — measured on screen, not reasoned about.
    // Near-white at these alphas photographed as a solid glowing lozenge with the four-bladed
    // silhouette completely washed out of it: legible, and no longer legible AS A SHURIKEN. Cyan
    // carries far less luminance at the same alpha, so the dark blade shape survives on top of it.
    //
    // It also does a second job for free. In this fight his stars and your stars are in the air at
    // the same time constantly — you are throwing his own ammo back at him — and the ONE thing the
    // player must never misread is which of them is going to hurt them. His are Wound red; yours are
    // Shift cyan. Both are already the game's own accents, so this spends no new hue.
    private static readonly Color StreakHot = new Color(0.72f, 0.97f, 1f, 1f);
    private static readonly Color StreakCool = new Color(0.16f, 0.34f, 0.42f, 1f);
    private static readonly Color KeyShift =
        new Color(Salvage.Shift.r, Salvage.Shift.g, Salvage.Shift.b, 0.45f);

    // How wide the star sits in the world, whatever art it is given.
    private const float WORLD_SIZE = 0.62f;

    // `art` is the pack's own shuriken sprite, handed in by the player so the star that flies is
    // literally the one that was in his hand. Null falls back to the procedural star — the card is
    // in the shared pool, so a character holding a staff can still throw one.
    public static Shuriken Spawn(Vector3 pos, Vector2 dir, float damage, RuntimeCard source, Sprite art = null)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        var go = new GameObject("Shuriken");
        go.transform.position = pos;

        int layer = LayerMask.NameToLayer("PlayerProjectile");
        if (layer >= 0) go.layer = layer;

        // Swept out on a room change with every other runtime spawn. Without it this is a
        // scene-root object that outlives the room that threw it.
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art != null ? art : GetStarSprite();
        sr.color = Color.white;
        sr.sortingOrder = 5;

        // ⚠️ SIZED FROM THE SPRITE'S OWN BOUNDS, never from a hard-coded scale. The pack's shuriken
        // is an 11px texture at whatever pixels-per-unit it was imported with; the procedural
        // fallback is 32px at 32 PPU. A fixed scale would make one of the two the wrong size, and
        // re-importing the art at a different PPU would silently resize the projectile.
        float native = sr.sprite.bounds.size.x;
        go.transform.localScale = Vector3.one * (native > 0.001f ? WORLD_SIZE / native : 1f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;              // we spin the transform ourselves, visually only
        rb.linearVelocity = dir * SPEED;

        var col = go.AddComponent<CircleCollider2D>();
        // ⚠️ Collider radius is in LOCAL units, so it must be divided back out of the scale above or
        // the hitbox changes size with whatever art was handed in. Backed out to a fixed ~0.26
        // world radius — a little inside the blade tips, so it doesn't clip walls it visually
        // passes.
        float scale = go.transform.localScale.x;
        col.radius = scale > 0.001f ? 0.26f / scale : 0.26f;
        col.isTrigger = true;

        var s = go.AddComponent<Shuriken>();
        s.damage = damage;
        s.sourceCard = source;

        AttachKeyline(go.transform, sr, KeyShift);
        AttachStreak(go.transform, sr.sortingOrder, StreakHot, StreakCool, 0.70f);
        Destroy(go, LIFE);
        return s;
    }

    // The card that threw it, stamped at spawn. Same reason Fireball carries one: the shot lands
    // after ExecuteAction has returned and cleared DeckManager.AttributedCard, so without this
    // every damage-time blessing silently does nothing on this card.
    [HideInInspector] public RuntimeCard sourceCard;

    private void Update()
    {
        transform.Rotate(0f, 0f, SPIN * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if (other.GetComponent<Portal>() != null) return;
        if (other.GetComponentInParent<PlayerController>() != null) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            hasHit = true;

            RuntimeCard prev = DeckManager.instance != null ? DeckManager.instance.AttributedCard : null;
            if (DeckManager.instance != null) DeckManager.instance.AttributedCard = sourceCard;

            float dealt = RelicManager.instance != null
                ? RelicManager.instance.ModifyPlayerDamage(damage, target as EnemyHealth)
                : damage;
            target.TakeDamage(dealt);

            // Cleared again immediately — a stale attribution would hand the next spike or pogo
            // bounce a Grudge bonus it never earned.
            if (DeckManager.instance != null) DeckManager.instance.AttributedCard = prev;

            if (CameraShake.instance != null) CameraShake.instance.Shake(0.06f, 0.18f);
            SparkBurst(transform.position, 7, 1f);
            Destroy(gameObject);
            return;
        }

        // Triggers (pickups, zones, water) are passed straight through; anything else is terrain.
        if (other.isTrigger) return;
        hasHit = true;
        // A smaller shower off stone — it glances, it doesn't bite.
        SparkBurst(transform.position, 4, 0.7f);
        Destroy(gameObject);
    }

    // Short radial streaks that fly out and die. Its own object, because the star is destroyed on
    // this same frame — the same rule EnemyHealth.Die's VFX has to follow.
    private static void SparkBurst(Vector3 pos, int count, float scale)
    {
        var root = new GameObject("ShurikenSparks");
        root.transform.position = pos;
        root.AddComponent<TemporaryObject>();
        root.AddComponent<SparkFade>();

        for (int i = 0; i < count; i++)
        {
            var s = new GameObject("Spark");
            s.transform.SetParent(root.transform, false);

            float ang = Random.Range(0f, 360f);
            float len = Random.Range(0.16f, 0.34f) * scale;
            s.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            s.transform.localPosition = Quaternion.Euler(0f, 0f, ang) * Vector3.right * (len * 0.6f);
            s.transform.localScale = new Vector3(len, Random.Range(0.03f, 0.055f) * scale, 1f);

            var sr = s.AddComponent<SpriteRenderer>();
            sr.sprite = FlatUI.Pixel();
            sr.color = new Color(1f, 0.97f, 0.88f, 1f);
            sr.sortingOrder = 7;
        }
    }

    /// <summary>
    /// The motion streak. SHARED with the boss's stars (see BossShuriken) so the ammo he throws and
    /// the ammo you throw back are visibly the same object in different hands — only the colour is
    /// his or yours.
    /// </summary>
    public static GameObject AttachStreak(Transform star, int baseOrder, Color hot, Color cool, float headAlpha)
    {
        var trailGO = new GameObject("Streak");
        trailGO.transform.SetParent(star, false);

        var trail = trailGO.AddComponent<TrailRenderer>();
        trail.time = 0.11f;
        trail.startWidth = 0.24f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.autodestruct = false;
        trail.numCapVertices = 2;
        trail.sortingOrder = baseOrder - 2;      // behind both the star and its keyline
        trail.material = new Material(Shader.Find("Sprites/Default"));

        // Hot for the first 35% and out by the end. See the StreakHot/StreakCool note above for why
        // this is a gradient rather than one colour at a higher alpha.
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(cool, 0.35f), new GradientColorKey(cool, 1f) },
            new[] { new GradientAlphaKey(headAlpha, 0f), new GradientAlphaKey(headAlpha * 0.42f, 0.35f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;

        // ⚠️ The streak must NOT inherit the star's spin, or it whips round in a circle instead of
        // trailing behind the flight. Detaching it from the rotation is what keeps it a streak.
        trailGO.transform.SetParent(null, true);
        // ...and detaching preserves the WORLD transform, so it would keep the star's art-derived
        // scale and multiply the widths above by it. The streak's width is already in world units.
        trailGO.transform.localScale = Vector3.one;
        trailGO.AddComponent<TrailFollow>().Init(star);
        trailGO.AddComponent<TemporaryObject>();
        return trailGO;
    }

    /// <summary>
    /// A copy of the star drawn one order BEHIND it and slightly larger, in a bright colour — so the
    /// silhouette is edged in light. This is what makes a dark star legible against dark stone, and
    /// it was the designer's actual complaint (2026-08-22: "they kind of blend in with the
    /// background"). The pack's own shuriken art is dark metal, the dungeon is dark stone, and there
    /// is essentially no edge contrast between them.
    ///
    /// ⚠️ IT IS THE SAME SILHOUETTE, NOT A SOFT HALO. A feathered ring at the blade radius was built
    /// here once and rejected: with hard pixel art in front of it, it read as a UI selection circle,
    /// because a smooth gradient is simply a different drawing language from the sprite it is behind.
    /// Scaling the sprite itself keeps one language, and it spins with the star for free.
    ///
    /// ⚠️ Returned so callers can RECOLOUR it. On the boss's stars it carries the whole state
    /// machine — red in flight means "dodge", gold on the floor means "take me".
    /// </summary>
    public static SpriteRenderer AttachKeyline(Transform star, SpriteRenderer sr, Color tint, float grow = 1.30f)
    {
        if (sr == null || sr.sprite == null) return null;

        var go = new GameObject("Keyline");
        go.transform.SetParent(star, false);
        // A child, so the star's own art-derived scale is already applied by the parent and this is a
        // clean 30% growth on top of it — the same trap BuildLane's one-pixel sprite fell into, dodged
        // by never touching world units here at all.
        go.transform.localScale = Vector3.one * grow;

        var k = go.AddComponent<SpriteRenderer>();
        k.sprite = sr.sprite;
        k.color = tint;
        k.sortingLayerID = sr.sortingLayerID;
        k.sortingOrder = sr.sortingOrder - 1;
        return k;
    }

    /// <summary>
    /// The procedural star, shared so the Ninja boss's thrown stars are visibly the SAME object the
    /// player ends up throwing back (see BossShuriken). Generating a second one would drift.
    /// </summary>
    public static Sprite StarSprite => GetStarSprite();

    // A four-bladed steel star, drawn as PIXEL ART.
    //
    // ⚠️ LOW RESOLUTION AND POINT FILTERING ARE THE POINT, NOT A SHORTCUT. A smooth high-res star
    // with feathered edges reads as a vector graphic pasted over the game — the pack's own shuriken
    // sprite is ELEVEN pixels across. A 32px texture at 32 pixels-per-unit puts each texel at
    // roughly two screen pixels, which is the same chunk size as the tiles behind it.
    //
    // ⚠️ AND IT NEEDS A DARK KEYLINE. The world is dark grey stone lit at half intensity; a pale
    // steel silhouette on it has almost no edge contrast and simply disappears while moving. The
    // outline is what makes it legible, exactly like the keyline the card medallions needed.
    //
    // The SWEEP is what makes it a shuriken rather than a sparkle: each blade has a straight radial
    // leading edge and a trailing edge cut back to the hub, so the shape states which way it turns.
    private static Sprite GetStarSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };

        Color outline = new Color(0.07f, 0.08f, 0.11f, 1f);
        float half = S * 0.5f;
        const float TAU = Mathf.PI * 2f;
        const float HUB = 0.30f;      // solid disc the blades grow from
        const float HOLE = 0.13f;     // pierced centre
        const float REACH = 0.66f;    // blade length beyond the hub
        const float SPAN = 0.70f;     // fraction of the 90° sector a blade occupies
        const float RIM = 0.085f;     // keyline thickness, in the same normalised units

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float seg = Mathf.Repeat(Mathf.Atan2(dy, dx) / (TAU * 0.25f), 1f);

                // Full reach on the leading edge, tapering to nothing across SPAN of the sector.
                float taper = Mathf.Pow(Mathf.Clamp01(1f - seg / SPAN), 0.8f);
                float outer = HUB + REACH * taper;

                bool solid = d <= outer && d >= HOLE;
                bool rim = d <= outer + RIM && d >= HOLE - RIM;

                if (!rim) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }
                if (!solid) { tex.SetPixel(x, y, outline); continue; }

                // Steel: dark at the hub, bright out along the blade, with a hot leading edge.
                float along = Mathf.Clamp01((d - HUB) / Mathf.Max(outer - HUB, 0.01f));
                float lit = Mathf.Lerp(0.50f, 0.97f, along);
                if (seg < 0.16f) lit = Mathf.Max(lit, 0.92f);

                // A dark ring reads as the rivet holding the blades together.
                if (Mathf.Abs(d - 0.215f) < 0.035f) lit *= 0.55f;

                tex.SetPixel(x, y, new Color(lit, lit * 0.99f, lit * 0.96f, 1f));
            }
        }
        tex.Apply();

        // 32 pixels-per-unit, so the sprite is exactly one world unit and transform scale is the
        // only thing that decides its size.
        cachedSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 32f);
        return cachedSprite;
    }

}

// Keeps the streak on the star's POSITION without inheriting its rotation, and takes itself away
// once the star is gone so the tail can finish fading instead of vanishing mid-air.
public class TrailFollow : MonoBehaviour
{
    private Transform target;
    private float orphan;

    public void Init(Transform t) { target = t; }

    private void LateUpdate()
    {
        if (target != null) { transform.position = target.position; return; }

        orphan += Time.deltaTime;
        if (orphan > 0.25f) Destroy(gameObject);
    }
}

// Throws the impact sparks outward and fades them. Scaled time on purpose: this happens in the
// world, so a HitStop freeze should hold it still along with everything else.
public class SparkFade : MonoBehaviour
{
    private float t;
    private const float LIFE = 0.22f;
    private SpriteRenderer[] parts;
    private Vector3[] dirs;

    private void Awake()
    {
        parts = GetComponentsInChildren<SpriteRenderer>();
        dirs = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            dirs[i] = parts[i].transform.localPosition.normalized;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / LIFE);

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            parts[i].transform.localPosition += dirs[i] * (2.6f * (1f - k) * Time.deltaTime);
            Color c = parts[i].color;
            c.a = 1f - k * k;
            parts[i].color = c;
        }

        if (k >= 1f) Destroy(gameObject);
    }
}
