"""Scenario 4 garment-factory textures: the hydrochloric acid bottle label and the green USE GLOVES sign.

    python Tools/generate_sc04_factory.py

Writes Assets/Art/Presentable/2D/Sc04/Sign_UseGloves.png. The Bangla line is shaped with libraqm
(same font as Tools/render_content_bn.py). Does not touch other scenarios.
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04")
FONT = r"C:\Windows\Fonts\segoeui.ttf"
FONTB = r"C:\Windows\Fonts\segoeuib.ttf"
BN_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")


def diamond(d, cx, cy, r, icon):
    pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
    d.polygon(pts, fill=(255, 255, 255, 255), outline=(206, 32, 41, 255))
    inner = [(cx, cy - r * 0.9), (cx + r * 0.9, cy), (cx, cy + r * 0.9), (cx - r * 0.9, cy)]
    d.line(inner + [inner[0]], fill=(206, 32, 41, 255), width=max(5, int(r * 0.09)))
    k = r / 100.0
    if icon == "exclaim":
        d.rounded_rectangle((cx - 9 * k, cy - 50 * k, cx + 9 * k, cy + 18 * k), 6, fill=(20, 20, 20, 255))
        d.ellipse((cx - 11 * k, cy + 32 * k, cx + 11 * k, cy + 54 * k), fill=(20, 20, 20, 255))
    elif icon == "skull":
        d.ellipse((cx - 32 * k, cy - 48 * k, cx + 32 * k, cy + 14 * k), fill=(20, 20, 20, 255))
        d.rectangle((cx - 18 * k, cy, cx + 18 * k, cy + 30 * k), fill=(20, 20, 20, 255))
        d.ellipse((cx - 20 * k, cy - 22 * k, cx - 6 * k, cy - 6 * k), fill=(255, 255, 255, 255))
        d.ellipse((cx + 6 * k, cy - 22 * k, cx + 20 * k, cy - 6 * k), fill=(255, 255, 255, 255))
        d.line((cx - 44 * k, cy + 40 * k, cx + 44 * k, cy + 6 * k), fill=(20, 20, 20, 255), width=int(9 * k))
        d.line((cx - 44 * k, cy + 6 * k, cx + 44 * k, cy + 40 * k), fill=(20, 20, 20, 255), width=int(9 * k))
    else:  # corrosion: two drops over a surface and a hand
        for dx in (-22, 16):
            d.polygon([(cx + dx * k, cy - 46 * k), (cx + (dx - 10) * k, cy - 26 * k), (cx + (dx + 10) * k, cy - 26 * k)],
                      fill=(20, 20, 20, 255))
            d.ellipse((cx + (dx - 11) * k, cy - 32 * k, cx + (dx + 11) * k, cy - 10 * k), fill=(20, 20, 20, 255))
        d.rectangle((cx - 46 * k, cy + 6 * k, cx - 6 * k, cy + 24 * k), fill=(20, 20, 20, 255))
        d.rectangle((cx + 6 * k, cy + 18 * k, cx + 46 * k, cy + 34 * k), fill=(20, 20, 20, 255))


def label():
    w, h = 1024, 640
    img = Image.new("RGBA", (w, h), (252, 253, 255, 255))
    d = ImageDraw.Draw(img)
    # blue wave bands top and bottom like the client's bottle
    d.rectangle((0, 0, w, 70), fill=(18, 82, 168, 255))
    d.rectangle((0, h - 70, w, h), fill=(18, 82, 168, 255))
    d.ellipse((-200, 40, w + 200, 140), fill=(252, 253, 255, 255))
    d.ellipse((-200, h - 150, w + 200, h - 50), fill=(252, 253, 255, 255))
    big = ImageFont.truetype(FONTB, 112)
    mid = ImageFont.truetype(FONTB, 92)
    d.text((w // 2, 215), "Hydrochloric Acid", font=big, fill=(18, 82, 168, 255), anchor="mm")
    sub = ImageFont.truetype(FONTB, 60)
    d.text((w // 2, 345), "HCl", font=mid, fill=(40, 46, 56, 255), anchor="mm")
    for i, icon in enumerate(("corrosion", "exclaim", "skull")):
        diamond(d, 330 + i * 182, 490, 82, icon)
    img.save(os.path.join(OUT, "BottleLabel.png"))
    print("BottleLabel.png")


def sign():
    w, h = 512, 768
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    green = (14, 126, 66, 255)
    d.rounded_rectangle((0, 0, w - 1, h - 1), 36, fill=(255, 255, 255, 255))
    d.rounded_rectangle((14, 14, w - 15, h - 15), 28, fill=green)
    d.rounded_rectangle((26, 26, w - 27, h - 27), 22, outline=(255, 255, 255, 255), width=6)
    cx, cy, r = w // 2, 270, 190
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(255, 255, 255, 255))
    d.ellipse((cx - r + 16, cy - r + 16, cx + r - 16, cy + r - 16), fill=green)
    # a raised hand in a glove (white): palm, four fingers, thumb, cuff
    wh = (255, 255, 255, 255)
    d.rounded_rectangle((cx - 78, cy - 20, cx + 78, cy + 92), 30, fill=wh)
    for i, fh in enumerate((112, 138, 144, 120)):
        x0 = cx - 78 + i * 40
        d.rounded_rectangle((x0, cy - 20 - fh, x0 + 34, cy + 10), 17, fill=wh)
    d.rounded_rectangle((cx - 124, cy + 6, cx - 60, cy + 46), 20, fill=wh)
    d.polygon([(cx - 124, cy + 20), (cx - 74, cy + 60), (cx - 66, cy + 10)], fill=wh)
    d.rounded_rectangle((cx - 94, cy + 84, cx + 94, cy + 136), 10, fill=wh)
    d.line((cx - 94, cy + 110, cx + 94, cy + 110), fill=green, width=5)
    bn = ImageFont.truetype(BN_BOLD, 86, layout_engine=ImageFont.Layout.RAQM)
    d.text((cx, 535), "হাতমোজা", font=bn, fill=wh, anchor="mm")
    d.text((cx, 620), "ব্যবহার করুন", font=ImageFont.truetype(BN_BOLD, 58, layout_engine=ImageFont.Layout.RAQM), fill=wh, anchor="mm")
    d.text((cx, 692), "USE GLOVES", font=ImageFont.truetype(FONTB, 50), fill=wh, anchor="mm")
    img.save(os.path.join(OUT, "Sign_UseGloves.png"))
    print("Sign_UseGloves.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    sign()  # the HCl bottle label (2D/Sc04/BottleLabel.png) is the one supplied earlier: not generated here
