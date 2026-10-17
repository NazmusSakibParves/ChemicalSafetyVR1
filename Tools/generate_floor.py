"""Bakes the warehouse floor (polished concrete + joints + scuffs + light reflections + worn yellow paint).

Output: Assets/Art/Presentable/2D/Env/FLOOR_Baked.png  (2048 x 4096, covers x -7..7, z -14..16)
Used by PresentableSceneBuilder on a quad lying on the floor. Re-run: python Tools/generate_floor.py
Needs numpy + Pillow.
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env", "FLOOR_Baked.png")

W, H = 2048, 4096
X0, X1, Z0, Z1 = -7.0, 7.0, -14.0, 16.0
rng = np.random.default_rng(5)
rnd = random.Random(5)


def px(x):
    return (x - X0) / (X1 - X0) * W


def py(z):
    return (Z1 - z) / (Z1 - Z0) * H


def fnoise(scale, seed):
    """Smooth tileable-ish noise in [0,1] via frequency filtering."""
    r = np.random.default_rng(seed)
    n = r.standard_normal((H, W)).astype(np.float32)
    f = np.fft.rfft2(n)
    fy = np.fft.fftfreq(H)[:, None]
    fx = np.fft.rfftfreq(W)[None, :]
    k = np.sqrt(fx * fx + fy * fy) * scale + 1e-4
    f *= np.exp(-(k * k))
    out = np.fft.irfft2(f, s=(H, W))
    out -= out.min()
    out /= out.max()
    return out


def layer():
    return Image.new("L", (W, H), 0)


def comp(base, mask_img, color, strength=1.0):
    m = np.asarray(mask_img, dtype=np.float32) / 255.0 * strength
    for c in range(3):
        base[..., c] = base[..., c] * (1 - m) + color[c] * m
    return base


def main():
    # --- polished concrete: mottled grey, brighter reflection pools, dark joints
    coarse = fnoise(60, 1)
    fine = fnoise(6, 2)
    base = np.empty((H, W, 3), np.float32)
    grey = 0.50 + 0.10 * coarse + 0.035 * fine
    base[..., 0] = grey
    base[..., 1] = grey * 1.005
    base[..., 2] = grey * 1.02

    # reflection pools under sky-lights (x +-3, every 4 m) and tube-light streaks (x +-3.2, on the beams)
    refl = layer()
    d = ImageDraw.Draw(refl)
    for z in (-12, -8, -4, 0, 4, 8, 12):
        for x in (-3.0, 3.0):
            d.ellipse((px(x - 1.15), py(z + 1.5), px(x + 1.15), py(z - 1.5)), fill=120)
    refl = refl.filter(ImageFilter.GaussianBlur(70))
    streak = layer()
    d = ImageDraw.Draw(streak)
    for z in (-10, -6, -2, 2, 6, 10, 14):
        for x in (-3.2, 3.2):
            d.rounded_rectangle((px(x - 0.12), py(z + 0.7), px(x + 0.12), py(z - 0.7)), radius=20, fill=170)
    streak = streak.filter(ImageFilter.GaussianBlur(14))
    base = comp(base, refl, (0.86, 0.88, 0.9), 0.85)
    base = comp(base, streak, (0.97, 0.98, 0.97), 0.8)

    # expansion joints
    joints = layer()
    d = ImageDraw.Draw(joints)
    for x in range(-7, 8, 4):
        d.line((px(x), 0, px(x), H), fill=170, width=4)
    for z in range(-14, 17, 4):
        d.line((0, py(z), W, py(z)), fill=170, width=4)
    joints = joints.filter(ImageFilter.GaussianBlur(1.2))
    base = comp(base, joints, (0.26, 0.27, 0.28), 0.55)

    # scuffs, tyre marks, oil
    scuff = layer()
    d = ImageDraw.Draw(scuff)
    for _ in range(900):
        x, y = rnd.uniform(0, W), rnd.uniform(0, H)
        a = rnd.uniform(0, math.tau)
        l = rnd.uniform(15, 90)
        d.line((x, y, x + math.cos(a) * l, y + math.sin(a) * l), fill=rnd.randint(40, 120), width=rnd.randint(1, 3))
    scuff = scuff.filter(ImageFilter.GaussianBlur(1.0))
    base = comp(base, scuff, (0.28, 0.28, 0.29), 0.5)

    tyre = layer()
    d = ImageDraw.Draw(tyre)
    for _ in range(14):
        x = rnd.uniform(px(-2.2), px(2.2))
        y = rnd.uniform(0, H)
        pts = []
        for i in range(40):
            pts.append((x + i * rnd.uniform(-6, 6) * 0.1 + math.sin(i * 0.2 + y) * 25, y + i * 30))
        d.line(pts, fill=90, width=rnd.randint(14, 26))
    tyre = tyre.filter(ImageFilter.GaussianBlur(5))
    base = comp(base, tyre, (0.2, 0.2, 0.21), 0.38)

    oil = layer()
    d = ImageDraw.Draw(oil)
    for _ in range(6):
        cx, cy = rnd.uniform(0, W), rnd.uniform(0, H)
        for _ in range(8):
            r = rnd.uniform(15, 60)
            ox, oy = rnd.uniform(-40, 40), rnd.uniform(-40, 40)
            d.ellipse((cx + ox - r, cy + oy - r, cx + ox + r, cy + oy + r), fill=170)
    oil = oil.filter(ImageFilter.GaussianBlur(10))
    base = comp(base, oil, (0.22, 0.22, 0.22), 0.22)

    # --- yellow safety paint (worn)
    paint = layer()
    d = ImageDraw.Draw(paint)
    w = 0.12

    def line(x0, z0, x1, z1, width=w):
        a = math.atan2(-(py(z1) - py(z0)), px(x1) - px(x0))
        hw = width / 2 * W / (X1 - X0)
        dx, dy = -math.sin(a) * hw, -math.cos(a) * hw
        x0p, y0p, x1p, y1p = px(x0), py(z0), px(x1), py(z1)
        d.polygon([(x0p + dx, y0p + dy), (x1p + dx, y1p + dy), (x1p - dx, y1p - dy), (x0p - dx, y0p - dy)], fill=255)

    # centre lanes + the yellow box in front of the hero rack
    line(2.0, -14.0, 2.0, 7.1)
    line(-2.0, -14.0, -2.0, 7.1)
    line(-2.06, 5.3, 2.06, 5.3)
    line(-2.06, 7.1, 2.06, 7.1)
    line(-2.06, -10.6, 2.06, -10.6)
    # lanes in front of the side racks + end caps + corner brackets
    for s in (-1, 1):
        line(s * 3.3, 1.2, s * 3.3, 14.8)
        line(s * 3.3, 1.2, s * 5.2, 1.2)
        line(s * 3.3, 14.8, s * 5.2, 14.8)
        line(s * 2.0, 5.3, s * 3.3, 5.3, 0.1)
    # diagonal hatch block, front right
    hx0, hx1, hz0, hz1 = 2.6, 4.3, -2.0, -0.6
    line(hx0, hz0, hx1, hz0)
    line(hx0, hz1, hx1, hz1)
    line(hx0, hz0, hx0, hz1)
    line(hx1, hz0, hx1, hz1)
    for i in range(-2, 10):
        x = hx0 + i * 0.26
        z0 = hz0
        a, b = (x, z0), (x + (hz1 - hz0), hz1)
        # clip diagonal bars to the box
        t0 = max(0.0, (hx0 - a[0]) / (b[0] - a[0]))
        t1 = min(1.0, (hx1 - a[0]) / (b[0] - a[0]))
        if t1 > t0:
            line(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0, a[0] + (b[0] - a[0]) * t1,
                 a[1] + (b[1] - a[1]) * t1, 0.11)
    # a second faded hatch block near the SC02 area
    for i in range(7):
        line(-1.6 + i * 0.5, -9.2, -1.2 + i * 0.5, -10.0, 0.14)

    paint_np = np.asarray(paint, dtype=np.float32) / 255.0
    wear = fnoise(14, 7)
    chips = (wear > 0.64).astype(np.float32)
    chips = np.asarray(Image.fromarray((chips * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)),
                       dtype=np.float32) / 255.0
    paint_np *= (1.0 - 0.55 * chips)
    paint_np *= 0.80 + 0.20 * fnoise(25, 9)
    paint_np = np.asarray(Image.fromarray((paint_np * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.9)),
                          dtype=np.float32) / 255.0
    yellow = np.array([0.92, 0.72, 0.08], np.float32)
    dirt = 0.78 + 0.22 * fnoise(18, 11)
    for c in range(3):
        base[..., c] = base[..., c] * (1 - paint_np) + yellow[c] * dirt * paint_np

    img = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8), "RGB")
    img.save(OUT)
    print("saved", OUT, img.size)


if __name__ == "__main__":
    main()
