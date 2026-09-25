using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **Deckshift → Fill Silent Audio Slots** — bakes procedural clips to real .wav assets and assigns
/// them to every NULL AudioClip field on our prefabs.
///
/// ⚠️ THIS IS WHERE THE GAME'S SILENCE ACTUALLY LIVED. Measured 2026-08-21: 75 AudioClip fields
/// across our 172 prefabs were null, and `SfxManager.PlayOn(source, clip)` with a null clip is a
/// SILENT NO-OP — no warning, no error, nothing in the console. So every melee enemy swung without
/// a sound (15 prefabs), no breakable wall made a noise (13), no spitter spat (8), and no Shift
/// Altar answered you (12). None of it was a bug in the audio code; the slots were simply empty.
///
/// ⚠️ AND IT IS NOT THE SOUNDBANK. `Sfx.Play` has one call site in the whole project while 68 sites
/// still use SfxManager, so filling the bank's 13 silent events would have built a library nothing
/// reads. Slots first, architecture second.
///
/// ⚠️ THE CLIPS ARE BAKED TO ASSETS, NOT GENERATED AT RUNTIME. A prefab's AudioClip field is a
/// serialized reference — it cannot point at something ProcSfx builds in memory. Baking also makes
/// every sound auditionable in the Project window and removes the generation cost from load.
///
/// Idempotent: only ever writes to slots that are null, so re-running it can never overwrite a sound
/// somebody chose by hand.
/// </summary>
public static class SilentSlotFiller
{
    private const string OutDir = "Assets/Audio/Procedural";

    /// <summary>
    /// field name -> the ProcSfx property that should fill it.
    ///
    /// ⚠️ KEYED ON THE FIELD NAME ALONE, not Type.field, deliberately — `attackSound` means the same
    /// thing on every AI that has one, and a per-type table would silently miss the next enemy added.
    /// Anything not listed here is left alone and reported, so the gap stays visible.
    /// </summary>
    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        { "attackSound",  "ZombieSwing" },
        { "spitSound",    "SpitterSpit" },
        { "breakSound",   "WallBreak" },
        { "paySound",     "AltarPay" },
        { "refuseSound",  "AltarRefuse" },
        { "collectSound", "CrystalCollect" },
        { "glassParrySound",    "GlassParry" },
        { "freefallBladeSound", "FreefallBlade" },

        // ⚠️ TYPE-QUALIFIED, the exception to the rule above. `deathSound` and `leapSound` are
        // generic names that also exist on the player and would on any future enemy — keyed on the
        // field alone, the next small enemy with an empty death slot would die with a boss's
        // collapse and toll. These are checked BEFORE the bare field name.
        { "MossKnightBoss.deathSound", "BossDeath" },
        { "MossKnightBoss.poundSound", "BossStomp" },
        { "MossKnightBoss.leapSound",  "BossLeap" },
        { "NinjaBoss.deathSound",      "BossDeath" },
        { "KagemushaBoss.deathSound",  "BossDeath" },
    };

    [MenuItem("Deckshift/Fill Silent Audio Slots")]
    public static void Fill()
    {
        Directory.CreateDirectory(OutDir);

        // Bake once, reuse for every slot.
        var baked = new Dictionary<string, AudioClip>();
        foreach (var kv in Map)
        {
            if (baked.ContainsKey(kv.Value)) continue;
            AudioClip clip = FromProcSfx(kv.Value);
            if (clip == null) { Debug.LogWarning("[SilentSlots] ProcSfx." + kv.Value + " missing"); continue; }
            baked[kv.Value] = WriteWav(clip, kv.Value);
        }
        AssetDatabase.Refresh();
        foreach (var k in new List<string>(baked.Keys))
            baked[k] = AssetDatabase.LoadAssetAtPath<AudioClip>(OutDir + "/" + k + ".wav");

        int filled = 0, prefabsTouched = 0;
        var unmapped = new Dictionary<string, int>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Cainos/")) continue;      // pack demo content, not ours

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;
            bool dirty = false;

            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;

                // ⚠️ NEVER WRITE INTO A NESTED PREFAB INSTANCE. Level prefabs contain the enemies and
                // props as instances of their source prefabs, so filling a slot here does not create
                // a sound — it creates a PINNED OVERRIDE that freezes that instance at today's clip
                // and stops it following the source prefab forever after. This project has a
                // dedicated auditor (Deckshift → Audit Prefab Overrides) precisely because that class
                // of drift is invisible until something mysteriously stops updating.
                //
                // The first run of this tool did exactly that to 13 level prefabs before it was
                // caught. Fill the SOURCE; the instances inherit for free.
                //
                // ⚠️ TEST THE COMPONENT, NOT ITS GAMEOBJECT. A prefab VARIANT's root is always "part of
                // an instance" (of its base), so testing the GameObject skipped every component a
                // variant ADDS — which is where the variant's own values live and the only place they
                // can be filled. That is how the Moss Knight stayed silent through the first run of
                // this tool: MossKnightBoss is added onto a variant of the Cainos knight. An added
                // component is not part of the instance, so this test fills it and still skips every
                // component that is inherited from another prefab.
                if (PrefabUtility.IsPartOfPrefabInstance(mb)) continue;

                foreach (FieldInfo f in mb.GetType().GetFields(BindingFlags.Public |
                                                               BindingFlags.NonPublic |
                                                               BindingFlags.Instance))
                {
                    if (f.FieldType != typeof(AudioClip)) continue;
                    if (!f.IsPublic && !System.Attribute.IsDefined(f, typeof(SerializeField))) continue;
                    if (f.GetValue(mb) as AudioClip != null) continue;   // never overwrite a real choice

                    string key;
                    string label = mb.GetType().Name + "." + f.Name;
                    if (!Map.TryGetValue(label, out key) && !Map.TryGetValue(f.Name, out key))
                    {
                        unmapped[label] = (unmapped.ContainsKey(label) ? unmapped[label] : 0) + 1;
                        continue;
                    }

                    AudioClip clip;
                    if (!baked.TryGetValue(key, out clip) || clip == null) continue;

                    f.SetValue(mb, clip);
                    EditorUtility.SetDirty(mb);
                    filled++; dirty = true;
                }
            }

            if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabsTouched++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();

        var report = new System.Text.StringBuilder();
        report.Append("[SilentSlots] filled ").Append(filled).Append(" slots across ")
              .Append(prefabsTouched).Append(" prefabs.\nStill unmapped (need a sound authored):\n");
        var list = new List<KeyValuePair<string, int>>(unmapped);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        foreach (var kv in list) report.Append("  ").Append(kv.Value).Append("  ").Append(kv.Key).Append("\n");
        Debug.Log(report.ToString());
    }

    private static AudioClip FromProcSfx(string property)
    {
        PropertyInfo p = typeof(ProcSfx).GetProperty(property, BindingFlags.Public | BindingFlags.Static);
        if (p == null || p.PropertyType != typeof(AudioClip)) return null;
        return (AudioClip)p.GetValue(null, null);
    }

    // Minimal 16-bit PCM WAV. BinaryWriter is little-endian, which is what WAV wants.
    private static AudioClip WriteWav(AudioClip clip, string name)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        string path = OutDir + "/" + name + ".wav";
        const int bits = 16;
        int blockAlign = clip.channels * bits / 8;
        int dataSize = samples.Length * (bits / 8);

        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)clip.channels);
            bw.Write(clip.frequency);
            bw.Write(clip.frequency * blockAlign);
            bw.Write((short)blockAlign);
            bw.Write((short)bits);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);
            foreach (float s in samples) bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
        return null;   // reloaded from disk by the caller after a Refresh
    }
}
