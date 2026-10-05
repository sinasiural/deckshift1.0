using UnityEngine;

/// <summary>
/// The tuning for Hard (Elite) map nodes, in one Inspector asset:
/// <c>Assets/Resources/HardRooms.asset</c>. Read by <see cref="ShiftInfusion"/> when a Hard node's
/// room spawns.
///
/// Design (designer-approved 2026-10-04, from the run-map notes in CLAUDE.md): a Hard room must be
/// worth choosing. A share of its enemies are SHIFT-INFUSED: tougher, visibly glowing, and they drop
/// Shift when killed. Target: an average player comes out of a Hard room behind on Shift, a good one
/// ahead. <see cref="shiftDrop"/> is the single most sensitive number in the tier; keep it here, never
/// baked into prefabs.
///
/// A ScriptableObject in Resources rather than fields on LevelManager, so tuning it never means
/// saving SampleScene (whose room list has been wiped five times).
/// </summary>
[CreateAssetMenu(menuName = "Deckshift/Hard Room Settings", fileName = "HardRooms")]
public class HardRoomSettings : ScriptableObject
{
    [Tooltip("Share of a Hard room's enemies that are infused. 0.34 is about a third.")]
    [Range(0f, 1f)] public float infusedShare = 0.34f;

    [Tooltip("At least this many are infused, if the room has that many enemies.")]
    [Min(0)] public int infusedMinimum = 2;

    [Tooltip("An infused enemy's health is multiplied by this. Scrap follows health, so it pays more scrap too.")]
    [Min(1f)] public float healthMultiplier = 1.5f;

    [Tooltip("Shift crystals an infused enemy drops when killed (1 Shift each). The most sensitive number in " +
             "the Hard tier: an average player should leave a Hard room behind on Shift, a good one ahead.")]
    [Min(0)] public int shiftDrop = 3;

    [Tooltip("The Shift crystal pickup to drop. Assign Prefabs/ShiftCrystal.")]
    public GameObject shiftCrystalPrefab;

    private static HardRoomSettings cached;
    private static bool warned;

    /// <summary>The asset in Resources, or a default instance (with a one-time warning) if it is missing.</summary>
    public static HardRoomSettings Get()
    {
        if (cached != null) return cached;
        cached = Resources.Load<HardRoomSettings>("HardRooms");
        if (cached == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("HardRoomSettings: no Resources/HardRooms.asset — using defaults, and infused " +
                                 "enemies will drop no crystals because no crystal prefab is assigned.");
            }
            cached = CreateInstance<HardRoomSettings>();
        }
        return cached;
    }
}
