using UnityEngine;

// Bu, Create men�s�ne "Deckshift/Relic Data" ad�nda yeni bir se�enek ekler
[CreateAssetMenu(fileName = "New Relic", menuName = "Deckshift/Relic Data")]
public class RelicData : ScriptableObject
{
    [Header("Info")]
    public string relicID; // E�yay� kodda tan�mak i�in benzersiz bir kimlik (�rn: "LavaBoots")
    public string relicName;
    [TextArea]
    public string description;
    public Sprite relicArt;
    public Rarity rarity;

    [Header("Art (boss relics only)")]
    [Tooltip("This relic adds a KEY the player has to press. Only boss relics may set this, and you " +
             "can only ever hold one at a time — see the warning in RelicPool.")]
    public bool isArt;

    /// <summary>
    /// ⚠️ RARITY.BOSS DOES NOT MEAN "HAS A KEYBIND". That separation is the whole answer to the
    /// problem the designer raised (2026-08-21): with ~10 bosses in a run, if every boss relic added
    /// an input the player would be asked to remember ten keys, which is not a build, it is homework.
    ///
    /// So most boss relics are PASSIVES at boss power, and only a couple are Arts — a verb on a key.
    /// `RelicPool` refuses to offer a second Art while you hold one, which caps the whole game at
    /// exactly ONE extra key no matter how many bosses you kill, and does it without ever leaving a
    /// dead relic sitting in a slot.
    /// </summary>
    public bool IsArt => isArt && rarity == Rarity.Boss;
}
