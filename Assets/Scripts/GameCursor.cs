using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The game's mouse cursor (designer, 2026-09-27: "make it feel like deckshift").
///
///   pointer          a shard of the Shift crystal painted on every card, tip up-left
///   pointer_pressed  the same shard energised into Shift cyan while the button is held
///   aim              four card-frame corners round the gem: shown while a card is armed and the
///                    mouse is over the world, because the click will cast it RIGHT THERE
///   aim_pressed      the corners snap inward and the gem lights: the cast
///
/// The art is four 16x16 PNGs in Assets/Resources/Cursor/, drawn at the world's pixel size and
/// meant to be edited directly (any pixel editor; keep the size). A missing "_pressed" image falls
/// back to its resting one, and missing art falls back to the system cursor, never to nothing.
///
/// ⚠️ HARDWARE CURSOR (Cursor.SetCursor), not a UI image chasing the mouse: a drawn cursor lags a
/// frame behind the hand, and in a game about precise jumps and aimed clicks that lag is felt.
/// ⚠️ SCALED BY WHOLE PIXELS for the screen height (2x at 1080p, 3x at 1440p, 4x at 4K), so the
/// pixels stay square and crisp. SetCursor is only called when the picture actually changes.
/// ⚠️ The hotspots below are where the click lands. If the art is redrawn with the tip or the gem
/// somewhere else, move them too.
///
/// One object for the whole session (DontDestroyOnLoad): it holds no run state, only pictures, so
/// it cannot carry anything stale from one run into the next.
/// </summary>
public class GameCursor : MonoBehaviour
{
    // Hotspots in the 16x16 source, measured from the TOP-LEFT (as Cursor.SetCursor wants them).
    private static readonly Vector2 PointerHotspot = new Vector2(1f, 1f);
    private static readonly Vector2 AimHotspot = new Vector2(7.5f, 7.5f);

    private enum Look { Pointer, PointerPressed, Aim, AimPressed }

    private static GameCursor instance;

    private readonly Texture2D[] source = new Texture2D[4];
    private readonly Texture2D[] scaled = new Texture2D[4];
    private int builtScale = -1;
    private int shown = -1;          // the look currently applied, -1 = none yet

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneBootstrap.Register(Create); }

    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("GameCursor");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameCursor>();
    }

    private void Awake()
    {
        source[(int)Look.Pointer] = Load("pointer");
        source[(int)Look.PointerPressed] = Load("pointer_pressed");
        source[(int)Look.Aim] = Load("aim");
        source[(int)Look.AimPressed] = Load("aim_pressed");
    }

    private static Texture2D Load(string name) { return Resources.Load<Texture2D>("Cursor/" + name); }

    private void LateUpdate()
    {
        int scale = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 540f), 2, 4);
        if (scale != builtScale) Rebuild(scale);

        Look look = Armed() ? Look.Aim : Look.Pointer;
        if (Input.GetMouseButton(0)) look = look == Look.Aim ? Look.AimPressed : Look.PointerPressed;
        Apply(look);
    }

    // A card is armed, nothing modal is up, and the mouse is over the world rather than the HUD:
    // the one situation where the next click casts a card at the cursor.
    private static bool Armed()
    {
        if (DeckManager.instance == null || DeckManager.instance.GetSelectedIndex() < 0) return false;
        if (GameManager.instance != null && GameManager.instance.IsUIPaused) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        return true;
    }

    private void Apply(Look look)
    {
        // Missing art falls back one step at a time: pressed -> resting, aim -> pointer.
        int i = (int)look;
        if (scaled[i] == null && look == Look.AimPressed) i = (int)Look.Aim;
        if (scaled[i] == null && look == Look.PointerPressed) i = (int)Look.Pointer;
        if (scaled[i] == null && i == (int)Look.Aim) i = (int)Look.Pointer;
        if (i == shown) return;
        shown = i;

        Texture2D tex = scaled[i];
        if (tex == null) { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); return; }
        bool aim = i == (int)Look.Aim || i == (int)Look.AimPressed;
        Cursor.SetCursor(tex, (aim ? AimHotspot : PointerHotspot) * builtScale, CursorMode.Auto);
    }

    private void Rebuild(int scale)
    {
        builtScale = scale;
        shown = -1;
        for (int i = 0; i < source.Length; i++)
        {
            if (scaled[i] != null) Destroy(scaled[i]);
            scaled[i] = Upscale(source[i], scale);
        }
    }

    // Nearest-neighbour: every source pixel becomes a scale x scale block.
    private static Texture2D Upscale(Texture2D src, int scale)
    {
        if (src == null) return null;
        try
        {
            Color32[] from = src.GetPixels32();
            int w = src.width, h = src.height, W = w * scale;
            Color32[] to = new Color32[W * h * scale];
            for (int y = 0; y < h * scale; y++)
                for (int x = 0; x < W; x++)
                    to[y * W + x] = from[(y / scale) * w + (x / scale)];

            Texture2D tex = new Texture2D(W, h * scale, TextureFormat.RGBA32, false)
            {
                name = src.name + " x" + scale,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
#if UNITY_EDITOR
            tex.alphaIsTransparency = true;   // editor-only flag; SetCursor warns without it
#endif
            tex.SetPixels32(to);
            tex.Apply(false);
            return tex;
        }
        catch (System.Exception e)
        {
            // Unreadable art (import settings lost): keep the system cursor rather than break.
            Debug.LogWarning("[GameCursor] Could not read cursor image '" + src.name + "': " + e.Message);
            return null;
        }
    }

    // Windows can hand the cursor back to the system when the window loses focus.
    private void OnApplicationFocus(bool focused) { if (focused) shown = -1; }

    // Leaving play mode in the editor: give the editor its own cursor back.
    private void OnApplicationQuit() { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
}
