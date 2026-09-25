using UnityEngine;

// One end of a portal pair. Walk into it and you come out of its twin.
//
// ⚠️ IT IS ALSO AN IInteractable, AND THAT IS A BUG FIX, NOT A CONVENIENCE.
// Traversal used to be OnTriggerEnter2D alone. Enter fires on ENTRY only, so after arriving you are
// standing INSIDE the destination portal's trigger with nothing more to enter — to go back you had
// to step fully out and walk in again. In a sealed pocket (exactly the walled-off loot rooms Portal
// exists to reach) there is nowhere to step out to, so the return trip was impossible and the player
// was stranded somewhere only a portal could have taken them. The designer hit this.
//
// OnTriggerStay2D is NOT the fix: the moment the cooldown lapsed it would fire again, bounce the
// player to the far portal, land them inside THAT trigger, and ping-pong forever. The traversal has
// to be player-driven, so E is the second door. Walking in still works and is still the normal way.
public class Portal : MonoBehaviour, IInteractable
{
    public Portal linkedPortal;
    public SpriteRenderer spriteRenderer;

    [Header("G�rsel Efektler")]
    public GameObject rangeIndicator;
    [Tooltip("E�er daire k���k kal�yorsa bu say�y� art�r (�rn: 1.1 veya 1.2)")]
    public float visualSizeMultiplier = 1.0f;
    // ---------------------------------------

    [Tooltip("Optional 'press E' hint (an InteractPrompt prefab instance). Shown only once the " +
             "player has SETTLED inside the portal — see promptDelay.")]
    public GameObject prompt;

    // Why the prompt is delayed rather than shown on entry: passing through a portal normally means
    // you are inside the trigger for a few frames, and a keycap that blinks on every traversal is
    // noise. Waiting until the player is still inside after the walk-in has had its chance surfaces
    // the hint precisely in the situation it exists for — standing in a portal that did not fire.
    private const float PromptDelay = 0.45f;

    // ⚠️ TWO SOUNDS, BECAUSE THEY ARE TWO EVENTS WITH OPPOSITE SHAPES. Opening the pair is a cast —
    // it can swell. Stepping through is instant — it has to be all attack, or it arrives after you
    // do. Portal.mp3 takes half a second to peak, so it belongs on opening and nowhere else.
    [Header("Sound")]
    [Tooltip("Played once when the second portal is placed and the pair links up. Portal.mp3.")]
    [SerializeField] private AudioClip openSound;
    [Range(0f, 2f)] [SerializeField] private float openVolume = 0.8f;
    [Tooltip("Played on every trip through. Empty = the procedural ProcSfx.PortalPass.")]
    [SerializeField] private AudioClip traverseSound;
    [Range(0f, 2f)] [SerializeField] private float traverseVolume = 0.9f;

    // 2D, like the boss sounds: this is the player's own action and must not fade with distance.
    // Built on first use because the prefab carries no AudioSource.
    private AudioSource voice;

    private PlayerController occupant;
    private float occupantSince;

    // The range border is now a procedural rotating dashed ring (PortalRangeRing) built at the
    // EXACT gameplay radius — the old flat rangeIndicator sprite is kept assigned on the prefab
    // for compatibility but stays hidden.
    private PortalRangeRing rangeRing;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            occupant = pc;
            occupantSince = Time.time;
        }

        TryTraverse(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        occupant = null;
        if (prompt != null) prompt.SetActive(false);
    }

    private void Update()
    {
        if (prompt == null) return;

        bool show = occupant != null
                 && linkedPortal != null
                 && Time.time - occupantSince >= PromptDelay;

        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    // IInteractable: E while standing in the portal. PlayerController.CheckInteraction sweeps
    // `interactableLayer`, so the portal prefab must stay on the Interactable layer (12) for this
    // to be reachable at all.
    public void Interact()
    {
        if (occupant != null) TryTraverse(occupant.gameObject);
    }

    public string GetInteractText() => "Step through";

    // Both doors — the trigger and E — funnel through here, so the two can never drift apart.
    // Teleportable owns the cooldown that stops an arrival immediately bouncing back.
    private void TryTraverse(GameObject traveller)
    {
        if (linkedPortal == null) return;

        Teleportable t = traveller.GetComponent<Teleportable>();
        if (t == null) return;

        if (t.TeleportTo(linkedPortal.transform.position))
            PlaySound(traverseSound != null ? traverseSound : ProcSfx.PortalPass, traverseVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }
        SfxManager.PlayOn(voice, clip, volume);
    }

    public void ShowRangeCircle(float range)
    {
        if (rangeIndicator != null) rangeIndicator.SetActive(false);   // superseded by the animated ring

        if (rangeRing == null)
            rangeRing = PortalRangeRing.Spawn(transform, range);
    }

    public void HideRangeCircle()
    {
        if (rangeIndicator != null) rangeIndicator.SetActive(false);

        if (rangeRing != null)
        {
            Destroy(rangeRing.gameObject);
            rangeRing = null;
        }
    }

    public void Link(Portal otherPortal)
    {
        HideRangeCircle();
        // -----------------------------------------------------

        linkedPortal = otherPortal;
        otherPortal.linkedPortal = this;

        PlaySound(openSound, openVolume);

        spriteRenderer.color = Color.cyan;
        otherPortal.spriteRenderer.color = Color.red;
    }

    private void OnDisable()
    {
        HideRangeCircle();
        if (prompt != null) prompt.SetActive(false);
    }
}
