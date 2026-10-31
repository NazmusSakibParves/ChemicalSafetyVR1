"""Scenario 11 environment textures (procedural, numpy + Pillow) -> Assets/Art/Presentable/2D/Sc06Tex/Label_Sc11*.png.

Ground (gravel + dried mud, tileable), Meadow (grass + flowers, tileable), Fence (chain-link diamonds, alpha),
Brick (gas-detector hut wall, tileable), Sky (storm clouds with a dark hill line at the bottom, 2:1).
"""
import os

import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Presentable", "2D", "Sc06Tex")
rng = np.random.default_rng(1111)


def noise(n, octaves, persistence=0.55, seed=0):
    """Tileable fractal noise 0..1 via FFT-filtered white noise."""
    r = np.random.default_rng(seed)
    total = np.zeros((n, n))
    amp = 1.0
    for o in range(octaves):
        cells = 4 * 2 ** o
        if cells > n:
            break
        base = r.random((cells, cells))
        img = Image.fromarray((base * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
        total += amp * np.asarray(img, dtype=np.float64) / 255.0
        amp *= persistence
    total -= total.min()
    return total / max(1e-6, total.max())


def save(name, arr, mode="RGB"):
    os.makedirs(OUT, exist_ok=True)
    if name in ("Label_Sc11Ground", "Label_Sc11Meadow", "Label_Sc11Brick"):  # mirror so the tile has no seams
        arr = np.concatenate([np.concatenate([arr, arr[:, ::-1]], axis=1), np.concatenate([arr[::-1], arr[::-1, ::-1]], axis=1)], axis=0)
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), mode).save(os.path.join(OUT, name + ".png"))


def ground():
    n = 1024
    big = noise(n, 7, 0.6, 1)
    fine = noise(n, 9, 0.7, 2)
    mud = np.array([92, 74, 56]) / 255.0
    dry = np.array([108, 90, 70]) / 255.0
    base = mud * (1 - big[..., None]) + dry * big[..., None]
    # gravel: bright/dark stones from thresholded fine noise
    stones = (fine > 0.62).astype(float) * (fine - 0.62) * 4
    grey = np.array([0.55, 0.54, 0.52])
    img = base * (0.75 + 0.5 * fine[..., None]) + stones[..., None] * grey * 0.8
    wet = (noise(n, 4, 0.5, 3) > 0.7)[..., None] * 0.18
    img *= 1 - wet
    save("Label_Sc11Ground", img * 255)


def meadow():
    n = 1024
    a = noise(n, 8, 0.65, 4)
    b = noise(n, 10, 0.7, 5)
    dark = np.array([52, 74, 28]) / 255.0
    light = np.array([128, 150, 62]) / 255.0
    img = dark * (1 - a[..., None]) + light * a[..., None]
    img *= 0.8 + 0.4 * b[..., None]
    for _ in range(260):  # tiny wild flowers
        x, y = rng.integers(0, n, 2)
        col = [(250, 240, 120), (240, 240, 240), (200, 150, 230)][rng.integers(0, 3)]
        img[y:y + 3, x:x + 3] = np.array(col) / 255.0
    save("Label_Sc11Meadow", img * 255)


def fence():
    n = 512
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    px = np.zeros((n, n, 4), dtype=np.float64)
    step = n / 8
    yy, xx = np.mgrid[0:n, 0:n]
    u = (xx + yy) % step
    v = (xx - yy) % step
    w = 3.2
    wire = (np.minimum(u, step - u) < w) | (np.minimum(v, step - v) < w)
    shade = 150 + 60 * np.sin((xx + yy) / step * np.pi)
    px[wire, 0] = shade[wire] * 0.78
    px[wire, 1] = shade[wire] * 0.80
    px[wire, 2] = shade[wire] * 0.82
    px[wire, 3] = 255
    save("Label_Sc11Fence", px, "RGBA")


def brick():
    n = 512
    img = np.zeros((n, n, 3))
    bh, bw = 32, 96
    base = noise(n, 6, 0.6, 7)
    for row in range(n // bh):
        off = (bw // 2) * (row % 2)
        for col in range(-1, n // bw + 1):
            x0 = col * bw + off
            tone = 0.55 + 0.35 * rng.random()
            c = np.array([150, 120, 100]) / 255.0 * tone
            img[row * bh + 2:(row + 1) * bh - 2, max(0, x0 + 2):max(0, min(n, x0 + bw - 2))] = c
    mortar = np.array([170, 168, 160]) / 255.0
    mask = img.sum(axis=2) == 0
    img[mask] = mortar
    img *= 0.8 + 0.4 * base[..., None]
    img = np.asarray(Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)))
    save("Label_Sc11Brick", img)


def sky():
    w, h = 2048, 1024
    cl = noise(1024, 9, 0.62, 8)
    cl = np.asarray(Image.fromarray((cl * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float64) / 255.0
    y = np.linspace(0, 1, h)[:, None]
    top = np.array([40, 46, 56]) / 255.0
    mid = np.array([132, 140, 150]) / 255.0
    sky = top * (1 - y[..., None]) + mid * y[..., None]
    sky = sky * (0.38 + 1.05 * cl[..., None] ** 1.3)
    # a brighter rift low on the horizon
    rift = np.exp(-((y - 0.58) / 0.12) ** 2) * np.exp(-(((np.arange(w) / w) - 0.72) / 0.18) ** 2)[None, :]
    sky += rift[..., None] * np.array([0.35, 0.33, 0.28])
    # hill silhouettes
    xs = np.arange(w) / w
    hill = 0.67 + 0.035 * np.sin(xs * 2 * np.pi * 3) + 0.02 * np.sin(xs * 2 * np.pi * 9 + 1) + 0.01 * np.sin(xs * 2 * np.pi * 23)
    row = (np.arange(h) / h)[:, None]
    mask = row > hill[None, :]
    haze = np.array([70, 82, 76]) / 255.0
    sky[mask] = haze * (0.85 + 0.25 * cl[mask][:, None])
    save("Label_Sc11Sky", np.clip(sky, 0, 1) * 255)


def tuft():
    n = 256
    px = np.zeros((n, n, 4), dtype=np.float64)
    for _ in range(46):
        x0 = rng.uniform(40, n - 40)
        h = rng.uniform(90, 235)
        lean = rng.uniform(-0.45, 0.45)
        col = np.array([rng.uniform(70, 120), rng.uniform(100, 150), rng.uniform(30, 60)])
        for t in np.linspace(0, 1, 160):
            x = x0 + lean * h * t * t * 1.2
            y = n - 1 - h * t
            w = max(0.6, 3.2 * (1 - t))
            xi, yi = int(x), int(y)
            for dx in range(-int(w), int(w) + 1):
                if 0 <= xi + dx < n and 0 <= yi < n:
                    px[yi, xi + dx, :3] = col * (0.7 + 0.5 * t)
                    px[yi, xi + dx, 3] = 255
    save("Label_Sc11Tuft", px, "RGBA")


def leaves():
    n = 512
    a = noise(n, 8, 0.65, 21)
    b = noise(n, 10, 0.7, 22)
    dark = np.array([22, 42, 18]) / 255.0
    light = np.array([70, 102, 36]) / 255.0
    img = dark * (1 - a[..., None]) + light * a[..., None]
    img *= 0.7 + 0.6 * b[..., None]
    save("Label_Sc11Leaves", img * 255)


if __name__ == "__main__":
    tuft()
    leaves()
    ground()
    meadow()
    # fence(): replaced by the hand-made texture (Label_Sc11Fence, finer galvanized mesh)
    brick()
    sky()
    print("wrote Sc11 textures")
