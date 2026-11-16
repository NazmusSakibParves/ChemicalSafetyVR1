"""Scenario 21 safety data sheet: the client's sds.jpg has a blurred workshop photo behind the clipboard.
The background is made transparent with a rounded-rectangle mask of the clipboard board + its clip.
Run: python -I Tools/generate_sc21_sds.py "<path to sds.jpg>"  -> Assets/Resources/Sc21/Sc21_Sds.png"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
im = Image.open(sys.argv[1]).convert("RGBA")
w, h = im.size
k = 4  # supersample the mask for smooth edges
m = Image.new("L", (w * k, h * k), 0)
d = ImageDraw.Draw(m)
d.rounded_rectangle([40 * k, 58 * k, 857 * k, 1166 * k], radius=26 * k, fill=255)   # board
d.rounded_rectangle([408 * k, 4 * k, 488 * k, 100 * k], radius=40 * k, fill=255)     # clip ring
d.rounded_rectangle([362 * k, 88 * k, 534 * k, 128 * k], radius=8 * k, fill=255)    # clip plate
m = m.resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6))
im.putalpha(m)
# 2x Lanczos + light unsharp so the text stays crisp when the sheet is read at arm's length in the headset
im = im.resize((w * 2, h * 2), Image.LANCZOS)
rgb = im.convert('RGB').filter(ImageFilter.UnsharpMask(radius=1.6, percent=60, threshold=2))
rgb.putalpha(im.getchannel('A'))
im = rgb
out = os.path.join(ROOT, "Assets", "Resources", "Sc21", "Sc21_Sds.png")
im.save(out)
print(out, im.size)
