using System.Collections.Generic;
using UnityEngine;

// Bank sounds:  Sfx.Play("Enemy.Hit", position);
//
// It resolves the id against the SoundBank, picks a variant that is not the one it just played,
// jitters pitch and volume, refuses to machine-gun, ducks stacked copies, and hands the result to a
// pooled AudioSource.
//
// ⚠️ MOST SOUNDS ARE STILL INSPECTOR SLOTS, NOT BANK EVENTS (2026-10-03). The bank lists ~40
// events, but only the footstep and the enemy hit / death / block are played from it; everything
// else is a slot played through SfxManager. SfxManager.PlayAtPoint now uses this pool too
// (PlayClip), so slot sounds get the voice pool, the distance curve and pitch variation without
// being moved into the bank.
//
// ⚠️ WHY POOLED SOURCES AND NOT `AudioSource.PlayClipAtPoint` — this is the important one.
// PlayClipAtPoint gives you NO WAY TO SET PITCH. Everything in the project used it, so every sound
// in the game played at exactly pitch 1.000 every single time: every sword swing byte-identical,
// every footstep byte-identical. That is a large part of why the audio reads as "the same stuff
// everywhere", and no amount of better source material fixes it. It also creates and destroys a
// GameObject per sound, which is pure garbage in a game that spawns scrap shards by the handful.
//
// The pool is created on the SfxManager (already a DontDestroyOnLoad singleton), so this needs no
// scene setup and survives scene loads.
public static class Sfx
{
    private const string BankPath = "SoundBank";     // Assets/Resources/SoundBank.asset
    private const float StackWindow = 0.14f;         // "already sounding" window for the duck
    private const int PoolSize = 32;   // was 24 before every PlayAtPoint came through here too

    private static SoundBank bank;
    private static AudioSource[] pool;
    private static int nextSource;
    private static bool warnedMissingBank;
    private static readonly HashSet<string> warnedIds = new HashSet<string>();

    public static SoundBank Bank
    {
        get
        {
            if (bank == null) bank = Resources.Load<SoundBank>(BankPath);
            return bank;
        }
    }

    /// <summary>Play a bank event at a world position.</summary>
    public static void Play(string id, Vector3 position)
    {
        var e = Resolve(id);
        if (e == null) return;
        PlayEvent(e, position, true);
    }

    /// <summary>Play a bank event in 2D — always equally loud, wherever it happened.</summary>
    public static void Play(string id)
    {
        var e = Resolve(id);
        if (e == null) return;
        PlayEvent(e, Vector3.zero, false);
    }

    // Per-clip crowd control for PlayClip, the same rules a bank event gets. Keyed by the clip
    // because a raw clip has no event to hang the state on.
    private class ClipState { public float lastPlayed = -999f; public readonly List<float> recent = new List<float>(8); }
    private static readonly Dictionary<AudioClip, ClipState> clipStates = new Dictionary<AudioClip, ClipState>();
    private const float ClipMinInterval = 0.03f;
    private const float ClipStackDuck = 0.72f;

    /// <summary>
    /// Play a bare clip (an Inspector slot, not a bank event) on a pooled voice with pitch jitter,
    /// the same burst throttle and stack duck as bank events, and the pool's distance curve.
    /// Returns false only when no voice exists yet (no SfxManager), so the caller can fall back.
    /// </summary>
    public static bool PlayClip(AudioClip clip, Vector3 position, float volume, bool positional, Vector2 pitchRange)
    {
        if (clip == null) return true;
        float now = Time.unscaledTime;

        ClipState st;
        if (!clipStates.TryGetValue(clip, out st)) { st = new ClipState(); clipStates[clip] = st; }
        if (now - st.lastPlayed < ClipMinInterval) return true;

        for (int i = st.recent.Count - 1; i >= 0; i--)
            if (now - st.recent[i] > StackWindow) st.recent.RemoveAt(i);
        float duck = Mathf.Pow(ClipStackDuck, st.recent.Count);

        var src = Take();
        if (src == null) return false;

        st.recent.Add(now);
        st.lastPlayed = now;

        src.clip = clip;
        src.volume = Mathf.Clamp01(volume * duck);
        src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        src.spatialBlend = positional ? 1f : 0f;
        src.transform.position = positional ? position : Vector3.zero;
        src.Play();
        return true;
    }

    private static SoundEvent Resolve(string id)
    {
        if (Bank == null)
        {
            // ⚠️ Warn ONCE, not per call. A missing bank in a busy scene would otherwise produce
            // thousands of identical console lines and bury whatever the real problem was.
            if (!warnedMissingBank)
            {
                warnedMissingBank = true;
                Debug.LogWarning("Sfx: no SoundBank at Resources/" + BankPath + " — all bank sounds are silent.");
            }
            return null;
        }
        var e = Bank.Find(id);
        if (e == null && warnedIds.Add(id))
            Debug.LogWarning("Sfx: no event '" + id + "' in the SoundBank. Add it, or fix the caller.");
        return e;
    }

    private static void PlayEvent(SoundEvent e, Vector3 position, bool positionalOverride)
    {
        float now = Time.unscaledTime;

        // Hard throttle. A burst arriving faster than this is not information the player can hear
        // as separate events — it is a buzz.
        if (now - e.lastPlayed < e.minInterval) return;

        AudioClip clip = e.NextVariant();
        if (clip == null) return;

        // Stack duck: how many of THIS event are still sounding right now?
        if (e.recent == null) e.recent = new List<float>(8);
        for (int i = e.recent.Count - 1; i >= 0; i--)
            if (now - e.recent[i] > StackWindow) e.recent.RemoveAt(i);
        float duck = Mathf.Pow(Mathf.Clamp(e.stackDuck, 0.3f, 1f), e.recent.Count);

        e.recent.Add(now);
        e.lastPlayed = now;

        float vol = Random.Range(e.volume.x, e.volume.y) * duck * SfxManager.Volume;
        float pitch = Random.Range(e.pitch.x, e.pitch.y);
        bool spatial = e.positional && positionalOverride;

        var src = Take();
        if (src == null) return;
        src.clip = clip;
        src.volume = Mathf.Clamp01(vol);
        src.pitch = pitch;
        src.spatialBlend = spatial ? 1f : 0f;
        src.transform.position = spatial ? position : Vector3.zero;
        src.Play();
    }

    // Round-robin over the pool. Prefer a free source; if every one is busy, steal the oldest —
    // dropping the sound entirely would be worse than clipping one that is already ending.
    private static AudioSource Take()
    {
        EnsurePool();
        if (pool == null) return null;

        for (int i = 0; i < pool.Length; i++)
        {
            var s = pool[(nextSource + i) % pool.Length];
            if (s != null && !s.isPlaying)
            {
                nextSource = (nextSource + i + 1) % pool.Length;
                return s;
            }
        }
        var steal = pool[nextSource];
        nextSource = (nextSource + 1) % pool.Length;
        return steal;
    }

    private static void EnsurePool()
    {
        // Statics survive a scene load but the GameObjects they point at may not, so this checks the
        // sources are still alive rather than trusting a null check on the array.
        if (pool != null && pool.Length > 0 && pool[0] != null) return;
        if (SfxManager.instance == null) return;

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("SfxVoice_" + i);
            go.transform.SetParent(SfxManager.instance.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            // ⚠️ THE LISTENER IS ON THE CAMERA AT z = -10 AND THE PLAY PLANE IS z = -2, so nothing
            // in the game is ever closer than 8 units. Unity's default curve (full volume inside 1
            // unit, then 1/distance) therefore plays everything at 1/8 volume or less: measured
            // -18.1 dB dead centre on screen. That is what PlayClipAtPoint did to every enemy and
            // pickup sound until 2026-10-03. Full volume out to 6 units, gone by 34, keeps a sound
            // on screen at 70-93% and lets off-screen ones fade.
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 6f;
            s.maxDistance = 34f;
            // A side-on camera chasing a dashing player would bend every sound's pitch.
            s.dopplerLevel = 0f;
            pool[i] = s;
        }
    }

    /// <summary>Editor/testing hook — forget the cached bank and pool.</summary>
    public static void Reset()
    {
        bank = null; pool = null; nextSource = 0;
        clipStates.Clear();
        warnedMissingBank = false; warnedIds.Clear();
    }
}
