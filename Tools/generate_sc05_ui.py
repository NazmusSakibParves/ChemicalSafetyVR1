"""Scenario 5 hologram-style UI sprites (the look of the designer's MASK INFORMATION and RESPIRATOR INSPECTION COMPLETE cards).

Output (re-run: python Tools/generate_sc05_ui.py) in Assets/Art/Presentable/2D/Sc05:
  Holo_Panel.png          9-slice (border 40 px): dark translucent teal glass, thin cyan border, rounded corners
  Holo_Button.png         9-slice (border 30 px): outlined button
  Holo_ButtonPrimary.png  9-slice (border 30 px): brighter NEXT button
  Icon_Back.png / Icon_Next.png / Icon_Close.png / Icon_Speaker.png   128 px line icons (white-cyan, soft glow)
  MaskIcon_0..3.png       upright line-art pictures of the four respirators (N95, half-face, surgical, full-face),
                          built from the real geometry of the supplied Masks.glb so each picture matches its model
"""
import math
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc05")
GLB = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc05", "Masks.glb")
CYAN = (110, 235, 255)
WHITE_CYAN = (215, 250, 255)


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name), optimize=True)
    print("wrote", name, im.size)


def glow(layer, radius, strength):
    """Adds a soft halo behind a transparent RGBA layer."""
    halo = layer.filter(ImageFilter.GaussianBlur(radius))
    a = halo.getchannel("A").point(lambda v: min(255, int(v * strength)))
    halo.putalpha(a)
    out = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    out = Image.alpha_composite(out, halo)
    return Image.alpha_composite(out, layer)


def panel(name, size, radius, border, fill_top, fill_bottom, line, line_w, cut=0):
    """Glass panel with a vertical fill gradient, cyan outline and an outer glow, as a 9-slice friendly sprite."""
    pad = 8
    w, h = size
    base = Image.new("RGBA", size, (0, 0, 0, 0))
    grad = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        t = y / max(1, h - 1)
        col = [int(fill_top[i] + (fill_bottom[i] - fill_top[i]) * t) for i in range(4)]
        grad[y, :, :] = col
    body = Image.fromarray(grad, "RGBA")
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle((pad, pad, w - pad - 1, h - pad - 1), radius, fill=255)
    if cut:
        # chamfered top corners like the hologram cards
        d.polygon([(pad, pad), (pad + cut, pad), (pad, pad + cut)], fill=0)
        d.polygon([(w - pad - 1, pad), (w - pad - 1 - cut, pad), (w - pad - 1, pad + cut)], fill=0)
    body.putalpha(ImageChops.multiply(body.getchannel("A"), mask))
    base = Image.alpha_composite(base, body)
    # faint inner scan lines
    sl = Image.new("RGBA", size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(sl)
    for y in range(pad + 6, h - pad - 6, 6):
        sd.line((pad + 6, y, w - pad - 7, y), fill=(150, 235, 255, 10), width=1)
    sl.putalpha(ImageChops.multiply(sl.getchannel("A"), mask))
    base = Image.alpha_composite(base, sl)
    edge = Image.new("RGBA", size, (0, 0, 0, 0))
    ed = ImageDraw.Draw(edge)
    ed.rounded_rectangle((pad, pad, w - pad - 1, h - pad - 1), radius, outline=line + (255,), width=line_w)
    base = Image.alpha_composite(base, glow(edge, 5, 1.4))
    save(base, name)


def icon_canvas():
    return Image.new("RGBA", (512, 512), (0, 0, 0, 0))


def finish_icon(img, name, size=128):
    img = glow(img, 10, 1.1)
    save(img.resize((size, size), Image.LANCZOS), name)


def icon_back():
    img = icon_canvas()
    d = ImageDraw.Draw(img)
    box = (96, 96, 416, 416)
    d.arc(box, start=-60, end=235, fill=WHITE_CYAN + (255,), width=34)
    # arrow head at the arc's start (pointing counter-clockwise, up-left)
    ang = math.radians(235)
    cx, cy, r = 256, 256, 160
    px, py = cx + r * math.cos(ang), cy + r * math.sin(ang)
    d.polygon([(px - 52, py - 6), (px + 40, py - 44), (px + 30, py + 54)], fill=WHITE_CYAN + (255,))
    finish_icon(img, "Icon_Back.png")


def icon_next():
    img = icon_canvas()
    d = ImageDraw.Draw(img)
    d.line([(96, 270), (210, 380), (420, 140)], fill=WHITE_CYAN + (255,), width=44, joint="curve")
    finish_icon(img, "Icon_Next.png")


def icon_close():
    img = icon_canvas()
    d = ImageDraw.Draw(img)
    d.ellipse((60, 60, 452, 452), outline=WHITE_CYAN + (255,), width=26)
    d.line([(170, 170), (342, 342)], fill=WHITE_CYAN + (255,), width=36)
    d.line([(342, 170), (170, 342)], fill=WHITE_CYAN + (255,), width=36)
    finish_icon(img, "Icon_Close.png")


def icon_speaker():
    img = icon_canvas()
    d = ImageDraw.Draw(img)
    d.ellipse((60, 60, 452, 452), outline=WHITE_CYAN + (255,), width=26)
    # cone
    d.polygon([(150, 215), (205, 215), (270, 165), (270, 347), (205, 297), (150, 297)], fill=WHITE_CYAN + (255,))
    d.arc((250, 196, 330, 316), start=-50, end=50, fill=WHITE_CYAN + (255,), width=20)
    d.arc((250, 160, 380, 352), start=-50, end=50, fill=WHITE_CYAN + (255,), width=20)
    finish_icon(img, "Icon_Speaker.png")


# ---------------------------------------------------------------- mask pictures (clean line art, upright)

W = 14  # line width on the 512 canvas


def mask_layers():
    return Image.new("RGBA", (512, 512), (0, 0, 0, 0)), Image.new("L", (512, 512), 0)


def finish_mask(lines, fill_mask, name):
    fill = Image.new("RGBA", (512, 512), CYAN + (0,))
    fill.putalpha(fill_mask.point(lambda v: int(v * 0.14)))
    img = Image.alpha_composite(fill, glow(lines, 7, 1.3))
    box = img.getchannel("A").point(lambda v: 255 if v > 40 else 0).getbbox()
    if box:
        m = 24
        box = (max(0, box[0] - m), max(0, box[1] - m), min(512, box[2] + m), min(512, box[3] + m))
        crop = img.crop(box)
        k = 470 / max(crop.size)
        crop = crop.resize((max(1, int(crop.size[0] * k)), max(1, int(crop.size[1] * k))), Image.LANCZOS)
        img = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        img.alpha_composite(crop, ((512 - crop.size[0]) // 2, (512 - crop.size[1]) // 2))
    save(img, name)


def mask_n95():
    lines, fm = mask_layers()
    d = ImageDraw.Draw(lines)
    f = ImageDraw.Draw(fm)
    body = [(150, 170), (362, 170), (414, 300), (332, 392), (180, 392), (98, 300)]
    f.polygon(body, fill=255)
    d.line(body + [body[0]], fill=WHITE_CYAN + (255,), width=W, joint="curve")
    d.line([(190, 196), (256, 184), (322, 196)], fill=WHITE_CYAN + (255,), width=W)        # nose clip
    d.ellipse((206, 252, 306, 352), outline=WHITE_CYAN + (255,), width=W)                  # exhalation valve
    d.ellipse((234, 280, 278, 324), outline=WHITE_CYAN + (255,), width=10)
    for y in (250, 360):
        d.line([(190, y), (322, y)], fill=WHITE_CYAN + (160,), width=8)
    d.arc((26, 190, 160, 372), start=95, end=265, fill=WHITE_CYAN + (255,), width=W)        # elastic bands
    d.arc((352, 190, 486, 372), start=-85, end=85, fill=WHITE_CYAN + (255,), width=W)
    finish_mask(lines, fm, "MaskIcon_0.png")


def mask_half_face():
    lines, fm = mask_layers()
    d = ImageDraw.Draw(lines)
    f = ImageDraw.Draw(fm)
    body = [(176, 130), (336, 130), (396, 236), (380, 330), (304, 388), (208, 388), (132, 330), (116, 236)]
    f.polygon(body, fill=255)
    d.line(body + [body[0]], fill=WHITE_CYAN + (255,), width=W, joint="curve")
    for cx in (92, 420):                                                                     # the two cartridges
        f.ellipse((cx - 66, 240, cx + 66, 372), fill=255)
        d.ellipse((cx - 66, 240, cx + 66, 372), outline=WHITE_CYAN + (255,), width=W)
        d.ellipse((cx - 30, 276, cx + 30, 336), outline=WHITE_CYAN + (255,), width=10)
    d.ellipse((222, 318, 290, 386), outline=WHITE_CYAN + (255,), width=W)                    # exhalation valve
    d.arc((200, 120, 312, 232), start=20, end=160, fill=WHITE_CYAN + (255,), width=10)       # nose bridge
    d.arc((120, 10, 392, 250), start=200, end=340, fill=WHITE_CYAN + (255,), width=W)        # head strap
    finish_mask(lines, fm, "MaskIcon_1.png")


def mask_surgical():
    lines, fm = mask_layers()
    d = ImageDraw.Draw(lines)
    f = ImageDraw.Draw(fm)
    box = (92, 150, 420, 366)
    f.rounded_rectangle(box, 44, fill=255)
    d.rounded_rectangle(box, 44, outline=WHITE_CYAN + (255,), width=W)
    for y in (204, 258, 312):                                                                # pleats
        d.line([(110, y), (256, y + 8), (402, y)], fill=WHITE_CYAN + (255,), width=10)
    d.arc((16, 176, 136, 340), start=90, end=270, fill=WHITE_CYAN + (255,), width=W)         # ear loops
    d.arc((376, 176, 496, 340), start=-90, end=90, fill=WHITE_CYAN + (255,), width=W)
    finish_mask(lines, fm, "MaskIcon_2.png")


def mask_full_face():
    lines, fm = mask_layers()
    d = ImageDraw.Draw(lines)
    f = ImageDraw.Draw(fm)
    f.ellipse((106, 60, 406, 400), fill=255)
    d.ellipse((106, 60, 406, 400), outline=WHITE_CYAN + (255,), width=W)
    d.rounded_rectangle((146, 118, 366, 268), 70, outline=WHITE_CYAN + (255,), width=W)       # visor
    d.line([(176, 150), (236, 126)], fill=WHITE_CYAN + (170,), width=8)                      # glare
    for cx in (84, 428):                                                                     # cartridges
        f.ellipse((cx - 62, 290, cx + 62, 414), fill=255)
        d.ellipse((cx - 62, 290, cx + 62, 414), outline=WHITE_CYAN + (255,), width=W)
        d.ellipse((cx - 28, 324, cx + 28, 380), outline=WHITE_CYAN + (255,), width=10)
    d.ellipse((214, 316, 298, 400), outline=WHITE_CYAN + (255,), width=W)                    # speaking diaphragm
    for off in (-14, 0, 14):
        d.line([(236, 358 + off // 2), (276, 358 + off // 2)], fill=WHITE_CYAN + (170,), width=6)
    d.arc((136, 14, 376, 190), start=200, end=340, fill=WHITE_CYAN + (255,), width=W)        # head straps
    finish_mask(lines, fm, "MaskIcon_3.png")


def mask_pictures():
    mask_n95()
    mask_half_face()
    mask_surgical()
    mask_full_face()


if __name__ == "__main__":
    panel("Holo_Panel.png", (256, 256), 26, 40, (14, 40, 48, 205), (8, 24, 31, 215), CYAN, 3, cut=14)
    panel("Holo_Button.png", (192, 96), 18, 30, (16, 70, 84, 150), (10, 48, 60, 170), CYAN, 3)
    panel("Holo_ButtonPrimary.png", (192, 96), 18, 30, (28, 120, 140, 205), (16, 86, 106, 215), WHITE_CYAN, 4)
    icon_back()
    icon_next()
    icon_close()
    icon_speaker()
    mask_pictures()
