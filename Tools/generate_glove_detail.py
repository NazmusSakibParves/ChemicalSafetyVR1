"""Fine nitrile wrinkle detail for the blue gloves (tileable, light grey multiplier over the glove colour).

Output: Assets/Art/Presentable/2D/Env/GLOVE_Detail.png   (re-run: python Tools/generate_glove_detail.py)
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env", "GLOVE_Detail.png")
N = 512
rng = np.random.default_rng(11)


def tile_noise(scale, seed):
    r = np.random.default_rng(seed)
    base = r.random((scale, scale)).astype(np.float32)
    big = np.tile(base, (3, 3))
    im = Image.fromarray((big * 255).astype(np.uint8)).resize((N * 3, N * 3), Image.BICUBIC)
    a = np.asarray(im, dtype=np.float32) / 255.0
    return a[N:2 * N, N:2 * N]


def main():
    soft = tile_noise(5, 1)
    mid = tile_noise(14, 2)
    fine = tile_noise(64, 3)
    # creases: thin dark lines where the mid noise crosses a threshold
    crease = np.clip(1.0 - np.abs(mid - 0.5) * 14.0, 0.0, 1.0)
    v = 0.9 + 0.1 * soft - 0.06 * crease * (0.6 + 0.4 * soft) + 0.04 * (fine - 0.5)
    v = np.clip(v, 0.55, 1.0)
    img = Image.fromarray((v * 255).astype(np.uint8)).convert("RGB").filter(ImageFilter.GaussianBlur(1.1))
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
