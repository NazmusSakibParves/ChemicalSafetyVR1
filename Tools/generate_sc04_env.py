"""Generates the SC04 clean-lab PBR textures: glossy wall tiles, epoxy floor, brushed steel, ceiling panels, soft AO decal.

Output (re-run: python Tools/generate_sc04_env.py) in Assets/Art/Presentable/2D/Sc04Tex:
  Tile_Albedo / Tile_Normal / Tile_MS       1024, 4x4 white tiles (0.6 m per tile image, 15 cm per tile): grout, slight tone
                                            variation per tile, glossy, small chips
  Epoxy_Albedo / Epoxy_Normal / Epoxy_MS    1024, tiles about 2 m: grey epoxy with mottling, fine speckle, foot-scuff, glossy
  Steel_Albedo / Steel_Normal / Steel_MS    1024, tiles about 1 m: horizontal brushed stainless, metallic
  Ceiling_Albedo / Ceiling_Normal           1024, 2x2 acoustic ceiling panels with a T-bar grid
  AO_Soft.png                               radial soft shadow (alpha) for contact / corner darkening
*_MS = metalness in R, smoothness in A (URP Lit metallic map).
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04Tex")
N = 1024


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


def ms_image(smooth, metal):
    a = (np.clip(smooth, 0, 1) * 255).astype(np.uint8)
    r = np.clip(metal * 255, 0, 255).astype(np.uint8) if isinstance(metal, np.ndarray) else np.full(a.shape, int(metal * 255), np.uint8)
    z = np.zeros_like(a)
    return Image.fromarray(np.dstack([r, z, z, a]), "RGBA")


def save(im, name):
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def blur(a, r):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0


def tiles():
    rng = np.random.default_rng(3)
    grid = 4
    cell = N // grid
    yy, xx = np.mgrid[0:N, 0:N]
    fx = (xx % cell).astype(np.float32)
    fy = (yy % cell).astype(np.float32)
    edge = np.minimum(np.minimum(fx, cell - 1 - fx), np.minimum(fy, cell - 1 - fy))
    grout = np.clip(1.0 - edge / 3.5, 0, 1)            # 1 at the joint centre
    grout = blur(grout, 0.9)
    # per tile tone
    tone = np.zeros((N, N), np.float32)
    for j in range(grid):
        for i in range(grid):
            tone[j * cell:(j + 1) * cell, i * cell:(i + 1) * cell] = rng.uniform(-0.025, 0.025)
    fine = fbm(N, 7, 40, 3)
    mid = fbm(N, 17, 6, 4)
    chips = (rng.random((N, N)) > 0.9993).astype(np.float32)
    chips = blur(chips, 1.5) * 6.0
    base = np.array([238, 240, 240], np.float32)
    img = base[None, None, :] * (1.0 + tone[..., None] + 0.02 * (mid[..., None] - 0.5) + 0.015 * (fine[..., None] - 0.5))
    gcol = np.array([205, 207, 205], np.float32)
    img = img * (1 - grout[..., None]) + gcol[None, None, :] * grout[..., None] * (0.95 + 0.1 * fine[..., None])
    img = img - chips[..., None] * 18
    # soft dirt in the joints, a little darker near the edge of each tile
    img = img * (1 - 0.06 * np.clip(1.0 - edge / 18.0, 0, 1)[..., None])
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Tile_Albedo")
    height = 0.55 - 0.5 * grout + 0.02 * (fine - 0.5) - 0.1 * chips
    save(normal_from_height(height.astype(np.float32), 5.0), "Tile_Normal")
    smooth = 0.86 - 0.5 * grout - 0.18 * np.clip(mid - 0.55, 0, 1) - 0.1 * chips
    save(ms_image(smooth, 0.0), "Tile_MS")


def epoxy():
    rng = np.random.default_rng(11)
    big = fbm(N, 31, 3)
    mid = fbm(N, 41, 9)
    fine = fbm(N, 51, 64, 3)
    speck = rng.random((N, N)).astype(np.float32)
    base = np.array([128, 133, 136], np.float32)
    img = base[None, None, :] * (0.93 + 0.14 * big[..., None] + 0.05 * (mid[..., None] - 0.5))
    # white and dark flecks like a quartz-flake epoxy floor
    light = (speck > 0.985).astype(np.float32)
    dark = (speck < 0.012).astype(np.float32)
    img = img + light[..., None] * 38 - dark[..., None] * 34
    # long faint scuff lines (trolley / foot traffic)
    scuff = np.clip(fbm(N, 61, 3, 3, sx=0.25, sy=4.0) - 0.62, 0, 1) * 2.2
    img = img * (1 - 0.1 * scuff[..., None])
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Epoxy_Albedo")
    height = 0.5 + 0.03 * (fine - 0.5) + 0.02 * (mid - 0.5) + 0.04 * light - 0.04 * dark
    save(normal_from_height(height.astype(np.float32), 3.0), "Epoxy_Normal")
    smooth = 0.78 - 0.25 * scuff - 0.12 * np.clip(mid - 0.6, 0, 1)
    save(ms_image(smooth, 0.0), "Epoxy_MS")


def steel():
    rng = np.random.default_rng(23)
    # brushed along X: noise stretched horizontally
    streak = fbm(N, 71, 4, 4, sx=0.12, sy=60.0)
    fine = fbm(N, 81, 3, 3, sx=0.05, sy=140.0)
    mid = fbm(N, 91, 5, 3)
    base = np.array([176, 180, 184], np.float32)
    img = base[None, None, :] * (0.86 + 0.2 * streak[..., None] + 0.08 * (fine[..., None] - 0.5) + 0.06 * (mid[..., None] - 0.5))
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Steel_Albedo")
    height = 0.5 + 0.1 * (streak - 0.5) + 0.12 * (fine - 0.5)
    save(normal_from_height(height.astype(np.float32), 2.2), "Steel_Normal")
    smooth = 0.52 + 0.22 * (fine - 0.5) + 0.1 * (mid - 0.5)
    save(ms_image(smooth, 0.92), "Steel_MS")


def ceiling():
    rng = np.random.default_rng(37)
    grid = 2
    cell = N // grid
    yy, xx = np.mgrid[0:N, 0:N]
    fx = (xx % cell).astype(np.float32)
    fy = (yy % cell).astype(np.float32)
    edge = np.minimum(np.minimum(fx, cell - 1 - fx), np.minimum(fy, cell - 1 - fy))
    bar = blur(np.clip(1.0 - edge / 6.0, 0, 1), 0.8)
    fine = fbm(N, 101, 90, 3)
    pits = (rng.random((N, N)) > 0.93).astype(np.float32)
    pits = blur(pits, 0.9)
    base = np.array([226, 228, 226], np.float32)
    img = base[None, None, :] * (0.97 + 0.04 * (fine[..., None] - 0.5)) - pits[..., None] * 16
    img = img * (1 - bar[..., None]) + np.array([200, 202, 202], np.float32)[None, None, :] * bar[..., None]
    save(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB"), "Ceiling_Albedo")
    height = 0.5 + 0.2 * bar - 0.12 * pits + 0.02 * (fine - 0.5)
    save(normal_from_height(height.astype(np.float32), 3.0), "Ceiling_Normal")


def ao():
    n = 128
    y, x = np.mgrid[0:n, 0:n]
    d = np.sqrt(((x - n / 2) / (n / 2)) ** 2 + ((y - n / 2) / (n / 2)) ** 2)
    a = np.clip(1 - d, 0, 1) ** 1.5
    img = np.zeros((n, n, 4), np.uint8)
    img[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(img, "RGBA"), "AO_Soft")


if __name__ == "__main__":
    tiles()
    epoxy()
    steel()
    ceiling()
    ao()
