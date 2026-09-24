using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Deckshift → Build Tutorial Room.
//
// Builds the tutorial from Assets/LevelTexts/Tutorial.txt in one step, so the text file stays the
// single source of truth:
//   1. imports the geometry through LevelTextImporter (the same importer every generated room uses);
//   2. dresses the result: TutorialRoom on the root, a chalk TutorialSign + checkpoint at every digit
//      anchor (text from the file's !signN lines), and a TutorialGate on every gate an altar or lever
//      does not already drive, watching the enemies just in front of it;
//   3. wires the prefab into SampleScene's LevelManager.tutorialRoomPrefab.
//
// ⚠️ IT REPLACES THE PREFAB EVERY TIME. Hand edits to Tutorial.prefab are lost on the next build —
// edit the .txt (layout and sign text) or this builder instead. That is deliberate: the importer
// cannot re-import over a prefab in place (it writes "Name 1.prefab" beside it), and a re-import
// renumbers every fileID, which silently nulls scene references into the room. Step 3 exists so that
// never matters here: the scene reference is re-made on every build.
public static class TutorialRoomBuilder
{
    private const string Source = "Assets/LevelTexts/Tutorial.txt";
    private const string Output = "Assets/LevelGenerated/Tutorial.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // How far in front of a kill-gate an enemy may stand and still count as "holding it shut".
    private const float GateWatchReach = 14f;

    private struct SignSpec { public int number; public int col, row; public string keys, caption; }

    [MenuItem("Deckshift/Build Tutorial Room")]
    public static void BuildFromMenu()
    {
        try
        {
            string report = Build();
            EditorUtility.DisplayDialog("Tutorial room built", report, "OK");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Tutorial build failed", e.Message, "OK");
            Debug.LogError("[TutorialRoomBuilder] " + e);
        }
    }

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Stop Play mode first — the builder saves SampleScene.");

        int width, height;
        List<SignSpec> signs = ReadSigns(out width, out height);

        // 1 — import. Delete first: the importer never overwrites (it would write "Tutorial 1.prefab").
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Output) != null) AssetDatabase.DeleteAsset(Output);

        MethodInfo importer = typeof(LevelTextImporter).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static);
        if (importer == null) throw new Exception("LevelTextImporter.Build(string) not found — was it renamed?");
        string built = (string)importer.Invoke(null, new object[] { Source });
        if (built != Output)
            throw new Exception($"The importer wrote '{built}', expected '{Output}'. Check the file's !name line.");

        // 2 — dress.
        GameObject root = PrefabUtility.LoadPrefabContents(built);
        int gateCount = 0, watchedCount = 0;
        try
        {
            if (root.GetComponent<TutorialRoom>() == null) root.AddComponent<TutorialRoom>();

            // ⚠️ CLAMP THE CAMERA TO THE ROOM'S OWN EDGES. The importer pads every zone by 2 tiles and
            // enforces a 20-tall minimum, so the view could slide past the outer wall and under the
            // floor into black void — tolerable deep in a run, not on the first screen a new player
            // ever sees. The grid's frame is solid rock (3 rows under the floor, more than the ~2.2
            // units the hand rail covers), so the exact grid is a safe zone. The camera is 14 tall
            // and the room is 18, so the zone never has to be smaller than the view.
            Transform bounds = root.transform.Find("CameraBounds");
            BoxCollider2D zone = bounds != null ? bounds.GetComponent<BoxCollider2D>() : null;
            if (zone != null)
            {
                bounds.position = new Vector3(width / 2f, height / 2f, bounds.position.z);
                zone.offset = Vector2.zero;
                zone.size = new Vector2(width, height);
            }
            else Debug.LogWarning("[TutorialRoomBuilder] no CameraBounds zone found to tighten.");

            Transform signParent = new GameObject("TutorialSigns").transform;
            signParent.SetParent(root.transform, false);
            foreach (SignSpec s in signs)
            {
                var go = new GameObject("Sign " + s.number);
                go.transform.SetParent(signParent, false);
                // The anchor cell is the air cell a player stands in; its bottom edge is the floor.
                // Same cell-to-world mapping as LevelTextImporter (cellY = height - 1 - row).
                go.transform.position = new Vector3(s.col + 0.5f, height - 1 - s.row, 0f);
                var sign = go.AddComponent<TutorialSign>();
                sign.keys = s.keys;
                sign.caption = s.caption;
            }

            // Gates an altar or lever already drives are left alone; every other gate is a kill-gate.
            var driven = new HashSet<Gate>();
            foreach (ShiftAltar altar in root.GetComponentsInChildren<ShiftAltar>(true))
                if (altar.signalTarget != null && altar.signalTarget.GetComponent<Gate>() != null)
                    driven.Add(altar.signalTarget.GetComponent<Gate>());

            EnemyHealth[] enemies = root.GetComponentsInChildren<EnemyHealth>(true);
            foreach (Gate gate in root.GetComponentsInChildren<Gate>(true))
            {
                if (driven.Contains(gate)) continue;
                Vector3 g = gate.transform.position;
                var watched = new List<EnemyHealth>();
                foreach (EnemyHealth e in enemies)
                {
                    Vector3 p = e.transform.position;
                    if (p.x < g.x && p.x > g.x - GateWatchReach && Mathf.Abs(p.y - g.y) < 5f) watched.Add(e);
                }
                if (watched.Count == 0)
                    Debug.LogWarning($"[TutorialRoomBuilder] the gate at x={g.x:F1} has no enemy in front of it " +
                                     "and no altar — it will never open.");
                gate.gameObject.AddComponent<TutorialGate>().Configure(gate, watched);
                gateCount++;
                watchedCount += watched.Count;
            }

            PrefabUtility.SaveAsPrefabAsset(root, built);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 3 — wire.
        WireIntoScene(AssetDatabase.LoadAssetAtPath<GameObject>(built));

        return $"{built}\n{signs.Count} signs, {gateCount} kill-gate(s) watching {watchedCount} enemies.\n" +
               "Wired into SampleScene → LevelManager → Tutorial Room Prefab.";
    }

    // Reads the !signN lines and finds each digit anchor in the grid. Parsing mirrors
    // LevelTextImporter: '//' comments and '!' directives are skipped, and fully empty lines are
    // trimmed from both ends of the grid.
    private static List<SignSpec> ReadSigns(out int width, out int height)
    {
        var text = new Dictionary<int, string>();
        var grid = new List<string>();
        foreach (string raw in File.ReadAllLines(Source))
        {
            string line = raw.TrimEnd('\r', '\n');
            string t = line.TrimStart();
            if (t.StartsWith("//")) continue;
            if (t.StartsWith("!"))
            {
                int colon = t.IndexOf(':');
                string key = colon > 1 ? t.Substring(1, colon - 1).Trim().ToLowerInvariant() : "";
                if (key.StartsWith("sign") && int.TryParse(key.Substring(4), out int n))
                    text[n] = t.Substring(colon + 1).Trim();
                continue;
            }
            grid.Add(line);
        }
        while (grid.Count > 0 && grid[0].Trim().Length == 0) grid.RemoveAt(0);
        while (grid.Count > 0 && grid[grid.Count - 1].Trim().Length == 0) grid.RemoveAt(grid.Count - 1);
        height = grid.Count;
        width = 0;
        foreach (string l in grid) width = Mathf.Max(width, l.Length);

        var signs = new List<SignSpec>();
        for (int row = 0; row < grid.Count; row++)
            for (int col = 0; col < grid[row].Length; col++)
            {
                char c = grid[row][col];
                if (c < '1' || c > '9') continue;
                int n = c - '0';
                if (!text.TryGetValue(n, out string spec))
                    throw new Exception($"Anchor '{c}' at column {col}, row {row} has no !sign{n} line.");
                int bar = spec.IndexOf('|');
                signs.Add(new SignSpec
                {
                    number = n, col = col, row = row,
                    keys = bar >= 0 ? spec.Substring(0, bar).Trim() : "",
                    caption = bar >= 0 ? spec.Substring(bar + 1).Trim() : spec,
                });
            }
        signs.Sort((a, b) => a.number.CompareTo(b.number));
        return signs;
    }

    private static void WireIntoScene(GameObject prefab)
    {
        if (prefab == null) throw new Exception("Built prefab could not be loaded for wiring.");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            LevelManager lm = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                lm = r.GetComponentInChildren<LevelManager>(true);
                if (lm != null) break;
            }
            if (lm == null) throw new Exception("No LevelManager in SampleScene.");

            // Only this one property is touched — LevelManager.roomPrefabs has been wiped five times
            // and must never be collateral damage of a tool.
            var so = new SerializedObject(lm);
            so.FindProperty("tutorialRoomPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
