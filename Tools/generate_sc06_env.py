"""Generates the Scenario 6 (chemistry lab, fume hood) textures, FX sprites and signs.

Output (re-run: python Tools/generate_sc06_env.py) in Assets/Art/Presentable/2D/Sc06Tex:
  Wall / Floor / Ceil / Steel _Albedo (+ _Normal, _MS)   1024, tile about 2.4 m (floor: 600 mm grey vinyl tiles, ceiling: 600 mm
                                                         drop-ceiling tiles, steel: brushed stainless)
  Fx_Vapor.png / Fx_Wisp.png / Fx_Streak.png / Fx_Drop.png / Fx_Ripple.png   alpha sprites for vapor, airflow, pouring
  Label_Panel.png     control panel face: Fume Hood / LEV Arm / General Ventilation (English + Bangla, ON / OFF)
  Label_SpillKit.png  red SPILL KIT / স্পিল কিট sign          Label_Jerry.png  black jerrycan pictogram sign
  Label_SdsPoster.png "Safety Data Sheet" wall poster           Label_SdsPocket.png  yellow SDS pocket holder
  Poster_Paper.png    Hierarchy of Controls poster (the designer's art on paper, Bangla caption)
  Label_HoodPlate.png FUME HOOD plate + face velocity readout    Label_Sash.png  hazard strip printed on the sash
*_MS = metalness in R, smoothness in A (URP Lit metallic map).
The Bangla words use the same fonts and shaping rules as Tools/render_bangla_text.py.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_sc05_env as g5          # noqa: E402  (noise, normal and MS helpers)
import render_bangla_text as bn         # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc06Tex")
SRC2D = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc06")
N = 1024
FONT = r"C:\Windows\Fonts\segoeui.ttf"
FONTB = r"C:\Windows\Fonts\segoeuib.ttf"
fbm, normal_from_height, ms_image, smooth01, blur = g5.fbm, g5.normal_from_height, g5.ms_image, g5.smooth01, g5.blur


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def f(size, bold=True):
    return ImageFont.truetype(FONTB if bold else FONT, size)


def bn_text(draw, xy, text, size, fill, bold=True, anchor="mm"):
    """Bangla (and mixed) text, centred on xy, with the project's run splitting."""
    fonts = {
        False: ImageFont.truetype(bn.FONT_BOLD if bold else bn.FONT_REGULAR, size, layout_engine=ImageFont.Layout.RAQM),
        True: ImageFont.truetype(bn.LATIN_BOLD if bold else bn.LATIN_REGULAR, int(size * 0.92), layout_engine=ImageFont.Layout.RAQM),
    }
    total = bn.measure(draw, text, fonts)
    x = xy[0] - total / 2
    y = xy[1] - size * 0.62
    for lat, chunk in bn.runs(text):
        draw.text((x, y), chunk, font=fonts[lat], fill=fill)
        x += draw.textlength(chunk, font=fonts[lat])


def centred(draw, xy, text, font, fill):
    draw.text(xy, text, font=font, fill=fill, anchor="mm")


# ------------------------------------------------------------------ tiled surfaces

def wall():
    n = fbm(N, 11, 5, 5)
    fine = fbm(N, 21, 40, 3)
    y = np.linspace(0, 1, N)[:, None]
    base = np.array([0.80, 0.78, 0.72], np.float32)           # warm grey-beige lab paint
    col = base[None, None, :] * (0.93 + 0.12 * n[..., None] + 0.04 * fine[..., None])
    grime = np.clip((0.22 - y) / 0.22, 0, 1) ** 1.5           # lower wall scuffs (texture repeats every 2.4 m: subtle)
    col *= (1 - 0.10 * grime * (0.4 + n))[..., None]
    streak = smooth01(fbm(N, 31, 3, 4, 6.0, 0.5), 0.58, 0.8)
    col *= (1 - 0.06 * streak)[..., None]
    img = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGB")
    save(img, "Wall_Albedo")
    h = fine * 0.5 + n * 0.5
    save(normal_from_height(h, 2.2), "Wall_Normal")
    save(ms_image(0.28 + 0.1 * fine), "Wall_MS")


def floor():
    tiles = 4                       # 4 x 4 tiles per 2.4 m = 600 mm
    cell = N // tiles
    yy, xx = np.mgrid[0:N, 0:N]
    fx = (xx % cell) / cell
    fy = (yy % cell) / cell
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy)) * cell   # px to the nearest edge
    grout = np.clip(1 - edge / 3.0, 0, 1)
    ti = (xx // cell) + (yy // cell) * tiles
    rng = np.random.default_rng(5)
    tone = rng.uniform(-0.035, 0.035, tiles * tiles)[ti]
    n = fbm(N, 41, 6, 5)
    fine = fbm(N, 51, 48, 3)
    base = np.array([0.52, 0.55, 0.57], np.float32)          # mid blue-grey vinyl
    col = base[None, None, :] * (0.96 + 0.08 * n[..., None] + tone[..., None] + 0.03 * fine[..., None])
    scuff = smooth01(fbm(N, 61, 24, 4), 0.6, 0.85)
    col = col * (1 - 0.05 * scuff[..., None])
    col = col * (1 - 0.35 * grout[..., None]) + 0.0
    save(Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGB"), "Floor_Albedo")
    h = 0.5 * fine - 1.2 * grout
    save(normal_from_height(h, 3.0), "Floor_Normal")
    smooth = 0.5 + 0.18 * (1 - scuff) - 0.3 * grout
    save(ms_image(smooth), "Floor_MS")


def ceiling():
    tiles = 4
    cell = N // tiles
    yy, xx = np.mgrid[0:N, 0:N]
    fx = (xx % cell) / cell
    fy = (yy % cell) / cell
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy)) * cell
    grid = np.clip(1 - edge / 5.0, 0, 1)
    fis = fbm(N, 71, 90, 3)
    pin = (fis > 0.72).astype(np.float32) * 0.035
    base = 0.935 - 0.03 * fbm(N, 81, 5, 3) - pin
    # thin dark T-bar grid with a light bevel, the tiles stay clean (the designer's ceiling)
    line = np.clip(1 - edge / 2.2, 0, 1)
    bevel = np.clip(1 - np.abs(edge - 3.6) / 1.6, 0, 1) * 0.05
    col = np.dstack([base, base, base * 0.985]) * (1 - 0.55 * line[..., None]) + bevel[..., None]
    save(Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGB"), "Ceil_Albedo")
    save(normal_from_height(fis * 0.4 - grid * 1.5, 2.2), "Ceil_Normal")


def steel():
    # brushed along x: heavy horizontal stretch of the noise
    streak = fbm(N, 91, 4, 5, 1.0, 40.0)
    fine = fbm(N, 92, 6, 4, 1.0, 120.0)
    smudge = fbm(N, 93, 5, 4)
    v = 0.66 + 0.10 * streak + 0.06 * fine - 0.04 * smudge
    col = np.dstack([v * 0.95, v * 0.98, v * 1.0])
    save(Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGB"), "Steel_Albedo")
    save(normal_from_height(streak * 0.7 + fine * 0.5, 1.0), "Steel_Normal")
    save(ms_image(0.55 + 0.25 * smudge - 0.12 * fine, 0.0), "Steel_MS")


# ------------------------------------------------------------------ FX sprites

def fx():
    n = 256
    y, x = np.mgrid[0:n, 0:n]
    d = np.sqrt(((x - n / 2) / (n / 2)) ** 2 + ((y - n / 2) / (n / 2)) ** 2)
    cloud = fbm(n, 601, 4, 5)
    a = np.clip(np.clip(1 - d, 0, 1) ** 1.2 * (0.45 + 1.0 * cloud), 0, 1)
    img = np.zeros((n, n, 4), np.uint8)
    img[..., :3] = (232, 238, 240)
    img[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(img, "RGBA"), "Fx_Vapor")

    w, h = 128, 256
    yy, xx = np.mgrid[0:h, 0:w]
    dx = (xx - w / 2) / (w / 2)
    dy = (yy - h / 2) / (h / 2)
    wisp = np.clip(1 - np.sqrt(dx ** 2 * 2.4 + dy ** 2 * 0.55), 0, 1) ** 1.3
    noise = fbm(256, 611, 3, 4)[:h, :w]
    a2 = np.clip(wisp * (0.45 + noise), 0, 1)
    img2 = np.zeros((h, w, 4), np.uint8)
    img2[..., :3] = (236, 242, 244)
    img2[..., 3] = (a2 * 255).astype(np.uint8)
    save(Image.fromarray(img2, "RGBA"), "Fx_Wisp")

    # airflow streak: thin horizontal comet, bright head on the right
    w, h = 256, 32
    yy, xx = np.mgrid[0:h, 0:w]
    u = xx / (w - 1)
    v = (yy - h / 2) / (h / 2)
    a3 = np.clip(1 - np.abs(v) ** 1.5, 0, 1) * (u ** 1.8) * np.clip(1 - (u - 0.9) * 8, 0, 1)
    img3 = np.zeros((h, w, 4), np.uint8)
    img3[..., :3] = 255
    img3[..., 3] = (np.clip(a3, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img3, "RGBA"), "Fx_Streak")

    # liquid droplet (soft teardrop)
    s = 64
    yy, xx = np.mgrid[0:s, 0:s]
    dx = (xx - s / 2) / (s / 2)
    dy = (yy - s * 0.58) / (s / 2)
    drop = np.clip(1 - (dx ** 2 * 1.3 + dy ** 2), 0, 1) ** 0.6
    img4 = np.zeros((s, s, 4), np.uint8)
    img4[..., :3] = (235, 245, 250)
    img4[..., 3] = (drop * 255).astype(np.uint8)
    save(Image.fromarray(img4, "RGBA"), "Fx_Drop")

    # ripple ring on the liquid surface
    s = 128
    yy, xx = np.mgrid[0:s, 0:s]
    r = np.sqrt(((xx - s / 2) / (s / 2)) ** 2 + ((yy - s / 2) / (s / 2)) ** 2)
    ring = np.exp(-((r - 0.75) / 0.09) ** 2) * np.clip(1 - np.maximum(r - 0.75, 0) * 4, 0, 1)
    img5 = np.zeros((s, s, 4), np.uint8)
    img5[..., :3] = 255
    img5[..., 3] = (np.clip(ring, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img5, "RGBA"), "Fx_Ripple")


# ------------------------------------------------------------------ labels and signs

def label_panel():
    w, h = 640, 960
    img = Image.new("RGBA", (w, h), (232, 233, 228, 255))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((6, 6, w - 7, h - 7), 22, outline=(120, 124, 126, 255), width=6)
    groups = [("Fume Hood", "ফিউম হুড"), ("LEV Arm", "এলইভি আর্ম"), ("General Ventilation", "সাধারণ ভেন্টিলেশন")]
    for i, (en, bnt) in enumerate(groups):
        top = 20 + i * 312
        centred(d, (w / 2, top + 42), en, f(46 if i < 2 else 40), (26, 28, 30, 255))
        bn_text(d, (w / 2, top + 98), bnt, 34, (60, 62, 66, 255))
        centred(d, (150, top + 150), "ON", f(34), (26, 28, 30, 255))
        centred(d, (150, top + 262), "OFF", f(34), (26, 28, 30, 255))
        d.ellipse((470 - 40, top + 206 - 40, 470 + 40, top + 206 + 40), fill=(48, 50, 52, 255), outline=(150, 154, 156, 255), width=5)
        centred(d, (470, top + 278), "STATUS", f(22), (90, 94, 98, 255))
        d.line((40, top + 306, w - 40, top + 306), fill=(170, 172, 170, 255), width=3)
    save(img, "Label_Panel")


def label_spill():
    w, h = 640, 240
    img = Image.new("RGBA", (w, h), (196, 28, 36, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((8, 8, w - 9, h - 9), outline=(255, 255, 255, 255), width=6)
    centred(d, (w / 2, 92), "SPILL KIT", f(112), (255, 255, 255, 255))
    bn_text(d, (w / 2, 184), "স্পিল কিট", 70, (255, 255, 255, 255))
    save(img, "Label_SpillKit")


def label_jerry():
    s = 512
    img = Image.new("RGBA", (s, s), (246, 246, 244, 255))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((6, 6, s - 7, s - 7), 18, outline=(60, 60, 64, 255), width=8)
    k = (22, 22, 24, 255)
    d.rounded_rectangle((130, 170, 392, 430), 28, fill=k)                    # body
    d.polygon([(130, 200), (210, 130), (392, 130), (392, 200)], fill=k)       # shoulder
    d.rounded_rectangle((152, 70, 226, 150), 14, fill=k)                      # handle block
    d.rectangle((174, 92, 204, 150), fill=(246, 246, 244, 255))               # handle gap
    d.rectangle((300, 74, 340, 140), fill=k)                                  # spout
    d.rounded_rectangle((258, 180, 360, 250), 10, fill=(246, 246, 244, 255))   # label window
    save(img, "Label_Jerry")


def label_sds_poster():
    w, h = 400, 560
    img = Image.new("RGBA", (w, h), (250, 250, 248, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w - 1, h - 1), outline=(190, 190, 190, 255), width=4)
    d.rectangle((0, 0, w, 96), fill=(196, 38, 40, 255))
    centred(d, (w / 2, 40), "Safety Data Sheet", f(44), (255, 255, 255, 255))
    bn_text(d, (w / 2, 76), "নিরাপত্তা তথ্য পত্র", 30, (255, 255, 255, 255))
    d.rectangle((24, 118, w - 24, 250), outline=(60, 90, 160, 255), width=3)
    for i in range(6):
        d.rectangle((40, 136 + i * 19, w - 40 - (i % 3) * 36, 144 + i * 19), fill=(60, 90, 160, 255))
    for i in range(8):
        d.rectangle((30, 280 + i * 28, w - 30 - (i % 4) * 30, 290 + i * 28), fill=(150, 152, 154, 255))
    d.ellipse((w - 84, h - 84, w - 24, h - 24), outline=(196, 38, 40, 255), width=6)
    d.line((w - 76, h - 76, w - 32, h - 32), fill=(196, 38, 40, 255), width=6)
    save(img, "Label_SdsPoster")


def label_sds_pocket():
    w, h = 400, 400
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((24, 30, w - 24, h - 20), 18, fill=(70, 72, 74, 255))             # holder body behind
    # papers sticking out
    d.rectangle((60, 6, w - 90, 140), fill=(244, 244, 240, 255), outline=(170, 170, 170, 255), width=2)
    d.rectangle((90, 0, w - 60, 130), fill=(236, 238, 242, 255), outline=(170, 170, 170, 255), width=2)
    for i in range(4):
        d.rectangle((110, 20 + i * 20, w - 90 - (i % 2) * 40, 28 + i * 20), fill=(110, 130, 180, 255))
    d.rounded_rectangle((24, 120, w - 24, h - 20), 18, fill=(240, 196, 24, 255), outline=(150, 118, 10, 255), width=5)
    centred(d, (w / 2, 250), "SDS", f(130), (40, 40, 40, 255))
    save(img, "Label_SdsPocket")


def poster_paper():
    src = Image.open(os.path.join(SRC2D, "Poster_Hierarchy.png")).convert("RGBA")
    w, h = 1280, 940
    img = Image.new("RGBA", (w, h), (248, 247, 242, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w - 1, h - 1), outline=(160, 160, 156, 255), width=6)
    art = src.copy()
    art.thumbnail((w - 80, h - 190), Image.Resampling.LANCZOS)
    img.alpha_composite(art, ((w - art.width) // 2, 36))
    bn_text(d, (w / 2, h - 108), "নিয়ন্ত্রণের স্তরবিন্যাস", 62, (40, 42, 46, 255))
    d.text((w / 2, h - 48), "Eliminate  ·  Substitute  ·  Engineer  ·  Administer  ·  PPE", font=f(32, False), fill=(90, 92, 96, 255), anchor="mm")
    save(img, "Poster_Paper")


def label_hood_plate(on=True):
    w, h = 512, 192
    img = Image.new("RGBA", (w, h), (46, 50, 54, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((4, 4, w - 5, h - 5), outline=(150, 154, 158, 255), width=4)
    centred(d, (150, 56), "FUME HOOD", f(48), (236, 238, 238, 255))
    bn_text(d, (150, 120), "ফিউম হুড", 40, (236, 238, 238, 255))
    d.rectangle((300, 24, 490, 168), fill=(8, 22, 12, 255), outline=(90, 94, 98, 255), width=3)
    col = (70, 255, 110, 255) if on else (255, 70, 60, 255)
    centred(d, (395, 78), "0.5" if on else "0.0", f(72), col)
    centred(d, (395, 138), "m/s  face velocity" if on else "m/s  hood OFF", f(22, False), col)
    save(img, "Label_HoodPlate" if on else "Label_HoodPlateOff")


def light_grid():
    """Ceiling light panel: bright white diffuser with the egg-crate grid of the designer's picture (used unlit)."""
    w, h = 512, 256
    img = Image.new("RGBA", (w, h), (252, 253, 250, 255))
    d = ImageDraw.Draw(img)
    for i in range(0, w + 1, 64):
        d.line((i, 0, i, h), fill=(196, 202, 204, 255), width=5)
    for j in range(0, h + 1, 64):
        d.line((0, j, w, j), fill=(196, 202, 204, 255), width=5)
    d.rectangle((0, 0, w - 1, h - 1), outline=(150, 156, 158, 255), width=8)
    save(img, "Label_LightGrid")


def diffuser():
    """Square ceiling air diffuser with concentric louvres."""
    s = 512
    img = Image.new("RGBA", (s, s), (224, 226, 224, 255))
    d = ImageDraw.Draw(img)
    for k in range(0, 9):
        o = 14 + k * 28
        d.rectangle((o, o, s - 1 - o, s - 1 - o), outline=(70, 74, 78, 255) if k % 2 == 0 else (240, 242, 240, 255), width=6)
    d.rectangle((0, 0, s - 1, s - 1), outline=(120, 124, 126, 255), width=10)
    save(img, "Label_Diffuser")


def ao_strip():
    h = 128
    a = np.linspace(1, 0, h) ** 2.0
    img = np.zeros((h, 4, 4), np.uint8)
    img[..., 3] = (a[:, None] * 255).astype(np.uint8)
    save(Image.fromarray(img, "RGBA"), "Fx_AOStrip")


def label_5l():
    img = Image.new("RGBA", (256, 160), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.text((128, 84), "5L", font=f(130), fill=(70, 96, 150, 235), anchor="mm")
    save(img, "Label_5L")


def chamfer_panel(name, size, fill_top, fill_bottom, line, line_w, cut, glow=True):
    """Glass UI plate with chamfered corners (the designer's Inspection completed card): light teal, bright outline, soft glow."""
    pad = 14
    w, h = size
    grad = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        t = y / max(1, h - 1)
        grad[y, :, :] = [int(fill_top[i] + (fill_bottom[i] - fill_top[i]) * t) for i in range(4)]
    body = Image.fromarray(grad, "RGBA")
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    pts = [(pad + cut, pad), (w - pad - 1, pad), (w - pad - 1, h - pad - 1 - cut), (w - pad - 1 - cut, h - pad - 1),
           (pad, h - pad - 1), (pad, pad + cut)]
    d.polygon(pts, fill=255)
    body.putalpha(ImageChops.multiply(body.getchannel("A"), mask))
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    ol = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(ol).polygon(pts, outline=line, width=line_w)
    if glow:
        halo = ol.filter(ImageFilter.GaussianBlur(7))
        halo.putalpha(halo.getchannel("A").point(lambda v: min(255, int(v * 1.6))))
        out = Image.alpha_composite(out, halo)
    out = Image.alpha_composite(out, body)
    hl = Image.new("RGBA", size, (0, 0, 0, 0))
    hd = ImageDraw.Draw(hl)
    hd.polygon([(pad + cut, pad + 4), (w - pad - 4, pad + 4), (w - pad - 4, pad + (h - 2 * pad) * 0.28), (pad + 4, pad + (h - 2 * pad) * 0.2)],
               fill=(255, 255, 255, 34))
    hl.putalpha(ImageChops.multiply(hl.getchannel("A"), mask))
    out = Image.alpha_composite(out, hl)
    out = Image.alpha_composite(out, ol)
    out.save(os.path.join(SRC2D, name + ".png"), optimize=True)
    print("wrote", name, size)


def gate_ui():
    chamfer_panel("Gate_Panel", (256, 256), (116, 190, 208, 170), (66, 138, 164, 190), (214, 250, 255, 255), 4, 42)
    chamfer_panel("Gate_Button", (192, 96), (30, 86, 108, 235), (22, 66, 88, 240), (150, 236, 255, 255), 3, 22, True)
    chamfer_panel("Gate_ButtonPrimary", (192, 96), (46, 124, 148, 245), (30, 96, 120, 250), (200, 250, 255, 255), 3, 22, True)


def label_flam():
    s = 512
    img = Image.new("RGBA", (s, s), (246, 246, 244, 255))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((6, 6, s - 7, s - 7), 18, outline=(60, 60, 64, 255), width=8)
    d.polygon([(256, 40), (472, 240), (256, 440), (40, 240)], fill=(206, 40, 36, 255))
    d.polygon([(256, 70), (440, 240), (256, 410), (72, 240)], fill=(246, 246, 244, 255))
    d.polygon([(256, 110), (330, 240), (300, 330), (256, 380), (212, 330), (186, 250), (226, 180)], fill=(20, 20, 22, 255))
    d.polygon([(256, 250), (290, 320), (256, 360), (222, 320)], fill=(246, 246, 244, 255))
    centred(d, (s / 2, 468), "FLAMMABLE", f(48), (30, 30, 32, 255))
    save(img, "Label_Flam")


def label_bottles():
    """Three small reagent labels (white paper, name, hazard diamond) for the amber bottles."""
    specs = [("ETHANOL", "99.8%", True), ("NaOH 1M", "CAUSTIC", True), ("SAMPLE", "Lot 24", False)]
    for i, (name, sub, hazard) in enumerate(specs):
        w, h = 256, 192
        img = Image.new("RGBA", (w, h), (246, 245, 240, 255))
        d = ImageDraw.Draw(img)
        d.rectangle((3, 3, w - 4, h - 4), outline=(150, 150, 150, 255), width=3)
        d.rectangle((3, 3, w - 4, 52), fill=(34, 60, 120, 255) if i != 1 else (170, 40, 40, 255))
        centred(d, (w / 2, 30), name, f(34), (255, 255, 255, 255))
        centred(d, (w / 2, 82), sub, f(24, False), (50, 50, 54, 255))
        if hazard:
            d.polygon([(w / 2, 100), (w / 2 + 40, 140), (w / 2, 180), (w / 2 - 40, 140)], outline=(200, 30, 36, 255), width=6)
            d.rectangle((w / 2 - 4, 120, w / 2 + 4, 148), fill=(20, 20, 20, 255))
            d.ellipse((w / 2 - 4, 154, w / 2 + 4, 162), fill=(20, 20, 20, 255))
        for k in range(3):
            d.rectangle((16, 120 + k * 16, 80 + (k % 2) * 20, 126 + k * 16), fill=(150, 150, 154, 255))
        save(img, "Label_Bottle%d" % i)


def label_sash():
    w, h = 1024, 64
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 8, w, h - 8), fill=(230, 190, 20, 160))
    for x in range(-64, w, 64):
        d.polygon([(x, h - 8), (x + 32, h - 8), (x + 64, 8), (x + 32, 8)], fill=(30, 30, 30, 170))
    save(img, "Label_Sash")


def main():
    wall()
    floor()
    ceiling()
    steel()
    fx()
    label_panel()
    label_spill()
    label_jerry()
    label_sds_poster()
    label_sds_pocket()
    poster_paper()
    label_hood_plate(True)
    label_hood_plate(False)
    label_sash()
    label_flam()
    label_bottles()
    light_grid()
    diffuser()
    ao_strip()
    label_5l()
    gate_ui()


if __name__ == "__main__":
    main()
