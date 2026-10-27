"""Generates the Scenario 9 sounds (no licensed audio). Output in Assets/Art/Audio/Sc09 (mono, 22.05 kHz, 16 bit):
  Glass_Break.wav   flask hits the bench: crack, then a shower of glass pings (1.8 s)
  Flask_Knock.wav   glass tapping the steel bench (0.35 s)
  Liquid_Splash.wav liquid splashing on the bench (1.0 s)
  Water_Loop.wav    safety shower / eye wash running (seamless, 4 s)
  Valve.wav         push plate and valve opening (0.5 s)
    python Tools/generate_sc09_audio.py
"""
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Audio", "Sc09")
SR = 22050


def lowpass(x, k):
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def highpass(x, k):
    return x - lowpass(x, k)


def write(name, data, peak=0.6):
    os.makedirs(OUT, exist_ok=True)
    data = data / max(1e-6, np.max(np.abs(data))) * peak
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((data * 32767).astype(np.int16).tobytes())
    print("wrote", name, len(data) / SR, "s")


def ping(n, t0, f, decay, amp):
    t = np.arange(n) / SR
    out = np.zeros(n, dtype=np.float32)
    i0 = int(t0 * SR)
    if i0 >= n:
        return out
    tt = t[: n - i0]
    out[i0:] = amp * np.sin(2 * np.pi * f * tt) * np.exp(-decay * tt)
    return out


def main():
    rng = np.random.default_rng(9)
    # --- glass break
    n = int(1.8 * SR)
    t = np.arange(n) / SR
    crack = highpass(rng.uniform(-1, 1, n).astype(np.float32), 0.15) * np.exp(-t * 30)
    thump = np.sin(2 * np.pi * 120 * t) * np.exp(-t * 25)
    mix = 1.0 * crack + 0.5 * thump
    for _ in range(70):
        t0 = 0.02 + rng.exponential(0.22)
        mix += ping(n, t0, rng.uniform(2200, 7500), rng.uniform(25, 70), rng.uniform(0.05, 0.25))
    write("Glass_Break.wav", mix)
    # --- knock
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    k = np.sin(2 * np.pi * 380 * t) * np.exp(-t * 40) + 0.5 * np.sin(2 * np.pi * 1700 * t) * np.exp(-t * 60)
    k += highpass(rng.uniform(-1, 1, n).astype(np.float32), 0.2) * np.exp(-t * 90) * 0.6
    write("Flask_Knock.wav", k)
    # --- splash
    n = int(1.0 * SR)
    t = np.arange(n) / SR
    s = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.35) * np.exp(-t * 6) * (1 + 0.4 * np.sin(2 * np.pi * 14 * t))
    s += 0.5 * highpass(rng.uniform(-1, 1, n).astype(np.float32), 0.25) * np.exp(-t * 14)
    write("Liquid_Splash.wav", s)
    # --- water loop (shower): band-limited noise with slow modulation, seamless
    sec = 4
    n = SR * (sec + 1)
    t = np.arange(n) / SR
    w = rng.uniform(-1, 1, n).astype(np.float32)
    band = highpass(lowpass(w, 0.55), 0.04)
    mod = 0.85 + 0.15 * np.sin(2 * np.pi * (round(3 * sec) / sec) * t) + 0.05 * np.sin(2 * np.pi * (round(7 * sec) / sec) * t + 1)
    mix = band * mod
    fade = SR
    ramp = np.linspace(0, 1, fade)
    mix[:fade] = mix[:fade] * ramp + mix[-fade:] * (1 - ramp)
    write("Water_Loop.wav", mix[:-fade], 0.45)
    # --- valve
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    v = np.sin(2 * np.pi * 200 * t) * np.exp(-t * 30)
    v += ping(n, 0.0, 1100, 60, 0.6) + ping(n, 0.12, 700, 40, 0.4)
    v += lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.3) * np.clip((t - 0.15) * 4, 0, 1) * np.exp(-(t - 0.15) * 6) * 0.4
    write("Valve.wav", v)


if __name__ == "__main__":
    main()
