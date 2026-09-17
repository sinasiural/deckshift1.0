using UnityEngine;
using Cainos.CustomizablePixelCharacter;

// One live, animated character rendered into a RenderTexture so the select screen can show it as a
// UI image.
//
// ⚠️ WHY A RENDER TEXTURE AND NOT JUST WORLD OBJECTS. The menu's Canvas is Screen Space - Overlay,
// which always draws on top of everything in the world — so a character placed in the scene would
// sit BEHIND the screen's own backdrop and never be seen. Rendering to a texture puts the character
// inside the UI stack, where it can be layered, tinted and dimmed like any other graphic.
//
// ⚠️ AND ONE TEXTURE PER CHARACTER, not one shared stage. It costs a few tiny cameras and buys two
// things worth more than that: each alcove positions its own image with no viewport maths, and
// dimming an unselected character is a plain `RawImage.color` tint instead of reaching into the
// Cainos shaders — which is exactly the "set a property that doesn't exist and it silently does
// nothing" trap that has cost this project months before.
public class CharacterStagePortrait : MonoBehaviour
{
    public RenderTexture Texture { get; private set; }

    private Animator animator;
    private Camera cam;

    // Stage plots are far from the world and far from EACH OTHER — the cameras all cull to the same
    // layer, so neighbours would otherwise wander into frame.
    private const float PLOT_SPACING = 60f;
    private static readonly Vector3 StageOrigin = new Vector3(3000f, -3000f, 0f);

    // ── Framing ─────────────────────────────────────────────────────────────────────────────────
    // Public because the select screen has to STAND the character on something, and the only way to
    // know where the feet land inside the texture is to know how the camera is framed.
    //
    // ⚠️ These are consts rather than numbers typed into `BuildCamera`, so `FeetFraction` cannot go
    // stale. The screen positions every figure by that fraction; a camera re-frame that left it
    // behind would silently sink the whole roster into the floor or float it above the line.
    public const float CAM_Y = 1.05f;      // the rig's origin is at its FEET, so look above it
    public const float CAM_SIZE = 1.35f;   // orthographic half-height, in world units

    /// <summary>Where world y = 0 (the character's feet) falls inside the texture. 0 = bottom edge.</summary>
    public static float FeetFraction { get { return (0f - (CAM_Y - CAM_SIZE)) / (CAM_SIZE * 2f); } }

    public static CharacterStagePortrait Create(CharacterData character, int index, int width, int height)
    {
        if (character == null || character.appearancePreset == null) return null;

        int layer = LayerMask.NameToLayer("CharacterStage");
        if (layer < 0) layer = 0;

        var root = new GameObject("Portrait_" + character.characterName);
        root.transform.position = StageOrigin + new Vector3(index * PLOT_SPACING, 0f, 0f);

        var p = root.AddComponent<CharacterStagePortrait>();
        p.BuildRig(character, root.transform, layer);
        p.BuildCamera(root.transform, layer, width, height);
        return p;
    }

    private void BuildRig(CharacterData character, Transform parent, int layer)
    {
        GameObject rig = Instantiate(character.appearancePreset, parent);
        rig.transform.localPosition = Vector3.zero;
        rig.name = "Rig";

        // ⚠️ Strip the pack's own driving components. The preset ships as a PLAYABLE character with
        // a controller, an input reader and physics — left on, it reads the keyboard and walks out
        // of frame while the player is trying to look at it.
        StripDrivers(rig);
        SetLayerRecursive(rig.transform, layer);

        PixelCharacter pc = rig.GetComponentInChildren<PixelCharacter>(true);
        if (pc != null && character.weaponPrefab != null) pc.AddWeapon(character.weaponPrefab, true);
        // The same two departures from the preset the live player gets: the back item switched off,
        // and the second weapon. Without these the portrait shows a different character from the
        // one you then play as.
        if (pc != null) CharacterAppearance.SetBackItem(pc, !character.hideBackItem);
        if (pc != null) CharacterAppearance.ApplyOffhand(pc, character);
        if (pc != null) SetLayerRecursive(rig.transform, layer);   // the weapons arrived after the first pass

        animator = rig.GetComponentInChildren<Animator>(true);
        SetAwake(false);
    }

    private static void StripDrivers(GameObject rig)
    {
        foreach (var c in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null) continue;
            string n = c.GetType().Name;
            // Everything that MOVES the character, by name — the pack's input reader and controller
            // are in a namespace we would otherwise have to reference just to delete them.
            if (n.Contains("Controller") || n.Contains("Input") || n.Contains("Behaviour") && n != "MonoBehaviour")
                DestroyImmediate(c);
        }
        foreach (var rb in rig.GetComponentsInChildren<Rigidbody2D>(true)) DestroyImmediate(rb);
        foreach (var col in rig.GetComponentsInChildren<Collider2D>(true)) DestroyImmediate(col);
    }

    private void BuildCamera(Transform parent, int layer, int width, int height)
    {
        Texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        { filterMode = FilterMode.Point };   // pixel art: never smooth the portrait
        Texture.Create();

        var camGO = new GameObject("PortraitCamera");
        camGO.transform.SetParent(parent, false);
        camGO.transform.localPosition = new Vector3(0f, CAM_Y, -10f);

        cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CAM_SIZE;
        cam.cullingMask = 1 << layer;                 // only this stage, never the menu behind it
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.targetTexture = Texture;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.depth = -50;                               // renders before the menu camera
    }

    // Unchosen characters are FROZEN, not merely dim. Motion is the second selection signal and it
    // costs nothing: the one you are on is the only thing breathing.
    public void SetAwake(bool awake)
    {
        if (animator != null) animator.speed = awake ? 1f : 0f;
    }

    /// <summary>
    /// Switch the whole plot — rig, animator and camera — off while nobody is looking at it.
    ///
    /// ⚠️ **THE PLOTS ARE SCENE-ROOT OBJECTS, NOT CHILDREN OF THE SCREEN.** They have to be: they
    /// live in world space 3000 units away, and the screen is a UI RectTransform. So hiding the
    /// screen does NOT stop them — without this, one camera per character goes on rendering a
    /// 420x614 target every frame for the rest of the session, through the whole run, for a menu
    /// the player left minutes ago.
    /// </summary>
    public void SetStageActive(bool active)
    {
        if (gameObject.activeSelf != active) gameObject.SetActive(active);
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }

    private void OnDestroy()
    {
        if (cam != null) cam.targetTexture = null;
        if (Texture != null) { Texture.Release(); Destroy(Texture); }
    }
}
