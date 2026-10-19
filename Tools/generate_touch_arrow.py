"""Slim double-chevron "touch here" arrow for the floating Touch buttons.

Output: Assets/Art/Presentable/2D/Env/ICON_Arrow.png   (re-run: python Tools/generate_touch_arrow.py)
"""
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Env", "ICON_Arrow.png")
S = 1024  # drawn large, downsampled for smooth edges


def chevron(d, top, color, width):
    d.line([(S * 0.2, top), (S * 0.5, top + S * 0.26), (S * 0.8, top)], fill=color, width=width, joint="curve")
    for x, y in [(S * 0.2, top), (S * 0.8, top)]:
        d.ellipse([x - width / 2, y - width / 2, x + width / 2, y + width / 2], fill=color)


def main():
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for top in (S * 0.2, S * 0.46):
        chevron(g, top, (90, 220, 255, 255), 120)
    glow = glow.filter(ImageFilter.GaussianBlur(26))

    core = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    c = ImageDraw.Draw(core)
    for top in (S * 0.2, S * 0.46):
        chevron(c, top, (255, 255, 255, 255), 84)

    img = Image.alpha_composite(glow, core).resize((256, 256), Image.LANCZOS)
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
