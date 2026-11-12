"""Prepares the client MCQ UI art (Scenarios 1-12) for Unity.

Source: the designer's folder `MCQ UI` (frame, three answer boxes, tick, cross). The answer boxes come with a lot of
transparent padding around the neon edge, and a different amount per colour, so a box drawn at a fixed row height looks thin
or fat depending on its colour. Every box is cropped to its visible area (+ a small margin) so blue / green / red are the same
size. The frame, tick and cross are copied as they are.

    python Tools/prepare_mcq_ui.py "C:/Users/<you>/Downloads/MCQ UI-.../MCQ UI"
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "McqUi")
FILES = {
    "POP up.png": ("Mcq_Frame.png", False),
    "light_blue_answer_box_no_text.png": ("Mcq_BoxBlue.png", True),
    "green_answer_box_no_text.png": ("Mcq_BoxGreen.png", True),
    "red_answer_box_no_text.png": ("Mcq_BoxRed.png", True),
    "tick mark.png": ("Mcq_Tick.png", True),
    "Cross mark.png": ("Mcq_Cross.png", True),
}
MARGIN = 4


def crop_to_content(im):
    """Crop to the rows / columns that really hold the artwork (a few stray pixels at an edge must not widen the crop)."""
    a = im.getchannel("A")
    w, h = im.size
    px = a.load()
    rows = [sum(1 for x in range(w) if px[x, y] > 150) for y in range(h)]
    cols = [sum(1 for y in range(h) if px[x, y] > 150) for x in range(w)]
    rmin = max(6, int(max(rows) * 0.12))
    cmin = max(6, int(max(cols) * 0.12))
    ys = [y for y in range(h) if rows[y] >= rmin]
    xs = [x for x in range(w) if cols[x] >= cmin]
    if not ys or not xs:
        return im
    return im.crop((max(0, xs[0] - MARGIN), max(0, ys[0] - MARGIN), min(w, xs[-1] + 1 + MARGIN), min(h, ys[-1] + 1 + MARGIN)))


def main(src):
    os.makedirs(OUT, exist_ok=True)
    for name, (dst, crop) in FILES.items():
        im = Image.open(os.path.join(src, name)).convert("RGBA")
        if crop:
            im = crop_to_content(im)
        im.save(os.path.join(OUT, dst))
        print(dst, im.size)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\msi\Downloads\MCQ UI-20261110T183500Z-1-001\MCQ UI")
