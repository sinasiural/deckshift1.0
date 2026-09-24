using System.Collections;
using System.Collections.Generic;

/// <summary>
/// The screenplay. Every shot in the trailer, written in <see cref="TrailerDirector"/>'s staging
/// vocabulary: put the player in a room, deal a hand, roll, drive, cut.
///
/// A shot is a ROUTINE, not data, on purpose — "wait until he lands, then cast Comet Dive at the
/// crowd" is a sentence, and a sentence is easier to write and read than a keyframe list. Each
/// shot starts from a neutral reset (real time, gameplay camera, HUD on, keyboard released) so
/// the cut can be reordered freely.
///
/// Times are in seconds of SHOT time at a fixed 60 fps; wall clock does not exist here.
///
/// Frames land in <c>TrailerCapture/&lt;shot&gt;/</c>; the cut is assembled by
/// <c>Tools/Trailer/build.ps1</c>. Record overs long — it is far cheaper to trim in the edit
/// than to re-film a shot that ended a beat early.
/// </summary>
public static class TrailerShots
{
    public static List<TrailerShot> All()
    {
        return new List<TrailerShot>
        {
            new TrailerShot("corridor", "GenLevel7 ground corridor: run, dash the acid, fireball the Shambler", Corridor),
            new TrailerShot("comet",    "GenLevel7: jump and Comet Dive onto the Rotbrute, slow-mo on impact", Comet),
            new TrailerShot("drop",     "EfeVrl7: run off the ledge, Comet Dive onto the slimes in the pit", Drop),
            new TrailerShot("smoke",    "pipeline test: walk right and jump in the hub", Smoke),
        };
    }

    // The shot camera. Tighter than gameplay (7): pixel art wants to be seen, and a trailer is
    // watched on a phone as often as a monitor.
    private const float ShotSize = 5.5f;

    // ---- shots -----------------------------------------------------------------------------

    // GenLevel7's bottom corridor, measured: floor y=4 for x 3..13, acid 14..19 (crystal at
    // 13.5), floor 20..26 with a Shambler at 22.5, acid 27..33, floor 34..47 with a Rotbrute at
    // 36.5. Ceiling at 11 the whole way.
    private static IEnumerator Corridor(TrailerDirector d)
    {
        yield return d.Room("GenLevel7");
        d.Hand("Dash", "Fireball", "CometDive", "Fireball");
        d.Shift(40);
        d.Heal();
        d.Place(4.5f, 4f);
        // Floor in the lower third, ceiling in frame: a corridor, not a wall with a floor in it.
        d.CamFollow(ShotSize, 1.5f, 2.5f);
        yield return d.Face(true);
        yield return d.Wait(1.0f);   // the dealt hand's animation has to settle first

        d.Record();
        yield return d.Wait(0.35f);
        d.Run(1f);

        // Over the acid: jump from the lip, Dash at the apex.
        yield return d.UntilX(12.2f);
        yield return d.Jump(0.3f);
        yield return d.UntilApex();
        yield return d.Cast("Dash", d.PlayerPos.x + 6f, d.PlayerPos.y, 0.05f);
        yield return d.UntilLanded();

        // The Shambler is two steps ahead: Fireball it on landing. Cut before the swinging
        // spike ball over that platform (24.4, 5.7) gets a say.
        yield return d.Cast("Fireball", d.PlayerPos.x + 6f, d.PlayerPos.y + 1f, 0.05f);
        yield return d.Wait(0.6f);
        d.Cut();
    }

    // Same corridor, further along: floor 34..47 with a Rotbrute at 36.5. Jump, dive on its head.
    private static IEnumerator Comet(TrailerDirector d)
    {
        yield return d.Room("GenLevel7");
        d.Hand("CometDive", "Fireball", "Dash", "Fireball");
        d.Shift(40);
        d.Heal();
        d.Place(33.8f, 4f);
        d.CamFollow(ShotSize, 1.0f, 2.5f);
        yield return d.Face(true);
        yield return d.Wait(1.0f);

        d.Record();
        yield return d.Wait(0.3f);
        yield return d.Jump(0.35f);
        yield return d.Wait(0.15f);
        d.Run(1f);
        yield return d.UntilApex();
        d.Stop();
        yield return d.Cast("CometDive", d.PlayerPos.x + 1f, d.PlayerPos.y - 3f, 0.05f);
        yield return d.UntilGrounded(2f);
        d.Speed(0.25f);
        yield return d.Wait(0.55f);
        d.Speed(1f);
        yield return d.Wait(0.9f);
        d.Cut();
    }

    // EfeVrl7, measured: a ledge at y=-7.9 for x -4..-2, then a 6-deep pit at x 0..6 (floor
    // -13.9) with slimes at (0.2, -13.9) and (8, -13.9).
    private static IEnumerator Drop(TrailerDirector d)
    {
        yield return d.Room("EfeVrl7");
        d.Hand("CometDive", "Fireball", "Dash", "Phase");
        d.Shift(40);
        d.Heal();
        d.Place(-6.5f, -7.9f);
        d.CamFollow(ShotSize, 1.0f, -1.5f);   // look down into the pit
        yield return d.Face(true);
        yield return d.Wait(1.0f);

        d.Record();
        yield return d.Wait(0.3f);
        d.Run(1f);
        yield return d.UntilX(-1.2f);
        yield return d.Jump(0.3f);
        yield return d.UntilApex();
        d.Stop();
        yield return d.Cast("CometDive", d.PlayerPos.x, d.PlayerPos.y - 4f, 0.05f);
        yield return d.UntilGrounded(2.5f);
        d.Speed(0.25f);
        yield return d.Wait(0.55f);
        d.Speed(1f);
        yield return d.Wait(1.0f);
        d.Cut();
    }

    private static IEnumerator Smoke(TrailerDirector d)
    {
        yield return d.Room("hub");
        d.Hand("Dash", "Fireball", "CometDive");
        d.Shift(40);
        yield return d.Face(true);
        yield return d.Wait(0.3f);

        d.Record();
        yield return d.Walk(1f, 0.8f);
        yield return d.Jump();
        yield return d.Walk(1f, 0.7f);
        yield return d.UntilGrounded();
        yield return d.Wait(0.4f);
        yield return d.Cast("Fireball", d.PlayerPos.x + 6f, d.PlayerPos.y + 1f);
        yield return d.Wait(1.2f);
        d.Cut();
    }
}
