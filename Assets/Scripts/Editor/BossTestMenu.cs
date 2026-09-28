using UnityEditor;
using UnityEngine;

/// <summary>
/// Jump straight into a boss arena while playing, without walking a whole run to reach a boss node.
///
/// ⚠️ IT TOUCHES NO POOL LIST. `LevelManager.roomPrefabs` has been wiped four times in this project
/// and the symptom never looks like a pool problem, so testing goes through `forcedNextRoom` — a
/// [NonSerialized] one-shot that clears itself the moment it is used and can never be saved into a
/// scene or prefab.
/// </summary>
public static class BossTestMenu
{
    private const string ARENA = "Assets/LevelGenerated/NinjaArena.prefab";

    [MenuItem("Deckshift/Test/Ninja Boss Fight %#j")]
    private static void GoToNinjaArena()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Ninja Boss Fight",
                "Press Play first, then run this again (Ctrl+Shift+J).\n\n" +
                "It swaps the room you are standing in for the Ninja arena, so the game has to be running.",
                "OK");
            return;
        }

        var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ARENA);
        if (arena == null) { Debug.LogError($"[BossTest] arena not found at {ARENA}"); return; }

        var lm = LevelManager.instance;
        if (lm == null) { Debug.LogError("[BossTest] no LevelManager — are you in SampleScene?"); return; }

        lm.forcedNextRoom = arena;
        lm.SpawnNextRoom();
        Debug.Log("[BossTest] Dropped into the Ninja arena. Walk RIGHT to trip the fight trigger.");
    }

    // ---- Kagemusha ------------------------------------------------------------------------------
    // Same one-shot hook. Two entries because the SAME prefab is two different fights: the
    // mid-map cut (one double, no twist) for the Wizard and the Ninja, and the FINALE (two doubles,
    // solid at 40%) for the Samurai. LevelManager decides that from CharacterSelection.Chosen when
    // it spawns the room, so the forced spawn already gives you whichever matches who you are
    // playing — the second entry just lets you see the finale as anyone.
    private const string HALL = "Assets/LevelGenerated/KagemushaHall.prefab";

    [MenuItem("Deckshift/Test/Kagemusha Boss Fight %#k")]
    private static void GoToKagemushaHall() => GoToHall(false);

    [MenuItem("Deckshift/Test/Kagemusha Boss Fight (force FINALE)")]
    private static void GoToKagemushaFinale() => GoToHall(true);

    private static void GoToHall(bool forceFinale)
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Kagemusha Boss Fight",
                "Press Play first, then run this again (Ctrl+Shift+K).\n\n" +
                "It swaps the room you are standing in for the Long Hall, so the game has to be running.",
                "OK");
            return;
        }

        var hall = AssetDatabase.LoadAssetAtPath<GameObject>(HALL);
        if (hall == null) { Debug.LogError($"[BossTest] hall not found at {HALL}"); return; }

        var lm = LevelManager.instance;
        if (lm == null) { Debug.LogError("[BossTest] no LevelManager — are you in SampleScene?"); return; }

        lm.forcedNextRoom = hall;
        lm.SpawnNextRoom();

        if (forceFinale)
            foreach (var m in Object.FindObjectsByType<KagemushaBoss>(FindObjectsSortMode.None))
                m.SetFinale(true);

        var boss = Object.FindFirstObjectByType<KagemushaBoss>();
        Debug.Log("[BossTest] Dropped into the Long Hall (" +
                  (boss != null && boss.finale ? "FINALE: two doubles, solid at 40%" : "mid-map: one double") +
                  "). Walk RIGHT a few tiles to trip the fight trigger.");
    }

    // ---- new combat rooms ------------------------------------------------------------------------
    // Same one-shot hook. Reaches a room directly, in the pool or not: new rooms stay out of
    // LevelManager.roomPrefabs until the designer has judged them, and this is how to play one
    // without walking a run and hoping it comes up. (The 2026-09-25 four joined the pool 09-27.)
    [MenuItem("Deckshift/Test/Play Room/The Descent (Easy)")]        private static void RoomDescent()  => GoToRoom("Descent");
    [MenuItem("Deckshift/Test/Play Room/Two Roads (Medium)")]        private static void RoomTwoRoads() => GoToRoom("TwoRoads");
    [MenuItem("Deckshift/Test/Play Room/The Cistern (Medium)")]      private static void RoomCistern()  => GoToRoom("Cistern");
    [MenuItem("Deckshift/Test/Play Room/The Crusher Works (Hard)")]  private static void RoomCrusher()  => GoToRoom("CrusherWorks");
    // 2026-09-28, first batch (in the pool since the same day)
    [MenuItem("Deckshift/Test/Play Room/The Long Jump (Easy)")]      private static void RoomLongJump() => GoToRoom("LongJump");
    [MenuItem("Deckshift/Test/Play Room/The Chapel (Easy)")]         private static void RoomChapel()   => GoToRoom("Chapel");
    [MenuItem("Deckshift/Test/Play Room/The Stacks (Medium)")]       private static void RoomStacks()   => GoToRoom("Stacks");
    [MenuItem("Deckshift/Test/Play Room/The Gallows (Medium)")]      private static void RoomGallows()  => GoToRoom("Gallows");
    [MenuItem("Deckshift/Test/Play Room/The Ossuary (Hard)")]        private static void RoomOssuary()  => GoToRoom("Ossuary");
    // 2026-09-28, second batch
    [MenuItem("Deckshift/Test/Play Room/The Mill (Easy)")]           private static void RoomMill()     => GoToRoom("Mill");
    [MenuItem("Deckshift/Test/Play Room/The Archery Range (Easy)")]  private static void RoomArchery()  => GoToRoom("ArcheryRange");
    [MenuItem("Deckshift/Test/Play Room/The Watchtower (Hard)")]     private static void RoomTower()    => GoToRoom("Watchtower");
    [MenuItem("Deckshift/Test/Play Room/The Pit and the Pendulum (Hard)")] private static void RoomPendulum() => GoToRoom("Pendulum");

    private static void GoToRoom(string name)
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Room",
                "Press Play first (in SampleScene), then pick the room again.\n\n" +
                "It swaps the room you are standing in for this one, so the game has to be running.", "OK");
            return;
        }
        string path = "Assets/LevelGenerated/" + name + ".prefab";
        var room = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (room == null) { Debug.LogError($"[RoomTest] no room at {path}"); return; }
        var lm = LevelManager.instance;
        if (lm == null) { Debug.LogError("[RoomTest] no LevelManager — are you in SampleScene?"); return; }
        lm.forcedNextRoom = room;
        lm.SpawnNextRoom();
        Debug.Log($"[RoomTest] Dropped into {name}.");
    }

    // ---- watching a boss without dying to it ----------------------------------------------------
    // Test-only. A boss is judged by watching its whole kit, and the Kagemusha killed the first
    // tester who stood still on the trigger. This is PlayerHealth.isInvincible, the same flag the
    // dash uses — play-mode state only, gone on Stop.
    [MenuItem("Deckshift/Test/Toggle Player Invincible %#i")]
    private static void ToggleInvincible()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[BossTest] play mode only."); return; }
        var player = GameManager.instance != null ? GameManager.instance.player : null;
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health == null) { Debug.LogWarning("[BossTest] no player."); return; }
        health.isInvincible = !health.isInvincible;
        Debug.Log("[BossTest] Player invincible: " + (health.isInvincible ? "ON" : "OFF"));
    }

    // Sets the pick the select screen would have made, so entering play mode straight into
    // SampleScene spawns that character. Persists (PlayerPrefs) until you pick someone else.
    [MenuItem("Deckshift/Test/Play As/Wizard")]  private static void PlayAsWizard()  => PlayAs("Wizard");
    [MenuItem("Deckshift/Test/Play As/Ninja")]   private static void PlayAsNinja()   => PlayAs("Ninja");
    [MenuItem("Deckshift/Test/Play As/Samurai")] private static void PlayAsSamurai() => PlayAs("Samurai");

    private static void PlayAs(string characterName)
    {
        var data = CharacterRoster.ByName(characterName);
        if (data == null) { Debug.LogError($"[BossTest] no character named '{characterName}' in Resources/Characters."); return; }
        CharacterSelection.Chosen = data;
        Debug.Log($"[BossTest] Next run plays as {characterName}" +
                  (EditorApplication.isPlaying ? " — takes effect on the next scene load (restart or die)." : "."));
    }

    [MenuItem("Deckshift/Test/Give 8 Salvaged Shuriken")]
    private static void GiveStars()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[BossTest] play mode only."); return; }
        if (DeckManager.instance == null) { Debug.LogWarning("[BossTest] no DeckManager."); return; }
        DeckManager.instance.AddSalvagedShuriken(8);
        Debug.Log("[BossTest] +8 stars. Quiver now holds " + DeckManager.instance.SalvagedShurikenCount + ".");
    }
}
