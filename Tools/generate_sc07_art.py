"""Scenario 7 (factory fire) 2D art. Output:
  2D/Sc07/SDS_SC07.png          clipboard with the Safety Data Sheet of workflow step 03 (fire classes and extinguishers)
  2D/Sc07/Icon_Extinguisher.png, Icon_Flammable.png, Icon_Badge_BC.png     hologram line icons for the summary card
  2D/Sc07Tex/*.png              decals and tiles: hazard stripes, yellow floor line, warning signs, extinguisher label bands,
                                breaker panel face, exit sign, oil stain
    python Tools/generate_sc07_art.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT2D = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc07")
OUTTEX = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc07Tex")
FONT = r"C:\Windows\Fonts\segoeui.ttf"
FONTB = r"C:\Windows\Fonts\segoeuib.ttf"
FONTBLK = r"C:\Windows\Fonts\seguibl.ttf"


def f(path, size):
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default()


# ------------------------------------------------------------------ SDS clipboard
def sds():
    img = Image.new("RGBA", (1024, 1280), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((40, 70, 984, 1240), 28, fill=(150, 98, 52, 255))
    d.rounded_rectangle((70, 150, 954, 1205), 8, fill=(250, 249, 245, 255))
    d.rounded_rectangle((390, 36, 634, 150), 16, fill=(176, 182, 188, 255))
    d.ellipse((480, 40, 544, 104), outline=(110, 116, 122, 255), width=10)
    title, sub, head, body, bold = f(FONTB, 50), f(FONT, 24), f(FONTB, 30), f(FONT, 29), f(FONTB, 29)
    d.text((512, 210), "SAFETY DATA SHEET", font=title, fill=(18, 22, 26, 255), anchor="mm")
    d.text((512, 262), "STANDARD STORAGE & HANDLING PROTOCOL", font=sub, fill=(60, 66, 72, 255), anchor="mm")
    d.rectangle((120, 292, 904, 296), fill=(150, 154, 160, 255))
    d.rectangle((120, 316, 904, 362), fill=(222, 226, 232, 255))
    d.text((140, 322), "SDS Standard Protocol", font=head, fill=(20, 24, 28, 255))
    d.text((120, 384), "Applicable Sections: Section 2 \u00b7 Section 5", font=body, fill=(30, 34, 38, 255))
    d.text((120, 424), "(Firefighting Measures) \u00b7 Section 10", font=body, fill=(30, 34, 38, 255))
    rows = [
        ("Class", "Type", "Extinguisher", True),
        ("A", "(Ordinary combustibles)", "Water, dry powder, foam", False),
        ("B", "(Flammable liquids)", "CO\u2082, dry powder, foam", False),
        ("C", "(Energized electrical)", "CO\u2082, dry powder (NOT water)", False),
        ("D", "(Combustible metals)", "Special dry powder", False),
        ("K", "(Cooking oils)", "Wet chemical", False),
    ]
    y = 480
    cols = (120, 205, 560, 904)
    small = f(FONT, 26)
    smallb = f(FONTB, 26)
    for cls, typ, ext, header in rows:
        h = 70
        d.rectangle((cols[0], y, cols[3], y + h), outline=(60, 64, 70, 255), width=3, fill=(240, 242, 245, 255) if header else None)
        d.line((cols[1], y, cols[1], y + h), fill=(60, 64, 70, 255), width=3)
        d.line((cols[2], y, cols[2], y + h), fill=(60, 64, 70, 255), width=3)
        face = smallb if header else small
        d.text((cols[0] + 14, y + h // 2), cls, font=bold if not header else smallb, fill=(20, 24, 28, 255), anchor="lm")
        d.text((cols[1] + 14, y + h // 2), typ, font=face, fill=(20, 24, 28, 255), anchor="lm")
        d.text((cols[2] + 14, y + h // 2), ext, font=face, fill=(20, 24, 28, 255) if not (cls == "C") else (150, 20, 20, 255), anchor="lm")
        y += h
    # GHS diamonds along the bottom
    for i, col in enumerate(((190, 30, 30), (190, 30, 30), (190, 30, 30), (190, 30, 30))):
        cx, cy = 690 + i * 62, 1130
        d.polygon([(cx, cy - 26), (cx + 26, cy), (cx, cy + 26), (cx - 26, cy)], outline=col + (255,), fill=(255, 255, 255, 255), width=4)
        d.text((cx, cy), "!" if i == 3 else "\u2022", font=f(FONTB, 26), fill=(20, 20, 20, 255), anchor="mm")
    d.line((120, 1090, 904, 1090), fill=(160, 164, 170, 255), width=2)
    img.save(os.path.join(OUT2D, "SDS_SC07.png"), optimize=True)
    print("SDS_SC07.png")


# ------------------------------------------------------------------ hologram icons (cyan line art)
CYAN = (200, 245, 255, 255)


def new_icon(size=512):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def glow(img):
    g = img.filter(ImageFilter.GaussianBlur(7))
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out = Image.alpha_composite(out, g)
    return Image.alpha_composite(out, img)


def icon_extinguisher():
    img, d = new_icon()
    w = 14
    # body
    d.rounded_rectangle((170, 200, 330, 470), 50, outline=CYAN, width=w)
    d.line((170, 300, 330, 300), fill=CYAN, width=w)
    d.line((170, 345, 330, 345), fill=CYAN, width=w)
    # neck, valve, handle
    d.rectangle((225, 150, 275, 200), outline=CYAN, width=w)
    d.line((225, 140, 330, 120), fill=CYAN, width=w + 2)
    d.line((250, 150, 360, 150), fill=CYAN, width=w)
    # hose
    d.arc((300, 130, 420, 330), 280, 80, fill=CYAN, width=w)
    d.line((372, 316, 372, 340), fill=CYAN, width=w)
    img = glow(img)
    img.save(os.path.join(OUT2D, "Icon_Extinguisher.png"), optimize=True)


def icon_flammable():
    img, d = new_icon()
    w = 14
    # jerrycan
    d.rounded_rectangle((120, 210, 350, 470), 30, outline=CYAN, width=w)
    d.polygon([(120, 270), (190, 210), (190, 170), (120, 170)], outline=CYAN)
    d.line((120, 270, 190, 210), fill=CYAN, width=w)
    d.rectangle((120, 160, 190, 215), outline=CYAN, width=w)
    d.arc((120, 120, 320, 220), 180, 360, fill=CYAN, width=w)  # handle
    # flame on the can
    pts = [(235, 420), (190, 380), (205, 330), (232, 305), (238, 335), (262, 300), (290, 345), (285, 395)]
    d.line(pts + [pts[0]], fill=CYAN, width=w, joint="curve")
    # flame above
    fl = [(395, 190), (365, 150), (375, 110), (398, 85), (402, 112), (420, 90), (440, 125), (430, 160)]
    d.line(fl + [fl[0]], fill=CYAN, width=w, joint="curve")
    img = glow(img)
    img.save(os.path.join(OUT2D, "Icon_Flammable.png"), optimize=True)


def icon_badge():
    img, d = new_icon(256)
    w = 9
    d.rounded_rectangle((20, 20, 236, 236), 26, outline=CYAN, width=w)
    d.text((100, 110), "B", font=f(FONTBLK, 92), fill=CYAN, anchor="mm")
    d.text((172, 120), "C", font=f(FONTBLK, 92), fill=CYAN, anchor="mm")
    d.line((60, 190, 196, 190), fill=CYAN, width=w - 2)
    img = glow(img)
    img.save(os.path.join(OUT2D, "Icon_Badge_BC.png"), optimize=True)


# ------------------------------------------------------------------ tiles and decals
def hazard():
    n = 512
    y, x = np.mgrid[0:n, 0:n]
    stripe = (((x + y) // 64) % 2).astype(np.float32)
    col = np.zeros((n, n, 3), np.float32)
    yel = np.array([0.98, 0.78, 0.05])
    blk = np.array([0.06, 0.06, 0.06])
    col[:] = yel * (1 - stripe[..., None]) + blk * stripe[..., None]
    noise = np.random.default_rng(1).normal(0, 0.03, (n, n, 1))
    col = np.clip(col + noise, 0, 1)
    Image.fromarray((col * 255).astype(np.uint8)).save(os.path.join(OUTTEX, "HazardStripe.png"))


def yellow_line():
    img = Image.new("RGBA", (256, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(3)
    d.rectangle((0, 12, 256, 52), fill=(214, 168, 28, 255))
    arr = np.asarray(img).astype(np.float32)
    wear = rng.uniform(0.55, 1.0, (64, 256, 1))
    arr[..., :3] *= wear
    arr[..., 3] *= np.clip(wear[..., 0] * 1.3, 0, 1)
    Image.fromarray(arr.astype(np.uint8)).save(os.path.join(OUTTEX, "YellowLine.png"))


def sign_triangle(name, glyph):
    n = 256
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((8, 8, n - 8, n - 8), 20, fill=(246, 246, 240, 255), outline=(40, 40, 40, 255), width=4)
    tri = [(n // 2, 34), (n - 34, n - 52), (34, n - 52)]
    d.polygon(tri, fill=(250, 200, 0, 255), outline=(20, 20, 20, 255), width=8)
    if glyph == "bolt":
        d.polygon([(138, 90), (96, 146), (126, 146), (112, 196), (160, 130), (130, 130)], fill=(20, 20, 20, 255))
    else:
        d.polygon([(128, 190), (100, 160), (112, 126), (130, 108), (136, 132), (152, 112), (164, 146), (156, 172)], fill=(20, 20, 20, 255))
    d.text((n // 2, n - 30), "DANGER" if glyph == "bolt" else "FLAMMABLE", font=f(FONTB, 22), fill=(20, 20, 20, 255), anchor="mm")
    img.save(os.path.join(OUTTEX, name))


def label_band(name, text, bg, fg):
    img = Image.new("RGBA", (512, 256), bg + (255,))
    d = ImageDraw.Draw(img)
    d.text((256, 128), text, font=f(FONTBLK, 150 if len(text) < 4 else 118), fill=fg + (255,), anchor="mm")
    img.save(os.path.join(OUTTEX, name))


def breakers():
    img = Image.new("RGBA", (512, 640), (150, 154, 156, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 511, 639), outline=(70, 74, 78, 255), width=10)
    rng = np.random.default_rng(5)
    for row in range(3):
        y0 = 40 + row * 190
        d.rectangle((26, y0, 486, y0 + 150), fill=(205, 206, 204, 255), outline=(80, 84, 88, 255), width=4)
        for i in range(8):
            x = 44 + i * 55
            d.rectangle((x, y0 + 20, x + 38, y0 + 120), fill=(40, 42, 46, 255), outline=(20, 22, 24, 255), width=3)
            d.rectangle((x + 8, y0 + 32 + (0 if rng.random() > 0.3 else 38), x + 30, y0 + 70 + (0 if rng.random() > 0.3 else 38)), fill=(235, 235, 232, 255))
    d.ellipse((430, 20, 470, 60), fill=(60, 230, 90, 255))
    img.save(os.path.join(OUTTEX, "BreakerPanel.png"))


def exit_sign():
    img = Image.new("RGBA", (512, 256), (18, 150, 70, 255))
    d = ImageDraw.Draw(img)
    d.rectangle((6, 6, 505, 249), outline=(240, 250, 240, 255), width=8)
    # running man
    d.ellipse((92, 52, 128, 88), fill=(255, 255, 255, 255))
    d.line((110, 92, 126, 150), fill=(255, 255, 255, 255), width=18)
    d.line((124, 110, 164, 98), fill=(255, 255, 255, 255), width=14)
    d.line((118, 112, 82, 130), fill=(255, 255, 255, 255), width=14)
    d.line((126, 150, 160, 186), fill=(255, 255, 255, 255), width=16)
    d.line((126, 150, 98, 196), fill=(255, 255, 255, 255), width=16)
    d.polygon([(330, 128), (390, 78), (390, 108), (440, 108), (440, 148), (390, 148), (390, 178)], fill=(255, 255, 255, 255))
    img.save(os.path.join(OUTTEX, "ExitSign.png"))


def oil():
    n = 256
    y, x = np.mgrid[0:n, 0:n]
    r = np.sqrt(((x - n / 2) / (n / 2)) ** 2 + ((y - n / 2) / (n / 2.4)) ** 2)
    rng = np.random.default_rng(9)
    noise = np.asarray(Image.fromarray((rng.random((16, 16)) * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)).astype(np.float32) / 255
    a = np.clip(1.15 - r + (noise - 0.5) * 0.6, 0, 1) ** 1.6 * 0.7
    rgba = np.zeros((n, n, 4), np.uint8)
    rgba[..., :3] = (18, 16, 14)
    rgba[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(rgba).save(os.path.join(OUTTEX, "OilStain.png"))


def lifted_pbr():
    """Lighter copies of the Scenario 5 concrete PBR sets (Floor07/Wall07/Ceil07 in 2D/Sc05Tex): the Scenario 7 hall of the
    workflow pictures is brighter than the dark dyeing factory. Normal and metal/smooth maps are copied unchanged."""
    d = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc05Tex") + os.sep

    def lift(name, new, gain, gamma, desat=0.0, contrast=1.0, warm=(1.0, 0.99, 0.96)):
        im = np.asarray(Image.open(d + name + "_Albedo.png").convert("RGB")).astype(np.float32) / 255
        grey = im.mean(axis=2, keepdims=True)
        im = im * (1 - desat) + grey * desat  # the dyeing factory floor has blue dye stains: take the colour out
        mean = im.mean(axis=(0, 1), keepdims=True)
        im = mean + (im - mean) * contrast  # calmer stains and streaks, the workflow floor and walls are cleaner
        im = np.clip((im ** gamma) * gain, 0, 1) * np.array(warm)
        Image.fromarray((np.clip(im, 0, 1) * 255).astype(np.uint8)).save(d + new + "_Albedo.png")
        for suf in ("_Normal", "_MS"):
            if os.path.exists(d + name + suf + ".png"):
                Image.open(d + name + suf + ".png").save(d + new + suf + ".png")

    lift("Floor", "Floor07", 1.05, 0.72, 0.85, 0.5)
    lift("Wall", "Wall07", 1.2, 0.74, 0.4, 0.65)
    lift("Ceil", "Ceil07", 1.1, 0.8, 0.4)


def main():
    lifted_pbr()
    os.makedirs(OUT2D, exist_ok=True)
    os.makedirs(OUTTEX, exist_ok=True)
    sds()
    icon_extinguisher()
    icon_flammable()
    icon_badge()
    hazard()
    yellow_line()
    sign_triangle("Sign_Electrical.png", "bolt")
    sign_triangle("Sign_Flammable.png", "flame")
    label_band("Label_Water.png", "Water", (246, 246, 246), (200, 30, 30))
    label_band("Label_CO2.png", "CO\u2082", (14, 14, 14), (255, 255, 255))
    label_band("Label_ABC.png", "ABC", (20, 62, 170), (255, 255, 255))
    breakers()
    exit_sign()
    oil()
    print("done")


if __name__ == "__main__":
    main()
