#!/usr/bin/env python
"""Scenario 22 caution popup icons (client picture 2): a water bottle in a red prohibition ring, and a head drinking from a bottle with a yellow warning
triangle. Drawn 4x supersampled -> Assets/Resources/Sc22/Sc22_IconNoBottle.png, Sc22_IconDrink.png (512 x 512, transparent).
Run: python -I Tools/generate_sc22_icons.py"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Sc22")
os.makedirs(OUT, exist_ok=True)
S = 4
N = 512 * S


def new():
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def finish(im, name):
    im = im.resize((512, 512), Image.LANCZOS)
    im.save(os.path.join(OUT, name + ".png"))
    print("saved", name)


def P(pts):
    return [(x * S, y * S) for x, y in pts]


def line(d, pts, w, col):
    pts = P(pts)
    d.line(pts, fill=col, width=int(w * S), joint="curve")
    r = w * S / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse([x - r, y - r, x + r, y + r], fill=col)


def rounded_poly(d, pts, col, outline=None, width=0):
    d.polygon(P(pts), fill=col)
    if outline is not None:
        line(d, pts + [pts[0]], width, outline)


# ------------------------------------------------------------------ icon 1: bottle + prohibition
im, d = new()
# bottle (centre 256, upright), semi-transparent plastic with a blue cap, label band and wave lines
cap = (30, 105, 210, 255)
body_col = (214, 232, 246, 235)
edge = (150, 185, 215, 255)
d.rounded_rectangle([225 * S, 96 * S, 287 * S, 130 * S], radius=6 * S, fill=cap)
d.rounded_rectangle([229 * S, 130 * S, 283 * S, 150 * S], radius=5 * S, fill=body_col)
body = [(229, 150), (283, 150), (292, 186), (330, 205), (338, 245), (338, 410), (330, 440), (300, 452), (212, 452), (182, 440), (174, 410), (174, 245), (182, 205), (220, 186)]
rounded_poly(d, body, body_col, edge, 5)
# wave lines at the foot
for k, yy in enumerate((388, 412, 430)):
    pts = [(180 + i * 4, yy + 6 * math.sin(i * 0.5 + k)) for i in range(0, 40)]
    line(d, pts, 4, (120, 165, 205, 220))
# label band
d.rectangle([174 * S, 270 * S, 338 * S, 350 * S], fill=(248, 250, 252, 255))
d.rectangle([174 * S, 270 * S, 338 * S, 282 * S], fill=(30, 105, 210, 255))
d.rectangle([174 * S, 338 * S, 338 * S, 350 * S], fill=(30, 105, 210, 255))
d.rectangle([194 * S, 296 * S, 318 * S, 306 * S], fill=(25, 50, 110, 255))
d.rectangle([204 * S, 316 * S, 308 * S, 326 * S], fill=(25, 50, 110, 255))
# prohibition ring + slash
red = (214, 28, 28, 255)
cx = cy = 256
r_out, r_in = 238, 200
d.ellipse([(cx - r_out) * S, (cy - r_out) * S, (cx + r_out) * S, (cy + r_out) * S], outline=red, width=(r_out - r_in) * S)
a = math.radians(-45)
dx, dy = math.cos(a) * 0.5, math.sin(a) * 0.5
w = 38
ux, uy = -math.sin(math.radians(45)), math.cos(math.radians(45))
# slash from top-left to bottom-right
p0 = (cx - r_in * 0.74, cy - r_in * 0.74)
p1 = (cx + r_in * 0.74, cy + r_in * 0.74)
line(d, [p0, p1], w, red)
finish(im, "Sc22_IconNoBottle")

# ------------------------------------------------------------------ icon 2: head drinking + warning triangle
im, d = new()
white = (255, 255, 255, 255)
head = [(150, 480), (150, 410), (112, 340), (108, 262), (132, 190), (190, 128), (262, 98), (330, 112), (372, 150), (384, 198), (388, 232),
        (424, 268), (392, 286), (392, 306), (410, 322), (392, 338), (396, 372), (404, 392), (372, 424), (336, 436), (336, 480)]
K, OX, OY = 0.80, 0, 14        # the head is drawn smaller so the bottle and the triangle fit the picture
H = lambda pts: [(x * K + OX, y * K + OY) for x, y in pts]
line(d, H(head), 13, white)
ang = math.radians(-28)
ca, sa = math.cos(ang), math.sin(ang)
mx, my = 410 * K + OX, 322 * K + OY


def tf(x, y):
    return (mx + (x * ca - y * sa) * K, my + (x * sa + y * ca) * K)


neck = [(0, -16), (46, -16), (74, -34), (96, -44), (210, -44), (222, -34), (222, 34), (210, 44), (96, 44), (74, 34), (46, 16), (0, 16)]
line(d, [tf(x, y) for x, y in neck] + [tf(*neck[0])], 11, white)
line(d, [tf(46, -16), tf(46, 16)], 9, white)
line(d, [tf(40, 0), tf(8, 0)], 8, white)
yellow = (255, 204, 0, 255)
tri = [(404, 318), (496, 482), (312, 482)]
d.polygon(P(tri), fill=yellow)
for (x, y) in tri:
    r = 12
    d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=yellow)
d.rounded_rectangle([393 * S, 360 * S, 415 * S, 430 * S], radius=9 * S, fill=(25, 25, 25, 255))
d.ellipse([392 * S, 442 * S, 416 * S, 466 * S], fill=(25, 25, 25, 255))
finish(im, "Sc22_IconDrink")
