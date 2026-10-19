"""Generates the SC02 storage-room textures (walls, floor, fan grille, louvre, legend, hand sign, door decals).

Output: Assets/Art/Presentable/2D/Env/SC02_*.png   (re-run: python Tools/generate_sc02_room_art.py)
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env")
SC02 = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc02")
FONT = "C:/Windows/Fonts/arialbd.ttf"
rng = np.random.default_rng(7)


def font(size):
    return ImageFont.truetype(FONT, size)


def noise(size, scale, seed):
    """Tileable-ish value noise in 0..1 (wrap-padded blur)."""
    r = np.random.default_rng(seed)
    small = r.random((scale, scale)).astype(np.float32)
    im = Image.fromarray((small * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255.0


def fbm(size, seed):
    return (noise(size, 4, seed) * 0.5 + noise(size, 12, seed + 1) * 0.3 + noise(size, 40, seed + 2) * 0.2)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name))
    print("wrote", name)


def wall():
    n = 512
    f = fbm(n, 1)
    grain = rng.random((n, n)).astype(np.float32)
    base = np.array([150, 158, 166], dtype=np.float32)
    img = base[None, None, :] * (0.9 + 0.16 * f[..., None]) * (0.97 + 0.05 * grain[..., None])
    # vertical grime streaks
    streak = noise(n, 3, 5)[:1, :]
    img *= (0.95 + 0.07 * np.repeat(noise(n, 24, 9)[:1, :], n, axis=0))[..., None]
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    # panel seams on tile edges
    d.line([(0, 0), (n, 0)], fill=(112, 120, 128), width=3)
    d.line([(0, 3), (n, 3)], fill=(176, 184, 192), width=1)
    d.line([(0, 0), (0, n)], fill=(116, 124, 132), width=3)
    d.line([(3, 0), (3, n)], fill=(176, 184, 192), width=1)
    for x, y in [(22, 22), (n - 22, 22), (22, n - 22), (n - 22, n - 22)]:
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=(122, 130, 138))
    save(im.filter(ImageFilter.GaussianBlur(0.6)), "SC02_Wall.png")


def floor():
    n = 512
    f = fbm(n, 21)
    fine = rng.random((n, n)).astype(np.float32)
    base = np.array([92, 98, 104], dtype=np.float32)
    img = base[None, None, :] * (0.82 + 0.3 * f[..., None]) * (0.95 + 0.1 * fine[..., None])
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    d.line([(0, 0), (n, 0)], fill=(58, 62, 66), width=3)
    d.line([(0, 0), (0, n)], fill=(58, 62, 66), width=3)
    save(im.filter(ImageFilter.GaussianBlur(0.7)), "SC02_Floor.png")


def fan_grille():
    n = 512
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    steel = (74, 80, 86, 255)
    hi = (150, 158, 166, 255)
    d.rounded_rectangle([0, 0, n - 1, n - 1], 28, fill=steel)
    d.rounded_rectangle([10, 10, n - 11, n - 11], 22, outline=hi, width=4)
    d.rounded_rectangle([34, 34, n - 35, n - 35], 18, fill=(0, 0, 0, 0))  # clears inner area
    cx = cy = n // 2
    R = n // 2 - 46
    # inner opening is transparent; add guard (rings + spokes)
    cut = Image.new("L", (n, n), 0)
    ImageDraw.Draw(cut).ellipse([cx - R, cy - R, cx + R, cy + R], fill=255)
    frame_fill = Image.new("RGBA", (n, n), steel)
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], 28, fill=255)
    base = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    base.paste(frame_fill, (0, 0), mask)
    inv = cut.point(lambda v: 255 - v)
    alpha = Image.new("L", (n, n), 0)
    alpha.paste(mask, (0, 0))
    alpha = Image.composite(Image.new("L", (n, n), 0), alpha, cut)
    base.putalpha(alpha)
    d = ImageDraw.Draw(base)
    d.rounded_rectangle([10, 10, n - 11, n - 11], 22, outline=hi, width=4)
    d.ellipse([cx - R - 4, cy - R - 4, cx + R + 4, cy + R + 4], outline=(46, 50, 54, 255), width=10)
    for k in (0.33, 0.62, 0.9):
        r = int(R * k)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(66, 72, 78, 255), width=5)
    for a in range(0, 360, 30):
        x = cx + math.cos(math.radians(a)) * R
        y = cy + math.sin(math.radians(a)) * R
        d.line([(cx, cy), (x, y)], fill=(66, 72, 78, 255), width=5)
    d.ellipse([cx - 22, cy - 22, cx + 22, cy + 22], fill=(40, 44, 48, 255))
    for x, y in [(20, 20), (n - 20, 20), (20, n - 20), (n - 20, n - 20)]:
        d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=(120, 128, 136, 255))
    save(base, "SC02_FanGrille.png")


def louvre():
    n = 512
    im = Image.new("RGBA", (n, n), (70, 76, 82, 255))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, n - 1, n - 1], 24, outline=(150, 158, 166, 255), width=6)
    y = 48
    while y < n - 56:
        d.polygon([(40, y), (n - 40, y), (n - 40, y + 22), (40, y + 30)], fill=(122, 130, 138, 255))
        d.rectangle([40, y + 30, n - 40, y + 40], fill=(24, 26, 30, 255))
        y += 46
    for x, yy in [(20, 20), (n - 20, 20), (20, n - 20), (n - 20, n - 20)]:
        d.ellipse([x - 6, yy - 6, x + 6, yy + 6], fill=(150, 158, 166, 255))
    save(im, "SC02_Louvre.png")


def hand_sign():
    n = 256
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, n - 1, n - 1], 22, fill=(255, 255, 255, 255))
    d.rounded_rectangle([10, 10, n - 11, n - 11], 16, fill=(18, 92, 190, 255))
    w = (255, 255, 255, 255)
    d.rounded_rectangle([88, 118, 170, 208], 24, fill=w)  # palm
    for i, (x, top) in enumerate([(92, 52), (113, 36), (134, 40), (155, 58)]):
        d.rounded_rectangle([x - 9, top, x + 9, 150], 9, fill=w)
    d.rounded_rectangle([60, 120, 92, 146], 12, fill=w)  # thumb
    save(im, "SC02_HandSign.png")


def legend():
    w, h = 720, 330
    im = Image.new("RGBA", (w, h), (238, 240, 242, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w - 1, h - 1], outline=(70, 76, 82, 255), width=5)
    d.text((26, 18), "LEGEND:", font=font(40), fill=(30, 34, 38, 255))
    rows = [
        ("= GENERALLY COMPATIBLE", "circle"),
        ("= CONTROLLED, check SDS", "tri"),
        ("= INCOMPATIBLE, never together", "x"),
    ]
    y = 80
    for text, kind in rows:
        cx, cy = 54, y + 26
        if kind == "circle":
            d.ellipse([cx - 20, cy - 20, cx + 20, cy + 20], fill=(30, 168, 70, 255))
        elif kind == "tri":
            d.polygon([(cx, cy - 22), (cx + 24, cy + 18), (cx - 24, cy + 18)], fill=(245, 200, 20, 255),
                      outline=(40, 40, 40, 255))
            d.text((cx - 4, cy - 8), "!", font=font(24), fill=(30, 30, 30, 255))
        else:
            d.line([(cx - 18, cy - 18), (cx + 18, cy + 18)], fill=(210, 30, 30, 255), width=9)
            d.line([(cx - 18, cy + 18), (cx + 18, cy - 18)], fill=(210, 30, 30, 255), width=9)
        d.text((96, y + 6), text, font=font(26), fill=(30, 34, 38, 255))
        y += 78
    save(im, "SC02_Legend.png")


def decal(icon_name, label, out):
    """Big hazard icon with a label under it, printed on the glass door."""
    w, h = 512, 1024
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    plate = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pd = ImageDraw.Draw(plate)
    pd.rounded_rectangle([20, 150, w - 20, 700], 36, fill=(236, 240, 244, 150))
    im = Image.alpha_composite(im, plate)
    icon = Image.open(os.path.join(SC02, icon_name + ".png")).convert("RGBA")
    box = icon.getbbox()
    if box:
        icon = icon.crop(box)
    s = 330 / max(icon.size)
    icon = icon.resize((max(1, int(icon.width * s)), max(1, int(icon.height * s))), Image.LANCZOS)
    im.alpha_composite(icon, ((w - icon.width) // 2, 190))
    d = ImageDraw.Draw(im)
    lines = label.split("\n")
    size = 60 if len(max(lines, key=len)) <= 8 else 48
    f = font(size)
    y = 560
    for line in lines:
        tw = d.textlength(line, font=f)
        d.text(((w - tw) / 2, y), line, font=f, fill=(24, 28, 32, 255))
        y += int(size * 1.15)
    save(im, out)


if __name__ == "__main__":
    wall()
    floor()
    fan_grille()
    louvre()
    hand_sign()
    legend()
    decal("GHS_Flammable", "FLAMMABLES", "SC02_Decal_Flammables.png")
    decal("GHS_Oxidizer", "OXIDIZERS", "SC02_Decal_Oxidizers.png")
    decal("GHS_Acid", "ACIDS", "SC02_Decal_Acids.png")
    decal("GHS_General", "GENERAL\nSTORAGE", "SC02_Decal_General.png")
