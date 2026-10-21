"""Generates the SC03 old-storage-room textures and the scan frame for the Presentable demo.

Output (re-run: python Tools/generate_sc03_room_art.py):
  Assets/Art/Presentable/2D/Env/SC03_Wall.png      dark damp concrete (tiles)
  Assets/Art/Presentable/2D/Env/SC03_Floor.png     cracked concrete (tiles)
  Assets/Art/Presentable/2D/Env/SC03_Residue.png   white crystalline residue patch (alpha)
  Assets/Art/Presentable/2D/Generated/FX_ScanFrame.png  white corner brackets for the "Selected Label" scan
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENV = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env")
GEN = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Generated")


def noise(size, scale, seed):
    r = np.random.default_rng(seed)
    small = r.random((scale, scale)).astype(np.float32)
    # wrap-pad so the result tiles
    pad = np.pad(small, 2, mode="wrap")
    im = Image.fromarray((pad * 255).astype(np.uint8)).resize(
        (size + 4 * size // scale, size + 4 * size // scale), Image.BICUBIC)
    off = 2 * size // scale
    return np.asarray(im, dtype=np.float32)[off:off + size, off:off + size] / 255.0


def fbm(size, seed):
    return noise(size, 4, seed) * 0.45 + noise(size, 12, seed + 1) * 0.3 + noise(size, 40, seed + 2) * 0.25


def save(img, folder, name):
    os.makedirs(folder, exist_ok=True)
    img.save(os.path.join(folder, name))
    print("wrote", name)


def wall():
    n = 512
    f = fbm(n, 31)
    grain = np.random.default_rng(3).random((n, n)).astype(np.float32)
    base = np.array([104, 100, 92], dtype=np.float32)  # warm grey, old concrete
    damp = noise(n, 6, 77)
    img = base[None, None, :] * (0.7 + 0.35 * f[..., None]) * (0.95 + 0.08 * grain[..., None])
    img *= (1.0 - 0.28 * np.clip(damp - 0.45, 0, 1) * 2.2)[..., None]  # dark damp patches
    # vertical grime streaks
    streak = np.repeat(noise(n, 28, 5)[:1, :], n, axis=0)
    img *= (0.88 + 0.16 * streak)[..., None]
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    # block joints
    for y in (0, 170, 340):
        d.line([(0, y), (n, y)], fill=(56, 54, 50), width=3)
    for y0, off in ((0, 0), (170, 128), (340, 64)):
        for x in range(off, n + 1, 256):
            d.line([(x, y0), (x, y0 + 170)], fill=(60, 58, 54), width=3)
    save(im.filter(ImageFilter.GaussianBlur(0.7)), ENV, "SC03_Wall.png")


def floor():
    n = 512
    f = fbm(n, 61)
    fine = np.random.default_rng(9).random((n, n)).astype(np.float32)
    base = np.array([88, 86, 82], dtype=np.float32)
    img = base[None, None, :] * (0.72 + 0.4 * f[..., None]) * (0.93 + 0.12 * fine[..., None])
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(12)
    for _ in range(7):  # cracks: random walks
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        a = rng.uniform(0, 6.28)
        for _ in range(60):
            a += rng.normal(0, 0.45)
            nx, ny = x + np.cos(a) * 9, y + np.sin(a) * 9
            d.line([(x, y), (nx, ny)], fill=(26, 25, 24), width=int(rng.integers(1, 3)))
            x, y = nx % n, ny % n
    d.line([(0, 0), (n, 0)], fill=(40, 39, 37), width=3)
    d.line([(0, 0), (0, n)], fill=(40, 39, 37), width=3)
    save(im.filter(ImageFilter.GaussianBlur(0.8)), ENV, "SC03_Floor.png")


def residue():
    n = 512
    f = fbm(n, 91)
    yy, xx = np.mgrid[0:n, 0:n]
    r = np.sqrt(((xx - n / 2) / (n / 2)) ** 2 + ((yy - n / 2) / (n / 2)) ** 2)
    mask = np.clip((0.95 - r) * 3.0 + (f - 0.5) * 2.4, 0, 1)
    mask = np.clip((mask - 0.25) * 1.8, 0, 1)
    speck = np.random.default_rng(4).random((n, n)).astype(np.float32)
    shade = 0.82 + 0.18 * speck + 0.12 * (noise(n, 60, 8) - 0.5)
    rgb = np.clip(np.array([206, 208, 201], dtype=np.float32)[None, None, :] * shade[..., None], 0, 255)
    alpha = (mask * (0.78 + 0.22 * speck)) * 0.8 * 255
    im = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA")
    save(im, ENV, "SC03_Residue.png")


def scan_frame():
    n = 512
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    L, t, m = 120, 12, 10
    for sx, sy in ((m, m), (n - m, m), (m, n - m), (n - m, n - m)):
        dx = 1 if sx < n / 2 else -1
        dy = 1 if sy < n / 2 else -1
        d.rectangle([min(sx, sx + dx * L), min(sy, sy + dy * t), max(sx, sx + dx * L), max(sy, sy + dy * t)],
                    fill=(255, 255, 255, 255))
        d.rectangle([min(sx, sx + dx * t), min(sy, sy + dy * L), max(sx, sx + dx * t), max(sy, sy + dy * L)],
                    fill=(255, 255, 255, 255))
    save(im, GEN, "FX_ScanFrame.png")


if __name__ == "__main__":
    wall()
    floor()
    residue()
    scan_frame()
