using System.Collections;
using UnityEngine;

public class PlatformCreateAction : CardAction
{
    public override CardActionType ActionType => CardActionType.PlatformCreate;

    public override bool Execute(PlayerController player, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        if (player.platformPrefab == null) return false;
        if (player.mainCamera == null) return false;
        SfxManager.PlayOn(player.audioSource, player.createPlatformSound);
        Vector2 spawnPosition = player.mainCamera.ScreenToWorldPoint(GameInput.MousePosition);
        Object.Instantiate(player.platformPrefab, spawnPosition, Quaternion.identity);
        return true;
    }
}
