using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitDoor : MonoBehaviour
{
    [Header("T�r Ayar� (�NEML�)")]
    public bool isSceneLoader = false;
    public string sceneToLoad = "GameScene";

    [Header("Etkile�im Ayarlar�")]
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactionPopup;

    // ⚠️ ONE SPRITE, NOT THE PACK'S FENCE PREFAB — and that is the whole point.
    // The first version of the boss gate instantiated `PF Dungeon Props - Door Iron Fence 01` whole
    // and parked it over the exit. The designer's verdict (2026-08-22): *"the exit door is very ugly.
    // its smaller than the exit door behind it, which is still there … i meant that we could perhaps
    // use the bars on that prefab only."* Correct on every count — that prefab carries its own stone
    // frame, its own shadow, a bright cyan sky panel, a light shaft, a Light2D and a dust emitter, so
    // dropping it in front of a door that already has an arch produced two arches, one smaller than
    // the other, lit differently.
    //
    // The bars now belong to THIS door: one sprite, fitted to the archway this door already has, and
    // driven by the lock the door already tracks. A room that seals its exit gets the visual for
    // free, including bosses nobody has written yet.
    [Header("Boss Gate")]
    [Tooltip("Bars drawn across the archway while the door is LOCKED. Assign " +
             "'TX Dungeon Props - Door Fence 01 A'. Empty = the door still locks, just silently.")]
    public Sprite barsSprite;
    [Tooltip("Vertical nudge if the bars do not sit flush in the arch.")]
    public float barsNudgeY = 0f;

    private bool hasBeenTriggered = false;
    private bool isPlayerInRange = false;
    private PlayerController currentPlayer;

    /// <summary>
    /// Sealed. A boss room locks its exit while the boss lives, so the fight cannot simply be walked
    /// past — that was possible and reported 2026-08-22.
    ///
    /// ⚠️ RUNTIME ONLY, AND IT DEFAULTS TO UNLOCKED. Never serialize a room shut: if whatever was
    /// meant to unlock it fails or is missing, an open door costs nothing and a locked one strands
    /// the run. Fail toward passable.
    /// </summary>
    public bool IsLocked { get; private set; }

    public event System.Action<bool> OnLockChanged;

    public void SetLocked(bool value)
    {
        if (IsLocked == value) return;
        IsLocked = value;

        // ⚠️ THE VISUAL HALF IS SKIPPED WHILE THIS DOOR IS BEING TORN DOWN, and the state half is
        // not. The boss unlocks the exit from his own `OnDestroy` — the deliberate fail-toward-
        // passable path — which on a room change runs while the room is already going away. Unity
        // then logs "GameObjects can not be made active when they are being destroyed" for the
        // prompt, and `StartCoroutine` throws outright for the bars. Neither matters visually at that
        // point (the room is gone), but an exception here would abort the rest of a call whose whole
        // job is to leave the room passable.
        bool alive = isActiveAndEnabled;

        if (alive && interactionPopup != null)
        {
            // The prompt must vanish the instant it stops being true, even if the player is standing
            // in the trigger right now.
            if (value) interactionPopup.SetActive(false);
            else if (isPlayerInRange) interactionPopup.SetActive(true);
        }

        if (value) { if (alive) DropBars(); }
        else RaiseBars();          // has its own teardown guard: destroys rather than animating

        OnLockChanged?.Invoke(value);
    }

    // ---- the bars ---------------------------------------------------------------------------------

    private SpriteRenderer bars;
    private Coroutine barsRoutine;

    // ⚠️ A SpriteRenderer TINT MULTIPLIES — it can only ever DARKEN. So there is no tint that makes
    // the pack's dark iron brighter, and reaching for one is a dead end (an HDR value above 1 depends
    // on pipeline settings this project does not guarantee). If the bars need to read harder, the
    // contrast has to come from what is BEHIND them, which is exactly why the pack's own fence prefab
    // ships with a lit sky panel. Left at white and judged on screen.
    private static readonly Color BarsLit = Color.white;

    private void DropBars()
    {
        if (barsSprite == null || bars != null) return;

        // Fit to the arch mouth this door already has, never to a hardcoded size. The exit art has
        // been swapped twice; a fixed scale would strand the bars the next time it changes.
        SpriteRenderer mouth = FindOpening();
        if (mouth == null) return;

        var go = new GameObject("Bars");
        go.transform.SetParent(mouth.transform.parent, false);

        bars = go.AddComponent<SpriteRenderer>();
        bars.sprite = barsSprite;
        bars.sortingLayerID = mouth.sortingLayerID;
        bars.sortingOrder = mouth.sortingOrder + 2;

        // ⚠️ AND A Z NUDGE AS WELL AS A SORTING ORDER. The arch's `Frame` renders through an OPAQUE
        // Cainos shader, and opaque geometry sorts by CAMERA DEPTH — `sortingOrder` is very nearly
        // ignored for it. This is the single most recurrent sorting trap in the project. The camera
        // looks along +Z, so nearer means SMALLER z.
        Vector3 size = mouth.bounds.size;
        Vector3 native = barsSprite.bounds.size;

        // ⚠️ UNIFORM, AND DIVIDED BACK OUT OF THE PARENT'S SCALE. Two separate traps, both of which
        // bit on the first attempt:
        //
        //   `localScale` is RELATIVE. The ratio here is computed from WORLD bounds, but the door's
        //   `Visual` is itself scaled ~1.32, so assigning the world ratio as a local one squared it —
        //   the bars came out roughly 1.7x oversize and hung through the floor.
        //
        //   Pixel art must scale UNIFORMLY. Fitting width and height independently shears the iron.
        //   Fitting to HEIGHT is right because the height is what has to reach the floor; the sprite
        //   and the arch mouth happen to be within 3% on aspect anyway (0.549 vs 0.564).
        float fit = native.y > 0.001f ? size.y / native.y : 1f;
        Vector3 parentScale = go.transform.parent != null ? go.transform.parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? fit : fit / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? fit : fit / parentScale.y,
            1f);
        // ⚠️ THIS SPRITE'S PIVOT IS TOP-CENTRE (0.50, 1.00), NOT THE MIDDLE — because a portcullis
        // hangs from its top, which is how the pack animates it. So `transform.position` places the
        // TOP EDGE, and setting it to the arch's centre put the whole grille exactly one half-height
        // low: the bars hung through the floor and out of the bottom of the screen. Same family as
        // the documented uGUI trap that `anchoredPosition` places the PIVOT, in world space.
        //
        // Backed out ARITHMETICALLY from the sprite's own local bounds rather than by re-reading
        // renderer bounds after moving — `Physics2D.autoSyncTransforms` is off and same-frame bounds
        // reads are one of this project's most expensive recurring mistakes.
        Vector3 pivotToCentre = barsSprite.bounds.center * fit;
        go.transform.position = new Vector3(mouth.bounds.center.x - pivotToCentre.x,
                                            mouth.bounds.center.y - pivotToCentre.y + barsNudgeY,
                                            mouth.transform.position.z - 0.05f);

        // ⚠️ AND THEY HAVE TO BE LIT, or they are black iron inside a black hole.
        // This is why the pack's own fence prefab ships with a bright cyan `Sky` panel and a light
        // shaft behind it — those exist to give the bars something to be a silhouette against. We
        // deliberately did not take them (they are what made the first version ugly), so the contrast
        // has to come from the bars themselves instead.
        bars.color = BarsLit;

        barsHome = go.transform.position;
        barsHeight = size.y;
    }

    private Vector3 barsHome;
    private float barsHeight;

    /// <summary>
    /// The dark opening inside the arch — that is the hole the bars have to cover. Falls back to the
    /// largest sprite on the door, so this still does something sensible on art it has never seen.
    /// </summary>
    private SpriteRenderer FindOpening()
    {
        SpriteRenderer best = null;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r.sprite == null) continue;
            if (r.name == "Inside") return r;
            if (best == null || r.bounds.size.y > best.bounds.size.y) best = r;
        }
        return best;
    }

    private void RaiseBars()
    {
        if (bars == null) return;

        // ⚠️ THE UNLOCK CAN ARRIVE WHILE THIS DOOR IS ALREADY BEING TORN DOWN, and `StartCoroutine`
        // on an inactive GameObject THROWS. The boss unlocks the exit from his own `OnDestroy` — the
        // deliberate fail-toward-passable path — which on a room change runs while the room is going
        // away, so the door is inactive and the animation has nowhere to run. Caught in the console
        // as "Coroutine couldn't be started because the the game object 'ExitDoor' is inactive!".
        //
        // An exception there would abort the rest of `SetLocked`, which is the one thing that must
        // never fail: the whole point of that call is to leave the room passable.
        if (!isActiveAndEnabled)
        {
            Destroy(bars.gameObject);
            bars = null;
            return;
        }

        if (barsRoutine != null) StopCoroutine(barsRoutine);
        barsRoutine = StartCoroutine(RaiseBarsRoutine(bars));
        bars = null;             // it is on its way out; a re-lock must build a fresh set
    }

    /// <summary>
    /// The portcullis going up.
    ///
    /// ⚠️ IT FADES AS IT RISES, and that is not decoration — it is the mask. These are world sprites
    /// with nothing to clip them, so bars that simply slid upward would emerge ABOVE the arch and
    /// travel up the wall in plain sight. Fading them out over the same motion is what sells them
    /// being drawn up INTO the stone.
    ///
    /// The sound is the gate family, which `ProcSfx` notes were written for a portcullis in the first
    /// place (strain → catch → ratchet) and have only ever been used on a double door since.
    /// </summary>
    private IEnumerator RaiseBarsRoutine(SpriteRenderer sr)
    {
        SfxManager.PlayAtPoint(ProcSfx.GateGroan, sr.transform.position, 0.9f);
        yield return new WaitForSeconds(0.30f);          // it takes the weight before it moves
        SfxManager.PlayAtPoint(ProcSfx.GateRelease, sr.transform.position, 1f);

        Vector3 from = sr.transform.position;
        float travel = barsHeight * 0.95f;
        const float DUR = 0.85f;
        float t = 0f;
        int ticks = 0;

        while (t < DUR)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DUR);
            // Ease OUT: it lurches free and then slows as the chain runs out. A linear rise reads as
            // a lift, not as a mechanism.
            float e = 1f - (1f - k) * (1f - k);

            sr.transform.position = from + Vector3.up * (travel * e);
            var c = sr.color;
            c.a = 1f - e * e;                            // gone before it would clear the archway
            sr.color = c;

            int want = Mathf.FloorToInt(e * 5f);
            if (want > ticks) { ticks = want; SfxManager.PlayAtPoint(ProcSfx.GateRatchet, sr.transform.position, 0.5f); }
            yield return null;
        }

        Destroy(sr.gameObject);
        barsRoutine = null;
    }

    private void Update()
    {
        if (isPlayerInRange && !IsLocked && !hasBeenTriggered && Input.GetKeyDown(interactKey))
        {
            PerformExit();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenTriggered) return;

        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            currentPlayer = other.GetComponent<PlayerController>();
            // No prompt while sealed — offering "press E" on a door that refuses is worse than
            // offering nothing, because it reads as the key being broken.
            if (!IsLocked && interactionPopup != null) interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            currentPlayer = null;
            if (interactionPopup != null) interactionPopup.SetActive(false);
        }
    }

    private void PerformExit()
    {
        if (hasBeenTriggered || IsLocked) return;
        hasBeenTriggered = true;

        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (isSceneLoader)
        {
            Debug.Log("Hub'dan ��k�l�yor, oyun ba�l�yor...");

            if (QuestSystem.instance != null) QuestSystem.instance.CloseBoard();

            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            // The hub is not a real combat room — leaving it undamaged must NOT count as a flawless
            // clear (this is why the "Invincible" quest was auto-completing on the very first exit).
            // Neither is a recharge room (Foundry / Market / Well): no enemies, so every payout
            // below would be free there. `isCombat` covers both; see RechargeRoomMarker.
            bool isCombat = LevelManager.instance == null || LevelManager.instance.IsCurrentRoomCombat();

            if (isCombat && currentPlayer != null && !currentPlayer.TookDamageThisRoom)
            {
                AchievementManager.instance.OnRoomClearedFlawlessly();

                if (QuestSystem.instance != null) QuestSystem.instance.ReportEvent(QuestType.NoDamageRoom, 1);
            }

            // Judge the per-room oaths. ⚠️ OUTSIDE the flawless-clear block above — this has to run
            // whether or not the player took damage, or an oath would only ever be scored on rooms
            // that also happened to be damage-free. Hub-excluded for the same reason the flawless
            // check is: nothing is spent in the sandbox, so every oath would pass there for free.
            if (isCombat && QuestSystem.instance != null) QuestSystem.instance.EndRoom();

            // Per-room relic payouts (Nest Egg). Placed beside the oath scoring and for exactly the
            // same reasons: outside the flawless-clear block, and hub-excluded.
            if (GameManager.instance != null && GameManager.instance.player != null)
                GameManager.instance.player.ScoreRoomRelics();

            // Straight to the map. No card reward screen — see LevelManager.AdvanceToNextRoom.
            if (LevelManager.instance != null)
            {
                LevelManager.instance.AdvanceToNextRoom();
            }
        }
    }
}