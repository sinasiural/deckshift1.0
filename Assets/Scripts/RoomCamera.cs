using UnityEngine;

/// <summary>
/// Optional marker on a ROOM PREFAB ROOT (same convention as HubMarker and RoomTier):
/// overrides the main camera's orthographic size for as long as that room is the current one.
///
/// Built for the Ninja boss arena, which is designed to be seen WHOLE at a fixed size —
/// that boss teleports, and a camera framed for an ordinary room loses him mid-blink.
/// See BossDesign_Ninja.md §8.
///
/// Leave the value at 0 (or omit the component entirely) and the room uses the camera's
/// authored default. LevelManager pushes this on EVERY room spawn, so a room that overrides
/// the size cannot leak that size into the rooms after it.
/// </summary>
public class RoomCamera : MonoBehaviour
{
    [Tooltip("Orthographic HALF-HEIGHT to use in this room. The camera is height-anchored, so this " +
             "is half the world units visible vertically; width follows from the aspect ratio. " +
             "0 or less = leave the camera at its default (7).")]
    public float orthographicSize = 0f;
}
