using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Films the game for the trailer. Stages a shot (which room, where the player stands, which cards
/// are in hand), drives the player through <see cref="GameInput"/>, moves a cinematic camera that
/// the gameplay camera never could, and writes every frame to disk at an exact, fixed frame rate.
///
/// The frames are the deliverable; ffmpeg turns them into clips and the cut. See
/// <c>Tools/Trailer/README.md</c> for the pipeline and <see cref="TrailerShots"/> for the shots.
///
/// ⚠️ TIME IS FIXED, NOT REAL. <c>Time.captureFramerate</c> makes Unity advance exactly 1/60 s per
/// rendered frame regardless of how long the frame took to render or write — so a 20-second shot
/// may take two minutes of wall clock and still come out as exactly 1200 evenly spaced frames.
/// Editor hitches, PNG encoding and domain-reload stalls cannot drop or stretch a frame. It also
/// means every wait in a shot is counted in FRAMES; a wall-clock wait would be meaningless here.
///
/// ⚠️ IT RUNS LAST. <c>[DefaultExecutionOrder(1000)]</c> puts its Update after the player's and its
/// LateUpdate after CameraFollow's. That is what makes a one-frame input edge exact (set here, read
/// by the player next frame, cleared here the frame after — one press, never two), and what lets
/// the cinematic camera overwrite the follow camera's result instead of fighting it.
///
/// ⚠️ THE STAGE ROUTINE IS HAND-PUMPED, NOT A UNITY COROUTINE. Unity resumes coroutines at a point
/// in the frame that is not pinned relative to other scripts' Update, which would make the input
/// edge above a coin flip. Pumping it from this Update keeps everything on one clock.
/// </summary>
[DefaultExecutionOrder(1000)]
public class TrailerDirector : MonoBehaviour
{
    public static TrailerDirector instance;

    public const int Fps = 60;

    public static bool IsRunning => instance != null;

    /// <summary>Where frames land: &lt;root&gt;/&lt;shot&gt;/f00001.png. Set by the launcher.</summary>
    public string outputRoot;

    /// <summary>Shots to film this run, in order.</summary>
    public List<TrailerShot> shots = new List<TrailerShot>();

    /// <summary>Called when the last shot has finished (or the run was aborted).</summary>
    public Action<bool> onFinished;

    // Progress, for the editor window.
    public string CurrentShotName { get; private set; } = "";
    public int CurrentShotIndex { get; private set; } = -1;
    public int FramesCaptured { get; private set; }
    public bool Recording { get; private set; }

    // ---- camera ----------------------------------------------------------------------------

    public enum CamMode { Follow, Fixed, Dolly }

    private CamMode camMode = CamMode.Follow;
    private Vector2 camFrom, camTo;
    private float camSizeFrom, camSizeTo;
    private int camFrames, camFrame;
    private AnimationCurve camEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private Vector2 followOffset;      // Follow mode: nudge off the player (e.g. lead the run)
    private float followSize = -1f;    // Follow mode: -1 = leave the room's size alone

    private Camera cam;
    private CameraFollow follow;
    private float defaultOrthoSize;

    // ---- run state -------------------------------------------------------------------------

    private readonly Stack<IEnumerator> stage = new Stack<IEnumerator>();
    private bool aborted;
    private bool jumpEdgeArmed;
    private int shotFrame;             // frames captured in the current shot
    private string shotDir;
    private readonly List<string> log = new List<string>();
    private GameObject hud;
    private float prevFixedDelta;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private static readonly StringBuilder Manifest = new StringBuilder();

    // =====================================================================================
    // Launch
    // =====================================================================================

    /// <summary>
    /// Start filming. Play mode must already be running (rooms only exist at runtime), and the
    /// Game View must already be the capture size — the launcher owns both.
    /// </summary>
    public static TrailerDirector Launch(string outputRoot, List<TrailerShot> shots, Action<bool> onFinished)
    {
        if (instance != null)
        {
            Debug.LogWarning("[Trailer] a recording is already running.");
            return instance;
        }
        var go = new GameObject("TrailerDirector");
        var d = go.AddComponent<TrailerDirector>();
        d.outputRoot = outputRoot;
        d.shots = shots;
        d.onFinished = onFinished;
        return d;
    }

    public void Abort()
    {
        aborted = true;
    }

    private void Awake()
    {
        instance = this;
    }

    // Start, not Awake: AddComponent runs Awake before Launch has assigned outputRoot and shots.
    private void Start()
    {
        cam = Camera.main;
        follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        defaultOrthoSize = cam != null ? cam.orthographicSize : 7f;
        hud = GameObject.Find("GameplayHUD");

        Directory.CreateDirectory(outputRoot);
        Manifest.Length = 0;
        Manifest.Append("{ \"fps\": ").Append(Fps).Append(", \"shots\": [\n");

        // Fixed-step time. The physics step follows so a 60 fps capture is one physics tick per
        // frame — otherwise physics would still advance on real elapsed time and the game would
        // look like it was running at a fraction of speed.
        Time.captureFramerate = Fps;
        prevFixedDelta = Time.fixedDeltaTime;
        Time.fixedDeltaTime = 1f / Fps;

        GameInput.Release();
        GameInput.Scripted = true;

        stage.Push(RunAll());
        started = true;
        Debug.Log("[Trailer] rolling — " + shots.Count + " shot(s) → " + outputRoot);
    }

    private bool started;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (!started) return;
        Time.captureFramerate = 0;
        Time.fixedDeltaTime = prevFixedDelta;
        Time.timeScale = 1f;
        GameInput.Release();
        if (cam != null) cam.orthographicSize = defaultOrthoSize;
        if (hud != null) hud.SetActive(true);
        CleanupSpawned();
    }

    // =====================================================================================
    // The clock
    // =====================================================================================

    private void Update()
    {
        if (!started) return;

        // The jump edge set on the previous frame has now been read by the player once. Clear it.
        if (jumpEdgeArmed) { GameInput.ScriptedJumpDown = false; jumpEdgeArmed = false; }

        if (aborted) { Finish(false); return; }

        // Pump the staging routine one step. `yield return null` = one frame; a float = that many
        // seconds of shot time; an IEnumerator = run it to completion first.
        try
        {
            while (stage.Count > 0)
            {
                IEnumerator top = stage.Peek();
                if (!top.MoveNext()) { stage.Pop(); continue; }

                object cur = top.Current;
                if (cur is IEnumerator sub) { stage.Push(sub); continue; }
                if (cur is float secs) { stage.Push(WaitFrames(Mathf.RoundToInt(secs * Fps))); continue; }
                break; // null: yield a frame
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Trailer] shot '" + CurrentShotName + "' threw: " + e);
            Finish(false);
            return;
        }

        if (stage.Count == 0) { Finish(true); return; }

        if (GameInput.ScriptedJumpDown) jumpEdgeArmed = true;

        if (Recording)
        {
            shotFrame++;
            FramesCaptured++;
            string path = Path.Combine(shotDir, "f" + shotFrame.ToString("D5") + ".png");
            ScreenCapture.CaptureScreenshot(path);
        }
    }

    private void LateUpdate()
    {
        if (!started || cam == null) return;

        switch (camMode)
        {
            case CamMode.Follow:
                if (followSize > 0f) cam.orthographicSize = followSize;
                cam.transform.position += (Vector3)followOffset;
                break;

            case CamMode.Fixed:
                cam.transform.position = new Vector3(camTo.x, camTo.y, cam.transform.position.z);
                cam.orthographicSize = camSizeTo;
                break;

            case CamMode.Dolly:
            {
                float t = camFrames <= 0 ? 1f : Mathf.Clamp01((float)camFrame / camFrames);
                float k = camEase.Evaluate(t);
                Vector2 p = Vector2.LerpUnclamped(camFrom, camTo, k);
                cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
                cam.orthographicSize = Mathf.LerpUnclamped(camSizeFrom, camSizeTo, k);
                camFrame++;
                break;
            }
        }
    }

    private IEnumerator WaitFrames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    private IEnumerator RunAll()
    {
        for (int i = 0; i < shots.Count; i++)
        {
            TrailerShot shot = shots[i];
            CurrentShotIndex = i;
            CurrentShotName = shot.name;
            shotFrame = 0;
            shotDir = Path.Combine(outputRoot, Sanitize(shot.name));
            Directory.CreateDirectory(shotDir);
            log.Clear();

            Debug.Log("[Trailer] ▶ " + shot.name);

            // Every shot starts from the same neutral state, so a shot can never depend on the
            // one before it — reordering the cut must not change what is on screen.
            yield return ResetForShot();

            IEnumerator body = null;
            try { body = shot.stage(this); }
            catch (Exception e) { Debug.LogError("[Trailer] shot '" + shot.name + "' failed to start: " + e); }
            if (body != null) yield return body;

            Cut();

            Manifest.Append(i > 0 ? ",\n" : "")
                    .Append("  { \"name\": \"").Append(Sanitize(shot.name))
                    .Append("\", \"frames\": ").Append(shotFrame)
                    .Append(", \"seconds\": ").Append((shotFrame / (float)Fps).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(", \"note\": \"").Append(Escape(shot.note)).Append("\" }");

            Debug.Log("[Trailer] ■ " + shot.name + " — " + shotFrame + " frames (" + (shotFrame / (float)Fps).ToString("0.00") + "s)");
        }
    }

    private void Finish(bool ok)
    {
        Manifest.Append("\n] }\n");
        try { File.WriteAllText(Path.Combine(outputRoot, "shots.json"), Manifest.ToString()); }
        catch (Exception e) { Debug.LogWarning("[Trailer] could not write manifest: " + e.Message); }

        Debug.Log("[Trailer] " + (ok ? "done" : "ABORTED") + " — " + FramesCaptured + " frames in " + outputRoot);
        Action<bool> cb = onFinished;
        onFinished = null;
        Destroy(gameObject);
        cb?.Invoke(ok);
    }

    private IEnumerator ResetForShot()
    {
        Cut();
        Time.timeScale = 1f;
        GameInput.Release();
        GameInput.Scripted = true;
        CamFollow();
        Hud(true);
        CleanupSpawned();
        if (follow != null) follow.enabled = true;
        yield return null;
    }

    // =====================================================================================
    // Staging vocabulary — what a shot is written in
    // =====================================================================================

    /// <summary>Start writing frames. Everything before this is silent set-up.</summary>
    public void Record() { Recording = true; }

    /// <summary>Stop writing frames.</summary>
    public void Cut() { Recording = false; }

    /// <summary>Wait this many seconds of shot time (exact frames).</summary>
    public IEnumerator Wait(float seconds) { yield return WaitFrames(Mathf.RoundToInt(seconds * Fps)); }

    /// <summary>Show or hide the gameplay HUD. Cinematic shots usually want it gone.</summary>
    public void Hud(bool on)
    {
        if (hud == null) hud = GameObject.Find("GameplayHUD");
        if (hud != null) hud.SetActive(on);
    }

    /// <summary>Slow-motion (or faster) for the rest of the shot. 1 = normal.</summary>
    public void Speed(float timeScale) { Time.timeScale = timeScale; }

    // ---- rooms & actors --------------------------------------------------------------------

    /// <summary>
    /// Spawn a room by prefab name and put the player at its entry point. Resolves through
    /// LevelManager's lists first, then (editor only) by asset search, so a boss hall that lives
    /// only on a CharacterData is still reachable.
    /// </summary>
    public IEnumerator Room(string prefabName)
    {
        GameObject prefab = FindRoomPrefab(prefabName);
        if (prefab == null) throw new Exception("room prefab not found: " + prefabName);
        LevelManager lm = LevelManager.instance;
        if (lm == null) throw new Exception("no LevelManager in scene");

        lm.forcedNextRoom = prefab;
        lm.SpawnNextRoom();

        // ReloadHand runs a 0.2 s coroutine (real coroutine, scaled time — fine under captureFramerate).
        yield return 0.35f;
        Settle();
    }

    /// <summary>Put the player somewhere, standing still. Also becomes the respawn point.</summary>
    public void Place(float x, float y)
    {
        PlayerController p = Player;
        if (p == null) return;
        Vector3 pos = new Vector3(x, y, PlayPlane.Z);
        p.transform.position = pos;
        p.SetCurrentEntryPoint(pos);
        Settle();
    }

    /// <summary>Zero the player's velocity.</summary>
    public void Settle()
    {
        PlayerController p = Player;
        if (p == null) return;
        Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
    }

    /// <summary>Spawn any prefab (an enemy, a prop) for this shot; swept at the next reset.</summary>
    public GameObject Spawn(string prefabName, float x, float y)
    {
        GameObject prefab = FindPrefab(prefabName);
        if (prefab == null) throw new Exception("prefab not found: " + prefabName);
        GameObject go = Instantiate(prefab, new Vector3(x, y, PlayPlane.Z), Quaternion.identity);
        spawned.Add(go);
        return go;
    }

    /// <summary>Deal exactly these cards into the hand (asset names, e.g. "CometDive").</summary>
    public void Hand(params string[] cardNames)
    {
        DeckManager dm = DeckManager.instance;
        if (dm == null) return;
        var list = new List<CardData>();
        foreach (string n in cardNames)
        {
            CardData c = FindCard(n);
            if (c == null) Debug.LogWarning("[Trailer] card not found: " + n);
            else list.Add(c);
        }
        dm.SetHandForTesting(list);
    }

    /// <summary>Set the Shift counter (clamped to max by the player).</summary>
    public void Shift(int value)
    {
        PlayerController p = Player;
        if (p == null) return;
        p.currentShift = 0;
        p.AddShift(value);
    }

    public void Heal() { Player?.Heal(9999f); }

    // ---- driving the player ----------------------------------------------------------------

    /// <summary>Hold a direction (-1 left, +1 right, 0 stop) for this long, then release.</summary>
    public IEnumerator Walk(float dir, float seconds)
    {
        GameInput.ScriptedHorizontal = dir;
        yield return WaitFrames(Mathf.RoundToInt(seconds * Fps));
        GameInput.ScriptedHorizontal = 0f;
    }

    /// <summary>Hold a direction without releasing — pair with Stop().</summary>
    public void Run(float dir) { GameInput.ScriptedHorizontal = dir; }
    public void Stop() { GameInput.ScriptedHorizontal = 0f; }

    /// <summary>Face a direction without moving (one frame of input).</summary>
    public IEnumerator Face(bool right)
    {
        GameInput.ScriptedHorizontal = right ? 1f : -1f;
        yield return null;
        GameInput.ScriptedHorizontal = 0f;
    }

    /// <summary>Press jump. Held for `holdSeconds` so the low-jump cut does not clip it.</summary>
    public IEnumerator Jump(float holdSeconds = 0.25f)
    {
        GameInput.ScriptedJumpDown = true;
        GameInput.ScriptedJumpHeld = true;
        yield return WaitFrames(Mathf.RoundToInt(holdSeconds * Fps));
        GameInput.ScriptedJumpHeld = false;
    }

    /// <summary>Point the (virtual) mouse at a world position — the aim indicator follows it.</summary>
    public void Aim(float x, float y)
    {
        if (cam == null) return;
        GameInput.ScriptedMouse = cam.WorldToScreenPoint(new Vector3(x, y, PlayPlane.Z));
    }

    /// <summary>
    /// Cast a card from the hand by asset name, aimed at a world point. Selects it, gives the aim
    /// indicator a beat to draw (that preview is part of the shot), then casts.
    /// </summary>
    public IEnumerator Cast(string cardName, float aimX, float aimY, float windup = 0.35f)
    {
        DeckManager dm = DeckManager.instance;
        if (dm == null) yield break;

        int index = -1;
        List<RuntimeCard> hand = dm.GetCurrentHand();
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null && hand[i].cardData != null && NameMatches(hand[i].cardData, cardName)) { index = i; break; }
        }
        if (index < 0) { Debug.LogWarning("[Trailer] '" + cardName + "' is not in hand"); yield break; }

        Aim(aimX, aimY);
        dm.SelectCard(index);
        yield return WaitFrames(Mathf.RoundToInt(windup * Fps));
        Aim(aimX, aimY); // the camera may have moved during the wind-up
        dm.TryCastSelectedCard();
    }

    /// <summary>Wait until the player is on the ground (with a cap so a shot cannot hang).</summary>
    public IEnumerator UntilGrounded(float maxSeconds = 3f)
    {
        int cap = Mathf.RoundToInt(maxSeconds * Fps);
        for (int i = 0; i < cap; i++)
        {
            PlayerController p = Player;
            if (p != null && p.IsGroundedCheck()) yield break;
            yield return null;
        }
    }

    /// <summary>Wait until the player has passed this x (in the direction of travel), capped.</summary>
    public IEnumerator UntilX(float x, float maxSeconds = 4f)
    {
        int cap = Mathf.RoundToInt(maxSeconds * Fps);
        bool goingRight = PlayerPos.x <= x;
        for (int i = 0; i < cap; i++)
        {
            float px = PlayerPos.x;
            if (goingRight ? px >= x : px <= x) yield break;
            yield return null;
        }
    }

    /// <summary>Wait until the player is airborne and no longer rising — the top of a jump.</summary>
    public IEnumerator UntilApex(float maxSeconds = 2f)
    {
        int cap = Mathf.RoundToInt(maxSeconds * Fps);
        Rigidbody2D rb = Player != null ? Player.GetComponent<Rigidbody2D>() : null;
        if (rb == null) yield break;
        bool left = false;
        for (int i = 0; i < cap; i++)
        {
            bool grounded = Player.IsGroundedCheck();
            if (!grounded) left = true;
            if (left && rb.linearVelocity.y <= 0.05f) yield break;
            yield return null;
        }
    }

    /// <summary>Wait until the player has left the ground, then landed again.</summary>
    public IEnumerator UntilLanded(float maxSeconds = 3f)
    {
        int cap = Mathf.RoundToInt(maxSeconds * Fps);
        bool left = false;
        for (int i = 0; i < cap; i++)
        {
            bool grounded = Player != null && Player.IsGroundedCheck();
            if (!grounded) left = true;
            if (left && grounded) yield break;
            yield return null;
        }
    }

    // ---- camera ----------------------------------------------------------------------------

    /// <summary>Gameplay camera, optionally zoomed and nudged.</summary>
    public void CamFollow(float size = -1f, float offsetX = 0f, float offsetY = 0f)
    {
        camMode = CamMode.Follow;
        followSize = size;
        followOffset = new Vector2(offsetX, offsetY);
        if (size <= 0f && cam != null) cam.orthographicSize = defaultOrthoSize;
    }

    /// <summary>Locked-off camera.</summary>
    public void CamFixed(float x, float y, float size)
    {
        camMode = CamMode.Fixed;
        camTo = new Vector2(x, y);
        camSizeTo = size;
    }

    /// <summary>A move from A to B over `seconds`, eased. Size interpolates too (push-in / pull-out).</summary>
    public void CamDolly(float x0, float y0, float size0, float x1, float y1, float size1, float seconds, bool linear = false)
    {
        camMode = CamMode.Dolly;
        camFrom = new Vector2(x0, y0);
        camTo = new Vector2(x1, y1);
        camSizeFrom = size0;
        camSizeTo = size1;
        camFrames = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
        camFrame = 0;
        camEase = linear ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    /// <summary>Where the player is right now — handy for building a dolly relative to them.</summary>
    public Vector2 PlayerPos => Player != null ? (Vector2)Player.transform.position : Vector2.zero;

    // =====================================================================================
    // Lookups
    // =====================================================================================

    public PlayerController Player
    {
        get
        {
            if (GameManager.instance != null && GameManager.instance.player != null) return GameManager.instance.player;
            return FindFirstObjectByType<PlayerController>();
        }
    }

    private static bool NameMatches(UnityEngine.Object asset, string name)
    {
        return string.Equals(Normalize(asset.name), Normalize(name), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string s) => s.Replace(" ", "").Replace("_", "");

    private static CardData FindCard(string name)
    {
        CardCatalogue cat = Resources.Load<CardCatalogue>(CardCatalogue.ResourcePath);
        if (cat != null)
            foreach (CardData c in cat.all)
                if (c != null && (NameMatches(c, name) || string.Equals(Normalize(c.cardName ?? ""), Normalize(name), StringComparison.OrdinalIgnoreCase)))
                    return c;
#if UNITY_EDITOR
        return FindAsset<CardData>(name);
#else
        return null;
#endif
    }

    private static GameObject FindRoomPrefab(string name)
    {
        LevelManager lm = LevelManager.instance;
        if (lm != null)
        {
            var pools = new List<GameObject>();
            if (lm.roomPrefabs != null) pools.AddRange(lm.roomPrefabs);
            if (lm.bossRoomPrefabs != null) pools.AddRange(lm.bossRoomPrefabs);
            pools.Add(lm.finalBossRoomPrefab);
            pools.Add(lm.foundryRoomPrefab);
            pools.Add(lm.marketRoomPrefab);
            pools.Add(lm.wellRoomPrefab);
            foreach (GameObject g in pools)
                if (g != null && NameMatches(g, name)) return g;
        }
        foreach (CharacterData c in Resources.LoadAll<CharacterData>("Characters"))
            if (c != null && c.bossRoom != null && NameMatches(c.bossRoom, name)) return c.bossRoom;
        return FindPrefab(name);
    }

    private static GameObject FindPrefab(string name)
    {
#if UNITY_EDITOR
        return FindAsset<GameObject>(name);
#else
        return Resources.Load<GameObject>(name);
#endif
    }

#if UNITY_EDITOR
    private static T FindAsset<T>(string name) where T : UnityEngine.Object
    {
        string filter = (typeof(T) == typeof(GameObject) ? "t:Prefab " : "t:" + typeof(T).Name + " ") + name;
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets(filter))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!NameMatches(Path.GetFileNameWithoutExtension(path), name)) continue;
            T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static bool NameMatches(string fileName, string name) =>
        string.Equals(Normalize(fileName), Normalize(name), StringComparison.OrdinalIgnoreCase);
#endif

    private void CleanupSpawned()
    {
        foreach (GameObject g in spawned) if (g != null) Destroy(g);
        spawned.Clear();
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}

/// <summary>One shot: a name and the routine that stages, records and ends it.</summary>
public class TrailerShot
{
    public string name;
    public string note;
    public Func<TrailerDirector, IEnumerator> stage;

    public TrailerShot(string name, string note, Func<TrailerDirector, IEnumerator> stage)
    {
        this.name = name;
        this.note = note;
        this.stage = stage;
    }
}
