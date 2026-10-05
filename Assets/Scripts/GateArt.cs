using UnityEngine;

// The gate's art, cut into its moving parts, plus the geometry needed to reassemble it exactly.
// Written by Editor/GateArtBaker (Deckshift -> Bake Gate Art) and loaded once by Gate.cs.
//
// This exists so the offsets are BAKED ALONGSIDE the sprites rather than hardcoded in Gate.cs.
// The leaves are cropped to their own bounds (so they can pivot on their hinges), which means
// their placement depends on where the cut landed - and that is decided by the baker reading the
// artwork. Hardcoding it would silently go wrong the moment the gate art is re-cut, and the
// failure would be a door hanging a few pixels out of its frame, which is easy to miss.
public class GateArt : ScriptableObject
{
    [Header("Pieces")]
    public Sprite arch;        // masonry only, opening punched out. Pivot matches the original sprite.
    public Sprite passage;     // dark fill of the opening, same pivot as arch
    public Sprite leafL;       // left door leaf,  pivoted on its LEFT edge (hinge)
    public Sprite leafR;       // right door leaf, pivoted on its RIGHT edge (hinge)

    [Header("Placement (local units, relative to the arch's pivot)")]
    public Vector2 leafLOffset;
    public Vector2 leafROffset;

    [Header("Opening extents (local units, relative to the arch's pivot)")]
    public float openingBottom;
    public float openingTop;
    public float openingLeft;
    public float openingRight;

    // Full drawn size of the arch sprite in local units - Gate.cs scales by this to fit its collider.
    public float archWidth;
    public float archHeight;
}
