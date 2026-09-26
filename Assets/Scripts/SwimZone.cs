using UnityEngine;
using Cainos.InteractivePixelWater;

// Marker + trigger that makes the water it sits on swimmable.
// Place this on a Pixel Water prefab (Clear / Normal) so the player can swim in it.
// Hazard waters (Acid, Lava, Poison) use HazardZone instead and are NOT swimmable.
// Relies on the water's own trigger BoxCollider2D, so no extra collider is needed.
//
// It also adapts the Cainos water to this game at Start (2026-09-26, designer: water "does not
// work great" — the swimmer did not look IN the water, and could not get out of it):
//
//   1. ⚠️ THE WATER IS MOVED IN FRONT OF THE ACTORS. It sat behind them in depth, so the rig drew
//      over the water and a swimmer looked like they were hovering in front of the pool. It now
//      sits just in front of PlayPlane.Z, drawn after the world sprites with a copy of the water
//      shader that does not write depth (Assets/Shaders/PixelWaterOverlay.shader) — so it tints
//      everything inside the pool, the player included, and hides nothing drawn after it.
//   2. ⚠️ THE WATER'S OWN PHYSICS NO LONGER TOUCHES THE PLAYER. Its drag (PixelWater.dragEnabled)
//      slowed every body in it, which is what ate the jump out of the water (measured: 0.4 tiles
//      above the surface instead of ~2), and its BuoyancyEffector2D would launch the player the
//      moment gravity comes back while they still overlap the water. Swimming is PlayerController's
//      job — speed, settling, treading water and the jump out all live there.
[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(100)]   // after PixelWater.Start, which resets the buoyancy mask and material
public class SwimZone : MonoBehaviour
{
    [Tooltip("The depth-free copy of the water shader. Assigned on the water prefab so it is included in builds.")]
    [SerializeField] private Shader overlayShader;

    // Drawn after every world sprite (they sit at Default order 0-2), before bright spell effects,
    // which should stay vivid over the water rather than be tinted by it.
    private const int OverlaySortingOrder = 10;

    private Collider2D zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        var water = GetComponent<PixelWater>();
        if (water == null) return;

        // 1. In front of the actors, drawn after them, writing no depth.
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, PlayPlane.Z - 0.5f);
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = OverlaySortingOrder;
        Shader shader = overlayShader != null ? overlayShader : Shader.Find("Deckshift/Pixel Water Overlay");
        if (shader != null && water.WaterMaterial != null) water.WaterMaterial.shader = shader;
        else Debug.LogWarning("[SwimZone] overlay water shader missing — the swimmer will draw in front of the water.", this);

        // 2. Hands off the player: no drag, no buoyancy.
        //
        // ⚠️ Buoyancy is switched off by DENSITY, never by taking the player out of the effector's
        // colliderMask. The water's trigger is `usedByEffector`, and a collider used by an effector
        // ignores every layer outside the effector's mask ENTIRELY — trigger callbacks included. The
        // first version of this did exactly that and the player fell through the pool to the floor
        // without ever starting to swim (caught by the swim probe, 2026-09-26).
        water.dragEnabled = false;
        var buoyancy = GetComponent<BuoyancyEffector2D>();
        if (buoyancy != null) buoyancy.density = 0f;
    }

    // World-space Y of the water surface (top edge of the water collider).
    public float SurfaceY => zoneCollider != null ? zoneCollider.bounds.max.y : transform.position.y;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) player.EnterWater(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null) player.ExitWater(this);
    }
}
