"""Scenario 22 safety data sheet: the client's SDS.jpg (clipboard on a dark background, GHS labelling / approved containers).
The background is made transparent with a rounded-rectangle mask of the clipboard board + its clip.
Run: python -I Tools/generate_sc22_sds.py "<path to SDS.jpg>"  -> Assets/Resources/Sc22/Sc22_Sds.png"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
im = Image.open(sys.argv[1]).convert("RGBA")
w, h = im.size
k = 4  # supersample the mask for smooth edges
m = Image.new("L", (w * k, h * k), 0)
d = ImageDraw.Draw(m)
d.rounded_rectangle([50 * k, 48 * k, 848 * k, 1156 * k], radius=26 * k, fill=255)   # board
d.rounded_rectangle([418 * k, 16 * k, 482 * k, 98 * k], radius=40 * k, fill=255)     # clip ring
d.rounded_rectangle([380 * k, 66 * k, 524 * k, 118 * k], radius=8 * k, fill=255)    # clip plate
m = m.resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6))
im.putalpha(m)
# 2x Lanczos + light unsharp so the text stays crisp when the sheet is read at arm's length in the headset
im = im.resize((w * 2, h * 2), Image.LANCZOS)
rgb = im.convert('RGB').filter(ImageFilter.UnsharpMask(radius=1.6, percent=60, threshold=2))
rgb.putalpha(im.getchannel('A'))
im = rgb
out = os.path.join(ROOT, "Assets", "Resources", "Sc22", "Sc22_Sds.png")
im.save(out)
print(out, im.size)
