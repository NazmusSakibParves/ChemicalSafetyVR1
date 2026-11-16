"""Scenario 21 room textures from the client's BG.jpg (the reference picture of the room) -> Assets/Art/Presentable/3D/Sc21/Tex.

Cut from the photo (so the colours / grime are the client's): concrete wall, brick pier, notice board with its papers, the factory hall seen through the
doorway. Made procedurally with colours sampled from the photo: floor, ceiling, table top (the photo shows them only in strong perspective).

Run: python -I Tools/generate_sc21_textures.py "<path to BG.jpg>" ["<path to scene06.jpg>"]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc21", "Tex")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(21)
bg = Image.open(sys.argv[1]).convert("RGB")


def mean(box):
    a = np.asarray(bg.crop(box), dtype=np.float32)
    return a.reshape(-1, 3).mean(0)


def noise(size, cells, seed_off=0):
    base = rng.random((cells, cells)).astype(np.float32)
    im = Image.fromarray((base * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255.0


def fbm(size, octaves=(4, 8, 16, 32, 64, 128), weights=(0.34, 0.26, 0.18, 0.12, 0.07, 0.03)):
    f = np.zeros((size, size), np.float32)
    for c, w in zip(octaves, weights):
        f += w * noise(size, c)
    return f / sum(weights)


def tileable(arr):
    """make a tile seamless by cross-fading the shifted copy over the seams"""
    h, w = arr.shape[:2]
    sh = np.roll(np.roll(arr, h // 2, 0), w // 2, 1)
    yy, xx = np.mgrid[0:h, 0:w]
    m = np.minimum(np.minimum(xx, w - 1 - xx) / (w * 0.5), np.minimum(yy, h - 1 - yy) / (h * 0.5))
    m = np.clip(m * 2.0, 0, 1)[..., None] if arr.ndim == 3 else np.clip(m * 2.0, 0, 1)
    return arr * m + sh * (1 - m)


def save(name, arr):
    a = np.clip(arr, 0, 255).astype(np.uint8)
    Image.fromarray(a).save(os.path.join(OUT, name + ".png"))
    print(name, a.shape)


# ---- wall: the flat concrete wall in the middle of the photo (stains, dark runs), made seamless
w = bg.crop((640, 95, 960, 470)).resize((512, 512), Image.LANCZOS)
wa = np.asarray(w, dtype=np.float32)
save("Sc21_Wall", tileable(wa))

# ---- brick pier + patch of the photo (corner column at the doorway)
b = bg.crop((930, 50, 1040, 235)).resize((256, 512), Image.LANCZOS)
save("Sc21_Brick", np.asarray(b, dtype=np.float32))

# ---- notice board with the papers (cork, frame, the five notes): used as a picture on the wall
c = bg.crop((34, 100, 306, 336)).resize((1024, 888), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1.2, 60, 2))
save("Sc21_Board", np.asarray(c, dtype=np.float32))

# ---- the factory hall seen through the doorway
hbg = bg.crop((1042, 90, 1345, 520)).resize((512, 724), Image.LANCZOS)
save("Sc21_HallBG", np.asarray(hbg, dtype=np.float32))

# ---- floor: dark blue-grey stained concrete (photo: right bottom corner)
fc = mean((1050, 500, 1330, 700)) * 1.7 + 6
f = fbm(1024)
f2 = fbm(1024, octaves=(2, 3, 6, 12), weights=(0.4, 0.3, 0.2, 0.1))
base = np.ones((1024, 1024, 3), np.float32) * fc[None, None, :]
dark = np.clip((f2 - 0.55) * 4.0, 0, 1)[..., None]            # oily dark patches
base = base * (0.72 + 0.5 * f[..., None]) * (1.0 - 0.45 * dark)
spl = np.clip((fbm(1024, (3, 6, 9)) - 0.62) * 6.0, 0, 1)[..., None]  # pale dried-chemical patches
base = base + spl * np.array([22, 24, 26], np.float32)
# hairline cracks
for _ in range(14):
    x, y = rng.integers(0, 1024, 2)
    for _ in range(rng.integers(80, 260)):
        x = int(np.clip(x + rng.integers(-2, 3), 0, 1023))
        y = int(np.clip(y + rng.integers(-1, 3), 0, 1023))
        base[y, x] *= 0.45
base = np.asarray(Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)), dtype=np.float32)
save("Sc21_Floor", tileable(base))

# ---- ceiling: rough grey concrete, water marks
cc = mean((0, 0, 500, 40)) * 0.9 + np.array([20, 20, 20], np.float32)
g = fbm(1024)
ceil = np.ones((1024, 1024, 3), np.float32) * cc[None, None, :] * (0.7 + 0.55 * g[..., None])
stain = np.clip((fbm(1024, (3, 5, 9)) - 0.6) * 5.0, 0, 1)[..., None]
ceil = ceil * (1 - 0.35 * stain) + stain * np.array([4, 2, -4], np.float32)
save("Sc21_Ceiling", tileable(ceil))

# ---- table top: off-white chipped concrete with paint drips and dark ring stains (photo: the table)
tt = np.array([1.0, 0.985, 0.95], np.float32) * float(mean((200, 580, 700, 650)).mean())
t = fbm(1024)
top = np.ones((1024, 1024, 3), np.float32) * (tt[None, None, :] * 1.18) * (0.78 + 0.4 * t[..., None])
ring = np.clip((fbm(1024, (4, 7, 12)) - 0.66) * 7.0, 0, 1)[..., None]
top = top * (1 - 0.55 * ring) + ring * np.array([-20, -34, -46], np.float32)
chip = np.clip((fbm(1024, (16, 32, 64)) - 0.7) * 8.0, 0, 1)[..., None]
top = top * (1 - 0.5 * chip)
save("Sc21_TableTop", tileable(top))

if len(sys.argv) > 2:
    s6 = Image.open(sys.argv[2]).convert("RGB")
    # safe-shower sign (green) and the duct-free floor marking green/yellow of the final picture
    sign = s6.crop((938, 192, 1006, 290)).resize((256, 360), Image.LANCZOS)
    save("Sc21_ShowerSign", np.asarray(sign, dtype=np.float32))
print("done")


# ================= aged variants (client: "the room must look old like my workflow picture") =================
def streaks(size, seed_cells=(6, 24), stretch=14):
    """vertical run marks: noise stretched along y"""
    a = rng.random((max(3, seed_cells[0] // 2), 90)).astype(np.float32)   # few rows, many columns = narrow vertical runs
    im = Image.fromarray((a * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255.0


def aged_wall(src_name="Sc21_Wall"):
    base = np.asarray(Image.open(os.path.join(OUT, src_name + ".png")).convert("RGB").resize((1024, 1024), Image.LANCZOS), dtype=np.float32)
    S = 1024
    # client: the wall is WHITE, but old - keep the photo's stain structure, lift it to old off-white paint
    lum = base.mean(axis=2)
    low = np.asarray(Image.fromarray(np.clip(lum, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(70)), dtype=np.float32) + 1.0
    flat = np.clip(lum / low, 0.35, 1.6)          # stains and texture without the photo's big light / dark patches (no visible tile quadrants)
    base = np.clip(flat[..., None] * np.array([206, 203, 194], np.float32), 0, 255)
    yy = np.linspace(0, 1, S, dtype=np.float32)[:, None]                  # 0 top .. 1 bottom
    damp = np.clip((yy - 0.45) / 0.55, 0, 1) ** 1.4                         # rising damp / grime near the floor
    st = streaks(S)
    run = np.clip((st - 0.52) * 3.2, 0, 1)                                  # dark vertical water runs
    blot = np.asarray(Image.fromarray((np.clip((fbm(S, (2, 3, 5)) - 0.52) * 2.2, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(18)), dtype=np.float32) / 255.0   # big soft stains
    fine = fbm(S, (32, 64, 128), (0.5, 0.3, 0.2))
    k = (0.98 - 0.30 * damp - 0.04 * run - 0.16 * blot)[..., None]
    out = base * k * (0.88 + 0.30 * fine[..., None])
    # pale plaster patches where the paint has flaked, with a dark rim
    patch = np.clip((fbm(S, (3, 5, 9)) - 0.74) * 4.0, 0, 1) * 0.6
    rim = np.clip(patch - np.asarray(Image.fromarray((patch * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4)), dtype=np.float32) / 255.0, 0, 1)[..., None]
    out = out * (1 - 0.2 * patch[..., None]) + patch[..., None] * np.array([22, 20, 18], np.float32) - rim * 40
    # hairline cracks
    for _ in range(9):
        x, y = rng.integers(0, S, 2)
        for _ in range(rng.integers(120, 380)):
            x = int(np.clip(x + rng.integers(-2, 3), 0, S - 1))
            y = int(np.clip(y + rng.integers(-1, 3), 0, S - 1))
            out[y, x] *= 0.30
    out = np.asarray(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), dtype=np.float32)
    save("Sc21_WallAged", out)


def puddles():
    """glossy dark wet patches with a soft rim, alpha-blended over the floor (RGBA)"""
    S = 1024
    f = fbm(S, (2, 3, 5, 9))
    m = np.clip((f - 0.58) * 6.0, 0, 1)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(5)), dtype=np.float32) / 255.0
    rgb = np.ones((S, S, 3), np.float32) * np.array([14, 18, 22], np.float32)
    a = (m * 0.85 * 255).astype(np.uint8)
    img = np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), a])
    Image.fromarray(img, "RGBA").save(os.path.join(OUT, "Sc21_Puddles.png"))
    print("Sc21_Puddles")


def grime_decal():
    """soft dark smudge for corners / below pipes (RGBA)"""
    S = 512
    yy, xx = np.mgrid[0:S, 0:S]
    r = np.hypot((xx - S / 2) / (S / 2), (yy - S / 2) / (S / 2))
    a = np.clip(1 - r, 0, 1) ** 1.3 * (0.55 + 0.45 * fbm(S, (4, 8, 16)))
    img = np.dstack([np.full((S, S, 3), 12, np.uint8), (a * 200).astype(np.uint8)])
    Image.fromarray(img, "RGBA").save(os.path.join(OUT, "Sc21_Smudge.png"))
    print("Sc21_Smudge")


aged_wall()
puddles()
grime_decal()
# table top: old off-white concrete, covered in paint smears (blue, green, rust, black), drips, ring stains from tins, worn dark rim
S = 1024
tbase = np.asarray(Image.open(os.path.join(OUT, "Sc21_TableTop.png")).convert("RGB"), dtype=np.float32)
tbase = tbase * 0.97
def smear(color, cells, thr, soft):
    m = np.clip((fbm(S, cells) - thr) * 6.0, 0, 1)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(soft)), dtype=np.float32) / 255.0
    return m[..., None], np.array(color, np.float32)
out = tbase.copy()
for col, cells, thr, soft, w in (((38, 66, 130), (3, 6, 12), 0.66, 2.0, 0.85), ((52, 108, 78), (3, 5, 10), 0.68, 2.0, 0.8),
                                 ((118, 58, 28), (2, 5, 9), 0.64, 2.5, 0.8), ((26, 24, 24), (3, 7, 14), 0.70, 1.6, 0.8)):
    m, c = smear(col, cells, thr, soft)
    out = out * (1 - w * m) + c * (w * m)
drip = np.clip((streaks(S, (8, 40), 20) - 0.60) * 5.0, 0, 1)[..., None]
out = out * (1 - 0.07 * drip) + drip * np.array([-3, -2, -5], np.float32)
ring2 = np.clip((fbm(S, (6, 11, 20)) - 0.64) * 5.0, 0, 1)[..., None]
out = out * (1 - 0.35 * ring2)
edge = np.minimum(np.minimum(np.arange(S)[None, :], S - 1 - np.arange(S)[None, :]), np.minimum(np.arange(S)[:, None], S - 1 - np.arange(S)[:, None])) / 70.0
out = out * (0.55 + 0.45 * np.clip(edge, 0, 1)[..., None])
save("Sc21_TableTop", out)


# ================= sharp notice board (client picture: cork board with five papers) =================
from PIL import ImageFont


def board_sharp():
    W, H = 2048, 1780
    base = np.ones((H, W, 3), np.float32) * np.array([176, 136, 92], np.float32)
    nz = np.asarray(Image.fromarray((rng.random((H // 6, W // 6)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), dtype=np.float32) / 255.0
    nz2 = np.asarray(Image.fromarray((rng.random((H // 2, W // 2)) * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR), dtype=np.float32) / 255.0
    cork = base * (0.78 + 0.22 * nz[..., None] + 0.16 * (nz2[..., None] - 0.5))
    img = Image.fromarray(np.clip(cork, 0, 255).astype(np.uint8)).convert("RGBA")
    d = ImageDraw.Draw(img)

    def font(names, size):
        for n in names:
            try:
                return ImageFont.truetype("C:/Windows/Fonts/" + n, size)
            except Exception:
                pass
        return ImageFont.load_default()

    hand = ["inkfree.ttf", "segoepr.ttf", "comic.ttf", "arial.ttf"]
    sans = ["arial.ttf", "segoeui.ttf"]

    def paper(box, color, angle, draw_fn):
        x0, y0, x1, y1 = [int(v) for v in box]
        pw, ph = x1 - x0, y1 - y0
        sheet = Image.new("RGBA", (pw + 40, ph + 40), (0, 0, 0, 0))
        sd = ImageDraw.Draw(sheet)
        shadow = Image.new("RGBA", sheet.size, (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rectangle([26, 28, 26 + pw, 28 + ph], fill=(0, 0, 0, 110))
        shadow = shadow.filter(ImageFilter.GaussianBlur(7))
        sheet = Image.alpha_composite(sheet, shadow)
        sd = ImageDraw.Draw(sheet)
        sd.rectangle([20, 20, 20 + pw, 20 + ph], fill=color)
        draw_fn(sd, 20, 20, pw, ph)
        # pin
        sd.ellipse([20 + pw // 2 - 13, 20 + 10, 20 + pw // 2 + 13, 20 + 36], fill=(190, 30, 30, 255), outline=(90, 10, 10, 255), width=3)
        sheet = sheet.rotate(angle, resample=Image.BICUBIC, expand=True)
        img.alpha_composite(sheet, (x0 - 20 - (sheet.size[0] - pw - 40) // 2, y0 - 20 - (sheet.size[1] - ph - 40) // 2))

    def lines(sd, x, y, w, h, n=17, title=True):
        ink = (60, 60, 64, 255)
        if title:
            sd.rectangle([x + w * 0.12, y + h * 0.07, x + w * 0.88, y + h * 0.09], fill=ink)
        for i in range(n):
            yy = y + h * 0.15 + i * (h * 0.80 / n)
            ln = w * (0.80 if i % 5 != 4 else 0.45)
            sd.rectangle([x + w * 0.10, yy, x + w * 0.10 + ln, yy + 4], fill=(78, 78, 84, 255))

    paper((120, 190, 640, 1120), (243, 242, 236, 255), 0.8, lambda sd, x, y, w, h: lines(sd, x, y, w, h))
    paper((700, 200, 1190, 1130), (246, 245, 240, 255), -0.6, lambda sd, x, y, w, h: lines(sd, x, y, w, h, 19))

    def shift_note(sd, x, y, w, h):
        f1 = font(hand, 64)
        sd.text((x + 22, y + 18), "SHIFT SCHEDULE", fill=(40, 40, 46, 255), font=f1)
        f2 = font(hand, 56)
        sd.text((x + 22, y + 108), "8:00 AM - 2:00 PM", fill=(40, 40, 46, 255), font=f2)
        sd.text((x + 22, y + 178), "2:00 PM - 8:00 PM", fill=(40, 40, 46, 255), font=f2)

    paper((1240, 200, 1800, 470), (247, 246, 238, 255), 1.0, shift_note)

    def warn(sd, x, y, w, h):
        f1 = font(hand, 74)
        sd.text((x + 26, y + 40), "SAFETY", fill=(36, 36, 40, 255), font=f1)
        sd.text((x + 26, y + 128), "WARNING", fill=(36, 36, 40, 255), font=f1)
        sd.line([x + 40, y + 300, x + 120, y + 340], fill=(70, 70, 70, 255), width=6)
        sd.line([x + 120, y + 340, x + 300, y + 262], fill=(70, 70, 70, 255), width=6)
        for i in range(3):
            sd.rectangle([x + 30, y + 400 + i * 60, x + w - 40 - (i % 2) * 90, y + 408 + i * 60], fill=(120, 110, 90, 255))

    paper((1330, 560, 1760, 1110), (243, 232, 188, 255), -0.8, warn)

    def roster(sd, x, y, w, h):
        f1 = font(hand, 58)
        sd.text((x + 20, y + 30), "SHIFT ROSTER", fill=(40, 40, 46, 255), font=f1)
        sd.text((x + 20, y + 110), "SCHEDULE", fill=(40, 40, 46, 255), font=f1)

    paper((140, 1210, 640, 1500), (243, 242, 236, 255), -1.2, roster)

    def process(sd, x, y, w, h):
        f1 = font(hand, 56)
        for i, wd in enumerate(("PROCESS", "INSTRUCTION", "ON BOARD")):
            sd.text((x + 22, y + 40 + i * 84), wd, fill=(40, 40, 46, 255), font=f1)

    paper((720, 1300, 1170, 1660), (246, 245, 240, 255), 0.9, process)

    # aluminium frame
    fr = 46
    d = ImageDraw.Draw(img)
    for i in range(fr):
        c = int(150 + 70 * (1 - abs(i - fr / 2) / (fr / 2)))
        d.rectangle([i, i, W - 1 - i, H - 1 - i], outline=(c, c, c + 4, 255))
    out = np.asarray(img.convert("RGB"))
    Image.fromarray(out).save(os.path.join(OUT, "Sc21_Board.png"))
    print("Sc21_Board (sharp)", out.shape)


from PIL import ImageDraw
board_sharp()
# brick: no dark right strip
b2 = bg.crop((932, 52, 1030, 232)).resize((392, 720), Image.LANCZOS)
save("Sc21_Brick", np.asarray(b2, dtype=np.float32))
