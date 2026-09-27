using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

// Editor-only relic cheat panel (F1). The CLASS exists in builds so the component on SampleScene's
// DebugManager still resolves (a class compiled out entirely logs "referenced script is missing" on
// every scene load); only its body is compiled out, so a build has no F1 panel.
public class DebugTools : MonoBehaviour
{
#if UNITY_EDITOR
    private List<RelicData> allRelics = new List<RelicData>();
    private bool showPanel = false;
    private Vector2 scrollPos;

    private void Awake()
    {
        string[] guids = AssetDatabase.FindAssets("t:RelicData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (relic != null) allRelics.Add(relic);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            showPanel = !showPanel;
    }

    private void OnGUI()
    {
        if (!showPanel) return;

        GUILayout.BeginArea(new Rect(20, 20, 260, 400), GUI.skin.box);
        GUILayout.Label("DEBUG — Relic Ver", GUI.skin.box);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        foreach (RelicData relic in allRelics)
        {
            bool owned = RelicManager.instance != null && RelicManager.instance.HasRelic(relic.relicID);

            GUI.enabled = !owned;
            if (GUILayout.Button(owned ? $"{relic.relicName} ✓" : relic.relicName))
            {
                if (RelicManager.instance != null)
                    RelicManager.instance.TryGrantRelic(relic);   // full loadout -> Swap Screen
                else
                    Debug.LogWarning("[DebugTools] RelicManager sahnede bulunamadı.");
            }
            GUI.enabled = true;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
#endif
}
