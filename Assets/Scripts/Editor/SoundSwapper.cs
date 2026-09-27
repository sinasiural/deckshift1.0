using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deckshift → Replace Sound Everywhere.
///
/// Drag the clip a slot holds today into FROM (usually one of the procedural placeholders in
/// Assets/Audio/Procedural), drag the real clip into TO, press the button. Every sound slot in every
/// prefab that currently plays FROM will play TO instead.
///
/// It exists because the placeholders are spread thin: `ZombieSwing` alone sits in the attackSound
/// slot of every melee enemy, and doing that by hand is how one enemy gets missed. It is also the
/// undo: swap TO back into FROM and everything returns.
///
/// ⚠️ SAME RULE AS THE FILLER (SilentSlotFiller): it only ever writes into a prefab's OWN components,
/// never into a nested prefab instance. Writing into an instance creates a pinned override that
/// stops that copy following its source forever after. Fill the source; the rooms inherit it.
///
/// ⚠️ It only replaces slots holding exactly FROM, so a slot the designer set by hand to something
/// else is never touched.
/// </summary>
public class SoundSwapper : EditorWindow
{
    private AudioClip from, to;
    private string lastResult = "";

    [MenuItem("Deckshift/Replace Sound Everywhere")]
    public static void Open() { GetWindow<SoundSwapper>(true, "Replace Sound Everywhere"); }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Every prefab sound slot that plays FROM will play TO instead. " +
                                "Click a clip in the Project window to hear it first.", MessageType.Info);
        from = (AudioClip)EditorGUILayout.ObjectField("From (current)", from, typeof(AudioClip), false);
        to = (AudioClip)EditorGUILayout.ObjectField("To (new)", to, typeof(AudioClip), false);

        using (new EditorGUI.DisabledScope(from == null || to == null || from == to))
        {
            if (GUILayout.Button("Replace everywhere", GUILayout.Height(30)))
            {
                List<string> where = Replace(from, to);
                lastResult = where.Count == 0
                    ? "No slot plays " + from.name + "."
                    : "Replaced " + where.Count + " slot(s):\n" + string.Join("\n", where);
            }
        }

        if (!string.IsNullOrEmpty(lastResult)) EditorGUILayout.HelpBox(lastResult, MessageType.None);
    }

    /// <summary>Swap every prefab slot holding <paramref name="oldClip"/> to <paramref name="newClip"/>.
    /// Returns "prefab :: Component.field" for each slot changed.</summary>
    public static List<string> Replace(AudioClip oldClip, AudioClip newClip)
    {
        var changed = new List<string>();
        if (oldClip == null || newClip == null || oldClip == newClip) return changed;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Cainos/")) continue;   // pack demo content, not ours

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;
            bool dirty = false;

            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || PrefabUtility.IsPartOfPrefabInstance(mb)) continue;   // see header

                foreach (FieldInfo f in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!f.IsPublic && !System.Attribute.IsDefined(f, typeof(SerializeField))) continue;

                    if (f.FieldType == typeof(AudioClip))
                    {
                        if ((f.GetValue(mb) as AudioClip) != oldClip) continue;
                        f.SetValue(mb, newClip);
                    }
                    else if (f.FieldType == typeof(AudioClip[]))
                    {
                        AudioClip[] arr = f.GetValue(mb) as AudioClip[];
                        if (arr == null) continue;
                        bool hit = false;
                        for (int i = 0; i < arr.Length; i++) if (arr[i] == oldClip) { arr[i] = newClip; hit = true; }
                        if (!hit) continue;
                    }
                    else continue;

                    EditorUtility.SetDirty(mb);
                    changed.Add(path + " :: " + mb.GetType().Name + "." + f.Name);
                    dirty = true;
                }
            }

            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SoundSwapper] " + oldClip.name + " -> " + newClip.name + ": " + changed.Count + " slot(s)\n" + string.Join("\n", changed));
        return changed;
    }
}
