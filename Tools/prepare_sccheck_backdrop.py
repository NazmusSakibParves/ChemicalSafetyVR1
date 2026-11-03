"""Builds the far background of the Supervisor Check hall (machines, windows, crane beams, roof) from the client's workflow picture.

    python Tools/prepare_sccheck_backdrop.py [workflow_step03.jpg]

Takes the hall part (left of the brick wall) of step03.jpg (the picture without the supervisor), paints the glass card out of it,
upscales it 2x with a light sharpening and writes Assets/Resources/ScCheck/backdrop.png. ScCheck's builder hangs it at the far
end of the hall as a large flat picture, with the real 3D hall (floor, table, brick wall, supervisor) in front of it.
Put a better picture at that path (same proportions, hall seen from the table) and rebuild the scene to replace it.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DEFAULT = os.path.join(ROOT, "Tools", "sccheck_src", "step03.jpg")
OUT = os.path.join(ROOT, "Assets", "Resources", "ScCheck", "backdrop.png")

# crop of the hall in the 1024x559 picture (the brick wall starts at x ~ 700) and the glass card to paint out
CROP = (0, 0, 700, 559)
CARD = (345, 150, 680, 395)


def smooth_fill(a, box):
    """Replaces the box by a vertical and horizontal blend of the pixels around it, then blurs the seam."""
    x0, y0, x1, y1 = box
    h, w = a.shape[:2]
    for y in range(y0, y1):
        t = (y - y0) / max(1, (y1 - y0 - 1))
        top = a[max(0, y0 - 6):y0, x0:x1].mean(axis=0)
        bot = a[y1:min(h, y1 + 6), x0:x1].mean(axis=0)
        v = top * (1 - t) + bot * t
        left = a[y, max(0, x0 - 6):x0].mean(axis=0)
        right = a[y, x1:min(w, x1 + 6)].mean(axis=0)
        xs = np.linspace(0, 1, x1 - x0)[:, None]
        hh = left[None, :] * (1 - xs) + right[None, :] * xs
        a[y, x0:x1] = 0.5 * v + 0.5 * hh
    return a


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else SRC_DEFAULT
    # the hall strip left of the glass card and of the table (x 0..340), mirrored at the press so the picture is twice as wide:
    # windows and a blue bench with jugs at both ends, the two halves of the press meet in the middle
    strip = Image.open(src).convert("RGB").crop((0, 0, 340, 559))
    mirror = strip.transpose(Image.FLIP_LEFT_RIGHT)
    im = Image.new("RGB", (680, 559))
    im.paste(strip, (0, 0))
    im.paste(mirror, (340, 0))
    im = im.resize((im.size[0] * 2, im.size[1] * 2), Image.LANCZOS).filter(ImageFilter.UnsharpMask(radius=2, percent=60, threshold=2))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    im.save(OUT)
    print("wrote", OUT, im.size)


if __name__ == "__main__":
    main()
