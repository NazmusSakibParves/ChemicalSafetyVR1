"""Dotted "data stream" texture for the scan beam between a held container and its details card.

Output: Assets/Art/Presentable/2D/Env/FX_ScanBeam.png   (re-run: python Tools/generate_scan_beam.py)
Tiles along the beam; the soft top and bottom edges fade to nothing.
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env", "FX_ScanBeam.png")
W, H, CELL = 256, 64, 8


def main():
    rng = np.random.default_rng(5)
    img = np.zeros((H, W, 4), dtype=np.float32)
    cols, rows = W // CELL, H // CELL
    for cx in range(cols):
        for cy in range(rows):
            # more dots near the centre line, fewer toward the edges
            centre = 1.0 - abs((cy + 0.5) / rows - 0.5) * 2.0
            if rng.random() < 0.18 + 0.62 * centre * centre:
                a = 0.35 + 0.65 * rng.random() * (0.4 + 0.6 * centre)
                s = 2 if rng.random() < 0.5 else 0
                img[cy * CELL + s:(cy + 1) * CELL - s, cx * CELL + s:(cx + 1) * CELL - s] = (0.5, 0.95, 1.0, a)
    # a faint solid core line so the beam reads even when the dots are small
    core = np.exp(-((np.arange(H) - H / 2) / (H * 0.07)) ** 2)[:, None]
    img[..., 3] = np.maximum(img[..., 3], 0.35 * core)
    img[..., :3] = np.where(img[..., 3:4] > 0, np.maximum(img[..., :3], [0.5, 0.95, 1.0]), img[..., :3])
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGBA").save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
