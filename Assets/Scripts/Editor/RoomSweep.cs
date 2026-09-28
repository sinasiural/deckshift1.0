using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Deckshift -> Test -> Sweep Every Room   (play mode, in SampleScene)
//
// Spawns every room a run can reach — the pool, the bosses, the recharge rooms and the tutorial —
// one after another, lets each one run for a couple of seconds, and reports what went wrong while it
// was up: every error and warning logged, whether the player ended up falling out of the room, and
// whether the room has an exit. Built for the demo pass (2026-09-28): the cheapest way to find a room
// that throws before a playtester does.
//
// It goes through LevelManager.forcedNextRoom, the same one-shot hook as the Play Room menu, so it
// touches no pool list. The player is made invincible for the sweep (play-mode state, gone on Stop).
public static class RoomSweep
{
    const double Dwell = 2.0;          // real seconds each room runs
    const float FellBelow = 12f;       // player this far under the spawn point = fell out of the room

    struct Finding { public string room; public List<string> problems; }

    static readonly List<GameObject> queue = new List<GameObject>();
    static readonly List<Finding> findings = new List<Finding>();
    static List<string> current;
    static int index;
    static double nextAt;
    static float spawnY;
    static bool running;

    [MenuItem("Deckshift/Test/Sweep Every Room")]
    static void Run()
    {
        if (!EditorApplication.isPlaying || LevelManager.instance == null)
        {
            EditorUtility.DisplayDialog("Sweep Every Room",
                "Press Play in SampleScene first, then run this again.\n\n" +
                "It spawns every room the demo can reach, one after another (about 2 seconds each), " +
                "and reports anything that went wrong in the Console.", "OK");
            return;
        }
        if (running) return;

        var lm = LevelManager.instance;
        queue.Clear();
        findings.Clear();
        void Add(GameObject g) { if (g != null && !queue.Contains(g)) queue.Add(g); }
        for (int i = 1; i < lm.roomPrefabs.Count; i++) Add(lm.roomPrefabs[i]);   // [0] is the hub
        if (lm.roomPrefabs.Count > 0) Add(lm.roomPrefabs[0]);
        foreach (var b in lm.bossRoomPrefabs) Add(b);
        Add(lm.finalBossRoomPrefab);
        Add(lm.foundryRoomPrefab);
        Add(lm.marketRoomPrefab);
        Add(lm.wellRoomPrefab);
        Add(lm.tutorialRoomPrefab);

        var ph = GameManager.instance != null && GameManager.instance.player != null
            ? GameManager.instance.player.GetComponent<PlayerHealth>() : null;
        if (ph != null) ph.isInvincible = true;

        index = -1;
        nextAt = 0;
        running = true;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        Debug.Log($"[RoomSweep] {queue.Count} rooms, ~{queue.Count * Dwell:0}s. Hands off until the report.");
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (current == null || type == LogType.Log) return;
        if (message.StartsWith("[RoomSweep]")) return;
        string line = type + ": " + message.Split('\n')[0];
        if (!current.Contains(line)) current.Add(line);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop("play mode ended before the sweep finished"); return; }
        if (EditorApplication.timeSinceStartup < nextAt) return;

        if (index >= 0) Record(queue[index]);
        index++;
        if (index >= queue.Count) { Stop(null); return; }

        // A screen left open (a chest, a relic swap, the run summary) holds the pause and freezes the
        // next room, so nothing in it would run. Close the gap by releasing any held pause.
        if (GameManager.instance != null)
            while (GameManager.instance.IsUIPaused) GameManager.instance.ReleasePause();
        Time.timeScale = 1f;

        current = new List<string>();
        var lm = LevelManager.instance;
        lm.forcedNextRoom = queue[index];
        lm.SpawnNextRoom();
        var player = GameManager.instance != null ? GameManager.instance.player : null;
        spawnY = player != null ? player.transform.position.y : 0f;
        nextAt = EditorApplication.timeSinceStartup + Dwell;
    }

    static void Record(GameObject room)
    {
        var problems = current ?? new List<string>();
        var player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player != null && player.transform.position.y < spawnY - FellBelow)
            problems.Add($"player fell out: y {player.transform.position.y:0.0} against a spawn at {spawnY:0.0}");
        if (Object.FindObjectsByType<ExitDoor>(FindObjectsSortMode.None).Length == 0)
            problems.Add("no ExitDoor in the spawned room");
        findings.Add(new Finding { room = room.name, problems = problems });
        current = null;
    }

    static void Stop(string abortReason)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        running = false;

        var sb = new StringBuilder("[RoomSweep] ");
        int bad = 0;
        foreach (var f in findings) if (f.problems.Count > 0) bad++;
        sb.AppendLine(abortReason != null
            ? $"ABORTED ({abortReason}) after {findings.Count} rooms."
            : $"{findings.Count} rooms swept, {bad} with problems.");
        foreach (var f in findings)
        {
            sb.AppendLine((f.problems.Count == 0 ? "  ok   " : "  !!   ") + f.room);
            foreach (var p in f.problems) sb.AppendLine("         " + p);
        }
        if (bad > 0 || abortReason != null) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }
}
