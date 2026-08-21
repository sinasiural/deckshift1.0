using UnityEngine;
using UnityEngine.SceneManagement; 

public class ExitDoor : MonoBehaviour
{
    [Header("T�r Ayar� (�NEML�)")]
    public bool isSceneLoader = false; 
    public string sceneToLoad = "GameScene"; 

    [Header("Etkile�im Ayarlar�")]
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactionPopup; 

    private bool hasBeenTriggered = false;
    private bool isPlayerInRange = false;
    private PlayerController currentPlayer;

    private void Update()
    {
        if (isPlayerInRange && !hasBeenTriggered && Input.GetKeyDown(interactKey))
        {
            PerformExit();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenTriggered) return;

        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            currentPlayer = other.GetComponent<PlayerController>();
            if (interactionPopup != null) interactionPopup.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            currentPlayer = null;
            if (interactionPopup != null) interactionPopup.SetActive(false);
        }
    }

    private void PerformExit()
    {
        if (hasBeenTriggered) return;
        hasBeenTriggered = true;

        if (interactionPopup != null) interactionPopup.SetActive(false);

        if (isSceneLoader)
        {
            Debug.Log("Hub'dan ��k�l�yor, oyun ba�l�yor...");

            if (QuestSystem.instance != null) QuestSystem.instance.CloseBoard();

            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            // The hub is not a real combat room — leaving it undamaged must NOT count as a flawless
            // clear (this is why the "Invincible" quest was auto-completing on the very first exit).
            bool isHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();

            if (!isHub && currentPlayer != null && !currentPlayer.TookDamageThisRoom)
            {
                AchievementManager.instance.OnRoomClearedFlawlessly();

                if (QuestSystem.instance != null) QuestSystem.instance.ReportEvent(QuestType.NoDamageRoom, 1);
            }

            // Judge the per-room oaths. ⚠️ OUTSIDE the flawless-clear block above — this has to run
            // whether or not the player took damage, or an oath would only ever be scored on rooms
            // that also happened to be damage-free. Hub-excluded for the same reason the flawless
            // check is: nothing is spent in the sandbox, so every oath would pass there for free.
            if (!isHub && QuestSystem.instance != null) QuestSystem.instance.EndRoom();

            // Per-room relic payouts (Nest Egg). Placed beside the oath scoring and for exactly the
            // same reasons: outside the flawless-clear block, and hub-excluded.
            if (GameManager.instance != null && GameManager.instance.player != null)
                GameManager.instance.player.ScoreRoomRelics();

            // Straight to the map. No card reward screen — see LevelManager.AdvanceToNextRoom.
            if (LevelManager.instance != null)
            {
                LevelManager.instance.AdvanceToNextRoom();
            }
        }
    }
}