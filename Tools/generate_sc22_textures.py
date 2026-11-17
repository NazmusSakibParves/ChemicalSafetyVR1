#!/usr/bin/env python
"""Scenario 22 textures -> Assets/Art/Presentable/3D/Sc22/Tex (run with `python -I Tools/generate_sc22_textures.py <workflow_dir> <downloads_dir>`).

 * labels: the client's four label pictures (WhatsApp 13.03.20 / 13.03.26) are cut out of their white background (rounded sticker alpha) and, for the
   curved DRINKING WATER sticker, straightened column by column so it can be wrapped on the bottle without a second bend;
 * room: procedural concrete wall, worn floor, ceiling, fabric, steel, taken in colour from the client's scene01.jpg;
 * signs: the blue safety sign and the chemical-guide poster cropped from scene01.jpg.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi

work = sys.argv[1]
dl = sys.argv[2]
out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Art", "Presentable", "3D", "Sc22", "Tex")
os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(22)


def save(img, name, mode=None):
    if isinstance(img, np.ndarray):
        img = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    img.save(os.path.join(out, name + ".png"))
    print("saved", name, img.size)


# ------------------------------------------------------------------ labels
def alpha_cut(path, thr=236, pad=0, rounded=0.03):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    bg = (a.min(axis=2) >= thr)
    lab, n = ndi.label(bg)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bgmask = np.isin(lab, list(border))
    fg = ~bgmask
    fg = ndi.binary_opening(fg, iterations=2)
    fg = ndi.binary_fill_holes(fg)
    ys, xs = np.where(fg)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    return a[y0:y1, x0:x1], fg[y0:y1, x0:x1]


def finish_label(rgb, mask, name, round_frac=0.022, shrink=2):
    mask = ndi.binary_erosion(mask, iterations=shrink)
    h, w = mask.shape
    # rounded rectangle limit so stray corners never show
    yy, xx = np.mgrid[0:h, 0:w]
    r = min(h, w) * round_frac
    cx = np.clip(xx, r, w - 1 - r)
    cy = np.clip(yy, r, h - 1 - r)
    rr = ((xx - cx) ** 2 + (yy - cy) ** 2) ** 0.5
    mask = mask & (rr <= r)
    alpha = ndi.gaussian_filter(mask.astype(np.float32), 1.0)
    # bleed edge colour outwards so mip-mapping never shows a white fringe
    rgb2 = rgb.copy()
    for c in range(3):
        ch = rgb[..., c]
        filled = ndi.distance_transform_edt(~mask, return_distances=False, return_indices=True)
        rgb2[..., c] = ch[tuple(filled)]
    img = np.dstack([rgb2, alpha * 255.0])
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGBA")
    if im.width > 2048:
        im = im.resize((2048, int(im.height * 2048 / im.width)), Image.LANCZOS)
    save(im, name)


def straighten(rgb, mask):
    """Top and bottom edges of the bowed sticker -> straight rectangle (column-wise resample)."""
    h, w = mask.shape
    cols = np.where(mask.sum(axis=0) > h * 0.35)[0]
    c0, c1 = cols.min(), cols.max() + 1
    H = int(np.median([(np.where(mask[:, x])[0].max() - np.where(mask[:, x])[0].min()) for x in range(c0, c1, 8)]))
    outc = np.zeros((H, c1 - c0, 3), np.float32)
    for i, x in enumerate(range(c0, c1)):
        ys = np.where(mask[:, x])[0]
        t, b = ys.min(), ys.max()
        src = np.linspace(t, b, H)
        for c in range(3):
            outc[:, i, c] = np.interp(src, np.arange(h), rgb[:, x, c])
    return outc, np.ones(outc.shape[:2], bool)


f_ghs = os.path.join(dl, "WhatsApp Image 2026-11-17 at 13.03.20 (1).jpeg")
f_water = os.path.join(dl, "WhatsApp Image 2026-11-17 at 13.03.26.jpeg")
f_plate = os.path.join(dl, "WhatsApp Image 2026-11-17 at 13.03.26 (1).jpeg")
f_can = os.path.join(dl, "WhatsApp Image 2026-11-17 at 13.03.20.jpeg")

for path, name, thr in ((f_ghs, "Sc22_LabelDrumGhs", 238), (f_plate, "Sc22_LabelDrumPlate", 238), (f_can, "Sc22_LabelCan", 232)):
    rgb, m = alpha_cut(path, thr)
    finish_label(rgb, m, name)

rgb, m = alpha_cut(f_water, 233)
rgb, m = straighten(rgb, m)
# trim the left / right ends where the bowed corners were
w = rgb.shape[1]
cut = int(w * 0.015)
finish_label(rgb[:, cut:w - cut], m[:, cut:w - cut], "Sc22_LabelWater", round_frac=0.02)


# ------------------------------------------------------------------ room textures
def fbm(n, octaves=6, base=4, seed=0):
    r = np.random.default_rng(seed)
    acc = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        k = base * 2 ** o
        g = r.random((k, k)).astype(np.float32)
        g = np.array(Image.fromarray((g * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)).astype(np.float32) / 255.0
        acc += g * amp
        tot += amp
        amp *= 0.5
    return acc / tot


def tint(base, n01, spread):
    base = np.array(base, np.float32)
    return base[None, None, :] + (n01[..., None] - 0.5) * spread


N = 1024
# concrete wall (sage grey, like the pillars of scene01)
n1 = fbm(N, 7, 3, 1)
n2 = fbm(N, 5, 12, 2)
wall = tint((150, 146, 134), n1, 40) + (n2[..., None] - 0.5) * 14
streak = ndi.gaussian_filter(rng.random((N, N)).astype(np.float32), (40, 1.5))
streak = (streak - streak.mean()) / streak.std()
wall -= streak[..., None] * 2.2
pits = (rng.random((N, N)) > 0.9985).astype(np.float32)
pits = ndi.gaussian_filter(pits, 1.4) * 80
wall -= pits[..., None]
save(wall, "Sc22_Concrete")

# painted pillar (lower part dirtier)
pillar = wall.copy()
grad = np.linspace(0, 1, N)[:, None, None]
pillar *= (1.0 - 0.22 * grad ** 2)
save(pillar, "Sc22_Pillar")

# floor: worn grey concrete with tyre / scuff marks and oil
f1 = fbm(N, 7, 4, 3)
floor = tint((124, 122, 113), f1, 34)
scuff = ndi.gaussian_filter(rng.random((N, N)).astype(np.float32), (2, 25))
floor += (scuff[..., None] - scuff.mean()) * 24
oil = ndi.gaussian_filter((fbm(N, 4, 3, 7) > 0.66).astype(np.float32), 6)
floor -= oil[..., None] * 5
cracks = ndi.gaussian_filter((rng.random((N, N)) > 0.9992).astype(np.float32), 0.9) * 120
floor -= cracks[..., None] * 0.7
save(floor, "Sc22_Floor")

# ceiling (old plaster, water stains)
c1 = fbm(N, 6, 3, 5)
ceil = tint((132, 128, 118), c1, 34)
stain = ndi.gaussian_filter((fbm(N, 4, 2, 11) > 0.7).astype(np.float32), 18)
ceil -= stain[..., None] * 30
save(ceil, "Sc22_Ceiling")

# fabric bale (off-white woven cloth) and dark folded fabric
y, x = np.mgrid[0:512, 0:512]
weave = (np.sin(x * 1.9) * np.sin(y * 1.9)) * 0.5 + 0.5
bale = tint((218, 214, 206), fbm(512, 5, 6, 9), 36) + (weave[..., None] - 0.5) * 10
save(bale, "Sc22_FabricLight")
dark = tint((40, 42, 46), fbm(512, 5, 8, 13), 34) + (weave[..., None] - 0.5) * 8
save(dark, "Sc22_FabricDark")

# brushed steel
st = ndi.gaussian_filter(rng.random((512, 512)).astype(np.float32), (0.6, 22))
st = (st - st.mean()) / st.std()
steel = np.dstack([150 + st * 12] * 3) + fbm(512, 4, 3, 17)[..., None] * 24 - 12
save(steel, "Sc22_Steel")

# yellow floor line (worn)
ln = np.zeros((64, 512, 4), np.float32)
ln[..., :3] = (214, 170, 20)
ln[..., 3] = 235 * (1 - ndi.gaussian_filter((rng.random((64, 512)) > 0.8).astype(np.float32), 2) * 0.45)
save(ln, "Sc22_YellowLine")

# ------------------------------------------------------------------ signs from scene01.jpg
s1 = Image.open(os.path.join(work, "scene01.jpg")).convert("RGB")
save(s1.crop((704, 0, 817, 112)).resize((452, 448), Image.LANCZOS), "Sc22_SignBlue")
save(s1.crop((1214, 22, 1380, 312)).resize((498, 870), Image.LANCZOS), "Sc22_PosterChem")
save(s1.crop((872, 190, 915, 262)).resize((258, 432), Image.LANCZOS), "Sc22_SignYellow")
print("done")
