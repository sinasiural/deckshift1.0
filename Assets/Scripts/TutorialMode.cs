using UnityEngine;
using UnityEngine.SceneManagement;

// The tutorial is an ordinary SampleScene run whose FIRST ROOM is the tutorial room instead of the
// hub. Nothing else about the scene changes, which is the point: the player learns on the real HUD,
// the real deck and the real Shift counter, not on a mock-up.
//
// Flow (designer, 2026-09-24): the main menu's TUTORIAL button, or the "first time?" prompt on the
// very first PLAY, calls Begin(). Walking out of the tutorial's exit calls Finish(), which returns to
// the main menu. Leaving the scene reloads everything, so the tutorial's Shift, HP and deck can never
// leak into a real run.
public static class TutorialMode
{
    private const string DoneKey = "Deckshift.TutorialDone";

    // The tutorial's signs name the Wizard's cards (Fireball, Create Platform), so it always plays as
    // the Wizard whatever was picked last time.
    private const string TutorialCharacter = "Wizard";

    private const string GameScene = "SampleScene";
    private const string MenuScene = "MainMenu";

    // ⚠️ A REQUEST, CONSUMED ONCE — not a mode that stays switched on. LevelManager reads it on its
    // first spawn and clears it. Held as a long-lived flag it would survive a tutorial abandoned from
    // the pause menu, and the player's next PLAY would drop them back into the tutorial room. Once the
    // room exists, "am I in the tutorial?" is answered by the room itself (LevelManager.IsCurrentRoomTutorial).
    private static bool requested;

    /// <summary>True once the player has walked out of the tutorial's exit at least once.</summary>
    public static bool HasCompleted => PlayerPrefs.GetInt(DoneKey, 0) == 1;

    /// <summary>Load the game scene with the tutorial room as its first room.</summary>
    public static void Begin()
    {
        requested = true;

        CharacterData wizard = CharacterRoster.ByName(TutorialCharacter);
        if (wizard != null) CharacterSelection.Chosen = wizard;
        else Debug.LogWarning($"TutorialMode: no character named '{TutorialCharacter}' — the tutorial " +
                              "will play as whoever was picked last, and its card signs may not match.");

        Time.timeScale = 1f;
        // Async for the same reason MainMenuController.StartRun is: the synchronous load freezes the
        // last frame for about a second and reads as a hang.
        SceneManager.LoadSceneAsync(GameScene);
    }

    /// <summary>LevelManager only: true exactly once after Begin().</summary>
    public static bool ConsumeRequest()
    {
        bool r = requested;
        requested = false;
        return r;
    }

    /// <summary>The tutorial's exit was taken. Remember it and go back to the menu.</summary>
    public static void Finish()
    {
        MarkCompleted();

        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        // A hard reset before a scene load, the same deliberate bypass PauseScreen.AbandonRun makes: an
        // unbalanced pause anywhere would otherwise leave the menu frozen.
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }

    /// <summary>Also called when the player declines the first-time prompt, so it never asks twice.</summary>
    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
    }
}
