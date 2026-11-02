"""Scenario 12 premium hologram UI sprites (workflow step06 look): glass panes with a soft glow rim, glass rows, a glowing NEXT pill and the
donned operator with a cyan outline glow. 9-slice friendly (rounded corners, flat centre).

    python Tools/generate_sc12_ui.py
Writes Assets/Art/Presentable/2D/Sc12/Sc12_PaneGlass.png, Sc12_PaneDark.png, Sc12_Row.png, Sc12_Pill.png, Sc12_OperatorHolo.png
(Sc12_OperatorHolo is made from Sc12_Operator.png, which must already be a transparent cut-out: python Tools/prepare_sc12_cutouts.py)
"""
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc12")
SS = 3  # supersampling


def rr_mask(w, h, r, inset=0):
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle(
        [inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1], radius=r * SS, fill=255)
    return m


def gradient(w, h, top, bottom):
    t = np.linspace(0, 1, h * SS)[:, None, None]
    top = np.array(top, float)[None, None, :]
    bottom = np.array(bottom, float)[None, None, :]
    g = top * (1 - t) + bottom * t
    g = np.repeat(g, w * SS, axis=1)
    return Image.fromarray(np.clip(g, 0, 255).astype(np.uint8), "RGBA")


def pane(w, h, r, top, bottom, rim, glow, sheen=0.16, rim_px=3, glow_px=7):
    """rounded glass: gradient fill, top sheen, diagonal streak, glowing rim"""
    base = gradient(w, h, top, bottom)
    # sheen: brighter band at the top, fading out
    sh = np.zeros((h * SS, w * SS), float)
    band = int(h * SS * 0.42)
    sh[:band] = np.linspace(sheen, 0, band)[:, None]
    # soft diagonal streak
    yy, xx = np.mgrid[0:h * SS, 0:w * SS]
    d = np.abs((xx / (w * SS)) * 0.9 - (yy / (h * SS)) * 0.55 - 0.18)
    sh += np.clip(1 - d / 0.06, 0, 1) * (sheen * 0.45)
    sh_img = Image.fromarray(np.clip(sh * 255, 0, 255).astype(np.uint8), "L")
    white = Image.new("RGBA", base.size, (255, 255, 255, 255))
    white.putalpha(sh_img)
    base = Image.alpha_composite(base, white)
    mask = rr_mask(w, h, r, glow_px + 1)
    # clip the fill to the rounded shape
    a = ImageChops.multiply(base.getchannel("A"), mask)
    base.putalpha(a)
    # rim and outer glow
    outer = rr_mask(w, h, r, glow_px + 1)
    inner = rr_mask(w, h, max(r - rim_px, 1), glow_px + 1 + rim_px)
    ring = ImageChops.subtract(outer, inner)
    rim_img = Image.new("RGBA", base.size, rim)
    rim_img.putalpha(ImageChops.multiply(ring, Image.new("L", base.size, rim[3])))
    glow_img = Image.new("RGBA", base.size, glow)
    gmask = ring.filter(ImageFilter.GaussianBlur(glow_px * SS * 0.7))
    glow_img.putalpha(ImageChops.multiply(gmask, Image.new("L", base.size, glow[3])))
    out = Image.alpha_composite(glow_img, base)
    # thin inner highlight line just inside the rim
    hl = ImageChops.subtract(rr_mask(w, h, max(r - rim_px - 3, 1), glow_px + 1 + rim_px + 3),
                             rr_mask(w, h, max(r - rim_px - 4, 1), glow_px + 1 + rim_px + 4))
    hl_img = Image.new("RGBA", base.size, (255, 255, 255, 255))
    hl_img.putalpha(ImageChops.multiply(hl, Image.new("L", base.size, 70)))
    out = Image.alpha_composite(out, hl_img)
    out = Image.alpha_composite(out, rim_img)
    return out.resize((w, h), Image.Resampling.LANCZOS)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, img.size)


def operator_holo():
    src = Image.open(os.path.join(OUT, "Sc12_Operator.png")).convert("RGBA")
    pad = 40
    canvas = Image.new("RGBA", (src.width + pad * 2, src.height + pad * 2), (0, 0, 0, 0))
    canvas.paste(src, (pad, pad))
    alpha = canvas.getchannel("A")
    # cyan outline: dilated alpha minus alpha, plus a wide soft glow
    dil = alpha.filter(ImageFilter.MaxFilter(9))
    edge = ImageChops.subtract(dil, alpha)
    outline = Image.new("RGBA", canvas.size, (110, 240, 255, 255))
    outline.putalpha(edge.filter(ImageFilter.GaussianBlur(1.2)))
    glow = Image.new("RGBA", canvas.size, (60, 200, 255, 255))
    glow.putalpha(alpha.filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.GaussianBlur(14)).point(lambda v: int(v * 0.55)))
    out = Image.alpha_composite(glow, outline)
    out = Image.alpha_composite(out, canvas)
    return out


if __name__ == "__main__":
    # left pane: faint glass
    save(pane(256, 256, 34, (190, 235, 255, 46), (110, 190, 225, 26), (190, 245, 255, 235), (70, 215, 255, 150), sheen=0.14), "Sc12_PaneGlass")
    # right pane: dark glass that carries the list
    save(pane(256, 256, 34, (16, 52, 82, 218), (6, 28, 50, 232), (170, 240, 255, 240), (60, 205, 255, 150), sheen=0.10), "Sc12_PaneDark")
    # rows
    save(pane(256, 128, 30, (44, 98, 128, 235), (22, 66, 92, 235), (160, 225, 245, 205), (60, 190, 235, 70), sheen=0.12, rim_px=2, glow_px=4), "Sc12_Row")
    # NEXT pill
    save(pane(256, 112, 40, (14, 70, 96, 245), (6, 40, 62, 245), (150, 250, 255, 255), (60, 235, 255, 170), sheen=0.14, rim_px=3, glow_px=7), "Sc12_Pill")
    save(operator_holo(), "Sc12_OperatorHolo")
