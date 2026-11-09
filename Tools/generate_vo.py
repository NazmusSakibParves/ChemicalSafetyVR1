"""Generates the Presentable voice-over clips with edge-tts (Bangladeshi Bangla voice).

    python Tools/generate_vo.py                 # writes every clip into Assets/Art/Audio/VO (slow, overwrites)
    python Tools/generate_vo.py --only <clip>   # one or more clips, comma separated, e.g. VO_Sc03Inspect_bn
    python Tools/generate_vo.py --show          # prints the text the voice reads for every clip, writes nothing
    python Tools/generate_vo.py --en            # English twins into Assets/Resources/VoEn (--show only prints the text)
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
    "নিরাপদে": "নিরাপোদে",       # "safely": the voice split it as "nirap de"; keep it one word
    "বাতাস": "বাতাশ",            # "air": the voice said "batach"
    "সরাতে": "শরাতে",            # "to remove": the voice said "chorate"
    "জিনিস": "জিনিশ",            # "thing": the voice said "jinich"
    "ঘন": "ঘনো",                # concentrated: the voice did not say the word clearly
    "বড় ": "বড়ো ",            # "big": the voice said a short "bor"; the user asked for "boro" (trailing space: whole word only)
    # প্রয়োজনীয় must be heard as pro-yo-jo-ni-yo. প্রোযোজনীয় dropped the য় (it sounded like pro-jo). প্রোয়ো keeps that য়.
    "প্রয়োজনীয়": "\u09aa\u09cd\u09b0\u09cb\u09df\u09cb\u099c\u09a8\u09c0\u09df",
}

SIX = "\u099b\u09df\u099f\u09bf"  # ছয়টি. Precomposed য় (U+09DF). Do not type this letter; editors save it as য + nukta.

# scenario number -> how it is spoken
CHHOY = "ছয়"  # ছয় (precomposed য়, see SIX)
NUM = {1: "এক", 2: "দুই", 3: "তিন", 4: "চার", 5: "পাঁচ", 6: CHHOY, 7: "সাত", 8: "আট", 9: "নয়", 10: "দশ", 11: "এগারো", 12: "বারো"}
TO_NUM = {2: "দুইয়ে", 3: "তিনে", 4: "চারে", 5: "পাঁচে", 6: CHHOY + "ে", 7: "সাতে", 8: "আটে", 9: "নয়ে", 10: "দশে", 11: "এগারোতে", 12: "বারোতে", 13: "তেরোতে"}  # "go to scenario N"
EN_NUM = {1: "one", 2: "two", 3: "three", 4: "four", 5: "five", 6: "six", 7: "seven", 8: "eight", 9: "nine", 10: "ten", 11: "eleven", 12: "twelve", 13: "thirteen"}


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
    # Scenario 2, controller-ray flow (Take Decision box, inspect, pick, compartment popup)
    "VO_Sc02Decision_bn": (BN_VOICE, "সিদ্ধান্ত নিন। বিপদের তথ্য দেখতে কন্টেইনারগুলো পরিদর্শন করুন, অথবা রাখতে প্রস্তুত হলে কন্টিনিউ বাটনে ক্লিক করুন।"),
    "VO_Sc02Inspect_bn": (BN_VOICE, "বৈশিষ্ট্য দেখতে একটি কন্টেইনার নির্বাচন করুন। বন্ধ করে পরেরটি দেখুন।"),
    "VO_Sc02Pick_bn": (BN_VOICE, "যে কন্টেইনারটি রাখতে চান সেটি নির্বাচন করুন।"),
    "VO_Sc02Dest_bn": (BN_VOICE, "এটি কোথায় রাখবেন?"),
    # scenario endings: the same sentence every time, only the numbers change
    "VO_Sc01Complete_bn": (BN_VOICE, complete_bn(1)),
    "VO_Sc01Complete_en": (EN_VOICE, complete_en(1)),
    # wrong answer in Scenario 1 (no retry): the reasons are on screen, NEXT goes on
    "VO_Sc01WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[2]),
    "VO_Sc01WrongNext_en": (EN_VOICE, "Wrong answer. Read the reason for each option. Click Next to go to Scenario two."),
    # Scenario 2: wrong answer, the reasons are shown and the scenario goes on by itself / on the last question with NEXT
    "VO_AnswerWrongSee_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন।"),
    "VO_Sc02WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[3]),
    "VO_Sc02WrongNext_en": (EN_VOICE, "Wrong answer. Read the reason for each option. Click Next to go to Scenario three."),
    # Scenario 3 on the ray: wrong answer (reasons + NEXT), and the narration of the emergency protocol (right answer)
    "VO_Sc03WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[4]),
    "VO_Sc03WrongNext_en": (EN_VOICE, "Wrong answer. Read the reason for each option. Click Next to go to Scenario four."),
    # Scenario 4-9 on the controller ray: the answer is shown with its reasons, no retry (same wording as Scenario 1-3)
    "VO_Sc04WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[5]),
    "VO_Sc05WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[6]),
    "VO_Sc06WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[7]),
    "VO_Sc07WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[8]),
    "VO_Sc08WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[9]),
    "VO_Sc09WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[10]),
    "VO_Sc03Protocol_bn": (BN_VOICE, "নষ্ট হলে জরুরি পদক্ষেপ: ড্রাম খুলবেন না। এলাকা খালি করুন। সুপারভাইজারকে জানান। বিশেষজ্ঞ নিষ্পত্তি দলকে ডাকুন।"),
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
        "আপনি ডাইং প্ল্যান্টের একজন টেকনিশিয়ান। ঘন হাইড্রোক্লোরিক অ্যাসিড নিয়ে কাজ করতে যাচ্ছেন। "
        "আগে সেফটি ডাটা শিট দেখুন। প্রতিটি গ্লাভস ও তার বৈশিষ্ট্য দেখুন, তারপর সঠিক গ্লাভস বেছে নিন।",
    ),
    "VO_Sc04Glove0_bn": (BN_VOICE, "এটা সাদা সুতির গ্লাভস। সুতিতে কোনো রাসায়নিক সুরক্ষা নেই। অ্যাসিড সাথে সাথে ভিজিয়ে ভেতরে চলে যায়।"),
    "VO_Sc04Glove1_bn": (BN_VOICE, "এটা পাতলা নীল নাইট্রাইল গ্লাভস। ঘন অ্যাসিডের বিরুদ্ধে এর রেটিং দুর্বল। ত্রিশ মিনিটের কমেই অ্যাসিড ভেদ করে যায়।"),
    "VO_Sc04Glove2_bn": (BN_VOICE, "এটা মোটা কালো নিওপ্রিন গ্লাভস। ঘন অ্যাসিডের বিরুদ্ধে এর রেটিং চমৎকার। চারশো আশি মিনিটের বেশি সময় টিকে থাকে।"),
    "VO_Sc04Gate_bn": (
        BN_VOICE,
        "গ্লাভস পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc04Complete_bn": (BN_VOICE, complete_bn(4)),
    "VO_Sc04Complete_en": (EN_VOICE, complete_en(4)),
    # Scenario 5
    "VO_SC05_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন স্টোরকিপার। ফ্যাক্টরী স্টোরে টলুইন সলভেন্টের বাষ্প উচ্চ মাত্রায় আছে। ভেতরে প্রবেশের আগে সঠিক মাস্ক পরে নিন।",
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
    "VO_Sc05Gate_bn": (
        BN_VOICE,
        "মাস্ক পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
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
    "VO_Sc06HoodOn_bn": (BN_VOICE, "ফিউম হুড চালু হয়েছে। এবার টেবিলে গিয়ে দ্রাবকের বোতলের মুখ বন্ধ করুন।"),
    "VO_Sc06Cap_bn": (BN_VOICE, "ঢাকনাটি ছুঁয়ে বোতলের মুখ বন্ধ করুন।"),
    "VO_Sc06HoodWarn_bn": (BN_VOICE, "আগে ফিউম হুড চালু করুন।"),
    "VO_Sc06Jug_bn": (BN_VOICE, "উদ্বায়ী দ্রাবকের পাত্রটি আঙুল দিয়ে ছুঁয়ে তুলুন।"),
    "VO_Sc06Sash_bn": (BN_VOICE, "এবার ফিউম হুডের কাচের দরজা ছুঁয়ে খুলুন।"),
    "VO_Sc06Pour_bn": (BN_VOICE, "কাচের জারটি ছুঁয়ে ধীরে ধীরে দ্রাবক ঢালুন।"),
    "VO_Sc06Gate_bn": (
        BN_VOICE,
        "পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, পুরো অ্যানিমেশন আবার দেখতে চাইলে প্রিভিউ বাটনে ক্লিক করুন।",
    ),
    "VO_Sc06AutoHood_bn": (BN_VOICE, "ফিউম হুড চালু হয়েছে। সবুজ বাতি জ্বলছে, মানে হুড কাজ করছে।"),
    "VO_Sc06AutoFan_bn": (BN_VOICE, "এবার এক্সজস্ট ফ্যান চালু হলো। ফ্যান ঘুরে ঘরের বাষ্প বাইরে টেনে নিচ্ছে।"),
    "VO_Sc06AutoMove_bn": (BN_VOICE, "পানির জগ, অ্যাসিডের ক্যান আর খালি পাত্রটি ফিউম হুডের ভেতরে নেওয়া হচ্ছে।"),
    "VO_Sc06AutoPour_bn": (BN_VOICE, "ফিউম হুডের ভেতরে খালি পাত্রে ধীরে ধীরে সালফিউরিক অ্যাসিড ঢালা হচ্ছে। বাষ্প সরাসরি হুড টেনে নিচ্ছে।"),
    "VO_Sc06Complete_bn": (BN_VOICE, complete_bn(6)),
    "VO_Sc06Complete_en": (EN_VOICE, complete_en(6)),
    # Scenario 7 (factory fire, choose the extinguisher). শঠিক in the instruction like VO_SC06_Instruction_bn.
    "VO_SC07_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন কারখানা শ্রমিক। কাজ চলাকালীন আপনার ফ্লোরে বৈদ্যুতিক যন্ত্রপাতি ও দাহ্য সলভেন্টে আগুন লেগেছে! "
        "আগুন এখনো ছড়িয়ে পড়েনি এবং নিয়ন্ত্রণের মধ্যে আছে। শঠিক ফায়ার এক্সটিংগুইশারটি বেছে নিন।",
    ),
    "VO_Sc07Ext0_bn": (
        BN_VOICE,
        "এটি ওয়াটার এক্সটিংগুইশার। এটি কাঠ, কাগজ ও কাপড়ের মতো সাধারণ দাহ্য পদার্থের আগুনে কার্যকর। "
        "বৈদ্যুতিক আগুন বা দাহ্য তরলের আগুনে এটি কখনো ব্যবহার করবেন না।",
    ),
    "VO_Sc07Ext1_bn": (BN_VOICE, "এটি লাল রঙের কার্বন ডাই-অক্সাইড বা এবিসি ড্রাই কেমিক্যাল এক্সটিংগুইশার। এটি দাহ্য তরল এবং বৈদ্যুতিক আগুনে কার্যকর।"),
    "VO_Sc07Ext2_bn": (
        BN_VOICE,
        "এটি ক্রিম রঙের ফোম এক্সটিংগুইশার। এটি দাহ্য তরলের আগুনে কার্যকর, কিন্তু ভেজা অবস্থায় বিদ্যুৎ পরিবহন করে, তাই বৈদ্যুতিক প্যানেলে ব্যবহার করা যাবে না।",
    ),
    "VO_Sc07Gate_bn": (
        BN_VOICE,
        "পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc07Hero_bn": (BN_VOICE, "আগুন নিভে গেছে। কার্বন ডাই অক্সাইড এক্সটিংগুইশার ব্যবহার করায় বড় দুর্ঘটনা এড়ানো গেছে।"),
    "VO_Sc07Complete_bn": (BN_VOICE, complete_bn(7)),
    "VO_Sc07Complete_en": (EN_VOICE, complete_en(7)),
    # Scenario 8 (acid dilution). শঠিক in the instruction like SC06/SC07. The ending uses complete_bn(8), same sentence as the others.
    "VO_SC08_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন ল্যাব টেকনিশিয়ান। একটি ডাই ব্যাচের জন্য পাতলা সালফিউরিক অ্যাসিড দরকার। "
        "শঠিক পদ্ধতিতে অ্যাসিডটি সাবধানে পানির সাথে মিশিয়ে পাতলা করুন।",
    ),
    "VO_Sc08Ppe_bn": (BN_VOICE, "প্রথমে প্রয়োজনীয় সুরক্ষা সরঞ্জামগুলো একটি একটি করে ছুঁয়ে নিন।"),
    "VO_Sc08PpeDone_bn": (BN_VOICE, "সব সুরক্ষা সরঞ্জাম নেওয়া হয়েছে। এবার ফিউম হুড চালু করুন।"),
    "VO_Sc08Hood_bn": (BN_VOICE, "কন্ট্রোল প্যানেলে ফিউম হুডের সুইচটি চালু করুন।"),
    "VO_Sc08HoodOn_bn": (BN_VOICE, "ফিউম হুড চালু হয়েছে। এবার টেবিলের ট্রে-টি ছুঁয়ে তুলে নিন।"),
    "VO_Sc08Sash_bn": (BN_VOICE, "ট্রে নিয়ে হুডের কাছে যান এবং কাচের দরজা ছুঁয়ে খুলুন।"),
    "VO_Sc08HoodWarn_bn": (BN_VOICE, "আগে ফিউম হুড চালু করুন।"),
    "VO_Sc08Mix_bn": (BN_VOICE, "পানির বিকারটি ছুঁয়ে শুরু করুন। অ্যাসিড ধীরে ধীরে পানিতে ঢালুন এবং কাচের দণ্ড দিয়ে নাড়তে থাকুন।"),
    "VO_Sc08Gate_bn": (
        BN_VOICE,
        "অ্যাসিড পাতলা করার অনুশীলন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc08Complete_bn": (BN_VOICE, complete_bn(8)),
    "VO_Sc08Complete_en": (EN_VOICE, complete_en(8)),
    # Scenario 8 on the controller ray (automatic flow in the Scenario 6 factory room)
    "VO_Sc08AutoPpe_bn": (BN_VOICE, "প্রথমে বাম দেয়ালের স্টেশন থেকে গগলস, রেসপিরেটর, গ্লাভস আর অ্যাপ্রন, সুরক্ষা সরঞ্জামগুলো একটি একটি করে নেওয়া হচ্ছে।"),
    "VO_Sc08AutoWater_bn": (BN_VOICE, "প্রথমে খালি পাত্রে পানি ঢালা হচ্ছে।"),
    "VO_Sc08AutoAcid_bn": (BN_VOICE, "এবার পানির মধ্যে ধীরে ধীরে অ্যাসিড ঢালা হচ্ছে, আর কাচের দণ্ড দিয়ে নাড়া হচ্ছে। সবসময় পানিতে অ্যাসিড ঢালতে হয়, অ্যাসিডে পানি নয়।"),
    "VO_Sc08GatePrev_bn": (
        BN_VOICE,
        "অ্যাসিড পাতলা করার অনুশীলন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, পুরো অ্যানিমেশন আবার দেখতে চাইলে প্রিভিউ বাটনে ক্লিক করুন।",
    ),
    # Scenario 9 (chemical splash on the face: broken flask, SDS, safety shower / eye wash). Nothing follows it: custom ending.
    "VO_SC09_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন ল্যাব টেকনিশিয়ান। টেবিলের নমুনা ফ্লাস্ক থেকে অন্য ফ্লাস্কে রাসায়নিক ঢালুন। "
        "কিছু ভুল হলে সাথে সাথে নিরাপত্তা পদ্ধতি মেনে কাজ করুন।",
    ),
    "VO_Sc09Flask_bn": (BN_VOICE, "বেগুনি নমুনার ফ্লাস্কটি ছুঁয়ে তুলে নিন এবং পাশের ফ্লাস্কে ঢালুন।"),
    "VO_Sc09Spill_bn": (
        BN_VOICE,
        "ফ্লাস্কটি হাত থেকে পড়ে ভেঙে গেছে। ক্ষতিকর রাসায়নিক আপনার মুখে ও চোখে ছিটকে লেগেছে। এখনই ব্যবস্থা নিতে হবে।",
    ),
    "VO_Sc09Sds_bn": (
        BN_VOICE,
        "নিরাপত্তা তথ্যপত্র দেখুন। চোখ ঘষবেন না। দ্রুত আইওয়াশে যান। চোখের পাতা খোলা রেখে পনেরো মিনিটের বেশি ধুতে হবে।",
    ),
    "VO_Sc09Walk_bn": (BN_VOICE, "সেফটি শাওয়ারের কাছে যান।"),
    "VO_Sc09Push_bn": (BN_VOICE, "পুশ প্লেটটি ছুঁয়ে শাওয়ার চালু করুন।"),
    "VO_Sc09Flush_bn": (
        BN_VOICE,
        "চোখের পাতা খোলা রাখুন। চোখ ঘুরিয়ে ভেতরের কোণ থেকে বাইরের কোণের দিকে পানি দিন। পনেরো মিনিট ধুতে থাকুন।",
    ),
    "VO_Sc09Gate_bn": (
        BN_VOICE,
        "আইওয়াশ ও শাওয়ারের অনুশীলন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    # Scenario 9 on the controller ray (automatic flow in the factory room)
    "VO_Sc09RayInstruction_bn": (
        BN_VOICE,
        "আপনি ডাইং সেকশনে কেমিক্যাল নিয়ে কাজ করছেন। টেবিলে থাকা কেমিক্যালের ক্যান থেকে খালি পাত্রে ঢালতে যাচ্ছেন। "
        "কিছু ভুল হলে সাথে সাথে নিরাপত্তা পদ্ধতি মেনে কাজ করুন।",
    ),
    "VO_Sc09RaySpill_bn": (
        BN_VOICE,
        "অ্যাসিডের ক্যানটি হাত থেকে পড়ে বিকারটি ভেঙে গেছে। অ্যাসিড আপনার চোখে ও মুখে ছিটকে লেগেছে। এখনই ব্যবস্থা নিতে হবে।",
    ),
    "VO_Sc09RayWalk_bn": (BN_VOICE, "ডান দিক থেকে ঘুরে পেছনের ইমার্জেন্সি সেফটি শাওয়ারের কাছে যাওয়া হচ্ছে।"),
    "VO_Sc09RayPush_bn": (BN_VOICE, "শাওয়ারের পুশ প্লেট চাপা হয়েছে, পানি চালু হচ্ছে।"),
    "VO_Sc09RayBack_bn": (
        BN_VOICE,
        "এবার টেবিলে ফিরে এসে দেখুন, অ্যাসিডের ক্যান পড়ে আছে, বিকার ভেঙে গেছে, আর অ্যাসিড টেবিলে ও মেঝেতে ছড়িয়ে আছে।",
    ),
    "VO_Sc09RayGate_bn": (
        BN_VOICE,
        "ইমার্জেন্সি শাওয়ারের অনুশীলন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, পুরো অ্যানিমেশন আবার দেখতে চাইলে প্রিভিউ বাটনে ক্লিক করুন।",
    ),
    "VO_Sc09Complete_bn": (BN_VOICE, complete_bn(9)),
    "VO_Sc09Complete_en": (EN_VOICE, complete_en(9)),
    # Scenario 10 (acid spill response). শঠিক in the instruction like SC06-SC08. The ending uses complete_bn(10).
    "VO_SC10_Instruction_bn": (
        BN_VOICE,
        "আপনি একজন ল্যাব টেকনিশিয়ান। মেঝেতে একটি অ্যাসিডের কাচের বোতল পিছলে পড়ে ভেঙে গেছে। "
        "ঘটনাস্থলে আপনিই প্রথম ব্যক্তি, ল্যাব সুপারভাইজার ঘরে নেই। আপনাকে নিরাপদে ও শঠিকভাবে পদক্ষেপ নিতে হবে।",
    ),
    "VO_Sc10Sds_bn": (BN_VOICE, "নিরাপত্তা তথ্যপত্র পড়ুন। এখানে স্পিল সামলানোর ধাপগুলো দেওয়া আছে।"),
    "VO_Sc10Ppe_bn": (BN_VOICE, "প্রথমে প্রয়োজনীয় সুরক্ষা সরঞ্জামগুলো একটি একটি করে নিয়ে নিন।"),
    "VO_Sc10Vent_bn": (BN_VOICE, "বাতাস চলাচলের জন্য ফিউম হুডের সুইচটি ছুঁয়ে ফিউম হুড চালু করুন।"),
    "VO_Sc10Kit_bn": (BN_VOICE, "স্পিল কিটের কাছে যান এবং স্পিল কিট বক্সটা খুলুন।"),
    "VO_Sc10Collect_bn": (BN_VOICE, "স্পিল কিট থেকে প্রয়োজনীয় সবগুলো জিনিস নিয়ে নিন।"),
    "VO_Sc10Contain_bn": (BN_VOICE, "ছড়িয়ে পড়া অ্যাসিড আটকাতে, আইকনটি ছুঁয়ে অ্যাবজর্বেন্ট সক স্পিলের চারপাশে গোল করে বিছিয়ে দিন।"),
    "VO_Sc10Neutralize_bn": (BN_VOICE, "অ্যাসিডকে নিষ্ক্রিয় করতে, আইকনটি ছুঁয়ে সোডা অ্যাশ অ্যাসিডের উপর ছিটিয়ে দিন।"),
    "VO_Sc10Absorb_bn": (BN_VOICE, "তরল শুষে নিতে, আইকনটি ছুঁয়ে অ্যাবজর্বেন্ট প্যাডগুলো স্পিলের উপর বিছিয়ে দিন।"),
    "VO_Sc10Clean_bn": (BN_VOICE, "ভাঙা কাচ সরাতে, আইকনটি ছুঁয়ে ব্রাশ দিয়ে কাচের টুকরোগুলো ডাস্টপ্যানে ঝেড়ে তুলুন।"),
    "VO_Sc10Dispose_bn": (BN_VOICE, "বর্জ্য ফেলতে, আইকনটি ছুঁয়ে অ্যাবজর্বেন্ট সক, প্যাড ও কাচের টুকরো হ্যাজার্ডাস ওয়েস্ট ব্যাগে ভরে বিনে ফেলুন।"),
    "VO_Sc10Gate_bn": (
        BN_VOICE,
        "স্পিল সামলানোর অনুশীলন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    # Scenario 10 on the controller ray (automatic flow in the factory room): narration only, nothing to touch
    "VO_Sc10WrongNext_bn": (BN_VOICE, "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % TO_NUM[11]),
    "VO_Sc10RayInstruction_bn": (
        BN_VOICE,
        "আপনি একজন কারখানা শ্রমিক। কারখানার কোয়ালিটি কন্ট্রোল রুমে কাজ করার সময় অ্যাসিডের কাঁচের বোতল ভেঙ্গে গেছে। "
        "ঘটনাস্থলে আপনিই প্রথম, আর ল্যাব সুপারভাইজার ঘরে নেই। আপনাকে নিরাপদ এবং সঠিক পদক্ষেপ নিতে হবে।",
    ),
    "VO_Sc10RayLook_bn": (BN_VOICE, "ক্যান থেকে অ্যাসিড মেঝেতে পড়ছে, কাচের জার ভেঙে গেছে, আর অ্যাসিডের ধোঁয়া উড়ছে।"),
    "VO_Sc10RayPpe_bn": (BN_VOICE, "প্রথমে বাম দিকের পিপিই স্টেশন থেকে সুরক্ষা সরঞ্জামগুলো একটি একটি করে নেওয়া হচ্ছে।"),
    "VO_Sc10RayKit_bn": (BN_VOICE, "এবার স্পিল কিট খুলে প্রয়োজনীয় জিনিসগুলো একটি একটি করে নেওয়া হচ্ছে।"),
    "VO_Sc10RayContain_bn": (BN_VOICE, "অ্যাবজর্বেন্ট সক স্পিলের চারপাশে বিছিয়ে অ্যাসিড আটকানো হচ্ছে।"),
    "VO_Sc10RayNeutralize_bn": (BN_VOICE, "সোডা অ্যাশ ছিটিয়ে অ্যাসিডকে নিষ্ক্রিয় করা হচ্ছে।"),
    "VO_Sc10RayAbsorb_bn": (BN_VOICE, "অ্যাবজর্বেন্ট প্যাড বিছিয়ে তরল শুষে নেওয়া হচ্ছে।"),
    "VO_Sc10RayClean_bn": (BN_VOICE, "ব্রাশ ও ডাস্টপ্যান দিয়ে ভাঙা কাচ তোলা হচ্ছে।"),
    "VO_Sc10RayDispose_bn": (BN_VOICE, "সব বর্জ্য হ্যাজার্ডাস ওয়েস্ট ব্যাগে ভরে বিনে ফেলা হচ্ছে।"),
    "VO_Sc10RayGate_bn": (
        BN_VOICE,
        "স্পিল সামলানোর পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন, পুরো প্রসেসটি আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc10Complete_bn": (BN_VOICE, complete_bn(10)),
    "VO_Sc10Complete_en": (EN_VOICE, complete_en(10)),
    "VO_SC11_Instruction_bn": (BN_VOICE, "আপনি একজন রক্ষণাবেক্ষণ টেকনিশিয়ান এবং গ্যাস সিলিন্ডার ইয়ার্ডে নিয়মিত ঘুরে দেখছেন। হঠাৎ হালকা হিস হিস শব্দ শুনতে পেলেন। কাছে গিয়ে দেখলেন, একটি সিলিন্ডারের ভালভের কাছে বরফ জমেছে, যা গ্যাস লিকের স্পষ্ট লক্ষণ। সঠিক পদক্ষেপ নিন।"),
    "VO_Sc11Sds_bn": (BN_VOICE, "নিরাপত্তা তথ্যপত্র পড়ুন। এখানে গ্যাস লিক সামলানো ও সিলিন্ডার সংরক্ষণের নিয়ম দেওয়া আছে।"),
    "VO_Sc11Alarm_bn": (BN_VOICE, "অ্যালার্ম বাজান। সাইরেন আইকনটিতে ক্লিক করুন।"),
    "VO_Sc11Evacuate_bn": (BN_VOICE, "অ্যালার্ম বাজছে। সবাই এলাকা খালি করছে। আপনিও এক্সিট গেট দিয়ে বেরিয়ে যান।"),
    "VO_Sc11Call_bn": (BN_VOICE, "নিরাপদ জায়গা থেকে সুপারভাইজারকে কল করুন। কল আইকনটিতে ক্লিক করুন।"),
    "VO_Sc11RadioPlayer_bn": (BN_VOICE, "সুপারভাইজার, আমি বাইরে নিরাপদ জায়গায় আছি, রিপোর্ট করছি। সেকশন বি খালি করা হয়েছে।"),
    "VO_Sc11RadioReply_bn": (BN_VOICE, "বুঝতে পেরেছি। এলাকা থেকে দূরে থাকুন। রেসপন্স টিম রওনা দিয়েছে।"),
    "VO_Sc11Gate_bn": (
        BN_VOICE,
        "গ্যাস লিক সামলানোর অনুশীলন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন। আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc11Complete_bn": (BN_VOICE, complete_bn(11)),
    "VO_Sc11Complete_en": (EN_VOICE, complete_en(11)),
    "VO_SC12_Instruction_bn": (BN_VOICE, "আপনি একজন প্রসেস অপারেটর এবং ক্ষয়কারী রাসায়নিক সামলাতে প্রসেস এলাকায় ঢুকতে যাচ্ছেন। নিরাপত্তা তথ্যপত্র পড়ুন, তারপর পুরো পিপিই সেট পরুন।"),
    "VO_Sc12Sds_bn": (BN_VOICE, "নিরাপত্তা তথ্যপত্র পড়ুন। এখানে প্রতিটি ঝুঁকির জন্য কোন পিপিই লাগবে তা দেওয়া আছে।"),
    "VO_Sc12Stand_bn": (BN_VOICE, "লকারের সামনে সবুজ তীরের উপর দাঁড়ান।"),
    "VO_Sc12Pick0_bn": (BN_VOICE, "ক্ষয়কারী অ্যাসিডের জন্য চোখ ও মুখের সুরক্ষা বেছে নিন।"),
    "VO_Sc12Pick1_bn": (BN_VOICE, "ক্ষয়কারী অ্যাসিডের জন্য হাতের সুরক্ষা বেছে নিন।"),
    "VO_Sc12Pick2_bn": (BN_VOICE, "অ্যাসিডের বাষ্পের জন্য রেসপিরেটর বেছে নিন।"),
    "VO_Sc12Pick3_bn": (BN_VOICE, "ক্ষয়কারী অ্যাসিডের জন্য শরীরের সুরক্ষা বেছে নিন।"),
    "VO_Sc12Right_bn": (BN_VOICE, "সঠিক পছন্দ। ক্ষয়কারী অ্যাসিডের জন্য উপযুক্ত।"),
    "VO_Sc12Wrong_bn": (BN_VOICE, "ভুল পছন্দ। ক্ষয়কারী অ্যাসিডের জন্য অনুপযুক্ত।"),
    "VO_Sc12Review_bn": (BN_VOICE, "আপনার পিপিই সেট সম্পূর্ণ। তালিকাটি দেখে নিন, তারপর নেক্সট-এ ক্লিক করুন।"),
    "VO_Sc12Gate_bn": (
        BN_VOICE,
        "পিপিই পরিদর্শন সম্পন্ন হয়েছে। প্রশ্নের জন্য প্রস্তুত থাকলে নেক্সট বাটনে ক্লিক করুন। আবার দেখতে চাইলে ব্যাক বাটনে ক্লিক করুন।",
    ),
    "VO_Sc12Complete_bn": (BN_VOICE, complete_bn(12)),
    "VO_Sc12Complete_en": (EN_VOICE, complete_en(12)),
}

# English twins of every Bangla clip (the "Complete" lines already have their own _en clips above and are not repeated).
# Key = the Bangla clip name without "_bn" = the file name in Assets/Resources/VoEn. Text = the English on-screen text where
# the code has one (SubtitledVoice string, ScNNContent constants); otherwise a translation of the Bangla line. See Docs/VO_EN_MANIFEST.md.
OUT_EN = os.path.join(ROOT, "Assets", "Resources", "VoEn")


def gate_en(what):
    return what + " If you are ready for the questions, click the Next button. To review again, click the Back button."


CLIPS_EN = {
    "VO_AssalamuAlaikum": "Assalamu Alaikum. Welcome to the chemical safety training.",
    "VO_SC01_Instruction": "Select the correctly labeled drum based on the provided Safety Data Sheet (SDS).",
    "VO_Carrier": "Load the correct drum onto the trolley for transfer.",
    "VO_ContinueHint": "Click the speaker to hear the instructions again, or Continue to go on.",
    "VO_PickLabel": "Select the correct label for each drum.",
    "VO_TouchDrum": "Touch the glowing button in front of a drum with your finger.",
    "VO_WrongLabel": "Wrong label for this drum. Select the correct label again.",
    "VO_Wrong": "Wrong selection. Try again.",
    "VO_Correct": "Correct. Excellent! You have selected the correct answer.",
    "VO_AnswerWrongSee": "Wrong answer. Read the reason for each option.",
    "VO_Sc03Protocol": "Emergency action if degraded: do not open the drum. Evacuate the area. Notify the supervisor. Contact specialist disposal.",
    "VO_SC02_Instruction": "A delivery of six chemicals has arrived. The supervisor asks you to place each container into the correct compartment.",
    "VO_Sc02PlaceHint": "Place the chemical in the correct compartment for its type.",
    "VO_Sc02Decision": "Take decision. Inspect the containers to read their hazards, or click Continue when you are ready to store them.",
    "VO_Sc02Inspect": "Select a container to read its properties. Close it, then inspect the next one.",
    "VO_Sc02Pick": "Select the container you want to store.",
    "VO_Sc02Dest": "Where do you store it?",
    "VO_SC03_Instruction": "You find an old drum of organic peroxide that has been sitting on the back shelf for years. Expiry date is over 2 years past. Crystallization visible around the cap. You must decide what to do.",
    "VO_Sc03Touch": "Touch and hold the drum label with your finger to scan it.",
    "VO_Sc03Inspect": "The drum is organic peroxide. Crystals have formed on the drum and the label has expired.",
    "VO_Sc04WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario five.",
    "VO_Sc05WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario six.",
    "VO_Sc06WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario seven.",
    "VO_Sc07WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario eight.",
    "VO_Sc08WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario nine.",
    "VO_Sc09WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario ten.",
    "VO_Sc03Wrong": "Wrong decision. Crystallized organic peroxide can explode when it is opened or rubbed.",
    "VO_SC04_Instruction": "You are a technician in the dyeing plant. You are about to handle concentrated Hydrochloric Acid. Check the SDS first. Watch each glove and its properties, then choose the right gloves.",
    "VO_Sc04Glove0": "This is a white cotton glove. Cotton gives no chemical protection. Acid soaks through at once.",
    "VO_Sc04Glove1": "This is a thin blue nitrile glove. Against concentrated acid its rating is poor. Acid goes through in under thirty minutes.",
    "VO_Sc04Glove2": "This is a thick black neoprene glove. Against concentrated acid its rating is excellent. It holds for more than four hundred and eighty minutes.",
    "VO_Sc04Gate": gate_en("Glove inspection complete."),
    "VO_Sc05Gate": gate_en("Mask inspection complete."),
    "VO_SC05_Instruction": "You are a storekeeper. In the factory store there are high levels of Toluene solvent vapors. Put on the right mask before you go in.",
    "VO_Sc05Mask0": "This is a simple dust mask, N95. It is incompatible with toluene solvent.",
    "VO_Sc05Mask1": "This is a half-face respirator with organic vapor cartridge. It is compatible with toluene solvent.",
    "VO_Sc05Mask2": "This is a surgical mask. It is incompatible with toluene solvent.",
    "VO_Sc05Mask3": "This is a full-face respirator with particulate-only filter. It is incompatible with toluene solvent.",
    "VO_SC06_Instruction": "You are a Mixing Operator. You need to pour a volatile solvent, but because the container is open, vapors are spreading into the air. Take the right Engineering Control before pouring.",
    "VO_Sc06Hood": "Switch on the Fume Hood at the control panel.",
    "VO_Sc06HoodOn": "The fume hood is running. Go to the table and close the solvent container.",
    "VO_Sc06Cap": "Touch the cap to close the container.",
    "VO_Sc06HoodWarn": "Switch on the fume hood first.",
    "VO_Sc06Jug": "Touch the Volatile Solvent container to pick it up.",
    "VO_Sc06Sash": "Touch the glass door of the fume hood to open it.",
    "VO_Sc06Pour": "Touch the glass jar to pour the solvent slowly.",
    "VO_Sc06Gate": "Inspection completed. If you are ready for the questions, click the Next button. To watch the whole animation again, click the Preview button.",
    "VO_Sc06AutoHood": "The fume hood is on. The green light is blinking, so the hood is running.",
    "VO_Sc06AutoFan": "Now the exhaust fan is switched on. The fan spins and pulls the vapour out of the room.",
    "VO_Sc06AutoMove": "The water jug, the acid can and the empty container are moved inside the hood.",
    "VO_Sc06AutoPour": "Inside the hood, the sulfuric acid is poured slowly into the empty container. The hood pulls the vapour away.",
    "VO_SC07_Instruction": "You are a factory worker. During work, a fire has broken out on your floor involving electrical equipment and flammable solvents! The fire has not yet spread and is still under control. Choose the correct fire extinguisher.",
    "VO_Sc07Ext0": "This is the silver water extinguisher. It is effective on fires of ordinary combustibles such as wood, paper and cloth. Never use it on electrical fires or flammable liquid fires.",
    "VO_Sc07Ext1": "This is the red carbon dioxide or ABC dry chemical extinguisher. It is effective on flammable liquid and electrical fires.",
    "VO_Sc07Ext2": "This is the cream foam extinguisher. It is effective on flammable liquid fires, but it is conductive when wet, so it is not for electrical panels.",
    "VO_Sc07Gate": gate_en("Extinguisher inspection complete."),
    "VO_Sc07Hero": "The fire is out. Using the carbon dioxide extinguisher prevented a major accident.",
    "VO_SC08_Instruction": "You are a lab technician. A batch of dye requires dilute sulfuric acid. Dilute the acid carefully with water.",
    "VO_Sc08Ppe": "Touch each piece of protective equipment to put it on.",
    "VO_Sc08PpeDone": "All protective equipment is on. Now switch on the fume hood.",
    "VO_Sc08Hood": "Switch on the Fume Hood at the control panel.",
    "VO_Sc08HoodOn": "The fume hood is running. Now touch the tray on the table to pick it up.",
    "VO_Sc08Sash": "Take the tray to the hood and touch the glass door to open it.",
    "VO_Sc08HoodWarn": "Switch on the fume hood first.",
    "VO_Sc08Mix": "Touch the water beaker: add the acid slowly and stir.",
    "VO_Sc08Gate": gate_en("Dilution practice complete."),
    "VO_Sc08AutoPpe": "First, the protective equipment is taken from the station on the left wall, one by one: goggles, respirator, gloves and apron.",
    "VO_Sc08AutoWater": "First, the water is poured into the empty container.",
    "VO_Sc08AutoAcid": "Now the acid is added slowly to the water, and the glass rod keeps stirring. Always add acid to water, never water to acid.",
    "VO_Sc08GatePrev": "Dilution practice complete. If you are ready for the questions, click the Next button. To watch the whole animation again, click the Preview button.",
    "VO_SC09_Instruction": "You are a lab technician. Pour the chemical from the sample flask into the next flask. If something goes wrong, follow the safety procedure at once.",
    "VO_Sc09Flask": "Touch the purple sample flask to lift it and pour.",
    "VO_Sc09Spill": "The flask slipped from your hand and broke. A harmful chemical has splashed onto your face and eyes. You must respond immediately.",
    "VO_Sc09Sds": "Read the Safety Data Sheet. Do not rub your eyes. Go to the eyewash now. Keep your eyelids open and flush for more than fifteen minutes.",
    "VO_Sc09Walk": "Walk to the safety shower and eyewash.",
    "VO_Sc09Push": "Touch the push plate to start the water.",
    "VO_Sc09Flush": "Hold your eyelids open. Roll your eyes and run water from the inner corner to the outer corner. Keep flushing for fifteen minutes.",
    "VO_Sc09Gate": gate_en("Eyewash and shower practice complete."),
    "VO_Sc09RayInstruction": "You are working with chemicals in the dyeing section. You are about to pour the chemical from the can on the table into an empty container. If something goes wrong, follow the safety procedure at once.",
    "VO_Sc09RaySpill": "The acid can slipped from your hand and the beaker broke. Acid has splashed onto your eyes and face. You must respond immediately.",
    "VO_Sc09RayWalk": "Turning right, to the emergency safety shower behind you.",
    "VO_Sc09RayPush": "The push plate of the shower has been pressed and the water is starting.",
    "VO_Sc09RayBack": "Now back at the table, see: the acid can lies there, the beaker is broken, and the acid has spread over the table and the floor.",
    "VO_Sc09RayGate": "Emergency shower practice complete. If you are ready for the questions, click the Next button. To watch the whole animation again, click the Preview button.",
    "VO_SC10_Instruction": "You are a lab technician. A glass bottle of acid has slipped and shattered on the floor. You are the first person on the scene. The lab supervisor is not in the room. You must respond correctly and safely.",
    "VO_Sc10Sds": "Read the Safety Data Sheet. It lists the spill response steps.",
    "VO_Sc10Ppe": "Put on your protective equipment. Touch each item.",
    "VO_Sc10Vent": "Ventilate the area. Touch the Fume Hood switch.",
    "VO_Sc10Kit": "Go to the spill kit and touch it to open it.",
    "VO_Sc10Collect": "Take every item from the spill kit.",
    "VO_Sc10Contain": "Touch CONTAIN. Lay the absorbent sock around the spill to stop the acid spreading.",
    "VO_Sc10Neutralize": "Touch NEUTRALIZE. Sprinkle soda ash over the acid to neutralize it.",
    "VO_Sc10Absorb": "Touch ABSORB. Lay absorbent pads on the spill to soak up the liquid.",
    "VO_Sc10Clean": "Touch CLEAN-UP GLASS. Sweep the broken glass into the dustpan with the brush.",
    "VO_Sc10Dispose": "Touch DISPOSE. Put the sock, pads and glass in the hazardous waste bag, then in the bin.",
    "VO_Sc10Gate": gate_en("Spill response practice complete."),
    "VO_Sc10WrongNext": "Wrong answer. Read the reason for each option. Click Next to go to Scenario eleven.",
    "VO_Sc10RayInstruction": "You are a factory worker. A glass acid bottle has broken in the quality control room. You are the first on the scene, and the lab supervisor is not present. You must take the safe and correct action.",
    "VO_Sc10RayLook": "Acid is running from the can onto the floor, the glass jar is broken, and acid vapour is rising.",
    "VO_Sc10RayPpe": "First, the protective equipment is taken one by one from the PPE station on the left.",
    "VO_Sc10RayKit": "Now the spill kit is opened and the items are taken one by one.",
    "VO_Sc10RayContain": "The absorbent sock is laid around the spill to stop the acid spreading.",
    "VO_Sc10RayNeutralize": "Soda ash is sprinkled over the acid to neutralize it.",
    "VO_Sc10RayAbsorb": "Absorbent pads are laid on the spill to soak up the liquid.",
    "VO_Sc10RayClean": "The broken glass is swept into the dustpan with the brush.",
    "VO_Sc10RayDispose": "All the waste goes into the hazardous waste bag and then into the bin.",
    "VO_Sc10RayGate": "Spill response inspection complete. If you are ready for the questions, click the Next button. To watch the whole process again, click the Back button.",
    "VO_SC11_Instruction": "You are a maintenance technician doing a routine walk-around of the gas cylinder yard. You hear a faint hissing sound. Looking closely, one cylinder shows frost near the valve. This is a clear sign of a leak. Take the correct action.",
    "VO_Sc11Sds": "Read the Safety Data Sheet. It lists the leak response and cylinder storage rules.",
    "VO_Sc11Alarm": "Raise the alarm. Click the siren icon.",
    "VO_Sc11Evacuate": "The alarm is ringing. Everyone is evacuating. Follow them out through the exit gate.",
    "VO_Sc11Call": "Call your supervisor from a safe place. Click the call icon.",
    "VO_Sc11RadioPlayer": "Supervisor, I am secure outside, calling in my report. Section B is evacuated.",
    "VO_Sc11RadioReply": "Copy that. Stay clear of the area. The response team is on its way.",
    "VO_Sc11Gate": gate_en("Gas leakage response complete."),
    "VO_SC12_Instruction": "You are a process operator about to enter the process area to handle a corrosive chemical. Read the SDS, then put on the full ensemble.",
    "VO_Sc12Sds": "Read the Safety Data Sheet. It lists the PPE for each hazard.",
    "VO_Sc12Stand": "Stand on the green arrow in front of the lockers.",
    "VO_Sc12Pick0": "Pick the eye and face protection for corrosive acid.",
    "VO_Sc12Pick1": "Pick the hand protection for corrosive acid.",
    "VO_Sc12Pick2": "Pick the respirator for acid vapour.",
    "VO_Sc12Pick3": "Pick the body protection for corrosive acid.",
    "VO_Sc12Right": "Right choice. Compatible with Corrosive Acid.",
    "VO_Sc12Wrong": "Wrong choice. Incompatible with Corrosive Acid.",
    "VO_Sc12Review": "Your PPE ensemble is complete. Check the list, then click NEXT.",
    "VO_Sc12Gate": gate_en("PPE inspection complete."),
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
    ap.add_argument("--en", action="store_true", help="generate the English twins (CLIPS_EN) into Assets/Resources/VoEn; add --only VO_Sc10Sds for one, --show to only print")
    ap.add_argument("--show", action="store_true", help="print the text the voice reads for every clip, write nothing")
    args = ap.parse_args()
    if args.en:
        os.makedirs(OUT_EN, exist_ok=True)
        wanted = set(args.only.split(",")) if args.only else None
        for name, text in CLIPS_EN.items():
            if wanted and name not in wanted:
                continue
            if args.show:
                print(name, "->", text)
                continue
            await synth(text, EN_VOICE, os.path.join(OUT_EN, name + ".mp3"), raw=True)
        return
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
