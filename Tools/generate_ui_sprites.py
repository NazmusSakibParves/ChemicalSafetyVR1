"""Generates tintable UI sprites + drum thumbnails for the Presentable demo.

All sprites are white on transparent so Unity Image.color can tint them
(green normal/correct, red wrong). Re-run: python Tools/generate_ui_sprites.py
"""

import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D")
OUT = os.path.join(ART, "Generated")
LABELS = os.path.join(ART, "Labels")
UI = os.path.join(ART, "UI")
WHITE = (255, 255, 255, 255)


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))


def ring(size=512, width=26, blur=10):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    m = blur * 2 + width
    d.ellipse((m, m, size - m, size - m), outline=WHITE, width=width)
    glow = img.filter(ImageFilter.GaussianBlur(blur))
    return Image.alpha_composite(glow, img)


def dashed_ring(size=512, width=14, segments=24):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    m = 30
    step = 360 / segments
    for i in range(segments):
        a0 = i * step
        d.arc((m, m, size - m, size - m), a0, a0 + step * 0.55, fill=WHITE, width=width)
    return img


def radial_glow(size=256):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    px = img.load()
    c = size / 2
    for y in range(size):
        for x in range(size):
            r = math.hypot(x - c, y - c) / c
            a = max(0.0, 1.0 - r) ** 2
            px[x, y] = (255, 255, 255, int(a * 255))
    return img


def beam(w=128, h=512):
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    px = img.load()
    for y in range(h):
        v = (1.0 - y / h) ** 1.6
        for x in range(w):
            u = 1.0 - abs((x - w / 2) / (w / 2))
            px[x, y] = (255, 255, 255, int(255 * v * (u ** 2) * 0.9))
    return img


def arrow_down(size=256):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    s = size
    d.polygon([(s * 0.5, s * 0.92), (s * 0.12, s * 0.45), (s * 0.34, s * 0.45),
               (s * 0.34, s * 0.08), (s * 0.66, s * 0.08), (s * 0.66, s * 0.45),
               (s * 0.88, s * 0.45)], fill=WHITE)
    glow = img.filter(ImageFilter.GaussianBlur(6))
    return Image.alpha_composite(glow, img)


def speaker(size=256):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    s = size
    d.rectangle((s * 0.10, s * 0.38, s * 0.26, s * 0.62), fill=WHITE)
    d.polygon([(s * 0.26, s * 0.38), (s * 0.46, s * 0.20), (s * 0.46, s * 0.80),
               (s * 0.26, s * 0.62)], fill=WHITE)
    for i, r in enumerate((0.16, 0.27, 0.38)):
        box = (s * 0.46 - s * r, s * 0.5 - s * r, s * 0.46 + s * r, s * 0.5 + s * r)
        d.arc(box, -45, 45, fill=WHITE, width=int(s * 0.045))
    return img


def check(size=256, stroke=0.11):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    s = size
    d.line([(s * 0.22, s * 0.52), (s * 0.42, s * 0.72), (s * 0.80, s * 0.30)],
           fill=WHITE, width=int(s * stroke), joint="curve")
    return img


def cross(size=256, stroke=0.11):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    s = size
    w = int(s * stroke)
    d.line([(s * 0.25, s * 0.25), (s * 0.75, s * 0.75)], fill=WHITE, width=w)
    d.line([(s * 0.75, s * 0.25), (s * 0.25, s * 0.75)], fill=WHITE, width=w)
    return img


def circle(size=128, filled=False, width=10):
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    m = width
    if filled:
        d.ellipse((m, m, size - m, size - m), fill=WHITE)
    else:
        d.ellipse((m, m, size - m, size - m), outline=WHITE, width=width)
    return img


def rounded_panel(w=512, h=256, r=36, border=6):
    """9-slice friendly frame: white border + translucent fill."""
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((border, border, w - border, h - border), radius=r,
                        fill=(255, 255, 255, 60), outline=WHITE, width=border)
    return img


def rounded_solid(w=512, h=256, r=36):
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((2, 2, w - 2, h - 2), radius=r, fill=WHITE)
    return img


def soft_rect(w=512, h=256, r=36):
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((24, 24, w - 24, h - 24), radius=r, fill=WHITE)
    return img.filter(ImageFilter.GaussianBlur(14))


def tintable(src_name, out_name):
    """Designer cyan frames -> white luminance + original alpha (tint in Unity)."""
    src = Image.open(os.path.join(UI, src_name)).convert("RGBA")
    r, g, b, a = src.split()
    lum = ImageChops.lighter(ImageChops.lighter(r, g), b)
    out = Image.merge("RGBA", (lum, lum, lum, a))
    save(out, out_name)


def drum_thumb(label_file, overlay=None, w=160, h=220):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    body = (14, 18, w - 14, h - 10)
    d.rounded_rectangle(body, radius=18, fill=(28, 84, 170, 255), outline=(12, 40, 90, 255), width=4)
    d.ellipse((14, 6, w - 14, 34), fill=(52, 112, 200, 255), outline=(12, 40, 90, 255), width=3)
    for y in (h * 0.36, h * 0.68):
        d.line([(16, y), (w - 16, y)], fill=(16, 56, 120, 255), width=5)
    if label_file:
        lab = Image.open(os.path.join(LABELS, label_file)).convert("RGBA")
        lw = int(w * 0.62)
        lab = lab.resize((lw, int(lab.height * lw / lab.width)), Image.LANCZOS)
        img.alpha_composite(lab, ((w - lw) // 2, int(h * 0.30)))
    if overlay:
        try:
            font = ImageFont.truetype("arialbd.ttf", 26)
        except OSError:
            font = ImageFont.load_default()
        tw = d.textlength(overlay, font=font)
        d.text(((w - tw) / 2, h * 0.80), overlay, font=font, fill=(230, 40, 40, 255))
    return img


def doc_icon(title, pictogram, w=150, h=180):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(8, 8), (w - 40, 8), (w - 8, 40), (w - 8, h - 8), (8, h - 8)], fill=WHITE,
              outline=(60, 60, 60, 255))
    try:
        font = ImageFont.truetype("arialbd.ttf", 30)
    except OSError:
        font = ImageFont.load_default()
    d.text((18, 14), title, font=font, fill=(20, 20, 20, 255))
    cx, cy, r = w / 2, h * 0.62, 44
    d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=WHITE,
              outline=(220, 30, 30, 255), width=7)
    if pictogram == "!":
        d.rectangle((cx - 5, cy - 24, cx + 5, cy + 8), fill=(10, 10, 10, 255))
        d.ellipse((cx - 6, cy + 14, cx + 6, cy + 26), fill=(10, 10, 10, 255))
    else:
        d.ellipse((cx - 16, cy - 22, cx + 16, cy + 6), fill=(10, 10, 10, 255))
        d.line([(cx - 22, cy + 10), (cx + 22, cy + 24)], fill=(10, 10, 10, 255), width=6)
        d.line([(cx + 22, cy + 10), (cx - 22, cy + 24)], fill=(10, 10, 10, 255), width=6)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    save(ring(), "FX_Ring")
    save(dashed_ring(), "FX_RingDashed")
    save(radial_glow(), "FX_RadialGlow")
    save(beam(), "FX_Beam")
    save(arrow_down(), "FX_ArrowDown")
    save(speaker(), "ICON_Speaker")
    save(check(), "ICON_Check")
    save(cross(), "ICON_Cross")
    save(circle(), "ICON_Circle")
    save(circle(filled=True), "ICON_Dot")
    save(rounded_panel(), "UI_RoundedPanel")
    save(rounded_solid(), "UI_RoundedSolid")
    save(soft_rect(), "UI_SoftGlow")
    tintable("UI_PopupScreen.png", "UI_PopupScreen_Tint")
    tintable("UI_AnswerBox.png", "UI_AnswerBox_Tint")
    tintable("UI_Pop02.png", "UI_Pop02_Tint")
    save(drum_thumb("Label_Acetone_Faded.png"), "THUMB_Drum_Faded")
    save(drum_thumb(None), "THUMB_Drum_NoLabel")
    save(drum_thumb("Label_Methanol.png", "Mismatch"), "THUMB_Drum_Mismatch")
    save(drum_thumb("Label_Isopropanol.png"), "THUMB_Drum_Correct")
    save(doc_icon("GHS", "!"), "ICON_GHS")
    save(doc_icon("SDS", "skull"), "ICON_SDS")
    print("generated in", OUT)


if __name__ == "__main__":
    main()
