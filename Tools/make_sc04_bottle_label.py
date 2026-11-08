"""Makes the Scenario 4 HCl bottle label texture from the designer's 'bottle level.png'
(Scene04 workflow / 2D Resource; it is Assets/Art/Presentable/2D/Sc04/BottleLabel.png in the project).

The picture has an off-white margin around the rounded red frame. This crops to the frame and makes everything outside the frame
transparent (flood fill from the four corners), so the label on the bottle is exactly the printed sticker with rounded corners.

    python Tools/make_sc04_bottle_label.py
Output: Assets/Art/Presentable/2D/Sc04/BottleLabelHcl.png
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04", "BottleLabel.png")
DST = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04", "BottleLabelHcl.png")

im = Image.open(SRC).convert("RGB")
w, h = im.size

# background = light pixels connected to a corner
mask = Image.new("L", (w, h), 255)
px = im.load()
light = Image.new("L", (w, h), 0)
lp = light.load()
for y in range(h):
    for x in range(w):
        r, g, b = px[x, y]
        lp[x, y] = 255 if (r > 200 and g > 200 and b > 200) else 0
ImageDraw.floodfill(light, (0, 0), 128, thresh=0)
for c in [(w - 1, 0), (0, h - 1), (w - 1, h - 1)]:
    if light.getpixel(c) == 255:
        ImageDraw.floodfill(light, c, 128, thresh=0)
alpha = light.point(lambda v: 0 if v == 128 else 255)
out = im.convert("RGBA")
out.putalpha(alpha)
box = alpha.getbbox()
out = out.crop(box)
out.save(DST)
print("saved", DST, out.size, "bbox", box)
