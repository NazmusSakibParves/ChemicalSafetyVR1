"""Scenario 17 / 18 safety data sheets in the dark glass look of Scenarios 13-16 (the client's pictures are pale paper: white background).

    python -X utf8 -I Tools/prepare_sc17_18_sds_dark.py

Every sheet Assets/Resources/ScCheck/SC17|SC18/sds{1,2,3}.png is first copied once to Assets/Art/Presentable/2D/ScCheck/SdsPaper/<SC>_sds{n}.png
(the original pale paper picture; copy it back over the sheet to restore it, the script then converts from it), then rewritten: the paper becomes the translucent navy of the other sheets, dark text becomes
white, grey header bars become a lighter blue band, red and gold (pictograms, bullets, clip) keep their colour.
"""
import os
import shutil

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.path.join(ROOT, "Assets", "Resources", "ScCheck")
NAVY = np.array([14.0, 46.0, 92.0])
LIGHT = np.array([236.0, 245.0, 255.0])


def convert(path):
    src = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    r, g, b = src[..., 0], src[..., 1], src[..., 2]
    v = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
    mx = src.max(axis=2)
    mn = src.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1.0), 0.0)
    sat = sat * np.clip((mx - 90.0) / 60.0, 0.0, 1.0)  # near-black pixels are ink, not colour (their tiny tint looks saturated)
    # paper -> navy, ink -> light; the curve keeps anti-aliased text edges smooth
    w = np.clip((v - 0.12) / 0.80, 0.0, 1.0) ** 1.6
    out = NAVY[None, None, :] * w[..., None] + LIGHT[None, None, :] * (1.0 - w[..., None])
    alpha = 224.0 + 31.0 * (1.0 - w)  # paper 224 like the other sheets, ink opaque
    # coloured parts (red pictogram borders, red bullets, gold clip): keep their colour, a little brighter
    colour = np.clip((sat - 0.22) / 0.20, 0.0, 1.0)
    boosted = np.clip(src * 1.08 + 6.0, 0, 255)
    out = out * (1.0 - colour[..., None]) + boosted * colour[..., None]
    alpha = alpha * (1.0 - colour) + 255.0 * colour
    rgba = np.dstack([np.clip(out, 0, 255), np.clip(alpha, 0, 255)]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


for sc in ("SC17", "SC18"):
    for n in (1, 2, 3):
        f = os.path.join(BASE, sc, "sds%d.png" % n)
        keep = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "ScCheck", "SdsPaper", "%s_sds%d.png" % (sc, n))
        os.makedirs(os.path.dirname(keep), exist_ok=True)
        if not os.path.exists(keep):
            shutil.copyfile(f, keep)
        convert(keep).save(f, optimize=True)
        print(sc, n, "ok")

# contact sheet for a visual check (not part of the game)
sheet = Image.new("RGBA", (3 * 380, 2 * 520), (60, 100, 140, 255))
for k, sc in enumerate(("SC17", "SC18")):
    for n in (1, 2, 3):
        im = Image.open(os.path.join(BASE, sc, "sds%d.png" % n)).convert("RGBA")
        im.thumbnail((370, 510))
        sheet.alpha_composite(im, ((n - 1) * 380 + 5, k * 520 + 5))
sheet.convert("RGB").save(os.path.join(ROOT, "Temp", "sds_dark_check.png"))
