using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Waits a beat after a boss dies, then raises the reward banner.
///
/// ⚠️ IT MUST BE ITS OWN OBJECT. `EnemyHealth.Die` fires `OnDied` and calls `Destroy(gameObject)` in
/// the SAME frame, so a coroutine started on the boss dies with the boss and the reward never
/// appears. This is the same reason `BossDeathVFX` is a free-standing object rather than a routine
/// on the knight, and it is a trap this project has already paid for once.
///
/// ⚠️ IT IS DELIBERATELY NOT PARENTED TO THE ROOM EITHER — and equally deliberately does NOT carry
/// `TemporaryObject`. It has to survive the seconds between the kill and the banner, and the room
/// is not destroyed in that window; if a room change ever did happen first, the guard below
/// (`RelicPool` returning nothing) is what stops it opening an empty screen.
///
/// ⚠️ AND IT MAKES NO POLICY DECISION. Which relics are offered comes from `RelicPool` — everything
/// the player does not already own — and how often the screen fires is the caller's business. The
/// designer has a run-structure decision pending (2026-08-21: "i have decided to make the game a bit
/// more different than most others … lets just build a boss reward system with a screen"), so a
/// first-kill-only or once-per-act rule belongs at the call site, not baked in here.
/// </summary>
public class BossRewardCue : MonoBehaviour
{
    public static void Schedule(int choices, float delay)
    {
        var go = new GameObject("BossRewardCue");
        DontDestroyOnLoad(go);   // survives a scene load mid-celebration rather than vanishing
        go.AddComponent<BossRewardCue>().StartCoroutine(go.GetComponent<BossRewardCue>().Run(choices, delay));
    }

    private IEnumerator Run(int choices, float delay)
    {
        // Real time: the death celebration runs a freeze-frame and slow-mo, and a scaled wait would
        // stretch or stall with it. Same reasoning as every other unscaled wait on these screens.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

        // Boss relics are the ONLY tier RelicPool hides from ordinary draws, so asking for them
        // explicitly is the one route through — see RelicPool.Offerable.
        List<RelicData> available = RelicPool.Offerable(Rarity.Boss);

        if (available.Count == 0)
        {
            // Already holding every boss relic. Skipping is correct: an empty banner would be worse
            // than no banner, and the loot shower has already paid the player for the kill.
            Debug.Log("[BossReward] every boss relic is already owned — no banner.");
            Destroy(gameObject);
            yield break;
        }

        // Shuffle, then take up to `choices`. Shuffling matters with a pool this small: taking the
        // first N would show the same two every run and make the third effectively unreachable.
        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            RelicData tmp = available[i]; available[i] = available[j]; available[j] = tmp;
        }
        int take = Mathf.Clamp(choices, 1, available.Count);
        List<RelicData> offer = available.GetRange(0, take);

        BossRewardScreen.Open(offer, chosen =>
        {
            if (chosen != null) Debug.Log("[BossReward] took " + chosen.relicName + ".");
            Destroy(gameObject);
        });
    }
}
