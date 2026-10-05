using UnityEngine;

// Central sound-effect volume so a single settings slider can scale every SFX in the game.
// AudioSource.PlayClipAtPoint cannot route to an AudioMixer, so instead of a mixer group we
// route all SFX through here and multiply by the saved SFX volume.
//
// All game SFX should play via SfxManager.PlayAtPoint(...) or SfxManager.PlayOn(...) instead of
// calling AudioSource.PlayClipAtPoint / AudioSource.PlayOneShot directly. Music is NOT affected
// (it is owned by MusicManager and has its own volume).
//
// Self-bootstraps before the first scene loads, so it always exists without any scene setup —
// if the manager is somehow missing, SFX still play at full volume (Volume falls back to 1).
public class SfxManager : MonoBehaviour
{
    public static SfxManager instance;

    const string PrefKey = "SfxVolume";

    [Range(0f, 1f)] public float sfxVolume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("SfxManager");
        go.AddComponent<SfxManager>();
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            sfxVolume = PlayerPrefs.GetFloat(PrefKey, 1f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Current global SFX volume (0..1). Safe to call before the instance exists.
    public static float Volume => instance != null ? instance.sfxVolume : 1f;

    // Set + persist the global SFX volume. Called by the settings slider.
    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKey, sfxVolume);
        PlayerPrefs.Save();
    }

    // One-shot at a world position, on a pooled voice (see Sfx.PlayClip), so it outlives whatever
    // played it, gets a little pitch variation, and is not turned into a buzz by a burst.
    //
    // ⚠️ THIS USED TO BE AudioSource.PlayClipAtPoint, AND THAT PLAYED EVERYTHING AT 1/8 VOLUME.
    // Its default falloff is full volume only within 1 unit of the listener, and the listener sits
    // on the camera 8 units in front of the play plane. Measured 2026-10-03: -18.1 dB dead centre,
    // worse off-centre. Every enemy attack, pickup, altar and breakable wall was nearly inaudible,
    // and the gold pickup (set to 0.15 on top of that) reached the ear at about -51 dB. It also
    // could not vary pitch, so every swing was byte-identical.
    private static readonly Vector2 PointPitch = new Vector2(0.95f, 1.05f);

    public static void PlayAtPoint(AudioClip clip, Vector3 position, float localVolume = 1f)
    {
        if (clip == null) return;
        if (!Sfx.PlayClip(clip, position, localVolume * Volume, true, PointPitch))
            AudioSource.PlayClipAtPoint(clip, position, localVolume * Volume);
    }

    // One-shot on an existing AudioSource. Replaces source.PlayOneShot.
    public static void PlayOn(AudioSource source, AudioClip clip, float localVolume = 1f)
    {
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, localVolume * Volume);
    }
}
