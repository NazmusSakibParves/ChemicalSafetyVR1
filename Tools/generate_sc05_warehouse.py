"""Scenario 5 warehouse signs: the pillar's aged VAPOR HAZARD plate (Bangla + English, two GHS diamonds) and the small SPOT CLEANING board.

    python Tools/generate_sc05_warehouse.py

Writes Assets/Art/Presentable/2D/Sc05/VaporSignClient.png and Sign_SpotCleaning.png (Bangla shaped with libraqm, as in
Tools/render_content_bn.py). Does not touch other scenarios.
"""
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc05")
BN_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")
EN_BOLD = r"C:\Windows\Fonts\arialbd.ttf"
EN_BLACK = r"C:\Windows\Fonts\ariblk.ttf"
RED = (214, 30, 36, 255)
BLK = (18, 18, 18, 255)


def grime(img, strength=40, seed=3):
    """Dirt, scratches and rusty corners over an RGBA image (alpha kept)."""
    rnd = np.random.RandomState(seed)
    a = np.asarray(img, dtype=np.float32)
    h, w = a.shape[:2]
    noise = rnd.rand(h // 4 + 1, w // 4 + 1).astype(np.float32)
    big = np.asarray(Image.fromarray((noise * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(6)), dtype=np.float32) / 255.0
    a[..., :3] *= (1.0 - (1.0 - big[..., None]) * (strength / 100.0))
    # darker, rusty edge
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum.reduce([xx, yy, w - 1 - xx, h - 1 - yy]) / (0.08 * min(w, h))
    edge = np.clip(1.0 - edge, 0, 1)[..., None]
    rust = np.array([120, 70, 36], dtype=np.float32)
    a[..., :3] = a[..., :3] * (1 - 0.55 * edge) + rust * 0.55 * edge
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def diamond(d, cx, cy, r):
    pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
    d.polygon(pts, fill=(255, 255, 255, 255))
    d.line(pts + [pts[0]], fill=RED, width=max(6, int(r * 0.1)))


def health_glyph(d, cx, cy, r):
    k = r / 100.0
    d.ellipse((cx - 14 * k, cy - 50 * k, cx + 14 * k, cy - 22 * k), fill=BLK)           # head
    d.polygon([(cx - 36 * k, cy + 38 * k), (cx - 26 * k, cy - 12 * k), (cx + 26 * k, cy - 12 * k), (cx + 36 * k, cy + 38 * k)], fill=BLK)  # torso
    for ang in range(0, 360, 45):                                                        # star burst on the chest
        import math
        x2 = cx + math.cos(math.radians(ang)) * 17 * k
        y2 = cy + 8 * k + math.sin(math.radians(ang)) * 17 * k
        d.line((cx, cy + 8 * k, x2, y2), fill=(255, 255, 255, 255), width=max(2, int(4 * k)))


def flame_glyph(d, cx, cy, r):
    k = r / 100.0
    d.polygon([(cx, cy - 52 * k), (cx + 34 * k, cy - 2 * k), (cx + 24 * k, cy + 30 * k), (cx - 24 * k, cy + 30 * k), (cx - 34 * k, cy - 2 * k), (cx - 8 * k, cy - 16 * k)], fill=BLK)
    d.ellipse((cx - 12 * k, cy - 2 * k, cx + 12 * k, cy + 26 * k), fill=(255, 255, 255, 255))
    d.rectangle((cx - 38 * k, cy + 38 * k, cx + 38 * k, cy + 48 * k), fill=BLK)


def vapor_sign():
    w, h = 1024, 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, w - 1, h - 1), 26, fill=(228, 224, 212, 255), outline=(60, 56, 50, 255), width=10)
    d.rounded_rectangle((22, 22, w - 23, h - 23), 18, outline=(90, 84, 76, 255), width=4)
    diamond(d, 150, 140, 104)
    health_glyph(d, 150, 140, 104)
    diamond(d, 150, 372, 104)
    flame_glyph(d, 150, 372, 104)
    bn1 = ImageFont.truetype(BN_BOLD, 110, layout_engine=ImageFont.Layout.RAQM)
    bn2 = ImageFont.truetype(BN_BOLD, 74, layout_engine=ImageFont.Layout.RAQM)
    d.text((300, 120), "বাষ্প সতর্কতা", font=bn1, fill=BLK, anchor="lm")
    d.text((300, 238), "বাষ্পের ঝুঁকি /", font=bn2, fill=BLK, anchor="lm")
    d.text((300, 372), "VAPOR HAZARD", font=ImageFont.truetype(EN_BLACK, 80), fill=BLK, anchor="lm")
    g = grime(img, 22)
    g.save(os.path.join(OUT, "VaporSignClient.png"))
    print("VaporSignClient.png")


def spot_sign():
    w, h = 512, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, w - 1, h - 1), 10, fill=(232, 230, 224, 255), outline=(70, 70, 66, 255), width=6)
    d.text((w // 2, 78), "SPOT CLEANING", font=ImageFont.truetype(EN_BOLD, 62), fill=(30, 30, 30, 255), anchor="mm")
    d.line((40, 128, w - 40, 128), fill=(60, 60, 60, 255), width=3)
    f = ImageFont.truetype(EN_BOLD, 26)
    d.text((w // 2, 168), "WASH AREA  -  KEEP CLEAR", font=f, fill=(40, 40, 40, 255), anchor="mm")
    d.text((w // 2, 208), "CODE: SC-05 / STORE", font=ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 22), fill=(70, 70, 70, 255), anchor="mm")
    grime(img, 30, seed=7).save(os.path.join(OUT, "Sign_SpotCleaning.png"))
    print("Sign_SpotCleaning.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    random.seed(1)
    vapor_sign()
    spot_sign()
