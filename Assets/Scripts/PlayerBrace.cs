using System.Collections;
using UnityEngine;

/// <summary>
/// The Brace card's effect, on the player: for a few seconds the player holds a block of armour,
/// and every hit taken while braced pays Shift.
///
/// Design (designer, 2026-09-27): "brace for 5 seconds. gain 10 block. for every hit you take, gain
/// +1 shift."
///
/// ⚠️ THE BLOCK IS ARMOUR, AND ONLY FOR THE BRACE. It goes into the existing armour pool, so the HUD's
/// armour bar shows it, damage drains it before health, and every armour rule (a hit on armour is
/// still a hit) holds for free. When the brace ends, whatever is left of the BRACE's share is taken
/// back — armour the player already had (the Samurai's Full Plate) is never touched. The brace's
/// share is spent FIRST: a temporary shield takes a hit before permanent plate does.
///
/// ⚠️ "A HIT" MEANS A HIT. OnDamaged fires even when armour eats the whole thing (that is the armour
/// system's rule), so a fully blocked hit still pays Shift. Stagger's blood price also reports
/// through OnDamaged; it is excluded via PlayerHealth.IsPayingCost, or paying Stagger during a brace
/// would pay you back. Parried hits and hits during i-frames never reach OnDamaged, so they don't pay.
///
/// ⚠️ NO SHIFT IN A SANDBOX (hub / recharge rooms) — same umbrella rule as every resource there.
///
/// Lives on the player (added on first use), so a brace carries through a room change.
/// </summary>
public class PlayerBrace : MonoBehaviour
{
    /// <summary>How long the stance holds.</summary>
    public const float Duration = 5f;
    /// <summary>Shift paid per hit taken while braced.</summary>
    public const int ShiftPerHit = 1;

    private PlayerController player;
    private PlayerHealth health;
    private Animator animator;
    private Rigidbody2D rb;
    private AudioSource audioSource;

    private bool active;
    private float blockGiven;     // the size of this brace's block
    private float blockLeft;      // how much of it is still standing
    private float lastArmour;
    private BraceVFX vfx;
    private bool stanceShown;

    public bool IsActive => active;

    public static PlayerBrace For(PlayerController p)
    {
        PlayerBrace b = p.GetComponent<PlayerBrace>();
        return b != null ? b : p.gameObject.AddComponent<PlayerBrace>();
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();

        // 2D and built in code: the brace's sounds must be equally audible wherever the camera is.
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public IEnumerator Run(float block)
    {
        Begin(block);

        float t = 0f;
        while (active && t < Duration)
        {
            t += Time.deltaTime;
            if (vfx != null) vfx.SetTimeLeft01(1f - t / Duration);
            yield return null;
        }

        End();
    }

    private void Begin(float block)
    {
        if (health == null || player == null) return;

        active = true;
        blockGiven = Mathf.Max(0f, block);
        blockLeft = blockGiven;

        health.OnArmourChanged += OnArmourChanged;
        health.OnDamaged += OnDamaged;
        health.OnDied += OnDied;

        lastArmour = health.Armour;
        health.AddArmour(blockGiven);          // reports back through OnArmourChanged

        vfx = BraceVFX.Spawn(transform);
        SfxManager.PlayOn(audioSource, player.braceStartSound != null ? player.braceStartSound : ProcSfx.BossStomp, 0.9f);
        if (CameraShake.instance != null) CameraShake.instance.Shake(0.14f, 0.28f);
    }

    private void OnArmourChanged(float total)
    {
        // Any drop is damage being absorbed, and the brace's share absorbs first.
        if (total < lastArmour) blockLeft = Mathf.Max(0f, blockLeft - (lastArmour - total));
        lastArmour = total;
        if (vfx != null) vfx.SetBlock01(blockGiven > 0f ? blockLeft / blockGiven : 0f);
    }

    private void OnDamaged(float amount)
    {
        if (!active || health.IsPayingCost) return;

        bool sandbox = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomSandbox();
        if (!sandbox) player.AddShift(ShiftPerHit);

        if (vfx != null) vfx.Hit(sandbox ? 0 : ShiftPerHit);
        SfxManager.PlayOn(audioSource, player.braceHitSound != null ? player.braceHitSound : ProcSfx.WallBreak, 1f);
    }

    private void OnDied() { End(); }

    private void End()
    {
        if (!active) return;
        active = false;

        if (health != null)
        {
            health.OnArmourChanged -= OnArmourChanged;
            health.OnDamaged -= OnDamaged;
            health.OnDied -= OnDied;

            // The block fades: take back only what is left of the brace's own share.
            float remove = Mathf.Min(blockLeft, health.Armour);
            if (remove > 0f) health.RemoveArmour(remove);
        }
        blockLeft = 0f;

        if (vfx != null) { vfx.Release(); vfx = null; }
        if (player != null)
            SfxManager.PlayOn(audioSource, player.braceEndSound != null ? player.braceEndSound : ProcSfx.PauseRelease, 0.7f);
        SetStance(false);
    }

    // The stance: the rig's crouch, but only while standing still on the ground. The rig will not
    // play its falling pose while crouched, and it has no crouch-walk driven by this controller, so
    // moving or jumping hands the body straight back to the normal animations.
    private void LateUpdate()
    {
        if (!active) return;
        bool planted = player != null && player.IsGroundedCheck()
                       && rb != null && Mathf.Abs(rb.linearVelocity.x) < 0.25f;
        SetStance(planted);
    }

    private void SetStance(bool on)
    {
        if (on == stanceShown || animator == null) return;
        stanceShown = on;
        animator.SetBool("IsCrouching", on);
    }

    private void OnDisable() { End(); }
}
