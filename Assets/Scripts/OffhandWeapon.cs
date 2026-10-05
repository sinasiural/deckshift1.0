using UnityEngine;
using Cainos.CustomizablePixelCharacter;

/// <summary>
/// A second weapon in the LEFT hand of a Cainos pixel-character rig.
///
/// The pack has exactly one weapon slot (`PixelCharacter.weaponSlot`), which it glues to the
/// `rigWeapon` bone on the right hand every LateUpdate. This does the same job for the left: a slot
/// object that follows `rigHandL`, with the weapon prefab instantiated under it. Built for the
/// Samurai's two swords (designer 2026-09-17) but nothing here is samurai-specific.
///
/// ⚠️ THE WEAPON PREFAB'S PHYSICS IS STRIPPED ON ARRIVAL. The pack's weapon prefabs ship with a
/// kinematic Rigidbody2D and a trigger PolygonCollider2D so the pack's own demo can throw them. On a
/// character they make the hitbox animation-dependent and cost a physics rebake every frame — the
/// exact reason the player's staff had its physics removed by hand. A weapon added at runtime must
/// not quietly bring it back.
///
/// Sorting and tint are MIRRORED from the main weapon each frame, so whatever the rig does to the
/// right-hand blade (Phase's fade, the sorting layer push) the left-hand one does too.
/// </summary>
public class OffhandWeapon : MonoBehaviour
{
    [Tooltip("Local offset from the left-hand bone. Tune by eye in play mode.")]
    public Vector3 positionOffset = new Vector3(0f, 0.05f, 0f);
    [Tooltip("Extra rotation (degrees, Z) on top of the bone's. ⚠️ The far hand sits BEHIND the body in this rig " +
             "(z +0.14), so the blade is only visible where it pokes past the silhouette: 45 angles it up and " +
             "back over the shoulder, which reads. 180 hangs it straight down like a cane; 0 buries it in the torso.")]
    public float rotationOffset = 45f;

    private PixelCharacter rig;
    private Transform bone;
    private SpriteRenderer[] mine;
    private SpriteRenderer main;

    public static OffhandWeapon Attach(PixelCharacter rig, GameObject weaponPrefab)
    {
        if (rig == null || weaponPrefab == null || rig.rigHandL == null) return null;

        // Already holding one — a second Apply must not add a second blade.
        var existing = rig.GetComponentInChildren<OffhandWeapon>(true);
        if (existing != null) return existing;

        var slot = new GameObject("Offhand Slot");
        slot.transform.SetParent(rig.transform, false);
        var off = slot.AddComponent<OffhandWeapon>();
        off.rig = rig;
        off.bone = rig.rigHandL;

        var weapon = Instantiate(weaponPrefab, slot.transform);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;
        StripPhysics(weapon);

        off.mine = weapon.GetComponentsInChildren<SpriteRenderer>(true);
        off.Sync();
        return off;
    }

    public static void StripPhysics(GameObject weapon)
    {
        foreach (var c in weapon.GetComponentsInChildren<Collider2D>(true)) Destroy(c);
        foreach (var r in weapon.GetComponentsInChildren<Rigidbody2D>(true)) Destroy(r);
    }

    private void LateUpdate() => Sync();

    private void Sync()
    {
        if (bone == null || rig == null) return;

        transform.position = bone.position + bone.rotation * positionOffset;
        transform.rotation = bone.rotation * Quaternion.Euler(0f, 0f, rotationOffset);
        // The pack flips its slot's Z scale for facing; the rig's own facing is a scale on the model,
        // which this slot inherits as a child — so only the slot's own scale needs to match.
        if (rig.weaponSlot != null) transform.localScale = rig.weaponSlot.localScale;

        if (main == null)
        {
            Weapon w = rig.Weapon;
            if (w != null) main = w.GetComponentInChildren<SpriteRenderer>(true);
        }
        if (main == null || mine == null) return;
        foreach (var sr in mine)
        {
            if (sr == null) continue;
            sr.sortingLayerID = main.sortingLayerID;
            sr.sortingOrder = main.sortingOrder;
            sr.color = main.color;
        }
    }
}
