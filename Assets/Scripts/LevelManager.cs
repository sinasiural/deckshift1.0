using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Header("Referanslar")]
    public Transform playerTransform;

    [Header("Oda Ayarları")]
    [Tooltip("Element 0 must be the hub. The rest are the run's combat levels. Add a RoomTier " +
             "component to a room prefab to bind it to one map tier; untagged rooms serve any tier.")]
    public List<GameObject> roomPrefabs;
    // ⚠️ WAS A SINGLE `bossRoomPrefab` FOR "the act finale". Acts are gone: a run now carries
    // several OPTIONAL bosses the player routes into or around, plus one unique finale.
    [Tooltip("The optional bosses a run can offer. The map places 2-5 of these as nodes you may " +
             "route into or avoid; each is drawn without repeating one already fought this run. " +
             "One entry is fine — it will simply repeat once the pool is exhausted.")]
    public List<GameObject> bossRoomPrefabs = new List<GameObject>();

    [Tooltip("The run's terminus, always fought, never drawn from the pool above. Empty falls back " +
             "to a pool boss so a run can still be finished.")]
    public GameObject finalBossRoomPrefab;

    [Header("Recharge rooms (map attachments)")]
    [Tooltip("Scrap: repair and salvage cards, Blompo. LEAVE EMPTY AND NO FOUNDRY IS EVER DRAWN ON " +
             "THE MAP — the map never promises a room it cannot spawn.")]
    public GameObject foundryRoomPrefab;
    [Tooltip("Gold: the shop. Leave empty and no Market is ever drawn on the map.")]
    public GameObject marketRoomPrefab;
    [Tooltip("Shift and healing. Leave empty and no Well is ever drawn on the map.")]
    public GameObject wellRoomPrefab;

    [Header("Tutorial")]
    [Tooltip("Spawned INSTEAD of the hub when the main menu starts the tutorial (TutorialMode). Its " +
             "exit returns to the main menu. Built by Deckshift → Build Tutorial Room.")]
    public GameObject tutorialRoomPrefab;

    private GameObject currentRoom;
    private bool hasSpawnedFirstRoom = false;

    // Rooms already spawned this run, so an act doesn't repeat a layout while it still has unused ones.
    private readonly List<GameObject> usedRoomPrefabs = new List<GameObject>();

    // A node's recharge room is entered AFTER its combat room and is NOT a floor, so it spawns
    // without advancing the map. Held here between the two spawns.
    private RechargeType pendingRecharge = RechargeType.None;

    // State for the pre-map room order, kept only as the fallback below.
    private List<int> availableRoomIndices = new List<int>();
    private bool bossSpawned = false;

    // Optional bosses already met this run, so a five-boss route fights five different ones rather
    // than the same arena repeatedly. Cleared with the rest of the run state.
    private readonly List<GameObject> usedBossPrefabs = new List<GameObject>();

    /// <summary>
    /// The next optional boss. Draws without repeating until the pool is exhausted, then resets —
    /// so a project with one authored boss still works, it just repeats, rather than failing.
    /// </summary>
    // The arena where the played character is the boss — their own mirror, held for the finale.
    // Read live, never cached: a character swap must not leave a stale room behind.
    private static GameObject OwnMirrorRoom =>
        CharacterSelection.Chosen != null ? CharacterSelection.Chosen.bossRoom : null;

    /// <summary>
    /// The run's terminus. The played character's own boss room when they have one (you fight
    /// yourself at the top of the castle); otherwise the shared `finalBossRoomPrefab`; otherwise a
    /// pool draw, so a project with no finale authored still ends the run rather than stranding it.
    /// </summary>
    private GameObject PickFinaleRoom()
    {
        GameObject mirror = OwnMirrorRoom;
        if (mirror != null) return mirror;
        return finalBossRoomPrefab != null ? finalBossRoomPrefab : PickBossRoom();
    }

    private GameObject PickBossRoom()
    {
        if (bossRoomPrefabs == null || bossRoomPrefabs.Count == 0)
        {
            // No boss authored at all. Falling back to a normal room is far better than spawning
            // nothing: an empty node would strand the run with no exit door.
            Debug.LogWarning("[LevelManager] a Boss node was reached but bossRoomPrefabs is empty — " +
                             "spawning an ordinary room instead.");
            return PickRoomForTier(MapNodeType.Elite);
        }

        // ⚠️ YOUR OWN MIRROR IS NEVER A MID-MAP BOSS. Every character is also a boss; the one made
        // from the character you are playing is held back for the finale (bosses-are-characters).
        GameObject mirror = OwnMirrorRoom;

        List<GameObject> fresh = new List<GameObject>();
        foreach (GameObject g in bossRoomPrefabs)
            if (g != null && g != mirror && !usedBossPrefabs.Contains(g)) fresh.Add(g);

        if (fresh.Count == 0)
        {
            usedBossPrefabs.Clear();
            foreach (GameObject g in bossRoomPrefabs) if (g != null && g != mirror) fresh.Add(g);
        }
        // A roster of one, whose only boss is the mirror: better to meet yourself early than to
        // reach a Boss node with nothing in it.
        if (fresh.Count == 0 && mirror != null) fresh.Add(mirror);
        if (fresh.Count == 0) return roomPrefabs != null && roomPrefabs.Count > 0 ? roomPrefabs[0] : null;

        GameObject pick = fresh[Random.Range(0, fresh.Count)];
        usedBossPrefabs.Add(pick);
        return pick;
    }

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
    }

    private void Start()
    {
        SpawnNextRoom();
    }

    // Queue every non-hub level (indices 1..n). The hub (0) is the always-first room and the boss
    // is a separate prefab, so neither belongs in this queue.
    private void BuildLevelQueue()
    {
        availableRoomIndices.Clear();
        for (int i = 1; i < roomPrefabs.Count; i++)
            availableRoomIndices.Add(i);
    }

    // Run order is now driven by the RunMap: hub → the branch the player picked, floor by floor →
    // boss → loop back to a fresh act. RunMapManager owns the graph and the position in it; this
    // method only translates "which node am I on" into "which prefab do I spawn".
    private GameObject PickNextRoomPrefab()
    {
        if (roomPrefabs == null || roomPrefabs.Count == 0) return null;

        RunMapManager mapMgr = RunMapManager.instance;
        if (mapMgr == null) return PickNextRoomPrefabWithoutMap();

        // 1) First room of the run: build the act, then the hub. The hub is the map's Start node,
        //    so entering it here and entering it on the map are the same event.
        if (!hasSpawnedFirstRoom)
        {
            hasSpawnedFirstRoom = true;
            usedRoomPrefabs.Clear();
            pendingRecharge = RechargeType.None;
            mapMgr.BeginRun(SpawnableRecharges());
            mapMgr.EnterStart();
            // The tutorial stands in for the hub as the first room. The map is still generated, so
            // the tutorial's "press M" sign opens a real map rather than nothing.
            if (TutorialMode.ConsumeRequest() && tutorialRoomPrefab != null) return tutorialRoomPrefab;
            return roomPrefabs[0];
        }

        // 2) A recharge room hangs off the node the player just cleared. It is not a floor, so it
        //    spawns WITHOUT advancing the map.
        if (pendingRecharge != RechargeType.None)
        {
            GameObject recharge = RechargeRoomPrefab(pendingRecharge);
            pendingRecharge = RechargeType.None;
            if (recharge != null) return recharge;
        }

        // 3) Move onto the next node.
        MapNode node = mapMgr.AdvanceToNext();

        // 4) Nothing left above the boss → the act is over, start a fresh one from the hub.
        if (node == null)
        {
            hasSpawnedFirstRoom = false;
            mapMgr.ClearMap();
            return PickNextRoomPrefab();
        }

        // An OPTIONAL boss the player routed into. Drawn from the boss pool without repeating one
        // already fought this run, so a five-boss route meets five different bosses.
        if (node.type == MapNodeType.Boss)
            return PickBossRoom();

        // The run's terminus. Deliberately its own prefab slot and never drawn from the pool — the
        // designer's stated intent is that the final fight is unique.
        if (node.type == MapNodeType.FinalBoss)
            return PickFinaleRoom();

        pendingRecharge = node.recharge;
        return PickRoomForTier(node.type);
    }

    // Which recharge rooms this project can currently spawn. The map is generated against exactly
    // this list, so an unassigned slot means that room type simply never appears — rather than
    // appearing as an icon the player routes toward and finds empty.
    private List<RechargeType> SpawnableRecharges()
    {
        List<RechargeType> list = new List<RechargeType>();
        if (foundryRoomPrefab != null) list.Add(RechargeType.Foundry);
        if (marketRoomPrefab != null) list.Add(RechargeType.Market);
        if (wellRoomPrefab != null) list.Add(RechargeType.Well);
        return list;
    }

    // --- Out-of-bounds safety net --------------------------------------------------------------
    //
    // ⚠️ PHASE CAN TAKE THE PLAYER OUT OF THE LEVEL ENTIRELY, and before this there was nothing to
    // catch them: the only fall handling in the game is a `DeathZone`-tagged trigger placed by hand
    // under each room. Phase turns off collision with the world, so the player can leave SIDEWAYS or
    // upward, miss the DeathZone completely, and then fall forever in empty space with the run
    // unrecoverable. The designer hit exactly this.
    //
    // The fix is deliberately not "put a bigger DeathZone in every room" — that is per-room authoring
    // that a new room will forget. The room's own geometry already says where the level is, so the
    // bounds are measured on spawn and anything far outside them is out of play, in any direction,
    // for any reason (Phase, Portal, a knockback through a gap, a future card nobody has written).
    //
    // Measured from RENDERERS, not colliders: a room's backdrop and decoration mark its extent even
    // where nothing is solid, and the margin is generous enough that legitimate play near an edge
    // can never trip it.
    [Header("Out of bounds")]
    [Tooltip("How far outside the room's own geometry the player may get before being returned to the entry point. Generous on purpose — this is a safety net, not a boundary.")]
    public float outOfBoundsMargin = 14f;

    private Bounds roomBounds;
    private bool hasRoomBounds;

    private void MeasureRoomBounds()
    {
        hasRoomBounds = false;
        if (currentRoom == null) return;

        bool any = false;
        Bounds b = new Bounds();
        foreach (Renderer r in currentRoom.GetComponentsInChildren<Renderer>(true))
        {
            // Particles and trails wander far outside the room and would inflate it into uselessness.
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!any) return;
        roomBounds = b;
        hasRoomBounds = true;
    }

    private void Update()
    {
        if (!hasRoomBounds || playerTransform == null) return;
        if (GameManager.instance == null || GameManager.instance.currentState != GameState.Playing) return;

        PlayerController pc = GameManager.instance.player;
        if (pc == null || pc.IsDead) return;

        Vector3 p = playerTransform.position;
        Bounds safe = roomBounds;
        safe.Expand(outOfBoundsMargin * 2f);   // Expand takes a total size delta, not a per-side one

        if (safe.Contains(new Vector3(p.x, p.y, safe.center.z))) return;

        Debug.LogWarning($"Player left the room at {p} (room {roomBounds.min}..{roomBounds.max}) — returning to the entry point.");
        pc.ReturnToEntryPoint();
    }

    private GameObject RechargeRoomPrefab(RechargeType type)
    {
        switch (type)
        {
            case RechargeType.Foundry: return foundryRoomPrefab;
            case RechargeType.Market: return marketRoomPrefab;
            case RechargeType.Well: return wellRoomPrefab;
            default: return null;
        }
    }

    // Picks a room built for this tier, preferring one that hasn't been used yet this run.
    private GameObject PickRoomForTier(MapNodeType tier)
    {
        GameObject pick = TryPickRoomForTier(tier);
        if (pick != null) { usedRoomPrefabs.Add(pick); return pick; }

        // Everything eligible has been used. Repeat a layout rather than spawn nothing: with only
        // seven combat rooms an act can legitimately outlast the pool, and a missing room is an
        // unfinishable run.
        usedRoomPrefabs.Clear();
        pick = TryPickRoomForTier(tier);
        if (pick != null) { usedRoomPrefabs.Add(pick); return pick; }

        Debug.LogError("LevelManager: no combat room available — roomPrefabs needs at least one level after the hub.");
        return null;
    }

    private GameObject TryPickRoomForTier(MapNodeType tier)
    {
        List<GameObject> tagged = new List<GameObject>();
        List<GameObject> untagged = new List<GameObject>();

        for (int i = 1; i < roomPrefabs.Count; i++)
        {
            GameObject room = roomPrefabs[i];
            if (room == null || usedRoomPrefabs.Contains(room)) continue;

            RoomTier rt = room.GetComponent<RoomTier>();
            if (rt == null) untagged.Add(room);
            else if (rt.Serves(tier)) tagged.Add(room);
        }

        // Prefer a room actually authored for this tier; otherwise any untagged room will do, which
        // is what keeps the map working with rooms that predate RoomTier.
        List<GameObject> pool = tagged.Count > 0 ? tagged : untagged;
        if (pool.Count == 0) return null;

        return pool[Random.Range(0, pool.Count)];
    }

    // The pre-map room order, used only if RunMapManager is somehow absent. It should be
    // unreachable — RunMapManager bootstraps itself — but a missing manager silently reverting to
    // random rooms would look almost right, so this stays as a named, obvious fallback.
    private GameObject PickNextRoomPrefabWithoutMap()
    {
        if (!hasSpawnedFirstRoom)
        {
            hasSpawnedFirstRoom = true;
            BuildLevelQueue();
            if (TutorialMode.ConsumeRequest() && tutorialRoomPrefab != null) return tutorialRoomPrefab;
            return roomPrefabs[0];
        }

        if (availableRoomIndices.Count > 0)
        {
            int pick = Random.Range(0, availableRoomIndices.Count);
            int idx = availableRoomIndices[pick];
            availableRoomIndices.RemoveAt(pick);
            return roomPrefabs[idx];
        }

        if (!bossSpawned)
        {
            GameObject finale = PickFinaleRoom();
            if (finale != null) { bossSpawned = true; return finale; }
        }

        hasSpawnedFirstRoom = false;
        bossSpawned = false;
        return PickNextRoomPrefabWithoutMap();
    }

    // Leaving a room through the ExitDoor. THE MAP IS THE ONLY THING THAT OPENS HERE.
    //
    // ⚠️ This logic used to live in RewardManager.FinishReward, because a card reward screen was
    // forced on the player between every pair of rooms and the map choice was bolted onto the end of
    // it. That screen is gone (designer 2026-08-09: cards come from chests placed in levels, so
    // taking one is a decision the player makes, not a toll they pay). The map hook had to move with
    // it — left in RewardManager it would simply have stopped running, and a missing route choice
    // does not error, it silently falls back to random room order.
    //
    // ⚠️ THE MAP OPENS ON EVERY ROOM CHANGE, INCLUDING WHEN THERE IS ONLY ONE WAY ON.
    //
    // It used to be gated on RunMapManager.NeedsRouteChoice, on the reasoning that a screen with a
    // single button is ceremony rather than a decision. That reasoning was wrong in practice, and
    // the designer reported the symptom as "the map works weird — I open it and I'm 2-3 floors ahead
    // of where I should be". Measured over 200 generated acts: **62% of room transitions offer
    // exactly one option**, and planning a branch with M suppressed the screen for a further one. So
    // the player crossed several rooms without the map ever appearing, and every time they opened it
    // they had moved without seeing it happen.
    //
    // Nothing was corrupt — the same 200 acts produced 0 invalid maps, 0 dead ends, and every step
    // advanced by exactly one floor. The bug was that the run's only sense of PLACE was being hidden
    // whenever it had nothing to ask. Orientation is worth more than the saved click.
    //
    // ⚠️ THE ZERO-OPTIONS GUARD IS LOad-BEARING. On the boss node AvailableNext() is empty, and the
    // map opened for a choice refuses Escape and the backdrop (that's what makes a required choice
    // required) — so opening it with nothing clickable would be an unescapable screen.
    public void AdvanceToNextRoom()
    {
        // The tutorial is a room with no run behind it: its exit goes back to the main menu.
        if (IsCurrentRoomTutorial())
        {
            TutorialMode.Finish();
            return;
        }

        RunMapManager mgr = RunMapManager.instance;

        if (mgr == null || !mgr.HasMap || mgr.AvailableNext().Count == 0)
        {
            SpawnNextRoom();
            return;
        }

        // The node's recharge room is NEXT no matter what — it hangs off the room just cleared and
        // is not a floor — so there is nothing to choose yet. Asking here made the player pick a
        // branch and then walk into a Well instead (designer, 2026-09-14). The map opens on the
        // recharge room's exit instead, when the choice is real.
        if (pendingRecharge != RechargeType.None && RechargeRoomPrefab(pendingRecharge) != null)
        {
            SpawnNextRoom();
            return;
        }

        RunMapScreen.OpenForChoice(SpawnNextRoom);
    }

    // Wipes everything the departing room put into the scene but does NOT own.
    //
    // ⚠️ DESTROYING `currentRoom` ONLY DESTROYS WHAT IS PARENTED UNDER IT. Anything spawned at
    // runtime with `Instantiate(prefab)` and no parent becomes a SCENE-ROOT object and simply
    // survives the room change — it then turns up in the next room, and in the hub. Reported by the
    // designer twice over: enemy health bars floating in later rooms, and the Moss Knight's summoned
    // slimes following the player into the hub when the fight was left unfinished.
    //
    // The `TemporaryObject` marker was the existing answer, and it is the wrong SHAPE of answer on
    // its own: it only cleans up things whose author remembered to stamp them, so every future
    // runtime spawn is one forgotten line away from the same bug. Sweeping by TYPE covers the
    // spawns nobody remembered, including ones not written yet.
    //
    // Sweeping every enemy unconditionally is safe because the room is destroyed on the very next
    // line — the room's own enemies were going anyway. Note these are destroyed, not killed: Die()
    // never runs, so leaving a room deliberately pays no scrap and completes no bounty.
    private void ClearRuntimeSpawns()
    {
        foreach (TemporaryObject obj in FindObjectsByType<TemporaryObject>(FindObjectsSortMode.None))
            Destroy(obj.gameObject);

        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            Destroy(enemy.gameObject);

        // Health bars are parentless by design (see EnemyHealthBar). They also delete themselves
        // once their target is gone, so this is belt-and-braces — it just gets them in the same
        // frame rather than on the next one.
        foreach (EnemyHealthBar bar in FindObjectsByType<EnemyHealthBar>(FindObjectsSortMode.None))
            Destroy(bar.gameObject);

        // Anything still in flight. A bolt that outlives its shooter would otherwise arrive in the
        // next room and hit the player there.
        foreach (Projectile shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            Destroy(shot.gameObject);
    }

    /// <summary>
    /// Testing only: makes the NEXT SpawnNextRoom use this prefab instead of the map's choice.
    /// Cleared the moment it is used. Never serialized — see the note at the use site.
    /// </summary>
    [System.NonSerialized] public GameObject forcedNextRoom;

    public void SpawnNextRoom()
    {
        // Room-end Held payoffs (Dead Weight): fire while the ending room's hand still
        // exists — the ReloadHand below discards it. Only when actually leaving a combat
        // room: not on the first spawn (currentRoom null), not when leaving a sandbox (hub or
        // recharge room — a held Dead Weight must not pay a second time on the Well's exit).
        if (currentRoom != null && !IsCurrentRoomSandbox() && DeckManager.instance != null)
            DeckManager.instance.OnRoomEnd();

        ClearRuntimeSpawns();

        if (currentRoom != null) Destroy(currentRoom);

        // ⚠️ TESTING HOOK. When set, the next spawn uses this room instead of asking the map — and it
        // is CLEARED IMMEDIATELY, so it can only ever affect one room change. It is [NonSerialized]
        // on purpose: it can never be saved into a scene or prefab and quietly hijack a real run,
        // which is the failure mode `roomPrefabs` has suffered four times.
        GameObject selectedRoomPrefab;
        if (forcedNextRoom != null)
        {
            selectedRoomPrefab = forcedNextRoom;
            forcedNextRoom = null;
            Debug.Log($"[LevelManager] FORCED room: {selectedRoomPrefab.name} (testing hook)");
        }
        else selectedRoomPrefab = PickNextRoomPrefab();

        if (selectedRoomPrefab == null)
        {
            Debug.LogError("LevelManager: no room prefab to spawn (is roomPrefabs empty?).");
            return;
        }

        MapNode at = RunMapManager.instance != null ? RunMapManager.instance.CurrentNode : null;
        Debug.Log($"Spawning room: {selectedRoomPrefab.name}" + (at != null ? $" — map node {at}" : " — no map"));

        currentRoom = Instantiate(selectedRoomPrefab, Vector3.zero, Quaternion.identity);

        // A character's own boss room spawned for THAT character is the finale — the boss dials up
        // (see IMirrorBoss). Everyone else meets the same room as an ordinary mid-map boss.
        if (selectedRoomPrefab == OwnMirrorRoom)
            foreach (IMirrorBoss mirror in currentRoom.GetComponentsInChildren<IMirrorBoss>(true))
                mirror.SetFinale(true);

        // Put every actor on the shared draw plane and shove decoration behind it. Opaque sprites
        // sort by camera depth, not sortingOrder, and each room had been authored at its own Z —
        // which is why the player and enemies sometimes rendered behind props. See PlayPlane.
        PlayPlane.Apply(currentRoom);

        Transform boundsObj = currentRoom.transform.Find("CameraBounds");
        if (boundsObj != null)
        {
            Debug.Log("CameraBounds bulundu: " + boundsObj.name);
            BoxCollider2D[] zones = boundsObj.GetComponentsInChildren<BoxCollider2D>();
            Debug.Log("Zone sayısı: " + zones.Length);
            CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
            if (cam != null)
            {
                cam.SetZones(zones);
                Debug.Log("SetZones çağrıldı!");
            }
            else
            {
                Debug.LogError("CameraFollow bulunamadı!");
            }
        }
        else
        {
            Debug.LogError("CameraBounds objesi bulunamadı!");
        }

        // Per-room camera size (RoomCamera on the room root, same convention as HubMarker).
        // Pushed on EVERY spawn, deliberately OUTSIDE the CameraBounds block above: a room that
        // only ever SET the size would leave the boss arena's framing on for the rest of the run.
        // Passing 0 for a room with no override is what restores the default.
        CameraFollow follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            RoomCamera roomCam = currentRoom.GetComponent<RoomCamera>();
            follow.SetRoomSize(roomCam != null ? roomCam.orthographicSize : 0f);
        }

        MeasureRoomBounds();

        Transform entryPoint = currentRoom.transform.Find("GirisNoktasi");
        if (entryPoint != null && playerTransform != null)
        {
            // ⚠️ Take X and Y from the entry point but NOT its Z. Rooms disagree wildly about depth
            // (spawn Z ranged from -1.06 to +2.56 across the pool), and copying the full Vector3 is
            // what put the player on a different plane in every room. The player belongs on the
            // play plane, always.
            Vector3 spawn = new Vector3(entryPoint.position.x, entryPoint.position.y, PlayPlane.Z);
            playerTransform.position = spawn;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.OnNewRoomEnter();
                playerController.SetCurrentEntryPoint(spawn);
            }
        }

        if (DeckManager.instance != null)
        {
            DeckManager.instance.ReloadHand();
            DeckManager.instance.ResetRecallCost();
            DeckManager.instance.ResetRoomRelicState();
        }

        // Per-room relic triggers (Pocket Battery, Flux Regulator, ...).
        if (RelicManager.instance != null)
            RelicManager.instance.OnRoomStart();
    }

    // Returns true when the active room has a HubMarker on its root.
    // Uses currentRoom — the single authoritative field set by SpawnNextRoom.
    public bool IsCurrentRoomHub()
    {
        if (currentRoom == null) return false;
        return currentRoom.GetComponent<HubMarker>() != null;
    }

    // Returns true when the active room has a RechargeRoomMarker on its root (Foundry / Market /
    // Well). Read from the PREFAB rather than from pendingRecharge so it stays right for a room
    // spawned through the forcedNextRoom testing hook, or by any future path that skips the map.
    public bool IsCurrentRoomRecharge()
    {
        if (currentRoom == null) return false;
        return currentRoom.GetComponent<RechargeRoomMarker>() != null;
    }

    // The one test for "does leaving this room count as clearing a room?" — used by the exit
    // door's per-room payouts (flawless clear, oaths, Nest Egg). Neither the hub nor a recharge
    // room has anything at stake, so neither may pay out. Prefer this over checking the hub alone.
    public bool IsCurrentRoomCombat()
    {
        if (currentRoom == null) return false;
        return !IsCurrentRoomHub() && !IsCurrentRoomRecharge() && !IsCurrentRoomTutorial();
    }

    // True in the tutorial room (TutorialRoom on its root). ⚠️ Deliberately NOT a sandbox: the designer
    // wants jumps to cost Shift there so the player sees the counter fall and learns it does not come
    // back. What the tutorial waives instead is card CHARGES (DeckManager) and DEATH (PlayerHealth), the
    // two things that could strand a new player in a room they cannot finish. Not combat either, so
    // leaving it pays no flawless clear, oath step or Nest Egg.
    public bool IsCurrentRoomTutorial()
    {
        if (currentRoom == null) return false;
        return currentRoom.GetComponent<TutorialRoom>() != null;
    }

    // THE UMBRELLA RULE'S TEST. True in the hub AND in a recharge room (designer, 2026-09-14:
    // "the recharge rooms should not waste anything, they should be more like a sandbox level,
    // just like the hub"). Every consumption site — Shift on jumps, cards, Recall, altars, card
    // charges, Stagger, blessing payouts — gates on THIS, never on IsCurrentRoomHub() directly, so
    // a new kind of free room is one line here rather than a hunt through a dozen files.
    public bool IsCurrentRoomSandbox()
    {
        if (currentRoom == null) return false;
        return IsCurrentRoomHub() || IsCurrentRoomRecharge();
    }
}