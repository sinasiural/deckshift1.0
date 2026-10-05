using System.Collections.Generic;
using UnityEngine;

// Every sound the game can make, in one asset.
//
// ── WHY THIS EXISTS ───────────────────────────────────────────────────────────────────────────
// Before it, a sound was an `[SerializeField] AudioClip` on whichever component happened to need
// it. Three things followed from that, and all three are the designer's complaint that the game
// "sounds like the same stuff everywhere":
//
//   · ONE CLIP PER EVENT. A single file played identically every time. The only place in the whole
//     project that had variants was the player's three footsteps.
//   · NO PITCH VARIATION AT ALL — not by choice. `AudioSource.PlayClipAtPoint` gives you no way to
//     set pitch, and it was what everything used. Every sword swing was byte-identical.
//   · 103 EMPTY SLOTS, because wiring a sound meant finding the prefab and remembering to drag.
//
// A bank fixes the shape of the problem rather than the instances: an event is a NAME plus a set
// of variants and a behaviour, code asks for the name, and adding a sound is dropping files in and
// filling a row — no code, no prefab hunting.
//
// ⚠️ **Run `Deckshift → Audit Sound Bank` after editing.** It is the part that actually serves
// "nothing should sound the same": it reports events with only one variant, events that are silent,
// and — most usefully — **the same clip wired to two different events**, which is how a game ends up
// sounding flat without anyone noticing.
public class SoundBank : ScriptableObject
{
    public List<SoundEvent> events = new List<SoundEvent>();

    private Dictionary<string, SoundEvent> lookup;

    public SoundEvent Find(string id)
    {
        if (lookup == null || lookup.Count != events.Count)
        {
            lookup = new Dictionary<string, SoundEvent>(events.Count);
            foreach (var e in events)
                if (e != null && !string.IsNullOrEmpty(e.id)) lookup[e.id] = e;
        }
        SoundEvent found;
        return lookup.TryGetValue(id, out found) ? found : null;
    }

    // Drops the cache so the editor picks up edits without a domain reload.
    public void Invalidate() { lookup = null; }
}

// The materials the game's audio is allowed to be made of.
//
// ⚠️ THIS IS NOT DECORATION — it is the discipline that keeps sounds distinct. It comes straight
// from ProcSfx's own design, which separates families by PHYSICS rather than by taste: magic is
// harmonic (bell partials), metal is inharmonic (bar modes), stone is noise plus sub, paper has no
// pitched component at all. Two events in different families cannot be confused even at a glance;
// two events in the same family need real work to tell apart. The auditor reports the distribution
// so you can see when everything has quietly drifted into one.
public enum SoundFamily
{
    Unset,
    Magic,      // harmonic — cards, blessings, arcane
    Metal,      // inharmonic bar modes — blades, armour, gates, levers
    Stone,      // noise + sub — impacts, rubble, crushers
    Flesh,      // wet and low — slimes, bites, bodies
    Wood,       // dry cracks — doors, breakables, chests
    Paper,      // no pitched component — cards, contracts
    Voice,      // the planned boss mumbles
    UI          // defined by pitch MOTION rather than material
}

[System.Serializable]
public class SoundEvent
{
    [Tooltip("What code asks for. Dot-grouped, e.g. Enemy.Zombie.Swing")]
    public string id;

    [Tooltip("What this sound is MADE OF. Keeps the palette honest — see SoundFamily.")]
    public SoundFamily family = SoundFamily.Unset;

    [Tooltip("Variants. Two or three rough takes beat one perfect file — see the header.")]
    public AudioClip[] variants = new AudioClip[0];

    [Header("Variation")]
    [Tooltip("Random volume range. Keep the spread small; this is texture, not dynamics.")]
    public Vector2 volume = new Vector2(0.92f, 1f);
    [Tooltip("Random pitch range. ±6% is enough to stop repetition being audible.")]
    public Vector2 pitch = new Vector2(0.94f, 1.06f);

    [Header("Placement")]
    [Tooltip("On = positional, falls off with distance. Off = 2D, always equally loud. " +
             "Anything the player must hear regardless of where it happened should be 2D.")]
    public bool positional = true;

    [Header("Crowd control")]
    [Tooltip("Refuse to retrigger faster than this. Stops a burst becoming a buzz.")]
    public float minInterval = 0.035f;
    [Tooltip("Each instance already sounding within StackWindow multiplies volume by this. " +
             "1 = off. This is what stops eight scrap shards being a wall of noise.")]
    [Range(0.3f, 1f)] public float stackDuck = 0.72f;

    // ---- runtime state, not serialized ----
    [System.NonSerialized] public float lastPlayed = -999f;
    [System.NonSerialized] public List<float> recent;
    [System.NonSerialized] public List<int> bag;

    // ⚠️ A SHUFFLE BAG, NOT `Random.Range`. With two or three variants plain randomness repeats the
    // same one back-to-back constantly, and a sound repeating immediately is the single most audible
    // flaw in game audio — it is what makes a set of clips read as "one sound" no matter how many
    // you recorded. The project already learned this once with the shopkeeper's barks. A bag plays
    // every variant before any repeats, and the refill is biased so a new bag cannot open with the
    // clip the old one closed on.
    public AudioClip NextVariant()
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];

        if (bag == null) bag = new List<int>();
        if (bag.Count == 0)
        {
            int last = lastIndex;
            for (int i = 0; i < variants.Length; i++) bag.Add(i);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int t = bag[i]; bag[i] = bag[j]; bag[j] = t;
            }
            // don't let the fresh bag start on the clip we just played
            if (bag.Count > 1 && bag[bag.Count - 1] == last)
            {
                int t = bag[bag.Count - 1]; bag[bag.Count - 1] = bag[0]; bag[0] = t;
            }
        }

        int idx = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        lastIndex = idx;
        return variants[Mathf.Clamp(idx, 0, variants.Length - 1)];
    }

    [System.NonSerialized] private int lastIndex = -1;
}
