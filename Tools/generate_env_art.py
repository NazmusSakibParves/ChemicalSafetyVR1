"""Generates warehouse signs and floor stains for the Presentable scene.

Output: Assets/Art/Presentable/2D/Env/*.png   (re-run: python Tools/generate_env_art.py)
Bangla text goes through the same RAQM + Latin-run pipeline as render_bangla_text.py.
"""

import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_bangla_text as rb  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env")

YELLOW = (250, 205, 25, 255)
GREEN = (14, 135, 70, 255)
BLACK = (20, 20, 20, 255)
WHITE = (255, 255, 255, 255)


def fonts(size, bold):
    return {
        False: ImageFont.truetype(rb.FONT_BOLD if bold else rb.FONT_REGULAR, size,
                                  layout_engine=ImageFont.Layout.RAQM),
        True: ImageFont.truetype(rb.LATIN_BOLD if bold else rb.LATIN_REGULAR, int(size * 0.92),
                                 layout_engine=ImageFont.Layout.RAQM),
    }


def text_block(draw, text, size, bold, cx, top, max_w, fill):
    f = fonts(size, bold)
    lines = rb.wrap(draw, text, f, max_w)
    lh = int(size * 1.35)
    y = top
    for line in lines:
        x = cx - rb.measure(draw, line, f) / 2
        for lat, chunk in rb.runs(line):
            draw.text((x, y + (int(size * 0.03) if lat else 0)), chunk, font=f[lat], fill=fill)
            x += draw.textlength(chunk, font=f[lat])
        y += lh
    return y


def board(w, h, color):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((2, 2, w - 3, h - 3), radius=26, fill=color)
    fg = BLACK if color == YELLOW else WHITE
    d.rounded_rectangle((16, 16, w - 17, h - 17), radius=18, outline=fg, width=6)
    return img, d, fg


def triangle(d, cx, cy, r, flame=False):
    pts = [(cx, cy - r), (cx - r * 0.95, cy + r * 0.8), (cx + r * 0.95, cy + r * 0.8)]
    d.polygon(pts, fill=YELLOW, outline=BLACK)
    d.line(pts + [pts[0]], fill=BLACK, width=10, joint="curve")
    if flame:
        d.ellipse((cx - r * 0.28, cy - r * 0.1, cx + r * 0.28, cy + r * 0.55), fill=BLACK)
        d.polygon([(cx, cy - r * 0.5), (cx - r * 0.2, cy + r * 0.1), (cx + r * 0.2, cy + r * 0.1)], fill=BLACK)
    else:
        d.rectangle((cx - 9, cy - r * 0.35, cx + 9, cy + r * 0.2), fill=BLACK)
        d.ellipse((cx - 11, cy + r * 0.3, cx + 11, cy + r * 0.52), fill=BLACK)


def cross(d, cx, cy, r):
    d.rounded_rectangle((cx - r * 0.3, cy - r, cx + r * 0.3, cy + r), radius=10, fill=WHITE)
    d.rounded_rectangle((cx - r, cy - r * 0.3, cx + r, cy + r * 0.3), radius=10, fill=WHITE)


def tall(name, color, icon, text, size):
    img, d, fg = board(512, 700, color)
    if icon == "warn":
        triangle(d, 256, 190, 130)
    elif icon == "flame":
        triangle(d, 256, 190, 130, flame=True)
    else:
        cross(d, 256, 190, 105)
    text_block(d, text, size, True, 256, 370, 440, fg)
    img.save(os.path.join(OUT, name + ".png"))


def wide(name, color, lines, arrow=False):
    img, d, fg = board(768, 330, color)
    y = 50
    for text, size in lines:
        y = text_block(d, text, size, True, 384, y, 690, fg) + 4
    if arrow:
        d.polygon([(640, 255), (700, 255), (700, 235), (740, 270), (700, 305), (700, 285), (640, 285)], fill=fg)
    img.save(os.path.join(OUT, name + ".png"))


def stain(name, color, seed, size=512, blobs=26, spread=0.28):
    rnd = random.Random(seed)
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    c = size / 2
    for _ in range(blobs):
        a = rnd.uniform(0, math.tau)
        dist = rnd.uniform(0, size * spread) * rnd.random()
        r = rnd.uniform(size * 0.03, size * 0.13) * (1.2 - dist / (size * spread) * 0.6)
        x, y = c + math.cos(a) * dist * 1.25, c + math.sin(a) * dist * 0.8
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)
    for _ in range(14):
        a = rnd.uniform(0, math.tau)
        dist = rnd.uniform(size * 0.2, size * 0.42)
        r = rnd.uniform(2, 9)
        x, y = c + math.cos(a) * dist, c + math.sin(a) * dist * 0.8
        d.ellipse((x - r, y - r, x + r, y + r), fill=230)
    mask = mask.filter(ImageFilter.GaussianBlur(size * 0.012)).point(lambda v: min(255, int(v * 1.1)))
    shade = Image.effect_noise((size, size), 38).convert("L")
    img = Image.new("RGBA", (size, size), color + (0,))
    px = img.load()
    mp, sp = mask.load(), shade.load()
    for y in range(size):
        for x in range(size):
            a = mp[x, y]
            if a:
                k = 0.78 + sp[x, y] / 255 * 0.4
                px[x, y] = (int(color[0] * k), int(color[1] * k), int(color[2] * k), int(a * 0.88))
    img.save(os.path.join(OUT, name + ".png"))


def shaft(name, w=128, h=512):
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    px = img.load()
    for y in range(h):
        v = y / (h - 1)
        fall = (1.0 - v) ** 1.4
        for x in range(w):
            u = abs(x / (w - 1) * 2 - 1)
            edge = max(0.0, 1.0 - u ** 2.2)
            px[x, y] = (255, 255, 255, int(255 * fall * edge))
    img.save(os.path.join(OUT, name + ".png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    tall("SIGN_SafetyEquipment", YELLOW, "warn", "নিরাপত্তা উপকরণ", 56)
    tall("SIGN_SafetyFirst", GREEN, "cross", "নিরাপত্তা প্রথম", 56)
    tall("SIGN_DangerHazard", YELLOW, "flame", "সাবধান: বিপজ্জনক উপকরণ", 50)
    wide("SIGN_WarningHazardous", YELLOW, [("Warning:", 70), ("Hazardous Materials", 60)])
    wide("SIGN_CautionHazardous", YELLOW, [("সাবধান:", 74), ("Hazardous Materials", 58)])
    wide("SIGN_SafetyStart", GREEN, [("নিরাপত্তা শুরুর এলাকা", 62), ("Safety Start area", 56)], arrow=True)
    stain("STAIN_Blood", (120, 18, 14), 11)
    stain("STAIN_Rust", (112, 62, 28), 23, blobs=34, spread=0.34)
    stain("STAIN_Oil", (28, 26, 24), 37, blobs=20, spread=0.22)
    shaft("FX_Shaft")
    print("generated in", OUT)


if __name__ == "__main__":
    main()
