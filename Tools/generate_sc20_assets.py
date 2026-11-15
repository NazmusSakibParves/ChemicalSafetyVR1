"""Scenario 20 procedural assets: sounds (numpy only) -> Assets/Art/Audio/Sc20/*.wav, chemical stain texture -> Assets/Art/Presentable/3D/Sc20/Tex,
safety data sheet picture (the client's SDS.jpg, dark background made transparent) -> Assets/Resources/Sc20/Sc20_Sds.png.
Run: python -I Tools/generate_sc20_assets.py "<path to SDS.jpg>"
"""
import os
import sys
import wave

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
AUD = os.path.join(ROOT, "Assets", "Art", "Audio", "Sc20")
TEX = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc20", "Tex")
SR = 44100
rng = np.random.default_rng(20)


def write(name, data, peak=0.7):
    os.makedirs(AUD, exist_ok=True)
    data = np.clip(data / max(1e-6, np.max(np.abs(data))) * peak, -1, 1)
    with wave.open(os.path.join(AUD, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((data * 32767).astype(np.int16).tobytes())
    print(name)


def band(x, lo, hi):
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    g = np.clip((f - lo) / (lo * 0.5 + 1), 0, 1) * np.exp(-(np.maximum(f - hi, 0) / (hi * 0.35)) ** 2)
    return np.fft.irfft(np.fft.rfft(x) * g, len(x))


def loopfade(x, n=int(SR * 0.25)):
    y = x.copy()
    y[:n] = x[:n] * np.linspace(0, 1, n) + x[-n:] * np.linspace(1, 0, n)
    return y[:-n]


def sounds():
    # Shower_Loop: pressurised water hitting a tiled floor, 6 s loop
    n = SR * 6
    w = band(rng.standard_normal(n + int(SR * 0.25)), 600, 9000)
    drops = np.zeros_like(w)
    idx = rng.integers(0, len(w) - 400, 900)
    for i in idx:
        drops[i:i + 300] += np.hanning(300) * rng.uniform(0.2, 0.9) * np.sin(np.arange(300) * rng.uniform(0.2, 0.7))
    sh = loopfade(w / np.max(np.abs(w)) + 0.25 * drops)
    write("Shower_Loop", sh, 0.55)
    # Splash: liquid slapping onto cloth + floor, 1.4 s
    n = int(SR * 1.4)
    t = np.arange(n) / SR
    w = band(rng.standard_normal(n), 300, 5000) * (np.exp(-t * 3.2) * (1 - np.exp(-t * 80)))
    gur = np.sin(2 * np.pi * (180 + 500 * np.exp(-t * 6)) * t) * np.exp(-t * 9) * 0.5
    spl = w / np.max(np.abs(w)) + gur
    for k in range(18):
        i = int(SR * rng.uniform(0.15, 1.2)); m = 260
        if i + m < n:
            spl[i:i + m] += np.hanning(m) * np.sin(np.arange(m) * rng.uniform(0.25, 0.6)) * rng.uniform(0.15, 0.5)
    write("Splash", spl, 0.8)
    # Slosh: liquid moving inside a drum as it jolts, 0.9 s
    n = int(SR * 0.9)
    t = np.arange(n) / SR
    s = band(rng.standard_normal(n), 120, 1400) * np.sin(np.pi * np.clip(t / 0.9, 0, 1)) ** 1.5
    s += 0.5 * np.sin(2 * np.pi * (90 + 60 * np.sin(t * 9)) * t) * np.exp(-t * 4)
    write("Slosh", s, 0.6)
    # Sizzle: corrosive chemical on fabric (hiss + crackle), 3 s
    n = SR * 3
    t = np.arange(n) / SR
    hs = band(rng.standard_normal(n), 3500, 12000) * (0.4 + 0.6 * np.abs(np.sin(t * 5.3))) * np.exp(-t * 0.35)
    for k in range(70):
        i = rng.integers(0, n - 200)
        hs[i:i + 120] += np.hanning(120) * rng.uniform(0.5, 1.5)
    write("Sizzle", hs, 0.35)
    # Pull: the chain / handle of the shower, then the valve opening (thunk + whoosh), 1.2 s
    n = int(SR * 1.2)
    t = np.arange(n) / SR
    th = np.sin(2 * np.pi * 70 * t) * np.exp(-t * 18)
    wh = band(rng.standard_normal(n), 800, 7000) * (1 - np.exp(-t * 14)) * np.exp(-(t - 0.45) ** 2 * 0.0) * np.minimum(1, t * 3)
    cl = np.zeros(n)
    for i in (int(SR * 0.02), int(SR * 0.07)):
        cl[i:i + 900] += np.sin(np.arange(900) * 0.5) * np.exp(-np.arange(900) / 120.0)
    write("Pull", th * 0.8 + wh * 0.35 + cl * 0.6, 0.8)
    # Footstep (running on concrete): 0.18 s
    n = int(SR * 0.18)
    t = np.arange(n) / SR
    fs = band(rng.standard_normal(n), 150, 1800) * np.exp(-t * 45) + np.sin(2 * np.pi * 85 * t) * np.exp(-t * 35) * 0.8
    write("Step", fs, 0.7)
    # Drip: single drop, 0.35 s
    n = int(SR * 0.35)
    t = np.arange(n) / SR
    dr = np.sin(2 * np.pi * (900 + 2400 * np.exp(-t * 40)) * t) * np.exp(-t * 28)
    write("Drip", dr, 0.5)
    # Plant_Loop (quiet hall hum) 6 s
    n = SR * 6 + int(SR * 0.25)
    hum = band(rng.standard_normal(n), 40, 400) * 0.6 + np.sin(2 * np.pi * 50 * np.arange(n) / SR) * 0.15
    write("Hall_Loop", loopfade(hum), 0.25)


def stain_texture():
    # blotchy wet-chemical mask: RGB = pale yellow-green liquid, A = patches with soft rims and drip streaks
    s = 1024
    base = rng.random((40, 40))
    img = Image.fromarray((base * 255).astype(np.uint8)).resize((s, s), Image.BICUBIC)
    m = np.asarray(img, dtype=np.float32) / 255.0
    base2 = rng.random((16, 16))
    m2 = np.asarray(Image.fromarray((base2 * 255).astype(np.uint8)).resize((s, s), Image.BICUBIC), dtype=np.float32) / 255.0
    f = 0.6 * m2 + 0.4 * m
    a = np.clip((f - 0.47) * 5.0, 0, 1) * 0.95
    # drips: vertical streaks hanging under patches
    streak = np.zeros_like(a)
    xs = rng.integers(0, s, 60)
    for x in xs[:28]:
        L = rng.integers(60, 220)
        y0 = rng.integers(0, s - L)
        streak[y0:y0 + L, max(0, x - 2):x + 3] = np.linspace(0.9, 0.0, L)[:, None]
    a = np.clip(a + 0.6 * streak * (np.roll(a, 40, axis=0) > 0.3), 0, 1)
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.2)), dtype=np.float32) / 255.0
    rgb = np.zeros((s, s, 3), dtype=np.float32)
    rgb[..., 0] = 0.17; rgb[..., 1] = 0.15; rgb[..., 2] = 0.05
    rgb *= (0.8 + 0.2 * f[..., None])
    out = np.dstack([np.clip(rgb, 0, 1) * 255, a * 255]).astype(np.uint8)
    os.makedirs(TEX, exist_ok=True)
    Image.fromarray(out, "RGBA").save(os.path.join(TEX, "Sc20_Stain.png"))
    print("Sc20_Stain.png")
    # soft round droplet sprite
    d = 128
    yy, xx = np.mgrid[0:d, 0:d]
    r = np.hypot(xx - d / 2, yy - d / 2) / (d / 2)
    al = np.clip(1 - r, 0, 1) ** 0.6
    drop = np.zeros((d, d, 4), dtype=np.uint8)
    drop[..., 0] = 215; drop[..., 1] = 225; drop[..., 2] = 120; drop[..., 3] = (al * 255).astype(np.uint8)
    # Sc20_Drop.png (liquid drop with a specular highlight), Sc20_WaterDrop.png and Sc20_Puddle.png were made by hand-run snippets (handoff 14.35)


def sds(path):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).copy()
    # the background is a flat dark grey around the clipboard: flood fill from the corners
    from collections import deque
    h, w = a.shape[:2]
    bg = a[2, 2, :3].astype(int)
    seen = np.zeros((h, w), bool)
    dq = deque([(0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)])
    while dq:
        y, x = dq.popleft()
        if y < 0 or x < 0 or y >= h or x >= w or seen[y, x]:
            continue
        if np.abs(a[y, x, :3].astype(int) - bg).sum() > 70:
            continue
        seen[y, x] = True
        dq.extend(((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)))
    a[seen, 3] = 0
    out = Image.fromarray(a, "RGBA")
    os.makedirs(os.path.join(ROOT, "Assets", "Resources", "Sc20"), exist_ok=True)
    out.save(os.path.join(ROOT, "Assets", "Resources", "Sc20", "Sc20_Sds.png"))
    print("Sc20_Sds.png", out.size)


if __name__ == "__main__":
    sounds()
    stain_texture()
    if len(sys.argv) > 1:
        sds(sys.argv[1])
