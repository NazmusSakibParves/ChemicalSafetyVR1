"""Scenario 11 sound effects (procedural, numpy only) -> Assets/Art/Audio/Sc11/*.wav.

Yard: Wind_Loop, Gas_Hiss_Loop, Siren_Loop. Wireless: Radio_Static_Loop, Radio_Squelch (push-to-talk key up/down burst),
Radio_Call (the calling chirp), Radio_Roger (end-of-message beep), Radio_Click.
"""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Audio", "Sc11")
rng = np.random.default_rng(11)


def write(name, data, peak=0.6):
    os.makedirs(OUT, exist_ok=True)
    data = np.clip(data / max(1e-6, np.max(np.abs(data))) * peak, -1, 1)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((data * 32767).astype(np.int16).tobytes())


def band(x, lo, hi):
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    g = np.clip((f - lo) / (lo * 0.5 + 1), 0, 1) * np.exp(-(np.maximum(f - hi, 0) / (hi * 0.35)) ** 2)
    return np.fft.irfft(np.fft.rfft(x) * g, len(x))


def seamless(x, fade):
    n = int(fade * SR)
    y = x.copy()
    w = np.linspace(0, 1, n)
    y[:n] = x[:n] * w + x[-n:] * (1 - w)
    return y[:-n]


def slow(n, hi, seed, depth):
    m = band(np.random.default_rng(seed).standard_normal(n), 0.2, hi)
    m /= max(1e-6, np.max(np.abs(m)))
    return 1 - depth + depth * (0.5 + 0.5 * m)


def wind():
    n = int(14 * SR)
    base = band(rng.standard_normal(n), 60, 520) * slow(n, 0.6, 1, 0.7)
    gust = band(rng.standard_normal(n), 300, 1400) * slow(n, 0.35, 2, 0.9) * 0.35
    return seamless(base + gust, 2.0)


def gas_hiss():
    n = int(8 * SR)
    h = band(rng.standard_normal(n), 2500, 9000) * slow(n, 4, 3, 0.25)
    h += band(rng.standard_normal(n), 900, 2200) * 0.3
    return seamless(h, 1.0)


def siren():
    """Slow industrial wail (rise and fall), a few harmonics, a little room tail."""
    n = int(6 * SR)
    t = np.arange(n) / SR
    f = 620 + 300 * (0.5 - 0.5 * np.cos(2 * np.pi * t / 3.0))
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.sin(ph) + 0.45 * np.sin(2 * ph) + 0.2 * np.sin(3 * ph)
    s = np.tanh(s * 1.4)
    return s


def static():
    n = int(6 * SR)
    x = band(rng.standard_normal(n), 400, 3200) * slow(n, 7, 4, 0.5)
    crackle = (rng.random(n) > 0.9993) * rng.standard_normal(n) * 6
    return seamless(x + band(crackle, 600, 3500) * 0.5, 0.8)


def squelch():
    n = int(0.32 * SR)
    t = np.arange(n) / SR
    burst = band(rng.standard_normal(n), 500, 3800) * np.exp(-t * 12)
    thump = np.sin(2 * np.pi * 180 * t) * np.exp(-t * 60) * 0.7
    return burst + thump


def tone(freq, dur, amp=1.0):
    t = np.arange(int(dur * SR)) / SR
    e = np.minimum(1, t / 0.005) * np.minimum(1, (dur - t) / 0.01)
    return amp * np.sign(np.sin(2 * np.pi * freq * t)) * 0.35 * e + amp * np.sin(2 * np.pi * freq * t) * 0.6 * e


def call():
    gap = np.zeros(int(0.07 * SR))
    parts = [tone(1450, 0.09), gap, tone(1900, 0.09), gap, tone(1450, 0.09), gap, tone(1900, 0.14)]
    return band(np.concatenate(parts), 400, 3400)


def roger():
    gap = np.zeros(int(0.05 * SR))
    return band(np.concatenate([tone(1250, 0.11), gap, tone(1650, 0.14)]), 400, 3400)


def click():
    n = int(0.06 * SR)
    t = np.arange(n) / SR
    return band(rng.standard_normal(n), 700, 4500) * np.exp(-t * 90) + np.sin(2 * np.pi * 240 * t) * np.exp(-t * 110)


def murmur():
    """Non-verbal crowd bed: several overlapping 'voices' (harmonic buzz through vowel formants, syllable-rate gating) + shuffling."""
    n = int(10 * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    r = np.random.default_rng(5)
    for v in range(9):
        f0 = r.uniform(95, 210)
        vib = f0 * (1 + 0.03 * np.sin(2 * np.pi * r.uniform(4, 6) * t + r.uniform(0, 6)))
        ph = 2 * np.pi * np.cumsum(vib) / SR
        src = sum(np.sin(h * ph) / h for h in range(1, 14))
        form = band(src, r.uniform(500, 800), r.uniform(1300, 2200))
        gate = np.clip(band(r.standard_normal(n), 0.5, r.uniform(2.5, 4.5)), -1, 1)
        gate = np.clip(gate / max(1e-6, np.max(np.abs(gate))) * 1.6, 0, 1) ** 1.5
        out += form * gate
    out += band(r.standard_normal(n), 200, 2500) * 0.25
    return seamless(out, 1.5)


def main():
    write("Crowd_Murmur_Loop", murmur(), 0.45)
    write("Wind_Loop", wind(), 0.5)
    write("Gas_Hiss_Loop", gas_hiss(), 0.45)
    write("Siren_Loop", siren(), 0.5)
    write("Radio_Static_Loop", static(), 0.35)
    write("Radio_Squelch", squelch(), 0.6)
    write("Radio_Call", call(), 0.6)
    write("Radio_Roger", roger(), 0.55)
    write("Radio_Click", click(), 0.6)
    print("wrote Sc11 audio")


if __name__ == "__main__":
    main()
