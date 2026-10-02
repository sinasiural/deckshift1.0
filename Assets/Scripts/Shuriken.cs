using System.Collections.Generic;
using UnityEngine;

// The Ninja's thrown star. House pattern: built ENTIRELY in code, sprite included, like ScrapPickup
// and SpitGlob — so there is no prefab to wire and nothing to lose out of a scene.
//
// ⚠️ IT FLIES WHERE THE CURSOR WAS, NOT WHERE THE PLAYER FACES. That is the entire point of the
// card and the one thing that separates it from Fireball. Direction is handed in at spawn.
//
// ⚠️ A STAR THAT MISSES STICKS (designer, 2026-10-02; the Shuriken card, for every character). A
// star that meets terrain instead of an enemy buries itself in the wall like the Ninja boss's do,
// wears a white outline, and walking into it puts the charge back on the card that threw it. Only a
// throw that actually COST a charge can stick, so a free throw (Sleight of Hand, the hub) can never
// mint one. Borrowed Steel's stars still break: that fight has its own pickup loop.
public class Shuriken : MonoBehaviour
{
    private float damage;
    private bool hasHit;

    // ---- fetching a missed star ----------------------------------------------------------------
    private bool fetchable;       // decided at spawn, never changes
    private bool stuck;
    private float flightTime;
    private Vector2 lastPos;
    private Rigidbody2D body;
    private SpriteRenderer face;
    private SpriteRenderer keyline;
    private SpriteRenderer outline;
    private StarAfterimages trail;
    private float glint;

    private const float PICKUP_R = 0.7f;   // forgiving: brushing past it is enough
    private const float EMBED = 0.12f;     // how far into the surface it buries, so it reads as stuck

    // Every fetchable star in flight or in a wall. DeckManager asks this whether an empty Shuriken
    // still has stars out (see DeckManager.HoldEmpty). Entries remove themselves in OnDestroy and
    // every read prunes nulls — a static list that outlives a domain reload is a known trap here.
    private static readonly List<Shuriken> live = new List<Shuriken>();

    public static int LiveStarsOf(RuntimeCard card)
    {
        if (card == null) return 0;
        int n = 0;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (live[i] == null) { live.RemoveAt(i); continue; }
            if (live[i].sourceCard == card && !live[i].hasHit) n++;
        }
        return n;
    }

    private static Sprite cachedSprite;

    private const float SPEED = 22f;
    private const float LIFE = 1.3f;      // ~28 units of range
    private const float SPIN = 1440f;     // deg/sec — fast enough to read as a blur, not a shape

    // ⚠️ THE TRAIL IS A CHAIN OF FADING COPIES OF THE STAR, NOT A STREAK (designer, 2026-10-02: "i
    // dont really like the trails anyway ... i think they are misaligned sometimes"). It used to be
    // a TrailRenderer — a smooth gradient ribbon on a separate object chasing the star's position —
    // and it lost twice: a soft ribbon is a different drawing language from pixel art, and being a
    // separate object it could fall out of step with the star. Each copy is the star's own pixels,
    // dropped exactly where the star was (see StarAfterimages), so it cannot drift.
    //
    // ⚠️ THE PLAYER'S TRAIL IS SHIFT CYAN, THE BOSS'S IS WOUND RED. In the Ninja fight his stars and
    // yours are in the air at the same time constantly — you are throwing his own ammo back at him —
    // and the ONE thing the player must never misread is which of them is going to hurt them. Both
    // are already the game's own accents, so this spends no new hue. (Not white: near-white at these
    // alphas washed the four-bladed silhouette out into a glowing lozenge, measured on screen.)
    private static readonly Color YourGhost = new Color(Salvage.Shift.r, Salvage.Shift.g, Salvage.Shift.b, 0.5f);
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
        // ⚠️ ON THE PLAY PLANE, like the boss's stars. The throw origin is a 2D point, so this used
        // to fly at z = 0 — BEHIND the plane every actor stands on, where any prop between the two
        // drew over the star and its trail. That is why the trail could look broken or missing.
        go.transform.position = new Vector3(pos.x, pos.y, PlayPlane.Z);

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
        s.body = rb;
        s.face = sr;
        s.lastPos = go.transform.position;

        // The thrower's own card reports whether this throw cost a charge; DeckManager set it on the
        // click, THROW_RELEASE before this spawn.
        s.fetchable = source != null && source.lastPlaySpentCharge
                      && DeckManager.instance != null && DeckManager.instance.FetchesStars(source);

        s.keyline = AttachKeyline(go.transform, sr, KeyShift);
        s.trail = StarAfterimages.Attach(go.transform, sr, YourGhost);

        // A fetchable star times out only while it is still FLYING (see Update); once it is in a
        // wall it waits there for the player, or for the room to end.
        if (s.fetchable) live.Add(s);
        else Destroy(go, LIFE);
        return s;
    }

    private void OnDestroy() { live.Remove(this); }

    // The card that threw it, stamped at spawn. Same reason Fireball carries one: the shot lands
    // after ExecuteAction has returned and cleared DeckManager.AttributedCard, so without this
    // every damage-time blessing silently does nothing on this card.
    [HideInInspector] public RuntimeCard sourceCard;

    private void Update()
    {
        if (stuck) { UpdateStuck(); return; }

        transform.Rotate(0f, 0f, SPIN * Time.deltaTime);

        if (fetchable)
        {
            flightTime += Time.deltaTime;
            if (flightTime >= LIFE) Destroy(gameObject);   // flew off into nothing: that one is gone
        }
    }

    // ⚠️ A FETCHABLE STAR FINDS TERRAIN BY RAYCAST TOO, not only by trigger. At 22 u/s a physics
    // step moves it ~0.44 units, so a trigger alone can let it slip through a thin wall — a star
    // buried on the far side of a wall is a charge the player can see and never reach. The sweep
    // from the previous position also gives the exact surface point to bury it at. Same reasoning
    // as BossShuriken's.
    private void FixedUpdate()
    {
        if (!fetchable || stuck || hasHit) return;

        Vector2 now = transform.position;
        Vector2 delta = now - lastPos;
        if (delta.sqrMagnitude > 0.000001f)
        {
            RaycastHit2D hit = Physics2D.Raycast(lastPos, delta.normalized, delta.magnitude,
                                                 LayerMask.GetMask("Ground"));
            if (hit.collider != null) { Stick(hit.point); return; }
        }
        lastPos = now;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || stuck) return;
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

        if (fetchable)
        {
            // Triggers report AFTER the physics step has already carried the star into the wall,
            // so find the surface it crossed by sweeping back along this step's flight line.
            Vector2 flight = body != null && body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized : Vector2.right;
            Vector2 from = lastPos;
            float reach = Vector2.Distance(from, transform.position) + 0.5f;
            RaycastHit2D hit = Physics2D.Raycast(from, flight, reach, LayerMask.GetMask("Ground"));
            Stick(hit.collider != null ? hit.point : (Vector2)transform.position);
            return;
        }

        hasHit = true;
        // A smaller shower off stone — it glances, it doesn't bite.
        SparkBurst(transform.position, 4, 0.7f);
        Destroy(gameObject);
    }

    private void Stick(Vector2 point)
    {
        stuck = true;

        Vector2 flight = body != null && body.linearVelocity.sqrMagnitude > 0.001f
            ? body.linearVelocity.normalized : Vector2.right;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        // It no longer touches anything: an enemy walking through a stuck star must not be hit by
        // it, and the pickup is a plain distance check (UpdateStuck), which does not depend on how
        // the PlayerProjectile layer is set up against the player in the collision matrix.
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Buried a little way along its own flight line, angled the way it flew, and moved onto the
        // play plane. ⚠️ Z is set by hand: assigning a Vector2 to a position silently zeroes it, and a
        // star off the plane renders behind the room's props (BossShuriken paid for that one).
        Vector2 rest = point + flight * EMBED;
        transform.position = new Vector3(rest.x, rest.y, PlayPlane.Z);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flight.y, flight.x) * Mathf.Rad2Deg);

        // A star in a wall wears a thin white outline: "yours, come and get it". Same treatment as the
        // boss's stars on the floor. See AttachOutline for why it is a traced outline, not a tint.
        if (trail != null) trail.enabled = false;
        if (keyline != null) keyline.enabled = false;
        outline = AttachOutline(transform, face, Color.white);

        SfxManager.PlayAtPoint(ProcSfx.ShurikenStick, transform.position, 0.55f);
        SparkBurst(transform.position, 4, 0.7f);
    }

    private void UpdateStuck()
    {
        // A slow glint on the OUTLINE so it reads as loot rather than scenery. Brightening the dark
        // star itself is invisible against dark stone; the white edge is where the contrast is.
        glint += Time.deltaTime * 3.2f;
        float k = 0.78f + 0.22f * Mathf.Sin(glint);
        if (outline != null) outline.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * k);

        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        if (p == null) return;
        Collider2D capsule = p.GetComponent<CapsuleCollider2D>();
        Vector2 here = transform.position;
        Vector2 nearest = capsule != null ? capsule.ClosestPoint(here) : (Vector2)p.transform.position;
        if ((nearest - here).sqrMagnitude > PICKUP_R * PICKUP_R) return;

        hasHit = true;
        if (DeckManager.instance != null) DeckManager.instance.ReturnStarCharge(sourceCard);
        SfxManager.PlayAtPoint(ProcSfx.ShurikenCatch, transform.position, 0.9f);
        SparkBurst(transform.position, 5, 0.8f);
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
    /// A one-pixel outline traced around the star's own pixels, as a child that spins with it.
    /// SHARED with the boss's stars: a star lying in a wall, his or yours, wears a thin white one
    /// (designer, 2026-10-02: "a small white outline to the black shurikens that are dropped").
    ///
    /// ⚠️ TRACED, NOT TINTED. The keyline below is the star's own sprite scaled up and tinted, and a
    /// tint MULTIPLIES — on the pack's dark-metal star, "gold" or "white" comes out as a muddy dark
    /// rim, which is why the old gold "take me" edge read as brown. Tracing the silhouette gives a
    /// flat colour that is actually that colour, one art pixel thick, so it stays pixel art.
    /// </summary>
    public static SpriteRenderer AttachOutline(Transform star, SpriteRenderer sr, Color colour)
    {
        if (sr == null || sr.sprite == null) return null;
        Sprite ring = StarArt.Outline(sr.sprite);
        if (ring == null) return null;

        var go = new GameObject("Outline");
        go.transform.SetParent(star, false);   // same pixels-per-unit, so scale 1 lines it up exactly

        var o = go.AddComponent<SpriteRenderer>();
        o.sprite = ring;
        o.sharedMaterial = StarArt.MaterialFor(ring);   // unlit: white stays white in a dark room
        o.color = colour;
        o.sortingLayerID = sr.sortingLayerID;
        o.sortingOrder = sr.sortingOrder - 1;
        return o;
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
    /// ⚠️ Returned so callers can RECOLOUR or HIDE it. On the boss's stars the red keyline means
    /// "dodge"; once a star is in a wall the keyline is hidden and the white outline takes over.
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

// A star's trail: flat-colour copies of its silhouette dropped along the path it actually flew, each
// fading and shrinking on its own. Shared by the player's stars (Shift cyan) and the Ninja boss's
// (Wound red). See the note above Shuriken.YourGhost for why it replaced the TrailRenderer streak.
//
// ⚠️ SPACED BY DISTANCE, NOT BY TIME. Copies are laid at fixed intervals along the segment the star
// covered since last frame, so the chain looks the same at 30 fps and at 144 — dropping one per frame
// would bunch them up on a fast machine and leave gaps on a slow one. Disable the component to stop
// the trail (a star stuck in a wall); re-enabling starts a fresh chain from wherever the star is.
public class StarAfterimages : MonoBehaviour
{
    private const float SPACING = 0.3f;     // world units between copies; the star is 0.62 wide
    private const float LIFE = 0.13f;       // so roughly five copies are on screen at full speed
    private const float END_SCALE = 0.55f;

    private Sprite ghost;
    private Material material;
    private Color colour;
    private int sortingLayer;
    private int order;
    private Vector3 last;
    private float sinceLast;

    public static StarAfterimages Attach(Transform star, SpriteRenderer sr, Color colour)
    {
        if (sr == null || sr.sprite == null) return null;
        Sprite ghost = StarArt.Silhouette(sr.sprite);
        if (ghost == null) return null;

        var t = star.gameObject.AddComponent<StarAfterimages>();
        t.ghost = ghost;
        t.material = StarArt.MaterialFor(ghost);
        t.colour = colour;
        t.sortingLayer = sr.sortingLayerID;
        t.order = sr.sortingOrder - 2;       // behind the star and its keyline
        t.last = star.position;
        return t;
    }

    private void OnEnable()
    {
        last = transform.position;
        sinceLast = 0f;
    }

    private void LateUpdate()
    {
        Vector3 now = transform.position;
        Vector3 step = now - last;
        float length = step.magnitude;
        if (length < 0.0001f) return;

        // Walk this frame's segment, dropping a copy every SPACING units of travel.
        for (float at = SPACING - sinceLast; at <= length; at += SPACING)
            Drop(last + step * (at / length));

        sinceLast = (sinceLast + length) % SPACING;
        last = now;
    }

    private void Drop(Vector3 where)
    {
        var go = new GameObject("StarGhost");
        // Just behind the play plane: behind the actor who threw it, in front of the room — the
        // spot BraceVFX measured (PlayPlane.Z + 0.0005), since PlayPlane leaves props that close.
        go.transform.position = new Vector3(where.x, where.y, PlayPlane.Z + 0.0005f);
        go.transform.rotation = transform.rotation;
        go.transform.localScale = transform.lossyScale;
        go.AddComponent<TemporaryObject>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ghost;
        sr.sharedMaterial = material;
        sr.color = colour;
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = order;

        go.AddComponent<AfterimageFade>().Init(colour, LIFE, END_SCALE);
    }
}

// Fades and shrinks one trail copy, then removes it. Scaled time, like SparkFade: it lives in the
// world, so a HitStop freeze holds it still with everything else.
public class AfterimageFade : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color colour;
    private float life;
    private float endScale;
    private Vector3 startScale;
    private float t;

    public void Init(Color c, float lifetime, float shrinkTo)
    {
        sr = GetComponent<SpriteRenderer>();
        colour = c;
        life = Mathf.Max(0.01f, lifetime);
        endScale = shrinkTo;
        startScale = transform.localScale;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        if (sr != null) sr.color = new Color(colour.r, colour.g, colour.b, colour.a * (1f - k));
        transform.localScale = startScale * Mathf.Lerp(1f, endScale, k);
        if (k >= 1f) Destroy(gameObject);
    }
}

// Flat-colour versions of a star's art, generated once per sprite and cached: a SILHOUETTE (every
// opaque pixel white, for the trail) and an OUTLINE (a one-pixel ring around those pixels, for a star
// in a wall). Both are white so the renderer's colour IS the colour — tinting the star's own dark
// art can only ever darken it.
//
// ⚠️ THE PIXELS ARE READ THROUGH A RENDER TEXTURE, so this works on art that was not imported as
// Read/Write — the pack's sprites are not, and flipping that import setting on a shared pack asset is
// not this code's business. Only ALPHA is read, which is the same in linear and gamma space.
public static class StarArt
{
    private static readonly Dictionary<Sprite, Sprite> silhouettes = new Dictionary<Sprite, Sprite>();
    private static readonly Dictionary<Sprite, Sprite> outlines = new Dictionary<Sprite, Sprite>();
    private static readonly Dictionary<Sprite, Material> materials = new Dictionary<Sprite, Material>();

    public static Sprite Silhouette(Sprite src)
    {
        if (src == null) return null;
        if (silhouettes.TryGetValue(src, out Sprite cached) && cached != null) return cached;

        bool[] solid = ReadSolid(src, out int w, out int h);
        if (solid == null) return null;

        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++)
            px[i] = solid[i] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);

        Sprite s = Make(px, w, h, src.pivot, src.pixelsPerUnit, "silhouette");
        silhouettes[src] = s;
        return s;
    }

    public static Sprite Outline(Sprite src)
    {
        if (src == null) return null;
        if (outlines.TryGetValue(src, out Sprite cached) && cached != null) return cached;

        bool[] solid = ReadSolid(src, out int w, out int h);
        if (solid == null) return null;

        // One pixel bigger on every side, so the ring has room around the blade tips.
        int W = w + 2, H = h + 2;

        // ⚠️ THE OUTSIDE EDGE ONLY. A star with a pierced centre would otherwise get a second little
        // ring inside the hole, which at game size reads as a stray white dot in the middle of it.
        // "Outside" = the clear pixels reachable from the border of the canvas without crossing art.
        bool[] outside = new bool[W * H];
        var open = new Queue<int>();
        for (int x = 0; x < W; x++) { Seed(outside, open, solid, w, h, W, x, 0); Seed(outside, open, solid, w, h, W, x, H - 1); }
        for (int y = 0; y < H; y++) { Seed(outside, open, solid, w, h, W, 0, y); Seed(outside, open, solid, w, h, W, W - 1, y); }
        while (open.Count > 0)
        {
            int i = open.Dequeue();
            int x = i % W, y = i / W;
            if (x > 0) Seed(outside, open, solid, w, h, W, x - 1, y);
            if (x < W - 1) Seed(outside, open, solid, w, h, W, x + 1, y);
            if (y > 0) Seed(outside, open, solid, w, h, W, x, y - 1);
            if (y < H - 1) Seed(outside, open, solid, w, h, W, x, y + 1);
        }

        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                bool ring = outside[y * W + x] && TouchesSolid(solid, w, h, x - 1, y - 1);
                px[y * W + x] = ring ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        Sprite s = Make(px, W, H, src.pivot + Vector2.one, src.pixelsPerUnit, "outline");
        outlines[src] = s;
        return s;
    }

    // ⚠️ ONE MATERIAL PER IMAGE, never one shared across them: SpriteRenderers sharing a custom
    // material can be batched into one draw carrying the FIRST one's texture (see BraceVFX, and the
    // "each other's texture" pitfall in CLAUDE.md). Sprites/Default is unlit, so a white outline stays
    // white in a dimly lit room, and it is a shader every build already includes.
    public static Material MaterialFor(Sprite s)
    {
        if (s == null) return null;
        if (materials.TryGetValue(s, out Material m) && m != null) return m;
        m = new Material(Shader.Find("Sprites/Default"));
        materials[s] = m;
        return m;
    }

    private static bool Solid(bool[] solid, int w, int h, int x, int y)
        => x >= 0 && y >= 0 && x < w && y < h && solid[y * w + x];

    // Flood-fill step on the padded canvas: (x, y) there is (x - 1, y - 1) in the art.
    private static void Seed(bool[] outside, Queue<int> open, bool[] solid, int w, int h, int W, int x, int y)
    {
        int i = y * W + x;
        if (outside[i] || Solid(solid, w, h, x - 1, y - 1)) return;
        outside[i] = true;
        open.Enqueue(i);
    }

    private static bool TouchesSolid(bool[] solid, int w, int h, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if ((dx != 0 || dy != 0) && Solid(solid, w, h, x + dx, y + dy)) return true;
        return false;
    }

    private static Sprite Make(Color32[] px, int w, int h, Vector2 pivotPixels, float ppu, string label)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Star " + label };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h),
                             new Vector2(pivotPixels.x / w, pivotPixels.y / h), ppu, 0, SpriteMeshType.FullRect);
    }

    // Which pixels of the sprite's own rectangle are opaque, bottom row first.
    private static bool[] ReadSolid(Sprite src, out int w, out int h)
    {
        Color32[] px = ReadPixels(src, out w, out h);
        if (px == null) return null;
        var solid = new bool[w * h];
        for (int i = 0; i < solid.Length; i++) solid[i] = px[i].a > 127;
        return solid;
    }

    /// <summary>
    /// The sprite's own rectangle as pixels, bottom row first, whether or not its texture was
    /// imported Read/Write. Also used by VanishVFX to repaint the decoy log's icon rim.
    /// </summary>
    public static Color32[] ReadPixels(Sprite src, out int w, out int h)
    {
        Rect r = src.textureRect;
        w = Mathf.RoundToInt(r.width);
        h = Mathf.RoundToInt(r.height);
        Texture2D tex = src.texture;
        if (tex == null || w <= 0 || h <= 0) return null;

        if (tex.isReadable)
            return Crop(tex.GetPixels32(), tex.width, Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);

        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] px = copy.GetPixels32();
        Object.Destroy(copy);
        return px;
    }

    private static Color32[] Crop(Color32[] px, int texWidth, int x0, int y0, int w, int h)
    {
        var cropped = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                cropped[y * w + x] = px[(y0 + y) * texWidth + (x0 + x)];
        return cropped;
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
