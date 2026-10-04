using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hard (Elite) map nodes: picks which of a freshly spawned room's enemies become Shift-infused.
/// Called by LevelManager.SpawnNextRoom when the node the player chose is Hard. The tuning lives in
/// <see cref="HardRoomSettings"/>; the per-enemy look and the drop live in <see cref="ShiftInfused"/>.
///
/// ⚠️ IT KEYS OFF THE NODE THE PLAYER CHOSE, NOT THE ROOM'S TAG. The map's promise is what decides
/// it: a Hard node is Hard even if, one day, a room serves more than one tier.
///
/// ⚠️ CALL IT IN THE SAME FRAME THE ROOM IS INSTANTIATED, before any EnemyHealth.Start has run.
/// EnemyHealth caches each enemy's colours in Start (to restore them after the hit flash and the
/// stun), so a tint applied before Start is the one the enemy keeps; applied after, the first hit
/// would wash it out.
///
/// Never infused: bosses (IBossFight; their fights are tuned as a whole) and the Mimic (a glow
/// would give its disguise away).
/// </summary>
public static class ShiftInfusion
{
    /// <summary>Infuse a share of the room's enemies. Returns how many were infused.</summary>
    public static int InfuseRoom(GameObject room)
    {
        if (room == null) return 0;
        HardRoomSettings s = HardRoomSettings.Get();

        var candidates = new List<EnemyHealth>();
        foreach (EnemyHealth e in room.GetComponentsInChildren<EnemyHealth>(false))
        {
            if (e.GetComponent<IBossFight>() != null) continue;
            if (e.GetComponent<MimicAI>() != null) continue;
            if (e.GetComponent<ShiftInfused>() != null) continue;
            candidates.Add(e);
        }
        if (candidates.Count == 0) return 0;

        int want = Mathf.Max(s.infusedMinimum, Mathf.RoundToInt(candidates.Count * s.infusedShare));
        want = Mathf.Clamp(want, 0, candidates.Count);

        // A plain shuffle: which enemies glow should differ from visit to visit.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            EnemyHealth t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
        }

        for (int i = 0; i < want; i++)
            candidates[i].gameObject.AddComponent<ShiftInfused>().Begin(s.healthMultiplier, s.shiftDrop, s.shiftCrystalPrefab);

        RunStats.Note("Hard room: " + want + " of " + candidates.Count + " enemies Shift-infused");
        return want;
    }
}
