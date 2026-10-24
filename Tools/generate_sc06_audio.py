"""Generates the Scenario 6 lab sounds (no licensed audio needed). Output in Assets/Art/Audio/Sc06 (mono, 22.05 kHz, 16 bit):
  Room_Loop.wav   quiet air-conditioning hum + fluorescent tube buzz (seamless, 8 s)
  Hood_Loop.wav   fume hood motor and the rush of air into the sash opening (seamless, 6 s)
  Switch_Click.wav  rocker switch
  Sash_Slide.wav    glass sash sliding up on its counterweights (1.8 s)
  Pour_Loop.wav     liquid pouring into a jar (seamless, 2 s)
    python Tools/generate_sc06_audio.py
All levels are modest on purpose; Sc06Ambience lowers the loops under the voice-over.
"""
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Audio", "Sc06")
SR = 22050


def lowpass(x, k):
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def write(name, data):
    os.makedirs(OUT, exist_ok=True)
    data = data / max(1e-6, np.max(np.abs(data))) * 0.5
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((data * 32767).astype(np.int16).tobytes())
    print("wrote", name, len(data) / SR, "s")


def seamless(mix, fade_s):
    fade = int(fade_s * SR)
    ramp = np.linspace(0, 1, fade)
    mix[:fade] = mix[:fade] * ramp + mix[-fade:] * (1 - ramp)
    return mix[:-fade]


def main():
    rng = np.random.default_rng(6)
    # --- room
    sec = 8
    n = SR * (sec + 1)
    t = np.arange(n) / SR
    cyc = lambda f: round(f * sec) / sec
    hum = 0.5 * np.sin(2 * np.pi * cyc(60) * t) + 0.25 * np.sin(2 * np.pi * cyc(120) * t + 0.5) + 0.1 * np.sin(2 * np.pi * cyc(240) * t)
    air = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.05) * (0.7 + 0.3 * np.sin(2 * np.pi * cyc(0.25) * t))
    buzz = 0.04 * np.sin(2 * np.pi * cyc(100) * t) * (1 + np.sin(2 * np.pi * cyc(1) * t) * 0.2)
    write("Room_Loop.wav", seamless(0.4 * hum + 0.9 * air + buzz, 1.0))
    # --- hood
    sec = 6
    n = SR * (sec + 1)
    t = np.arange(n) / SR
    cyc = lambda f: round(f * sec) / sec
    rush_src = rng.uniform(-1, 1, n).astype(np.float32)
    rush = lowpass(rush_src, 0.16) * (0.75 + 0.25 * np.sin(2 * np.pi * cyc(0.5) * t + 1.0))
    motor = 0.35 * np.sin(2 * np.pi * cyc(96) * t) + 0.2 * np.sin(2 * np.pi * cyc(192) * t + 0.4) + 0.08 * np.sin(2 * np.pi * cyc(288) * t)
    flutter = 0.05 * (rush_src - lowpass(rush_src, 0.4))
    write("Hood_Loop.wav", seamless(0.9 * rush + 0.5 * motor + flutter, 0.8))
    # --- click
    n = int(0.12 * SR)
    t = np.arange(n) / SR
    click = np.exp(-t * 220) * np.sin(2 * np.pi * 2400 * t) * 0.8
    click[int(0.055 * SR):] += (np.exp(-t[: n - int(0.055 * SR)] * 260) * np.sin(2 * np.pi * 1700 * t[: n - int(0.055 * SR)]) * 0.6)[: n - int(0.055 * SR)]
    write("Switch_Click.wav", click.astype(np.float32))
    # --- sash slide
    n = int(1.8 * SR)
    t = np.arange(n) / SR
    env = np.sin(np.pi * np.clip(t / 1.8, 0, 1)) ** 0.7
    noise = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.09)
    rumble = np.sin(2 * np.pi * 58 * t) * 0.4 + np.sin(2 * np.pi * (180 + 40 * t) * t) * 0.12
    slide = (noise * 0.8 + rumble) * env
    thunk = np.zeros(n, np.float32)
    k = int(0.12 * SR)
    thunk[-k:] = np.exp(-np.arange(k) / SR * 40) * np.sin(2 * np.pi * 85 * np.arange(k) / SR) * 0.9
    write("Sash_Slide.wav", slide + thunk)
    # --- pour
    sec = 2
    n = SR * sec + int(0.3 * SR)
    t = np.arange(n) / SR
    src = rng.uniform(-1, 1, n).astype(np.float32)
    water = (src - lowpass(src, 0.25)) * 0.6
    bubbles = np.zeros(n, np.float32)
    for _ in range(26):
        at = int(rng.uniform(0, n - 3000))
        f = rng.uniform(500, 1400)
        m = 2200
        tt = np.arange(m) / SR
        bubbles[at:at + m] += np.exp(-tt * 40) * np.sin(2 * np.pi * (f + 900 * tt) * tt) * 0.35
    body = lowpass(src, 0.04) * 0.9
    write("Pour_Loop.wav", seamless(water + bubbles + body, 0.3))


if __name__ == "__main__":
    main()
