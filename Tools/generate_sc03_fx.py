"""Generates the SC03 detonation textures (flip-books are 6 x 6 frames of 128 px).

Output (re-run: python Tools/generate_sc03_fx.py), Assets/Art/Presentable/2D/Generated:
  FX_FireSheet.png    billowing fireball, hot white -> orange -> dark red, additive
  FX_SmokeSheet.png   dark billowing smoke puff, alpha blended
  FX_Soot.png         scorch mark decal (black, alpha)
  FX_Vignette.png     red screen-edge vignette (transparent centre)
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Generated")
FRAME = 128
GRID = 6


def field(size, cells, seed):
    r = np.random.default_rng(seed)
    small = r.random((cells, cells)).astype(np.float32)
    im = Image.fromarray((small * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255.0


def fbm(size, seed, t=0.0, rise=0.0):
    """Value noise that drifts upwards with time (t = 0..1) and morphs by cross-fading two seeds."""
    out = np.zeros((size, size), np.float32)
    amp, tot = 0.5, 0.0
    for o, cells in enumerate((3, 6, 12, 24, 48)):
        a = field(size, cells, seed + o * 11)
        b = field(size, cells, seed + o * 11 + 5)
        f = (1 - t) * a + t * b
        shift = int(rise * size * (1 + o * 0.4))
        out += np.roll(f, shift, axis=0) * amp
        tot += amp
        amp *= 0.55
    return out / tot


def ramp(x):
    """Temperature 0..1 -> fire colour."""
    stops = [(0.0, (20, 6, 4)), (0.25, (150, 28, 8)), (0.5, (255, 100, 16)), (0.75, (255, 190, 60)), (1.0, (255, 250, 215))]
    x = np.clip(x, 0, 1)
    out = np.zeros(x.shape + (3,), np.float32)
    for (x0, c0), (x1, c1) in zip(stops[:-1], stops[1:]):
        m = (x >= x0) & (x <= x1)
        k = ((x - x0) / (x1 - x0))[..., None]
        out = np.where(m[..., None], np.array(c0) * (1 - k) + np.array(c1) * k, out)
    return out


def fire_frame(i, n):
    t = i / (n - 1)
    s = FRAME
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    u = (xx - s / 2) / (s / 2)
    v = (yy - s * (0.6 - 0.12 * t)) / (s / 2)         # centre drifts up with time
    r = np.sqrt(u * u + (v * (0.85 + 0.35 * t)) ** 2)  # stretched upwards as it rises
    nz = fbm(s, 100, t, rise=0.25 * t)
    nz2 = fbm(s, 200, t, rise=0.4 * t)
    radius = 0.62 + 0.3 * np.sqrt(t)
    edge = radius * (0.35 + 1.15 * nz)
    body = np.clip((edge - r) / (edge * 0.4 + 1e-4), 0, 1)
    env = np.sin(np.clip(t * 1.25, 0, 1) * np.pi * 0.5) * np.clip((1 - t) * 2.6, 0, 1)  # swell, then die out
    temp = (body ** 0.8) * (0.55 + 0.6 * nz2) * (1.15 - 0.95 * t) + 0.18 * body
    rgb = ramp(temp)
    a = np.clip(body * (0.55 + 0.7 * nz2) * env, 0, 1)
    rgb = rgb * a[..., None] / 255.0                    # additive: colour carries the intensity
    rgb = np.clip(rgb * 1.5, 0, 1)
    return np.dstack([rgb * 255, a * 255]).astype(np.uint8)


def smoke_frame(i, n):
    t = i / (n - 1)
    s = FRAME
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    u = (xx - s / 2) / (s / 2)
    v = (yy - s * (0.62 - 0.1 * t)) / (s / 2)
    r = np.sqrt(u * u + (v * 0.9) ** 2)
    nz = fbm(s, 300, t, rise=0.15 * t)
    radius = 0.55 + 0.35 * np.sqrt(t)
    edge = radius * (0.45 + 0.95 * nz)
    body = np.clip((edge - r) / (edge * 0.55 + 1e-4), 0, 1)
    env = np.clip(t * 5.0, 0, 1) * np.clip((1 - t) * 2.2, 0, 1)
    shade = 0.16 + 0.34 * nz + 0.12 * (1 - body)
    a = np.clip(body * env * (0.5 + 0.9 * nz), 0, 1)
    g = np.clip(shade * 255, 0, 255)
    return np.dstack([g, g * 0.97, g * 0.94, a * 255]).astype(np.uint8)


def sheet(fn, name):
    n = GRID * GRID
    im = Image.new("RGBA", (FRAME * GRID, FRAME * GRID), (0, 0, 0, 0))
    for i in range(n):
        f = Image.fromarray(fn(i, n), "RGBA").filter(ImageFilter.GaussianBlur(0.8))
        col, row = i % GRID, i // GRID
        # Unity flip-books read the top row first
        im.paste(f, (col * FRAME, row * FRAME))
    im.save(os.path.join(GEN, name))
    print("wrote", name)


def soot():
    s = 512
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    r = np.sqrt(((xx - s / 2) / (s / 2)) ** 2 + ((yy - s / 2) / (s / 2)) ** 2)
    nz = fbm(s, 41, 0.3)
    mask = np.clip((0.95 - r) * 2.2 + (nz - 0.5) * 1.6, 0, 1) ** 1.3
    a = np.clip(mask * (0.6 + 0.5 * fbm(s, 77)), 0, 1) * 0.8
    rgb = np.dstack([np.full((s, s), 12.0), np.full((s, s), 10.0), np.full((s, s), 9.0)])
    Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA").save(os.path.join(GEN, "FX_Soot.png"))
    print("wrote FX_Soot.png")


def vignette():
    s = 256
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    r = np.sqrt(((xx - s / 2) / (s / 2)) ** 2 + ((yy - s / 2) / (s / 2)) ** 2)
    a = np.clip((r - 0.55) / 0.55, 0, 1) ** 1.6
    rgb = np.dstack([np.full((s, s), 255.0), np.full((s, s), 28.0), np.full((s, s), 20.0)])
    Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA").save(os.path.join(GEN, "FX_Vignette.png"))
    print("wrote FX_Vignette.png")


if __name__ == "__main__":
    os.makedirs(GEN, exist_ok=True)
    sheet(fire_frame, "FX_FireSheet.png")
    sheet(smoke_frame, "FX_SmokeSheet.png")
    soot()
    vignette()
