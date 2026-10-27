"""Scenario 9 art: designer pack (SDS, whiteboard, flask labels) -> project, plus generated hologram text and floor marker.
Run: python -I Tools/generate_sc09_art.py
Decal-style textures go to 2D/Sc06Tex (Label_ names are imported as clamped decals), the SDS sheet to 2D/Sc09.
"""
import os
from collections import deque

from PIL import Image, ImageDraw, ImageFilter, ImageFont

PACK = r"C:\Users\msi\Downloads\Scene09 workflow-20261027T055804Z-1-001\Scene09 workflow\2D resources"
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Presentable", "2D")
TEX = os.path.join(ROOT, "Sc06Tex")
SC9 = os.path.join(ROOT, "Sc09")
os.makedirs(SC9, exist_ok=True)
FONT_B = r"C:\Windows\Fonts\arialbd.ttf"
FONT_R = r"C:\Windows\Fonts\arial.ttf"


def font(size, bold=True):
    return ImageFont.truetype(FONT_B if bold else FONT_R, size)


def fit(im, maxd):
    s = maxd / max(im.size)
    if s < 1:
        im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
    return im


def white_to_alpha(im, thresh=238):
    """Border-connected near-white pixels become transparent."""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        q.append((x, 0))
        q.append((x, h - 1))
    for y in range(h):
        q.append((0, y))
        q.append((w - 1, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y * w + x]:
            continue
        r, g, b, a = px[x, y]
        if min(r, g, b) < thresh:
            continue
        seen[y * w + x] = 1
        px[x, y] = (r, g, b, 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return im


def sds():
    im = Image.open(os.path.join(PACK, "SDS.jpg")).convert("RGB")
    d = ImageDraw.Draw(im)
    # the designer's sheet reads "Emergency procenees": repaint that bullet text
    paper = im.getpixel((365, 700))
    d.rectangle((120, 690, 372, 726), fill=paper)
    d.text((126, 696), "Emergency procedures", font=font(21, False), fill=(40, 40, 44))
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    # black surround outside the clipboard -> transparent
    seen = bytearray(w * h)
    q = deque([(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)])
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y * w + x]:
            continue
        r, g, b, a = px[x, y]
        if max(r, g, b) > 22:
            continue
        seen[y * w + x] = 1
        px[x, y] = (0, 0, 0, 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    im.save(os.path.join(SC9, "SDS_SC09.png"))
    print("SDS_SC09", im.size)


def labels():
    names = ["flask level.png", "flask level2.png", "flask level3.png", "flask level4.png"]
    for i, n in enumerate(names):
        im = white_to_alpha(Image.open(os.path.join(PACK, n)))
        im = fit(im, 512)
        im.save(os.path.join(TEX, "Label_Sc09Flask%d.png" % (i + 1)))
    board = fit(Image.open(os.path.join(PACK, "board writings.png")).convert("RGBA"), 1536)
    board.save(os.path.join(TEX, "Label_Sc09Board.png"))
    print("labels + board")


def glow_text(im, xy, text, f, fill, glow):
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).text(xy, text, font=f, fill=glow)
    layer = layer.filter(ImageFilter.GaussianBlur(7))
    im.alpha_composite(layer)
    im.alpha_composite(layer)
    ImageDraw.Draw(im).text(xy, text, font=f, fill=fill)


def holo():
    """Eye icon + HOLD Eyelids OPEN text, clock + 15 MINUTES (workflow step10)."""
    w, h = 1100, 420
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    green = (150, 255, 190, 255)
    glow = (60, 255, 140, 200)
    # eye icon
    lay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    cx, cy = 130, 95
    d.arc((cx - 100, cy - 52, cx + 100, cy + 52), 200, 340, fill=glow, width=7)
    d.arc((cx - 100, cy - 52, cx + 100, cy + 52), 20, 160, fill=glow, width=7)
    d.ellipse((cx - 32, cy - 32, cx + 32, cy + 32), outline=glow, width=7)
    d.ellipse((cx - 11, cy - 11, cx + 11, cy + 11), fill=glow)
    for k in range(-3, 4):
        d.line((cx + k * 22, cy - 64, cx + k * 26, cy - 90), fill=glow, width=5)
    d.polygon([(260, 95), (240, 80), (240, 110)], fill=glow)
    d.line((205, 95, 245, 95), fill=glow, width=6)
    b = lay.filter(ImageFilter.GaussianBlur(6))
    im.alpha_composite(b)
    im.alpha_composite(b)
    im.alpha_composite(lay)
    glow_text(im, (290, 38), "HOLD Eyelids OPEN. Roll eyes.", font(52), green, glow)
    glow_text(im, (290, 100), "Flash from inner to outer corners.", font(52), green, glow)
    # clock
    lay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    cx, cy = 170, 290
    d.ellipse((cx - 60, cy - 60, cx + 60, cy + 60), outline=glow, width=7)
    d.line((cx, cy, cx, cy - 38), fill=glow, width=7)
    d.line((cx, cy, cx + 28, cy + 16), fill=glow, width=7)
    b = lay.filter(ImageFilter.GaussianBlur(6))
    im.alpha_composite(b)
    im.alpha_composite(b)
    im.alpha_composite(lay)
    glow_text(im, (270, 252), "15 MINUTES", font(88), green, glow)
    im.save(os.path.join(TEX, "Label_Sc09Holo.png"))
    im.save(os.path.join(SC9, "Holo_EyeWash.png"))
    print("holo", im.size)


def stand_here():
    w, h = 768, 512
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    glow = (70, 255, 140, 255)
    lay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    d.polygon([(384, 20), (610, 230), (480, 230), (480, 330), (288, 330), (288, 230), (158, 230)], outline=glow, fill=(60, 255, 130, 70))
    d.line([(384, 20), (610, 230), (480, 230), (480, 330), (288, 330), (288, 230), (158, 230), (384, 20)], fill=glow, width=9)
    b = lay.filter(ImageFilter.GaussianBlur(9))
    im.alpha_composite(b)
    im.alpha_composite(lay)
    t = "STAND HERE"
    f = font(84)
    tw = ImageDraw.Draw(im).textlength(t, font=f)
    glow_text(im, ((w - tw) / 2, 372), t, f, (170, 255, 200, 255), (60, 255, 140, 220))
    im.save(os.path.join(TEX, "Label_Sc09StandHere.png"))
    print("stand here")


def puddle():
    """Irregular spilled-liquid blob (white, alpha) and a radial splatter of drops; tinted purple by the material."""
    import random
    rnd = random.Random(9)
    n = 512
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    for _ in range(26):
        a = rnd.random() * 6.283
        r = rnd.random() * 120
        cx = n / 2 + 120 * (r / 120) ** 0.7 * __import__("math").cos(a)
        cy = n / 2 + 90 * (r / 120) ** 0.7 * __import__("math").sin(a)
        rad = rnd.randint(40, 95)
        d.ellipse((cx - rad, cy - rad * 0.8, cx + rad, cy + rad * 0.8), fill=255)
    # tendrils
    for _ in range(7):
        a = rnd.random() * 6.283
        m = __import__("math")
        x0, y0 = n / 2 + 70 * m.cos(a), n / 2 + 55 * m.sin(a)
        for k in range(1, 5):
            rad = max(5, 26 - k * 5)
            d.ellipse((x0 + k * 26 * m.cos(a) - rad, y0 + k * 26 * m.sin(a) - rad, x0 + k * 26 * m.cos(a) + rad, y0 + k * 26 * m.sin(a) + rad), fill=255)
    im = im.filter(ImageFilter.GaussianBlur(9)).point(lambda v: 255 if v > 150 else int(v * 255 / 150))
    im = im.filter(ImageFilter.GaussianBlur(3))
    rgba = Image.merge("RGBA", (Image.new("L", (n, n), 255),) * 3 + (im,))
    rgba.save(os.path.join(TEX, "Label_Sc09Puddle.png"))
    sp = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(sp)
    m = __import__("math")
    for _ in range(170):
        a = rnd.random() * 6.283
        r = (rnd.random() ** 1.6) * 235
        rad = max(1.5, 14 * (1 - r / 260) * rnd.random() + 1.5)
        x, y = n / 2 + r * m.cos(a), n / 2 + r * m.sin(a)
        d.ellipse((x - rad * 1.4, y - rad, x + rad * 1.4, y + rad), fill=255)
    sp = sp.filter(ImageFilter.GaussianBlur(1.4)).point(lambda v: min(255, int(v * 1.6)))
    Image.merge("RGBA", (Image.new("L", (n, n), 255),) * 3 + (sp,)).save(os.path.join(TEX, "Label_Sc09Splat.png"))
    print("puddle + splat")


def signs():
    """Green bilingual wall signs like the designer picture: eyewash / safety shower (Bangla over English)."""
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import render_bangla_text as rbt
    specs = [("Label_Sc09SignEyewash", "আইওয়াশ", "Eyewash"), ("Label_Sc09SignShower", "সেফটি শাওয়ার", "Safety Shower")]
    for name, bn, en in specs:
        rbt.render("_tmp_sign", bn, 150 if len(bn) < 10 else 104, True, 960)
        bn_im = Image.open(os.path.join(rbt.OUT_DIR, "_tmp_sign.png")).convert("RGBA")
        os.remove(os.path.join(rbt.OUT_DIR, "_tmp_sign.png"))
        w, h = 1024, 512
        im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rounded_rectangle((4, 4, w - 5, h - 5), radius=26, fill=(14, 108, 60, 255))
        d.rounded_rectangle((18, 18, w - 19, h - 19), radius=18, outline=(235, 245, 238, 255), width=7)
        s = min(1.0, (w - 140) / bn_im.width, 190 / bn_im.height)
        bn_im = bn_im.resize((int(bn_im.width * s), int(bn_im.height * s)), Image.LANCZOS)
        im.alpha_composite(bn_im, ((w - bn_im.width) // 2, 70))
        f = font(104)
        tw = d.textlength(en, font=f)
        d.text(((w - tw) / 2, 290), en, font=f, fill=(255, 255, 255, 255))
        im.save(os.path.join(TEX, name + ".png"))
    print("signs")


if __name__ == "__main__":
    signs()
    puddle()
    sds()
    labels()
    holo()
    stand_here()
