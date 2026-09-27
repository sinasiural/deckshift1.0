using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the exit of the run's FINAL boss room barred until every boss in it is dead.
///
/// ⚠️ WHY: leaving the finale room is what wins the run (LevelManager.AdvanceToNextRoom). The Ninja
/// and Kagemusha bar their own doors during the fight, but the Moss Knight never has — its arena has
/// the exit at the far end, open, with the boss standing between you and it. As a mid-map boss that
/// is a design question; as the Wizard's finale it meant a player could run past a living boss and be
/// told they had won.
///
/// Added by LevelManager to the finale room at spawn, so no boss has to opt in. It only ever pushes
/// the door towards LOCKED while a boss lives, so it agrees with bosses that lock their own doors and
/// never fights them. A finale room with no boss in it is left alone: an unfinishable run is worse
/// than an unearned win.
/// </summary>
public class FinaleExitLock : MonoBehaviour
{
    private readonly List<EnemyHealth> bosses = new List<EnemyHealth>();
    private ExitDoor[] doors;

    private void Start()
    {
        foreach (MonoBehaviour mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(mb is IBossFight)) continue;
            EnemyHealth h = mb.GetComponent<EnemyHealth>();
            if (h == null) h = mb.GetComponentInParent<EnemyHealth>();
            if (h == null) h = mb.GetComponentInChildren<EnemyHealth>(true);
            if (h != null && !bosses.Contains(h)) bosses.Add(h);
        }

        doors = GetComponentsInChildren<ExitDoor>(true);

        if (bosses.Count == 0 || doors.Length == 0)
        {
            Debug.LogWarning("[FinaleExitLock] the finale room has no boss or no exit door — leaving the exit alone.");
            enabled = false;
        }
    }

    private void Update()
    {
        // EnemyHealth.Die destroys the boss's GameObject, so a dead boss reads as Unity-null here.
        bool anyAlive = false;
        foreach (EnemyHealth b in bosses)
            if (b != null) { anyAlive = true; break; }

        foreach (ExitDoor d in doors)
        {
            if (d == null) continue;
            if (anyAlive && !d.IsLocked) d.SetLocked(true);
            else if (!anyAlive && d.IsLocked) d.SetLocked(false);
        }

        if (!anyAlive) enabled = false;   // the boss is down and the bars are up; nothing left to watch
    }
}
