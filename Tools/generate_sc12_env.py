"""Scenario 12 (PPE Donning Station) environment textures, procedural and seamless: grey stucco wall, acoustic ceiling tile,
polished concrete floor, light wood for the lockers, yellow painted band, steel frame. Writes Assets/Art/Presentable/2D/Sc12Tex.

    python Tools/generate_sc12_env.py
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc12Tex")
S = 1024
rng = np.random.default_rng(12)


def noise(size, beta, seed):
    """seamless 1/f^beta noise in [0,1]"""
    r = np.random.default_rng(seed)
    f = np.fft.fft2(r.standard_normal((size, size)))
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    k = np.sqrt(fx * fx + fy * fy)
    k[0, 0] = 1.0
    f = f / k ** beta
    f[0, 0] = 0
    n = np.real(np.fft.ifft2(f))
    n -= n.min()
    return n / n.max()


def save(name, arr):
    os.makedirs(OUT, exist_ok=True)
    im = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def tint(base, n, amp):
    return np.stack([base[i] * (1 - amp + 2 * amp * n) for i in range(3)], axis=-1)


def wall():
    n = 0.55 * noise(S, 1.6, 1) + 0.30 * noise(S, 1.0, 2) + 0.15 * noise(S, 0.5, 3)
    speck = (rng.random((S, S)) > 0.985).astype(float) * 0.12
    img = tint((150, 152, 148), n, 0.16) - speck[..., None] * 60
    # faint vertical streaks (old paint)
    streak = noise(S, 2.2, 4)
    streak = np.tile(streak[:8].mean(axis=0, keepdims=True), (S, 1))
    img *= (0.96 + 0.08 * streak)[..., None]
    save("Sc12_Wall", img)


def ceiling():
    img = np.zeros((S, S, 3))
    tile = S // 2  # 2 x 2 tiles per texture
    n = noise(S, 1.2, 5)
    base = tint((226, 226, 220), n, 0.05)
    img[:] = base
    # perforations
    for y in range(0, S, 16):
        for x in range(0, S, 16):
            cx = x + 8 + (8 if (y // 16) % 2 else 0)
            if rng.random() < 0.9:
                yy, xx = np.ogrid[:S, :S]
    pit = noise(S, 0.2, 6) > 0.72
    img[pit] *= 0.86
    # grid lines (T-bar)
    w = 6
    for g in range(0, S, tile):
        img[max(0, g - w // 2):g + w // 2 + 1, :] = (92, 94, 96)
        img[:, max(0, g - w // 2):g + w // 2 + 1] = (92, 94, 96)
    img[-3:, :] = (92, 94, 96)
    img[:, -3:] = (92, 94, 96)
    save("Sc12_Ceiling", img)


def floor():
    n = 0.5 * noise(S, 1.8, 7) + 0.35 * noise(S, 1.1, 8) + 0.15 * noise(S, 0.4, 9)
    img = tint((128, 130, 128), n, 0.14)
    # mottling and a few speckles of aggregate
    sp = (rng.random((S, S)) > 0.992).astype(float)
    img += sp[..., None] * 38 - ((rng.random((S, S)) > 0.992).astype(float))[..., None] * 38
    # control joint lines every half texture
    for g in (0, S // 2):
        img[max(0, g - 1):g + 2, :] *= 0.62
        img[:, max(0, g - 1):g + 2] *= 0.62
    save("Sc12_Floor", img)


def wood():
    # long grain along x, honey / pine like the lockers in the workflow
    g = noise(S, 2.6, 10)
    g = np.tile(g[:4].mean(axis=0, keepdims=True), (S, 1))
    rings = np.sin((np.arange(S)[:, None] * 0.07) + 7 * noise(S, 1.4, 11)) * 0.5 + 0.5
    n = 0.5 * rings + 0.3 * noise(S, 1.0, 12) + 0.2 * g
    img = tint((212, 150, 78), n, 0.22)
    # plank seams every quarter
    for y in range(0, S, S // 4):
        img[max(0, y - 1):y + 2, :] *= 0.55
    save("Sc12_Wood", img)


def yellow():
    n = 0.6 * noise(S, 1.4, 13) + 0.4 * noise(S, 0.8, 14)
    save("Sc12_Yellow", tint((236, 190, 28), n, 0.07))


def steel():
    g = noise(S, 2.4, 15)
    g = np.tile(g[:3].mean(axis=0, keepdims=True), (S, 1))
    n = 0.6 * g + 0.4 * noise(S, 1.0, 16)
    save("Sc12_Steel", tint((166, 170, 176), n, 0.10))


def normal_from(name, strength):
    """tangent normal map from a height noise"""
    h = 0.6 * noise(S, 1.5, 21) + 0.4 * noise(S, 0.7, 22)
    gx = np.roll(h, -1, 1) - np.roll(h, 1, 1)
    gy = np.roll(h, -1, 0) - np.roll(h, 1, 0)
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    img = np.stack([(nx / ln * 0.5 + 0.5) * 255, (ny / ln * 0.5 + 0.5) * 255, (nz / ln * 0.5 + 0.5) * 255], axis=-1)
    save(name, img)


if __name__ == "__main__":
    wall()
    ceiling()
    floor()
    wood()
    yellow()
    steel()
    normal_from("Sc12_Stucco_Normal", 18.0)
