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


def band(x, lo, hi):
    """Smooth band-pass by FFT (no harsh edges)."""
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    g = np.clip((f - lo) / (lo * 0.5 + 1), 0, 1) * np.exp(-(np.maximum(f - hi, 0) / (hi * 0.35)) ** 2)
    return np.fft.irfft(np.fft.rfft(x) * g, len(x))


def smooth_env(n, a, r):
    t = np.arange(n) / SR
    na, nr = a * SR, r * SR
    return np.minimum(1, t * SR / na) ** 2 * np.minimum(1, (n - t * SR) / nr) ** 2


def slow_mod(n, rate, depth, seed):
    r = np.random.default_rng(seed)
    m = band(r.standard_normal(n), 0.5, rate)
    m = m / max(1e-6, np.max(np.abs(m)))
    return 1 - depth + depth * (0.5 + 0.5 * m)


def fizz():
    """Soft effervescence: gentle small bubble ticks over a quiet band of hiss, no sharp highs."""
    n = int(3.5 * SR)
    hiss = band(rng.standard_normal(n), 1800, 5200) * 0.35 * slow_mod(n, 3, 0.5, 1)
    out = hiss.copy()
    for _ in range(260):
        t0 = rng.integers(0, n - 1500)
        ln = rng.integers(300, 1100)
        t = np.arange(ln) / SR
        f = rng.uniform(1200, 3200) * (1 + 0.6 * t * 80)
        pop = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * rng.uniform(500, 1100)) * rng.uniform(0.05, 0.22)
        out[t0:t0 + ln] += pop
    out = band(out, 600, 6000)
    write("Fizz", out * smooth_env(n, 0.35, 1.0))


def powder():
    """Granular pour: soft mid-band shh with grain texture and a slow swell."""
    n = int(3.6 * SR)
    x = band(rng.standard_normal(n), 500, 3200)
    grains = band(rng.standard_normal(n), 900, 2600) * (rng.random(n) > 0.985) * 6
    out = (x * 0.6 + grains) * slow_mod(n, 4, 0.35, 2)
    out = band(out, 350, 3600)
    write("Powder", out * smooth_env(n, 0.3, 0.9))


def sweep():
    """Bristles brushing tile: two soft strokes of low-mid noise."""
    n = int(1.0 * SR)
    t = np.arange(n) / SR
    x = band(rng.standard_normal(n), 400, 2600)
    stroke = np.maximum(0, np.sin(2 * np.pi * 2.2 * t - 0.4)) ** 1.5
    fine = band(rng.standard_normal(n), 1500, 3500) * 0.25
    write("Sweep", (x + fine) * (0.25 + 0.75 * stroke) * smooth_env(n, 0.12, 0.35))


def rustle():
    """Plastic bag / sock handling: soft low crinkle, no clicks."""
    n = int(0.9 * SR)
    out = np.zeros(n)
    for _ in range(26):
        t0 = rng.integers(0, n - 2200)
        ln = rng.integers(500, 2000)
        w = np.hanning(ln)
        out[t0:t0 + ln] += band(rng.standard_normal(ln), 300, 2200) * w * rng.uniform(0.2, 1)
    out = band(out, 250, 2600)
    write("Rustle", out * smooth_env(n, 0.06, 0.4))


def hood():
    """Fume hood airflow, seamless 12 s loop: broad air rush with slow turbulence, low duct rumble, a faint motor hum."""
    n = SR * 12
    f = np.fft.rfftfreq(n, 1.0 / SR)
    rnd = np.random.default_rng(77)

    def shaped(gain):
        ph = rnd.uniform(0, 2 * np.pi, len(f))
        spec = gain * np.exp(1j * ph)
        spec[0] = 0
        return np.fft.irfft(spec, n)

    ff = np.maximum(f, 1.0)
    rush = (1.0 / np.sqrt(ff)) * np.clip((ff - 90) / 160, 0, 1) * np.exp(-(ff / 2600.0) ** 1.6)
    rush = rush * (1 + 1.6 * np.exp(-((ff - 700) / 380.0) ** 2))
    air = shaped(rush)
    rumble = shaped((1.0 / np.maximum(ff, 20) ** 0.5) * np.exp(-(ff / 160.0) ** 2) * (ff > 25))
    air = air / np.max(np.abs(air))
    rumble = rumble / np.max(np.abs(rumble))
    # slow turbulence: a periodic low-pass noise as amplitude modulation (periodic, so the loop stays seamless)
    mod = shaped((f < 2.5) * 1.0 / np.maximum(f, 0.3))
    mod = mod / np.max(np.abs(mod))
    t = np.arange(n) / SR
    hum = (np.sin(2 * np.pi * 100 * t) * 0.5 + np.sin(2 * np.pi * 200 * t) * 0.25) * (1 + 0.15 * mod)
    out = air * (0.82 + 0.18 * mod) * 0.8 + rumble * 0.32 + hum * 0.05
    write("Hood_Loop", out)


for fn in (fizz, powder, sweep, rustle, hood):
    fn()
print("written to", os.path.abspath(OUT))
