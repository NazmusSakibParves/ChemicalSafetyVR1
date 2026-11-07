"""Generates the Scenario 1 workshop-hall PBR textures (client reference: concrete walls, corrugated roof, dirty floor).

Output (re-run: python Tools/generate_sc01_env.py) in Assets/Art/Presentable/2D/Sc01Tex, all 1024 and tileable
unless noted (about 3 m per tile on walls/floor, 1.2 m on the roof sheet):
  Wall_Albedo / Wall_Normal / Wall_MS     rough grey concrete: formwork joints, tie holes, damp streaks, rust runs
  Floor_Albedo / Floor_Normal / Floor_MS  dirty stained concrete, grime, worn tyre paths, colour splashes (blue, red,
                                          yellow, orange paint / chemical), a few cracks
  Roof_Albedo / Roof_Normal / Roof_MS     corrugated galvanised sheet with rust streaks and dirt
  Pallet_Albedo / Box_Albedo              wooden pallet planks, brown cardboard
  Sign_SafetyFirst.png                    1024x512 green board: SAFETY FIRST / নিরাপত্তা প্রথম (not tiling)
*_MS = metalness in R, smoothness in A (URP Lit metallic map).
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import render_bangla_text as rb  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc01Tex")
N = 1024


def tile_noise(size, cells_x, cells_y, seed):
    r = np.random.default_rng(seed)
    small = r.random((cells_y, cells_x)).astype(np.float32)
    pad = np.pad(small, 2, mode="wrap")
    w = int(round(size * (cells_x + 4) / cells_x))
    h = int(round(size * (cells_y + 4) / cells_y))
    im = Image.fromarray((pad * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    ox = int(round(2 * size / cells_x))
    oy = int(round(2 * size / cells_y))
    return np.asarray(im, dtype=np.float32)[oy:oy + size, ox:ox + size] / 255.0


def fbm(seed, base=4, octaves=5, sx=1.0, sy=1.0, size=N):
    out = np.zeros((size, size), np.float32)
    amp, tot, cells = 0.5, 0.0, base
    for o in range(octaves):
        out += tile_noise(size, max(2, int(cells * sx)), max(2, int(cells * sy)), seed + o * 13) * amp
        tot += amp
        amp *= 0.52
        cells *= 2
    return out / tot


def smooth01(x, lo, hi):
    t = np.clip((x - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def blur(a, r):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0


def normal_from_height(h, strength):
    # wrap-around gradient so the normal map tiles without a seam
    gx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    gy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -gx * strength, gy * strength, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.dstack([nx / ln, ny / ln, nz / ln]) * 0.5 + 0.5
    return Image.fromarray((rgb * 255).astype(np.uint8), "RGB")


def ms_image(smooth, metal=0.0):
    a = (np.clip(smooth, 0, 1) * 255).astype(np.uint8)
    m = np.clip(metal, 0, 1) if isinstance(metal, np.ndarray) else np.full(a.shape, metal, np.float32)
    r = (m * 255).astype(np.uint8)
    z = np.zeros_like(a)
    return Image.fromarray(np.dstack([r, z, z, a]), "RGBA")


def rgb_img(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


# ------------------------------------------------------------------ walls

def wall():
    """Old plastered wall like the reference: pale grey, fine grain, repaired patches, rain streaks, dirty foot, hairline cracks."""
    rng = np.random.default_rng(3)
    big, mid = fbm(101, 3), fbm(111, 8)
    grit = rng.random((N, N)).astype(np.float32)
    grain = blur(grit, 0.6)
    fine = fbm(121, 60, 3)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    v = yy / N                                   # 0 = top, 1 = bottom (the tile is 3 m high)

    base = np.array([176, 174, 168], np.float32)
    tone = 0.84 + 0.34 * big + 0.1 * (mid - 0.5)
    img = base[None, None, :] * tone[..., None] * (0.9 + 0.22 * (grain[..., None] - 0.5) + 0.12 * (fine[..., None] - 0.5))

    # repaired plaster patches: straight-edged rectangles a little darker or lighter, with a thin shadow line
    patch = np.zeros((N, N), np.float32)
    pr = np.random.default_rng(12)
    for _ in range(3):
        x0, y0 = int(pr.uniform(0, N - 260)), int(pr.uniform(0, N - 260))
        w, h = int(pr.uniform(140, 320)), int(pr.uniform(120, 260))
        patch[y0:y0 + h, x0:x0 + w] = pr.choice([-1.0, 1.0]) * pr.uniform(0.04, 0.08)
    edge = np.abs(blur(np.abs(patch) > 0, 2.0) - (np.abs(patch) > 0))
    img = img * (1 + patch[..., None]) * (1 - 0.12 * np.clip(blur(edge, 1.5) * 5, 0, 1)[..., None])

    # rain streaks running down from the top, damp patches, soot band near the roof and a dirty foot
    streak = smooth01(fbm(131, 40, 3, sx=1.0, sy=0.05), 0.55, 0.78) * smooth01(1 - v, 0.0, 0.9)
    damp = smooth01(fbm(141, 3, 4), 0.55, 0.8) * (0.4 + 0.6 * v)
    foot_edge = 0.78 + 0.1 * (fbm(151, 12, 3, sx=1.0, sy=0.2) - 0.5)
    foot = smooth01(v, foot_edge, 1.0) ** 1.2
    top = smooth01(1 - v, 0.88, 1.0)
    img = img * (1 - 0.16 * streak[..., None] * np.array([0.7, 0.75, 0.9])[None, None, :])
    img = img * (1 - 0.2 * damp[..., None]) * (1 - 0.5 * foot[..., None]) * (1 - 0.25 * top[..., None])
    mould = smooth01(fbm(191, 10, 4, sx=1.0, sy=0.3), 0.66, 0.78) * smooth01(v, 0.55, 0.95) * 0.5
    img = img * (1 - mould[..., None] * 0.5) + mould[..., None] * np.array([70, 82, 58], np.float32)[None, None, :] * 0.5

    # fine pits and hairline cracks
    pits = smooth01(blur(np.random.default_rng(45).random((N, N)).astype(np.float32), 1.2), 0.64, 0.665)
    img = img * (1 - 0.35 * pits[..., None])
    cr = cracks(5, 4, 70)
    crb = blur(cr, 0.9)
    img = img * (1 - 0.32 * crb[..., None])
    save(rgb_img(img), "Wall_Albedo")

    height = 0.5 + 0.1 * (mid - 0.5) + 0.18 * (fine - 0.5) + 0.2 * (grain - 0.5) + 0.5 * patch - 0.35 * pits - 0.5 * crb
    save(normal_from_height(height.astype(np.float32), 4.5), "Wall_Normal")
    smooth = 0.16 + 0.12 * damp + 0.05 * (1 - foot)
    save(ms_image(np.clip(smooth, 0.06, 0.4)), "Wall_MS")


def cracks(seed, count, length):
    rng = np.random.default_rng(seed)
    canvas = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(canvas)
    for _ in range(count):
        x, y, ang = rng.uniform(0, N), rng.uniform(0, N), rng.uniform(0, 6.28)
        pts = [(x, y)]
        for _ in range(length):
            ang += rng.normal(0, 0.2)
            x += math.cos(ang) * rng.uniform(6, 14)
            y += math.sin(ang) * rng.uniform(6, 14)
            pts.append((x, y))
        for dx in (-N, 0, N):
            for dy in (-N, 0, N):
                d.line([(px + dx, py + dy) for px, py in pts], fill=255, width=int(rng.integers(1, 3)))
    return np.asarray(canvas, dtype=np.float32) / 255.0


# ------------------------------------------------------------------ floor

def floor():
    """Worn industrial concrete: aggregate speckle, oil stains with a dark rim, many small drips, tyre tracks, scuffs, a couple of faint spills."""
    rng = np.random.default_rng(5)
    big, mid = fbm(11, 3), fbm(21, 8)
    grit = rng.random((N, N)).astype(np.float32)
    grain = blur(grit, 0.7)
    fine = fbm(31, 90, 3)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)

    base = np.array([120, 119, 116], np.float32)
    tone = 0.78 + 0.34 * big + 0.12 * (mid - 0.5)
    img = base[None, None, :] * tone[..., None] * (0.86 + 0.34 * (grain[..., None] - 0.5) + 0.2 * (fine[..., None] - 0.5))
    # coarse aggregate: a second, larger speckle
    agg = blur(np.random.default_rng(6).random((N, N)).astype(np.float32), 1.6)
    img = img * (0.92 + 0.5 * (agg[..., None] - 0.5))

    # expansion joint (one cross per tile)
    joint = np.zeros((N, N), np.float32)
    joint[:, 0:4] = 1
    joint[0:4, :] = 1
    joint = blur(joint, 1.2)
    img = img * (1 - 0.65 * joint[..., None])

    # tyre tracks: two long parallel darker, smoother bands with broken edges
    tr = np.zeros((N, N), np.float32)
    for cx in (300, 380, 740, 820):
        tr += smooth01(1 - np.abs(xx - cx) / 22.0, 0.0, 0.7)
    tr = np.clip(tr, 0, 1) * smooth01(fbm(41, 2, 4, sx=0.4, sy=3.0), 0.35, 0.65)
    img = img * (1 - 0.28 * tr[..., None])

    # oil / ink stains: dark with a darker, crusty rim
    oil = smooth01(fbm(61, 4, 5), 0.66, 0.8)
    rim = np.clip(oil - blur(oil, 6), 0, 1) * 3
    img = img * (1 - 0.72 * oil[..., None]) * (1 - 0.3 * np.clip(rim, 0, 1)[..., None])
    img = img + oil[..., None] * np.array([6, 4, 0], np.float32)[None, None, :]

    # small drips and spots everywhere (black, hard-edged)
    spots = np.zeros((N, N), np.float32)
    sr = np.random.default_rng(88)
    for _ in range(260):
        cx, cy = sr.uniform(0, N, 2)
        rad = sr.choice([2, 3, 4, 6, 9], p=[0.35, 0.3, 0.2, 0.1, 0.05])
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        spots = np.maximum(spots, smooth01(1 - d / rad, 0.0, 0.35) * sr.uniform(0.5, 1.0))
    img = img * (1 - 0.8 * spots[..., None])

    # scuffs: pale scrape marks
    scuff = smooth01(fbm(71, 30, 3, sx=2.0, sy=0.5), 0.66, 0.82)
    img = img + 16 * scuff[..., None]

    # a couple of faint coloured spills (paint / chemical), soft and irregular
    splash_h = np.zeros((N, N), np.float32)
    for (cx, cy, rad, col, k) in ((820, 760, 150, (40, 110, 80), 0.5), (700, 880, 120, (150, 40, 80), 0.45)):
        nz = fbm(500 + cx, 6, 5)
        m = np.zeros((N, N), np.float32)
        for dx in (-N, 0, N):
            for dy in (-N, 0, N):
                d = np.sqrt(((xx - cx - dx) * 0.7) ** 2 + (yy - cy - dy) ** 2)
                m = np.maximum(m, smooth01((1.0 - d / rad) * 1.2 + (nz - 0.5) * 1.5, 0.0, 0.5))
        m = m * k
        c = np.array(col, np.float32)[None, None, :]
        img = img * (1 - m[..., None] * 0.6) + c * m[..., None] * 0.6 * (0.6 + 0.4 * tone[..., None])
        splash_h = np.maximum(splash_h, m)

    cr = cracks(9, 3, 60)
    crb = blur(cr, 0.9)
    img = img * (1 - 0.6 * crb[..., None])
    save(rgb_img(img), "Floor_Albedo")

    height = 0.5 + 0.14 * (mid - 0.5) + 0.16 * (fine - 0.5) + 0.22 * (grain - 0.5) + 0.15 * (agg - 0.5) - 0.4 * joint - 0.5 * crb - 0.08 * tr
    save(normal_from_height(height.astype(np.float32), 5.0), "Floor_Normal")
    smooth = 0.2 + 0.35 * oil + 0.2 * spots + 0.15 * tr - 0.12 * scuff + 0.1 * splash_h
    save(ms_image(np.clip(smooth, 0.06, 0.75)), "Floor_MS")


def box_label():
    """Small printed shipping label for the cardboard boxes (paper, red hazard stripe, text lines, barcode). RGBA, not tiling."""
    w, h = 256, 160
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, w - 1, h - 1), fill=(226, 224, 214, 255))
    d.rectangle((0, 0, w - 1, 26), fill=(176, 36, 30, 255))
    r = np.random.default_rng(3)
    for i in range(4):
        d.rectangle((12, 38 + i * 15, 12 + int(r.uniform(90, 200)), 44 + i * 15), fill=(36, 36, 38, 255))
    x = 12
    while x < w - 40:
        bw = int(r.choice([2, 3, 5]))
        d.rectangle((x, 108, x + bw, 148), fill=(20, 20, 22, 255))
        x += bw + int(r.choice([2, 3, 4]))
    d.rectangle((w - 52, 36, w - 14, 74), outline=(36, 36, 38, 255), width=3)
    arr = np.asarray(im, dtype=np.float32).copy()
    n = np.random.default_rng(4).random((h, w)).astype(np.float32)
    arr[..., :3] *= (0.85 + 0.2 * n[..., None])
    save(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA"), "Label_Box")


def grunge():
    """Tileable fine mottling (mid grey) multiplied over buckets, jerrycans, rack steel and sacks so they do not look flat."""
    g = 0.62 + 0.5 * fbm(801, 6, 5) - 0.25 + 0.12 * (np.random.default_rng(9).random((N, N)).astype(np.float32) - 0.5)
    streak = smooth01(fbm(802, 30, 3, sx=1.0, sy=0.08), 0.6, 0.8)
    g = g * (1 - 0.25 * streak)
    v = np.clip(g * 255, 0, 255).astype(np.uint8)
    save(Image.fromarray(np.dstack([v, v, v]), "RGB"), "Grunge_Albedo")


# ------------------------------------------------------------------ corrugated roof

def roof():
    rng = np.random.default_rng(13)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    ridges = 8                                              # 8 corrugations per tile (about 15 cm each)
    ph = xx / N * ridges * 2 * math.pi
    wave = 0.5 + 0.5 * np.sin(ph)
    mid, fine = fbm(201, 6), fbm(211, 40, 3)
    grit = rng.random((N, N)).astype(np.float32)

    base = np.array([158, 164, 170], np.float32)
    shade = 0.55 + 0.55 * wave                                # crests catch the light, valleys are dark
    shade = shade * (0.85 + 0.3 * mid)
    img = base[None, None, :] * shade[..., None] * (0.94 + 0.08 * grit[..., None])
    streak = fbm(221, 24, 4, sx=1.0, sy=0.1)
    rust = smooth01(streak, 0.55, 0.74) * smooth01(fbm(231, 5), 0.4, 0.7)
    dirt = smooth01(fbm(241, 7), 0.5, 0.78) * (1 - wave * 0.6)
    img = img * (1 - 0.4 * dirt[..., None])
    img = img * (1 - 0.75 * rust[..., None]) + rust[..., None] * np.array([130, 70, 34], np.float32)[None, None, :] * 0.75
    # panel overlap seam each tile
    seam = np.zeros((N, N), np.float32)
    seam[0:3, :] = 1
    img = img * (1 - 0.5 * blur(seam, 1.2)[..., None])
    save(rgb_img(img), "Roof_Albedo")

    height = 0.5 * wave + 0.03 * (fine - 0.5) - 0.1 * blur(seam, 1.2)
    save(normal_from_height(height.astype(np.float32), 7.0), "Roof_Normal")
    metal = np.clip(0.75 - 0.6 * rust, 0, 1)
    smooth = np.clip(0.52 - 0.3 * dirt - 0.3 * rust + 0.1 * wave, 0.1, 0.75)
    save(ms_image(smooth, metal), "Roof_MS")


# ------------------------------------------------------------------ wood + cardboard

def pallet():
    """Old, dirty pallet wood: weathered grey-brown, dark grime and oil, scuffs, splits, dirt in the gaps."""
    rng = np.random.default_rng(21)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    plank = 4
    ph = N // plank
    img = np.zeros((N, N, 3), np.float32)
    grain = fbm(301, 4, 4, sx=0.3, sy=8.0)
    grain2 = fbm(302, 14, 3, sx=0.2, sy=14.0)
    for i in range(plank):
        tone = rng.uniform(0.7, 1.1)
        tint = np.array([128, 100, 68], np.float32) * tone       # dull, sun-bleached brown
        img[i * ph:(i + 1) * ph] = tint[None, None, :]
    img = img * (0.5 + 0.75 * grain[..., None]) * (0.8 + 0.3 * grain2[..., None])
    grey = smooth01(fbm(311, 5), 0.45, 0.75)                       # weathered grey patches
    img = img * (1 - 0.5 * grey[..., None]) + 0.5 * grey[..., None] * np.array([112, 108, 100], np.float32)[None, None, :] * (0.6 + 0.6 * grain[..., None])
    gap = np.zeros((N, N), np.float32)
    for i in range(plank):
        gap[i * ph:i * ph + 6, :] = 1
    img = img * (1 - 0.8 * blur(gap, 1.8)[..., None])
    dirt = smooth01(fbm(321, 3), 0.4, 0.75)
    oil = smooth01(fbm(331, 6), 0.62, 0.78)
    img = img * (1 - 0.5 * dirt[..., None]) * (1 - 0.65 * oil[..., None])
    scuff = smooth01(fbm(341, 24, 3, sx=0.4, sy=2.0), 0.6, 0.8)    # pale scrape marks across the grain
    img = img + 22 * scuff[..., None]
    splits = np.zeros((N, N), np.float32)
    rr = np.random.default_rng(5)
    for _ in range(10):
        x0 = rr.uniform(0, N)
        y0 = rr.uniform(0, N)
        ln = rr.uniform(60, 220)
        splits[int(y0) % N:int(y0 + rr.uniform(2, 5)) % N + 1, int(x0) % N:int(min(N, x0 + ln))] = 1
    img = img * (1 - 0.7 * blur(splits, 1.0)[..., None])
    save(rgb_img(img), "Pallet_Albedo")


def box():
    rng = np.random.default_rng(31)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    fibre = fbm(401, 6, 4, sx=6.0, sy=0.4)
    img = np.array([176, 138, 92], np.float32)[None, None, :] * (0.72 + 0.5 * fibre[..., None])
    flute = 0.93 + 0.07 * np.sin(yy / N * 2 * math.pi * 40)
    img = img * flute[..., None] * (0.94 + 0.1 * rng.random((N, N)).astype(np.float32)[..., None])
    tape = np.zeros((N, N), np.float32)
    tape[:, N // 2 - 40:N // 2 + 40] = 1
    tape = blur(tape, 1.5)
    img = img * (1 - tape[..., None]) + tape[..., None] * np.array([196, 160, 110], np.float32)[None, None, :]
    save(rgb_img(img), "Box_Albedo")


# ------------------------------------------------------------------ SAFETY FIRST sign (not tiling)

def sign():
    w, h = 1024, 512
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    green = (14, 135, 70, 255)
    d.rounded_rectangle((4, 4, w - 5, h - 5), 28, fill=(245, 245, 240, 255))
    d.rounded_rectangle((20, 20, w - 21, h - 21), 20, fill=green)
    d.rounded_rectangle((34, 34, w - 35, h - 35), 14, outline=(245, 245, 240, 255), width=5)
    # white first-aid cross on the left
    cx, cy, a, t = 160, h // 2, 92, 36
    d.rectangle((cx - t, cy - a, cx + t, cy + a), fill=(245, 245, 240, 255))
    d.rectangle((cx - a, cy - t, cx + a, cy + t), fill=(245, 245, 240, 255))
    fe = ImageFont.truetype(rb.LATIN_BOLD, 96, layout_engine=ImageFont.Layout.RAQM)
    fb = ImageFont.truetype(rb.FONT_BOLD, 104, layout_engine=ImageFont.Layout.RAQM)
    tx = 625
    d.text((tx, 175), "SAFETY FIRST", font=fe, fill=(245, 245, 240, 255), anchor="mm")
    d.text((tx, 345), "নিরাপত্তা প্রথম", font=fb, fill=(255, 224, 80, 255), anchor="mm")
    # old screwed-on board: grime, rust creeping in from the edges, chipped paint, scratches
    arr = np.asarray(im, dtype=np.float32).copy()
    a = arr[..., 3] / 255.0
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    nz = fbm(701, 6, 5, size=1024)[:h, :w]
    nz2 = fbm(702, 30, 3, size=1024)[:h, :w]
    edge = np.minimum.reduce([xx, w - xx, yy, h - yy]) / 70.0
    edge = 1 - np.clip(edge, 0, 1)
    rust = np.clip(edge * (0.5 + nz) * 1.3 + smooth01(nz2, 0.7, 0.85) * 0.5, 0, 1)
    drip = smooth01(fbm(703, 14, 3, size=1024)[:h, :w], 0.62, 0.78) * (yy / h)
    arr[..., :3] *= (0.62 + 0.45 * nz[..., None]) * (1 - 0.35 * drip[..., None])
    arr[..., :3] = arr[..., :3] * (1 - rust[..., None]) + rust[..., None] * np.array([112, 64, 34], np.float32) * (0.6 + 0.5 * nz2[..., None])
    for _ in range(40):
        x0, y0 = np.random.randint(0, w - 60), np.random.randint(0, h - 10)
        arr[y0:y0 + 2, x0:x0 + np.random.randint(15, 60), :3] += 55
    im = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    save(im, "Sign_SafetyFirst")


if __name__ == "__main__":
    wall()
    floor()
    roof()
    pallet()
    box()
    grunge()
    box_label()
    sign()
