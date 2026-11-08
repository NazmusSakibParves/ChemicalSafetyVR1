"""Scenario 7 garment-factory hall: bilingual wall signs (FIRE EXTINGUISHER / EMERGENCY EXIT) like the client's picture.
Run: python -I Tools/generate_sc07_factory.py
Writes Assets/Art/Presentable/2D/Sc07Tex/Sign_FireExt.png and Sign_EmergencyExit.png (needs Pillow with raqm for Bangla shaping).
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc07Tex")
FONT_BN = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")
FONT_EN = "C:/Windows/Fonts/arialbd.ttf"


def sign(name, bg, bn, en, icon):
    w, h = 1024, 256
    im = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(im)
    d.rectangle([6, 6, w - 7, h - 7], outline=(255, 255, 255), width=6)
    # icon block (white square with a simple pictogram)
    d.rectangle([22, 22, 226, h - 23], fill=(255, 255, 255) if icon == "ext" else bg)
    if icon == "ext":
        d.rounded_rectangle([88, 70, 150, 200], 18, fill=bg)
        d.rectangle([108, 50, 130, 76], fill=bg)
        d.polygon([(130, 56), (178, 62), (178, 76), (130, 72)], fill=bg)
        d.line([(150, 90), (190, 140), (190, 200)], fill=bg, width=8)
    else:
        d.polygon([(70, 200), (110, 70), (140, 70), (180, 200), (150, 200), (125, 110), (100, 200)], fill=(255, 255, 255))
        d.ellipse([112, 36, 142, 66], fill=(255, 255, 255))
    fb = ImageFont.truetype(FONT_BN, 96, layout_engine=ImageFont.Layout.RAQM)
    fe = ImageFont.truetype(FONT_EN, 62)
    d.text((250, 22), bn, font=fb, fill=(255, 255, 255))
    d.text((250, 158), en, font=fe, fill=(255, 255, 255))
    im.save(os.path.join(OUT, name))


os.makedirs(OUT, exist_ok=True)
sign("Sign_FireExt.png", (196, 30, 36), "অগ্নিনির্বাপক যন্ত্র", "FIRE EXTINGUISHER", "ext")
sign("Sign_EmergencyExit.png", (24, 130, 62), "জরুরী নির্গমন", "EMERGENCY EXIT", "exit")


def label(name, bg, head_bg, head_fg, line1, line2, body_fg):
    # the model's own label material is a 473 x 812 image mapped round the body: same size so it wraps like the original label
    w, h = 474, 812
    im = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(im)
    d.rectangle([6, 6, w - 7, h - 7], outline=(110, 110, 110), width=5)
    d.rectangle([6, 6, w - 7, 250], fill=head_bg)
    f1 = ImageFont.truetype(FONT_EN, 112)
    f2 = ImageFont.truetype(FONT_EN, 88)
    tw = d.textlength(line1, font=f1)
    d.text(((w - tw) / 2, 34), line1, font=f1, fill=head_fg)
    tw = d.textlength(line2, font=f2)
    d.text(((w - tw) / 2, 148), line2, font=f2, fill=head_fg)
    for i in range(8):  # small pictogram + instruction lines like the real label
        y = 290 + i * 62
        d.rectangle([34, y, 34 + 78, y + 44], outline=body_fg, width=4)
        d.line([(136, y + 8), (w - 40, y + 8)], fill=body_fg, width=5)
        d.line([(136, y + 32), (w - 110, y + 32)], fill=body_fg, width=5)
    im.save(os.path.join(OUT, name))


label("Label_Silver.png", (232, 232, 232), (200, 30, 36), (255, 255, 255), "Silver", "(Water)", (70, 70, 70))
label("Label_Cream.png", (236, 226, 190), (236, 226, 190), (40, 30, 20), "Cream", "(Foam)", (60, 50, 40))


def pillar_photo():
    # the big concrete pillar on the left of the client picture (7.jpeg), cropped for the hall's pillars
    src = os.path.join(os.path.expanduser("~"), "Downloads", "7.jpeg")
    if not os.path.exists(src):
        print("7.jpeg not found, pillar photo skipped")
        return
    im = Image.open(src).convert("RGB")
    sx = im.size[0] / 2000.0
    box = (int(150 * sx), int(40 * sx), int(385 * sx), int(960 * sx))
    crop = im.crop(box).resize((384, 1024), Image.LANCZOS)
    # the picture's pillar is lit orange by the fire: keep only its dirt and stains, on old off-white paint
    gray = ImageOps.autocontrast(ImageOps.grayscale(crop), cutoff=1)
    crop = ImageOps.colorize(gray, black=(96, 92, 86), white=(252, 250, 244))
    crop.save(os.path.join(OUT, "Pillar_Photo.png"))




def floor_concrete():
    # seamless old grey factory-floor concrete like the client picture: soft dust / grime variation, fine speckle, a few small dark oil
    # spots and faint warm stains. Deliberately low contrast (no camouflage look).
    import numpy as np
    n = 1024
    rng = np.random.default_rng(11)
    fx = np.fft.fftfreq(n)[:, None]
    fy = np.fft.fftfreq(n)[None, :]
    rad2 = fx * fx + fy * fy

    def noise(sigma_px):
        w = rng.standard_normal((n, n))
        f = np.fft.fft2(w) * np.exp(-2.0 * (np.pi * sigma_px) ** 2 * rad2)
        a = np.real(np.fft.ifft2(f))
        a -= a.mean()
        return a / (a.std() + 1e-9)

    low = noise(140)
    mid = noise(30)
    fine = noise(3.0)
    grain = noise(0.9)
    streak = noise(8)
    base = np.array([142.0, 140.0, 135.0])
    img = np.zeros((n, n, 3)) + base
    lum = 1.0 + 0.07 * low + 0.045 * mid + 0.04 * fine + 0.03 * grain
    img *= lum[..., None]
    warm = np.clip((noise(70) - 0.9) / 1.4, 0, 1)[..., None]
    img = img * (1 - 0.14 * warm) + np.array([158.0, 140.0, 118.0]) * 0.14 * warm
    grime = np.clip((noise(45) - 0.7) / 1.6, 0, 1)[..., None]
    img = img * (1 - 0.22 * grime) + np.array([88.0, 83.0, 76.0]) * 0.22 * grime
    spots = np.clip((noise(10) - 2.1) / 0.8, 0, 1)[..., None]
    img = img * (1 - 0.65 * spots) + np.array([42.0, 38.0, 34.0]) * 0.65 * spots
    scuff = np.clip((streak - 1.6) / 1.0, 0, 1)[..., None]
    img = img * (1 - 0.12 * scuff) + np.array([70.0, 66.0, 60.0]) * 0.12 * scuff
    Image.fromarray(np.clip(img, 0, 255).astype('uint8')).save(os.path.join(OUT, "Floor_Concrete.png"))


floor_concrete()
pillar_photo()
print("ok")
