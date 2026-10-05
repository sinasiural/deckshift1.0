"""
Beat map for a music track, so the trailer can be cut to it by measurement.

    py -3 Tools/Trailer/beatmap.py "<track>" [out.json]

Prints the tempo, the beat grid, the bar (4-beat) grid and the strongest onsets ("hits" — the
places a cut wants to land), and writes them as JSON. Pure Python + ffmpeg, no numpy: the track is
decoded to 11025 Hz mono PCM through ffmpeg, an onset envelope is built from the rectified rise in
band energy, tempo is picked by autocorrelation over 60–200 BPM, and the beat phase is the offset
that best lines the grid up with the onsets.

It is a measurement, not an oracle: check the printed hits against your ears once. Tempo halving /
doubling is the classic failure — if it says 70 and the track feels like 140, use 140.
"""
import json
import math
import os
import struct
import subprocess
import sys

SR = 11025
HOP = 128                       # envelope resolution: 11.6 ms


def find_ffmpeg():
    for c in ("ffmpeg", os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Links", "ffmpeg.exe")):
        try:
            subprocess.run([c, "-version"], capture_output=True, check=True)
            return c
        except Exception:
            pass
    root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Packages")
    for dp, _, files in os.walk(root):
        if "ffmpeg.exe" in files:
            return os.path.join(dp, "ffmpeg.exe")
    raise SystemExit("ffmpeg not found")


def decode(path):
    raw = subprocess.run([find_ffmpeg(), "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "s16le", "-"],
                         capture_output=True, check=True).stdout
    n = len(raw) // 2
    return struct.unpack("<%dh" % n, raw[: n * 2])


def envelope(samples):
    """Frame energy in two bands (low = kick/bass, full), then rectified rise = onset strength."""
    frames = len(samples) // HOP
    low = [0.0] * frames
    full = [0.0] * frames
    # crude one-pole low-pass for the "low" band
    lp = 0.0
    a = 0.02
    idx = 0
    for f in range(frames):
        el = 0.0
        ef = 0.0
        for i in range(HOP):
            s = samples[idx] / 32768.0
            idx += 1
            lp += a * (s - lp)
            el += lp * lp
            ef += s * s
        low[f] = math.sqrt(el / HOP)
        full[f] = math.sqrt(ef / HOP)

    def rise(e):
        out = [0.0] * len(e)
        for i in range(1, len(e)):
            d = e[i] - e[i - 1]
            out[i] = d if d > 0 else 0.0
        return out

    rl, rf = rise(low), rise(full)
    ml = max(rl) or 1.0
    mf = max(rf) or 1.0
    # weight the low band: kicks carry the beat in this kind of track
    return [1.5 * rl[i] / ml + rf[i] / mf for i in range(frames)]


def tempo(onset):
    fps = SR / HOP
    best = (0.0, 0)
    for bpm10 in range(600, 2001):           # 60.0 .. 200.0 BPM in 0.1 steps
        bpm = bpm10 / 10.0
        lag = fps * 60.0 / bpm
        l0 = int(lag)
        frac = lag - l0
        s = 0.0
        n = len(onset) - l0 - 1
        for i in range(n):
            s += onset[i] * (onset[i + l0] * (1 - frac) + onset[i + l0 + 1] * frac)
        s /= n
        # mild preference for the 100-160 range to fight octave errors
        w = 1.0 if 100 <= bpm <= 160 else 0.85
        if s * w > best[0]:
            best = (s * w, bpm)
    return best[1]


def phase(onset, bpm):
    fps = SR / HOP
    period = fps * 60.0 / bpm
    best = (0.0, 0.0)
    steps = int(period)
    for k in range(steps):
        s = 0.0
        t = float(k)
        while t < len(onset):
            i = int(t)
            s += onset[i]
            t += period
        if s > best[0]:
            best = (s, k / fps)
    return best[1]


def hits(onset, count=40, min_gap=0.35):
    fps = SR / HOP
    gap = int(min_gap * fps)
    cands = sorted(range(len(onset)), key=lambda i: -onset[i])
    picked = []
    for i in cands:
        if all(abs(i - j) > gap for j in picked):
            picked.append(i)
        if len(picked) >= count:
            break
    return sorted((i / fps, onset[i]) for i in picked)


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    path = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else None

    samples = decode(path)
    dur = len(samples) / SR
    onset = envelope(samples)
    bpm = tempo(onset)
    ph = phase(onset, bpm)
    beat = 60.0 / bpm

    beats = []
    t = ph
    while t < dur:
        beats.append(round(t, 3))
        t += beat
    bars = beats[::4]
    strong = hits(onset)

    print("track   : %s" % os.path.basename(path))
    print("duration: %.2fs" % dur)
    print("tempo   : %.1f BPM  (beat %.3fs, bar %.3fs)  first beat at %.3fs" % (bpm, beat, beat * 4, ph))
    print("bars    : " + " ".join("%.1f" % b for b in bars))
    print("hits    : " + " ".join("%.2f" % t for t, _ in strong))

    if out:
        with open(out, "w") as f:
            json.dump({"track": path, "duration": dur, "bpm": bpm, "beat": beat, "firstBeat": ph,
                       "beats": beats, "bars": bars, "hits": [round(t, 3) for t, _ in strong]}, f, indent=1)
        print("written : " + out)


if __name__ == "__main__":
    main()
