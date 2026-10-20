"""Generates Scenario 2 voice-over clips with edge-tts (Bangladeshi Bangla voice).

    python Tools/generate_vo.py                 # writes the final clips into Assets/Art/Audio/VO
    python Tools/generate_vo.py --variants DIR  # writes pronunciation variants of the instruction line into DIR

Bangla text is written with the precomposed letter YA-NUKTA (U+09DF) on purpose. Written as YA (U+09AF) plus the
nukta sign (U+09BC) the voice read the number six as "shoshti".
"""
import argparse
import asyncio
import os

import edge_tts

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Audio", "VO")

BN_VOICE = "bn-BD-NabanitaNeural"
EN_VOICE = "en-IN-NeerjaNeural"  # same voice as VO_Sc01Complete_en

SIX = "ছয়টি"  # ছয়টি with U+09DF

CLIPS = {
    # The voice says "সঠিক" as "chothik" and "স্বাগতম" with a break ("sbag tom"). Bangladeshi speech has no
    # [sw] / [ch] there, so the words are respelled the way they are spoken: শঠিক, শাগতম.
    "VO_AssalamuAlaikum_bn": (
        BN_VOICE,
        "আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে শাগতম।",
    ),
    "VO_SC01_Instruction_bn": (
        BN_VOICE,
        "সরবরাহ করা সেফটি ডাটা শিট-এর উপর ভিত্তি করে শঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।",
    ),
    "VO_SC02_Instruction_bn": (
        BN_VOICE,
        SIX + " রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার শঠিক কম্পার্টমেন্টে রাখতে বলেছেন।",
    ),
    "VO_Sc02Complete_bn": (
        BN_VOICE,
        "আপনি সিনারিও দুই সম্পন্ন করেছেন। সিনারিও তিনে যেতে নেক্সট বাটনে ক্লিক করুন।",
    ),
    "VO_Sc02PlaceHint_bn": (
        BN_VOICE,
        "কেমিক্যালটি নির্দিষ্ট কম্পার্টমেন্টে রাখুন।",
    ),
    "VO_Sc02Complete_en": (
        EN_VOICE,
        "You have completed Scenario two. Click Next to go to Scenario three.",
    ),
}

REST = " রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।"
VARIANTS = {
    "A_precomposed": SIX + REST,
    "B_choyta": "ছয়টা" + REST,
    "C_digit": "৬টি" + REST,
    "D_old_decomposed": "ছয়টি" + REST,
}


async def synth(text, voice, path):
    await edge_tts.Communicate(text, voice).save(path)
    print("wrote", path, os.path.getsize(path), "bytes")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--variants", help="folder for pronunciation variants of the SC02 instruction line")
    ap.add_argument("--only", help="generate just this clip name (for example VO_Sc02PlaceHint_bn)")
    args = ap.parse_args()
    if args.variants:
        os.makedirs(args.variants, exist_ok=True)
        for name, text in VARIANTS.items():
            await synth(text, BN_VOICE, os.path.join(args.variants, "SC02_Instruction_" + name + ".mp3"))
        return
    for name, (voice, text) in CLIPS.items():
        if args.only and args.only != name:
            continue
        await synth(text, voice, os.path.join(OUT, name + ".mp3"))


if __name__ == "__main__":
    asyncio.run(main())
