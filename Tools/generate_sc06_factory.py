"""Scenario 6 factory room textures: signs, switch plates, container labels and the broken-plaster brick patch.

    python Tools/generate_sc06_factory.py

Writes into Assets/Art/Presentable/2D/Sc06/ (Bangla shaped with libraqm). Does not touch other scenarios.
"""
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc06")
BN_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")
EN_BOLD = r"C:\Windows\Fonts\arialbd.ttf"
EN_BLACK = r"C:\Windows\Fonts\ariblk.ttf"


def f(path, size):
    return ImageFont.truetype(path, size)


def centred(d, text, font, cx, cy, fill):
    box = d.textbbox((0, 0), text, font=font)
    d.text((cx - (box[0] + box[2]) / 2, cy - (box[1] + box[3]) / 2), text, font=font, fill=fill)


def grime(img, strength=30, seed=3, rust=True):
    rnd = np.random.RandomState(seed)
    a = np.asarray(img.convert("RGBA"), dtype=np.float32)
    h, w = a.shape[:2]
    noise = rnd.rand(h // 5 + 2, w // 5 + 2).astype(np.float32)
    big = np.asarray(Image.fromarray((noise * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(5)), dtype=np.float32) / 255.0
    a[..., :3] *= (1.0 - (1.0 - big[..., None]) * (strength / 100.0))
    if rust:
        yy, xx = np.mgrid[0:h, 0:w]
        edge = np.clip(1.0 - np.minimum.reduce([xx, yy, w - 1 - xx, h - 1 - yy]) / (0.06 * min(w, h)), 0, 1)[..., None]
        a[..., :3] = a[..., :3] * (1 - 0.45 * edge) + np.array([110, 70, 40], dtype=np.float32) * 0.45 * edge
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def diamond(d, cx, cy, r, glyph):
    d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=(255, 255, 255, 255), outline=(214, 30, 36, 255))
    d.line([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy), (cx, cy - r)], fill=(214, 30, 36, 255), width=max(3, r // 9))
    if glyph == "flame":
        d.polygon([(cx, cy - r * 0.5), (cx + r * 0.32, cy + r * 0.05), (cx + r * 0.22, cy + r * 0.45), (cx - r * 0.22, cy + r * 0.45), (cx - r * 0.32, cy + r * 0.05)], fill=(20, 20, 20, 255))
    elif glyph == "corrosion":
        d.rectangle([cx - r * 0.3, cy - r * 0.45, cx + r * 0.3, cy - r * 0.05], fill=(20, 20, 20, 255))
        d.polygon([(cx - r * 0.35, cy + r * 0.1), (cx + r * 0.35, cy + r * 0.1), (cx, cy + r * 0.5)], fill=(20, 20, 20, 255))
    else:  # health hazard
        d.ellipse([cx - r * 0.16, cy - r * 0.45, cx + r * 0.16, cy - r * 0.13], fill=(20, 20, 20, 255))
        d.polygon([(cx - r * 0.4, cy + r * 0.5), (cx + r * 0.4, cy + r * 0.5), (cx + r * 0.22, cy - r * 0.05), (cx - r * 0.22, cy - r * 0.05)], fill=(20, 20, 20, 255))


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name))
    print("wrote", name, im.size)


def sign_fume_hood_area():
    im = Image.new("RGBA", (1000, 300), (28, 78, 168, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([8, 8, 991, 291], outline=(238, 242, 250, 255), width=6)
    centred(d, "ফুম হুড এলাকা", f(BN_BOLD, 118), 500, 96, (246, 248, 255, 255))
    centred(d, "FUME HOOD AREA", f(EN_BOLD, 92), 500, 218, (246, 248, 255, 255))
    save(grime(im, 26, 5), "Sign_FumeHoodArea.png")


def sign_caution_toxic():
    im = Image.new("RGBA", (900, 420), (244, 206, 24, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([10, 10, 889, 409], outline=(30, 30, 30, 255), width=7)
    centred(d, "সাবধান! বিষাক্ত বাষ্প", f(BN_BOLD, 84), 450, 100, (24, 24, 24, 255))
    centred(d, "CAUTION", f(EN_BLACK, 118), 450, 235, (24, 24, 24, 255))
    centred(d, "TOXIC VAPOURS", f(EN_BLACK, 84), 450, 340, (24, 24, 24, 255))
    save(grime(im, 24, 6), "Sign_CautionToxic.png")


def switch_plate(name, bn, en, seed):
    im = Image.new("RGBA", (512, 200), (214, 214, 208, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([4, 4, 507, 195], outline=(40, 40, 44, 255), width=4)
    centred(d, bn, f(BN_BOLD, 66), 256, 62, (24, 24, 24, 255))
    centred(d, en, f(EN_BOLD, 52), 256, 142, (24, 24, 24, 255))
    save(grime(im, 22, seed, False), name)


def label_water():
    im = Image.new("RGBA", (640, 400), (234, 226, 196, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([4, 4, 635, 395], outline=(24, 24, 28, 255), width=10)
    centred(d, "পানি", f(BN_BOLD, 160), 320, 125, (14, 14, 18, 255))
    centred(d, "WATER", f(EN_BLACK, 140), 320, 285, (14, 14, 18, 255))
    save(grime(im, 38, 8), "Label_WaterJug.png")


def label_mixture():
    im = Image.new("RGBA", (512, 512), (249, 244, 226, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([4, 4, 507, 507], outline=(24, 24, 28, 255), width=10)
    centred(d, "মিশ্রণ", f(BN_BOLD, 120), 256, 100, (14, 14, 18, 255))
    centred(d, "MIXTURE", f(EN_BLACK, 104), 256, 240, (14, 14, 18, 255))
    centred(d, "লঘু এসিড", f(BN_BOLD, 112), 256, 378, (150, 30, 30, 255))
    save(grime(im, 12, 9, False), "Label_MixtureJar.png")


def label_acid():
    im = Image.new("RGBA", (640, 480), (242, 240, 232, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 639, 120], fill=(236, 128, 36, 255))
    centred(d, "গাঢ় সালফিউরিক অ্যাসিড", f(BN_BOLD, 46), 320, 60, (255, 255, 255, 255))
    big, small = f(EN_BOLD, 66), f(EN_BOLD, 40)
    x = 40
    for txt, fnt, dy in [("CONC. H", big, 0), ("2", small, 32), ("SO", big, 0), ("4", small, 32)]:
        d.text((x, 136 + dy), txt, font=fnt, fill=(24, 24, 24, 255))
        x += d.textlength(txt, font=fnt) + 2
    for i, g in enumerate(["corrosion", "flame", "health"]):
        diamond(d, 100 + i * 150, 330, 62, g)
    d.text((40, 420), "DANGER  -  CORROSIVE", font=f(EN_BOLD, 34), fill=(190, 30, 30, 255))
    save(grime(im, 30, 10), "Label_AcidCan.png")


def label_ghs_box():
    im = Image.new("RGBA", (300, 110), (206, 206, 202, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([3, 3, 296, 106], outline=(40, 40, 44, 255), width=3)
    centred(d, "GHS", f(EN_BLACK, 76), 150, 56, (30, 30, 34, 255))
    save(grime(im, 20, 11, False), "Plate_Ghs.png")


def board_blue_safety():
    im = Image.new("RGBA", (360, 560), (30, 82, 170, 255))
    d = ImageDraw.Draw(im)
    d.rectangle([6, 6, 353, 553], outline=(238, 242, 250, 255), width=5)
    centred(d, "সুরক্ষা সরঞ্জাম পরুন", f(BN_BOLD, 40), 180, 60, (246, 248, 255, 255))
    centred(d, "WEAR PPE", f(EN_BOLD, 46), 180, 120, (246, 248, 255, 255))
    for i in range(3):
        cx = 70 + i * 110
        d.polygon([(cx, 300), (cx + 52, 392), (cx - 52, 392)], fill=(244, 206, 24, 255), outline=(20, 20, 20, 255))
        centred(d, "!", f(EN_BLACK, 56), cx, 358, (20, 20, 20, 255))
    d.ellipse([130, 430, 230, 530], fill=(238, 242, 250, 255))
    save(grime(im, 26, 12), "Sign_BlueBoard.png")


def brick_patch():
    """Broken plaster: an irregular hole with old bricks inside, a dark rim and plaster crumbs. RGBA, hole shape in alpha."""
    w, h = 512, 640
    rnd = random.Random(21)
    brick = Image.new("RGBA", (w, h), (120, 70, 56, 255))
    d = ImageDraw.Draw(brick)
    bh, bw = 52, 118
    for r in range(0, h // bh + 1):
        off = 0 if r % 2 == 0 else bw // 2
        for c in range(-1, w // bw + 2):
            x0 = c * bw + off
            y0 = r * bh
            tone = rnd.randint(-22, 22)
            col = (150 + tone, 80 + tone // 2, 60 + tone // 3, 255)
            d.rectangle([x0 + 3, y0 + 3, x0 + bw - 3, y0 + bh - 3], fill=col)
    brick = brick.filter(ImageFilter.GaussianBlur(0.8))
    # irregular hole mask
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    cx, cy = w * 0.5, h * 0.52
    pts = []
    for i in range(34):
        ang = i / 34 * 6.2832
        rx = (w * 0.40) * (0.78 + 0.28 * rnd.random())
        ry = (h * 0.44) * (0.74 + 0.30 * rnd.random())
        pts.append((cx + rx * np.cos(ang), cy + ry * np.sin(ang)))
    md.polygon(pts, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(2.2))
    a = np.asarray(brick, dtype=np.float32)
    m = np.asarray(mask, dtype=np.float32) / 255.0
    # dark rim inside the hole edge
    inner = np.asarray(mask.filter(ImageFilter.MinFilter(21)).filter(ImageFilter.GaussianBlur(7)), dtype=np.float32) / 255.0
    rim = np.clip(m - inner, 0, 1)[..., None]
    a[..., :3] = a[..., :3] * (1 - 0.7 * rim) + np.array([34, 28, 24], dtype=np.float32) * 0.7 * rim
    out = np.dstack([a[..., :3], (m * 255)]).astype(np.uint8)
    save(Image.fromarray(out, "RGBA"), "Wall_BrickPatch.png")


if __name__ == "__main__":
    sign_fume_hood_area()
    sign_caution_toxic()
    switch_plate("Plate_FumeHood.png", "ফুম হুড", "FUME HOOD", 13)
    switch_plate("Plate_ExhaustFan.png", "এক্সজস্ট ফ্যান", "EXHAUST FAN", 14)
    label_water()
    label_mixture()
    label_acid()
    label_ghs_box()
    board_blue_safety()
    brick_patch()
