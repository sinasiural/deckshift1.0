using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What happened this run, for the end-of-run summary and for bug reports.
///
/// ⚠️ STATIC, SO IT MUST BE RESET BY HAND. Statics survive scene loads, and a run is a scene load
/// (death → PLAY AGAIN reloads SampleScene). <see cref="BeginRun"/> is called from the one place a
/// run starts — LevelManager's first-room branch — so a stale count can never leak into the next run.
///
/// Every counter is fed from the SAME chokepoint the rest of the game already trusts for that
/// number, so nothing new has to remember to report here:
///   · kills      — EnemyHealth.Die (where quests and relics hear about kills)
///   · cards      — DeckManager.PlayCard, on a play that actually left the hand
///   · Shift      — PlayerController.SpendShift (every Shift cost funnels through it; callers already
///                  apply the sandbox rule, so hub spending is never counted)
///   · recalls    — DeckManager.TryRecall, on a Recall that went through (outside sandbox rooms).
///                  RecallShift is the part of ShiftSpent that went on Recall, which is the number
///                  that says how much of the run's Shift the hand is eating rather than the jumps.
///   · gold       — PlayerController.AddGold
///   · damage     — PlayerHealth.ApplyDamage (HP actually lost, after armour)
///   · rooms      — ExitDoor, on leaving a COMBAT room (same test the flawless-clear payout uses)
///   · bosses     — BossHealthBar.Initialize, which every boss calls when it wakes
/// </summary>
public static class RunStats
{
    public static float StartTime { get; private set; }
    public static int RoomsCleared { get; private set; }
    public static int Kills { get; private set; }
    public static int CardsPlayed { get; private set; }
    public static int ShiftSpent { get; private set; }
    public static int Recalls { get; private set; }
    public static int RecallShift { get; private set; }
    public static int GoldCollected { get; private set; }
    public static float DamageTaken { get; private set; }

    /// <summary>Names of bosses killed this run, in order.</summary>
    public static readonly List<string> BossesKilled = new List<string>();

    private static readonly HashSet<int> engaged = new HashSet<int>();

    /// <summary>The boss the player is fighting right now, or null outside a boss fight.</summary>
    public static string CurrentBoss { get; private set; }

    /// <summary>True when the last HP the player lost was Stagger's price rather than a hit.</summary>
    public static bool LastLossWasStagger { get; private set; }

    /// <summary>Seconds of PLAY: Time.time is scaled, so every pause and menu is excluded for free.</summary>
    public static float PlaySeconds => Mathf.Max(0f, Time.time - StartTime);

    // A short trail of what just happened, newest last. It is the part of a bug report that says HOW
    // the player got there, which is usually the part that matters.
    private const int TrailLength = 60;
    private static readonly Queue<string> trail = new Queue<string>();
    public static IEnumerable<string> Trail => trail;

    public static void BeginRun()
    {
        StartTime = Time.time;
        RoomsCleared = Kills = CardsPlayed = ShiftSpent = Recalls = RecallShift = GoldCollected = 0;
        DamageTaken = 0f;
        BossesKilled.Clear();
        engaged.Clear();
        CurrentBoss = null;
        LastLossWasStagger = false;
        trail.Clear();
        Note("Run started" + (CharacterSelection.Chosen != null ? " as " + CharacterSelection.Chosen.characterName : ""));
    }

    public static void NoteRoomEntered(string roomName, MapNode node)
    {
        CurrentBoss = null;   // a boss fight never carries over a room change
        Note("Entered " + roomName + (node != null ? " (floor " + node.floor + ", " + node.type + ")" : ""));
    }

    public static void NoteRoomCleared() { RoomsCleared++; }
    public static void NoteKill() { Kills++; }

    public static void NoteCardPlayed(string cardName)
    {
        CardsPlayed++;
        Note("Played " + cardName);
    }

    public static void NoteShiftSpent(int amount) { if (amount > 0) ShiftSpent += amount; }

    // shiftPaid is 0 for a free Recall (Second Nature, Tunnel Vision, or Offering, which pays in HP).
    // The Shift itself is already in ShiftSpent through SpendShift; this only labels that share.
    public static void NoteRecall(int shiftPaid)
    {
        Recalls++;
        if (shiftPaid > 0) RecallShift += shiftPaid;
        Note("Recalled" + (shiftPaid > 0 ? " (" + shiftPaid + " Shift)" : " (free)"));
    }
    public static void NoteGold(int amount) { if (amount > 0) GoldCollected += amount; }

    public static void NoteDamage(float hpLost, bool stagger, float hpLeft)
    {
        if (hpLost > 0f) DamageTaken += hpLost;
        LastLossWasStagger = stagger;
        Note((stagger ? "Paid Stagger " : "Took ") + Mathf.CeilToInt(hpLost) + " HP (left " + Mathf.CeilToInt(hpLeft) + ")");
    }

    /// <summary>Called when a boss wakes and binds its health bar.</summary>
    public static void NoteBossEngaged(EnemyHealth boss, string name)
    {
        string display = string.IsNullOrEmpty(name) ? "the boss" : name;
        CurrentBoss = display;

        // A boss that re-binds its bar (a phase change, a re-wake) must not be counted twice.
        if (boss == null || !engaged.Add(boss.GetInstanceID())) return;
        Note("Boss fight: " + display);
        // ⚠️ The handler outlives nothing: EnemyHealth fires OnDied and is destroyed in the same frame,
        // so this runs exactly once per boss and the subscription dies with the object.
        boss.OnDied += () =>
        {
            BossesKilled.Add(display);
            if (CurrentBoss == display) CurrentBoss = null;
            Note("Defeated " + display);
        };
    }

    public static void Note(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        int s = Mathf.FloorToInt(PlaySeconds);
        trail.Enqueue($"[{s / 60:00}:{s % 60:00}] {line}");
        while (trail.Count > TrailLength) trail.Dequeue();
    }

    /// <summary>"12:05" or "1:02:40".</summary>
    public static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}
