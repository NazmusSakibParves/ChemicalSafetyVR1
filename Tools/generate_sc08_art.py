"""Scenario 8 art: designer pack (SDS, wall poster, labels) -> project, plus generated signs.
Run: python -I Tools/generate_sc08_art.py
Decal-style textures go to 2D/Sc06Tex (Label_ / Poster_ names are imported as clamped decals), the SDS sheet to 2D/Sc08.
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

PACK = r"C:\Users\msi\Downloads\Scene08 workflow-20261026T152746Z-1-001\Scene08 workflow\2D resources"
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Presentable", "2D")
TEX = os.path.join(ROOT, "Sc06Tex")
SC8 = os.path.join(ROOT, "Sc08")
os.makedirs(SC8, exist_ok=True)

FONT_B = r"C:\Windows\Fonts\arialbd.ttf"


def font(size):
    return ImageFont.truetype(FONT_B, size)


def fit(im, maxd):
    s = maxd / max(im.size)
    if s < 1:
        im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
    return im


def cut_white_edges(im):
    """The label PNGs have a faint white fringe: make near-white pixels at the border transparent."""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 40:
                px[x, y] = (0, 0, 0, 0)
    return im


# ---- designer files
sds = Image.open(os.path.join(PACK, "SDS Sheet.png")).convert("RGBA")
fit(sds, 1024).save(os.path.join(SC8, "SDS_SC08.png"))
poster = Image.open(os.path.join(PACK, "Wall poster.png")).convert("RGBA")
fit(poster, 1024).save(os.path.join(TEX, "Label_Sc08Rules.png"))
fit(cut_white_edges(Image.open(os.path.join(PACK, "Acid.png"))), 512).save(os.path.join(TEX, "Label_Sc08Acid.png"))
fit(cut_white_edges(Image.open(os.path.join(PACK, "Water.png"))), 512).save(os.path.join(TEX, "Label_Sc08Water.png"))


def sign(name, size, bg, fg, lines, icon=None, border=None, radius=18):
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=bg)
    if border:
        d.rounded_rectangle((8, 8, size[0] - 9, size[1] - 9), radius=radius - 6, outline=border, width=4)
    return im, d


# ---- PPE station title board (white, black text)
im = Image.new("RGBA", (1024, 160), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
d.rounded_rectangle((0, 0, 1023, 159), radius=10, fill=(244, 244, 242, 255), outline=(150, 152, 150, 255), width=4)
f = font(86)
t = "PPE Donning Station"
tw = d.textlength(t, font=f)
d.text(((1024 - tw) / 2, 28), t, font=f, fill=(25, 25, 28, 255))
im.save(os.path.join(TEX, "Label_Sc08PpeTitle.png"))

# ---- slot captions (small black text on transparent) for the board: goggles, respirator, gloves, apron
caps = ["GOGGLES / FACE SHIELD", "RESPIRATOR", "NEOPRENE GLOVES", "ACID-RESISTANT APRON"]
for i, c in enumerate(caps):
    im = Image.new("RGBA", (768, 96), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    f = font(58 if len(c) < 18 else 44)
    tw = d.textlength(c, font=f)
    d.text(((768 - tw) / 2, 16), c, font=f, fill=(35, 38, 40, 255))
    im.save(os.path.join(TEX, "Label_Sc08Cap%d.png" % i))

# ---- green safety sign: SAFETY SHOWER / EYE WASH (ISO style, pictogram drawn simply)
def green_sign(name, text, pict):
    im = Image.new("RGBA", (400, 520), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, 399, 519), radius=22, fill=(16, 122, 66, 255), outline=(235, 245, 235, 255), width=8)
    d.rounded_rectangle((26, 26, 373, 376), radius=14, outline=(255, 255, 255, 255), width=6)
    cx = 200
    if pict == "shower":
        d.ellipse((cx - 22, 70, cx + 22, 114), fill="white")  # head
        d.polygon([(cx - 40, 130), (cx + 40, 130), (cx + 52, 300), (cx - 52, 300)], fill="white")
        d.arc((cx - 100, 60, cx + 100, 120), 180, 360, fill="white", width=10)
        for k in range(-3, 4):
            d.line((cx + k * 22, 330, cx + k * 22 - 4, 360), fill="white", width=6)
    else:
        d.polygon([(cx - 100, 200), (cx - 40, 150), (cx + 40, 150), (cx + 100, 200), (cx + 40, 280), (cx - 40, 280)], fill="white")
        d.ellipse((cx - 30, 180, cx + 30, 250), fill=(16, 122, 66, 255))
        d.ellipse((cx - 14, 196, cx + 14, 232), fill="white")
        for k in (-60, 0, 60):
            d.line((cx + k, 296, cx + k, 360), fill="white", width=6)
    f = font(50)
    y = 392
    for ln in text:
        tw = d.textlength(ln, font=f)
        d.text(((400 - tw) / 2, y), ln, font=f, fill="white")
        y += 58
    im.save(os.path.join(TEX, name + ".png"))


green_sign("Label_Sc08ShowerSign", ["SAFETY", "SHOWER"], "shower")
green_sign("Label_Sc08EyewashSign", ["EYE", "WASH"], "eye")

# ---- notebook page on the table (lined paper with scribbles)
im = Image.new("RGBA", (512, 640), (246, 246, 242, 255))
d = ImageDraw.Draw(im)
for y in range(70, 620, 34):
    d.line((24, y, 488, y), fill=(160, 190, 225, 255), width=2)
d.line((70, 0, 70, 640), fill=(225, 120, 120, 255), width=3)
import random
random.seed(8)
for y in range(70, 560, 34):
    x = 84
    while x < 450:
        w = random.randint(14, 60)
        d.line((x, y - 8 + random.randint(-3, 3), x + w, y - 8 + random.randint(-3, 3)), fill=(40, 50, 90, 255), width=2)
        x += w + 10
im.save(os.path.join(TEX, "Label_Sc08Notebook.png"))

# ---- vapor sprite for acid fumes is the SC06 Fx_Vapor/Fx_Wisp; nothing to add


import numpy as np

# ---- apron PVC-coated fabric: albedo (fine weave + faint mottling) and normal (weave), tile 512
N = 512
rng = np.random.RandomState(5)
yy, xx = np.mgrid[0:N, 0:N]
weave = (np.sin(xx / N * 2 * np.pi * 64) * np.sin(yy / N * 2 * np.pi * 64))
mott = rng.rand(N // 16, N // 16)
mott = np.array(Image.fromarray((mott * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)) / 255.0
base = np.array([150, 156, 158], dtype=np.float32)
alb = np.zeros((N, N, 3), dtype=np.float32)
for c in range(3):
    alb[..., c] = base[c] * (0.94 + 0.05 * mott + 0.012 * weave)
Image.fromarray(np.clip(alb, 0, 255).astype(np.uint8), "RGB").save(os.path.join(TEX, "Apron_Albedo.png"))
# ---- venetian blinds seen from inside (slats, daylight gaps, cord), 512x640, alpha
W, H = 512, 640
im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
n = 40
sh = H / n
for i in range(n):
    y0 = int(i * sh)
    d.rectangle((0, y0, W, y0 + int(sh * 0.72)), fill=(226, 226, 222, 255))
    d.rectangle((0, y0 + int(sh * 0.72), W, y0 + int(sh * 0.86)), fill=(150, 150, 146, 255))
    d.rectangle((0, y0 + int(sh * 0.86), W, y0 + int(sh)), fill=(205, 222, 235, 70))
d.line((W - 36, 0, W - 36, H), fill=(120, 120, 118, 255), width=3)
d.rectangle((W - 44, H - 34, W - 28, H - 8), fill=(110, 110, 108, 255))
im.save(os.path.join(TEX, "Label_Sc08Blinds.png"))

# ---- floor stains / scuffs (soft dark blotches with alpha)
im = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
px = np.zeros((512, 512), dtype=np.float32)
for _ in range(14):
    cx, cy = rng.randint(40, 470, 2)
    r = rng.randint(20, 70)
    g = np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / (2.0 * (r * 0.6) ** 2)))
    px = np.maximum(px, g * rng.uniform(0.25, 0.6))
a = (px * 150).astype(np.uint8)
arr = np.zeros((512, 512, 4), dtype=np.uint8)
arr[..., 0] = 40; arr[..., 1] = 38; arr[..., 2] = 34; arr[..., 3] = a
Image.fromarray(arr, "RGBA").save(os.path.join(TEX, "Label_Sc08Stain.png"))

# ---- small wall signs: first aid, fire alarm pull station label, exit
def plate(name, size, bg, draw):
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=14, fill=bg)
    draw(d, size)
    im.save(os.path.join(TEX, name + ".png"))


def firstaid(d, size):
    cx, cy = size[0] // 2, size[1] // 2 - 20
    d.rectangle((cx - 28, cy - 90, cx + 28, cy + 90), fill="white")
    d.rectangle((cx - 90, cy - 28, cx + 90, cy + 28), fill="white")
    f = font(46)
    t = "FIRST AID"
    d.text(((size[0] - d.textlength(t, font=f)) / 2, size[1] - 70), t, font=f, fill="white")


plate("Label_Sc08FirstAid", (360, 360), (20, 130, 70, 255), firstaid)


def exitsign(d, size):
    f = font(120)
    t = "EXIT"
    d.text(((size[0] - d.textlength(t, font=f)) / 2, 20), t, font=f, fill="white")
    d.polygon([(40, 160), (110, 130), (110, 190)], fill="white")


plate("Label_Sc08Exit", (512, 220), (14, 140, 64, 255), exitsign)


def alarm(d, size):
    d.rectangle((20, 20, size[0] - 20, size[1] - 20), fill=(190, 30, 30, 255))
    f = font(34)
    t = "FIRE ALARM"
    d.text(((size[0] - d.textlength(t, font=f)) / 2, 60), t, font=f, fill="white")
    d.rectangle((60, 120, size[0] - 60, size[1] - 70), fill=(230, 230, 230, 255))
    f2 = font(26)
    t2 = "PULL DOWN"
    d.text(((size[0] - d.textlength(t2, font=f2)) / 2, size[1] - 58), t2, font=f2, fill="white")


plate("Label_Sc08Alarm", (240, 320), (150, 20, 20, 255), alarm)
print("SC08 art written")
