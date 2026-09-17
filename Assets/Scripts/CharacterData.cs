using System.Collections.Generic;
using UnityEngine;

// A playable character. A character is TWO things and no more: the deck you start the run with,
// and one small passive trait.
//
// ⚠️ IT IS DELIBERATELY NOT AN ACTIVE ABILITY (designer 2026-08-16). The first version of this
// gave the Wizard a free, unlimited, ranged attack on right mouse. It worked, and it broke the
// game: a free answer to every fight doesn't out-damage your attack cards, it PREVENTS the
// situations those cards exist for — nothing ever gets close enough to need Comet Dive, or
// dangerous enough to need Glass Wail. It also let the player chip down breakable walls for
// nothing and, in principle, kill a boss without ever using the boss room's own mechanic. A
// starting deck and a passive shape a run without ever solving a moment in it.
[CreateAssetMenu(fileName = "Character", menuName = "Deckshift/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Wizard";
    [TextArea(2, 4)] public string description;
    public Sprite portrait;

    [Header("Starting Deck")]
    // The run's opening deck. Duplicates are allowed — list a card twice to start with two copies.
    // Left empty, DeckManager falls back to its own startingDeck list.
    public List<CardData> startingDeck = new List<CardData>();

    [Header("Trait")]
    // Player-facing, so it follows the house voice (see Tone & Voice): a wink that still hints at
    // what the thing does.
    public string traitName = "Big Sleeves";
    [TextArea(2, 4)] public string traitDescription = "You hold one more card in hand.";

    // Added to DeckManager's base hand capacity. May be NEGATIVE — a trait is allowed to cost you
    // something. Read through DeckManager.HandCapacity — never add it at a call site, or the next
    // place that checks a full hand will quietly forget it.
    public int handCapacityBonus = 0;

    // Recall costs Shift and normally gets one more expensive with every use inside a level.
    // Consumed by DeckManager.RecallCostIsLocked.
    public bool recallCostNeverRises = false;

    // Armour granted on entering each COMBAT room (the Samurai's "Full Plate"). Armour is a second
    // health pool that empties before HP and does NOT reset between rooms, so a player who is never
    // touched walks into room five wearing five times this. The streak IS the trait.
    //
    // ⚠️ Granted on combat rooms only — PlayerController.OnNewRoomEnter gates it on
    // LevelManager.IsCurrentRoomCombat(). The hub and the recharge rooms (Foundry / Market / Well)
    // are sandboxes; paying out on every visit there is the umbrella rule broken from the income
    // side, and the Well in particular could be farmed.
    //
    // Read through the live character at the grant site — never copied into a field on the player,
    // for the same reason handCapacityBonus never is: a character swap must not be able to leave a
    // stale copy behind.
    public float armourPerRoom = 0f;

    [Header("The boss made from this character")]
    // Every playable character is also a boss (the castle's former owners — see the
    // bosses-are-characters premise). This is the arena where THIS character is the boss.
    //
    // It does two things in LevelManager, both read live off CharacterSelection.Chosen:
    //   - it is the run's FINALE for a player who picked this character (your own mirror is held
    //     for the top of the castle), falling back to `finalBossRoomPrefab` when empty;
    //   - it is FILTERED OUT of the mid-map boss pool for that same player, so you never meet
    //     yourself before the end.
    // For everyone else it is an ordinary entry in `bossRoomPrefabs`.
    public GameObject bossRoom;

    [Header("Look")]
    // A Cainos character PRESET prefab (Assets/Cainos/.../Character Preset/). Its outfit is COPIED
    // onto the player's existing rig at runtime — materials only.
    //
    // ⚠️ THE MODEL IS NOT SWAPPED, AND THAT IS DELIBERATE. The player's visual model carries a pile
    // of careful hand setup that is documented as fragile: the Cainos controller scripts and
    // physics stripped off, AnimationEventReceiver removed, PlayerAnimEventSink added to the
    // Animator child, the 0.8 scale. Instantiating a fresh preset would have to reproduce all of
    // that at runtime, and every one of those steps fails SILENTLY when it is missed. Every preset
    // is the same rig wearing different materials, so re-dressing the one we already trust gets the
    // same result with none of that risk.
    public GameObject appearancePreset;

    // A Cainos weapon prefab (Assets/Cainos/.../Prefab/Weapon/). Replaces whatever the player is
    // holding. Left empty the character keeps the prefab's own weapon.
    //
    // Unlike the outfit this IS instantiated, because a weapon is a separate object in the hand
    // rather than a material on the body — but it goes into the same "Weapon Slot" the pack
    // already animates, so the hand still carries it correctly through every clip.
    public GameObject weaponPrefab;
}
