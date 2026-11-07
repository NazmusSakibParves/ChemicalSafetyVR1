"""FALLBACK ONLY. The real label is the client's file (WhatsApp Image 2026-11-01 at 18.31.35.jpeg), cropped to the rounded rectangle and
saved as BottleLabelWide.png; do not run this script over it. This redraw (1536 x 1024) was the stand-in used before the file was found.

    python Tools/generate_sc04_label_wide.py

Writes Assets/Art/Presentable/2D/Sc04/BottleLabelWide.png. The two big GHS pictograms are cut out of the label the client supplied
first (BottleLabel.png, kept as it is); the small skull and flame ones are drawn here. If the client's own file is saved as
BottleLabelWide.png it replaces this one (do not run this script then).
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04")
BLACK_FONT = r"C:\Windows\Fonts\ariblk.ttf"
BOLD = r"C:\Windows\Fonts\arialbd.ttf"
REG = r"C:\Windows\Fonts\arial.ttf"
W, H = 1536, 1024
RED = (230, 28, 36, 255)
BLK = (12, 12, 12, 255)


def fit(draw, text, font_path, max_w, start):
    size = start
    while size > 10:
        f = ImageFont.truetype(font_path, size)
        if draw.textlength(text, font=f) <= max_w:
            return f
        size -= 2
    return ImageFont.truetype(font_path, 10)


def small_diamond(d, cx, cy, r, kind):
    pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
    d.polygon(pts, fill=(255, 255, 255, 255), outline=RED)
    d.line(pts + [pts[0]], fill=RED, width=9)
    k = r / 100.0
    if kind == "skull":
        d.ellipse((cx - 30 * k, cy - 46 * k, cx + 30 * k, cy + 10 * k), fill=BLK)
        d.rectangle((cx - 17 * k, cy, cx + 17 * k, cy + 26 * k), fill=BLK)
        d.ellipse((cx - 19 * k, cy - 22 * k, cx - 6 * k, cy - 8 * k), fill=(255, 255, 255, 255))
        d.ellipse((cx + 6 * k, cy - 22 * k, cx + 19 * k, cy - 8 * k), fill=(255, 255, 255, 255))
        d.line((cx - 42 * k, cy + 40 * k, cx + 42 * k, cy + 8 * k), fill=BLK, width=int(9 * k))
        d.line((cx - 42 * k, cy + 8 * k, cx + 42 * k, cy + 40 * k), fill=BLK, width=int(9 * k))
    else:  # flame over a bar
        d.polygon([(cx, cy - 48 * k), (cx + 30 * k, cy - 4 * k), (cx + 22 * k, cy + 22 * k), (cx - 22 * k, cy + 22 * k),
                   (cx - 30 * k, cy - 4 * k), (cx - 8 * k, cy - 14 * k)], fill=BLK)
        d.ellipse((cx - 11 * k, cy - 4 * k, cx + 11 * k, cy + 20 * k), fill=(255, 255, 255, 255))
        d.rectangle((cx - 34 * k, cy + 32 * k, cx + 34 * k, cy + 42 * k), fill=BLK)


def main():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((20, 20, W - 21, H - 21), 34, fill=(255, 255, 255, 255), outline=BLK, width=8)
    # title
    f = fit(d, "HYDROCHLORIC ACID", BLACK_FONT, W - 190, 190)
    d.text((W // 2, 150), "HYDROCHLORIC ACID", font=f, fill=BLK, anchor="mm")
    # big pictograms cut out of the label supplied first
    src = Image.open(os.path.join(DIR, "BottleLabel.png")).convert("RGBA")
    corr = src.crop((112, 528, 618, 1048)).resize((326, 335), Image.LANCZOS)
    exc = src.crop((610, 528, 1112, 1048)).resize((324, 335), Image.LANCZOS)
    img.alpha_composite(corr, (392, 208))
    img.alpha_composite(exc, (730, 208))
    d.text((W // 2 - 20, 232), "DANGER", font=ImageFont.truetype(BOLD, 50), fill=BLK, anchor="mm")
    # chemical formula
    d.text((140, 410), "CHEMICAL", font=ImageFont.truetype(BOLD, 40), fill=BLK, anchor="lm")
    d.text((140, 452), "FORMULA", font=ImageFont.truetype(BOLD, 40), fill=BLK, anchor="lm")
    d.text((140, 522), "HCl", font=ImageFont.truetype(BLACK_FONT, 96), fill=BLK, anchor="lm")
    # small pictograms + hazard statements
    small_diamond(d, 1300, 262, 66, "skull")
    small_diamond(d, 1298, 345, 52, "flame")
    d.text((1130, 425), "HAZARD STATEMENTS:", font=ImageFont.truetype(BOLD, 21), fill=BLK, anchor="lm")
    y = 452
    for line in ("Causes severe skin burns", "and eye damage.", "May cause respiratory irritation.",
                 "Harmful if swallowed.", "Very toxic to aquatic life."):
        d.text((1146, y), ("\u2022 " if not line.startswith("and") else "   ") + line, font=ImageFont.truetype(REG, 19), fill=BLK,
               anchor="lm")
        y += 22
    # yellow band
    d.rectangle((14, 572, W - 15, 732), fill=(255, 226, 0, 255))
    d.line((14, 572, W - 15, 572), fill=BLK, width=5)
    d.line((14, 732, W - 15, 732), fill=BLK, width=5)
    f = fit(d, "hydrochloric Acid", BLACK_FONT, W - 190, 170)
    d.text((W // 2, 652), "hydrochloric Acid", font=f, fill=BLK, anchor="mm")
    # product id
    d.text((W // 2, 775), "PRODUCT ID:  HCL-IND-G50", font=fit(d, "PRODUCT ID:  HCL-IND-G50", BLACK_FONT, 940, 70), fill=BLK,
           anchor="mm")
    d.text((W // 2, 850), "5 LITERS", font=ImageFont.truetype(BLACK_FONT, 66), fill=BLK, anchor="mm")
    # footer
    d.text((145, 855), "MANUFACTURED BY:", font=ImageFont.truetype(BOLD, 25), fill=BLK, anchor="lm")
    for i, line in enumerate(("ABC Chemicals Limited", "123 Industrial Estate, Dhaka-1208, Bangladesh",
                              "Tel: +880-2-1234567  |  www.abcchem.com")):
        d.text((145, 884 + i * 26), line, font=ImageFont.truetype(REG, 24), fill=BLK, anchor="lm")
    d.line((1078, 868, 1078, 940), fill=BLK, width=4)
    d.text((1104, 886), "FOR INDUSTRIAL USE ONLY", font=ImageFont.truetype(BOLD, 24), fill=BLK, anchor="lm")
    d.text((1104, 917), "KEEP OUT OF REACH OF CHILDREN", font=ImageFont.truetype(BOLD, 21), fill=BLK, anchor="lm")
    img.save(os.path.join(DIR, "BottleLabelWide.png"))
    print("BottleLabelWide.png")


if __name__ == "__main__":
    main()
