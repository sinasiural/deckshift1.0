using UnityEngine;
using System.Collections;

public class BreakableWall : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHP = 30f;
    [SerializeField] private GameObject breakVFX;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float headBarOffset = 1f;

    private float currentHP;
    private EnemyHealthBar healthBar;

    // Flash — SpriteRenderer first (standard dungeon props); SkinnedMeshRenderer fallback (Cainos-style rigs).
    private SpriteRenderer spriteRenderer;
    private Color spriteOriginalColor;

    private SkinnedMeshRenderer skinnedRenderer;
    private MaterialPropertyBlock propBlock;
    private Color skinnedOriginalColor = Color.white;

    private void Awake()
    {
        currentHP = maxHP;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
            spriteOriginalColor = spriteRenderer.color;

        skinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skinnedRenderer != null)
        {
            propBlock = new MaterialPropertyBlock();
            if (skinnedRenderer.sharedMaterial != null && skinnedRenderer.sharedMaterial.HasProperty("_Color"))
                skinnedOriginalColor = skinnedRenderer.sharedMaterial.GetColor("_Color");
        }
    }

    private void Start()
    {
        if (healthBarPrefab != null)
        {
            GameObject barGO = Instantiate(healthBarPrefab);
            healthBar = barGO.GetComponent<EnemyHealthBar>();
            if (healthBar != null)
            {
                Collider2D col = GetComponent<Collider2D>();
                float barWidth = col != null ? col.bounds.size.x * 1.2f : 1f;
                healthBar.Initialize(transform, new Vector2(0f, headBarOffset), barWidth);
                healthBar.SetHealth(currentHP, maxHP);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentHP <= 0f) return;

        currentHP -= damage;
        StartCoroutine(FlashRoutine());
        healthBar?.SetHealth(currentHP, maxHP);

        if (currentHP <= 0f)
            Break();
    }

    // Crowbar: bumping into the wall breaks it outright.
    //
    // ⚠️ THIS IS THE RELIC THAT LIFTS LEVEL LAW #6. That law forbids hiding anything behind terrain
    // precisely because the player has no wall-breaking attack — so every breakable wall in the game
    // is currently optional, and must stay that way. The relic opens shortcuts and caches; it must
    // never become the way a room is finished.
    //
    // ⚠️ NO KEYBIND, BY DESIGN (designer, 2026-08-21: only boss relics may add an input). Walking
    // into it is the whole interaction.
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (currentHP <= 0f) return;
        if (!other.collider.CompareTag("Player")) return;
        if (RelicManager.instance == null || !RelicManager.instance.HasRelic("Crowbar")) return;

        // Straight to Break rather than TakeDamage: the wall's HP is tuned against card damage, and
        // a Crowbar that merely chipped it would read as the relic not working.
        Break();
    }

    private IEnumerator FlashRoutine()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;

        if (skinnedRenderer != null)
        {
            skinnedRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", Color.white);
            skinnedRenderer.SetPropertyBlock(propBlock);
        }

        yield return new WaitForSeconds(0.08f);

        if (spriteRenderer != null)
            spriteRenderer.color = spriteOriginalColor;

        if (skinnedRenderer != null)
        {
            skinnedRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", skinnedOriginalColor);
            skinnedRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void Break()
    {
        if (healthBar != null) Destroy(healthBar.gameObject);

        if (breakVFX != null)
            Instantiate(breakVFX, transform.position, Quaternion.identity);

        SfxManager.PlayAtPoint(breakSound, transform.position);

        CameraShake.instance?.Shake(0.1f, 0.15f);
        Destroy(gameObject);
    }
}
