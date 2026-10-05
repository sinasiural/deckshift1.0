using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Paneller")]
    // ⚠️ `settingsPanel` is gone (2026-08-09). The main menu and the pause menu now open the SAME
    // SettingsScreen, which is the only sane arrangement: with two copies, every new setting had to
    // be added twice and the two would drift apart the first time one was missed. The old
    // SettingsPanel prefab and its SettingsMenu script were deleted with it.
    public GameObject tutorialPanel;

    // PLAY butonu i�in
    //
    // Play always asks who you are playing. The screen carries the pick over to the run itself
    // through CharacterSelection; if it cannot open for any reason it starts the run anyway rather
    // than leaving the button dead.
    public void PlayGame()
    {
        // The very first PLAY asks whether to take the tutorial. Either answer is remembered, so this
        // only ever happens once (see TutorialMode.HasCompleted).
        if (!TutorialMode.HasCompleted)
        {
            if (FirstTimePrompt.IsOpen) return;
            FirstTimePrompt.Open(FindMenuButton(nameof(PlayGame)), TutorialMode.Begin, () =>
            {
                TutorialMode.MarkCompleted();
                CharacterSelectScreen.Open(StartRun);
            });
            return;
        }

        CharacterSelectScreen.Open(StartRun);
    }

    // The menu button whose click calls `method` — used as the template for the prompt's plaques so
    // they are the menu's own art, not a lookalike.
    private static UnityEngine.UI.Button FindMenuButton(string method)
    {
        foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == method) return b;
        return null;
    }

    private void StartRun()
    {
        // ⚠️ ASYNC, NOT `LoadScene`. Measured: loading SampleScene takes **1.04 seconds**, and the
        // synchronous call spends every one of them frozen on the last rendered frame — no
        // animation, no feedback, indistinguishable from a hang. Async costs the same second but
        // the character select keeps playing its exit over it, and the character select now fires
        // this callback at the START of that exit rather than after it, so the load and the
        // animation overlap instead of queueing. That is the whole of the "it takes too long"
        // fix — the load did not get faster, it stopped being dead time.
        //
        // `allowSceneActivation` is left at its default (true): the scene swaps in the moment it is
        // ready, and the select screen holds a bright wash by then so the cut is invisible.
        //
        // ⚠️ buildIndex + 1 resolves to SampleScene because `Hub` is DISABLED in Build Settings and
        // disabled scenes are not counted — verified, [0] MainMenu, [1] SampleScene, [2] GameOver,
        // [3] GameScene. Enabling Hub would silently send PLAY to the wrong scene.
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // SETTINGS butonu i�in
    public void OpenSettings()
    {
        SettingsScreen.Open();
    }

    // TUTORIAL button. It used to open `tutorialPanel`, a slideshow whose text had gone stale ("we have
    // 6 rooms"); it now starts the playable tutorial room (designer, 2026-09-24). The old panel is left
    // in the scene because the pause screen's HOW TO PLAY still opens its in-game twin.
    public void OpenTutorial()
    {
        if (FirstTimePrompt.IsOpen) return;
        TutorialMode.Begin();
    }

    // QUIT butonu i�in
    public void QuitGame()
    {
        Debug.Log("��k�� yap�ld�.");
        Application.Quit();
    }

    // Tutorial panelinin i�indeki "Back" butonuna bunu ba�layabilirsin
    // Ya da direkt Button'�n OnClick olay�na TutorialPanel objesini s�r�kleyip
    // GameObject.SetActive (false) yapabilirsin. Kodsuz ��z�m :)
    public void CloseTutorial()
    {
        tutorialPanel.SetActive(false);
    }
}