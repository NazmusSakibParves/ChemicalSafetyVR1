"""Scenario 19 sound effects (procedural, numpy only) -> Assets/Art/Audio/Sc19/*.wav.

Plant_Loop (distant chemical plant), Blower_Loop (ventilator motor and air), Gasp (laboured breathing in), Thud (body falling on a steel floor),
Beep (gas meter OK), Clank (metal clip / tripod), Pen (pen on paper), and Cough_Synth_0..2 (stand-in coughs: used only when no real recorded
Cough_*.wav is present in the folder; the builder prefers Cough_0..2 if they exist).
Run: python -I Tools/generate_sc19_audio.py
"""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Audio", "Sc19")
rng = np.random.default_rng(19)


def write(name, data, peak=0.7):
    os.makedirs(OUT, exist_ok=True)
    data = np.clip(data / max(1e-6, np.max(np.abs(data))) * peak, -1, 1)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((data * 32767).astype(np.int16).tobytes())
    print(name)


def band(x, lo, hi):
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    g = np.clip((f - lo) / (lo * 0.5 + 1), 0, 1) * np.exp(-(np.maximum(f - hi, 0) / (hi * 0.35)) ** 2)
    return np.fft.irfft(np.fft.rfft(x) * g, len(x))


def formant(x, centres, widths, gains):
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    g = np.zeros_like(f)
    for c, w, a in zip(centres, widths, gains):
        g += a * np.exp(-((f - c) / w) ** 2)
    return np.fft.irfft(np.fft.rfft(x) * g, len(x))


def seamless(x, fade):
    n = int(fade * SR)
    y = x.copy()
    w = np.linspace(0, 1, n)
    y[:n] = x[:n] * w + x[-n:] * (1 - w)
    return y[:-n]


def env(n, attack, decay, power=2.0):
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-t / decay) ** power if power else np.exp(-t / decay)
    return a * d


def plant():
    n = int(16 * SR)
    low = band(rng.standard_normal(n), 30, 220) * 0.8
    mid = band(rng.standard_normal(n), 300, 1500) * 0.12
    # slow breathing of the plant
    m = band(rng.standard_normal(n), 0.1, 0.6)
    m = 0.6 + 0.4 * m / max(1e-6, np.max(np.abs(m)))
    return seamless((low + mid) * m, 2.0)


def blower():
    n = int(6 * SR)
    t = np.arange(n) / SR
    motor = 0.5 * np.sin(2 * np.pi * 96 * t) + 0.3 * np.sin(2 * np.pi * 192 * t) + 0.15 * np.sin(2 * np.pi * 288 * t)
    air = band(rng.standard_normal(n), 200, 3500) * 0.9
    blade = (0.5 + 0.5 * np.sin(2 * np.pi * 24 * t)) * band(rng.standard_normal(n), 400, 1800) * 0.25
    return seamless(motor * 0.5 + air + blade, 1.0)


def gasp():
    n = int(1.7 * SR)
    x = rng.standard_normal(n)
    breath = formant(x, [500, 1400, 2600], [350, 600, 900], [1.0, 0.7, 0.35])
    t = np.arange(n) / SR
    e = np.clip(t / 0.55, 0, 1) ** 1.5 * np.exp(-np.maximum(t - 0.75, 0) / 0.18)
    rasp = 1 + 0.5 * np.sin(2 * np.pi * 38 * t)
    return breath * e * rasp


def thud():
    n = int(1.1 * SR)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * (70 - 35 * np.minimum(t, 0.25) / 0.25) * t) * np.exp(-t / 0.12)
    cloth = band(rng.standard_normal(n), 60, 900) * np.exp(-t / 0.06) * 0.8
    steel = (np.sin(2 * np.pi * 210 * t) * 0.15 + np.sin(2 * np.pi * 530 * t) * 0.1) * np.exp(-t / 0.35)
    return body + cloth + steel


def beep():
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    s = np.sin(2 * np.pi * 2200 * t) * ((t < 0.12) | ((t > 0.22) & (t < 0.34)))
    return s * 0.8


def clank():
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    y = np.zeros(n)
    for f, a, d in [(840, 1, 0.09), (1330, 0.7, 0.07), (2210, 0.5, 0.05), (3480, 0.3, 0.04)]:
        y += a * np.sin(2 * np.pi * f * t) * np.exp(-t / d)
    y += band(rng.standard_normal(n), 1800, 6000) * np.exp(-t / 0.012) * 0.6
    return y


def pen():
    n = int(2.2 * SR)
    x = band(rng.standard_normal(n), 1800, 7000)
    t = np.arange(n) / SR
    m = (0.5 + 0.5 * np.sin(2 * np.pi * 5.5 * t + 2 * np.sin(2 * np.pi * 1.1 * t))) ** 2
    return x * m * np.clip(t / 0.1, 0, 1) * np.clip((2.2 - t) / 0.2, 0, 1) * 0.5


def cough(seed, pitch, long):
    """A man's cough: a short glottal 'eh', an explosive burst, a rough tail. Formants of an open 'uh'."""
    r = np.random.default_rng(seed)
    n = int((0.95 if long else 0.7) * SR)
    t = np.arange(n) / SR
    noise = r.standard_normal(n)
    burst = formant(noise, [450, 1100, 2300, 3600], [280, 400, 650, 800], [1.0, 0.8, 0.45, 0.2]) * np.exp(-t / 0.075)
    # voiced part: a falling glottal pitch with jitter
    f0 = pitch * (1.0 - 0.25 * np.minimum(t / 0.5, 1))
    ph = 2 * np.pi * np.cumsum(f0 + r.standard_normal(n) * 6) / SR
    voice = sum(np.sin(ph * k) / k for k in range(1, 9))
    voice = formant(voice, [600, 1200, 2400], [350, 500, 700], [1.0, 0.6, 0.25])
    venv = env(n, 0.012, 0.22, 1.0)
    rough = 1 + 0.55 * np.sin(2 * np.pi * 58 * t)
    first = burst * 1.6 + voice * venv * rough * 0.45
    # a second small pulse
    delay = int(0.2 * SR)
    second = np.zeros(n)
    second[delay:] = (burst[:n - delay] * 0.6 + voice[:n - delay] * venv[:n - delay] * 0.25)
    y = first + second
    return y * np.clip((n - np.arange(n)) / (0.08 * SR), 0, 1)


write("Plant_Loop", plant(), 0.45)
write("Blower_Loop", blower(), 0.55)
write("Gasp", gasp(), 0.75)
write("Thud", thud(), 0.9)
write("Beep", beep(), 0.5)
write("Clank", clank(), 0.7)
write("Pen", pen(), 0.5)
for i, (p, l) in enumerate([(118, False), (108, True), (124, False)]):
    write("Cough_Synth_%d" % i, cough(200 + i, p, l), 0.85)
