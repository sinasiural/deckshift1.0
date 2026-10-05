using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Deckshift → Audit Sound Bank
//
// The bank plays sounds; this is the part that keeps them DISTINCT. The designer's brief was "I
// don't want the player to feel like they are hearing the same stuff everywhere", and that is not
// something a data structure can enforce on its own — it needs someone to notice when it has
// stopped being true. This notices.
//
// It answers four questions, in descending order of how badly they hurt:
//
//   1. WHICH EVENTS ARE SILENT?          a slot with no clips is a thing the game never says
//   2. WHICH CLIPS ARE SHARED?           the same file on two events is how a game goes flat
//   3. WHICH EVENTS CANNOT VARY?         one variant means it is byte-identical every time
//   4. IS THE PALETTE LOPSIDED?          if everything is one family, everything sounds related
//
// ⚠️ (2) is the one worth running for on its own. Sharing a clip is invisible in the Inspector —
// you have to open both events to see it — and it is exactly how "everything sounds the same"
// happens without anyone deciding it should.
public static class SoundBankAuditor
{
    [MenuItem("Deckshift/Audit Sound Bank")]
    public static void Audit()
    {
        var bank = Resources.Load<SoundBank>("SoundBank");
        if (bank == null)
        {
            Debug.LogError("Audit Sound Bank: no Assets/Resources/SoundBank.asset. Run Deckshift → Create Sound Bank first.");
            return;
        }
        bank.Invalidate();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SOUND BANK AUDIT — " + bank.events.Count + " events ===\n");

        // ---- 1. silent + duplicate ids -------------------------------------------------------
        var silent = new List<string>();
        var seenIds = new HashSet<string>();
        var dupeIds = new List<string>();
        foreach (var e in bank.events)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.id)) { silent.Add("<blank id>"); continue; }
            if (!seenIds.Add(e.id)) dupeIds.Add(e.id);
            int n = e.variants == null ? 0 : e.variants.Count(c => c != null);
            if (n == 0) silent.Add(e.id);
        }
        sb.AppendLine("--- SILENT (" + silent.Count + ") — no clips, the game never makes this sound ---");
        foreach (var s in silent) sb.AppendLine("   " + s);
        if (dupeIds.Count > 0)
        {
            sb.AppendLine("\n--- ⚠ DUPLICATE IDS (" + dupeIds.Count + ") — only the first is reachable ---");
            foreach (var d in dupeIds) sb.AppendLine("   " + d);
        }

        // ---- 2. the same clip on more than one event ------------------------------------------
        var byClip = new Dictionary<AudioClip, List<string>>();
        foreach (var e in bank.events)
        {
            if (e == null || e.variants == null) continue;
            foreach (var c in e.variants)
            {
                if (c == null) continue;
                if (!byClip.TryGetValue(c, out var list)) byClip[c] = list = new List<string>();
                if (!list.Contains(e.id)) list.Add(e.id);
            }
        }
        var shared = byClip.Where(kv => kv.Value.Count > 1).ToList();
        sb.AppendLine("\n--- ⚠ SHARED CLIPS (" + shared.Count + ") — these events sound IDENTICAL ---");
        if (shared.Count == 0) sb.AppendLine("   none — every event has its own material");
        foreach (var kv in shared)
            sb.AppendLine("   " + kv.Key.name + "  ->  " + string.Join(" , ", kv.Value));

        // ---- 3. events that cannot vary -------------------------------------------------------
        var single = bank.events.Where(e => e != null && e.variants != null
                                            && e.variants.Count(c => c != null) == 1).ToList();
        sb.AppendLine("\n--- SINGLE-VARIANT (" + single.Count + ") — identical every time except pitch ---");
        foreach (var e in single) sb.AppendLine("   " + e.id);

        // ---- 4. palette ------------------------------------------------------------------------
        sb.AppendLine("\n--- FAMILY SPREAD ---");
        foreach (SoundFamily f in System.Enum.GetValues(typeof(SoundFamily)))
        {
            int n = bank.events.Count(e => e != null && e.family == f);
            if (n > 0) sb.AppendLine(string.Format("   {0,-8} {1,3}", f, n));
        }
        int unset = bank.events.Count(e => e != null && e.family == SoundFamily.Unset);
        if (unset > 0) sb.AppendLine("   ⚠ " + unset + " events have no family set — the palette can't be judged until they do");

        // ---- 5. audio in the project that no event uses ----------------------------------------
        var used = new HashSet<AudioClip>(byClip.Keys);
        var loose = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains("/_Unused/") || p.Contains("/Music/")) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (clip != null && !used.Contains(clip)) loose.Add(p.Replace("Assets/Audio/SFX/", ""));
        }
        loose.Sort();
        sb.AppendLine("\n--- NOT IN THE BANK (" + loose.Count + ") — files nothing plays ---");
        foreach (var l in loose) sb.AppendLine("   " + l);

        Debug.Log(sb.ToString());
    }

    // Fills the bank from what the project already has, and — just as importantly — creates the
    // events that are SILENT so the shopping list lives in the bank rather than in a document.
    //
    // ⚠️ ADDITIVE ONLY. It never edits an event that already exists, so re-running it after you have
    // tuned pitch ranges or added variants cannot undo that work. New events appear; nothing is
    // overwritten.
    [MenuItem("Deckshift/Populate Sound Bank From Project")]
    public static void Populate()
    {
        CreateBank();
        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>("Assets/Resources/SoundBank.asset");
        if (bank == null) return;

        System.Func<string, AudioClip> C = n => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/" + n);
        var existing = new HashSet<string>(bank.events.Where(e => e != null).Select(e => e.id));
        int added = 0;

        System.Action<string, SoundFamily, bool, AudioClip[]> add = (id, fam, positional, clips) =>
        {
            if (existing.Contains(id)) return;
            bank.events.Add(new SoundEvent
            {
                id = id,
                family = fam,
                positional = positional,
                variants = (clips ?? new AudioClip[0]).Where(c => c != null).ToArray(),
            });
            existing.Add(id);
            added++;
        };

        // ---- player ----------------------------------------------------------------------------
        // 2D, all of it: the player is always at the centre of the player's attention, and a
        // footstep that gets quieter because the camera drifted is just wrong.
        add("Player.Footstep", SoundFamily.Stone, false, new[] { C("Player/Footstep_1.mp3"), C("Player/Footstep_2.mp3"), C("Player/Footstep_3.mp3") });
        add("Player.Jump", SoundFamily.Magic, false, new[] { C("Player/Jump.mp3") });
        add("Player.Dash", SoundFamily.Magic, false, new[] { C("Player/Dash.mp3") });
        add("Player.Death", SoundFamily.Flesh, false, new[] { C("Player/Death.mp3") });
        add("Player.Hurt", SoundFamily.Flesh, false, new[] { C("Player/Hurt_HeavyImpact.mp3") });
        add("Player.Adrenaline", SoundFamily.Magic, false, new[] { C("Player/Adrenaline.mp3") });

        // ---- cards -----------------------------------------------------------------------------
        add("Card.Fireball", SoundFamily.Magic, false, new[] { C("Cards/Fireball.mp3") });
        add("Card.Phase", SoundFamily.Magic, false, new[] { C("Cards/Phase.mp3") });
        add("Card.CometDive", SoundFamily.Stone, false, new[] { C("Cards/CometDive.mp3") });
        add("Card.CreatePlatform", SoundFamily.Magic, false, new[] { C("Cards/CreatePlatform.mp3") });
        add("Card.GlassWail", SoundFamily.Magic, false, new[] { C("Cards/GlassWail.mp3") });
        add("Card.VampiricBite", SoundFamily.Flesh, false, new[] { C("Cards/VampiricBite.mp3") });
        add("Card.Portal", SoundFamily.Magic, false, new[] { C("Cards/Portal.mp3") });
        add("Card.Shuriken", SoundFamily.Metal, false, new[] { C("Player/AirSwish.mp3") });
        add("Card.GlassParry", SoundFamily.Magic, false, null);        // SILENT — shopping list
        add("Card.FreefallBlade", SoundFamily.Metal, false, null);     // SILENT — shopping list

        // ---- enemies ---------------------------------------------------------------------------
        add("Enemy.Melee.Swing", SoundFamily.Metal, true, new[] { C("Enemies/Melee_Sword.wav") });
        add("Enemy.Ranged.Shoot", SoundFamily.Wood, true, new[] { C("Enemies/Ranged_ArrowShot.wav") });
        add("Enemy.Slime.Attack", SoundFamily.Flesh, true, new[] { C("Enemies/Slime_Attack.wav") });
        add("Enemy.Zombie.Swing", SoundFamily.Flesh, true, null);      // SILENT — ~27 enemies
        add("Enemy.Spitter.Spit", SoundFamily.Flesh, true, null);      // SILENT — 7 enemies

        // ---- boss ------------------------------------------------------------------------------
        // Boss_Armor 1/2/3 are three takes of one sound — exactly what a variant array is for.
        add("Boss.Armor", SoundFamily.Metal, true, new[] { C("Enemies/Boss_Armor_1.wav"), C("Enemies/Boss_Armor_2.wav"), C("Enemies/Boss_Armor_3.wav") });
        add("Boss.Dash", SoundFamily.Metal, true, new[] { C("Enemies/Boss_Dash.wav") });
        add("Boss.Roar", SoundFamily.Voice, false, new[] { C("Enemies/Boss_Roar.wav") });
        add("Boss.Death", SoundFamily.Voice, false, null);             // SILENT — the run's finale
        add("Boss.Pound", SoundFamily.Stone, true, null);              // SILENT
        add("Boss.Leap", SoundFamily.Stone, true, null);               // SILENT

        // ---- world -----------------------------------------------------------------------------
        add("World.Crusher.Slam", SoundFamily.Stone, false, new[] { C("World/Crusher_Slam.wav") });
        add("World.Lever.Switch", SoundFamily.Metal, true, new[] { C("World/Lever_Switch.mp3") });
        add("World.Gate.Break", SoundFamily.Metal, true, null);        // SILENT — ProcSfx today
        add("World.Gate.Stop", SoundFamily.Stone, true, null);         // SILENT — ProcSfx today
        add("World.BreakableWall.Break", SoundFamily.Wood, true, null);// SILENT
        add("World.Altar.Pay", SoundFamily.Magic, true, null);         // SILENT
        add("World.Altar.Refuse", SoundFamily.Magic, true, null);      // SILENT

        // ---- pickups ---------------------------------------------------------------------------
        add("Pickup.Gold", SoundFamily.Metal, true, new[] { C("Pickups/Gold.mp3") });
        add("Pickup.ShiftCrystal", SoundFamily.Magic, true, new[] { C("Pickups/ShiftCrystal.wav") });
        add("Pickup.Chest", SoundFamily.Wood, true, new[] { C("Pickups/Chest_Open.mp3") });
        add("Pickup.Scrap", SoundFamily.Metal, true, null);            // SILENT — ProcSfx today
        add("Shop.Purchase", SoundFamily.Metal, false, new[] { C("Pickups/Purchase.mp3") });

        // ---- ui --------------------------------------------------------------------------------
        add("UI.CardPlay", SoundFamily.Paper, false, new[] { C("UI/CardPlay.mp3") });
        add("UI.LevelStart", SoundFamily.Magic, false, new[] { C("UI/LevelStart.mp3") });

        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        bank.Invalidate();
        Debug.Log("Populate Sound Bank: added " + added + " events (" + bank.events.Count + " total). Nothing existing was modified.");
        Audit();
    }

    // Creates the asset if it does not exist. Deliberately does NOT overwrite an existing bank —
    // that would throw away hand-tuned variation settings, which is exactly the kind of silent
    // destruction a menu item should never be able to do.
    [MenuItem("Deckshift/Create Sound Bank")]
    public static void CreateBank()
    {
        const string path = "Assets/Resources/SoundBank.asset";
        if (AssetDatabase.LoadAssetAtPath<SoundBank>(path) != null)
        {
            Debug.Log("Create Sound Bank: one already exists at " + path + " — left untouched.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SoundBank>(path);
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var bank = ScriptableObject.CreateInstance<SoundBank>();
        AssetDatabase.CreateAsset(bank, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = bank;
        Debug.Log("Create Sound Bank: created " + path);
    }
}
