using UnityEngine;

// Declares which map node type a room prefab is built to serve. Put it on the room prefab's ROOT,
// the same way HubMarker marks the hub.
//
// WHY IT LIVES ON THE PREFAB rather than in a list on LevelManager: a room's tier is a property of
// its LAYOUT, not of how it happens to be slotted. Platforming difficulty is authored into the
// geometry and cannot change at runtime without violating Level Design Law #1 (every level must be
// completable with only jumping and moving), so a room IS a Skirmish or IS an Elite. Keeping that
// on the prefab means it travels with the room, survives reordering, and can't drift out of sync
// with a parallel list.
//
// UNTAGGED ROOMS ARE ELIGIBLE FOR EVERY TIER (at half the weight of a tagged room). That was
// deliberate while the 7 original rooms predated this system. ⚠️ As of 2026-10-04 EVERY pool room
// is tagged (designer-approved: efeslevel1 + EfeVrl4 Easy, the other five originals Medium), because
// untagged rooms answered about 4 in 10 Hard nodes and made the label a coin flip. Tag new rooms.
//
// Only the three COMBAT tiers are meaningful here. Start is the hub (HubMarker) and Boss is
// LevelManager's own bossRoomPrefab slot, so neither is chosen by tier.
public class RoomTier : MonoBehaviour
{
    [Tooltip("Which node type this room is built for (the map calls them Easy / Medium / Hard). " +
             "Skirmish (Easy) = simple layout, thin loot. Fight (Medium) = harder layout, at least one chest. " +
             "Elite (Hard) = hardest layouts, uncomfortable to pick.")]
    public MapNodeType tier = MapNodeType.Skirmish;

    public bool Serves(MapNodeType nodeType)
    {
        return tier == nodeType;
    }
}
