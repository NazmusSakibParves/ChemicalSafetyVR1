"""Renders correctly shaped Bangla strings to transparent PNGs for Unity.

TextMeshPro cannot reorder Bangla vowel signs / conjuncts, so static Bangla UI
text in the Presentable demo is shipped as images. Re-run after editing TEXTS:

    python Tools/render_bangla_text.py
"""

import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_REGULAR = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Regular.ttf")
FONT_BOLD = os.path.join(ROOT, "Assets", "Art", "Fonts", "NotoSansBengali-Bold.ttf")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "BanglaText")

# name: (text, font size px, bold, max line width px)
TEXTS = {
    "BN_Salam": ("আসসালামু আলাইকুম", 96, True, 1800),
    "BN_WelcomeSub": ("আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে স্বাগতম।", 64, False, 1700),
    "BN_InstructionSub": (
        "সরবরাহ করা সেফটি ডাটা শিট-এর উপর ভিত্তি করে সঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।",
        64, False, 1700),
    "BN_InstructionPanel": (
        "(ভয়েস-ওভার: সরবরাহ করা সেফটি ডাটা শিট (SDS)-এর উপর ভিত্তি করে সঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।)",
        72, False, 1650),
    "BN_StandHere": ("এখানে দাঁড়ান", 88, True, 1200),
    "BN_CorrectSub": ("সঠিক। চমৎকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", 64, False, 1700),
    "BN_WrongSub": ("ভুল নির্বাচন। আবার চেষ্টা করুন।", 64, False, 1700),
    "BN_ClipboardHint": ("X বাটন: SDS দেখুন / লুকান", 56, False, 1200),
    "BN_ZoomHint": ("A / B বাটন: জুম ইন / আউট", 56, False, 1200),
    "BN_Sign_SafetyFirst": ("নিরাপত্তা প্রথম", 120, True, 1600),
    "BN_Sign_Danger": ("সাবধান: বিপজ্জনক উপকরণ", 110, True, 2000),
    "BN_Sign_Equipment": ("নিরাপত্তা উপকরণ", 120, True, 1600),
    "BN_PickLabel": ("ড্রাম গুলাতে সঠিক লেবেল নির্বাচন করুন", 64, False, 1700),
    "BN_WrongLabel": (
        "আপনি ড্রামটির জন্য ভুল লেবেল বাছাই করেছেন, পুনরায় সঠিক লেবেল টি নির্বাচন করুন",
        64, False, 1700),
    "BN_ContinueHint": (
        "Instructions টা পুনরায় শুনতে speaker icon এ ক্লিক করুন অথবা এগিয়ে যেতে Continue বাটনে ক্লিক করুন",
        64, False, 1700),
    "BN_TouchDrum": ("যেকোনো একটি ড্রামের সামনের বাটনে আঙুল দিয়ে স্পর্শ করুন", 64, False, 1700),
    "BN_Carrier": ("সঠিক ড্রামটি ট্রান্সফারের জন্য ট্রলিতে তুলুন", 64, True, 1100),
    "BN_NextZone": ("পরবর্তী ধাপ: রাসায়নিক সংরক্ষণ এলাকা", 64, False, 1700),
    "BN_Sc02Instruction": (
        "ছয়টি রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।",
        64, False, 1700),
    "BN_Sc02InstructionPanel": (
        "(ভয়েস-ওভার: ছয়টি রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।)",
        64, False, 1650),
    "BN_Sc03InstructionPanel": (
        "(ভয়েস-ওভার: আপনি পুরনো স্টোররুমের পেছনের তাকে একটি পুরনো অর্গানিক পারক্সাইডের ড্রাম খুঁজে পেয়েছেন। মেয়াদ দুই বছরের বেশি আগে শেষ এবং ঢাকনার চারপাশে ক্রিস্টাল জমেছে। আপনাকে সিদ্ধান্ত নিতে হবে কী করবেন।)",
        60, False, 1650),
    "BN_Sc03Touch": ("ড্রামের লেবেলে আঙুল ছুঁয়ে ধরে রাখুন, স্ক্যান শেষ হলে রিপোর্ট দেখা যাবে", 64, False, 1700),
    "BN_Sc04InstructionPanel": (
        "(ভয়েস-ওভার: আপনি একজন ল্যাব টেকনিশিয়ান। ঘন হাইড্রোক্লোরিক অ্যাসিড দিয়ে একটি ডাই নমুনা পরীক্ষার প্রস্তুতি নিচ্ছেন। আগে সেফটি ডাটা শিট দেখুন। সঠিক গ্লাভস বেছে নিন।)",
        58, False, 1650),
    "BN_Sc05InstructionPanel": (
        "(ভয়েস-ওভার: আপনি একজন ডাইং মেশিন অপারেটর। একটি কাপড়ের ব্যাচ টলুইন-ভিত্তিক দ্রাবক দিয়ে প্রক্রিয়া করতে হবে। আজ ভেতরে বাষ্পের মাত্রা বেশি। ভেতরে ঢোকার আগে সঠিক মাস্কটি পরুন।)",
        58, False, 1650),
}

PAD = 12
# Noto Sans Bengali has no Latin glyphs, so ASCII letters/digits use a Latin font.
LATIN_REGULAR = r"C:\Windows\Fonts\segoeui.ttf"
LATIN_BOLD = r"C:\Windows\Fonts\segoeuib.ttf"


def is_latin(ch):
    return ch.isascii() and ch.isalnum()


def runs(text):
    """Splits text into (is_latin, chunk) runs; spaces / punctuation stay with the current run."""
    out = []
    for ch in text:
        latin = is_latin(ch) if (ch.isalnum() or not ch.isascii()) else None
        if out and (latin is None or latin == out[-1][0]):
            out[-1][1] += ch
        else:
            out.append([bool(latin), ch])
    return out


def measure(draw, text, fonts):
    return sum(draw.textlength(chunk, font=fonts[lat]) for lat, chunk in runs(text))


def wrap(draw, text, fonts, max_w):
    words = text.split(" ")
    lines, cur = [], ""
    for w in words:
        trial = w if not cur else cur + " " + w
        if measure(draw, trial, fonts) <= max_w:
            cur = trial
        else:
            if cur:
                lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def render(name, text, size, bold, max_w):
    fonts = {
        False: ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, size,
                                  layout_engine=ImageFont.Layout.RAQM),
        True: ImageFont.truetype(LATIN_BOLD if bold else LATIN_REGULAR, int(size * 0.92),
                                 layout_engine=ImageFont.Layout.RAQM),
    }
    probe = ImageDraw.Draw(Image.new("RGBA", (8, 8)))
    lines = wrap(probe, text, fonts, max_w)
    line_h = int(size * 1.45)
    width = int(max(measure(probe, l, fonts) for l in lines)) + PAD * 2
    height = line_h * len(lines) + PAD * 2

    img = Image.new("RGBA", (width, height), (255, 255, 255, 0))
    draw = ImageDraw.Draw(img)
    for i, line in enumerate(lines):
        x = (width - measure(draw, line, fonts)) / 2
        for lat, chunk in runs(line):
            dy = int(size * 0.03) if lat else 0
            draw.text((x, PAD + i * line_h + dy), chunk, font=fonts[lat], fill=(255, 255, 255, 255))
            x += draw.textlength(chunk, font=fonts[lat])

    bbox = img.getbbox()
    if bbox:
        img = img.crop((max(0, bbox[0] - PAD), max(0, bbox[1] - PAD),
                        min(width, bbox[2] + PAD), min(height, bbox[3] + PAD)))
    img.save(os.path.join(OUT_DIR, name + ".png"))
    return img.size


def main():
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", help="comma separated image names, so the other Bangla cards are not rewritten")
    args = ap.parse_args()
    only = set(args.only.split(",")) if args.only else None
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, (text, size, bold, max_w) in TEXTS.items():
        if only and name not in only:
            continue
        print(name, render(name, text, size, bold, max_w))


if __name__ == "__main__":
    main()
