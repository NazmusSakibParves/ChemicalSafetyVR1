"""Scenario 11 yard (chlorine cylinder leak) textures -> Assets/Art/Presentable/2D/Sc11Yard/*.png

Yard_Concrete (dirty wall, tileable), Yard_Floor (pale slab floor with oil stains, tileable), Yard_Tin (corrugated shed roof, tileable),
Sign_ToxicGas (bilingual yellow warning sign), Label_Chlorine (white cylinder stencil with the two hazard diamonds, alpha),
Windsock (orange / white stripes). Procedural, numpy + Pillow (raqm for the Bangla text).
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc11Yard")
BN_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")
LAT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"


def noise(n, octaves, persistence=0.55, seed=0):
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


def mirror_tile(a):
    return np.concatenate([np.concatenate([a, a[:, ::-1]], axis=1), np.concatenate([a[::-1], a[::-1, ::-1]], axis=1)], axis=0)


def save(name, arr, mode="RGB"):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), mode).save(os.path.join(OUT, name + ".png"))


def concrete():
    n = 512
    base = 0.55 * noise(n, 6, 0.6, 1) + 0.25 * noise(n, 8, 0.7, 2) + 0.2 * noise(n, 3, 0.5, 3)
    stain = noise(n, 4, 0.6, 4)
    streak = np.asarray(Image.fromarray((noise(n, 3, 0.5, 5) * 255).astype(np.uint8)).resize((n, n // 4)).resize((n, n), Image.BICUBIC), dtype=np.float64) / 255.0
    col = np.zeros((n, n, 3))
    g = 150 + base * 60 - stain * 38 - streak * 22
    col[..., 0] = g * 1.02
    col[..., 1] = g * 1.0
    col[..., 2] = g * 0.95
    # damp dark patches near the bottom of the tile, a few cracks
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    r = np.random.default_rng(9)
    for _ in range(5):
        x, y = r.integers(40, n - 40, 2)
        pts = [(x, y)]
        for _ in range(14):
            x += r.integers(-14, 14)
            y += r.integers(6, 22)
            pts.append((x, y))
        d.line(pts, fill=(70, 68, 64), width=1)
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    save("Yard_Concrete", mirror_tile(np.asarray(img, dtype=np.float64)))


def floor():
    n = 512
    base = 0.5 * noise(n, 6, 0.6, 11) + 0.3 * noise(n, 8, 0.7, 12) + 0.2 * noise(n, 2, 0.5, 13)
    oil = noise(n, 5, 0.65, 14)
    wear = noise(n, 3, 0.5, 16)
    col = np.zeros((n, n, 3))
    g = 138 + base * 46 - wear * 26
    oilmask = np.clip((oil - 0.58) * 4.5, 0, 1)
    g = g * (1 - 0.40 * oilmask)
    # tyre / drag streaks: long soft dark bands
    streak = np.asarray(Image.fromarray((noise(n, 3, 0.5, 17) * 255).astype(np.uint8)).resize((n // 6, n)).resize((n, n), Image.BICUBIC), dtype=np.float64) / 255.0
    g = g * (1 - 0.22 * np.clip((streak - 0.55) * 3.0, 0, 1))
    rust = np.clip((noise(n, 4, 0.6, 15) - 0.82) * 5.0, 0, 1) * 0.30
    col[..., 0] = g * (1.0 + 0.08 * rust) * (1 - 0.04 * oilmask)
    col[..., 1] = g * (1.0 - 0.06 * rust)
    col[..., 2] = g * (0.96 - 0.20 * rust)
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    r = np.random.default_rng(19)
    for _ in range(7):
        x, y = r.integers(30, n - 30, 2)
        pts = [(x, y)]
        for _ in range(16):
            x += r.integers(-12, 12)
            y += r.integers(6, 22)
            pts.append((x, y))
        d.line(pts, fill=(58, 56, 54), width=1)
    for x in (0, n - 2):  # slab joints, mirror tiling doubles them
        d.line([(x, 0), (x, n)], fill=(84, 82, 78), width=3)
    d.line([(0, n - 2), (n, n - 2)], fill=(84, 82, 78), width=3)
    save("Yard_Floor", mirror_tile(np.asarray(img.filter(ImageFilter.GaussianBlur(0.5)), dtype=np.float64)))


def frost_mouth():
    # the small clump of white ice right at the valve mouth: mostly covered, soft ragged edge
    n = 256
    m = 0.6 * noise(n, 5, 0.6, 91) + 0.4 * noise(n, 7, 0.75, 92)
    alpha = np.clip((m - 0.30) * 8.0, 0, 1)
    col = np.zeros((n, n, 4))
    k = 0.5 + 0.5 * noise(n, 6, 0.6, 93)
    col[..., 0] = 226 + 28 * k
    col[..., 1] = 238 + 16 * k
    col[..., 2] = 250 + 5 * k
    col[..., 3] = alpha * 255
    save("Yard_FrostMouth", mirror_tile(col), "RGBA")


def wall_white():
    # old whitewashed plaster: off-white, damp streaks running down from the top, soot near the floor, peeled patches, cracks, rust drips
    n = 512
    base = 0.5 * noise(n, 6, 0.6, 81) + 0.3 * noise(n, 8, 0.7, 82) + 0.2 * noise(n, 3, 0.5, 83)
    streak_src = noise(n, 4, 0.55, 84)
    streak = np.asarray(Image.fromarray((streak_src * 255).astype(np.uint8)).resize((n // 5, n)).resize((n, n), Image.BICUBIC), dtype=np.float64) / 255.0
    yy = np.linspace(0, 1, n)[:, None]  # 0 = top
    g = 232 + base * 18 - 10
    g -= np.clip((streak - 0.45) * 2.4, 0, 1) * 62 * (1.0 - 0.5 * yy)  # damp streaks
    g -= np.clip((yy - 0.72) * 3.2, 0, 1) * 55  # soot / splash near the floor
    patch = np.clip((noise(n, 5, 0.65, 85) - 0.76) * 4.0, 0, 1) * 0.7
    col = np.zeros((n, n, 3))
    # peeled paint shows the grey concrete below
    gg = g * (1 - patch) + (132 + base * 30) * patch
    col[..., 0] = gg * 1.0
    col[..., 1] = gg * 0.985
    col[..., 2] = gg * (0.93 - 0.05 * np.clip((streak - 0.5) * 2, 0, 1))
    rust = np.clip((noise(n, 4, 0.6, 86) - 0.88) * 6.0, 0, 1) * np.clip(1.0 - yy * 0.4, 0, 1) * 0.5
    col[..., 0] += rust * 24
    col[..., 1] -= rust * 18
    col[..., 2] -= rust * 40
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    r = np.random.default_rng(87)
    for _ in range(8):
        x, y = int(r.integers(30, n - 30)), int(r.integers(0, n // 2))
        pts = [(x, y)]
        for _ in range(18):
            x += int(r.integers(-10, 10))
            y += int(r.integers(8, 24))
            pts.append((x, y))
        d.line(pts, fill=(78, 74, 68), width=1)
    save("Yard_WallWhite", mirror_tile(np.asarray(img.filter(ImageFilter.GaussianBlur(0.6)), dtype=np.float64)))


def tin():
    w, h = 256, 256
    x = np.arange(w)
    ridge = 0.5 + 0.5 * np.sin(x / w * 2 * math.pi * 8)  # 8 corrugations per tile
    grime = noise(256, 5, 0.6, 21)
    rust = np.clip((noise(256, 5, 0.65, 22) - 0.55) * 2.4, 0, 1)
    yy = np.linspace(0, 1, h)[:, None]
    col = np.zeros((h, w, 3))
    shade = 0.55 + 0.45 * ridge[None, :]
    g = (150 + grime * 40) * shade * (1 - 0.18 * yy)
    col[..., 0] = g * (1 + 0.55 * rust)
    col[..., 1] = g * (1 - 0.08 * rust) * (1 + 0.0)
    col[..., 2] = g * (1 - 0.45 * rust)
    save("Yard_Tin", col)


def font(path, size):
    return ImageFont.truetype(path, size, layout_engine=ImageFont.Layout.RAQM)


def centered(d, text, box, fnt, fill):
    x0, y0, x1, y1 = box
    bb = d.textbbox((0, 0), text, font=fnt)
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    d.text(((x0 + x1) / 2 - w / 2 - bb[0], (y0 + y1) / 2 - h / 2 - bb[1]), text, font=fnt, fill=fill)


def diamond(d, cx, cy, r, fill_border=(205, 25, 30), bg=(255, 255, 255, 255)):
    d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=fill_border)
    ri = r * 0.86
    d.polygon([(cx, cy - ri), (cx + ri, cy), (cx, cy + ri), (cx - ri, cy)], fill=bg)


def skull(d, cx, cy, s, fill=(20, 20, 20, 255), bg=(255, 255, 255, 255)):
    d.ellipse([cx - 0.30 * s, cy - 0.42 * s, cx + 0.30 * s, cy + 0.14 * s], fill=fill)
    d.rectangle([cx - 0.16 * s, cy + 0.02 * s, cx + 0.16 * s, cy + 0.26 * s], fill=fill)
    d.ellipse([cx - 0.20 * s, cy - 0.22 * s, cx - 0.04 * s, cy - 0.04 * s], fill=bg)
    d.ellipse([cx + 0.04 * s, cy - 0.22 * s, cx + 0.20 * s, cy - 0.04 * s], fill=bg)
    d.polygon([(cx, cy - 0.04 * s), (cx - 0.04 * s, cy + 0.06 * s), (cx + 0.04 * s, cy + 0.06 * s)], fill=bg)
    for k in (-0.09, 0.0, 0.09):
        d.line([(cx + k * s, cy + 0.14 * s), (cx + k * s, cy + 0.26 * s)], fill=bg, width=max(1, int(0.02 * s)))
    for sg in (-1, 1):  # crossed bones
        d.line([(cx - sg * 0.42 * s, cy + 0.20 * s), (cx + sg * 0.42 * s, cy + 0.48 * s)], fill=fill, width=int(0.07 * s))
        for e in (-1, 1):
            ex = cx + e * sg * 0.42 * s
            ey = cy + (0.48 if e == 1 else 0.20) * s * (1 if sg == 1 else 1)
    for sx in (-0.44, 0.44):
        for sy in (0.18, 0.50):
            d.ellipse([cx + sx * s - 0.06 * s, cy + sy * s - 0.06 * s, cx + sx * s + 0.06 * s, cy + sy * s + 0.06 * s], fill=fill)


def corrosion(d, cx, cy, s, fill=(20, 20, 20, 255)):
    # two drops falling on a bar and on a hand
    for sx in (-0.24, 0.16):
        x = cx + sx * s
        d.polygon([(x, cy - 0.46 * s), (x - 0.07 * s, cy - 0.32 * s), (x + 0.07 * s, cy - 0.32 * s)], fill=fill)
        d.ellipse([x - 0.07 * s, cy - 0.37 * s, x + 0.07 * s, cy - 0.23 * s], fill=fill)
        for k in range(3):
            d.ellipse([x - 0.02 * s, cy - 0.17 * s + k * 0.07 * s, x + 0.02 * s, cy - 0.13 * s + k * 0.07 * s], fill=fill)
    d.rectangle([cx - 0.46 * s, cy + 0.14 * s, cx - 0.02 * s, cy + 0.22 * s], fill=fill)
    d.polygon([(cx + 0.06 * s, cy + 0.32 * s), (cx + 0.10 * s, cy + 0.10 * s), (cx + 0.46 * s, cy + 0.10 * s), (cx + 0.42 * s, cy + 0.32 * s)], fill=fill)


def sign():
    W, H = 1400, 900
    img = Image.new("RGB", (W, H), (236, 234, 226))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W, 400], fill=(244, 188, 28))
    d.rectangle([0, 0, W - 1, H - 1], outline=(40, 40, 40), width=10)
    # warning triangle
    d.polygon([(130, 340), (260, 90), (390, 340)], fill=(20, 20, 20))
    d.polygon([(160, 322), (260, 135), (360, 322)], fill=(244, 188, 28))
    d.rectangle([252, 190, 268, 262], fill=(20, 20, 20))
    d.ellipse([250, 276, 270, 296], fill=(20, 20, 20))
    bn = font(BN_BOLD, 64)
    lat = font(LAT_BOLD, 54)
    centered(d, "সাবধান!  বিষাক্ত গ্যাস সিলিন্ডার", (430, 70, W - 40, 220), bn, (24, 24, 24))
    centered(d, "CAUTION! TOXIC GAS CYLINDER", (430, 240, W - 40, 330), lat, (24, 24, 24))
    d.rectangle([0, 400, W, 410], fill=(40, 40, 40))
    # corrosive diamond and no-smoking circle
    diamond(d, 170, 640, 120)
    corrosion(d, 170, 650, 150)
    cx, cy, r = 470, 640, 105
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(205, 25, 30))
    d.ellipse([cx - r + 16, cy - r + 16, cx + r - 16, cy + r - 16], fill=(255, 255, 255))
    d.rectangle([cx - 70, cy + 16, cx + 40, cy + 36], fill=(25, 25, 25))
    d.rectangle([cx + 52, cy + 16, cx + 66, cy + 36], fill=(25, 25, 25))
    d.line([(cx + 8, cy + 6), (cx + 8, cy - 40)], fill=(150, 150, 150), width=8)
    d.line([(cx - r + 30, cy + r - 30), (cx + r - 30, cy - r + 30)], fill=(205, 25, 30), width=18)
    centered(d, "ধূমপান নিষেধ", (640, 520, W - 40, 650), font(BN_BOLD, 108), (24, 24, 24))
    centered(d, "NO SMOKING", (640, 650, W - 40, 780), font(LAT_BOLD, 108), (24, 24, 24))
    # age: dirt and scratches
    arr = np.asarray(img, dtype=np.float64)
    dirt = noise(512, 6, 0.6, 31)
    dirt = np.asarray(Image.fromarray((dirt * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), dtype=np.float64) / 255.0
    arr *= (0.88 + 0.14 * dirt)[..., None]
    arr[600:, :, :] *= 0.97
    save("Sign_ToxicGas", arr)


def label():
    W, H = 512, 640
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    white = (238, 240, 238, 255)
    lat = font(LAT_BOLD, 84)
    bn = font(BN_BOLD, 84)
    centered(d, "CHLORINE", (0, 20, W, 140), lat, white)
    centered(d, "ক্লোরিন", (0, 150, W, 270), bn, white)
    # skull diamond and corrosion diamond side by side
    diamond(d, 130, 450, 120, bg=(246, 246, 246, 255))
    skull(d, 130, 440, 120)
    diamond(d, 372, 450, 120, bg=(246, 246, 246, 255))
    corrosion(d, 372, 455, 150)
    # worn off a little
    arr = np.asarray(img, dtype=np.float64)
    wear = noise(512, 6, 0.65, 41)
    wear = np.asarray(Image.fromarray((wear * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), dtype=np.float64) / 255.0
    arr[..., 3] *= np.clip(0.78 + 0.5 * wear, 0, 1)
    arr[..., :3] *= (0.86 + 0.18 * wear)[..., None]
    save("Label_Chlorine", arr, "RGBA")


def windsock():
    W, H = 256, 512
    img = Image.new("RGB", (W, H), (240, 240, 240))
    d = ImageDraw.Draw(img)
    for i in range(6):
        y0 = i * H // 6
        d.rectangle([0, y0, W, y0 + H // 6], fill=(232, 96, 28) if i % 2 == 0 else (244, 242, 238))
    arr = np.asarray(img, dtype=np.float64)
    dirt = noise(256, 4, 0.6, 51)
    dirt = np.asarray(Image.fromarray((dirt * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), dtype=np.float64) / 255.0
    arr *= (0.86 + 0.16 * dirt)[..., None]
    save("Windsock", arr)


def frost():
    # ice crust: ragged white-blue patches (alpha) with a few bright crystal sparkles, for the valve of the leaking cylinder
    n = 256
    big = noise(n, 5, 0.6, 71)
    fine = noise(n, 7, 0.75, 72)
    m = 0.6 * big + 0.4 * fine
    alpha = np.clip((m - 0.56) * 8.0, 0, 1) * 0.85
    col = np.zeros((n, n, 4))
    k = 0.5 + 0.5 * fine
    col[..., 0] = 214 + 36 * k
    col[..., 1] = 230 + 22 * k
    col[..., 2] = 246 + 9 * k
    spark = (noise(n, 8, 0.8, 73) > 0.86).astype(np.float64)
    col[..., :3] = np.clip(col[..., :3] + spark[..., None] * 40, 0, 255)
    col[..., 3] = alpha * 255
    save("Yard_Frost", mirror_tile(col), "RGBA")


def sky():
    # pale hazy overcast: bluish at the top, almost white at the horizon (the dome is centred slightly below the floor)
    h = 512
    t = np.linspace(1.0, 0.0, h)[:, None]  # row 0 = top of the dome
    top = np.array([128, 160, 205], dtype=np.float64)
    hor = np.array([226, 230, 228], dtype=np.float64)
    k = np.clip((t - 0.5) / 0.5, 0, 1) ** 0.8
    col = hor[None, :] * (1 - k) + top[None, :] * k
    cloud = noise(512, 5, 0.6, 61)[:h, :256]
    arr = np.repeat(col[:, None, :], 256, axis=1)
    arr = arr + (cloud[..., None] - 0.5) * 26 * np.clip(k + 0.25, 0, 1)[:, :, None]
    save("Yard_Sky", arr)


if __name__ == "__main__":
    frost_mouth()
    wall_white()
    frost()
    sky()
    concrete()
    floor()
    tin()
    sign()
    label()
    windsock()
    print("Sc11 yard textures written to", OUT)
