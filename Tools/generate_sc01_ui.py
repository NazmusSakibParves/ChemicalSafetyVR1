"""Glass-style INSTRUCTIONS panel and NEXT/Continue button for Scenario 1 (client reference picture): translucent dark-teal
glass, thin bright aqua border with a soft outer glow, rounded corners, faint diagonal sheen.

Output (re-run: python Tools/generate_sc01_ui.py) in Assets/Art/Presentable/2D/Generated:
  Glass_Panel.png   320 x 320, 9-slice (border 84)
  Glass_Button.png  256 x 112, 9-slice (border 56)
The slice borders are set by PresentableSceneBuilder.ConfigureImports.
"""
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Generated")
SS = 3  # supersampling


def rounded_mask(size, inset, radius):
    w, h = size
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle((inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1), radius * SS, fill=255)
    return m


def glass(name, size, radius, margin, top_rgb, bot_rgb, top_a, bot_a, border_w, border_rgb, glow_a, sheen):
    w, h = size
    mask = rounded_mask(size, margin, radius)                       # the glass body

    # vertical gradient fill
    ys = np.linspace(0, 1, h * SS, dtype=np.float32)[:, None, None]
    top = np.array(top_rgb + (top_a,), np.float32)[None, None, :]
    bot = np.array(bot_rgb + (bot_a,), np.float32)[None, None, :]
    grad = top * (1 - ys) + bot * ys
    grad = np.broadcast_to(grad, (h * SS, w * SS, 4)).copy()
    body = Image.fromarray(grad.astype(np.uint8), "RGBA")

    # diagonal sheen across the upper-left
    xx, yy = np.meshgrid(np.linspace(0, 1, w * SS, dtype=np.float32), np.linspace(0, 1, h * SS, dtype=np.float32))
    band = np.exp(-(((xx * 1.0 + yy * 0.55) - 0.55) / 0.12) ** 2) * sheen
    sh = np.zeros((h * SS, w * SS, 4), np.float32)
    sh[..., :3] = 255
    sh[..., 3] = band * 255
    sheen_im = Image.fromarray(sh.astype(np.uint8), "RGBA")
    body = Image.alpha_composite(body, sheen_im)

    # inner soft light just inside the border
    ring = ImageChops.subtract(mask, mask.filter(ImageFilter.GaussianBlur(7 * SS)))
    inner = Image.new("RGBA", body.size, border_rgb + (0,))
    inner.putalpha(ring.point(lambda v: int(v * 0.5)))
    body = Image.alpha_composite(body, inner)
    body.putalpha(ImageChops.multiply(body.getchannel("A"), mask))

    # border line (outer edge of the mask minus an inset copy)
    inset = rounded_mask(size, margin + border_w, max(2, radius - border_w))
    border_mask = ImageChops.subtract(mask, inset)
    border = Image.new("RGBA", body.size, border_rgb + (255,))
    border.putalpha(border_mask)
    # a thinner, brighter core line inside the border
    core_in = rounded_mask(size, margin + border_w + 3, max(2, radius - border_w - 3))
    core_mask = ImageChops.subtract(inset, core_in).point(lambda v: int(v * 0.35))
    core = Image.new("RGBA", body.size, (210, 255, 255, 255))
    core.putalpha(core_mask)

    # outer glow
    glow_src = border_mask.filter(ImageFilter.GaussianBlur(5 * SS))
    glow = Image.new("RGBA", body.size, border_rgb + (255,))
    glow.putalpha(glow_src.point(lambda v: min(255, int(v * glow_a))))

    out = Image.alpha_composite(glow, body)
    out = Image.alpha_composite(out, core)
    out = Image.alpha_composite(out, border)
    out = out.resize((w, h), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    out.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, out.size)


def divider(name="Glass_Divider", size=(512, 24)):
    """Thin glowing aqua line that fades out at both ends (header underline, footer line, top cap)."""
    w, h = size
    xs = np.linspace(0, 1, w, dtype=np.float32)
    fade = np.clip(np.minimum(xs, 1 - xs) / 0.16, 0, 1)
    fade = fade * fade * (3 - 2 * fade)
    ys = np.arange(h, dtype=np.float32) - (h - 1) / 2.0
    core = np.exp(-(ys / 1.1) ** 2)[:, None]
    glow = np.exp(-(ys / 5.0) ** 2)[:, None] * 0.38
    a = np.clip(core + glow, 0, 1) * fade[None, :]
    rgb = np.zeros((h, w, 4), np.float32)
    rgb[..., 0] = 150 * core + 70 * (1 - core)
    rgb[..., 1] = 244 * core + 226 * (1 - core)
    rgb[..., 2] = 252 * core + 240 * (1 - core)
    rgb[..., 3] = a * 255
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgb.astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, (w, h))


if __name__ == "__main__":
    divider()
    # panel: darker, more transparent (the hall shows through), thin border
    glass("Glass_Panel", (320, 320), 44, 18, (32, 90, 102), (18, 64, 78), 150, 185, 5, (88, 228, 240), 1.6, 0.07)
    # button: brighter teal, more opaque, stronger glow
    glass("Glass_Button", (256, 112), 32, 14, (26, 120, 134), (16, 88, 102), 225, 240, 4, (104, 236, 244), 2.4, 0.10)
