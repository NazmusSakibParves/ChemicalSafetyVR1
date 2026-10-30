"""Renders every Bangla string of the game to a transparent PNG for BnTmp (the TMP replacement that shows Bangla as images).

TextMeshPro cannot shape Bengali, so `BnTmp` (Assets/Scripts/ChemicalSafety/Localization/BnTmp.cs) swaps any Bangla text for
an image made here with Pillow + libraqm (correct conjuncts / vowel signs) and Noto Sans Bengali (Latin letters, @, arrows come
from Segoe UI / Segoe UI Symbol because the Bengali font has none).

    python Tools/render_bn_auto.py            # renders what is missing
    python Tools/render_bn_auto.py --force    # renders everything again
    python Tools/render_bn_auto.py --check    # renders nothing; exit 1 when an image is missing

Inputs
  * every `bn` string / array element in Assets/Resources/Content/*.json, and
  * Temp/bn_requests.jsonl, written by BnTmp in the editor for every text it needed and did not find (dynamic labels).
Each text is rendered at a ladder of wrap widths (LADDER), widest last; the ladder stops at the first width that gives ONE line,
because every wider width looks identical. BnTmp picks the widest variant that fits its box, so a text that fits on one line is
never wrapped. A `<size=70%>` sub line is rendered as its own text (BnTmp stacks the lines).
Output: Assets/Resources/BnAuto/<hash>_<width>.png and Assets/Resources/BnAuto/index.json ({hash: [widths]}).
hash = first 16 hex of sha1(text + "|" + ("b" or "r")); the same function is in BnTmp.cs.
"""

import hashlib
import json
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont, features

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "Resources", "Content")
OUT = os.path.join(ROOT, "Assets", "Resources", "BnAuto")
REQ = os.path.join(ROOT, "Temp", "bn_requests.jsonl")
FONT_REGULAR = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Regular.ttf")
FONT_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")
# Noto Sans Bengali has no Latin letters, @ or arrows: those runs are drawn with these fonts
LATIN_REGULAR = "C:/Windows/Fonts/segoeui.ttf"
LATIN_BOLD = "C:/Windows/Fonts/segoeuib.ttf"
SYMBOL = "C:/Windows/Fonts/seguisym.ttf"

RENDER_PX = 80          # keep equal to BnTmp.RenderPx
LADDER = (900, 1500, 2400)
PAD = 6
LINE_H = int(RENDER_PX * 1.5)  # Bengali metrics are tall; 1.5 em keeps matras clear without airy lines
BOLD_WORDS = ("TITLE", "HEADER", "LABEL", "BUTTON")


def text_hash(text, bold):
    return hashlib.sha1((text + "|" + ("b" if bold else "r")).encode("utf8")).hexdigest()[:16]


def strip_tags(t):
    t = t.replace("<sub>2</sub>", "2")
    return re.sub(r"</?(b|i|u|s|color|size|sprite|mark|align|font|material|nobr|sub|sup|link|alpha|cspace|indent|line-height|margin|pos|rotate|voffset|width|style|lowercase|uppercase|smallcaps|gradient|page|space|mspace|noparse|allcaps)[^>]*>", "", t)


_glyph_cache = {}


def _has_glyph(font, ch):
    # a missing glyph draws the font's .notdef box: compare with a code point no font defines
    key = (id(font), ch)
    if key not in _glyph_cache:
        _glyph_cache[key] = bytes(font.getmask(ch)) != bytes(font.getmask("\u0378"))
    return _glyph_cache[key]


def font_index(fonts, ch):
    """0 = Bengali font, 1 = Latin font (letters, degree, @ ...), 2 = symbol font. Digits, space and Bengali stay Bengali."""
    if "\u0980" <= ch <= "\u09ff" or ch == " " or ch.isdigit():
        return 0
    for idx, f in ((0, fonts.bn), (1, fonts.lat), (2, fonts.sym)):
        if _has_glyph(f, ch) and not (idx == 0 and (("A" <= ch <= "Z") or ("a" <= ch <= "z"))):
            return idx
    return 1


def runs_of(text, fonts):
    runs = []
    for ch in text:
        k = font_index(fonts, ch)
        if runs and runs[-1][0] == k:
            runs[-1][1] += ch
        else:
            runs.append([k, ch])
    return runs


class Fonts:
    def __init__(self, bold):
        self.bn = ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, RENDER_PX, layout_engine=ImageFont.Layout.RAQM)
        self.lat = ImageFont.truetype(LATIN_BOLD if bold else LATIN_REGULAR, int(RENDER_PX * 0.92))
        self.sym = ImageFont.truetype(SYMBOL, int(RENDER_PX * 0.9))

    def pick(self, k):
        return (self.bn, self.lat, self.sym)[k]

    def width(self, draw, text):
        return sum(draw.textlength(t, font=self.pick(k)) for k, t in runs_of(text, self))

    def draw(self, draw, x, base, text):
        for k, t in runs_of(text, self):
            f = self.pick(k)
            draw.text((x, base), t, font=f, fill=255, anchor="ls")
            x += draw.textlength(t, font=f)


def wrap(draw, text, fonts, max_w):
    lines = []
    for para in text.split(chr(10)):
        cur = ""
        for word in para.split(" "):
            trial = word if not cur else cur + " " + word
            if fonts.width(draw, trial) <= max_w or not cur:
                cur = trial
            else:
                lines.append(cur)
                cur = word
        lines.append(cur)
    return lines


def render(text, bold, bucket):
    """Returns (image, number of lines)."""
    fonts = Fonts(bold)
    probe = ImageDraw.Draw(Image.new("RGBA", (8, 8)))
    lines = wrap(probe, strip_tags(text), fonts, bucket)
    widths = [fonts.width(probe, l) for l in lines]
    w = int(max(widths)) + PAD * 2
    h = LINE_H * len(lines) + PAD * 2
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for i, l in enumerate(lines):
        fonts.draw(d, (w - widths[i]) / 2, PAD + i * LINE_H + int(RENDER_PX * 1.12), l)
    rgba = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    rgba.putalpha(img)
    return rgba, len(lines)


SIZE_LINE = re.compile(r"^\s*<size=\d+(?:\.\d+)?%>(.*?)(</size>)?\s*$")


def split_size_lines(text):
    """A text with a <size=NN%> sub line is shown line by line by BnTmp: each line is its own image."""
    if "<size=" not in text or chr(10) not in text:
        return [text]
    out = []
    for raw in text.split(chr(10)):
        m = SIZE_LINE.match(raw)
        line = m.group(1) if m else raw
        if line.strip():
            out.append(line)
    return out


def content_strings():
    for fn in sorted(os.listdir(CONTENT)):
        if not fn.endswith(".json") or fn == "LOGIN.json":
            continue
        with open(os.path.join(CONTENT, fn), encoding="utf8") as f:
            data = json.load(f)
        for s in data.get("strings", []):
            if s.get("bn"):
                yield s["key"], s["bn"]
        for a in data.get("arrays", []):
            for i, t in enumerate(a.get("bn", [])):
                if t:
                    yield f"{a['key']}[{i}]", t


def main():
    force = "--force" in sys.argv
    check = "--check" in sys.argv
    if not features.check("raqm"):
        print("Pillow has no libraqm: Bangla would be shaped wrongly. Aborting.")
        return 2
    os.makedirs(OUT, exist_ok=True)
    wanted = set()
    for key, bn in content_strings():
        bold = any(k in key.upper() for k in BOLD_WORDS)
        for part in split_size_lines(bn):
            wanted.add((part, bold))
    if os.path.exists(REQ):
        with open(REQ, encoding="utf8") as f:
            for line in f:
                line = line.strip()
                if line:
                    r = json.loads(line)
                    wanted.add((r["text"], bool(r["bold"])))
    bad = 0
    done = 0
    keep = set()
    for text, bold in sorted(wanted):
        h = text_hash(text, bold)
        for bucket in LADDER:
            path = os.path.join(OUT, f"{h}_{bucket}.png")
            keep.add(os.path.basename(path))
            if os.path.exists(path) and not force:
                # the ladder ends at the first one-line width: a stored one-line image means no wider variant exists
                if Image.open(path).height <= LINE_H + PAD * 2 + 2:
                    break
                continue
            if check:
                print(f"MISSING image: {bucket} {text[:60]}")
                bad += 1
                break
            img, lines = render(text, bold, bucket)
            img.save(path)
            done += 1
            if lines == 1:
                break
    if not check:
        for fn in os.listdir(OUT):
            if fn.endswith(".png") and fn not in keep:
                os.remove(os.path.join(OUT, fn))
                if os.path.exists(os.path.join(OUT, fn + ".meta")):
                    os.remove(os.path.join(OUT, fn + ".meta"))
    index = {}
    for fn in os.listdir(OUT):
        m = re.match(r"^([0-9a-f]{16})_(\d+)\.png$", fn)
        if m:
            index.setdefault(m.group(1), []).append(int(m.group(2)))
    for v in index.values():
        v.sort()
    if not check:
        with open(os.path.join(OUT, "index.json"), "w", encoding="utf8") as f:
            json.dump(index, f)
    print(f"rendered {done}, images {sum(len(v) for v in index.values())}, problems {bad}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
