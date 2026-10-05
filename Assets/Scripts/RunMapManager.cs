using System.Collections.Generic;
using UnityEngine;

// Owns the current act's RunMap and the player's position in it.
//
// SELF-BOOTSTRAPPING. It creates itself via RuntimeInitializeOnLoadMethod rather than needing a
// GameObject placed in SampleScene, because "the system exists in code but isn't in the scene" is
// this project's single most recurring bug (CameraShake was disabled for nine months; QuestSystem
// went missing from SampleScene entirely). A map that silently doesn't exist would fall back to the
// old random room order and look almost right, which is the worst possible failure mode. Same
// pattern as ScrapHUD.
//
// It is scene-local like every other manager here — no DontDestroyOnLoad. A map is per-run by
// design and must reset on death, exactly like QuestSystem's quests.
public class RunMapManager : MonoBehaviour
{
    public static RunMapManager instance;

    [Header("Act shape")]
    public RunMapSettings settings = new RunMapSettings();

    [Tooltip("0 = random each run. Set non-zero to regenerate the same act every time, which is " +
             "what you want while tuning or reproducing a bug.")]
    public int fixedSeed = 0;

    private RunMap map;

    // The node the player picked on the map screen, consumed by the next room spawn. -1 means
    // "nothing chosen" — see AdvanceToNext for what happens then.
    private int chosenNextId = -1;

    public RunMap Map => map;
    public bool HasMap => map != null;
    public MapNode CurrentNode => map?.Current;

    // Raised whenever the map is generated or the player moves, so the map screen and any HUD
    // marker can rebuild without polling.
    public event System.Action OnMapChanged;

    // ⚠️ Registered through SceneBootstrap, NOT called directly. RuntimeInitializeOnLoadMethod runs
    // once per play session, so creating the manager here alone meant that after the player died
    // (SampleScene -> GameOverScene) and restarted (-> SampleScene), this manager was destroyed by
    // the scene load and never came back. The map then silently vanished for the rest of the
    // session and LevelManager fell back to random room order — precisely the "looks almost right"
    // failure this class's own header warns about. See SceneBootstrap.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneBootstrap.Register(Create);
    }

    private static void Create()
    {
        if (instance != null) return;
        if (FindFirstObjectByType<RunMapManager>() != null) return;

        GameObject go = new GameObject("RunMapManager");
        go.AddComponent<RunMapManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        // Bake the map's paper here rather than on the first press of M. Parchment caches every
        // sprite in a static, so this costs nothing after the first scene load — but it is ~100ms
        // of pixel work, and paying it during a scene load nobody is looking at is free where
        // paying it the moment a screen is asked for is a visible stutter.
        Parchment.Prewarm();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Generates a fresh act. `allowed` is the set of recharge rooms LevelManager can actually
    // spawn — see RunMapSettings.allowedRecharges for why the map refuses to draw the others.
    public void BeginRun(List<RechargeType> allowed)
    {
        settings.allowedRecharges = allowed ?? new List<RechargeType>();

        int seed = fixedSeed != 0 ? fixedSeed : Random.Range(1, int.MaxValue);
        map = RunMapGenerator.Generate(settings, seed);

        string error;
        if (!map.Validate(out error))
        {
            // A structurally broken act means a run that cannot be completed. Say so loudly rather
            // than letting the player discover it three floors in.
            Debug.LogError($"RunMapManager: generated an INVALID act (seed {seed}): {error}");
        }

        chosenNextId = -1;
        OnMapChanged?.Invoke();
    }

    // Moves onto the act's Start node (the hub). Kept separate from BeginRun so "the map exists"
    // and "the player is standing on it" are never the same question.
    public void EnterStart()
    {
        if (map == null) return;
        MapNode s = map.StartNode;
        if (s != null) map.TravelTo(s.id);
        OnMapChanged?.Invoke();
    }

    public List<MapNode> AvailableNext()
    {
        return map != null ? map.AvailableNext() : new List<MapNode>();
    }

    // Called by the map screen when the player commits to a branch. Rejects anything not actually
    // reachable, so a mis-wired button cannot teleport the run.
    public bool ChooseNext(int nodeId)
    {
        if (map == null || !map.CanTravelTo(nodeId)) return false;
        chosenNextId = nodeId;
        OnMapChanged?.Invoke();
        return true;
    }

    public bool HasChosenNext => chosenNextId >= 0 && map != null && map.CanTravelTo(chosenNextId);

    // -1 when nothing is chosen. The map screen draws this branch as committed.
    public int ChosenNextId => HasChosenNext ? chosenNextId : -1;

    // True when the run cannot sensibly continue until the player picks a branch, so the map has
    // to be put in front of them rather than silently rolling for them.
    //
    // Deliberately FALSE when there is only one option: a forced screen with a single button is
    // ceremony, not a decision. Also false once the player has already planned ahead with M, which
    // is what makes planning worth doing instead of being asked twice.
    public bool NeedsRouteChoice
    {
        get
        {
            if (map == null) return false;
            if (HasChosenNext) return false;
            return map.AvailableNext().Count > 1;
        }
    }

    // The map key lives here rather than on the screen because this manager is always present and
    // always active, while the screen deactivates itself when closed and so cannot listen for the
    // key that reopens it.
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) RunMapScreen.Toggle();
    }

    // Advances the run one node and returns where it landed, or null when the act is over.
    //
    // If the player has not chosen, this picks at random rather than blocking. That is a
    // deliberate stopgap while the map SCREEN is still being built: the run stays playable and
    // every commit ships. Once the screen exists the player will always have chosen, and a random
    // pick becomes unreachable in normal play — it stays only as the "player somehow didn't
    // choose" guard.
    public MapNode AdvanceToNext()
    {
        if (map == null) return null;

        List<MapNode> options = map.AvailableNext();
        if (options.Count == 0) return null;

        int target = HasChosenNext ? chosenNextId : options[Random.Range(0, options.Count)].id;
        chosenNextId = -1;

        if (!map.TravelTo(target)) return null;

        OnMapChanged?.Invoke();
        return map.Current;
    }

    // True once the player has reached the FINAL boss — the run is over.
    //
    // ⚠️ Acts no longer exist (designer, 2026-08-21), and this used to be true at any Boss node
    // because Boss meant "the act finale". A run now passes THROUGH optional bosses; only the
    // terminus ends it.
    public bool IsRunFinished => map != null && map.IsFinished;

    /// <summary>How many optional bosses this run has taken on. Zero is a legitimate run.</summary>
    public int BossesDefeated => map != null ? map.BossesDefeated : 0;

    /// <summary>How many optional bosses this map is offering in total.</summary>
    public int BossesOffered => map != null ? map.BossNodes.Count : 0;

    public void ClearMap()
    {
        map = null;
        chosenNextId = -1;
        OnMapChanged?.Invoke();
    }
}
