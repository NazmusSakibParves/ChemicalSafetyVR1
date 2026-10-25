"""Scenario 4 GLOVE PROPERTIES card icons.

The designer's four icons (JPEG on an off-white page) become transparent PNGs by turning the page colour into alpha
("colour to alpha"), then they are cropped to the artwork. Output goes to Assets/Art/Presentable/2D/Sc04.

    python Tools/generate_sc04_ui.py [source folder]

icon1 = material, icon2 = permeation time (clock), icon3 = rating (rosette), icon4 = glove outline.
"""
import os
import sys

import numpy as np
from PIL import Image

SRC = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\msi\Downloads"
DST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Presentable", "2D", "Sc04")
FILES = {
    "SC4 - icon4.jpeg": "GloveCard_Glove.png",
    "SC4 - icon1.jpeg": "GloveCard_Material.png",
    "SC4 - icon3.jpeg": "GloveCard_Rating.png",
    "SC4 - icon2.jpeg": "GloveCard_Time.png",
}


def colour_to_alpha(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    # page colour = the median of the border pixels
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    bg = np.median(border, axis=0)
    diff = a - bg
    denom = np.where(diff < 0, bg, 1.0 - bg)
    ch = np.abs(diff) / np.maximum(denom, 1e-4)
    alpha = np.clip(ch.max(axis=2), 0.0, 1.0)
    t = np.clip((alpha - 0.16) / 0.42, 0.0, 1.0)  # drop the JPEG speckle, keep the lines solid
    alpha = t * t * (3.0 - 2.0 * t)
    safe = np.maximum(alpha, 1e-3)[..., None]
    rgb = np.clip(bg + diff / safe, 0.0, 1.0)
    rgb = np.where(alpha[..., None] < 0.02, 0.0, rgb)
    out = np.dstack([rgb, alpha])
    return Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), "RGBA")


def lighten(img, k):
    arr = np.asarray(img).astype(np.float32)
    target = np.array([115, 205, 255], np.float32)
    arr[..., :3] = arr[..., :3] * (1 - k) + target * k
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def crop_pad(img, pad=0.06, side=512):
    arr = np.asarray(img)
    ys, xs = np.where(arr[..., 3] > 12)
    if len(xs) == 0:
        return img
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    img = img.crop(box)
    w, h = img.size
    scale = side * (1.0 - 2 * pad) / max(w, h)
    img = img.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(img, ((side - img.size[0]) // 2, (side - img.size[1]) // 2), img)
    return canvas


def main():
    os.makedirs(DST, exist_ok=True)
    for src, dst in FILES.items():
        path = os.path.join(SRC, src)
        if not os.path.exists(path):
            print("missing", path)
            continue
        out = crop_pad(colour_to_alpha(Image.open(path)))
        if dst.endswith("Time.png"):
            out = lighten(out, 0.5)  # the clock is dark blue, too dim on the dark card
        out.save(os.path.join(DST, dst))
        print("wrote", dst)


if __name__ == "__main__":
    main()
