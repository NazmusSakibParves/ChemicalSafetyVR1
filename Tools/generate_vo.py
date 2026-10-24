"""Generates the Presentable voice-over clips with edge-tts (Bangladeshi Bangla voice).

    python Tools/generate_vo.py                 # writes every clip into Assets/Art/Audio/VO (slow, overwrites)
    python Tools/generate_vo.py --only <clip>   # one or more clips, comma separated, e.g. VO_Sc03Inspect_bn
    python Tools/generate_vo.py --show          # prints the text the voice reads for every clip, writes nothing
    python Tools/generate_vo.py --variants DIR  # pronunciation variants of the SC02 instruction line into DIR
    python Tools/generate_vo.py --words DIR     # listening variants of the welcome / success words into DIR

ONE VOICE, ONE PRONUNCIATION FOR THE WHOLE GAME (about 22 scenarios).
  * Every Bangla clip uses BN_VOICE, every English clip uses EN_VOICE. Never change them per scenario.
  * Write the Bangla text with the normal spelling and let PRON below respell the words the voice mispronounces.
    A word is respelled in ONE place, so it sounds the same in every scenario. Add new words to PRON, not to a clip.
  * The line that ends a scenario is built by complete_bn / complete_en: the same sentence in every scenario, only the
    numbers change. Add a scenario by adding its numbers to NUM / TO_NUM / EN_NUM.

The respelling is only what the voice reads; the screen text and the Bangla images keep the normal spelling.
The number six is the precomposed letter YYA (U+09DF) inside SIX. YA (U+09AF) plus the nukta sign
(U+09BC) looks the same on screen, and the voice reads that form as "shoshti".
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
    "স্বাগতম": "স্বাগোতোম",     # the voice split it as "sbag tom"; the user asked for this spelling
    "সফল": "শফল",              # the voice said "chofol"
    "চমৎকার": "চমতকারর",        # the voice dropped the final র ("chomotka"); chosen by listening (variant B)
    "সিদ্ধান্ত": "শিদ্ধান্ত",    # the voice said "chiddhanto"
    "পারক্সাইড": "পারঅক্সাইড",  # peroxide: the voice swallowed the vowel
    "ঘন": "ঘনো",                # concentrated: the voice did not say the word clearly
}

SIX = "\u099b\u09df\u099f\u09bf"  # ছয়টি. Precomposed য় (U+09DF). Do not type this letter; editors save it as য + nukta.

# scenario number -> how it is spoken
CHHOY = "ছয়"  # ছয় (precomposed য়, see SIX)
NUM = {1: "এক", 2: "দুই", 3: "তিন", 4: "চার", 5: "পাঁচ", 6: CHHOY, 7: "সাত"}
TO_NUM = {2: "দুইয়ে", 3: "তিনে", 4: "চারে", 5: "পাঁচে", 6: CHHOY + "ে", 7: "সাতে"}  # "go to scenario N"
EN_NUM = {1: "one", 2: "two", 3: "three", 4: "four", 5: "five", 6: "six", 7: "seven"}


def complete_bn(n):
    return "আপনি সিনারিও %s সফলভাবে সম্পন্ন করেছেন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % (NUM[n], TO_NUM[n + 1])


def complete_en(n):
    return "You have successfully completed Scenario %s. Click Next to go to Scenario %s." % (EN_NUM[n], EN_NUM[n + 1])


CLIPS = {
    "VO_AssalamuAlaikum_bn": (BN_VOICE, "আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে স্বাগতম।"),
    # শঠিক is only for this clip. The voice reads সঠিক as ছঠিক here. The on-screen Bangla still says সঠিক.
    "VO_SC01_Instruction_bn": (
        BN_VOICE,
        "সরবরাহ করা সেফটি ডাটা শিট-এর উপর ভিত্তি করে শঠিক লেবেলযুক্ত ড্রামটি নির্বাচন করুন।",
    ),
    "VO_SC02_Instruction_bn": (
        BN_VOICE,
        SIX + " রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।",
    ),
    # said after a right answer in EVERY scenario: no scenario-specific words (label, drum, ...)
    "VO_Correct_bn": (BN_VOICE, "সঠিক। চমৎকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),  # third value = speaking rate
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
    "VO_Sc03Touch_bn": (BN_VOICE, "ড্রামের লেবেলে আঙুল ছুঁয়ে ধরে রাখুন। স্ক্যান শেষ হলে রিপোর্ট দেখা যাবে।"),
    "VO_Sc03Inspect_bn": (
        BN_VOICE,
        "ড্রামটি অর্গানিক পারক্সাইড। ড্রামের গায়ে ক্রিস্টাল জমেছে এবং লেবেলের মেয়াদ শেষ হয়ে গেছে।",
    ),
    "VO_Sc03Wrong_bn": (
        BN_VOICE,
        "ভুল সিদ্ধান্ত। ক্রিস্টাল জমা অর্গানিক পারক্সাইড খোলার সময় বা ঘষা লাগলে বিস্ফোরিত হতে পারে।",
    ),
    # Scenario 4
    "VO_SC04_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন ল্যাব টেকনিশিয়ান। ঘন হাইড্রোক্লোরিক অ্যাসিড দিয়ে একটি ডাই নমুনা পরীক্ষার প্রস্তুতি নিচ্ছেন। "
        "আগে সেফটি ডাটা শিট দেখুন। সঠিক গ্লাভস বেছে নিন।",
    ),
    "VO_Sc04Complete_bn": (BN_VOICE, complete_bn(4)),
    "VO_Sc04Complete_en": (EN_VOICE, complete_en(4)),
    # Scenario 5
    "VO_SC05_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন ডাইং মেশিন অপারেটর। একটি কাপড়ের ব্যাচ টলুইন-ভিত্তিক দ্রাবক দিয়ে প্রক্রিয়া করতে হবে। "
        "আজ ভেতরে বাষ্পের মাত্রা বেশি। ভেতরে ঢোকার আগে সঠিক মাস্কটি পরুন।",
    ),
    # said when a respirator is picked up: what it is and whether it is compatible with toluene (same order as the cards)
    "VO_Sc05Mask0_bn": (BN_VOICE, "এটি সাধারণ ডাস্ট মাস্ক, এন নাইনটি ফাইভ। এটি টলুইন দ্রাবকের সাথে সামঞ্জস্যপূর্ণ নয়।"),
    "VO_Sc05Mask1_bn": (
        BN_VOICE,
        "এটি অর্গানিক ভেপার কার্ট্রিজসহ হাফ-ফেস রেসপিরেটর। এটি টলুইন দ্রাবকের সাথে সামঞ্জস্যপূর্ণ।",
    ),
    "VO_Sc05Mask2_bn": (BN_VOICE, "এটি সার্জিক্যাল মাস্ক। এটি টলুইন দ্রাবকের সাথে সামঞ্জস্যপূর্ণ নয়।"),
    "VO_Sc05Mask3_bn": (
        BN_VOICE,
        "এটি শুধু পার্টিকুলেট ফিল্টারসহ ফুল-ফেস রেসপিরেটর। এটি টলুইন দ্রাবকের সাথে সামঞ্জস্যপূর্ণ নয়।",
    ),
    "VO_Sc05Complete_bn": (BN_VOICE, complete_bn(5)),
    "VO_Sc05Complete_en": (EN_VOICE, complete_en(5)),
    # Scenario 6
    "VO_SC06_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন মিক্সিং অপারেটর। আপনাকে একটি উদ্বায়ী দ্রাবক ঢালতে হবে, কিন্তু পাত্রটি খোলা থাকায় বাষ্প বাতাসে ছড়িয়ে পড়ছে। "
        "ঢালার আগে শঠিক ইঞ্জিনিয়ারিং কন্ট্রোল নিন।",  # শঠিক: the voice read সঠিক here as ছঠিক (same fix as VO_SC01_Instruction_bn); screen text keeps সঠিক
    ),
    "VO_Sc06Hood_bn": (BN_VOICE, "কন্ট্রোল প্যানেলে ফিউম হুডের সুইচটি চালু করুন।"),
    "VO_Sc06HoodOn_bn": (BN_VOICE, "ফিউম হুড চালু হয়েছে। এবার উদ্বায়ী দ্রাবকের পাত্রটি তুলুন।"),
    "VO_Sc06HoodWarn_bn": (BN_VOICE, "আগে ফিউম হুড চালু করুন।"),
    "VO_Sc06Jug_bn": (BN_VOICE, "উদ্বায়ী দ্রাবকের পাত্রটি আঙুল দিয়ে ছুঁয়ে তুলুন।"),
    "VO_Sc06Sash_bn": (BN_VOICE, "এবার ফিউম হুডের কাচের দরজা ছুঁয়ে খুলুন।"),
    "VO_Sc06Pour_bn": (BN_VOICE, "কাচের জারটি ছুঁয়ে ধীরে ধীরে দ্রাবক ঢালুন।"),
    "VO_Sc06Gate_bn": (
        BN_VOICE,
        "পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc06Complete_bn": (BN_VOICE, complete_bn(6)),
    "VO_Sc06Complete_en": (EN_VOICE, complete_en(6)),
}

REST = " রাসায়নিকের একটি চালান এসেছে। সুপারভাইজার আপনাকে প্রতিটি কন্টেইনার সঠিক কম্পার্টমেন্টে রাখতে বলেছেন।"
WORD_VARIANTS = {
    "welcome_1_sbagotom": "আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে স্বাগোতোম।",
    "welcome_2_shagotom": "আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে শাগোতোম।",
    "welcome_3_shbagotom": "আসসালামু আলাইকুম। রাসায়নিক নিরাপত্তা প্রশিক্ষণে শ্বাগোতোম।",
    "success_1_shofol": "আপনি সিনারিও তিন শফলভাবে সম্পন্ন করেছেন।",
    "success_2_sofol": "আপনি সিনারিও তিন সফলভাবে সম্পন্ন করেছেন।",
    "success_3_shophol": "আপনি সিনারিও তিন শোফলভাবে সম্পন্ন করেছেন।",
}
VARIANTS = {
    "A_precomposed": SIX + REST,
    "B_choyta": "ছয়টা" + REST,
    "C_digit": "৬টি" + REST,
    "D_old_decomposed": "\u099b\u09af\u09bc\u099f\u09bf" + REST,  # য + nukta: the voice reads this as "shoshti"
}


# listening variants for the right-answer line: (file name, text exactly as sent, rate)
CORRECT_VARIANTS = [
    ("A_tik_chomotkar", "সঠিক। চমতকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),
    ("B_chomotkarr", "সঠিক। চমতকারর! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),
    ("C_fullstop_after", "সঠিক। চমতকার। আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),
    ("D_slow", "সঠিক। চমতকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-16%"),
    ("E_hyphen", "সঠিক। চম-তকার! আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),
    ("F_pause_before_next", "সঠিক। চমতকার... আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),
    ("G_chomotkar_word_last", "সঠিক। আপনি সঠিক উত্তরটি নির্বাচন করেছেন। চমতকার!", "-8%"),
    ("H_normal_spelling_chomotkar", "সঠিক। চমৎকার। আপনি সঠিক উত্তরটি নির্বাচন করেছেন।", "-8%"),
]


def spoken(text):
    for written, say in PRON.items():
        text = text.replace(written, say)
    return text


async def synth(text, voice, path, raw=False, rate="+0%"):
    await edge_tts.Communicate(text if raw else spoken(text), voice, rate=rate).save(path)
    print("wrote", path, os.path.getsize(path), "bytes")


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--variants", help="folder for pronunciation variants of the SC02 instruction line")
    ap.add_argument("--correct", help="folder for listening variants of the right-answer line")
    ap.add_argument("--words", help="folder for listening variants of the welcome / success words (text used as written)")
    ap.add_argument("--only", help="comma separated clip names to generate (for example VO_Sc03Inspect_bn)")
    ap.add_argument("--show", action="store_true", help="print the text the voice reads for every clip, write nothing")
    args = ap.parse_args()
    if args.show:
        for name, entry in CLIPS.items():
            print(name, "->", spoken(entry[1]), entry[2] if len(entry) > 2 else "")
        return
    if args.correct:
        os.makedirs(args.correct, exist_ok=True)
        for name, text, rate in CORRECT_VARIANTS:
            await edge_tts.Communicate(text, BN_VOICE, rate=rate).save(os.path.join(args.correct, name + ".mp3"))
            print("wrote", name)
        return
    if args.words:
        os.makedirs(args.words, exist_ok=True)
        for name, text in WORD_VARIANTS.items():
            await synth(text, BN_VOICE, os.path.join(args.words, name + ".mp3"), raw=True)  # written exactly as given
        return
    if args.variants:
        os.makedirs(args.variants, exist_ok=True)
        for name, text in VARIANTS.items():
            await synth(text, BN_VOICE, os.path.join(args.variants, "SC02_Instruction_" + name + ".mp3"))
        return
    only = set(args.only.split(",")) if args.only else None
    for name, entry in CLIPS.items():
        if only and name not in only:
            continue
        await synth(entry[1], entry[0], os.path.join(OUT, name + ".mp3"), rate=entry[2] if len(entry) > 2 else "+0%")


if __name__ == "__main__":
    asyncio.run(main())
