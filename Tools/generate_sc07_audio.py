"""Generates the Scenario 7 (factory fire) sounds without licensed audio. Output in Assets/Art/Audio/Sc07 (mono, 32 kHz, 16 bit):
  Alarm_Loop.wav      electric fire bell: a motor-driven hammer on a bell shell (inharmonic partials), room echo, seamless 4 s
  Step_Walk_0..5.wav  work boots on concrete, walking (heel thud + leather scuff + a little grit), 0.28 s each
  Step_Run_0..5.wav   the same, running (harder, shorter), 0.2 s each
  Fire_Loop.wav       fire: low roar + crackles and pops, seamless 4 s
  Co2_Hiss.wav        CO2 extinguisher discharge: loud gas hiss, seamless 2 s
  Hall_Loop.wav       big factory hall: low machine hum, far pump rumble, air, seamless 8 s
    python Tools/generate_sc07_audio.py
Levels are modest; Sc07Ambience / Sc07Human balance them and lower the loops under the voice-over.
"""
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Audio", "Sc07")
SR = 32000


def lowpass(x, k):
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def highpass(x, k):
    return x - lowpass(x, k)


def write(name, data, peak=0.55):
    os.makedirs(OUT, exist_ok=True)
    data = data / max(1e-6, np.max(np.abs(data))) * peak
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((data * 32767).astype(np.int16).tobytes())
    print("wrote", name, round(len(data) / SR, 2), "s")


def seamless(mix, fade_s):
    fade = int(fade_s * SR)
    ramp = np.linspace(0, 1, fade)
    mix[:fade] = mix[:fade] * ramp + mix[-fade:] * (1 - ramp)
    return mix[:-fade]


def echo(x, taps):
    y = x.copy()
    for delay, gain in taps:
        d = int(delay * SR)
        y[d:] += x[:-d] * gain
    return y


def alarm(rng):
    sec = 4
    n = SR * sec
    out = np.zeros(n + SR, np.float32)  # tail wraps around for a seamless loop
    rate = 17.0  # hammer strikes per second
    partials = [(1180, 1.0, 22), (1940, 0.8, 30), (2570, 0.55, 38), (3290, 0.45, 46), (4310, 0.25, 60), (5700, 0.12, 80)]
    mlen = int(0.18 * SR)
    tt = np.arange(mlen) / SR
    strike = np.zeros(mlen, np.float32)
    for f, g, dec in partials:
        strike += (g * np.exp(-tt * dec) * np.sin(2 * np.pi * f * tt + rng.uniform(0, 6))).astype(np.float32)
    strike += (np.exp(-tt * 400) * rng.uniform(-1, 1, mlen) * 0.6).astype(np.float32)  # hammer click
    k = int(sec * rate)
    for i in range(k):
        at = int((i / rate + rng.uniform(-0.0025, 0.0025)) * SR)
        if at < 0:
            at = 0
        amp = 0.85 + 0.15 * (i % 2)
        out[at:at + mlen] += strike * amp
    out[:n] += out[n:n + len(out) - n][:n] if False else 0
    wrapped = out[:n].copy()
    tail = out[n:]
    wrapped[:len(tail)] += tail
    wrapped = echo(wrapped, [(0.045, 0.28), (0.11, 0.18), (0.21, 0.1), (0.37, 0.06)])
    return wrapped


def step(rng, run, variant):
    dur = 0.2 if run else 0.28
    n = int(dur * SR)
    t = np.arange(n) / SR
    pitch = rng.uniform(0.9, 1.15)
    thud = np.sin(2 * np.pi * (95 + 25 * np.exp(-t * 40)) * pitch * t) * np.exp(-t * (46 if run else 34))
    body = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.16) * np.exp(-t * (52 if run else 40))
    click_len = int(0.02 * SR)
    click = np.zeros(n, np.float32)
    nz = highpass(rng.uniform(-1, 1, click_len).astype(np.float32), 0.15)
    click[:click_len] = nz * np.exp(-np.arange(click_len) / SR * 220) * (0.9 if run else 0.55)
    scuff = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.28)
    scuff_env = np.exp(-((t - 0.06) / 0.05) ** 2) * (0.12 if run else 0.22)
    grit = np.zeros(n, np.float32)
    for _ in range(int(rng.integers(3, 7))):
        at = int(rng.uniform(0.01, dur * 0.7) * SR)
        m = int(0.004 * SR)
        grit[at:at + m] += rng.uniform(-1, 1, m).astype(np.float32) * 0.25
    s = thud * (1.0 if run else 0.85) + body * 0.7 + click + scuff * scuff_env + grit
    s = echo(s, [(0.02, 0.2), (0.055, 0.1)])
    return s[:n]


def fire(rng):
    sec = 4
    n = SR * (sec + 1)
    t = np.arange(n) / SR
    cyc = lambda f: round(f * sec) / sec
    src = rng.uniform(-1, 1, n).astype(np.float32)
    roar = lowpass(src, 0.035) * (0.8 + 0.2 * np.sin(2 * np.pi * cyc(0.5) * t)) + 0.4 * lowpass(src, 0.012)
    crackle = np.zeros(n, np.float32)
    for _ in range(95):
        at = int(rng.uniform(0, n - 2400))
        m = int(rng.uniform(0.002, 0.012) * SR)
        e = np.exp(-np.arange(m) / m * 6)
        crackle[at:at + m] += rng.uniform(-1, 1, m).astype(np.float32) * e * rng.uniform(0.2, 1.0)
    pops = np.zeros(n, np.float32)
    for _ in range(14):
        at = int(rng.uniform(0, n - 4000))
        m = int(0.025 * SR)
        tt = np.arange(m) / SR
        pops[at:at + m] += np.sin(2 * np.pi * rng.uniform(180, 420) * tt) * np.exp(-tt * 120) * 0.7
    return seamless(roar * 0.9 + crackle * 0.8 + pops, 0.6)


def hiss(rng):
    sec = 2
    n = SR * (sec + 1)
    t = np.arange(n) / SR
    src = rng.uniform(-1, 1, n).astype(np.float32)
    gas = highpass(src, 0.08) * (0.85 + 0.15 * np.sin(2 * np.pi * 3 * t))
    body = lowpass(src, 0.1) * 0.35
    return seamless(gas + body, 0.4)


def hall(rng):
    sec = 8
    n = SR * (sec + 1)
    t = np.arange(n) / SR
    cyc = lambda f: round(f * sec) / sec
    hum = 0.55 * np.sin(2 * np.pi * cyc(50) * t) + 0.3 * np.sin(2 * np.pi * cyc(100) * t + 0.7) + 0.12 * np.sin(2 * np.pi * cyc(150) * t)
    pump = 0.35 * np.sin(2 * np.pi * cyc(28) * t) * (0.7 + 0.3 * np.sin(2 * np.pi * cyc(0.75) * t))
    air = lowpass(rng.uniform(-1, 1, n).astype(np.float32), 0.04) * (0.7 + 0.3 * np.sin(2 * np.pi * cyc(0.125) * t))
    return seamless(hum * 0.7 + pump + air * 0.9, 1.0)


def door(rng):
    """Fire-exit door pushed open: push-bar clack, a hinge creak and a soft bang when it reaches the stop (1.5 s)."""
    n = int(1.5 * SR)
    t = np.arange(n) / SR
    out = np.zeros(n, np.float32)
    # push-bar clack at 0.02 s: two metallic ticks
    for at, f0, g in ((0.02, 1900, 0.9), (0.075, 1300, 0.6)):
        m = int(0.07 * SR)
        tt = np.arange(m) / SR
        k = int(at * SR)
        out[k:k + m] += (np.exp(-tt * 90) * np.sin(2 * np.pi * f0 * tt) * g + np.exp(-tt * 300) * rng.uniform(-1, 1, m) * 0.4).astype(np.float32)
    # hinge creak 0.12 .. 0.8 s: a slow rising squeal with noise
    lo, hi = int(0.12 * SR), int(0.8 * SR)
    tt = np.arange(hi - lo) / SR
    env = np.sin(np.pi * tt / tt[-1]) ** 1.5
    freq = 380 + 260 * tt / tt[-1] + 30 * np.sin(2 * np.pi * 11 * tt)
    creak = np.sin(2 * np.pi * np.cumsum(freq) / SR) * 0.22 + np.sin(2 * np.pi * np.cumsum(freq * 2.02) / SR) * 0.1
    out[lo:hi] += (creak * env + lowpass(rng.uniform(-1, 1, hi - lo).astype(np.float32), 0.2) * 0.15 * env).astype(np.float32)
    # soft bang at the stop (0.78 s)
    k = int(0.78 * SR)
    m = n - k
    tt = np.arange(m) / SR
    bang = np.sin(2 * np.pi * (85 + 30 * np.exp(-tt * 18)) * tt) * np.exp(-tt * 9) + 0.5 * lowpass(rng.uniform(-1, 1, m).astype(np.float32), 0.18) * np.exp(-tt * 14)
    out[k:] += (bang * 0.9).astype(np.float32)
    return echo(out, [(0.06, 0.25), (0.15, 0.12)])


def main():
    rng = np.random.default_rng(7)
    write("Alarm_Loop.wav", alarm(rng), 0.6)
    for i in range(6):
        write("Step_Walk_%d.wav" % i, step(rng, False, i), 0.5)
        write("Step_Run_%d.wav" % i, step(rng, True, i), 0.6)
    write("Fire_Loop.wav", fire(rng), 0.5)
    write("Co2_Hiss.wav", hiss(rng), 0.5)
    write("Hall_Loop.wav", hall(rng), 0.45)
    write("Door_Open.wav", door(rng), 0.6)


if __name__ == "__main__":
    main()
