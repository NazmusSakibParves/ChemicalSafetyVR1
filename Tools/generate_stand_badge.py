"""Badge shown over the floor "Stand Here" marker (English + Bangla on one plate).

Output: Assets/Art/Presentable/2D/Env/UI_StandHere.png   (re-run: python Tools/generate_stand_badge.py)
Bangla shaping goes through the same RAQM pipeline as render_bangla_text.py.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_bangla_text as rb  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env", "UI_StandHere.png")

SS = 2                      # supersample
W, H = 900 * SS, 300 * SS
GREEN = (77, 255, 140)
NAVY_TOP = (12, 34, 52)
NAVY_BOT = (6, 18, 30)


def tracked(draw, xy, text, font, fill, tracking):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=font, fill=fill)
        x += draw.textlength(ch, font=font) + tracking
    return x


def tracked_width(draw, text, font, tracking):
    return sum(draw.textlength(ch, font=font) + tracking for ch in text) - tracking


def main():
    pad = 40 * SS
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle([pad, pad, W - pad, H - pad], 46 * SS, outline=GREEN + (255,), width=10 * SS)
    glow = glow.filter(ImageFilter.GaussianBlur(14 * SS))

    plate = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    grad = Image.new("RGBA", (W, H))
    gd = ImageDraw.Draw(grad)
    for y in range(H):
        t = y / H
        c = tuple(int(NAVY_TOP[i] + (NAVY_BOT[i] - NAVY_TOP[i]) * t) for i in range(3))
        gd.line([(0, y), (W, y)], fill=c + (232,))
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([pad, pad, W - pad, H - pad], 46 * SS, fill=255)
    plate.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(plate)
    d.rounded_rectangle([pad, pad, W - pad, H - pad], 46 * SS, outline=GREEN + (255,), width=3 * SS)
    d.rounded_rectangle([pad + 9 * SS, pad + 9 * SS, W - pad - 9 * SS, H - pad - 9 * SS], 38 * SS,
                        outline=(255, 255, 255, 38), width=1 * SS)

    # location pin on the left
    cx, cy = pad + 88 * SS, H // 2
    d.ellipse([cx - 44 * SS, cy - 44 * SS, cx + 44 * SS, cy + 44 * SS], fill=GREEN + (40,), outline=GREEN + (255,),
              width=3 * SS)
    d.polygon([(cx - 15 * SS, cy - 4 * SS), (cx + 15 * SS, cy - 4 * SS), (cx, cy + 24 * SS)], fill=GREEN + (255,))
    d.ellipse([cx - 15 * SS, cy - 22 * SS, cx + 15 * SS, cy + 8 * SS], fill=GREEN + (255,))
    d.ellipse([cx - 6 * SS, cy - 13 * SS, cx + 6 * SS, cy - 1 * SS], fill=NAVY_TOP + (255,))

    # divider
    tx = pad + 170 * SS
    d.line([(tx - 16 * SS, pad + 38 * SS), (tx - 16 * SS, H - pad - 38 * SS)], fill=GREEN + (120,), width=2 * SS)

    # English
    fe = ImageFont.truetype(rb.LATIN_BOLD, 92 * SS)
    ew = tracked_width(d, "Stand Here", fe, 3 * SS)
    ty = pad + 22 * SS
    area = (W - pad - 40 * SS) - tx
    ex = tx + (area - ew) / 2
    tracked(d, (ex, ty), "Stand Here", fe, (255, 255, 255, 255), 3 * SS)

    # Bangla
    fb = ImageFont.truetype(rb.FONT_BOLD, 52 * SS, layout_engine=ImageFont.Layout.RAQM)
    bw = d.textlength("এখানে দাঁড়ান", font=fb)
    d.text((tx + (area - bw) / 2, ty + 118 * SS), "এখানে দাঁড়ান", font=fb, fill=(190, 255, 215, 255))

    out = Image.alpha_composite(glow, plate)
    out.resize((W // SS, H // SS), Image.LANCZOS).save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
