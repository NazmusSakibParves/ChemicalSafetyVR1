"""Generates the login screen art (Assets/Resources/Login/*.png) in the style of Docs/Login/LoginReference.jpg.

    python Tools/generate_login_ui.py

The panel / fields / buttons are drawn here (2x supersampled); the backdrop is the reference photo with the panel,
molecules and logo bar removed (diffusion fill) so the 3D molecules, tubes and particles move over a clean lab;
the partner logo bar and the BMQAS badge are made from the client's logo files in Docs/Login/Logos.
"""

import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REF = os.path.join(ROOT, "Docs", "Login", "LoginReference.jpg")
OUT = os.path.join(ROOT, "Assets", "Resources", "Login")
os.makedirs(OUT, exist_ok=True)

SS = 2
CYAN = (79, 195, 255)
CYAN_SOFT = (120, 200, 235)


def save(img, name):
    img.save(os.path.join(OUT, name))


def glow(layer, radius, strength=1.0):
    blur = layer.filter(ImageFilter.GaussianBlur(radius * SS))
    if strength != 1.0:
        a = blur.getchannel("A").point(lambda v: min(255, int(v * strength)))
        blur.putalpha(a)
    return Image.alpha_composite(blur, layer)


def rrect(d, box, r, **kw):
    d.rounded_rectangle([int(v * SS) for v in box], radius=int(r * SS), **kw)


def vgrad(size, top, bottom):
    w, h = size
    arr = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        t = y / max(1, h - 1)
        arr[y, :, :] = [int(top[i] + (bottom[i] - top[i]) * t) for i in range(4)]
    return Image.fromarray(arr, "RGBA")


def field(w, h, focus=False, error=False):
    W, H = w * SS, h * SS
    base = vgrad((W, H), (46, 92, 112, 235), (22, 56, 74, 235))
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius=14 * SS, fill=255)
    base.putalpha(ImageChops.multiply(base.getchannel("A"), mask))
    line = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(line)
    col = (255, 90, 80, 255) if error else ((120, 235, 255, 255) if focus else (130, 190, 215, 235))
    d.rounded_rectangle([2, 2, W - 3, H - 3], radius=14 * SS, outline=col, width=(4 if focus else 3) * SS // 2)
    d.rounded_rectangle([8 * SS, 5 * SS, W - 8 * SS, 6 * SS], radius=2 * SS, fill=(255, 255, 255, 40))
    out = Image.alpha_composite(base, line)
    if focus or error:
        halo = Image.new("RGBA", (W + 60 * SS, H + 60 * SS), (0, 0, 0, 0))
        ImageDraw.Draw(halo).rounded_rectangle([30 * SS, 30 * SS, W + 30 * SS, H + 30 * SS], radius=14 * SS,
                                               outline=col[:3] + (200,), width=5 * SS)
        halo = halo.filter(ImageFilter.GaussianBlur(9 * SS))
        halo.alpha_composite(out, (30 * SS, 30 * SS))
        out = halo.resize((w + 60, h + 60), Image.LANCZOS)
        return out
    return out.resize((w, h), Image.LANCZOS)


def start_btn(w, h, hot=False):
    W, H = w * SS, h * SS
    top, bot = ((34, 150, 160, 245), (18, 96, 110, 245)) if not hot else ((60, 200, 210, 250), (30, 140, 155, 250))
    base = vgrad((W, H), top, bot)
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, H - 1], radius=16 * SS, fill=255)
    base.putalpha(ImageChops.multiply(base.getchannel("A"), mask))
    line = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(line)
    d.rounded_rectangle([2, 2, W - 3, H - 3], radius=16 * SS, outline=(110, 230, 255, 255), width=3 * SS)
    # inner bracket accents like the reference
    for x0, x1 in ((14, 40), (w - 40, w - 14)):
        d.line([x0 * SS, 12 * SS, x1 * SS, 12 * SS], fill=(150, 240, 255, 230), width=2 * SS)
        d.line([x0 * SS, (h - 12) * SS, x1 * SS, (h - 12) * SS], fill=(150, 240, 255, 230), width=2 * SS)
    out = Image.alpha_composite(base, line)
    halo = Image.new("RGBA", (W + 80 * SS, H + 80 * SS), (0, 0, 0, 0))
    ImageDraw.Draw(halo).rounded_rectangle([40 * SS, 40 * SS, W + 40 * SS, H + 40 * SS], radius=16 * SS,
                                           outline=(90, 220, 255, 230 if hot else 150), width=6 * SS)
    halo = halo.filter(ImageFilter.GaussianBlur(10 * SS))
    halo.alpha_composite(out, (40 * SS, 40 * SS))
    return halo.resize((w + 80, h + 80), Image.LANCZOS)


def panel(w=860, h=860):
    W, H = w * SS, h * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    x0, y0, x1, y1 = 34, 34, w - 44, h - 62  # frame box
    ch = 34  # corner chamfer

    def poly(inset=0):
        a, b, c, e = x0 + inset, y0 + inset, x1 - inset, y1 - inset
        return [(a + ch, b), (c - ch, b), (c, b + ch), (c, e - ch), (c - ch, e), (a + ch, e), (a, e - ch), (a, b + ch)]

    fill = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(fill)
    d.polygon([(x * SS, y * SS) for x, y in poly()], fill=(4, 18, 32, 218))
    # faint circuit traces in the lower body
    rng = np.random.default_rng(7)
    for _ in range(26):
        sx = int(rng.integers(x0 + 20, x1 - 20))
        sy = int(rng.integers(215, y1 - 20))
        ln = int(rng.integers(40, 150))
        d.line([sx * SS, sy * SS, (sx + ln) * SS, sy * SS], fill=(40, 110, 150, 40), width=2 * SS)
        d.line([(sx + ln) * SS, sy * SS, (sx + ln + 18) * SS, (sy + 18) * SS], fill=(40, 110, 150, 40), width=2 * SS)
    img = Image.alpha_composite(img, fill)

    lines = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(lines)
    pts = [(x * SS, y * SS) for x, y in poly(0)]
    d.line(pts + [pts[0]], fill=CYAN + (255,), width=4 * SS, joint="curve")
    pts2 = [(x * SS, y * SS) for x, y in poly(10)]
    d.line(pts2 + [pts2[0]], fill=(60, 140, 200, 150), width=2 * SS, joint="curve")
    # header divider with the centre dips of the reference
    yy = 184
    d.line([(x0 + 14) * SS, yy * SS, 160 * SS, yy * SS, 190 * SS, (yy + 10) * SS, (w - 190) * SS, (yy + 10) * SS,
            (w - 160) * SS, yy * SS, (x1 - 14) * SS, yy * SS], fill=(90, 190, 255, 230), width=3 * SS, joint="curve")
    # corner brackets and side notches
    for (cx, cy, sx, sy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        d.line([(cx + sx * 2) * SS, (cy + sy * 60) * SS, (cx + sx * 2) * SS, (cy + sy * 20) * SS], fill=(150, 235, 255, 255),
               width=5 * SS)
    for ny in (300, 560):
        d.line([(x0 - 3) * SS, ny * SS, (x0 - 3) * SS, (ny + 70) * SS], fill=(120, 215, 255, 220), width=5 * SS)
        d.line([(x1 + 3) * SS, ny * SS, (x1 + 3) * SS, (ny + 70) * SS], fill=(120, 215, 255, 220), width=5 * SS)
    img = Image.alpha_composite(img, glow(lines, 5, 1.1))
    return img.resize((w, h), Image.LANCZOS)


def icon_user(s=110):
    S = s * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = CYAN_SOFT + (255,)
    k = SS
    d.ellipse([34 * k, 8 * k, 76 * k, 58 * k], outline=c, width=4 * k)
    d.arc([6 * k, 58 * k, 104 * k, 150 * k], 195, 345, fill=c, width=4 * k)
    d.line([8 * k, 98 * k, 102 * k, 98 * k], fill=(0, 0, 0, 0), width=1)
    return glow(img, 3, 1.0).resize((s, s), Image.LANCZOS)


def icon_mail(w=110, h=84):
    W, H = w * SS, h * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = CYAN_SOFT + (255,)
    k = SS
    d.rounded_rectangle([5 * k, 5 * k, (w - 5) * k, (h - 5) * k], radius=4 * k, outline=c, width=4 * k)
    d.line([5 * k, 8 * k, (w / 2) * k, (h * 0.58) * k, (w - 5) * k, 8 * k], fill=c, width=4 * k, joint="curve")
    d.line([5 * k, (h - 8) * k, (w * 0.38) * k, (h * 0.46) * k], fill=c, width=3 * k)
    d.line([(w - 5) * k, (h - 8) * k, (w * 0.62) * k, (h * 0.46) * k], fill=c, width=3 * k)
    return glow(img, 3, 1.0).resize((w, h), Image.LANCZOS)


def toggle_bg(w=394, h=54):
    W, H = w * SS, h * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=14 * SS, fill=(10, 36, 54, 235), outline=(90, 160, 200, 230),
                        width=3 * SS)
    return img.resize((w, h), Image.LANCZOS)


def toggle_on(w=190, h=44):
    W, H = (w + 40) * SS, (h + 40) * SS
    base = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(base)
    d.rounded_rectangle([20 * SS, 20 * SS, (w + 20) * SS, (h + 20) * SS], radius=12 * SS, fill=(60, 200, 235, 255))
    halo = base.filter(ImageFilter.GaussianBlur(10 * SS))
    top = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(top).rounded_rectangle([20 * SS, 20 * SS, (w + 20) * SS, (h + 20) * SS], radius=12 * SS,
                                          fill=(24, 110, 140, 255), outline=(170, 245, 255, 255), width=3 * SS)
    out = Image.alpha_composite(halo, top)
    return out.resize((w + 40, h + 40), Image.LANCZOS)


def key_img(s=76, accent=False):
    W = s * SS
    img = vgrad((W, W), (50, 96, 120, 240), (24, 58, 78, 240)) if not accent else vgrad(
        (W, W), (40, 150, 170, 245), (20, 96, 112, 245))
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, W - 1], radius=12 * SS, fill=255)
    img.putalpha(ImageChops.multiply(img.getchannel("A"), mask))
    ImageDraw.Draw(img).rounded_rectangle([2, 2, W - 3, W - 3], radius=12 * SS, outline=(130, 200, 225, 240), width=2 * SS)
    return img.resize((s, s), Image.LANCZOS)


def backdrop():
    ref = Image.open(REF).convert("RGB")
    a = np.asarray(ref).astype(np.float32)
    h, w = a.shape[:2]
    mask = np.zeros((h, w), bool)
    for x0, y0, x1, y1 in ((306, 78, 722, 496), (230, 474, 800, 559), (178, 176, 296, 452), (740, 352, 880, 458),
                           (218, 68, 302, 146), (728, 50, 822, 132)):
        mask[y0:y1, x0:x1] = True
    out = a.copy()
    out[mask] = a[~mask].mean(axis=0)
    soft = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(5))).astype(np.float32)[..., None] / 255.0
    # diffusion fill: repeatedly average the neighbours inside the hole
    for _ in range(900):
        p = np.pad(out, ((1, 1), (1, 1), (0, 0)), mode="edge")
        avg = (p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]) / 4.0
        out[mask] = avg[mask]
    out = a * (1 - soft) + out * soft
    img = Image.fromarray(out.astype(np.uint8)).resize((2048, 1118), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.4))
    img.save(os.path.join(OUT, "Login_Backdrop.png"))


def crops():
    """Partner logos supplied by the client (Docs/Login/Logos): NOSHTRI, German Cooperation, GIZ on one white card, BMQAS badge."""
    logo_dir = os.path.join(ROOT, "Docs", "Login", "Logos")

    def load(name):
        return Image.open(os.path.join(logo_dir, name + ".jpg")).convert("RGB")

    H = 200
    parts = []
    for name in ("noshtri", "german_cooperation", "giz"):
        im = load(name)
        if name == "noshtri":  # light grey background -> white, drop the photo border
            im = im.crop((24, 0, im.width - 24, im.height)).point(lambda v: min(255, int(v * 255 / 238)))
        else:  # the supplied files carry a baked chequerboard: light, colourless pixels become white
            a = np.asarray(im).astype(np.int16)
            light = (a.min(axis=2) > 196) & ((a.max(axis=2) - a.min(axis=2)) < 22)
            a[light] = 255
            im = Image.fromarray(a.astype(np.uint8))
        parts.append(im.resize((int(im.width * H / im.height), H), Image.LANCZOS))
    gap = 70
    W = sum(p.width for p in parts) + gap * (len(parts) + 1)
    card = Image.new("RGBA", (W, H + 40), (255, 255, 255, 255))
    x = gap
    for p in parts:
        card.paste(p, (x, 20))
        x += p.width + gap
    mask = Image.new("L", card.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, card.width - 1, card.height - 1], radius=26, fill=255)
    card.putalpha(mask)
    save(card, "Login_Logos.png")

    badge = load("bmqas").convert("RGBA")
    for corner in ((0, 0), (badge.width - 1, 0), (0, badge.height - 1), (badge.width - 1, badge.height - 1)):
        ImageDraw.floodfill(badge, corner, (255, 255, 255, 0), thresh=40)
    badge = badge.resize((badge.width * 2, badge.height * 2), Image.LANCZOS)
    save(badge, "Login_Bmqas.png")


def main():
    save(panel(), "Login_Panel.png")
    save(field(496, 72), "Login_Field.png")
    save(field(496, 72, focus=True), "Login_FieldFocus.png")
    save(field(496, 72, error=True), "Login_FieldError.png")
    save(start_btn(624, 70), "Login_Start.png")
    save(start_btn(624, 70, hot=True), "Login_StartHot.png")
    save(icon_user(), "Login_IconUser.png")
    save(icon_mail(), "Login_IconMail.png")
    save(toggle_bg(), "Login_ToggleBg.png")
    save(toggle_on(), "Login_ToggleOn.png")
    save(key_img(), "Login_Key.png")
    save(key_img(accent=True), "Login_KeyAccent.png")
    crops()
    backdrop()
    print("login art written to", OUT)


if __name__ == "__main__":
    main()
