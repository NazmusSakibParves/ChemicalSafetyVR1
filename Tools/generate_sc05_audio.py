"""Generates the Scenario 5 factory machine ambience (a seamless loop) so no licensed audio is needed.

Output: Assets/Art/Audio/Sc05/Machine_Loop.wav (mono, 22.05 kHz, 16 bit, 12 s). It is a low motor drone with a
slow beat, a rhythmic belt/pump thump, faint clanks and a soft steam hiss. Level is kept moderate on purpose:
the user must not be bothered by it (Sc05Ambience also plays it quietly and lowers it under voice-over).

    python Tools/generate_sc05_audio.py
"""
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Audio", "Sc05")
SR = 22050
SECONDS = 12


def lowpass(x, k):
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def main():
    os.makedirs(OUT, exist_ok=True)
    n = SR * SECONDS
    t = np.arange(n) / SR
    rng = np.random.default_rng(5)
    # whole-number cycles over the loop so the ends meet without a click
    def cyc(f):
        return round(f * SECONDS) / SECONDS
    drone = (0.55 * np.sin(2 * np.pi * cyc(48) * t) + 0.32 * np.sin(2 * np.pi * cyc(96) * t + 0.7) +
             0.16 * np.sin(2 * np.pi * cyc(144) * t + 1.3)) * (0.8 + 0.2 * np.sin(2 * np.pi * cyc(0.5) * t))
    beat_f = cyc(2.0)
    phase = (t * beat_f) % 1.0
    thump = np.exp(-phase * 14.0) * np.sin(2 * np.pi * 62 * phase / beat_f)
    belt = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.06) * (0.35 + 0.65 * np.exp(-((t * cyc(4.0)) % 1.0) * 6.0))
    hiss_src = rng.uniform(-1, 1, n).astype(np.float32)
    hiss = (hiss_src - lowpass(hiss_src, 0.35)) * (0.5 + 0.5 * np.sin(2 * np.pi * cyc(0.25) * t + 2.0) ** 2)
    clank = np.zeros(n, np.float32)
    for at in (2.3, 5.1, 8.4, 10.7):
        i0 = int(at * SR)
        m = min(int(0.35 * SR), n - i0)
        tt = np.arange(m) / SR
        clank[i0:i0 + m] += np.exp(-tt * 16) * (np.sin(2 * np.pi * 760 * tt) + 0.5 * np.sin(2 * np.pi * 1310 * tt)) * 0.18
    mix = 0.55 * drone + 0.28 * thump + 0.22 * belt + 0.07 * hiss + clank
    # crossfade the last 0.25 s into the first so the loop point is silent of clicks
    fade = int(0.25 * SR)
    ramp = np.linspace(0, 1, fade)
    mix[:fade] = mix[:fade] * ramp + mix[-fade:] * (1 - ramp)
    mix = mix[:-fade]
    mix = mix / np.max(np.abs(mix)) * 0.5
    data = (mix * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, "Machine_Loop.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote Machine_Loop.wav", len(data) / SR, "s")


if __name__ == "__main__":
    main()
