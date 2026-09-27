using UnityEngine;

// Keeps a shipped build's Player.log readable.
//
// The code carries ~80 chatty Debug.Log lines (one per hit taken, per crystal picked up, per boss
// hit — many in Turkish, which the log file mangles into mojibake). They are useful in the editor
// and pure noise in a build, where the log's only reader is someone going through a friend's bug
// report. A release build therefore records warnings, errors and exceptions only; the editor and
// Development builds keep everything.
//
// Once per SESSION is correct here, unlike the scene-local managers: the logger is process-wide
// state that no scene load resets.
public static class BuildLogFilter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.filterLogType = LogType.Warning;
#endif
    }
}
