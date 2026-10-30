"""Renders every Bangla string of Assets/Resources/Content/*.json to a transparent PNG.

TextMeshPro cannot shape Bengali conjuncts, so the game shows Bangla content as images made here with Pillow +
libraqm (correct shaping) and Noto Sans Bengali. The English text stays a normal TMP label.

    python Tools/render_content_bn.py            # renders only strings that changed (hash kept in _hashes.json)
    python Tools/render_content_bn.py --force    # renders everything again
    python Tools/render_content_bn.py --check    # renders nothing, exits 1 when a PNG is missing or stale

Output: Assets/Resources/BnText/<SCENARIO>/<key>.png. Key names:
  strings:   <key>
  questions: <qid>_<field> and <qid>_<optionId>_<field>   (qid dots become underscores)
Rendered at RENDER_PX pixels per font size unit: the game scales it (LocText.fontPx / RENDER_PX).
"""

import hashlib
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont, features

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "Resources", "Content")
OUT = os.path.join(ROOT, "Assets", "Resources", "BnText")
FONT_REGULAR = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Regular.ttf")
FONT_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")

RENDER_PX = 96          # font size used for every image (LocText scales to the wanted on-screen size)
WRAP_PX = 1500          # lines wrap at this width in image pixels
PAD = 10

# keys that are drawn bold on screen (titles, buttons); everything else is regular
BOLD_KEYS = ("title", "subtitle", "welcome", "start", "name_label", "contact_label", "lang_", "_header", "correctTitle",
             "wrongTitle", "nextLabel", "kb_done")


def wrap_for(key):
    # one-line messages (login errors) never wrap; the game shrinks them to the box
    return 4000 if key.startswith("err_") else WRAP_PX


def key_names(scenario, data):
    for s in data.get("strings", []):
        yield s["key"], s.get("bn", "")
    for q in data.get("questions", []):
        qid = q["id"].replace(".", "_")
        for field in ("header", "prompt", "correctTitle", "wrongTitle", "nextLabel"):
            if q.get(field):
                yield f"{qid}_{field}", q[field].get("bn", "")
        for o in q.get("options", []):
            for field in ("text", "feedback", "wrongTitle"):
                if o.get(field):
                    yield f"{qid}_{o['id']}_{field}", o[field].get("bn", "")


def wrap(draw, text, font, max_w):
    lines = []
    for para in text.split("\n"):
        cur = ""
        for word in para.split(" "):
            trial = word if not cur else cur + " " + word
            if draw.textlength(trial, font=font) <= max_w or not cur:
                cur = trial
            else:
                lines.append(cur)
                cur = word
        lines.append(cur)
    return lines


def render(text, bold, wrap_px=WRAP_PX):
    font = ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, RENDER_PX, layout_engine=ImageFont.Layout.RAQM)
    probe = ImageDraw.Draw(Image.new("RGBA", (8, 8)))
    lines = wrap(probe, text, font, wrap_px)
    asc, desc = font.getmetrics()
    line_h = asc + desc + 6
    widths = [probe.textlength(l, font=font) for l in lines]
    w = int(max(widths)) + PAD * 2
    h = line_h * len(lines) + PAD * 2
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    for i, l in enumerate(lines):
        x = (w - widths[i]) / 2  # centred; LocText aligns the whole image
        d.text((x, PAD + i * line_h), l, font=font, fill=(255, 255, 255, 255))
    return img


def main():
    force = "--force" in sys.argv
    check = "--check" in sys.argv
    if not features.check("raqm"):
        print("Pillow has no libraqm: Bangla would be shaped wrongly. Aborting.")
        return 2
    bad = 0
    done = 0
    for fn in sorted(os.listdir(CONTENT)):
        if not fn.endswith(".json"):
            continue
        scenario = fn[:-5]
        with open(os.path.join(CONTENT, fn), encoding="utf8") as f:
            data = json.load(f)
        folder = os.path.join(OUT, scenario)
        hash_path = os.path.join(folder, "_hashes.json")
        hashes = {}
        if os.path.exists(hash_path):
            with open(hash_path, encoding="utf8") as f:
                hashes = json.load(f)
        os.makedirs(folder, exist_ok=True)
        wanted = {}
        for key, bn in key_names(scenario, data):
            if not bn:
                print(f"MISSING bn text: {scenario}/{key}")
                bad += 1
                continue
            bold = any(k in key for k in BOLD_KEYS)
            wanted[key] = hashlib.sha1(f"{RENDER_PX}|{bold}|{wrap_for(key)}|{bn}".encode("utf8")).hexdigest()
        for key, h in wanted.items():
            path = os.path.join(folder, key + ".png")
            if not force and hashes.get(key) == h and os.path.exists(path):
                continue
            if check:
                print(f"STALE: {scenario}/{key}")
                bad += 1
                continue
            bold = any(k in key for k in BOLD_KEYS)
            text = next(b for k, b in key_names(scenario, data) if k == key)
            render(text, bold, wrap_for(key)).save(path)
            hashes[key] = h
            done += 1
        for stale in [k for k in hashes if k not in wanted]:
            p = os.path.join(folder, stale + ".png")
            for q in (p, p + ".meta"):
                if os.path.exists(q) and not check:
                    os.remove(q)
            hashes.pop(stale, None)
        if not check:
            with open(hash_path, "w", encoding="utf8") as f:
                json.dump(hashes, f, ensure_ascii=False, indent=1)
    print(f"rendered {done}, problems {bad}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
