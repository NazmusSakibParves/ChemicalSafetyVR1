"""Removes the near-white background of the supplied SC12 cut-out pictures (operator figure and the four PPE icons) so they can sit on
a transparent hologram. Flood fill from the border with a tolerance, soft alpha edge, inner whites are kept.

    python Tools/prepare_sc12_cutouts.py
"""
import os
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc12")
NAMES = ["Sc12_Operator", "Sc12_IconGoggles", "Sc12_IconGloves", "Sc12_IconMask", "Sc12_IconApron"]
TOL = 22  # distance from the corner colour that still counts as background


def cut(path):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.int16)
    h, w, _ = a.shape
    bg = np.median(np.concatenate([a[0, :], a[-1, :], a[:, 0], a[:, -1]]), axis=0)
    near = (np.abs(a - bg).max(axis=2) <= TOL)
    seen = np.zeros((h, w), bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if near[y, x] and not seen[y, x]:
                seen[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if near[y, x] and not seen[y, x]:
                seen[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and near[ny, nx] and not seen[ny, nx]:
                seen[ny, nx] = True
                q.append((ny, nx))
    alpha = np.where(seen, 0, 255).astype(np.uint8)
    # soft edge: blur the mask a little and shrink it by one pixel so no white halo remains
    mask = Image.fromarray(alpha).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    out = im.convert("RGBA")
    out.putalpha(mask)
    out.save(path, optimize=True)
    print("cut", os.path.basename(path), out.size)


if __name__ == "__main__":
    for n in NAMES:
        p = os.path.join(DIR, n + ".png")
        if Image.open(p).mode == "RGBA" and Image.open(p).getpixel((2, 2))[3] == 0:
            print("already transparent", n)
            continue
        cut(p)
