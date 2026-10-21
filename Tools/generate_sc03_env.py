"""Generates the SC03 old-storage-room PBR textures: cracked dirty floor, damp peeling wall, cobwebs, grime, light shaft.

Output (re-run: python Tools/generate_sc03_env.py) in Assets/Art/Presentable/2D/Sc03Tex:
  Floor_Albedo / Floor_Normal / Floor_MS    1024, tiles (about 2.4 m per tile): branching cracks, oil and water stains,
                                            dirt, white salt crust, wet patches (smooth)
  Wall_Albedo / Wall_Normal / Wall_MS       1024, tiles (about 2.4 m): plaster blocks, damp streaks, peeled patches that
                                            show brick, grime at the foot
  Web_Corner.png / Web_Full.png             cobweb decals (alpha)
  Grime_Drip.png / Stain_Blot.png           dark drip streak and blot decals (alpha)
  Shaft.png                                 soft light shaft under the tube lights (alpha)
*_MS = metalness in R, smoothness in A (URP Lit metallic map).
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc03Tex")
N = 1024


def tile_noise(size, cells_x, cells_y, seed):
    """Tileable value noise 0..1 (wrap padded, bicubic)."""
    r = np.random.default_rng(seed)
    small = r.random((cells_y, cells_x)).astype(np.float32)
    pad = np.pad(small, 2, mode="wrap")
    w = int(round(size * (cells_x + 4) / cells_x))
    h = int(round(size * (cells_y + 4) / cells_y))
    im = Image.fromarray((pad * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    ox = int(round(2 * size / cells_x))
    oy = int(round(2 * size / cells_y))
    return np.asarray(im, dtype=np.float32)[oy:oy + size, ox:ox + size] / 255.0


def fbm(size, seed, base=4, octaves=5, sx=1.0, sy=1.0):
    out = np.zeros((size, size), np.float32)
    amp, tot = 0.5, 0.0
    cells = base
    for o in range(octaves):
        out += tile_noise(size, max(2, int(cells * sx)), max(2, int(cells * sy)), seed + o * 13) * amp
        tot += amp
        amp *= 0.52
        cells *= 2
    return out / tot


def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    gx = np.roll(gx, 0, axis=1)
    nx, ny, nz = -gx * strength, gy * strength, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.dstack([nx / ln, ny / ln, nz / ln]) * 0.5 + 0.5
    return Image.fromarray((rgb * 255).astype(np.uint8), "RGB")


def ms_image(smooth, metal=0.0):
    a = (np.clip(smooth, 0, 1) * 255).astype(np.uint8)
    r = np.full(a.shape, int(metal * 255), np.uint8)
    z = np.zeros_like(a)
    return Image.fromarray(np.dstack([r, z, z, a]), "RGBA")


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def draw_wrapped(draw, pts, width, fill):
    """Polyline that also appears on the wrapped sides so the tile stays seamless."""
    for dx in (-N, 0, N):
        for dy in (-N, 0, N):
            draw.line([(x + dx, y + dy) for x, y in pts], fill=fill, width=width)


def cracks(seed, count, length):
    rng = np.random.default_rng(seed)
    canvas = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(canvas)

    def walk(x, y, ang, steps, width, depth):
        pts = [(x, y)]
        for _ in range(steps):
            ang += rng.normal(0, 0.17)
            x += math.cos(ang) * rng.uniform(8, 16)
            y += math.sin(ang) * rng.uniform(8, 16)
            pts.append((x, y))
            if depth < 3 and rng.random() < 0.035:
                walk(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.1), steps // 2, max(1, width - 1), depth + 1)
        draw_wrapped(d, pts, width, 255)

    for _ in range(count):
        walk(rng.uniform(0, N), rng.uniform(0, N), rng.uniform(0, 6.28), length, int(rng.integers(2, 5)), 0)
    return np.asarray(canvas, dtype=np.float32) / 255.0


def smooth01(x, lo, hi):
    t = np.clip((x - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def floor():
    rng = np.random.default_rng(5)
    big = fbm(N, 11, 3)
    mid = fbm(N, 21, 8)
    fine = fbm(N, 31, 48, 3)
    grit = rng.random((N, N)).astype(np.float32)
    crack = cracks(41, 6, 75)
    crack_soft = np.asarray(Image.fromarray((crack * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(5)),
                            dtype=np.float32) / 255.0
    crack_thin = np.asarray(Image.fromarray((crack * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.1)),
                            dtype=np.float32) / 255.0

    base = np.array([112, 108, 100], np.float32)
    shade = 0.62 + 0.5 * big + 0.18 * (mid - 0.5)
    img = base[None, None, :] * shade[..., None] * (0.9 + 0.2 * grit[..., None] * 0.6 + 0.08 * fine[..., None])

    water = smooth01(fbm(N, 51, 3), 0.55, 0.72)                  # brown water stains
    oil = smooth01(fbm(N, 61, 5), 0.66, 0.8)                     # dark oily blotches
    dirt = np.clip(crack_soft * 1.8 + (1 - big) * 0.15, 0, 1)    # dirt gathers in the cracks
    salt = smooth01(fbm(N, 71, 6, 4), 0.7, 0.82) * (0.5 + 0.5 * grit)  # white crust
    salt = np.clip(salt + crack_soft * 0.25 * (grit > 0.45), 0, 1)
    wet = smooth01(fbm(N, 81, 4), 0.62, 0.74)

    img = img * (1 - 0.35 * dirt[..., None])
    img = img * (1 - water[..., None] * np.array([0.14, 0.22, 0.34])[None, None, :])
    img = img * (1 - 0.45 * oil[..., None])
    img = img * (1 - 0.55 * crack_thin[..., None])
    img = img * (1 - 0.2 * wet[..., None])
    img = img + salt[..., None] * np.array([120, 120, 112], np.float32)[None, None, :] * 0.8
    albedo = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB")

    height = 0.5 + 0.18 * (mid - 0.5) + 0.12 * (fine - 0.5) + 0.05 * (grit - 0.5) - 0.5 * crack_thin - 0.12 * crack_soft
    height += 0.05 * salt
    normal = normal_from_height(height.astype(np.float32), 5.0)
    smooth = 0.12 + 0.2 * (1 - dirt) + 0.55 * wet + 0.25 * oil - 0.2 * salt
    smooth = np.clip(smooth, 0.05, 0.92)
    save(albedo, "Floor_Albedo")
    save(normal, "Floor_Normal")
    save(ms_image(smooth), "Floor_MS")


def wall():
    rng = np.random.default_rng(9)
    big = fbm(N, 101, 3)
    mid = fbm(N, 111, 9)
    fine = fbm(N, 121, 40, 3)
    grit = rng.random((N, N)).astype(np.float32)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)

    # block joints: 3 courses of long blocks, staggered
    joints = np.zeros((N, N), np.float32)
    course = N // 3
    for c in range(3):
        y0 = c * course
        joints[max(0, y0 - 2):y0 + 3, :] = 1.0
        off = 0 if c % 2 == 0 else N // 4
        for x in range(off, N + 1, N // 2):
            joints[y0:y0 + course, max(0, x - 2):x + 3] = 1.0
        # wrap seam
    joints = np.asarray(Image.fromarray((joints * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6)),
                        dtype=np.float32) / 255.0

    base = np.array([146, 138, 124], np.float32)
    shade = 0.6 + 0.55 * big + 0.2 * (mid - 0.5)
    img = base[None, None, :] * shade[..., None] * (0.92 + 0.14 * grit[..., None])

    streak = fbm(N, 131, 14, 4, sx=1.0, sy=0.12)               # tall thin vertical drips
    damp = smooth01(streak, 0.5, 0.72) * smooth01(1 - yy / N, 0.1, 0.9)
    peel_mask = smooth01(fbm(N, 141, 4), 0.69, 0.75)
    peel_edge = np.clip(peel_mask - np.asarray(Image.fromarray((peel_mask * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(5)), dtype=np.float32) / 255.0, 0, 1)
    brick = np.ones((N, N), np.float32)
    bh = N // 12
    for r in range(12):
        brick[r * bh:r * bh + 3, :] = 0.0
        off = (r % 2) * (N // 12)
        for x in range(off, N + 1, N // 6):
            brick[r * bh:(r + 1) * bh, max(0, x - 1):x + 2] = 0.0
    brick_col = np.array([112, 84, 70], np.float32)[None, None, :] * (0.55 + 0.6 * fbm(N, 151, 14))[..., None]
    brick_col = brick_col * (0.4 + 0.6 * brick[..., None])
    mold = smooth01(fbm(N, 161, 6), 0.66, 0.8) * smooth01(1 - yy / N, 0.0, 0.7)
    grime = smooth01(yy / N, 0.62, 1.0)                         # darker towards the foot

    img = img * (1 - 0.32 * damp[..., None] * np.array([0.9, 0.7, 0.9])[None, None, :])
    img = img * (1 - 0.5 * joints[..., None])
    img = img * (1 - 0.25 * grime[..., None])
    img = img + mold[..., None] * np.array([-14, 5, -14], np.float32)[None, None, :]
    img = img * (1 - peel_mask[..., None]) + brick_col * peel_mask[..., None]
    img = img * (1 - 0.5 * peel_edge[..., None])
    albedo = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB")

    height = 0.5 + 0.12 * (mid - 0.5) + 0.1 * (fine - 0.5) - 0.3 * joints - 0.35 * peel_mask * (0.4 + 0.6 * brick)
    normal = normal_from_height(height.astype(np.float32), 4.0)
    smooth = np.clip(0.18 + 0.4 * damp - 0.08 * peel_mask, 0.05, 0.8)
    save(albedo, "Wall_Albedo")
    save(normal, "Wall_Normal")
    save(ms_image(smooth), "Wall_MS")


def web(name, corner, seed):
    rng = np.random.default_rng(seed)
    S = 512
    SS = 2
    im = Image.new("RGBA", (S * SS, S * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if corner:
        cx, cy = 4.0, 4.0
        a0, a1 = 0.0, math.pi / 2
        count = 9
        reach = S * SS * 0.95
    else:
        cx, cy = S * SS / 2, S * SS / 2
        a0, a1 = 0.0, 2 * math.pi
        count = 15
        reach = S * SS * 0.46
    angs = sorted(rng.uniform(a0 + 0.03, a1 - 0.03, count) if corner else np.linspace(a0, a1, count, endpoint=False)
                  + rng.normal(0, 0.09, count))
    lens = [reach * rng.uniform(0.75, 1.0) for _ in angs]
    col = (232, 232, 226)
    for a, ln in zip(angs, lens):
        pts = []
        for t in np.linspace(0, 1, 12):
            sag = math.sin(t * math.pi) * rng.uniform(-6, 6)
            pts.append((cx + math.cos(a) * ln * t + sag * math.sin(a), cy + math.sin(a) * ln * t - sag * math.cos(a)))
        d.line(pts, fill=col + (205,), width=2 * SS // 2 + 1)
    radii = np.arange(0.12, 1.0, 0.1)
    for ri, rad in enumerate(radii):
        for i in range(len(angs) - (1 if corner else 0)):
            j = (i + 1) % len(angs)
            if rng.random() < 0.16:
                continue
            ra, rb = lens[i] * rad, lens[j] * rad
            p1 = (cx + math.cos(angs[i]) * ra, cy + math.sin(angs[i]) * ra)
            p2 = (cx + math.cos(angs[j]) * rb, cy + math.sin(angs[j]) * rb)
            mid = ((p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2)
            sag = (cx - mid[0], cy - mid[1])
            m = (mid[0] + sag[0] * 0.07, mid[1] + sag[1] * 0.07)
            curve = []
            for t in np.linspace(0, 1, 8):
                x = (1 - t) ** 2 * p1[0] + 2 * (1 - t) * t * m[0] + t * t * p2[0]
                y = (1 - t) ** 2 * p1[1] + 2 * (1 - t) * t * m[1] + t * t * p2[1]
                curve.append((x, y))
            d.line(curve, fill=col + (int(rng.integers(120, 190)),), width=SS)
    # dust clumps on the threads
    for _ in range(40):
        a = rng.choice(angs)
        t = rng.uniform(0.1, 0.95)
        x = cx + math.cos(a) * reach * t
        y = cy + math.sin(a) * reach * t
        r = rng.uniform(1.5, 4.5) * SS
        d.ellipse([x - r, y - r, x + r, y + r], fill=(205, 200, 190, 150))
    im = im.resize((S, S), Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(0.4))
    save(im, name)


def grime_drip():
    w, h = 256, 1024
    a = np.zeros((h, w), np.float32)
    rng = np.random.default_rng(3)
    for _ in range(9):
        x = rng.uniform(0.15, 0.85) * w
        width = rng.uniform(3, 14)
        length = rng.uniform(0.35, 0.95) * h
        yy = np.arange(h)[:, None]
        xx = np.arange(w)[None, :]
        wobble = np.sin(yy / rng.uniform(40, 90) + rng.uniform(0, 6)) * 5
        prof = np.exp(-((xx - x - wobble) / width) ** 2)
        fade = np.clip(1 - yy / length, 0, 1) ** 0.6 * np.clip(yy / 30, 0, 1)
        a = np.maximum(a, prof * fade * rng.uniform(0.35, 0.9))
    a *= 0.8 + 0.2 * np.random.default_rng(8).random((h, w))
    rgb = np.dstack([np.full((h, w), 40.0), np.full((h, w), 32.0), np.full((h, w), 24.0)])
    save(Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA"), "Grime_Drip")


def stain_blot():
    s = 512
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    r = np.sqrt(((xx - s / 2) / (s / 2)) ** 2 + ((yy - s / 2) / (s / 2)) ** 2)
    nz = fbm(s, 201, 4, 5)
    mask = np.clip((0.95 - r) * 2.4 + (nz - 0.5) * 2.2, 0, 1)
    mask = smooth01(mask, 0.15, 0.85)
    a = mask * (0.55 + 0.4 * fbm(s, 211, 10, 3))
    rgb = np.dstack([np.full((s, s), 30.0), np.full((s, s), 24.0), np.full((s, s), 18.0)])
    save(Image.fromarray(np.dstack([rgb, np.clip(a, 0, 1) * 255]).astype(np.uint8), "RGBA"), "Stain_Blot")


def shaft():
    w, h = 128, 512
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    v = yy / h                      # 0 top (at the tube) .. 1 bottom
    u = (xx / w - 0.5) * 2
    half = 0.18 + 0.8 * v
    edge = np.clip(1 - np.abs(u) / half, 0, 1) ** 1.6
    a = edge * (1 - v) ** 1.3 * np.clip(v * 12, 0, 1) * 0.9
    rgb = np.dstack([np.full((h, w), 255.0), np.full((h, w), 244.0), np.full((h, w), 215.0)])
    save(Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA"), "Shaft")


if __name__ == "__main__":
    floor()
    wall()
    web("Web_Corner", True, 4)
    web("Web_Full", False, 6)
    grime_drip()
    stain_blot()
    shaft()
