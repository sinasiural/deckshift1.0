using UnityEngine;

/// <summary>
/// The player's inputs, read through one door so a script can stand in for the keyboard.
///
/// With <see cref="Scripted"/> off (always, in a real game) every property is a straight
/// pass-through to <see cref="Input"/> — nothing about play changes. With it on, the values are
/// whatever the driver last wrote, which is what lets <c>TrailerDirector</c> walk the character
/// across a room, jump on a beat and aim a Shuriken at a point it chose, frame-exactly and
/// repeatably. The same door serves a future attract mode or an input replay.
///
/// Only PLAYER inputs route through here: movement, jump, and the mouse the cards aim with.
/// UI screens keep reading <see cref="Input"/> directly — a scripted shot never needs to press a
/// menu button, and a driver that could would be a driver that could get a screen stuck.
///
/// ⚠️ <see cref="JumpDown"/> is an EDGE, not a level. The driver sets it for one frame and the
/// director clears it after every simulated frame, mirroring <c>Input.GetButtonDown</c> — leave
/// it set and the jump buffer re-arms every frame, which reads as a bunny-hop.
/// </summary>
public static class GameInput
{
    /// <summary>True while a script is driving the player instead of the keyboard.</summary>
    public static bool Scripted;

    // Written by the driver while Scripted is on.
    public static float ScriptedHorizontal;
    public static float ScriptedVertical;
    public static bool ScriptedJumpDown;
    public static bool ScriptedJumpHeld;
    public static Vector3 ScriptedMouse;

    public static float Horizontal => Scripted ? ScriptedHorizontal : Input.GetAxisRaw("Horizontal");
    public static float Vertical   => Scripted ? ScriptedVertical   : Input.GetAxisRaw("Vertical");
    public static bool  JumpDown   => Scripted ? ScriptedJumpDown   : Input.GetButtonDown("Jump");
    public static bool  JumpHeld   => Scripted ? ScriptedJumpHeld   : Input.GetKey(KeyCode.Space);

    /// <summary>Screen-space mouse position, the thing every aimed card reads.</summary>
    public static Vector3 MousePosition => Scripted ? ScriptedMouse : Input.mousePosition;

    /// <summary>Hand control back to the keyboard and forget everything the driver wrote.</summary>
    public static void Release()
    {
        Scripted = false;
        ScriptedHorizontal = 0f;
        ScriptedVertical = 0f;
        ScriptedJumpDown = false;
        ScriptedJumpHeld = false;
        ScriptedMouse = Vector3.zero;
    }
}
