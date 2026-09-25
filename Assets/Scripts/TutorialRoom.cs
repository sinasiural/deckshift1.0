using UnityEngine;

// Marks a room prefab as THE tutorial (same convention as HubMarker / RechargeRoomMarker — its
// presence on the root is what LevelManager.IsCurrentRoomTutorial reads), and owns the two safety
// nets that keep a first-time player from being stranded:
//
//   · CHECKPOINTS. Every TutorialSign the player walks past becomes the respawn point, so a fall or a
//     death costs one section, not the whole tutorial. Only ever moves forward.
//   · A SHIFT FLOOR ON RESPAWN. The tutorial charges Shift for jumps (designer: the player must see
//     the counter fall), so a player who spends it all could otherwise respawn unable to jump. Every
//     respawn tops Shift back up to at least `respawnShiftFloor`.
//
// The other two nets live where the rules they bend live: card charges in DeckManager.PlayCard,
// death in PlayerHealth.ApplyDamage.
public class TutorialRoom : MonoBehaviour
{
    [Tooltip("On every respawn (a fall or a death) Shift is topped up to at least this, so a player " +
             "who spent it all can never be left unable to jump.")]
    [SerializeField] private int respawnShiftFloor = 10;

    private PlayerController player;
    private PlayerHealth health;
    private float furthestCheckpointX = float.NegativeInfinity;

    private void Start()
    {
        player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        health = player.GetComponent<PlayerHealth>();
        if (health != null) health.OnFallRespawn += OnRespawn;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnFallRespawn -= OnRespawn;
    }

    private void OnRespawn()
    {
        if (player != null && player.currentShift < respawnShiftFloor)
            player.AddShift(respawnShiftFloor - player.currentShift);
    }

    /// <summary>Called by a TutorialSign the player has walked past. Forward-only.</summary>
    public void ReachCheckpoint(Vector3 floorPosition)
    {
        if (player == null || floorPosition.x <= furthestCheckpointX) return;
        furthestCheckpointX = floorPosition.x;
        player.SetCurrentEntryPoint(new Vector3(floorPosition.x, floorPosition.y, PlayPlane.Z));
    }
}
