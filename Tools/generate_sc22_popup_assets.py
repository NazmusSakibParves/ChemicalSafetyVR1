#!/usr/bin/env python
"""Scenario 22 caution popup assets (client picture 6 + the three icon pictures WhatsApp 15.50.09 / 15.50.09 (1) / 15.50.20).

 * Sc22_IconNoBottle.png  bottle in a red prohibition ring (white background cut out, also the white inside the ring)
 * Sc22_IconWarn.png      yellow warning triangle (white background cut out)
 * Sc22_IconDrink.png     white outline of a head drinking from a bottle (grey-white background -> transparent, the line stays white)
 * Sc22_PopupFrame.png    frosted-glass card: thin rose-to-white border with a soft red glow, almost clear inside (9-slice, border 56 px)
 * Sc22_PopupTile.png     the two inner tiles: faint white fill and a thin light border (9-slice, border 40 px)
 * Sc22_CloseIcon.png     the small circled x of the picture's top right corner
Run: python -I Tools/generate_sc22_popup_assets.py "<downloads folder>"   -> Assets/Resources/Sc22
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter
from scipy import ndimage as ndi

dl = sys.argv[1]
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Sc22")
os.makedirs(OUT, exist_ok=True)


def save(im, name):
    im.save(os.path.join(OUT, name + ".png"))
    print("saved", name, im.size)


def trim(im, pad=6):
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 8)
    box = (max(0, xs.min() - pad), max(0, ys.min() - pad), min(im.width, xs.max() + pad + 1), min(im.height, ys.max() + pad + 1))
    return im.crop(box)


def cut_background(path, thr, seeds_inside=()):
    """near-white / light-grey background connected to the border (and to optional inner seeds) -> transparent."""
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    light = (a.min(axis=2) >= thr)
    lab, n = ndi.label(light)
    keep = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    for (x, y) in seeds_inside:
        if lab[y, x] > 0:
            keep.add(lab[y, x])
    bg = np.isin(lab, list(keep))
    alpha = (~bg).astype(np.float32)
    alpha = ndi.binary_opening(alpha > 0.5, iterations=1).astype(np.float32)
    alpha = ndi.gaussian_filter(alpha, 0.9)
    out = np.dstack([a, alpha * 255.0])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


# ------------------------------------------------------------------ the three client icons
bottle = cut_background(os.path.join(dl, "WhatsApp Image 2026-11-17 at 15.50.09 (1).jpeg"), 236,
                        seeds_inside=[(110, 200), (150, 380), (480, 430), (330, 70), (470, 250), (90, 330)])
save(trim(bottle), "Sc22_IconNoBottle")
tri = cut_background(os.path.join(dl, "WhatsApp Image 2026-11-17 at 15.50.09.jpeg"), 232)
save(trim(tri), "Sc22_IconWarn")

# head: a white line on a light grey page: the page grey (about 246) is the background, the line is white (255)
head = np.asarray(Image.open(os.path.join(dl, "WhatsApp Image 2026-11-17 at 15.50.20.jpeg")).convert("L")).astype(np.float32)
bgv = np.median(head[:6, :])
line = np.clip((head - (bgv + 1.5)) / (255.0 - bgv - 1.5), 0, 1)
line = ndi.gaussian_filter(line, 0.6)
line = np.clip(line * 1.6, 0, 1)
rgba = np.dstack([np.full_like(line, 255), np.full_like(line, 255), np.full_like(line, 255), line * 255])
head_im = Image.fromarray(rgba.astype(np.uint8), "RGBA")
# thicken a little so the thin dashed outline reads from 1.5 m
alpha = head_im.getchannel("A").filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(0.8))
head_im.putalpha(alpha)
save(trim(head_im), "Sc22_IconDrink")


# ------------------------------------------------------------------ frosted glass frame / tile / close icon
def rr_mask(size, radius, inset=0, ss=3):
    w, h = size
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset * ss, inset * ss, (w - inset) * ss - 1, (h - inset) * ss - 1], radius=radius * ss, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def frame(size=256, radius=44, border=3.0):
    w = h = size
    base = np.zeros((h, w, 4), np.float32)
    outer = np.asarray(rr_mask((w, h), radius)).astype(np.float32) / 255.0
    inner = np.asarray(rr_mask((w, h), radius - border, inset=border)).astype(np.float32) / 255.0
    ring = np.clip(outer - inner, 0, 1)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = np.clip((xx / w) * 0.75 + (yy / h) * 0.25, 0, 1)          # rose on the left / top, white-blue on the right (picture 6)
    rose = np.array([255, 150, 150], np.float32)
    white = np.array([232, 240, 255], np.float32)
    col = rose[None, None, :] * (1 - t[..., None]) + white[None, None, :] * t[..., None]
    # soft red glow just inside the left edge
    glow_w = np.clip(1.0 - xx / (w * 0.18), 0, 1) ** 2
    fill_col = np.array([255, 235, 235], np.float32)
    base[..., :3] = np.where(ring[..., None] > 0.01, col, fill_col)
    fill_a = inner * (0.10 + 0.10 * glow_w)                       # almost clear inside, a little more at the glowing left edge
    ring_a = ring * (0.60 + 0.25 * (1 - t))
    base[..., 3] = np.clip((fill_a + ring_a) * 255.0, 0, 255)
    # outer red glow
    glow = ndi.gaussian_filter(outer, 5.0) * (1 - outer)
    glow_a = glow * 0.22 * (1 - t)
    base[..., 3] = np.clip(base[..., 3] + glow_a * 255.0, 0, 255)
    base[..., :3] = np.where((glow_a > 0.004)[..., None] & (ring[..., None] < 0.01) & (inner[..., None] < 0.01), np.array([255, 110, 110], np.float32), base[..., :3])
    return Image.fromarray(base.astype(np.uint8), "RGBA")


def tile(size=128, radius=26, border=2.0):
    w = h = size
    base = np.zeros((h, w, 4), np.float32)
    outer = np.asarray(rr_mask((w, h), radius)).astype(np.float32) / 255.0
    inner = np.asarray(rr_mask((w, h), radius - border, inset=border)).astype(np.float32) / 255.0
    ring = np.clip(outer - inner, 0, 1)
    base[..., :3] = 255
    base[..., 3] = np.clip((inner * 0.07 + ring * 0.38) * 255.0, 0, 255)
    return Image.fromarray(base.astype(np.uint8), "RGBA")


save(frame(), "Sc22_PopupFrame")
save(tile(), "Sc22_PopupTile")

ss = 4
ci = Image.new("RGBA", (64 * ss, 64 * ss), (0, 0, 0, 0))
d = ImageDraw.Draw(ci)
col = (255, 255, 255, 150)
d.ellipse([6 * ss, 6 * ss, 58 * ss, 58 * ss], outline=col, width=3 * ss)
d.line([(22 * ss, 22 * ss), (42 * ss, 42 * ss)], fill=col, width=3 * ss)
d.line([(42 * ss, 22 * ss), (22 * ss, 42 * ss)], fill=col, width=3 * ss)
save(ci.resize((64, 64), Image.LANCZOS), "Sc22_CloseIcon")
