using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public bool isInvincible = false;

    // ============================================================================================
    // ARMOUR — a second pool that sits ON TOP of health and empties first.
    //
    // Introduced with the Samurai (whose "Full Plate" trait grants 5 on entering every combat room
    // and lets it stack across rooms), but it is a GAME system, not a character one: relics, cards
    // and blessings are expected to grant and spend it later. Rules, each load-bearing:
    //
    //  - No regeneration and no cap. Sources add; damage takes. Nothing refills it on its own —
    //    the same philosophy as Shift.
    //  - Damage order is ARMOUR -> HP, always, and it happens in ONE place (ApplyDamage) so no
    //    future damage source can forget it.
    //  - ⚠️ A HIT ABSORBED BY ARMOUR IS STILL A HIT. The hurt animation plays, OnDamaged fires with
    //    the FULL incoming size, knockback applies, the flawless-clear payout is lost and oaths
    //    break. Armour changes what a hit COSTS, never whether it happened — otherwise every
    //    "took no damage" consumer in the project (tookDamageThisRoom, RelicManager.OnPlayerTakeDamage,
    //    the NoDamageRoom quest type, Glass cards reading low HP) silently changes meaning the
    //    moment anybody is holding 1 Armour.
    //  - ⚠️ Stagger's blood price BYPASSES it — see PayHealthCost.
    // ============================================================================================
    [Header("Armour")]
    [Tooltip("A hit that lands on Armour always absorbs up to the armour value. This decides what " +
             "happens to the REMAINDER: off = chip (the rest survives, so it stacks across rooms " +
             "for a player who is never touched); on = shatter (any hit at all empties it).")]
    public bool armourShatters = false;

    private float armour = 0f;
    public float Armour => armour;

    /// <summary>Fires whenever the armour pool changes, carrying the new total. Drives the HUD bar.</summary>
    public event System.Action<float> OnArmourChanged;

    /// <summary>Adds to the armour pool. The only way in; there is no setter and no maximum.</summary>
    public void AddArmour(float amount)
    {
        if (isDead || amount <= 0f) return;
        armour += amount;
        OnArmourChanged?.Invoke(armour);
    }

    [Header("Audio")]
    [SerializeField] AudioClip hurtSound;
    [SerializeField] AudioClip deathSound;
    [SerializeField] float deathVolume = 1f;

    private float currentHealth;
    private bool isDead = false;
    private float baseMaxHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    // The unmodified max HP, captured before any relic touches it. Relic passives are always
    // recomputed from THIS (see RelicManager.RecomputePassives) so selling a relic reverses it
    // exactly, regardless of what order relics were gained or sold in.
    public float BaseMaxHealth => baseMaxHealth;

    // --- Glass Parry window (opened by PlayerController.GlassParryRoutine) ---
    // The first hit that lands inside the window is negated entirely and flips
    // ParryTriggered instead of dealing damage; the routine watches that flag.
    private bool parryWindowActive = false;
    public bool ParryTriggered { get; private set; }

    public void BeginParryWindow() { parryWindowActive = true; ParryTriggered = false; }

    // Clears BOTH flags — ParryTriggered also gates ApplyKnockback, and leaving it set
    // would suppress every knockback for the rest of the run after one good parry.
    // Callers must read ParryTriggered BEFORE ending the window.
    public void EndParryWindow()   { parryWindowActive = false; ParryTriggered = false; }

    public event System.Action<float> OnDamaged;
    public event System.Action OnDied;
    public event System.Action<Vector2> OnKnockback;
    public event System.Action OnFallRespawn;

    private Rigidbody2D rb;
    private Animator animator;
    private AudioSource audioSource;
    private PlayerController playerController;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        playerController = GetComponent<PlayerController>();
        baseMaxHealth = maxHealth;
    }

    // Applied by relic passives (RelicManager.RecomputePassives). Clamp-only, deliberately:
    // healing on a capacity GAIN would let the player equip Glass Heart (halved max HP), take
    // damage, then SELL it for a free ~50 HP refill. Never drops below 1, so equipping a
    // max-HP-reducing relic can't kill you outright.
    public void SetMaxHealth(float newMax)
    {
        if (isDead) return;

        maxHealth = Mathf.Max(1f, newMax);
        currentHealth = Mathf.Clamp(currentHealth, 1f, maxHealth);
    }

    // A PERMANENT max-HP gain (quest rewards). ⚠️ It has to raise `baseMaxHealth`, not `maxHealth`:
    // RelicManager.RecomputePassives rebuilds maxHealth from the base every time the loadout
    // changes, so a bonus written straight onto maxHealth would silently vanish the next time the
    // player gained or sold any relic. Recomputing here keeps HP relics (Reinforced Plating, Glass
    // Heart) stacking correctly on top of the new base.
    public void IncreaseBaseMaxHealth(float amount)
    {
        if (amount <= 0f) return;

        baseMaxHealth += amount;
        if (RelicManager.instance != null) RelicManager.instance.RecomputePassives();
        else SetMaxHealth(maxHealth + amount);

        // The gain arrives as usable health, not just a bigger empty bar.
        Heal(amount);
    }

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible || isDead) return;

        // Glass Parry: the hit shatters on the glass — no damage, no hurt anim,
        // no OnDamaged. One hit per window; the parry routine handles the payoff.
        if (parryWindowActive && !ParryTriggered)
        {
            ParryTriggered = true;
            return;
        }

        // Relic scaling on damage TAKEN (Paper Skin, Odd Socket).
        //
        // ⚠️ HERE, NOT IN ApplyDamage. PayHealthCost also routes through ApplyDamage, and that is
        // Stagger's bill — a price the player CHOSE to pay, not a hit. Scaling it there would make
        // Paper Skin quietly raise the cost of Stagger by 50%, which is not what it says it does.
        // Sitting after the invincibility and parry returns also means a hit that deals nothing
        // stays nothing.
        if (RelicManager.instance != null) damage = RelicManager.instance.ModifyIncomingDamage(damage);

        ApplyDamage(damage);
    }

    // A price the player CHOSE to pay — currently only Stagger's escalating HP cost. Not a hit.
    //
    // ⚠️ It deliberately ignores BOTH invincibility and the parry window, which is the whole reason
    // it isn't just TakeDamage. Those two guards make TakeDamage a silent no-op, and the payout for
    // a self-inflicted cost is granted by the CALLER, not here — so routing Stagger through
    // TakeDamage would hand out free Shift any time the player happened to be mid-dash, inside a
    // Phoenix Cog mercy window, or holding a parry. "Sometimes free" is worse than either.
    //
    // It can still kill, and Phoenix Cog can still save you from it: paying more than you have is
    // exactly the fail state Stagger is supposed to be.
    // ⚠️ AND IT BYPASSES ARMOUR, for the same family of reason. Stagger's bill is the fail state —
    // the price of having spent Shift you did not have. Paying it out of armour would make Stagger
    // free for exactly the character who stacks armour, and "sometimes free" is worse than either.
    public void PayHealthCost(float amount)
    {
        if (isDead || amount <= 0f) return;
        ApplyDamage(amount, ignoreArmour: true);
    }

    private void ApplyDamage(float damage, bool ignoreArmour = false)
    {
        // Captured before armour eats any of it — this is what OnDamaged reports.
        float incoming = damage;

        // ARMOUR FIRST. The absorbed part never reaches health; only the remainder does.
        // Both modes absorb identically — they differ only in what happens to what is LEFT.
        if (!ignoreArmour && armour > 0f && damage > 0f)
        {
            float absorbed = Mathf.Min(armour, damage);
            damage -= absorbed;
            armour = armourShatters ? 0f : armour - absorbed;
            OnArmourChanged?.Invoke(armour);
        }

        currentHealth = Mathf.Max(currentHealth - damage, 0f);

        SfxManager.PlayOn(audioSource, hurtSound);

        if (animator != null) animator.SetTrigger("InjuredFront");

        Debug.Log($"Hasar Alındı! Kalan Can: {currentHealth}");

        // ⚠️ Fires even when armour ate the whole hit, and carries the hit's FULL size. See the
        // Armour header: a hit is a hit. Every consumer of this event is asking "was the player
        // struck", not "did the health number move".
        OnDamaged?.Invoke(incoming);

        if (currentHealth <= 0)
        {
            // Phoenix Cog: once per run, a lethal hit leaves you at 1 HP and erupts instead.
            if (RelicManager.instance != null && RelicManager.instance.TryConsumePhoenixCog())
            {
                currentHealth = 1f;
                RelicManager.instance.PhoenixBlast(transform.position);
                StartCoroutine(GrantInvincibility(1.5f));   // mercy window so 1 HP isn't instant death
                return;
            }

            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // Adrenaline slow-mo scales Time.timeScale/fixedDeltaTime and restores them in a
        // coroutine that dies with the scene load below — so a death during slow-mo would
        // leave the whole game at 40% speed forever. Reset defensively on every death;
        // outside Adrenaline these are already 1 / 0.02 (audit_report.md Critical #3).
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (playerController.currentState == PlayerState.CometDiving)
            playerController.EndCometDive();

        Debug.Log("💀 Oyuncu Öldü! Ses Çalınıyor...");

        if (deathSound != null)
        {
            if (Camera.main != null)
                SfxManager.PlayAtPoint(deathSound, Camera.main.transform.position, deathVolume);
            else
                SfxManager.PlayAtPoint(deathSound, transform.position, deathVolume);
        }

        if (animator != null) animator.SetBool("IsDead", true);
        if (rb != null) rb.simulated = false;

        OnDied?.Invoke();

        StartCoroutine(WaitAndReload());
    }

    public void Kill() => Die();

    private IEnumerator WaitAndReload()
    {
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene("GameOverScene");
    }

    public void ApplyKnockback(Vector2 knockbackForce)
    {
        // Glass-steady: while a parry window is open (or just triggered), the player
        // doesn't get shoved — a parried hit that still knocked you into spikes
        // would make the negation feel like a lie.
        if (parryWindowActive || ParryTriggered) return;

        OnKnockback?.Invoke(knockbackForce);
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector2 knockbackForce)
    {
        if (playerController.currentState == PlayerState.CometDiving)
            playerController.EndCometDive();
        playerController.ChangeState(PlayerState.KnockedBack);
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockbackForce, ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.2f);
        if (playerController.currentState == PlayerState.KnockedBack)
            playerController.ChangeState(PlayerState.Jumping);
    }

    public void FallAndRespawn()
    {
        if (playerController.currentState == PlayerState.CometDiving)
            playerController.EndCometDive();
        rb.linearVelocity = Vector2.zero;
        transform.position = playerController.currentRoomEntryPoint;
        // Clears fall tracking (the teleport isn't a fall — don't Meteor on landing) and re-anchors
        // a live Phase bubble, which would otherwise yank the player back out of the respawn.
        playerController.OnTeleported();
        OnFallRespawn?.Invoke();
    }

    public IEnumerator GrantInvincibility(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }
}
