"""Scenario 4: textures cut out of the client's reference photo (Assets/Art/Reference/Ref_Sc04Factory.jpg).

    python Tools/make_sc04_photo_textures.py

Writes to Assets/Art/Presentable/2D/Sc04:
  Photo_Backdrop.png   the hall behind the table (machines, pipes, pillars, windows, sign) with the acid bottle painted out and the
                       table cut away (alpha), mapped on a curved screen behind the table (far field, so little parallax)
  Photo_Steel.png      seamless tile of the scratched stainless table top (clean patches of the photo, mirrored tiling)
  Photo_Cloth.png      the dark cloth

Only needs Pillow + numpy. The photo is 1678 x 937: a sharper original gives a sharper result (re-run with the file replaced).
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "Art", "Reference", "Ref_Sc04Factory.jpg")
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04")

im = Image.open(SRC).convert("RGB")
W, H = im.size
sx, sy = W / 1678.0, H / 937.0  # coordinates below are for the 1678 x 937 photo


def box(x0, y0, x1, y1):
    return (int(x0 * sx), int(y0 * sy), int(x1 * sx), int(y1 * sy))


def backdrop():
    a = np.asarray(im, dtype=np.float32)
    # 1. paint the bottle out (columns 628..886 over the whole upper part): the narrow strip right of it (machine / pillar edge) is
    #    stretched over the gap, so it reads as one wide dark machine behind the bottle (no mirrored windows, no blur band)
    hx0, hx1 = int(628 * sx), int(886 * sx)
    gap = hx1 - hx0
    strip = Image.fromarray(np.clip(a[:, hx1 + 4:hx1 + 4 + int(64 * sx)], 0, 255).astype(np.uint8))
    stretched = np.asarray(strip.resize((gap, a.shape[0]), Image.BICUBIC), dtype=np.float32)
    left = a[:, hx0 - 24:hx0].copy()
    k = np.linspace(0, 1, 24, dtype=np.float32)[None, :, None]
    stretched[:, :24] = left * (1 - k) + stretched[:, :24] * k
    a[:, hx0:hx1] = stretched
    out = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    # 2. keep everything above the table; cut the table (below the back lip) away with alpha
    alpha = np.zeros((H, W), dtype=np.float32)
    yl = int(378 * sy)
    alpha[:yl, :] = 1.0
    # left of the table: the floor and the machine feet continue down the diagonal table edge
    for x in range(0, int(245 * sx)):
        k = x / (245 * sx)
        alpha[: int((520 - 142 * k) * sy), x] = 1.0
    # right of the table: the pillar and the floor continue to the table's right end
    for x in range(int(1585 * sx), W):
        k = (x - 1585 * sx) / max(1.0, W - 1585 * sx)
        alpha[: int((380 + 60 * k) * sy), x] = 1.0
    # soft top edge (the real hall continues above the photo)
    fade = int(150 * sy)
    alpha[:fade, :] *= np.linspace(0.0, 1.0, fade, dtype=np.float32)[:, None] ** 1.3
    al = Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.5))
    rgba = out.convert("RGBA")
    rgba.putalpha(al)
    # 2x (Lanczos) + a gentle unsharp mask: the photo is only 1678 px wide, this keeps edges crisper in the headset
    big = rgba.resize((rgba.width * 2, rgba.height * 2), Image.LANCZOS)
    rgb = big.convert("RGB").filter(ImageFilter.UnsharpMask(radius=1.8, percent=85, threshold=3))
    big = Image.merge("RGBA", (*rgb.split(), big.getchannel("A")))
    big.save(os.path.join(OUT, "Photo_Backdrop.png"))
    rgba = big
    print("Photo_Backdrop.png", rgba.size)


def steel():
    # clean steel only (no gloves, bottle, hands, sun patch): the strip left of the cloth, above the hands
    patch = im.crop(box(125, 492, 535, 655))
    pw, ph = patch.size
    # mirrored 2x2 tiling makes it seamless; the texture is 2:1 (tile = about 1.0 m x 0.5 m on the table)
    tile = Image.new("RGB", (pw * 2, ph * 2))
    tile.paste(patch, (0, 0))
    tile.paste(patch.transpose(Image.FLIP_LEFT_RIGHT), (pw, 0))
    tile.paste(patch.transpose(Image.FLIP_TOP_BOTTOM), (0, ph))
    tile.paste(patch.transpose(Image.ROTATE_180), (pw, ph))
    tile = tile.resize((1024, 512), Image.LANCZOS)
    # stainless: brighter silver, stronger scratches and streaks
    tile = ImageEnhance.Contrast(tile).enhance(1.35)
    tile = ImageEnhance.Brightness(tile).enhance(1.18)
    tile.save(os.path.join(OUT, "Photo_Steel.png"))
    print("Photo_Steel.png")


def tabletop():
    """The whole table top of the photo as ONE texture (same pixel space as the photo, so the builder can project it onto the
    table from the learner's spot): the tray / bottle, the cloth, the gloves and the hands are painted out with clean steel."""
    a = np.asarray(im, dtype=np.float32).copy()
    H_, W_ = a.shape[0], a.shape[1]
    # 1. the bottle's foot and the tray (x 555..955, rows 372..612) <- the clean steel and lip to the left, mirrored
    x0, x1 = int(555 * sx), int(955 * sx)
    r0, r1 = int(372 * sy), int(612 * sy)
    w = x1 - x0
    a[r0:r1, x0:x1] = a[r0:r1, x0 - w:x0][:, ::-1]
    # 2. the table top (rows 450 down, columns clamped inside the table edges): clean rows 456..545 ping-pong mirrored,
    #    so cloth, gloves and hands disappear and no slanted table edge is repeated
    top, bot = int(456 * sy), int(545 * sy)
    period = bot - top
    cl, cr = int(255 * sx), int(1565 * sx)
    out = a.copy()
    cols = np.clip(np.arange(W_), cl, cr)
    for y in range(int(450 * sy), H_):
        if y <= bot:
            row = a[y]
        else:
            k = (y - bot) % (2 * period)
            row = a[bot - k] if k < period else a[top + (k - period)]
        out[y] = row[cols]
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "Photo_TableTop.png"))
    print("Photo_TableTop.png")


def reflection():
    """Horizontal-cross cubemap for the table's reflections: all four sides show the hall of the photo (direction-proof),
    the ceiling is dark grey with tube lights, the floor dark green-grey."""
    f = 512
    hall = im.crop(box(0, 0, 1000, 376)).resize((f, f), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.2))
    # brighten a little so the metal picks up the daylight and the lights
    hall = Image.eval(hall, lambda v: min(255, int(v * 1.25)))
    ceil = Image.new("RGB", (f, f), (62, 64, 62))
    cd = ImageDraw.Draw(ceil)
    for i in range(4):
        cd.rectangle((70 + i * 105, 120, 120 + i * 105, 400), fill=(235, 240, 230))
    ceil = ceil.filter(ImageFilter.GaussianBlur(6))
    floor = Image.new("RGB", (f, f), (66, 82, 76))
    cross = Image.new("RGB", (4 * f, 3 * f), (40, 40, 40))
    cross.paste(ceil, (f, 0))        # +Y
    cross.paste(hall, (0, f))        # -X
    cross.paste(hall, (f, f))        # +Z
    cross.paste(hall, (2 * f, f))    # +X
    cross.paste(hall, (3 * f, f))    # -Z
    cross.paste(floor, (f, 2 * f))   # -Y
    cross.save(os.path.join(OUT, "Photo_ReflectionCross.png"))
    print("Photo_ReflectionCross.png")


def cloth():
    patch = im.crop(box(560, 700, 760, 820)).resize((256, 256), Image.LANCZOS)
    patch.save(os.path.join(OUT, "Photo_Cloth.png"))
    print("Photo_Cloth.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    backdrop()
    steel()
    cloth()
    tabletop()
    reflection()
