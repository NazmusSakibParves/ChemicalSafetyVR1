"""Draws the text-free artwork of the final result card (client design 'result card 2.jpeg') as ONE png.

Coordinates are the pixel coordinates of the reference picture (card box x 420..1180, y 100..840); the picture is
rendered at 2x. ResultCardView.cs places every text at the same reference coordinates, so the card matches the design.

    python Tools/generate_result_card.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Ui", "result_card_bg.png")

X0, Y0, X1, Y1 = 420, 100, 1180, 840
S = 2  # supersample
W, H = (X1 - X0) * S, (Y1 - Y0) * S

CYAN = (60, 190, 255)
CYAN_L = (150, 230, 255)
GREEN = (40, 230, 80)
RED = (255, 50, 50)
NAVY = (4, 22, 58)


def P(x, y):
    return ((x - X0) * S, (y - Y0) * S)


def chamfer(x0, y0, x1, y1, c, cut_tl=True, cut_tr=True, cut_br=True, cut_bl=True):
    pts = []
    pts += [P(x0 + c, y0), P(x1 - c, y0)] if cut_tr else [P(x0 + c * 0, y0), P(x1, y0)]
    pts += [P(x1, y0 + c)] if cut_tr else []
    pts += [P(x1, y1 - c), P(x1 - c, y1)] if cut_br else [P(x1, y1)]
    pts += [P(x0 + c, y1), P(x0, y1 - c)] if cut_bl else [P(x0, y1)]
    pts += [P(x0, y0 + c), P(x0 + c, y0)] if cut_tl else [P(x0, y0)]
    return pts


def glow_layer(draw_fn, color, blur, strength=1.0):
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(layer), color)
    a = layer.getchannel("A").filter(ImageFilter.GaussianBlur(blur * S))
    a = a.point(lambda v: min(255, int(v * strength)))
    g = Image.new("RGBA", (W, H), color + (0,))  # constant colour, only the alpha is blurred (no grey fringe)
    g.putalpha(a)
    return g


def outline_poly(pts, width):
    def fn(d, col):
        d.line(pts + [pts[0]], fill=col + (255,), width=int(width * S), joint="curve")
    return fn


def main():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def comp(layer):
        nonlocal img
        img = Image.alpha_composite(img, layer)

    # ---- main body: glowing chamfered frame, dark translucent glass
    body = chamfer(430, 232, 1170, 800, 36)
    comp(glow_layer(outline_poly(body, 7), CYAN, 9, 1.4))
    fill = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(fill).polygon(body, fill=NAVY + (222,))
    comp(fill)
    # fine grid like the reference
    grid = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grid)
    for gx in range(440, 1170, 28):
        gd.line([P(gx, 236), P(gx, 796)], fill=(60, 150, 255, 14), width=1)
    for gy in range(240, 800, 28):
        gd.line([P(432, gy), P(1168, gy)], fill=(60, 150, 255, 14), width=1)
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon(body, fill=255)
    grid.putalpha(Image.composite(grid.getchannel("A"), Image.new("L", (W, H), 0), mask))
    comp(grid)
    d = ImageDraw.Draw(img)
    d.line(body + [body[0]], fill=CYAN_L + (255,), width=3 * S, joint="curve")
    inner = chamfer(442, 244, 1158, 788, 30)
    d.line(inner + [inner[0]], fill=CYAN + (150,), width=1 * S, joint="curve")

    # ---- header: wide chamfered plate, diagonal stripes at the right
    head = chamfer(425, 103, 1175, 222, 34, cut_tl=False, cut_tr=True, cut_br=False, cut_bl=True)
    comp(glow_layer(outline_poly(head, 6), CYAN, 8, 1.4))
    hf = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(hf).polygon(head, fill=(5, 28, 74, 245))
    comp(hf)
    d = ImageDraw.Draw(img)
    d.line(head + [head[0]], fill=CYAN_L + (255,), width=3 * S, joint="curve")
    stripes = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(stripes)
    for i, off in enumerate((0, 36, 72)):
        sd.polygon([P(1058 + off, 222), P(1090 + off, 222), P(1128 + off, 103), P(1096 + off, 103)], fill=(30, 120, 255, 255))
    smask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(smask).polygon(head, fill=255)
    stripes.putalpha(Image.composite(stripes.getchannel("A"), Image.new("L", (W, H), 0), smask))
    comp(stripes)

    # ---- FINAL RESULT pill
    pill = chamfer(615, 232, 985, 288, 22)
    comp(glow_layer(outline_poly(pill, 5), CYAN, 6, 1.3))
    pf = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(pf).polygon(pill, fill=(8, 40, 100, 255))
    comp(pf)
    ImageDraw.Draw(img).line(pill + [pill[0]], fill=CYAN_L + (255,), width=2 * S, joint="curve")

    def rounded(box, col, fillcol, glow=6, r=14, w=3):
        x0, y0, x1, y1 = box

        def fn(dd, c):
            dd.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=r * S, outline=c + (255,), width=w * S + 2)

        comp(glow_layer(fn, col, glow, 1.5))
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(layer)
        dd.rounded_rectangle([P(x0, y0), P(x1, y1)], radius=r * S, fill=fillcol + (255,), outline=col + (255,), width=w * S)
        comp(layer)

    rounded((497, 450, 1102, 512), GREEN, (8, 70, 28))     # obtained marks
    rounded((497, 520, 1102, 580), CYAN, (6, 40, 110))     # time
    rounded((495, 640, 788, 738), GREEN, (8, 60, 24), r=16)  # correct
    rounded((812, 640, 1105, 738), RED, (70, 8, 12), r=16)   # incorrect

    # ---- thin rule above the message
    d = ImageDraw.Draw(img)
    d.line([P(470, 612), P(512, 612)], fill=CYAN + (140,), width=S)
    d.line([P(1090, 612), P(1132, 612)], fill=CYAN + (140,), width=S)

    # ---- icons
    def check_circle(cx, cy, r, col, w):
        def fn(dd, c):
            dd.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], outline=c + (255,), width=int(w * S))
            dd.line([P(cx - r * 0.45, cy), P(cx - r * 0.1, cy + r * 0.38), P(cx + r * 0.52, cy - r * 0.38)],
                    fill=c + (255,), width=int(w * 1.4 * S), joint="curve")
        comp(glow_layer(fn, col, 4, 1.2))
        fn(ImageDraw.Draw(img), col)

    check_circle(537, 481, 20, GREEN, 4)
    check_circle(545, 690, 30, GREEN, 6)

    # X (incorrect)
    def x_mark(cx, cy, r, col, w):
        def fn(dd, c):
            dd.line([P(cx - r, cy - r), P(cx + r, cy + r)], fill=c + (255,), width=int(w * S))
            dd.line([P(cx - r, cy + r), P(cx + r, cy - r)], fill=c + (255,), width=int(w * S))
        comp(glow_layer(fn, col, 4, 1.2))
        fn(ImageDraw.Draw(img), col)

    x_mark(860, 690, 24, RED, 9)

    # stopwatch
    def stopwatch(cx, cy, r, col):
        def fn(dd, c):
            dd.ellipse([P(cx - r, cy - r + 3), P(cx + r, cy + r + 3)], outline=c + (255,), width=4 * S)
            dd.rectangle([P(cx - 5, cy - r - 6), P(cx + 5, cy - r)], fill=c + (255,))
            dd.line([P(cx, cy + 3), P(cx, cy - r * 0.55 + 3)], fill=c + (255,), width=3 * S)
            dd.line([P(cx, cy + 3), P(cx + r * 0.45, cy + 3 + r * 0.25)], fill=c + (255,), width=3 * S)
        comp(glow_layer(fn, col, 4, 1.2))
        fn(ImageDraw.Draw(img), (120, 200, 255))

    stopwatch(537, 550, 18, CYAN)

    # ---- BACK TO HOME button (hexagon-like pill)
    btn = chamfer(622, 768, 978, 840, 26)
    comp(glow_layer(outline_poly(btn, 6), CYAN, 8, 1.5))
    bf = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(bf).polygon(btn, fill=(6, 36, 100, 255))
    comp(bf)
    ImageDraw.Draw(img).line(btn + [btn[0]], fill=CYAN_L + (255,), width=3 * S, joint="curve")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print("wrote", OUT, img.size)


if __name__ == "__main__":
    main()
