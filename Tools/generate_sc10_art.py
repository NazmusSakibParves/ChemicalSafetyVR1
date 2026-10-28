"""Scenario 10 art (acid spill response): procedural floor tile, concrete wall, spill-kit bag labels, sign, pads, puddle.
Run: python -I Tools/generate_sc10_art.py
Everything is written to Assets/Art/Presentable/2D/Sc06Tex (Label_ names are imported as clamped decals / textures).
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Presentable", "2D")
TEX = os.path.join(ROOT, "Sc06Tex")
os.makedirs(TEX, exist_ok=True)
FONT_B = r"C:\Windows\Fonts\arialbd.ttf"
FONT_BLACK = r"C:\Windows\Fonts\ariblk.ttf"
FONT_R = r"C:\Windows\Fonts\arial.ttf"


def font(size, bold=True, black=False):
    return ImageFont.truetype(FONT_BLACK if black else (FONT_B if bold else FONT_R), size)


def save(im, name):
    path = os.path.join(TEX, name)
    im.save(path, optimize=True)
    print(name, im.size, im.mode, os.path.getsize(path) // 1024, "KB")


# ---------------------------------------------------------------- noise helpers
def pnoise(w, h, power=1.5, seed=0):
    """Periodic (tileable) 1/f^power noise, normalised to 0..1."""
    rng = np.random.RandomState(seed)
    f = np.fft.fft2(rng.randn(h, w))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1
    f = f / (r ** power)
    f[0, 0] = 0
    n = np.real(np.fft.ifft2(f))
    n -= n.min()
    return n / max(n.max(), 1e-9)


def to_img(a, mode="RGB"):
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), mode)


def center_text(d, cx, cy, text, f, fill, spacing=0):
    widths = [d.textlength(c, font=f) for c in text]
    total = sum(widths) + spacing * (len(text) - 1)
    x = cx - total / 2
    bb = d.textbbox((0, 0), "H", font=f)
    y = cy - (bb[1] + bb[3]) / 2
    for c, w in zip(text, widths):
        d.text((x, y), c, font=f, fill=fill)
        x += w + spacing


def fit_font(d, text, max_w, start, bold=True, black=False, spacing=0):
    s = start
    while s > 6:
        f = font(s, bold, black)
        w = sum(d.textlength(c, font=f) for c in text) + spacing * (len(text) - 1)
        if w <= max_w:
            return f
        s -= 2
    return font(6, bold, black)


# ---------------------------------------------------------------- icons (drawn supersampled, returned RGBA)
def biohazard(size, color=(0, 0, 0, 255)):
    S = size * 4
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = S / 2
    for k in range(3):
        a = math.radians(90 + 120 * k)
        px, py = c + 0.24 * S * math.cos(a), c - 0.27 * S * math.sin(a)
        r = 0.25 * S
        w = int(0.085 * S)
        out_deg = -math.degrees(a)  # PIL angles run clockwise from 3 o'clock
        d.arc((px - r, py - r, px + r, py + r), out_deg + 40, out_deg + 320, fill=color, width=w)
        # spoke from the centre ring to the lobe
        d.line((c, c, c + 0.17 * S * math.cos(a), c - 0.17 * S * math.sin(a)), fill=color, width=int(0.05 * S))

    d.ellipse((c - 0.14 * S, c - 0.14 * S, c + 0.14 * S, c + 0.14 * S), outline=color, width=int(0.045 * S))
    d.ellipse((c - 0.055 * S, c - 0.055 * S, c + 0.055 * S, c + 0.055 * S), fill=color)
    return im.resize((size, size), Image.LANCZOS)


def corrosive_diamond(size):
    """White diamond, red border, tubes dripping on a hand (GHS corrosion style)."""
    S = size * 4
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = S / 2
    pts = [(c, 0.02 * S), (0.98 * S, c), (c, 0.98 * S), (0.02 * S, c)]
    d.polygon(pts, fill=(200, 25, 30, 255))
    inner = [(c, 0.09 * S), (0.91 * S, c), (c, 0.91 * S), (0.09 * S, c)]
    d.polygon(inner, fill=(255, 255, 255, 255))
    k = (20, 20, 20, 255)
    # two tilted tubes at the top
    for ox, ang in ((-0.10, 28), (0.10, -28)):
        t = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        td = ImageDraw.Draw(t)
        td.rounded_rectangle((c + ox * S - 0.04 * S, 0.22 * S, c + ox * S + 0.04 * S, 0.42 * S), radius=int(0.03 * S), fill=k)
        t = t.rotate(ang, center=(c + ox * S, 0.3 * S))
        im.alpha_composite(t)
    d = ImageDraw.Draw(im)
    # drops
    for x, y in ((0.40, 0.50), (0.60, 0.50), (0.45, 0.56), (0.57, 0.57)):
        d.ellipse((x * S - 0.018 * S, y * S - 0.018 * S, x * S + 0.018 * S, y * S + 0.026 * S), fill=k)
    # hand
    d.rounded_rectangle((0.26 * S, 0.66 * S, 0.50 * S, 0.74 * S), radius=int(0.03 * S), fill=k)
    for i in range(4):
        x = 0.27 + i * 0.058
        d.rounded_rectangle((x * S, 0.56 * S, (x + 0.04) * S, 0.68 * S), radius=int(0.02 * S), fill=k)
    # bar with a corroded notch
    d.rounded_rectangle((0.54 * S, 0.66 * S, 0.76 * S, 0.74 * S), radius=int(0.02 * S), fill=k)
    d.polygon([(0.62 * S, 0.74 * S), (0.66 * S, 0.74 * S), (0.64 * S, 0.70 * S)], fill=(255, 255, 255, 255))
    return im.resize((size, size), Image.LANCZOS)


def slip_icon(size, color=(15, 15, 15, 255)):
    """Slipping person in a triangle outline (wet-floor sign)."""
    S = size * 4
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    tri = [(0.5 * S, 0.04 * S), (0.97 * S, 0.92 * S), (0.03 * S, 0.92 * S)]
    d.line(tri + [tri[0]], fill=color, width=int(0.07 * S), joint="curve")
    w = int(0.055 * S)
    d.ellipse((0.40 * S, 0.34 * S, 0.50 * S, 0.44 * S), fill=color)  # head
    d.line((0.52 * S, 0.46 * S, 0.44 * S, 0.62 * S), fill=color, width=w)  # torso leaning back
    d.line((0.50 * S, 0.50 * S, 0.66 * S, 0.44 * S), fill=color, width=int(w * 0.8))  # arm up
    d.line((0.49 * S, 0.52 * S, 0.30 * S, 0.54 * S), fill=color, width=int(w * 0.8))  # arm back
    d.line((0.44 * S, 0.62 * S, 0.60 * S, 0.70 * S), fill=color, width=w)  # leg forward
    d.line((0.60 * S, 0.70 * S, 0.66 * S, 0.78 * S), fill=color, width=w)
    d.line((0.44 * S, 0.62 * S, 0.33 * S, 0.76 * S), fill=color, width=w)  # leg back
    d.line((0.33 * S, 0.76 * S, 0.24 * S, 0.74 * S), fill=color, width=w)
    pts = [(0.14 * S + i * 0.012 * S, 0.84 * S + math.sin(i / 3.0) * 0.012 * S) for i in range(0, 60)]
    d.line(pts, fill=color, width=int(0.035 * S))
    return im.resize((size, size), Image.LANCZOS)


# ---------------------------------------------------------------- floor tile / concrete
def tile():
    N, T = 1024, 512
    rng = random.Random(10)
    yy, xx = np.mgrid[0:N, 0:N]
    u, v = xx % T, yy % T
    dg = np.minimum(np.minimum(u, T - 1 - u), np.minimum(v, T - 1 - v)).astype(np.float32)  # distance to grout centre-line
    base = np.full((N, N), 222.0, np.float32)
    for ty in range(2):
        for tx in range(2):
            base[ty * T:(ty + 1) * T, tx * T:(tx + 1) * T] += rng.uniform(-4, 3)
    sheen = pnoise(N, N, 2.2, 1)
    base += (sheen - 0.5) * 14
    base += (pnoise(N, N, 0.6, 2) - 0.5) * 3  # fine grain
    # slight bevel: brighter just inside the grout, grout dark
    bevel = np.clip(1 - (dg - 4) / 10, 0, 1) * (dg >= 4)
    base += bevel * 6
    grout = np.clip((5.5 - dg) / 2.0, 0, 1)
    gcol = 112 + (pnoise(N, N, 1.0, 3) - 0.5) * 20
    base = base * (1 - grout) + gcol * grout
    rgb = np.stack([base, base + 1.0, base + 3.0], -1)
    im = to_img(rgb)
    # scuffs and dirt, drawn at all wrap offsets so the texture stays seamless
    layer = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(70):
        x, y = rng.uniform(0, N), rng.uniform(0, N)
        a = rng.uniform(0, math.pi)
        L = rng.uniform(14, 60)
        col = (95, 90, 85, rng.randint(18, 50)) if rng.random() < 0.7 else (255, 255, 255, rng.randint(30, 70))
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d.line((x + ox, y + oy, x + ox + math.cos(a) * L, y + oy + math.sin(a) * L), fill=col, width=rng.choice((1, 1, 2)))
    for _ in range(260):
        x, y, r = rng.uniform(0, N), rng.uniform(0, N), rng.uniform(1, 3)
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d.ellipse((x + ox - r, y + oy - r, x + ox + r, y + oy + r), fill=(80, 76, 70, rng.randint(25, 70)))
    layer = layer.filter(ImageFilter.GaussianBlur(0.8))
    im = im.convert("RGBA")
    im.alpha_composite(layer)
    # dirt collected along grout
    dirt = (pnoise(N, N, 1.4, 4) > 0.62) * np.clip((14 - dg) / 14, 0, 1)
    dl = Image.fromarray((dirt * 70).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2))
    dark = Image.new("RGBA", (N, N), (70, 66, 60, 255))
    dark.putalpha(dl)
    im.alpha_composite(dark)
    save(im.convert("RGB"), "Label_Sc10Tile.png")


def concrete():
    N = 1024
    rng = random.Random(11)
    n = pnoise(N, N, 1.9, 5) * 0.55 + pnoise(N, N, 1.1, 6) * 0.3 + pnoise(N, N, 0.5, 7) * 0.15
    g = 150 + (n - 0.5) * 38
    stain = pnoise(N, N, 2.6, 8)
    g -= np.clip((stain - 0.66) * 60, 0, 9)
    rgb = np.stack([g, g * 1.0, g * 0.985], -1)
    im = to_img(rgb).convert("RGBA")
    layer = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(260):
        x, y, r = rng.uniform(0, N), rng.uniform(0, N), rng.uniform(0.8, 2.2)
        a = rng.randint(30, 80)
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d.ellipse((x + ox - r, y + oy - r, x + ox + r, y + oy + r), fill=(50, 50, 50, a))
    layer = layer.filter(ImageFilter.GaussianBlur(0.7))
    im.alpha_composite(layer)
    save(im.convert("RGB"), "Label_Sc10Concrete.png")


# ---------------------------------------------------------------- labels
def title():
    im = Image.new("RGBA", (1024, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    f = fit_font(d, "SPILL KIT", 960, 110, black=False, spacing=34)
    center_text(d, 512, 64, "SPILL KIT", f, (255, 255, 255, 255), spacing=34)
    save(im, "Label_Sc10SpillKitTitle.png")


def paper_base(w, h, seed, tone=(246, 246, 244)):
    n = pnoise(w, h, 1.3, seed)
    a = np.stack([np.full((h, w), t, np.float32) for t in tone], -1) + ((n - 0.5) * 8)[..., None]
    return to_img(a).convert("RGBA")


def bag_crimp(im, h_top=46, h_bot=46):
    w, h = im.size
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(sh)
    for y in range(h_top):
        d.line((0, y, w, y), fill=(0, 0, 0, int(70 * (1 - y / h_top) ** 1.4)))
    for y in range(h_bot):
        d.line((0, h - 1 - y, w, h - 1 - y), fill=(0, 0, 0, int(60 * (1 - y / h_bot) ** 1.4)))
    for x in range(0, w, 7):  # crimp ridges at the top and bottom edges
        d.line((x, 0, x, h_top * 0.7), fill=(0, 0, 0, 38))
        d.line((x, h - h_bot * 0.7, x, h), fill=(0, 0, 0, 34))
    # soft puffy side shading
    for x in range(40):
        a = int(46 * (1 - x / 40) ** 1.8)
        d.line((x, 0, x, h), fill=(0, 0, 0, a))
        d.line((w - 1 - x, 0, w - 1 - x, h), fill=(0, 0, 0, a))
    im.alpha_composite(sh)
    return im


def soda_ash():
    w, h = 512, 768
    im = paper_base(w, h, 20)
    d = ImageDraw.Draw(im)
    ink = (28, 30, 36, 255)
    d.rectangle((44, 90, w - 44, h - 90), outline=ink, width=5)
    d.rectangle((58, 104, w - 58, h - 104), outline=(120, 124, 132, 255), width=2)
    f = fit_font(d, "SODA", 380, 190, black=True, spacing=6)
    center_text(d, w // 2, 300, "SODA", f, ink, spacing=6)
    f2 = fit_font(d, "ASH", 380, 190, black=True, spacing=6)
    center_text(d, w // 2, 480, "ASH", f2, ink, spacing=6)
    d.line((110, 585, w - 110, 585), fill=ink, width=3)
    center_text(d, w // 2, 625, "SODIUM CARBONATE", font(26), (70, 74, 82, 255), spacing=3)
    bag_crimp(im)
    save(im.convert("RGB"), "Label_Sc10SodaAsh.png")


def vermiculite():
    w, h = 512, 768
    rng = np.random.RandomState(21)
    base = np.zeros((h, w, 3), np.float32) + np.array([196, 170, 124], np.float32)
    n = pnoise(w, h, 0.5, 22)
    base += ((n - 0.5) * 70)[..., None] * np.array([1, 0.9, 0.7])
    im = to_img(base).convert("RGBA")
    d = ImageDraw.Draw(im)
    rnd = random.Random(21)
    for _ in range(5200):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h)
        r = rnd.uniform(1.2, 4.2)
        t = rnd.random()
        col = (232, 214, 170, 255) if t < 0.35 else ((122, 94, 58, 255) if t < 0.7 else (168, 140, 96, 255))
        d.ellipse((x - r, y - r * 0.7, x + r, y + r * 0.7), fill=col)
    # label band behind the word for legibility
    band = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(band).rounded_rectangle((36, 70, w - 36, 190), radius=16, fill=(244, 235, 212, 205))
    im.alpha_composite(band)
    d = ImageDraw.Draw(im)
    brown = (110, 40, 28, 255)
    f = fit_font(d, "VERMICULITE", 400, 70, black=False, spacing=4)
    center_text(d, w // 2, 120, "VERMICULITE", f, brown, spacing=4)
    f2 = font(26, False)
    center_text(d, w // 2, 166, "ABSORBENT GRANULES", f2, (120, 70, 50, 255), spacing=3)
    bag_crimp(im, 44, 44)
    save(im.convert("RGB"), "Label_Sc10Vermiculite.png")


def waste_bag():
    w, h = 768, 1024
    n = pnoise(w, h, 1.7, 30) * 0.6 + pnoise(w, h, 0.9, 31) * 0.4
    base = np.zeros((h, w, 3), np.float32) + np.array([240, 196, 24], np.float32)
    base += ((n - 0.5) * 46)[..., None] * np.array([1, 0.9, 0.2])
    # plastic wrinkles
    wr = pnoise(w, h, 2.4, 32)
    base *= (0.94 + 0.12 * wr)[..., None]
    im = to_img(base).convert("RGBA")
    d = ImageDraw.Draw(im)
    k = (24, 22, 20, 255)
    red = (200, 28, 32, 255)
    # white label panel
    L, T, R, B = 90, 250, w - 90, 930
    d.rounded_rectangle((L, T, R, B), radius=14, fill=(250, 246, 232, 255), outline=k, width=6)
    # red banner
    d.rectangle((L + 14, T + 14, R - 14, T + 124), fill=red)
    f = fit_font(d, "SPILL KIT WASTE", R - L - 70, 74, black=True, spacing=2)
    center_text(d, w // 2, T + 62, "SPILL KIT WASTE", f, (255, 255, 255, 255), spacing=2)
    f = fit_font(d, "CONTAINS SOCKS, PADS, SODA ASH", R - L - 60, 34, spacing=1)
    center_text(d, w // 2, T + 155, "CONTAINS SOCKS, PADS, SODA ASH", f, k, spacing=1)
    # hazard diamond (left) and biohazard (right)
    im.alpha_composite(corrosive_diamond(230), (L + 36, T + 215))
    im.alpha_composite(biohazard(230), (R - 36 - 230, T + 215))
    d = ImageDraw.Draw(im)
    d.line((L + 24, T + 470, R - 24, T + 470), fill=(200, 28, 32, 255), width=3)
    f = fit_font(d, "HAZARDOUS CHEMICAL WASTE", R - L - 60, 44, spacing=1)
    center_text(d, w // 2, T + 520, "HAZARDOUS CHEMICAL WASTE", f, k, spacing=1)
    f = fit_font(d, "(GLASS/DEBRIS) (CONTAMINATED)", R - L - 70, 36, spacing=1)
    center_text(d, w // 2, T + 575, "(GLASS/DEBRIS) (CONTAMINATED)", f, k, spacing=1)
    # faint brand text above the label
    f = font(96, black=True)
    center_text(d, w // 2, 150, "SPILL", f, (150, 24, 24, 150), spacing=8)
    # specular streaks on the plastic
    sp = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sp)
    for x0, a in ((60, 60), (700, 40), (400, 22)):
        sd.polygon([(x0, 30), (x0 + 26, 30), (x0 + 60, h - 40), (x0 + 20, h - 40)], fill=(255, 255, 255, a))
    sp = sp.filter(ImageFilter.GaussianBlur(14))
    im.alpha_composite(sp)
    save(im.convert("RGB"), "Label_Sc10WasteBag.png")


def bin_label():
    w, h = 512, 640
    im = paper_base(w, h, 40, (252, 246, 214))
    d = ImageDraw.Draw(im)
    red = (200, 28, 32, 255)
    k = (22, 22, 22, 255)
    d.rectangle((0, 0, w - 1, h - 1), outline=k, width=8)
    d.rectangle((8, 8, w - 9, 190), fill=red)
    f = fit_font(d, "HAZARDOUS", w - 80, 92, black=True, spacing=2)
    center_text(d, w // 2, 72, "HAZARDOUS", f, (255, 255, 255, 255), spacing=2)
    f = fit_font(d, "WASTE", w - 80, 92, black=True, spacing=8)
    center_text(d, w // 2, 148, "WASTE", f, (255, 255, 255, 255), spacing=8)
    im.alpha_composite(biohazard(280), (w // 2 - 140, 215))
    d = ImageDraw.Draw(im)
    f = fit_font(d, "GLASS/DEBRIS", w - 70, 64, black=True, spacing=2)
    center_text(d, w // 2, 548, "GLASS/DEBRIS", f, k, spacing=2)
    d.line((60, 600, w - 60, 600), fill=red, width=5)
    save(im.convert("RGB"), "Label_Sc10BinLabel.png")


def haz_tag():
    w, h = 384, 512
    n = pnoise(w, h, 1.4, 50)
    base = np.zeros((h, w, 3), np.float32) + np.array([248, 214, 40], np.float32)
    base += ((n - 0.5) * 18)[..., None]
    im = to_img(base).convert("RGBA")
    d = ImageDraw.Draw(im)
    k = (24, 22, 20, 255)
    d.rectangle((6, 6, w - 7, h - 7), outline=k, width=7)
    d.ellipse((w // 2 - 20, 24, w // 2 + 20, 64), fill=(60, 60, 60, 255))
    d.ellipse((w // 2 - 11, 33, w // 2 + 11, 55), fill=(230, 200, 40, 255))
    im.alpha_composite(biohazard(210), (w // 2 - 105, 90))
    d = ImageDraw.Draw(im)
    f = fit_font(d, "HAZARDOUS", w - 60, 62, black=True, spacing=1)
    center_text(d, w // 2, 350, "HAZARDOUS", f, k, spacing=1)
    f = fit_font(d, "WASTE", w - 60, 62, black=True, spacing=6)
    center_text(d, w // 2, 418, "WASTE", f, k, spacing=6)
    d.line((50, 462, w - 50, 462), fill=k, width=3)
    save(im.convert("RGB"), "Label_Sc10HazTag.png")


def caution():
    w, h = 512, 768
    n = pnoise(w, h, 1.5, 60)
    base = np.zeros((h, w, 3), np.float32) + np.array([250, 205, 16], np.float32)
    base += ((n - 0.5) * 16)[..., None] * np.array([1, 1, 0.3])
    im = to_img(base).convert("RGBA")
    d = ImageDraw.Draw(im)
    k = (18, 18, 16, 255)
    d.rectangle((30, 34, w - 30, 190), fill=k)
    f = fit_font(d, "CAUTION", w - 110, 124, black=True, spacing=2)
    center_text(d, w // 2, 112, "CAUTION", f, (250, 205, 16, 255), spacing=2)
    im.alpha_composite(slip_icon(300), (w // 2 - 150, 215))
    d = ImageDraw.Draw(im)
    f = fit_font(d, "- SPILL", w - 100, 130, black=True, spacing=3)
    center_text(d, w // 2, 640, "- SPILL", f, k, spacing=3)
    # moulded plastic edge shading
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sh)
    for i in range(18):
        a = int(70 * (1 - i / 18) ** 2)
        sd.rectangle((i, i, w - 1 - i, h - 1 - i), outline=(60, 40, 0, a))
    im.alpha_composite(sh)
    save(im.convert("RGB"), "Label_Sc10Caution.png")


# ---------------------------------------------------------------- pad / sock
def pad():
    N, P = 512, 64
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    u = (xx % P) / P - 0.5
    v = (yy % P) / P - 0.5
    dia = np.abs(u) + np.abs(v)  # 0 in the cell centre, 0.5 at the quilt seam
    # diamond cells offset by half a period too: seams at dia ~ 0.5 and a dimple at each seam crossing
    puff = 1 - np.clip(dia / 0.5, 0, 1) ** 2.2
    seam = np.clip((dia - 0.43) / 0.07, 0, 1)
    dimple = np.exp(-(u * u + v * v) / 0.0006) + np.exp(-(((np.abs(u) - 0.5) ** 2) + ((np.abs(v) - 0.5) ** 2)) / 0.0006)
    fib = pnoise(N, N, 0.35, 70)
    fib2 = pnoise(N, N, 1.4, 71)
    g = 118 + puff * 20 - seam * 26 - dimple * 14 + (fib - 0.5) * 38 + (fib2 - 0.5) * 18
    rgb = np.stack([g, g + 1, g + 3], -1)
    save(to_img(rgb), "Label_Sc10Pad.png")


def sock():
    W, H = 512, 256
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    P = 64
    ring = (xx % P) / P
    band = np.exp(-((ring - 0.5) ** 2) / 0.006)  # lighter stitched ring
    seam = np.exp(-((ring - 0.5) ** 2) / 0.00025) * 0.0
    stitch = (((yy % 14) < 7) & (np.abs(ring - 0.5) < 0.012)).astype(np.float32)
    fib = pnoise(W, H, 0.4, 80)
    fib2 = pnoise(W, H, 1.5, 81)
    # fibres run along the tube circumference (vertical here): stretch noise
    streak = pnoise(W, H, 0.9, 82)
    tube = 1 - 0.28 * (np.abs(yy / H - 0.5) * 2) ** 3  # shading toward the rim of the unwrapped tube
    g = (122 + band * 20 + stitch * 28 + (fib - 0.5) * 40 + (fib2 - 0.5) * 18 + (streak - 0.5) * 14) * tube
    rgb = np.stack([g, g + 1, g + 3], -1)
    save(to_img(rgb), "Label_Sc10Sock.png")


# ---------------------------------------------------------------- decals
def blob_mask(N, seed, lobes, thresh=0.42, warp=0.25):
    rnd = random.Random(seed)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    f = np.zeros((N, N), np.float32)
    for cx, cy, rx, ry, ang in lobes:
        c, s = math.cos(ang), math.sin(ang)
        dx, dy = xx - cx * N, yy - cy * N
        a = (dx * c + dy * s) / (rx * N)
        b = (-dx * s + dy * c) / (ry * N)
        f += np.exp(-(a * a + b * b) * 1.6)
    f += (pnoise(N, N, 2.0, seed) - 0.5) * warp
    f += (pnoise(N, N, 1.0, seed + 1) - 0.5) * 0.08
    return f > thresh


def puddle():
    N = 1024
    lobes = [
        (0.20, 0.52, 0.13, 0.17, 0.0),
        (0.33, 0.55, 0.12, 0.12, 0.2),
        (0.46, 0.50, 0.12, 0.075, -0.2),
        (0.58, 0.46, 0.11, 0.06, -0.1),
        (0.70, 0.50, 0.11, 0.055, 0.15),
        (0.82, 0.46, 0.10, 0.05, -0.1),
        (0.93, 0.48, 0.08, 0.045, 0.05),
        (0.12, 0.36, 0.05, 0.04, 0.0),
    ]
    m = blob_mask(N, 91, lobes, 0.42, 0.5)
    mask = Image.fromarray((m * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.2))
    mask = mask.point(lambda p: 255 if p > 128 else 0).filter(ImageFilter.GaussianBlur(1.3))
    eroded = mask.filter(ImageFilter.MinFilter(11)).filter(ImageFilter.GaussianBlur(2))
    rim = ImageChops.subtract(mask, eroded)
    ma = np.asarray(mask, np.float32) / 255
    ra = np.asarray(rim, np.float32) / 255
    # a subtle darker inner edge band gives the liquid depth
    inner = np.asarray(eroded.filter(ImageFilter.MinFilter(9)).filter(ImageFilter.GaussianBlur(5)), np.float32) / 255
    alpha = ma * 0.22 + ra * 0.38 + np.clip(ma - inner, 0, 1) * 0.04
    # soft specular streaks inside
    st = Image.new("L", (N, N), 0)
    sd = ImageDraw.Draw(st)
    sd.ellipse((260, 360, 470, 395), fill=255)
    sd.ellipse((520, 520, 760, 552), fill=255)
    sd.ellipse((340, 470, 520, 492), fill=200)
    sd.ellipse((660, 600, 790, 622), fill=200)
    st = st.filter(ImageFilter.GaussianBlur(9))
    sa = np.asarray(st, np.float32) / 255 * ma * 0.34
    alpha = np.clip(alpha + sa, 0, 0.9) * (ma > 0.01)
    alpha = np.where(ma > 0.01, np.clip(alpha, 0, 0.9), 0)
    # clear liquid like the picture: a faint grey body, a darker wet outline, bright specular streaks
    body = np.array([150, 188, 196], np.float32)
    edge = np.array([52, 86, 98], np.float32)
    shine = np.array([255, 255, 255], np.float32)
    a_body = ma * 0.55
    a_edge = ra * 0.95
    a_shine = np.clip(sa / 0.34, 0, 1) * 0.8
    A = np.clip(a_body + a_edge + a_shine, 0, 0.97) * (ma > 0.01)
    wsum = np.maximum(a_body + a_edge + a_shine, 1e-5)
    rgb = (body * a_body[..., None] + edge * a_edge[..., None] + shine * a_shine[..., None]) / wsum[..., None]
    im = np.zeros((N, N, 4), np.uint8)
    im[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    im[..., 3] = (A * 255).astype(np.uint8)
    save(Image.fromarray(im, "RGBA"), "Label_Sc10Puddle.png")


def powder():
    N = 512
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    cx, cy = N / 2, N / 2
    dx, dy = (xx - cx) / (N * 0.42), (yy - cy) / (N * 0.36)
    ang = np.arctan2(dy, dx)
    r = np.sqrt(dx * dx + dy * dy)
    wob = 1 + 0.16 * np.sin(ang * 3 + 1.1) + 0.09 * np.sin(ang * 5 + 0.4) + (pnoise(N, N, 2.0, 100) - 0.5) * 0.45
    rr = r / wob
    heap = np.clip(1 - rr, 0, 1) ** 0.55
    grain = pnoise(N, N, 0.2, 101)
    a = heap * (0.55 + 0.45 * grain)
    edge = np.clip((1 - rr) * 4, 0, 1)
    a = np.where(grain > 1 - 0.35 * edge, np.maximum(a, 0.7), a)  # grainy dots toward the edge
    rnd = np.random.RandomState(102)
    sp = rnd.rand(N, N) < 0.02 * np.clip(1.25 - rr, 0, 1) ** 1.5
    a = np.maximum(a, sp * 0.55)
    a = np.clip(a * 0.95, 0, 1)
    g = 245 + (pnoise(N, N, 0.6, 103) - 0.5) * 20
    im = np.zeros((N, N, 4), np.uint8)
    im[..., 0], im[..., 1], im[..., 2] = g, g, g + 2
    im[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(im, "RGBA"), "Label_Sc10Powder.png")


def stain():
    N = 512
    lobes = [(0.45, 0.5, 0.26, 0.20, 0.5), (0.62, 0.42, 0.16, 0.12, 0.0), (0.34, 0.62, 0.14, 0.10, 0.0)]
    m = blob_mask(N, 110, lobes, 0.5, 0.45)
    mask = Image.fromarray((m * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(3))
    eroded = mask.filter(ImageFilter.MinFilter(9)).filter(ImageFilter.GaussianBlur(3))
    rim = ImageChops.subtract(mask, eroded)
    ma = np.asarray(mask, np.float32) / 255
    ra = np.asarray(rim, np.float32) / 255
    tex = pnoise(N, N, 1.6, 111)
    a = ma * (0.12 + 0.12 * tex) + ra * 0.18
    a = np.clip(a, 0, 0.42) * (ma > 0.01)
    im = np.zeros((N, N, 4), np.uint8)
    im[..., 0] = 156 - (tex * 20)
    im[..., 1] = 98 - (tex * 14)
    im[..., 2] = 96 - (tex * 14)
    im[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(im, "RGBA"), "Label_Sc10Stain.png")


def sds_poster():
    w, h = 640, 900
    im = Image.new("RGBA", (w, h), (250, 250, 247, 255))
    d = ImageDraw.Draw(im)
    red = (200, 28, 32, 255)
    k = (22, 24, 30, 255)
    d.rectangle((4, 4, w - 5, h - 5), outline=k, width=8)
    d.rectangle((16, 16, w - 17, 190), fill=red)
    f = fit_font(d, "SAFETY DATA", w - 80, 96, black=True, spacing=2)
    center_text(d, w // 2, 78, "SAFETY DATA", f, (255, 255, 255, 255), spacing=2)
    f = fit_font(d, "SHEET", w - 80, 96, black=True, spacing=10)
    center_text(d, w // 2, 148, "SHEET", f, (255, 255, 255, 255), spacing=10)
    f = font(230, black=True)
    center_text(d, w // 2, 330, "SDS", f, k, spacing=10)
    d.line((60, 470, w - 60, 470), fill=k, width=5)
    f = font(46)
    for i, t in enumerate(("Read before you respond", "Hazards - First aid", "Spill and disposal steps")):
        center_text(d, w // 2, 530 + i * 70, t, f, (60, 64, 72, 255))
    im.alpha_composite(corrosive_diamond(150), (w // 2 - 215, 735))
    im.alpha_composite(biohazard(130), (w // 2 - 65, 745))
    d = ImageDraw.Draw(im)
    d.rectangle((w // 2 + 100, 745, w // 2 + 230, 875), outline=k, width=6)
    f = font(80, black=True)
    center_text(d, w // 2 + 165, 810, "!", f, k)
    save(im.convert("RGB"), "Label_Sc10SdsPoster.png")


def paper():
    w, h = 256, 360
    im = paper_base(w, h, 120, (250, 250, 248))
    d = ImageDraw.Draw(im)
    g = (140, 144, 150, 255)
    k = (22, 24, 30, 255)
    d.rectangle((0, 0, w - 1, h - 1), outline=(205, 205, 205, 255), width=2)
    d.rectangle((18, 18, w - 18, 34), fill=(200, 28, 32, 255))
    f = font(96, black=True)
    center_text(d, w // 2, 92, "SDS", f, k, spacing=6)
    d.line((26, 130, w - 26, 130), fill=k, width=3)
    y = 148
    rnd = random.Random(7)
    while y < h - 30:
        if rnd.random() < 0.18:
            d.rectangle((26, y, 26 + 80, y + 7), fill=(60, 64, 72, 255))
            y += 20
            continue
        d.rectangle((26, y, w - 26 - rnd.randint(0, 60), y + 5), fill=g)
        y += 15
    save(im.convert("RGB"), "Label_Sc10Paper.png")


def wall_plate():
    w, h = 512, 160
    im = Image.new("RGBA", (w, h), (248, 248, 246, 255))
    d = ImageDraw.Draw(im)
    k = (22, 24, 28, 255)
    d.rectangle((3, 3, w - 4, h - 4), outline=k, width=4)
    d.rectangle((14, 14, w - 15, 32), fill=(200, 28, 32, 255))
    f = fit_font(d, "SPILL KIT", w - 80, 92, black=True, spacing=6)
    center_text(d, w // 2, 94, "SPILL KIT", f, k, spacing=6)
    d.rectangle((14, h - 28, w - 15, h - 14), fill=(200, 28, 32, 255))
    save(im.convert("RGB"), "Label_Sc10WallPlate.png")


if __name__ == "__main__":
    import sys
    every = (tile, concrete, title, soda_ash, vermiculite, waste_bag, bin_label, haz_tag, caution,
             pad, sock, puddle, powder, stain, paper, wall_plate, sds_poster)
    only = sys.argv[sys.argv.index("--only") + 1].split(",") if "--only" in sys.argv else None
    for fn in every:
        if only is None or fn.__name__ in only:
            fn()
