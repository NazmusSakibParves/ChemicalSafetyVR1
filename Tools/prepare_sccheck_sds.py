"""Turns an SDS sheet picture (a paper on a clipboard, like MCQ1/2/3) into the holographic Reference SDS look of the workflow
pictures: paper -> translucent navy, dark text -> white, coloured pictograms kept.

    python Tools/prepare_sccheck_sds.py                       # the three default sheets from Tools/sccheck_src
    python Tools/prepare_sccheck_sds.py in.png out.png        # one sheet (e.g. a new chemical's sheet for SC14)

Put the result at Assets/Resources/ScCheck/SC14/sds1.png (per scenario) or Assets/Resources/ScCheck/sds1.png (default).
A picture that is already in the holo look can simply be copied there.
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Tools", "sccheck_src")
OUT = os.path.join(ROOT, "Assets", "Resources", "ScCheck")


def convert(src, dst):
    raw = Image.open(src).convert("RGBA")
    raw = raw.resize((raw.width * 2, raw.height * 2), Image.LANCZOS)  # smoother glyph edges when the sheet is drawn larger than the source
    im = Image.new("RGB", raw.size, (0, 0, 0))
    im.paste(raw, mask=raw.split()[3])  # transparent corners count as black, never as paper
    a = np.asarray(im, dtype=np.float32)
    mx, mn = a.max(2), a.min(2)
    lum = a @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    sat = (mx - mn) / (mx + 1.0)
    white = (lum > 228) & (sat < 0.10)
    ys, xs = np.where(white)
    # the paper = the big white block (ignore the clip, the border): use rows/cols where most pixels are white
    rows = np.where(white.sum(1) > 0.45 * white.shape[1] * 0.8)[0]
    cols = np.where(white.sum(0) > 0.45 * white.shape[0] * 0.8)[0]
    y0, y1 = rows.min() + 6, rows.max() - 6
    x0, x1 = cols.min() + 6, cols.max() - 6
    a = a[y0:y1, x0:x1]
    mx, mn = mx[y0:y1, x0:x1], mn[y0:y1, x0:x1]
    lum = lum[y0:y1, x0:x1]
    sat = sat[y0:y1, x0:x1]
    navy = np.array([14.0, 46.0, 92.0])
    band = np.array([44.0, 110.0, 176.0])
    out = np.zeros(a.shape[:2] + (4,), dtype=np.float32)
    t = np.clip((215.0 - lum) / 85.0, 0.0, 1.0)  # text strength (crisper than before)
    is_band = (lum > 150) & (lum < 228) & (sat < 0.12)
    base = np.where(is_band[..., None], band, navy)
    rgb = base * (1 - t[..., None]) + 255.0 * t[..., None]
    alpha = np.where(is_band, 0.94, 0.88) * (1 - t) + 1.0 * t  # nearly opaque so the table behind never muddies the text
    colour = sat > 0.32  # red / orange pictograms keep their colour
    rgb = np.where(colour[..., None], a, rgb)
    alpha = np.where(colour, 1.0, alpha)
    out[..., :3] = rgb
    out[..., 3] = alpha * 255.0
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(dst)
    print("wrote", dst, "crop", (x0, y0, x1, y1))


if __name__ == "__main__":
    if len(sys.argv) == 3:
        convert(sys.argv[1], sys.argv[2])
    else:
        for i, name in enumerate(["MCQ1.png", "MCQ2.jpg", "MCQ3.jpg"], 1):
            convert(os.path.join(SRC, name), os.path.join(OUT, "sds%d.png" % i))
