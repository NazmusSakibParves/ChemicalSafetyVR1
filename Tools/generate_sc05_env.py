"""Generates the Scenario 5 dyeing-factory textures: grimy concrete wall, wet stained floor, dark ceiling, steam sprites,
the OSHA SDS holder label and the gas detector readout.

Output (re-run: python Tools/generate_sc05_env.py) in Assets/Art/Presentable/2D/Sc05Tex:
  Wall_Albedo / Wall_Normal / Wall_MS        1024, tiles (about 2.4 m): board-marked concrete, soot, damp streaks, chips
  Floor_Albedo / Floor_Normal / Floor_MS     1024, tiles (about 2.4 m): dark worn concrete, wet dye puddles, oil, scuffs
  Ceil_Albedo / Ceil_Normal                  1024, tiles: dark soot-stained slab with form-board lines
  Steam_Puff.png / Steam_Wisp.png            soft cloud sprites (alpha) for the vapor particles
  Label_Sds.png                              OSHA "Safety Data Sheet" holder label with a sheet
  Detector.png                               gas detector face: red "85" ppm readout (bezel + screen)
*_MS = metalness in R, smoothness in A (URP Lit metallic map).
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc05Tex")
N = 1024
FONT = r"C:\Windows\Fonts\segoeui.ttf"
FONTB = r"C:\Windows\Fonts\segoeuib.ttf"


def tile_noise(size, cells_x, cells_y, seed):
    r = np.random.default_rng(seed)
    small = r.random((cells_y, cells_x)).astype(np.float32)
    pad = np.pad(small, 2, mode="wrap")
    w = int(round(size * (cells_x + 4) / cells_x))
    h = int(round(size * (cells_y + 4) / cells_y))
    im = Image.fromarray((pad * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    ox = int(round(2 * size / cells_x))
    oy = int(round(2 * size / cells_y))
    return np.asarray(im, dtype=np.float32)[oy:oy + size, ox:ox + size] / 255.0


def fbm(size, seed, base=4, octaves=5, sx=1.0, sy=1.0):
    out = np.zeros((size, size), np.float32)
    amp, tot = 0.5, 0.0
    cells = base
    for o in range(octaves):
        out += tile_noise(size, max(2, int(cells * sx)), max(2, int(cells * sy)), seed + o * 13) * amp
        tot += amp
        amp *= 0.52
        cells *= 2
    return out / tot


def normal_from_height(h, strength):
    gy, gx = np.gradient(h)
    nx, ny, nz = -gx * strength, gy * strength, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.dstack([nx / ln, ny / ln, nz / ln]) * 0.5 + 0.5
    return Image.fromarray((rgb * 255).astype(np.uint8), "RGB")


def ms_image(smooth, metal=0.0):
    a = (np.clip(smooth, 0, 1) * 255).astype(np.uint8)
    r = np.full(a.shape, int(metal * 255), np.uint8)
    z = np.zeros_like(a)
    return Image.fromarray(np.dstack([r, z, z, a]), "RGBA")


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def smooth01(x, lo, hi):
    t = np.clip((x - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def blur(a, r):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0


def wall():
    rng = np.random.default_rng(21)
    big = fbm(N, 201, 3)
    mid = fbm(N, 211, 9)
    fine = fbm(N, 221, 48, 3)
    grit = rng.random((N, N)).astype(np.float32)
    # horizontal board marks of poured concrete, every 128 px, with a slight offset noise
    yy = np.arange(N)[:, None].astype(np.float32)
    board = np.exp(-((yy % 128) - 2.0) ** 2 / 3.0) * np.ones((1, N), np.float32)
    board = np.clip(board + 0.25 * (fine - 0.5), 0, 1)
    ties = np.zeros((N, N), np.float32)           # form-tie holes
    for ty in range(64, N, 256):
        for tx in range(96, N, 256):
            ties[ty - 6:ty + 6, tx - 6:tx + 6] = 1.0
    ties = blur(ties, 2.0)
    base = np.array([112, 112, 108], np.float32)
    shade = 0.62 + 0.5 * big + 0.16 * (mid - 0.5)
    img = base[None, None, :] * shade[..., None] * (0.92 + 0.14 * grit[..., None] * 0.7 + 0.08 * fine[..., None])
    # soot rising from the floor, damp dye streaks coming down, board lines and tie holes
    streak = smooth01(fbm(N, 231, 3, 4, sx=3.0, sy=0.4), 0.58, 0.75)
    foot = np.clip((np.arange(N)[:, None] / N - 0.55) * 1.8, 0, 1) * np.ones((1, N), np.float32)
    img = img * (1 - 0.3 * streak[..., None] * np.array([0.8, 0.9, 1.0])[None, None, :])
    img = img * (1 - 0.18 * board[..., None]) * (1 - 0.5 * ties[..., None])
    img = img * (1 - 0.16 * foot[..., None])
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Wall_Albedo")
    height = 0.5 + 0.1 * (mid - 0.5) + 0.12 * (fine - 0.5) + 0.04 * (grit - 0.5) - 0.2 * board - 0.3 * ties
    save(normal_from_height(height.astype(np.float32), 4.0), "Wall_Normal")
    smooth = 0.14 + 0.14 * streak + 0.05 * (1 - big)
    save(ms_image(smooth), "Wall_MS")


def floor():
    rng = np.random.default_rng(31)
    big = fbm(N, 301, 3)
    mid = fbm(N, 311, 8)
    fine = fbm(N, 321, 56, 3)
    grit = rng.random((N, N)).astype(np.float32)
    base = np.array([74, 74, 72], np.float32)
    shade = 0.6 + 0.55 * big + 0.2 * (mid - 0.5)
    img = base[None, None, :] * shade[..., None] * (0.9 + 0.2 * grit[..., None] * 0.6 + 0.08 * fine[..., None])
    puddle = smooth01(fbm(N, 331, 4), 0.6, 0.72)            # wet patches
    dye = smooth01(fbm(N, 341, 5), 0.68, 0.8)               # blue and rust dye spills
    oil = smooth01(fbm(N, 351, 6), 0.66, 0.78)
    scuff = np.clip(fbm(N, 361, 3, 3, sx=0.3, sy=4.0) - 0.6, 0, 1) * 2.0
    img = img * (1 - 0.3 * oil[..., None]) * (1 - 0.1 * scuff[..., None])
    img = img * (1 - 0.25 * puddle[..., None])
    img = img + dye[..., None] * np.array([-14, 4, 26], np.float32)[None, None, :] * 0.9
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Floor_Albedo")
    height = 0.5 + 0.12 * (mid - 0.5) + 0.12 * (fine - 0.5) + 0.05 * (grit - 0.5) - 0.12 * puddle
    save(normal_from_height(height.astype(np.float32), 3.5), "Floor_Normal")
    smooth = 0.2 + 0.7 * puddle + 0.15 * oil - 0.1 * scuff + 0.05 * (1 - big)
    save(ms_image(np.clip(smooth, 0.08, 0.94)), "Floor_MS")


def ceiling():
    rng = np.random.default_rng(41)
    big = fbm(N, 401, 3)
    fine = fbm(N, 411, 40, 3)
    yy = np.arange(N)[:, None].astype(np.float32)
    lines = np.exp(-((yy % 256) - 3.0) ** 2 / 5.0) * np.ones((1, N), np.float32)
    soot = smooth01(fbm(N, 421, 4), 0.5, 0.75)
    base = np.array([68, 67, 64], np.float32)
    img = base[None, None, :] * (0.7 + 0.45 * big[..., None]) * (0.9 + 0.12 * fine[..., None])
    img = img * (1 - 0.35 * soot[..., None]) * (1 - 0.3 * lines[..., None])
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Ceil_Albedo")
    height = 0.5 + 0.1 * (fine - 0.5) - 0.3 * lines
    save(normal_from_height(height.astype(np.float32), 3.0), "Ceil_Normal")


def steam():
    n = 256
    y, x = np.mgrid[0:n, 0:n]
    d = np.sqrt(((x - n / 2) / (n / 2)) ** 2 + ((y - n / 2) / (n / 2)) ** 2)
    cloud = fbm(n, 501, 4, 5)
    a = np.clip(1 - d, 0, 1) ** 1.3 * (0.55 + 0.9 * cloud)
    a = np.clip(a, 0, 1)
    img = np.zeros((n, n, 4), np.uint8)
    img[..., :3] = 235
    img[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(img, "RGBA"), "Steam_Puff")
    # a tall thin wisp
    w, h = 128, 256
    yy, xx = np.mgrid[0:h, 0:w]
    dx = (xx - w / 2) / (w / 2)
    dy = (yy - h / 2) / (h / 2)
    wisp = np.clip(1 - np.sqrt(dx ** 2 * 2.2 + dy ** 2 * 0.6), 0, 1) ** 1.4
    noise = fbm(256, 511, 3, 4)[:h, :w]
    a2 = np.clip(wisp * (0.5 + noise), 0, 1)
    img2 = np.zeros((h, w, 4), np.uint8)
    img2[..., :3] = 232
    img2[..., 3] = (a2 * 255).astype(np.uint8)
    save(Image.fromarray(img2, "RGBA"), "Steam_Wisp")


def label_sds():
    img = Image.new("RGBA", (512, 640), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((16, 16, 496, 624), 20, fill=(170, 176, 178, 120), outline=(120, 126, 130, 255), width=6)
    d.rectangle((44, 60, 468, 604), fill=(244, 242, 236, 255))
    d.text((70, 76), "OSHA", font=ImageFont.truetype(FONTB, 46), fill=(30, 60, 140, 255))
    d.text((70, 134), "Safety Data Sheet", font=ImageFont.truetype(FONTB, 34), fill=(40, 40, 44, 255))
    d.text((150, 190), "SDS", font=ImageFont.truetype(FONTB, 96), fill=(196, 28, 30, 255))
    for i in range(9):
        d.rectangle((70, 330 + i * 28, 440 - (i % 3) * 40, 340 + i * 28), fill=(150, 150, 154, 255))
    save(img, "Label_Sds")


def detector():
    img = Image.new("RGBA", (256, 400), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((8, 8, 248, 392), 36, fill=(44, 48, 52, 255), outline=(20, 22, 24, 255), width=6)
    d.rounded_rectangle((34, 40, 222, 186), 14, fill=(20, 4, 4, 255), outline=(70, 70, 74, 255), width=4)
    d.text((54, 54), "85", font=ImageFont.truetype(FONTB, 104), fill=(255, 40, 30, 255))
    d.text((150, 150), "ppm", font=ImageFont.truetype(FONTB, 26), fill=(255, 70, 50, 255))
    d.rounded_rectangle((38, 28, 218, 40), 5, fill=(70, 220, 90, 255))        # green status strip
    d.ellipse((92, 224, 164, 296), fill=(30, 32, 36, 255), outline=(90, 94, 98, 255), width=5)
    for i in range(3):
        d.rectangle((60 + i * 56, 330, 90 + i * 56, 350), fill=(70, 74, 78, 255))
    save(img, "Detector")


if __name__ == "__main__":
    wall()
    floor()
    ceiling()
    steam()
    label_sds()
    detector()
