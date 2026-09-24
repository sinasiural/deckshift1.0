using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow instance;

    [Header("Takip Ayarları")]
    public Transform target;
    public Vector2 offset;

    [Header("Cinematic Focus")]
    [Tooltip("Seconds for the camera to ease onto / off a focus target (boss awaken, etc.).")]
    public float focusSmoothTime = 0.28f;

    [Header("HUD")]
    [Tooltip("Height of the hand rail in canvas pixels (of 1080). The camera composes the room ABOVE " +
             "this strip: a locked arena is centred in the space above it, and a scrolling room can " +
             "sink this much lower, so a floor never ends up under the cards.")]
    public float handRailPx = 170f;

    private BoxCollider2D[] zones;
    private BoxCollider2D activeZone;

    // Camera.main is a tag lookup on every call and this runs in LateUpdate — cache it.
    private Camera cam;

    // The size authored on the camera, captured ONCE in Awake. Never re-read from the live
    // camera: doing that would let a room's override become the "default" the next room
    // restores to, and the zoom would ratchet across the run instead of resetting.
    private float defaultOrthoSize;

    // Cinematic focus: temporarily pan toward another transform (e.g. the boss on awaken),
    // then release back to the player. Applied as a post-clamp offset like shake/peek so it
    // reads as a smooth pan without disturbing normal follow state.
    private Transform focusTarget;
    private float focusWeight;         // 0 = on player, 1 = fully on focusTarget
    private float focusTargetWeight;   // where focusWeight is easing toward
    private float focusVel;            // SmoothDamp velocity

    private void Awake()
    {
        instance = this;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        defaultOrthoSize = cam != null ? cam.orthographicSize : 7f;
    }

    /// <summary>
    /// Set the camera's orthographic size for the current room. Pass 0 or less to restore the
    /// size authored on the camera. Called by LevelManager on EVERY room spawn — including rooms
    /// that don't override it, which is what stops an override leaking into later rooms.
    /// Applied instantly, not eased: room changes are hard cuts, so a zoom afterwards reads as a glitch.
    /// </summary>
    public void SetRoomSize(float size)
    {
        if (cam == null) return;
        cam.orthographicSize = size > 0f ? size : defaultOrthoSize;
    }

    // Pan the camera onto a target (holds until ReleaseFocus). Null-safe to call.
    public void FocusOn(Transform t)
    {
        focusTarget = t;
        focusTargetWeight = 1f;
    }

    // Ease the camera back to the player.
    public void ReleaseFocus()
    {
        focusTargetWeight = 0f;
    }

    public void SetZones(BoxCollider2D[] newZones)
    {
        zones = newZones;
        activeZone = null;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        UpdateActiveZone();

        Vector3 pos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        if (activeZone != null && cam != null)
        {
            Bounds b = activeZone.bounds;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            // ⚠️ THE HAND RAIL IS PART OF THE FRAME. The cards stand permanently across the bottom
            // of the screen, so the bottom `handRailPx` of the view is not really view. Both boss
            // arenas had their zone starting exactly at the floor (zone min 5, floor 5, with five
            // units of art below it unused), so the locked camera centred a 15-tall arena in a
            // 20-tall view and put the whole fight — every foot on the floor — under the cards.
            // Designer, 2026-09-17: "the hand is basically covering up the whole ground area."
            //
            // So a LOCKED arena is centred in the space above the rail — the zone is treated as
            // extending downward by the rail's height in world units, scaled from the live
            // orthographic size so it is right at 7 and at a boss room's 10.
            //
            // ⚠️ SCROLLING ROOMS ARE DELIBERATELY LEFT ALONE. Tried extending the clamp too, and
            // every generated room grew a wider black band along the bottom: the importer pads
            // the zone 2 units past the art on all sides, and letting the camera sink further
            // just showed more of nothing. There the floor-to-zone margin is the room author's
            // to set (pad the zone below the lowest floor by at least the rail's height; the
            // importer's 2 already clears it at size 7). Here the camera has no freedom, so the
            // composition is this code's job.
            float reserve = 2f * halfH * (handRailPx / 1080f);
            float zoneMinY = b.min.y - reserve;
            bool lockedY = (b.max.y - zoneMinY) <= halfH * 2f;

            // A zone SMALLER than the view cannot be clamped into: min would exceed max, and
            // Mathf.Clamp with min > max silently returns min — snapping the camera to one edge
            // and showing undressed space outside the room. Centre on the zone instead, which is
            // the honest framing for a room smaller than the screen AND is exactly what gives a
            // boss arena its locked camera (see RoomCamera / BossDesign_Ninja.md §8).
            pos.x = (b.size.x <= halfW * 2f) ? b.center.x : Mathf.Clamp(pos.x, b.min.x + halfW, b.max.x - halfW);
            pos.y = lockedY ? (zoneMinY + b.max.y) * 0.5f : Mathf.Clamp(pos.y, b.min.y + halfH, b.max.y - halfH);
        }

        // Shake and peek offsets applied after clamping so zone edges don't suppress them
        Vector3 shakeOffset = CameraShake.instance != null ? (Vector3)CameraShake.instance.shakeOffset : Vector3.zero;
        Vector3 peekOffset = CameraPeek.instance != null ? (Vector3)CameraPeek.instance.peekOffset : Vector3.zero;

        // Cinematic focus offset (unscaled so it pans through hit-stop freezes).
        focusWeight = Mathf.SmoothDamp(focusWeight, focusTargetWeight, ref focusVel,
                                       Mathf.Max(0.01f, focusSmoothTime), Mathf.Infinity, Time.unscaledDeltaTime);
        Vector3 focusOffset = Vector3.zero;
        if (focusTarget != null && focusWeight > 0.001f)
            focusOffset = (Vector3)(Vector2)(focusTarget.position - target.position) * focusWeight;

        transform.position = pos + shakeOffset + peekOffset + focusOffset;
    }

    private void UpdateActiveZone()
    {
        if (zones == null || zones.Length == 0) return;

        if (activeZone != null && activeZone.OverlapPoint(target.position))
            return;

        foreach (var zone in zones)
        {
            if (zone.OverlapPoint(target.position))
            {
                activeZone = zone;
                return;
            }
        }

        float minDist = float.MaxValue;
        foreach (var zone in zones)
        {
            Vector2 closest = zone.ClosestPoint(target.position);
            float dist = Vector2.Distance(closest, target.position);
            if (dist < minDist)
            {
                minDist = dist;
                activeZone = zone;
            }
        }
    }
}