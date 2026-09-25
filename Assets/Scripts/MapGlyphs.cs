using UnityEngine;

// The map's node and recharge symbols, built procedurally and cached (house pattern — no art
// files). They live here rather than in FlatUI because FlatUI is the shared kit every screen draws
// from, and these are meaningful only on the map.
//
// THE DESIGN RULE THEY EXIST TO SERVE: one node, one icon, one promise. Difficulty IS the node
// type, so a player must be able to tell Skirmish from Fight from Elite in a glance across a whole
// act. Two things carry that, deliberately redundantly:
//
//   · SILHOUETTE — a quiet ring, a solid diamond, a spiked star. Different shapes, not different
//     colours, so the read survives the map being nearly monochrome.
//   · SIZE — small / medium / large, ascending with cost and reward. Even at a glance, and even if
//     the shapes blur together at distance, the ELITES are the big ones.
//
// Every shape is WHITE and tinted by Image.color, like everything in FlatUI, so one cached sprite
// serves the visited, available and unreachable states.
public static class MapGlyphs
{
    private static Sprite skirmish, fight, elite, boss, start;
    private static Sprite foundry, market, well;
    private static Sprite ring;

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    // A quiet hollow ring. The lightest possible mark — a Skirmish is the option you take when you
    // want the act to leave you alone.
    public static Sprite Skirmish()
    {
        if (skirmish != null) return skirmish;
        skirmish = Build(64, (dx, dy, d, ang) => Band(d, 0.62f, 0.10f));
        return skirmish;
    }

    // A solid diamond. Reads as heavier and more deliberate than the ring without being hostile.
    public static Sprite Fight()
    {
        if (fight != null) return fight;
        fight = Build(64, (dx, dy, d, ang) =>
        {
            float diamond = Mathf.Abs(dx) + Mathf.Abs(dy);
            float solid = Mathf.Clamp01((0.70f - diamond) * 7f);
            float edge = Mathf.Clamp01(1f - Mathf.Abs(diamond - 0.86f) / 0.075f);
            return Mathf.Clamp01(solid * 0.85f + edge);
        });
        return fight;
    }

    // An eight-pointed star with a hard core. Spikes read as danger at any size, which is the whole
    // job: an Elite must look uncomfortable before the player reads a single word.
    public static Sprite Elite()
    {
        if (elite != null) return elite;
        elite = Build(72, (dx, dy, d, ang) =>
        {
            if (d > 1f) return 0f;
            float axis = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 8f);
            float diag = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 8f);
            float spike = Mathf.Max(axis, diag * 0.72f);
            float reach = Mathf.Lerp(0.34f, 0.98f, spike);
            float body = Mathf.Clamp01((reach - d) * 8f);
            float core = Mathf.Clamp01((0.22f - d) * 12f);
            return Mathf.Clamp01(body + core);
        });
        return elite;
    }

    // A heavy double ring with radial ticks — the only mark on the map that looks built rather than
    // drawn, because it is the thing the whole act points at.
    public static Sprite Boss()
    {
        if (boss != null) return boss;
        boss = Build(96, (dx, dy, d, ang) =>
        {
            float outer = Band(d, 0.92f, 0.055f);
            float inner = Band(d, 0.60f, 0.085f);
            float ticks = (d > 0.66f && d < 0.88f)
                ? Mathf.Pow(Mathf.Abs(Mathf.Cos(6f * ang)), 40f) : 0f;
            float core = Mathf.Clamp01((0.26f - d) * 10f);
            return Mathf.Clamp01(outer + inner * 0.9f + ticks * 0.8f + core);
        });
        return boss;
    }

    // Ring plus a solid centre: "you started here".
    public static Sprite Start()
    {
        if (start != null) return start;
        start = Build(64, (dx, dy, d, ang) =>
        {
            float r = Band(d, 0.78f, 0.075f);
            float core = Mathf.Clamp01((0.38f - d) * 8f);
            return Mathf.Clamp01(r + core);
        });
        return start;
    }

    // ---- recharge attachment badges -------------------------------------------------------------
    // Drawn small, beside a node. They must survive at ~18px, so each is a single flat silhouette
    // with no interior detail — at that size any inner line closes up into a blob.

    // Hexagon: a fastener. The Foundry is a workbench.
    public static Sprite Foundry()
    {
        if (foundry != null) return foundry;
        foundry = Build(48, (dx, dy, d, ang) => Mathf.Clamp01((0.78f - Polygon(dx, dy, 6, 0f)) * 9f));
        return foundry;
    }

    // Triangle: the peak of a stall awning. The Market is the shop.
    public static Sprite Market()
    {
        if (market != null) return market;
        market = Build(48, (dx, dy, d, ang) => Mathf.Clamp01((0.74f - Polygon(dx, dy, 3, Mathf.PI * 0.5f)) * 9f));
        return market;
    }

    // A droplet: the Well restores Shift and health.
    public static Sprite Well()
    {
        if (well != null) return well;
        well = Build(48, (dx, dy, d, ang) =>
        {
            // Circle for the lower body, tapering to a point at the top.
            float body = Mathf.Sqrt(dx * dx + (dy + 0.18f) * (dy + 0.18f)) / 0.62f;
            float taper = Mathf.Abs(dx) / Mathf.Max(0.001f, (0.92f - dy) * 0.42f);
            float shape = dy > -0.18f ? Mathf.Max(taper, body * 0.55f) : body;
            return Mathf.Clamp01((1f - shape) * 7f);
        });
        return well;
    }

    // A plain thin ring, used for the selection halo around the committed branch.
    public static Sprite Ring()
    {
        if (ring != null) return ring;
        ring = Build(72, (dx, dy, d, ang) => Band(d, 0.88f, 0.045f));
        return ring;
    }

    // ---- engraving primitives --------------------------------------------------------------------
    // The plate is read as METAL WITH SHAPES CUT INTO IT, so a node is not a symbol sitting on a
    // surface — it is a socket punched into one. Three pieces build that, and the order matters:
    // a filled recess, then a rim drawn TWICE at small opposite offsets (dark on the lit side,
    // light on the shaded side). Reversing those two makes the socket pop OUT as a boss instead of
    // sinking in, which is the whole difference between an engraved chart and a sticker sheet.

    private static Sprite disc, socketRim, bloom;

    // Filled disc with a soft edge — the recessed floor of a node socket.
    public static Sprite Disc()
    {
        if (disc != null) return disc;
        disc = Build(72, (dx, dy, d, ang) => Mathf.Clamp01((0.94f - d) * 9f));
        return disc;
    }

    // Thicker and softer than Ring(): this is a bevelled wall catching light, not a drawn outline.
    public static Sprite SocketRim()
    {
        if (socketRim != null) return socketRim;
        socketRim = Build(72, (dx, dy, d, ang) => Band(d, 0.88f, 0.11f));
        return socketRim;
    }

    // Soft radial falloff. The acid still working at the frontier — the map's only moving thing.
    public static Sprite Bloom()
    {
        if (bloom != null) return bloom;
        bloom = Build(96, (dx, dy, d, ang) =>
        {
            float a = Mathf.Clamp01(1f - d);
            return a * a * a;   // cubed: keeps the haze tight instead of washing the whole plate
        });
        return bloom;
    }

    public static Sprite ForNode(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Skirmish: return Skirmish();
            case MapNodeType.Fight: return Fight();
            case MapNodeType.Elite: return Elite();
            // The finale shares the boss glyph but is drawn LARGER (see SizeFor) - size is half
            // the type signal on this map, so the terminus reads as the biggest thing on it.
            case MapNodeType.Boss: return Boss();
            case MapNodeType.FinalBoss: return Boss();
            default: return Start();
        }
    }

    public static Sprite ForRecharge(RechargeType type)
    {
        switch (type)
        {
            case RechargeType.Foundry: return Foundry();
            case RechargeType.Market: return Market();
            case RechargeType.Well: return Well();
            default: return null;
        }
    }

    // On-screen diameter per type. Size is half the type signal — see the header.
    public static float SizeFor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Skirmish: return 30f;
            case MapNodeType.Fight: return 40f;
            case MapNodeType.Elite: return 52f;
            case MapNodeType.Boss: return 66f;
            case MapNodeType.FinalBoss: return 88f;
            default: return 38f;
        }
    }

    public static string LabelFor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Skirmish: return "SKIRMISH";
            case MapNodeType.Fight: return "FIGHT";
            case MapNodeType.Elite: return "ELITE";
            case MapNodeType.Boss: return "BOSS";
            case MapNodeType.FinalBoss: return "THE END";
            default: return "HUB";
        }
    }

    public static string LabelFor(RechargeType type)
    {
        switch (type)
        {
            case RechargeType.Foundry: return "FOUNDRY";
            case RechargeType.Market: return "MARKET";
            case RechargeType.Well: return "WELL";
            default: return "";
        }
    }

    // ---- construction ---------------------------------------------------------------------------

    // Alpha at a normalised point. dx/dy are in -1..1, d is the radius, ang the angle.
    private delegate float Field(float dx, float dy, float d, float ang);

    private static Sprite Build(int size, Field f)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;   // Point aliases these edges badly
        tex.wrapMode = TextureWrapMode.Clamp;

        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);

                float a = Mathf.Clamp01(f(dx, dy, d, ang));
                tex.SetPixel(x, y, a <= 0f ? Clear : new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // A soft annulus at `radius` of half-width `w`.
    private static float Band(float d, float radius, float w)
    {
        return Mathf.Clamp01(1f - Mathf.Abs(d - radius) / w);
    }

    // Distance-ish field for a regular n-gon, used to cut flat-sided badge silhouettes.
    private static float Polygon(float dx, float dy, int sides, float rotation)
    {
        float ang = Mathf.Atan2(dy, dx) + rotation;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float seg = Mathf.PI * 2f / sides;
        float a = Mathf.Repeat(ang, seg) - seg * 0.5f;
        return r * Mathf.Cos(a) / Mathf.Cos(seg * 0.5f);
    }
}
