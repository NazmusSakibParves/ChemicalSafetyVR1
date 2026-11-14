"""Makes the six GHS pictograms of Scenario 18 question 2 transparent (the client's JPEGs have a black background outside the diamond).

    python -X utf8 -I Tools/prepare_sc18_pictograms.py

The black background is flood-filled from the four corners (the black symbols inside the diamond are enclosed by the white
diamond, so they stay), the picture is cropped to the diamond, resized to 512 x 512 and given a soft anti-aliased edge.
Output: Assets/Resources/ScCheck/SC18/pict_<name>.png
"""
import os

from PIL import Image, ImageDraw, ImageFilter

SRC = r"C:/Users/msi/Downloads/Scene13-18 workflow-20261102T065200Z-1-001/"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "ScCheck", "SC18")
SIZE = 512
FILES = {
    "exclamation": "Bleaching powder pictogram/WhatsApp Image 2026-11-11 at 00.43.15 (1).jpeg",
    "environment": "Bleaching powder pictogram/WhatsApp Image 2026-11-11 at 00.43.15 (2).jpeg",
    "oxidizer": "Bleaching powder pictogram/WhatsApp Image 2026-11-11 at 00.43.15.jpeg",
    "corrosion": "Bleaching powder pictogram/WhatsApp Image 2026-11-11 at 00.43.16.jpeg",
    "health": "Not Bleaching Powder pictogram/WhatsApp Image 2026-11-11 at 00.47.04.jpeg",
    "skull": "Not Bleaching Powder pictogram/WhatsApp Image 2026-11-11 at 00.47.05.jpeg",
}


def cut(path):
    img = Image.open(path).convert("RGB")
    w, h = img.size
    # mask of "background": near-black pixels reachable from the corners
    mask = Image.new("L", (w, h), 0)
    px = img.load()
    dark = Image.new("L", (w, h), 0)
    dp = dark.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r < 60 and g < 60 and b < 60:
                dp[x, y] = 255
    # flood fill from the corners over the dark pixels
    fill = dark.copy()
    for c in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        if fill.getpixel(c) == 255:
            ImageDraw.floodfill(fill, c, 128, thresh=0)
    mp = mask.load()
    fp = fill.load()
    for y in range(h):
        for x in range(w):
            mp[x, y] = 0 if fp[x, y] == 128 else 255  # 0 = background (transparent)
    # soften the edge a little so the diamond does not look jagged, and eat the one-pixel dark fringe
    mask = mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    rgba = img.copy()
    rgba.putalpha(mask)
    bbox = mask.point(lambda v: 255 if v > 40 else 0).getbbox()
    rgba = rgba.crop(bbox)
    side = max(rgba.size)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(rgba, ((side - rgba.width) // 2, (side - rgba.height) // 2))
    return sq.resize((SIZE, SIZE), Image.LANCZOS)


os.makedirs(OUT, exist_ok=True)
for name, rel in FILES.items():
    out = cut(SRC + rel)
    out.save(os.path.join(OUT, "pict_" + name + ".png"), optimize=True)
    print(name, out.size, "corner alpha", out.getpixel((0, 0))[3], "centre alpha", out.getpixel((SIZE // 2, SIZE // 2))[3])

# contact sheet on grey and on the card blue, for a visual check (not part of the game)
sheet = Image.new("RGBA", (3 * 256, 2 * 256), (60, 100, 140, 255))
for k, name in enumerate(FILES):
    im = Image.open(os.path.join(OUT, "pict_" + name + ".png")).resize((256, 256), Image.LANCZOS)
    sheet.alpha_composite(im, ((k % 3) * 256, (k // 3) * 256))
sheet.save(os.path.join(ROOT, "Temp", "sc18_pictograms_check.png"))
