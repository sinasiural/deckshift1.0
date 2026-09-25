// Marker component — a room prefab with this on its root is a RECHARGE room (Foundry / Market /
// Well): an attachment hanging off a map node, not a floor. Same shape as HubMarker.
//
// Why it exists: ExitDoor pays three things on leaving "a room" — a flawless clear (NoDamageRoom
// quest + achievement), a step on every per-room oath, and Nest Egg's +2 max Shift. All three were
// hub-excluded because nothing is at stake in the sandbox. Nothing is at stake in a recharge room
// either — there is not a single enemy in one — so without this marker every recharge room would
// have been a free oath step, a free flawless clear and a free Nest Egg. LevelManager reads it
// through IsCurrentRoomRecharge() / IsCurrentRoomCombat().
using UnityEngine;

public class RechargeRoomMarker : MonoBehaviour { }
