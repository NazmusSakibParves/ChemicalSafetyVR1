"""Scenario 10 sound effects (procedural, numpy only): Fizz, Powder, Sweep, Rustle -> Assets/Art/Audio/Sc10/*.wav."""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Audio", "Sc10")
rng = np.random.default_rng(10)


def write(name, data):
    os.makedirs(OUT, exist_ok=True)
    data = np.clip(data / max(1e-6, np.max(np.abs(data))) * 0.8, -1, 1)
    pcm = (data * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def lowpass(x, k):
    c = np.cumsum(np.insert(x, 0, 0))
    y = (c[k:] - c[:-k]) / k
    return np.concatenate([y, np.zeros(len(x) - len(y))])


def env(n, a, r):
    e = np.ones(n)
    na, nr = int(a * SR), int(r * SR)
    e[:na] = np.linspace(0, 1, na)
    e[-nr:] = np.linspace(1, 0, nr)
    return e


def fizz():
    n = int(3.5 * SR)
    out = np.zeros(n)
    for _ in range(420):
        t0 = rng.integers(0, n - 2000)
        ln = rng.integers(200, 1400)
        f = rng.uniform(2500, 7000)
        t = np.arange(ln) / SR
        pop = np.sin(2 * np.pi * f * t) * np.exp(-t * rng.uniform(900, 2500)) * rng.uniform(0.05, 0.3)
        out[t0:t0 + ln] += pop
    out += 0.05 * (rng.standard_normal(n) - lowpass(rng.standard_normal(n), 30))
    write("Fizz", out * env(n, 0.2, 0.8))


def powder():
    n = int(3.6 * SR)
    x = rng.standard_normal(n)
    x = lowpass(x, 3) - 0.5 * lowpass(x, 40)
    mod = 0.65 + 0.35 * np.sin(np.arange(n) / SR * 11) * rng.uniform(0.6, 1.0)
    write("Powder", x * mod * env(n, 0.15, 0.6))


def sweep():
    n = int(1.0 * SR)
    x = rng.standard_normal(n)
    x = lowpass(x, 6) - lowpass(x, 80)
    t = np.arange(n) / SR
    mod = 0.5 + 0.5 * np.sin(2 * np.pi * 7 * t) ** 2
    bristle = (rng.random(n) > 0.9) * rng.standard_normal(n) * 0.5
    write("Sweep", (x * mod + 0.2 * bristle) * env(n, 0.05, 0.3))


def rustle():
    n = int(0.9 * SR)
    out = np.zeros(n)
    for _ in range(60):
        t0 = rng.integers(0, n - 800)
        ln = rng.integers(60, 700)
        out[t0:t0 + ln] += rng.standard_normal(ln) * np.exp(-np.arange(ln) / ln * 5) * rng.uniform(0.2, 1)
    out = out - lowpass(out, 25)
    write("Rustle", out * env(n, 0.02, 0.3))


for fn in (fizz, powder, sweep, rustle):
    fn()
print("written to", os.path.abspath(OUT))
