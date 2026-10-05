using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deckshift → Trailer. Tick the shots you want, press Record: the Game View is driven to
/// 1920x1080 (the capture is whatever size the view is — see ScreenGallery for why a fixed
/// resolution rather than "free aspect"), <see cref="TrailerDirector"/> films each shot at a
/// fixed 60 fps into <c>TrailerCapture/&lt;shot&gt;/</c>, and the view is put back afterwards.
///
/// ⚠️ Needs Play mode and will not start one — entering play mode domain-reloads and would
/// wipe whatever run state is staged.
///
/// Frames are PNG, one per frame: a 3-second shot is 180 files. That is deliberate — lossless
/// source, and ffmpeg makes the clip in seconds. <c>TrailerCapture/</c> is gitignored.
/// </summary>
public class TrailerWindow : EditorWindow
{
    private const int CaptureW = 1920, CaptureH = 1080;

    private Vector2 scroll;
    private List<TrailerShot> shots;
    private bool[] selected;
    private int restoreSizeIndex = -1;
    private bool wipeBeforeRecord = true;

    [MenuItem("Deckshift/Trailer")]
    private static void Open()
    {
        var w = GetWindow<TrailerWindow>("Trailer");
        w.minSize = new Vector2(380, 320);
    }

    private static string OutputRoot =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TrailerCapture");

    private void OnEnable()
    {
        shots = TrailerShots.All();
        selected = new bool[shots.Count];
        for (int i = 0; i < selected.Length; i++) selected[i] = true;
        EditorApplication.update += Repaint;
    }

    private void OnDisable() { EditorApplication.update -= Repaint; }

    private void OnGUI()
    {
        bool running = TrailerDirector.IsRunning;

        EditorGUILayout.LabelField("Shots", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(running))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", GUILayout.Width(50))) for (int i = 0; i < selected.Length; i++) selected[i] = true;
            if (GUILayout.Button("None", GUILayout.Width(50))) for (int i = 0; i < selected.Length; i++) selected[i] = false;
            if (GUILayout.Button("Reload list", GUILayout.Width(90))) OnEnable();
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < shots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                selected[i] = EditorGUILayout.ToggleLeft(shots[i].name, selected[i], GUILayout.Width(160));
                EditorGUILayout.LabelField(shots[i].note ?? "", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            wipeBeforeRecord = EditorGUILayout.ToggleLeft("Wipe previous frames of the selected shots first", wipeBeforeRecord);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output: " + OutputRoot, EditorStyles.miniLabel);

        if (running)
        {
            TrailerDirector d = TrailerDirector.instance;
            EditorGUILayout.LabelField("Recording  " + d.CurrentShotName + "  (" + (d.CurrentShotIndex + 1) + "/" + d.shots.Count + ")", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("frames written: " + d.FramesCaptured + (d.Recording ? "   ● REC" : "   staging…"));
            if (GUILayout.Button("Abort")) d.Abort();
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Press Play first. Rooms only exist at runtime, and this will not enter Play mode for you (it would wipe the staged run).", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("● Record selected", GUILayout.Height(30))) StartRecording();
        if (GUILayout.Button("Open folder", GUILayout.Width(90), GUILayout.Height(30)))
        {
            Directory.CreateDirectory(OutputRoot);
            EditorUtility.RevealInFinder(OutputRoot);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void StartRecording()
    {
        var picked = new List<TrailerShot>();
        for (int i = 0; i < shots.Count; i++) if (selected[i]) picked.Add(shots[i]);
        if (picked.Count == 0) { EditorUtility.DisplayDialog("Trailer", "No shots selected.", "OK"); return; }

        if (wipeBeforeRecord)
        {
            foreach (TrailerShot s in picked)
            {
                string dir = Path.Combine(OutputRoot, Sanitize(s.name));
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // 1920x1080 exactly. Restored when the run ends, however it ends.
        restoreSizeIndex = ScreenGallery.GameViewSizer.Current();
        ScreenGallery.GameViewSizer.Select(ScreenGallery.GameViewSizer.Ensure(CaptureW, CaptureH, "Deckshift Trailer"));

        // A frame or two for the view to relayout before the first capture; the director's own
        // neutral reset yields one, and the first shot's staging spends far more than that.
        TrailerDirector.Launch(OutputRoot, picked, ok =>
        {
            if (restoreSizeIndex >= 0)
            {
                try { ScreenGallery.GameViewSizer.Select(restoreSizeIndex); } catch { }
                restoreSizeIndex = -1;
            }
            Repaint();
        });
    }

    private static string Sanitize(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }
}
