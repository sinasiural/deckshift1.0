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
// UNTAGGED ROOMS ARE ELIGIBLE FOR EVERY TIER. That is deliberate: the 7 existing rooms predate
// this system, and requiring all of them to be tagged before the map works would mean the map is
// broken until a chore is finished. Tagging a room narrows it; not tagging it costs nothing. As
// rooms get tagged the act's difficulty curve sharpens on its own.
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
