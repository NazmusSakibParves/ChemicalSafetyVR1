"""Generates the touch-button icon, rack upright texture and drum detail texture.

Output: Assets/Art/Presentable/2D/Env/ICON_Touch.png, RACK_Upright.png, DRUM_Detail.png
Re-run: python Tools/generate_props_art.py   (needs numpy + Pillow)
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env")
rnd = random.Random(3)


def icon_touch(size=256):
    """White pointing-finger glyph with tap ripples (tinted in Unity)."""
    s = size
    img = Image.new("RGBA", (s, s), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = (255, 255, 255, 255)
    # finger
    d.rounded_rectangle((s * 0.40, s * 0.10, s * 0.56, s * 0.58), radius=s * 0.08, fill=w)
    # palm / fist
    d.rounded_rectangle((s * 0.30, s * 0.46, s * 0.74, s * 0.84), radius=s * 0.12, fill=w)
    # thumb
    d.rounded_rectangle((s * 0.22, s * 0.56, s * 0.38, s * 0.74), radius=s * 0.07, fill=w)
    # knuckle separators (cut-outs)
    for x in (0.495, 0.575, 0.655):
        d.line((s * x, s * 0.60, s * x, s * 0.72), fill=(255, 255, 255, 0), width=int(s * 0.012))
    # tap ripples around the fingertip
    for r, a in ((0.20, 220), (0.30, 140)):
        d.arc((s * (0.48 - r), s * (0.12 - r), s * (0.48 + r), s * (0.12 + r)), 200, 340,
              fill=(255, 255, 255, a), width=int(s * 0.025))
    img.save(os.path.join(OUT, "ICON_Touch.png"))


def rack_upright(w=64, h=1024):
    """Blue perforated upright: slot every 5 cm on a 4.4 m post, edge highlights."""
    img = Image.new("RGB", (w, h), (30, 85, 205))
    d = ImageDraw.Draw(img)
    for y in range(8, h - 8, 12):
        d.rounded_rectangle((w * 0.36, y, w * 0.64, y + 6), radius=3, fill=(10, 28, 70))
    d.rectangle((0, 0, 4, h), fill=(70, 125, 235))
    d.rectangle((w - 5, 0, w, h), fill=(16, 50, 140))
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    img.save(os.path.join(OUT, "RACK_Upright.png"))


def fnoise(shape, scale, seed):
    r = np.random.default_rng(seed)
    n = r.standard_normal(shape).astype(np.float32)
    f = np.fft.rfft2(n)
    fy = np.fft.fftfreq(shape[0])[:, None]
    fx = np.fft.rfftfreq(shape[1])[None, :]
    k = np.sqrt(fx * fx + fy * fy) * scale + 1e-4
    f *= np.exp(-(k * k))
    o = np.fft.irfft2(f, s=shape)
    o -= o.min()
    o /= o.max()
    return o


def drum_detail(w=512, h=512):
    """Greyscale multiply texture: u wraps around the drum, v runs bottom (0) to top (1)."""
    grain = fnoise((h, w), 30, 1)
    mott = fnoise((h, w), 6, 2)
    base = 0.86 + 0.08 * grain + 0.06 * mott
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None]  # row 0 = top of image = v 1
    v = v[::-1]
    # rolling hoops (darker band + highlight) at v 0.30 and 0.70, chime rims at the ends
    for c in (0.30, 0.70):
        base -= 0.30 * np.exp(-((v - c) / 0.014) ** 2)
        base += 0.10 * np.exp(-((v - c - 0.03) / 0.010) ** 2)
    for c in (0.02, 0.98):
        base -= 0.18 * np.exp(-((v - c) / 0.03) ** 2)
    # dirt toward the bottom, brighter towards the top where light hits
    base *= (0.78 + 0.28 * v)
    # vertical rust / grime streaks
    streak = fnoise((h, w), 70, 5)
    streak = np.clip((streak - 0.62) * 4.0, 0, 1)
    streak *= np.clip(1.2 - v, 0, 1)
    base -= 0.22 * streak
    img = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8), "L").convert("RGB")
    d = ImageDraw.Draw(img)
    for _ in range(260):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h)
        a = rnd.uniform(0, math.tau)
        l = rnd.uniform(6, 40)
        c = rnd.randint(150, 230) if rnd.random() < 0.35 else rnd.randint(70, 130)
        d.line((x, y, x + math.cos(a) * l, y + math.sin(a) * l), fill=(c, c, c), width=rnd.randint(1, 2))
    img = img.filter(ImageFilter.GaussianBlur(0.7))
    img.save(os.path.join(OUT, "DRUM_Detail.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    icon_touch()
    rack_upright()
    drum_detail()
    print("generated in", OUT)


if __name__ == "__main__":
    main()
