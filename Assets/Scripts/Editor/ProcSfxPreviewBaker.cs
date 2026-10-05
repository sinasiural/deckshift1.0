using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **Deckshift → Bake ALL ProcSfx Previews** — renders every procedural clip to a .wav you can
/// click in the Project window and audition without entering Play mode.
///
/// ⚠️ WHY THIS REPLACES THE TWO EARLIER BAKERS. `UISfxPreviewBaker` and `JumpSfxPreviewBaker` each
/// hand-listed a subset, so of ProcSfx's 21 clips only 9 could ever be listened to — and the ones
/// you could not hear included every gate sound, every arcane sound, and the whole pause family.
/// You cannot judge audio you cannot play.
///
/// This finds the clips by REFLECTION over ProcSfx's public static AudioClip properties, so adding
/// a new one to ProcSfx makes it auditionable automatically. A hand-kept list is exactly how the
/// coverage gap happened, and this project has been bitten by hand-kept lists repeatedly (the shop's
/// relic pool, the chest's per-tier lists, the card pools).
///
/// The output folder is throwaway — nothing references it, nothing ships from it, and deleting it
/// costs nothing. The LIVE sounds are always the `ProcSfx.*` properties, never these files.
/// </summary>
public static class ProcSfxPreviewBaker
{
    private const string OutDir = "Assets/ProcSfxPreview";

    // Judging order. Sounds that fire constantly go first, because those are the ones that become
    // annoying; sounds that must be told APART from each other are grouped so you can compare them
    // back to back. Anything not listed is baked afterwards in reflection order.
    private static readonly string[] Order =
    {
        "UIMove", "UIConfirm", "UICancel", "UIRefuse", "UIOpen", "UIClose",
        "Jump", "BuildShift", "ScrapPickup",
        "GateGroan", "GateRelease", "GateRatchet", "GateSeat",
        "ArcaneGather", "ArcaneBind",
        "PauseHalt", "PauseRelease", "PauseTick",
        "PaperRustle", "WaxStamp",
        "MeteorImpact",
    };

    [MenuItem("Deckshift/Bake ALL ProcSfx Previews")]
    public static void Bake()
    {
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        // every public static AudioClip property on ProcSfx
        var found = new Dictionary<string, PropertyInfo>();
        foreach (var p in typeof(ProcSfx).GetProperties(BindingFlags.Public | BindingFlags.Static))
            if (p.PropertyType == typeof(AudioClip)) found[p.Name] = p;

        var names = new List<string>();
        foreach (var n in Order) if (found.ContainsKey(n)) names.Add(n);
        foreach (var kv in found) if (!names.Contains(kv.Key)) names.Add(kv.Key);   // anything new

        int ok = 0, failed = 0;
        for (int i = 0; i < names.Count; i++)
        {
            string n = names[i];
            AudioClip c = null;
            try { c = (AudioClip)found[n].GetValue(null, null); }
            catch (System.Exception ex) { Debug.LogWarning("[ProcSfx] " + n + " threw: " + ex.Message); failed++; continue; }
            if (c == null) { Debug.LogWarning("[ProcSfx] " + n + " built null"); failed++; continue; }

            var samples = new float[c.samples * c.channels];
            c.GetData(samples, 0);
            // numeric prefix so the Project window lists them in judging order, not alphabetically
            string file = string.Format("{0:00}_{1}.wav", i + 1, n);
            WriteWav(OutDir + "/" + file, samples, c.frequency, c.channels);
            ok++;
        }

        // ⚠️ `BuildShift(ShiftStyle)` is a parameterised METHOD, not a property, so reflection over
        // properties correctly skips it — but it is three deliberately authored alternatives for the
        // jump/shift sound and they deserve to be judged too. Nothing in the game calls it today;
        // ProcSfx.Jump is what actually plays. Kept auditionable so the choice can be revisited.
        foreach (ProcSfx.ShiftStyle style in System.Enum.GetValues(typeof(ProcSfx.ShiftStyle)))
        {
            AudioClip c = null;
            try { c = ProcSfx.BuildShift(style); }
            catch (System.Exception ex) { Debug.LogWarning("[ProcSfx] BuildShift(" + style + ") threw: " + ex.Message); failed++; continue; }
            if (c == null) { failed++; continue; }
            var s = new float[c.samples * c.channels];
            c.GetData(s, 0);
            ok++;
            WriteWav(OutDir + "/" + string.Format("{0:00}_ShiftAlt_{1}.wav", ok, style), s, c.frequency, c.channels);
        }

        AssetDatabase.Refresh();
        Debug.Log("[ProcSfx] Baked " + ok + " previews (" + failed + " failed) to " + OutDir +
                  "\nClick any .wav in the Project window to audition — no Play mode needed." +
                  "\nThey are numbered in judging order: the ones that fire constantly first, then " +
                  "the groups that have to be told apart from each other.");
    }

    /// <summary>Clears the folder. The previews are disposable; the live sounds are the code.</summary>
    [MenuItem("Deckshift/Bake ALL ProcSfx Previews", true)]
    private static bool BakeValidate() { return !Application.isPlaying; }

    // Minimal 16-bit PCM WAV writer (BinaryWriter is little-endian, which is what WAV wants).
    private static void WriteWav(string path, float[] samples, int sampleRate, int channels)
    {
        const int bits = 16;
        int blockAlign = channels * bits / 8;
        int byteRate = sampleRate * blockAlign;
        int dataSize = samples.Length * (bits / 8);

        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });

            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);            // PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)bits);

            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);
            foreach (float s in samples)
                bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
