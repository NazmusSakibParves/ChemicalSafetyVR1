"""Draws the Scene 02 SDS clipboard image (replaces the Scene 01 sheet on the X button).

    python Tools/generate_sc02_sds.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc02", "SDS_SC02.png")
FONT = r"C:\Windows\Fonts\segoeui.ttf"
FONTB = r"C:\Windows\Fonts\segoeuib.ttf"


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img = Image.new("RGBA", (1024, 1280), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((40, 70, 984, 1240), 28, fill=(92, 58, 32, 255))
    d.rounded_rectangle((70, 150, 954, 1205), 8, fill=(248, 246, 240, 255))
    d.rounded_rectangle((390, 36, 634, 150), 16, fill=(176, 182, 188, 255))
    d.rectangle((470, 78, 554, 108), fill=(120, 126, 132, 255))

    title = ImageFont.truetype(FONTB, 54)
    sub = ImageFont.truetype(FONT, 28)
    head = ImageFont.truetype(FONTB, 32)
    body = ImageFont.truetype(FONT, 30)
    d.text((150, 185), "SAFETY DATA SHEET", font=title, fill=(20, 24, 28, 255))
    d.text((150, 250), "STANDARD STORAGE & HANDLING PROTOCOL", font=sub, fill=(40, 70, 110, 255))
    d.rectangle((150, 300, 874, 304), fill=(180, 40, 40, 255))

    y = 330
    d.text((150, y), "APPLICABLE SDS SECTIONS", font=head, fill=(20, 24, 28, 255))
    y += 52
    for line in (
        "Section 2: Hazard identification",
        "Section 7: Handling and storage",
        "Section 10: Stability and reactivity",
    ):
        d.text((170, y), "-  " + line, font=body, fill=(30, 34, 38, 255))
        y += 46

    y += 20
    d.rectangle((150, y, 874, y + 48), fill=(176, 32, 32, 255))
    d.text((168, y + 6), "HANDLING & STORAGE REQUIREMENTS", font=head, fill=(255, 255, 255, 255))
    y += 70
    for line in (
        "Segregate by hazard class. Never store",
        "incompatible chemicals together.",
        "Flammables stay in the flammable cabinet,",
        "away from oxidizers and acids.",
        "Read the compatibility chart before placing",
        "any container.",
        "Keep containers closed and labelled.",
    ):
        d.text((170, y), "-  " + line, font=body, fill=(30, 34, 38, 255))
        y += 48

    d.text((150, 1140), "SCENE 02  -  CHEMICAL STORAGE", font=sub, fill=(90, 96, 102, 255))
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
