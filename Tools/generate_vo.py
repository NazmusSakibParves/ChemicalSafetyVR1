"""Generates the Presentable voice-over clips with edge-tts (Bangladeshi Bangla voice).

    python Tools/generate_vo.py                 # writes every clip into Assets/Art/Audio/VO (slow, overwrites)
    python Tools/generate_vo.py --only <clip>   # one or more clips, comma separated, e.g. VO_Sc03Inspect_bn
    python Tools/generate_vo.py --show          # prints the text the voice reads for every clip, writes nothing
    python Tools/generate_vo.py --variants DIR  # pronunciation variants of the SC02 instruction line into DIR

ONE VOICE, ONE PRONUNCIATION FOR THE WHOLE GAME (about 22 scenarios).
  * Every Bangla clip uses BN_VOICE, every English clip uses EN_VOICE. Never change them per scenario.
  * Write the Bangla text with the normal spelling and let PRON below respell the words the voice mispronounces.
    A word is respelled in ONE place, so it sounds the same in every scenario. Add new words to PRON, not to a clip.
  * The line that ends a scenario is built by complete_bn / complete_en: the same sentence in every scenario, only the
    numbers change. Add a scenario by adding its numbers to NUM / TO_NUM / EN_NUM.

The respelling is only what the voice reads; the screen text and the Bangla images keep the normal spelling.
Bangla is written with the precomposed letter YA-NUKTA (U+09DF) on purpose. Written as YA (U+09AF) plus the nukta sign
(U+09BC) the voice read the number six as "shoshti".
"""
import argparse
import asyncio
import os

import sys

import edge_tts

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Audio", "VO")

BN_VOICE = "bn-BD-NabanitaNeural"
EN_VOICE = "en-IN-NeerjaNeural"

# normal spelling -> what the voice has to read to sound Bangladeshi
PRON = {
    "স্বাগতম": "শাগতম",        # the voice split it as "sbag tom"
    "সঠিক": "শঠিক",            # the voice said "chothik"
    "সিদ্ধান্ত": "শিদ্ধান্ত",    # the voice said "chiddhanto"
    "পারক্সাইড": "পারঅক্সাইড",  # peroxide: the voice swallowed the vowel
}

SIX = "ছয়টি"  # ছয়টি with U+09DF

# scenario number -> how it is spoken
NUM = {1: "এক", 2: "দুই", 3: "তিন", 4: "চার"}
TO_NUM = {2: "দুইয়ে", 3: "তিনে", 4: "চারে"}  # "go to scenario N"
EN_NUM = {1: "one", 2: "two", 3: "three", 4: "four"}


def complete_bn(n):
    return "আপনি সিনারিও %s সফলভাবে সম্পন্ন করেছেন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % (NUM[n], TO_NUM[n + 1])


def complete_en(n):
    return "You have successfully completed Scenario %s. Click Next to go to Scenario %s." % (EN_NUM[n], EN_NUM[n + 1])


CLIPS = {
    "VO_AssalamuAlaikum_bn": (BN_VOICE, "আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে স্বাগতম।"),
    "VO_SC01_Instruction_bn": (
        BN_VOICE,
        "সরবরাহ করা সেফটি ডাটা শিট-এর উপর ভিত্তি করে সঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।",
    ),
    "VO_SC02_Instruction_bn": (
        BN_VOICE,
        SIX + " রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।",
    ),
    "VO_Sc02PlaceHint_bn": (BN_VOICE, "কেমিক্যালটি নির্দিষ্ট কম্পার্টমেন্টে রাখুন।"),
    # scenario endings: the same sentence every time, only the numbers change
    "VO_Sc01Complete_bn": (BN_VOICE, complete_bn(1)),
    "VO_Sc01Complete_en": (EN_VOICE, complete_en(1)),
    "VO_Sc02Complete_bn": (BN_VOICE, complete_bn(2)),
    "VO_Sc02Complete_en": (EN_VOICE, complete_en(2)),
    "VO_Sc03Complete_bn": (BN_VOICE, complete_bn(3)),
    "VO_Sc03Complete_en": (EN_VOICE, complete_en(3)),
    # Scenario 3
    "VO_SC03_Instruction_bn": (
        BN_VOICE,
        "আপনি পুরনো স্টোররুমের পেছনের তাকে একটি পুরনো অর্গানিক পারক্সাইডের ড্রাম খুঁজে পেয়েছেন। "
        "ড্রামটি বছরের পর বছর ধরে সেখানে পড়ে আছে। মেয়াদ দুই বছরের বেশি আগে শেষ হয়ে গেছে এবং "
        "ঢাকনার চারপাশে ক্রিস্টাল জমেছে। এখন আপনাকে সিদ্ধান্ত নিতে হবে কী করবেন।",
    ),
    "VO_Sc03Touch_bn": (BN_VOICE, "ড্রামের সামনের বাটনে আঙুল দিয়ে স্পর্শ করে ড্রামটি পরীক্ষা করুন।"),
    "VO_Sc03Inspect_bn": (
        BN_VOICE,
        "ড্রামটি অর্গানিক পারক্সাইড। ড্রামের গায়ে ক্রিস্টাল জমেছে এবং লেবেলের মেয়াদ শেষ হয়ে গেছে।",
    ),
    "VO_Sc03Wrong_bn": (
        BN_VOICE,
        "ভুল সিদ্ধান্ত। ক্রিস্টাল জমা অর্গানিক পারক্সাইড খোলার সময় বা ঘষা লাগলে বিস্ফোরিত হতে পারে।",
    ),
}

REST = " রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।"
VARIANTS = {
    "A_precomposed": SIX + REST,
    "B_choyta": "ছয়টা" + REST,
    "C_digit": "৬টি" + REST,
    "D_old_decomposed": "ছয়টি" + REST,
}


def spoken(text):
    for written, say in PRON.items():
        text = text.replace(written, say)
    return text


async def synth(text, voice, path):
    await edge_tts.Communicate(spoken(text), voice).save(path)
    print("wrote", path, os.path.getsize(path), "bytes")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--variants", help="folder for pronunciation variants of the SC02 instruction line")
    ap.add_argument("--only", help="comma separated clip names to generate (for example VO_Sc03Inspect_bn)")
    ap.add_argument("--show", action="store_true", help="print the text the voice reads for every clip, write nothing")
    args = ap.parse_args()
    if args.show:
        for name, (voice, text) in CLIPS.items():
            print(name, "->", spoken(text))
        return
    if args.variants:
        os.makedirs(args.variants, exist_ok=True)
        for name, text in VARIANTS.items():
            await synth(text, BN_VOICE, os.path.join(args.variants, "SC02_Instruction_" + name + ".mp3"))
        return
    only = set(args.only.split(",")) if args.only else None
    for name, (voice, text) in CLIPS.items():
        if only and name not in only:
            continue
        await synth(text, voice, os.path.join(OUT, name + ".mp3"))


if __name__ == "__main__":
    asyncio.run(main())
