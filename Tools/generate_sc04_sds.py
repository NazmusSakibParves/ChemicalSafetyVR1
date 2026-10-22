"""Scenario 4 clipboard SDS and the small AC readout. Does not touch other scenarios.

    python Tools/generate_sc04_sds.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04")
FONT = r"C:\Windows\Fonts\segoeui.ttf"
FONTB = r"C:\Windows\Fonts\segoeuib.ttf"


def sds():
    img = Image.new("RGBA", (1024, 1280), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((40, 70, 984, 1240), 28, fill=(92, 58, 32, 255))
    d.rounded_rectangle((70, 150, 954, 1205), 8, fill=(248, 246, 240, 255))
    d.rounded_rectangle((390, 36, 634, 150), 16, fill=(176, 182, 188, 255))
    d.rectangle((470, 78, 554, 108), fill=(120, 126, 132, 255))
    title = ImageFont.truetype(FONTB, 48)
    sub = ImageFont.truetype(FONT, 26)
    head = ImageFont.truetype(FONTB, 28)
    body = ImageFont.truetype(FONT, 26)
    d.text((150, 180), "SAFETY DATA SHEET", font=title, fill=(20, 24, 28, 255))
    d.text((150, 245), "Hydrochloric acid, concentrated", font=sub, fill=(40, 70, 110, 255))
    d.rectangle((150, 292, 874, 298), fill=(180, 40, 40, 255))
    y = 320
    d.text((150, y), "APPLICABLE SDS SECTIONS", font=head, fill=(20, 24, 28, 255))
    y += 46
    for line in (
        "Section 2  Hazard identification",
        "Section 4  First-aid measures",
        "Section 8  Exposure controls / PPE",
    ):
        d.text((170, y), line, font=body, fill=(30, 34, 38, 255))
        y += 40
    y += 16
    d.rectangle((150, y, 874, y + 44), fill=(176, 32, 32, 255))
    d.text((170, y + 8), "GLOVE SELECTION AND PERMEATION", font=head, fill=(255, 255, 255, 255))
    y += 58
    rows = (
        ("Material", "Rating", "Time", True),
        ("Neoprene", "Excellent", ">480 min", False),
        ("Butyl rubber", "Excellent", ">480 min", False),
        ("Nitrile", "Poor", "<30 min", False),
        ("PVC", "Poor", "<10 min", False),
        ("Latex", "Poor", "<30 min", False),
        ("Cotton / leather", "None", "Immediate", False),
    )
    for mat, rating, time, header in rows:
        if header:
            d.rectangle((150, y, 874, y + 40), fill=(230, 232, 236, 255))
        color = (20, 24, 28, 255) if header or rating == "Excellent" else (160, 32, 32, 255)
        if rating == "Excellent":
            color = (16, 110, 48, 255)
        face = head if header else body
        d.text((170, y + 6), mat, font=face, fill=(20, 24, 28, 255) if header else color)
        d.text((470, y + 6), rating, font=face, fill=(20, 24, 28, 255) if header else color)
        d.text((700, y + 6), time, font=face, fill=(20, 24, 28, 255) if header else color)
        y += 42
    d.text((150, 1148), "Check this sheet before you choose a glove.", font=sub, fill=(40, 70, 110, 255))
    img.save(os.path.join(OUT, "SDS_SC04.png"))
    print("SDS_SC04.png")


def display():
    img = Image.new("RGBA", (256, 96), (8, 16, 18, 255))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(FONTB, 64)
    d.text((28, 12), "25°C", font=font, fill=(120, 230, 190, 255))
    img.save(os.path.join(OUT, "AC_Display.png"))
    print("AC_Display.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    sds()
    display()
