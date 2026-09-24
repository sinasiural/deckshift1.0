using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hand rail: where your cards live, permanently, at the bottom edge of the screen.
///
/// ⚠️ THIS USED TO BE A HIDE-ON-EXIT DRAWER, AND THAT IS THE THING THAT WAS WRONG WITH IT.
/// It slid out of sight and only rose when the pointer entered a 1000x200 zone across the bottom
/// centre of the screen. Designer's report, 2026-08-22:
///
///   "i find it hard to hover over cards at the same time as trying to play them, and the hover
///    panel always gets in the middle of the screen, so much so that sometimes i cant see my
///    character or the boss."
///
/// Both halves of that were structural, not cosmetic:
///
///   THE MOUSE IS ALREADY BUSY. Aimed cards — Shuriken, Borrowed Steel — fly at the CURSOR, so the
///   cursor is the aim. A hand that has to be hovered before it can be read makes seeing your
///   options and aiming a shot the same input, fighting each other, in the middle of a boss fight.
///   Cards are playable on 1/2/3 already; the hover requirement bought nothing and cost that.
///
///   IT ROSE INTO THE PLAY AREA. Measured in the Ninja arena: the camera there runs at
///   orthographicSize 10 instead of the usual 7, so the world draws smaller while the HUD does not.
///   The player is 91 canvas px tall; a hovered card was 288 — over three times the height of the
///   character it was covering, sitting dead centre.
///
/// So: it no longer hides, it no longer needs the pointer, and it rests low enough that the cards
/// occupy the strip of screen below the floor line rather than the fight. `SetLocked` still tucks it
/// away entirely, which is the one case where hiding is correct — a full-screen panel is up and the
/// hand is not playable anyway.
/// </summary>
public class HandUIDrawer : MonoBehaviour
{
    [Header("UI Bağlantıları")]
    public RectTransform handContainer;

    [Header("Pozisyon Ayarları")]
    [Tooltip("Where the hand sits during normal play — always visible. Low enough that the cards " +
             "hang off the bottom edge instead of standing in the arena.")]
    public float restY = 58f;
    [Tooltip("Where it goes when a full-screen panel takes over. Fully off-screen.")]
    public float lockedY = -260f;
    public float slideSpeed = 15f;

    [Header("Durum Kontrolü (Debug İçin)")]
    public bool isLocked = false;

    private Image drawerImage;

    public static HandUIDrawer instance;

    private void Awake()
    {
        instance = this;
        drawerImage = GetComponent<Image>();

        // ⚠️ NOTHING HERE CATCHES THE POINTER ANY MORE. The Image existed to detect hover, and its
        // raycast target was a 1000x200 invisible blocker lying across the bottom centre of the
        // screen for the whole run. With the rail always open there is nothing to detect, and an
        // input-eating rectangle over the play area is exactly what we are trying to get rid of.
        if (drawerImage != null) drawerImage.raycastTarget = false;
    }

    private void Start()
    {
        if (handContainer == null) handContainer = GetComponent<RectTransform>();
        // Opens AT rest, not from off-screen: the hand sliding up every time the room loads reads as
        // a notification rather than as furniture.
        handContainer.anchoredPosition = new Vector2(handContainer.anchoredPosition.x, restY);
    }

    private void Update()
    {
        float targetY = isLocked ? lockedY : restY;

        if (Mathf.Abs(handContainer.anchoredPosition.y - targetY) > 0.5f)
        {
            float newY = Mathf.Lerp(handContainer.anchoredPosition.y, targetY, Time.unscaledDeltaTime * slideSpeed);
            handContainer.anchoredPosition = new Vector2(handContainer.anchoredPosition.x, newY);
        }
    }

    /// <summary>
    /// Kept because callers exist in the wild and a drag system may come back. It no longer needs to
    /// force the rail open — the rail is always open.
    /// </summary>
    public void SetCardDraggingState(bool state) { }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }
}
