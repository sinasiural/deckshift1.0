using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Can Ayarları")]
    public float maxHealth = 30f;

    // Set the first time the player damages this enemy (Whetstone relic reads/sets it).
    [HideInInspector] public bool playerHasStruck = false;

    [Header("Head Bounce")]
    public bool canBeHeadBounced = true;

    [Header("Health Bar")]
    public GameObject healthBarPrefab;
    public Vector2 headBarOffset = new Vector2(0f, 1f);

    [Header("Stun")]
    [SerializeField] private SpriteRenderer[] stunSpriteRenderers = new SpriteRenderer[0];
    [SerializeField] private SkinnedMeshRenderer[] stunSkinnedRenderers = new SkinnedMeshRenderer[0];

    [Header("Efektler")]
    public GameObject damagePopupPrefab;

    [Header("Drops")]
    [Tooltip("Scrap dropped on death. Leave at -1 to derive it automatically from maxHealth " +
             "(ScrapEconomy.ScrapForEnemy), which tiers new enemies correctly with no wiring. " +
             "Set a positive number to override — this is the hook for 'shift-infused' elites, " +
             "which should pay noticeably more than their base version. 0 means drops nothing.")]
    public int scrapDropOverride = -1;

    private float currentHealth;

    // Read-only accessor so external HUDs (e.g. the boss health bar) can poll current HP.
    public float CurrentHealth => currentHealth;

    public event System.Action OnDamaged;
    // Carries the hit's damage amount (e.g. so the boss can flinch on big hits). Fires after a landed hit.
    public event System.Action<float> OnDamagedAmount;
    // Fired once when this enemy dies, right before the GameObject is destroyed
    // (e.g. so the boss can hand music back to the level track).
    public event System.Action OnDied;

    // Polled by AI scripts — they return early while this is true.
    public bool IsStunned { get; private set; }

    private Color[] stunSpriteOriginalColors;
    private Color[] stunSkinnedOriginalColors;
    private Coroutine stunRoutineRef;

    // Damage flash — SpriteRenderer and SkinnedMeshRenderer both supported
    private SpriteRenderer flashSpriteRenderer;
    private SkinnedMeshRenderer flashSkinnedRenderer;
    private Color flashSpriteOriginalColor;
    private Color flashSkinnedOriginalColor;
    private bool flashSkinnedHasColor;

    private EnemyHealthBar healthBar;

    void Start()
    {
        currentHealth = maxHealth;

        stunSpriteOriginalColors = new Color[stunSpriteRenderers.Length];
        for (int i = 0; i < stunSpriteRenderers.Length; i++)
        {
            if (stunSpriteRenderers[i] != null)
                stunSpriteOriginalColors[i] = stunSpriteRenderers[i].color;
        }

        stunSkinnedOriginalColors = new Color[stunSkinnedRenderers.Length];
        for (int i = 0; i < stunSkinnedRenderers.Length; i++)
        {
            if (stunSkinnedRenderers[i] != null && stunSkinnedRenderers[i].material.HasProperty("_Color"))
                stunSkinnedOriginalColors[i] = stunSkinnedRenderers[i].material.color;
        }

        // Flash renderer: SkinnedMeshRenderer first (AeroBat etc.), fallback SpriteRenderer
        flashSkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (flashSkinnedRenderer != null)
        {
            flashSkinnedHasColor = flashSkinnedRenderer.material.HasProperty("_Color");
            if (flashSkinnedHasColor) flashSkinnedOriginalColor = flashSkinnedRenderer.material.color;
        }

        flashSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (flashSpriteRenderer != null) flashSpriteOriginalColor = flashSpriteRenderer.color;

        if (healthBarPrefab != null)
        {
            GameObject barGO = Instantiate(healthBarPrefab);
            healthBar = barGO.GetComponent<EnemyHealthBar>();
            if (healthBar != null)
            {
                healthBar.Initialize(transform, headBarOffset, ComputeBarWidth());
                healthBar.SetHealth(currentHealth, maxHealth);
            }
        }
    }

    // Width for the health bar. Prefer an ENABLED collider — a disabled one (e.g. the box on
    // capsule-fixed enemies) reports a zero-size bounds, which produced a zero-width, invisible
    // bar. Fall back to the visual renderer, then a sane floor so the bar is never sized to nothing.
    private float ComputeBarWidth()
    {
        foreach (Collider2D c in GetComponents<Collider2D>())
            if (c.enabled && c.bounds.size.x > 0.05f)
                return Mathf.Max(0.6f, c.bounds.size.x * 1.2f);

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null && r.bounds.size.x > 0.05f)
            return Mathf.Max(0.6f, r.bounds.size.x * 1.2f);

        return 1f;
    }

    public void TakeDamage(float damage)
    {
        ShieldEnemy shield = GetComponent<ShieldEnemy>();
        if (shield != null && shield.IsBlocking())
        {
            Debug.Log("BLOKLANDI!");
            return;
        }

        currentHealth -= damage;

        // Executioner's Seal (relic): a hit that leaves a non-boss enemy at/under 20% HP finishes it.
        // NOTE: only the Moss Knight is excluded today — future bosses must be added to this guard.
        if (currentHealth > 0 && currentHealth <= maxHealth * 0.2f
            && RelicManager.instance != null && RelicManager.instance.HasRelic("ExecutionersSeal")
            && GetComponent<MossKnightBoss>() == null)
        {
            currentHealth = 0;
        }

        OnDamaged?.Invoke();
        OnDamagedAmount?.Invoke(damage);
        if (HitStop.instance != null) HitStop.instance.Stop(0.15f);
        Debug.Log($"{gameObject.name} hasar aldı! Kalan Can: {currentHealth}");

        StartCoroutine(FlashRoutine());

        if (damagePopupPrefab != null && GameSettings.DamageNumbers)
        {
            GameObject popup = Instantiate(damagePopupPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            popup.GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage));
        }

        healthBar?.SetHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    private IEnumerator FlashRoutine()
    {
        if (flashSkinnedRenderer != null && flashSkinnedHasColor)
            flashSkinnedRenderer.material.SetColor("_Color", Color.white);

        if (flashSpriteRenderer != null)
            flashSpriteRenderer.color = Color.white;

        yield return new WaitForSeconds(0.1f);

        if (flashSkinnedRenderer != null && flashSkinnedHasColor)
            flashSkinnedRenderer.material.SetColor("_Color", flashSkinnedOriginalColor);

        if (flashSpriteRenderer != null)
            flashSpriteRenderer.color = flashSpriteOriginalColor;
    }

    private void Die()
    {
        if (healthBar != null) Destroy(healthBar.gameObject);

        // Blompo: credit the kill to the card that landed the killing blow (Grudge grows, Toll
        // Booth refunds). Die() is called from inside TakeDamage, so the attribution set around
        // that damage is still live — which is what makes this exact rather than a time window.
        if (DeckManager.instance != null)
            CardEnhancements.NoteKill(DeckManager.instance.AttributedCard);

        if (RelicManager.instance != null)
            RelicManager.instance.OnEnemyKilled();

        if (QuestSystem.instance != null)
        {
            QuestSystem.instance.ReportEvent(QuestType.KillEnemy, 1);

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && !player.IsGroundedCheck())
                QuestSystem.instance.ReportEvent(QuestType.AirKill, 1);
        }

        Debug.Log($"{gameObject.name} öldü!");

        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.Overclock))
        {
            if (DeckManager.instance != null)
            {
                DeckManager.instance.isNextCardFree = true;
                Debug.Log("OVERCLOCK AKTİF: Sonraki kart bedava!");
            }
        }

        // Scrap payout. This is the ONLY intrinsic reward for killing anything — before it,
        // a kill paid nothing at all and skipping every fight was the optimal play.
        // Spawned before OnDied/Destroy because the shards must outlive this GameObject
        // (SpawnBurst builds free-standing objects, so they do).
        int scrap = scrapDropOverride >= 0 ? scrapDropOverride : ScrapEconomy.ScrapForEnemy(maxHealth);

        // Magpie: +1 scrap per kill. Added AFTER the override so an elite that hand-sets its drop
        // still benefits — the override says what the enemy is worth, not what the player earns.
        // Applied only when something actually drops, so it can't make a zero-value enemy pay.
        if (scrap > 0 && RelicManager.instance != null && RelicManager.instance.HasRelic("Magpie")) scrap += 1;

        if (scrap > 0) ScrapPickup.SpawnBurst(transform.position, scrap);

        // Bounce House: the corpse leaves a pad. Spawned here alongside the scrap and for the same
        // reason — it must outlive this GameObject, and BouncePad.Spawn builds a free-standing one.
        // The relic check lives inside Spawn so this stays one line.
        BouncePad.Spawn(transform.position);

        // Notify listeners (e.g. the boss) before the object is destroyed.
        OnDied?.Invoke();

        Destroy(gameObject);
    }

    // Calling Stun while already stunned extends the duration (cancels old timer, starts fresh).
    public void Stun(float duration)
    {
        if (stunRoutineRef != null)
            StopCoroutine(stunRoutineRef);
        stunRoutineRef = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        IsStunned = true;
        for (int i = 0; i < stunSpriteRenderers.Length; i++)
        {
            if (stunSpriteRenderers[i] != null) stunSpriteRenderers[i].color = Color.blue;
        }
        for (int i = 0; i < stunSkinnedRenderers.Length; i++)
        {
            if (stunSkinnedRenderers[i] != null && stunSkinnedRenderers[i].material.HasProperty("_Color"))
                stunSkinnedRenderers[i].material.SetColor("_Color", Color.blue);
        }
        Debug.Log($"{gameObject.name} DONDU!");

        yield return new WaitForSeconds(duration);

        IsStunned = false;
        for (int i = 0; i < stunSpriteRenderers.Length; i++)
        {
            if (stunSpriteRenderers[i] != null) stunSpriteRenderers[i].color = stunSpriteOriginalColors[i];
        }
        for (int i = 0; i < stunSkinnedRenderers.Length; i++)
        {
            if (stunSkinnedRenderers[i] != null && stunSkinnedRenderers[i].material.HasProperty("_Color"))
                stunSkinnedRenderers[i].material.SetColor("_Color", stunSkinnedOriginalColors[i]);
        }
        stunRoutineRef = null;
        Debug.Log($"{gameObject.name} ÇÖZÜLDÜ!");
    }
}
