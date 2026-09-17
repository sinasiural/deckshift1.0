using UnityEngine;
using Cainos.CustomizablePixelCharacter;

// Re-dresses the player's existing rig to look like a character's chosen Cainos preset.
//
// ⚠️ IT COPIES MATERIALS ONTO THE RIG WE ALREADY HAVE — it does not instantiate the preset. See the
// note on CharacterData.appearancePreset for why. Every preset in the pack is the same skeleton
// wearing different slot materials, so this produces the same character with none of the risk of
// rebuilding the player's carefully stripped-down visual model at runtime.
public static class CharacterAppearance
{
    // ⚠️ NOTHING IN HERE MAY EVER THROW INTO THE CALLER, AND THIS IS NOT DEFENSIVE PROGRAMMING FOR
    // ITS OWN SAKE. It is called from PlayerController.Awake, and Unity DISABLES a MonoBehaviour
    // whose Awake throws — so a costume that failed to load took the whole player component with
    // it: no Start, no Update, no movement, no jump, no card hotkeys. The game still looked alive
    // because Recall lives on DeckManager and clicking a card is a UI event, which is exactly what
    // makes that failure so confusing to diagnose. A cosmetic pass must never be able to brick the
    // character; the worst it may do is leave them in the wrong clothes.
    public static void Apply(PlayerController player, CharacterData character)
    {
        try { ApplyInner(player, character); }
        catch (System.Exception e)
        {
            Debug.LogWarning("CharacterAppearance: could not apply the look, leaving the default outfit. " + e.Message);
        }
    }

    private static void ApplyInner(PlayerController player, CharacterData character)
    {
        if (player == null || character == null || character.appearancePreset == null) return;

        PixelCharacter target = player.visualModel != null
            ? player.visualModel.GetComponentInChildren<PixelCharacter>(true)
            : player.GetComponentInChildren<PixelCharacter>(true);

        PixelCharacter source = character.appearancePreset.GetComponentInChildren<PixelCharacter>(true);

        if (target == null || source == null)
        {
            Debug.LogWarning("CharacterAppearance: no PixelCharacter on " +
                             (target == null ? "the player" : "the preset") + " — look not applied.");
            return;
        }

        // Every wearable slot the pack exposes. A slot the preset leaves empty comes back null,
        // which is correct: that is how a preset says "no hat".
        target.HatMaterial      = Safe(() => source.HatMaterial);
        target.HairMaterial     = Safe(() => source.HairMaterial);
        target.EyeMaterial      = Safe(() => source.EyeMaterial);
        target.EyeBaseMaterial  = Safe(() => source.EyeBaseMaterial);
        target.FacewearMaterial = Safe(() => source.FacewearMaterial);
        target.ClothMaterial    = Safe(() => source.ClothMaterial);
        target.PantsMaterial    = Safe(() => source.PantsMaterial);
        target.SocksMaterial    = Safe(() => source.SocksMaterial);
        target.ShoesMaterial    = Safe(() => source.ShoesMaterial);
        target.BackMaterial     = Safe(() => source.BackMaterial);
        target.BodyMaterial     = Safe(() => source.BodyMaterial);

        // The back slot is the one place a character deliberately differs from its preset: the
        // Samurai's banner belongs on the BOSS made from him, not on the player (designer 2026-09-17).
        SetBackItem(target, !character.hideBackItem);

        target.ClipHair     = source.ClipHair;
        target.HideHair     = source.HideHair;
        target.ShoesInFront = source.ShoesInFront;

        // ⚠️ HairRampTexture is deliberately NOT copied. It is the one slot most presets leave
        // unassigned — the Ninja is one — and on this pack READING an unassigned object reference
        // throws rather than returning null. It only shades hair the ninja hood hides anyway, so
        // the player keeps the ramp their own rig already has.

        ApplyWeapon(target, character);
    }

    // ⚠️ USE THE PACK'S OWN `AddWeapon`, NOT HAND-PARENTING. `PixelCharacter.Weapon` is read-only,
    // and the pack does more on a weapon change than reparent an object — it syncs the weapon slot
    // to the rig bone and pushes the character's sorting layer and alpha onto the new renderers.
    // A weapon dropped into the hierarchy by hand looks right standing still and then sorts or
    // fades wrongly the first time anything touches those.
    //
    // The PRESET's own Weapon reference is still never used: it points at an object living inside
    // the preset ASSET, which a live player must never hold. The character names the prefab it
    // wants instead.
    private static void ApplyWeapon(PixelCharacter target, CharacterData character)
    {
        if (character.weaponPrefab != null)
        {
            // Already holding it — a second Apply on the same player must not add a second weapon.
            Weapon current = target.Weapon;
            if (current == null || !current.name.StartsWith(character.weaponPrefab.name))
            {
                target.AddWeapon(character.weaponPrefab, true);

                // ⚠️ The pack's weapon prefabs carry a kinematic Rigidbody2D and a trigger collider
                // (so its demo can throw them). The player's staff had those removed by hand for a
                // reason — an animation-driven hitbox and a physics rebake every frame — and a
                // weapon added at runtime must not quietly bring them back.
                if (target.Weapon != null) OffhandWeapon.StripPhysics(target.Weapon.gameObject);
            }
        }

        ApplyOffhand(target, character);
    }

    /// <summary>
    /// Show or hide the preset's back item. ⚠️ By DISABLING THE RENDERER, never by nulling the
    /// material: `BackMaterial = null` leaves the back renderer drawing with no shader, which is a
    /// large MAGENTA quad behind the character — photographed on the select screen 2026-09-17.
    /// Public because the portrait builds its rig from the preset and has to match.
    /// </summary>
    public static void SetBackItem(PixelCharacter target, bool visible)
    {
        if (target == null || target.back == null) return;
        target.back.enabled = visible;
    }

    /// <summary>
    /// The left-hand weapon, if the character has one. Public because the select screen's portrait
    /// builds its rig from the preset directly and has to dress it the same way.
    /// </summary>
    public static void ApplyOffhand(PixelCharacter target, CharacterData character)
    {
        if (target == null || character == null || character.offhandWeaponPrefab == null) return;
        OffhandWeapon.Attach(target, character.offhandWeaponPrefab);
    }

    // Reading an unassigned reference on these presets throws UnassignedReferenceException instead
    // of giving back null, so every slot read goes through here. Empty is a legitimate value.
    private static T Safe<T>(System.Func<T> read) where T : class
    {
        try { return read(); }
        catch { return null; }
    }
}
