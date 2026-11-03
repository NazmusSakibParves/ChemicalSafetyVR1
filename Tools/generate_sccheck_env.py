"""Procedural textures of the Supervisor Pre-Task Safety Check hall (scenarios 13-18): red brick, concrete floor, steel wall panel.

    python Tools/generate_sccheck_env.py

Output: Assets/Art/Presentable/2D/ScCheckTex/*.png (tileable, 1024 px).
"""
import os
import random

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "ScCheckTex")
os.makedirs(OUT, exist_ok=True)
rng = random.Random(13)
np.random.seed(13)
N = 1024


def noise(size, scale, amp):
    small = np.random.rand(max(2, size // scale), max(2, size // scale))
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return (np.asarray(img, dtype=np.float32) / 255.0 - 0.5) * amp


def brick():
    rows, cols = 16, 8  # one tile = 16 courses, 8 bricks wide
    bh, bw = N // rows, N // cols
    img = np.zeros((N, N, 3), dtype=np.float32)
    mortar = np.array([168, 160, 150], dtype=np.float32)
    img[:] = mortar
    for r in range(rows):
        off = (bw // 2) if r % 2 else 0
        for c in range(-1, cols + 1):
            x0 = c * bw + off + 3
            y0 = r * bh + 3
            base = np.array([150 + rng.randint(-26, 26), 62 + rng.randint(-14, 14), 46 + rng.randint(-10, 10)], dtype=np.float32)
            tone = rng.uniform(0.82, 1.12)
            for y in range(y0, min(N, y0 + bh - 6)):
                for x in range(x0, x0 + bw - 6):
                    img[y % N, x % N] = base * tone
    img += noise(N, 6, 40)[..., None] * np.array([1.0, 0.6, 0.5])
    img += noise(N, 64, 26)[..., None]
    img = np.clip(img, 0, 255).astype(np.uint8)
    Image.fromarray(img).filter(ImageFilter.GaussianBlur(0.8)).save(os.path.join(OUT, "ScCheck_Brick.png"))


def floor():
    base = np.full((N, N, 3), 172, dtype=np.float32)
    base += noise(N, 4, 22)[..., None] + noise(N, 32, 20)[..., None] + noise(N, 128, 18)[..., None]
    base *= np.array([1.0, 0.99, 0.96])
    img = np.clip(base, 0, 255).astype(np.uint8)
    im = Image.fromarray(img)
    # expansion joints
    px = im.load()
    for i in range(N):
        for t in range(3):
            px[i, (N // 2 + t) % N] = (92, 92, 90)
            px[(N // 2 + t) % N, i] = (92, 92, 90)
    im.save(os.path.join(OUT, "ScCheck_Floor.png"))


def panel():
    img = np.full((N, N, 3), 150, dtype=np.float32)
    for i in range(0, N, 128):
        img[:, i:i + 4] -= 38
    img += noise(N, 8, 16)[..., None]
    img *= np.array([0.96, 0.98, 1.02])
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "ScCheck_Panel.png"))


def form():
    from PIL import ImageDraw
    w, h = 512, 704
    im = Image.new("RGB", (w, h), (246, 245, 240))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w, 74], fill=(28, 52, 98))
    d.rectangle([28, 22, 250, 36], fill=(235, 238, 245))
    d.rectangle([28, 44, 170, 52], fill=(170, 185, 215))
    for i in range(14):
        y = 112 + i * 38
        d.line([28, y + 24, w - 28, y + 24], fill=(150, 160, 175), width=2)
        if i % 3 != 2:
            x = 36
            while x < w - 90:
                run = rng.randint(30, 90)
                d.rectangle([x, y + 8, x + run, y + 14], fill=(70, 78, 92))
                x += run + rng.randint(10, 22)
    for i in range(4):
        d.rectangle([34, 640 - i * 0, 52, 658], outline=(60, 70, 90), width=2) if i == 0 else None
    Image.fromarray(np.asarray(im)).save(os.path.join(OUT, "ScCheck_FormPaper.png"))


def normal_from_height(h, strength):
    gy, gx = np.gradient(h.astype(np.float32))
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(h, dtype=np.float32)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / ln, ny / ln, nz / ln], axis=-1)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))


def normals():
    # brick: raised bricks, sunken mortar (same layout as brick())
    rows, cols = 16, 8
    bh, bw = N // rows, N // cols
    hgt = np.zeros((N, N), dtype=np.float32)
    for r in range(rows):
        off = (bw // 2) if r % 2 else 0
        for c in range(-1, cols + 1):
            x0 = c * bw + off + 3
            y0 = r * bh + 3
            for y in range(y0, min(N, y0 + bh - 6)):
                for x in range(x0, x0 + bw - 6):
                    hgt[y % N, x % N] = 1.0
    hgt = np.asarray(Image.fromarray((hgt * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.2)), dtype=np.float32) / 255.0
    hgt += noise(N, 5, 0.25)
    normal_from_height(hgt * 6.0, 1.0).save(os.path.join(OUT, "ScCheck_Brick_Normal.png"))
    f = noise(N, 3, 1.0) + noise(N, 16, 1.0)
    normal_from_height(f * 3.0, 0.8).save(os.path.join(OUT, "ScCheck_Floor_Normal.png"))
    streak = np.tile(noise(N, 2, 1.0)[:1, :], (N, 1)) + noise(N, 4, 0.4)
    normal_from_height(streak * 2.0, 0.6).save(os.path.join(OUT, "ScCheck_Steel_Normal.png"))


def props():
    from PIL import ImageDraw, ImageFont
    def font(sz, bold=False):
        try:
            return ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf", sz)
        except Exception:
            return ImageFont.load_default()
    # tablet screen
    im = Image.new("RGB", (512, 352), (238, 242, 247))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 512, 44], fill=(32, 64, 120))
    d.text((18, 6), "SAFETY CHECKLIST", font=font(26, True), fill=(255, 255, 255))
    for i, t in enumerate(["1. Correct PPE worn?", "2. Ventilation active?", "3. Spill kit in place?", "4. SDS reviewed?"]):
        y = 70 + i * 62
        d.rectangle([22, y, 54, y + 32], outline=(32, 64, 120), width=3)
        d.text((72, y - 2), t, font=font(30), fill=(30, 40, 60))
    im.save(os.path.join(OUT, "ScCheck_TabletScreen.png"))
    # work order sheet
    im = Image.new("RGB", (512, 704), (248, 247, 243))
    d = ImageDraw.Draw(im)
    d.text((24, 18), "WORK ORDER", font=font(46, True), fill=(30, 36, 50))
    d.line([24, 80, 488, 80], fill=(40, 46, 60), width=3)
    d.text((24, 96), "HYDROCHLORIC ACID (HCL)", font=font(30, True), fill=(30, 36, 50))
    for i in range(12):
        y = 160 + i * 42
        d.line([24, y + 30, 488, y + 30], fill=(160, 168, 180), width=2)
        d.text((24, y), ["Task", "Location", "Supervisor", "PPE", "Hazard", "SDS", "Date", "Time", "Notes", "Signature", "Check", "Approval"][i], font=font(22), fill=(70, 78, 92))
    im.save(os.path.join(OUT, "ScCheck_WorkOrder.png"))
    # danger carton face
    im = Image.new("RGB", (512, 512), (170, 132, 86))
    d = ImageDraw.Draw(im)
    d.rectangle([40, 40, 472, 180], fill=(246, 196, 28))
    d.text((70, 62), "DANGER", font=font(84, True), fill=(20, 20, 20))
    d.rectangle([40, 190, 472, 472], fill=(246, 246, 240))
    d.ellipse([170, 215, 340, 385], outline=(20, 20, 20), width=10)
    d.text((100, 400), "CORROSIVE", font=font(64, True), fill=(20, 20, 20))
    im.save(os.path.join(OUT, "ScCheck_DangerCarton.png"))
    # contact shadow blob
    yy, xx = np.mgrid[0:128, 0:128]
    r = np.sqrt((xx - 64.0) ** 2 + (yy - 64.0) ** 2) / 64.0
    a = np.clip(1.0 - r, 0, 1) ** 1.6
    blob = np.zeros((128, 128, 4), dtype=np.uint8)
    blob[..., 3] = (a * 200).astype(np.uint8)
    Image.fromarray(blob, "RGBA").save(os.path.join(OUT, "ScCheck_Blob.png"))


brick()
floor()
panel()
form()
normals()
props()
print("done")
