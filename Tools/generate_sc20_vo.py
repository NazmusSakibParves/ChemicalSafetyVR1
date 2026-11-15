"""Voice-over of Scenario 20 (chemical splash, emergency shower, report to the supervisor), Bangla and English -> Assets/Resources/Sc20Vo/{bn,en}/<Key>.mp3.

    python Tools/generate_sc20_vo.py            # every missing clip
    python Tools/generate_sc20_vo.py --force    # regenerate all
    python Tools/generate_sc20_vo.py --show     # print the spoken text, write nothing

Same voices and pronunciation table as every other scenario (generate_vo.py). Instruction, Wrong and Right are the client's text, word for word.
"""
import argparse
import asyncio
import os
import subprocess
import sys

import edge_tts

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import generate_vo as gv  # noqa: E402

OUT = os.path.join(os.path.dirname(HERE), "Assets", "Resources", "Sc20Vo")

LINES = [
    ("Instruction",
     "You work in the chemical storage area of a factory. If a corrosive chemical splashes onto your hands or clothing, immediately use the emergency shower. Remove contaminated clothing and footwear while rinsing. Flush the affected areas for the duration specified in the SDS and seek medical attention promptly.",
     "আপনি কারখানার কেমিক্যাল স্টোরেজে কাজ করেন। করসিভ কেমিক্যাল হাত বা কাপড়ে ছিটকে পড়লে সঙ্গে সঙ্গে জরুরি শাওয়ার ব্যবহার করুন। পানি দিয়ে ধোয়ার সময় দূষিত কাপড় ও জুতা খুলে ফেলুন। SDS অনুযায়ী নির্ধারিত সময় ধরে আক্রান্ত স্থান ধুয়ে দ্রুত চিকিৎসা সহায়তা নিন।"),
    ("Sds", "Read the Safety Data Sheet first: the emergency shower standard protocol.",
     "প্রথমে নিরাপত্তা তথ্যপত্র পড়ুন: জরুরি শাওয়ারের স্ট্যান্ডার্ড নিয়ম।"),
    # the film (cue times in seconds of the animator's film, see Sc20Cinematic)
    ("n1", "A worker moves a chemical drum on a trolley.",
     "একজন কর্মী ট্রলিতে করে ড্রাম নিয়ে যাচ্ছেন।"),
    ("n2", "He trips. The chemical splashes onto him.",
     "হোঁচট খেয়ে কেমিক্যাল তাঁর গায়ে ছিটকে পড়ল।"),
    ("n3", "He runs straight to the emergency shower.",
     "তিনি দৌড়ে জরুরি শাওয়ারের কাছে গেলেন।"),
    ("n4", "He pulls the handle.",
     "হ্যান্ডেল টেনে ধরলেন।"),
    ("n5", "Under the water he removes the contaminated clothing, one piece at a time.",
     "পানির নিচে একে একে দূষিত কাপড় খুলছেন।"),
    ("n6", "He rinses for a full fifteen minutes.",
     "পুরো পনেরো মিনিট শাওয়ার নিলেন।"),
    ("n7", "Then he reports the incident to the supervisor.",
     "এরপর সুপারভাইজারকে ঘটনাটি জানালেন।"),
    ("Gate", "Decontamination complete. Click Next to go on, or Back to watch the process again.",
     "ডিকনটামিনেশন সম্পন্ন। এগিয়ে যেতে নেক্সট চাপুন, আবার দেখতে ব্যাক চাপুন।"),
    ("Wrong",
     "Contaminated clothing can keep chemicals in contact with the skin and cause further injury. Delaying the use of the emergency shower can make the injury worse. Do not wait to finish your work. Use the emergency shower immediately and remove contaminated clothing while flushing with water.",
     "পোশাকে রাসায়নিক পদার্থ লাগলে তা ত্বকের সংস্পর্শে থেকে ত্বকের ক্ষতি করতে পারে। আবার শাওয়ার ব্যবহার করতে দেরি করলে ক্ষতির পরিমাণ আরও বাড়তে পারে। তাই কাজ শেষ করার জন্য অপেক্ষা না করে দ্রুত জরুরি শাওয়ার ব্যবহার করতে হবে এবং পানি দিয়ে ধোয়ার সময় রাসায়নিক পদার্থ লেগে থাকা কাপড় খুলে ফেলতে হবে।"),
    ("Right",
     "Correct! If a chemical splashes onto your body or clothing, use the emergency shower immediately. Remove contaminated clothing, shoes, and other items while flushing with water. Rinse the affected areas with plenty of water for the duration specified in the SDS and seek medical attention as needed.",
     "সঠিক! রাসায়নিক পদার্থ শরীর বা কাপড়ে ছিটকে পড়লে দেরি না করে জরুরি শাওয়ার ব্যবহার করতে হবে। পানি দিয়ে ধোয়ার সময় দূষিত কাপড়, জুতা ও অন্যান্য সামগ্রী খুলে ফেলতে হবে। SDS অনুযায়ী নির্ধারিত সময় ধরে প্রচুর পানি দিয়ে আক্রান্ত স্থান ধুতে হবে এবং প্রয়োজন অনুযায়ী চিকিৎসা সহায়তা নিতে হবে।"),
    # the ending of every scenario: the same template (generate_vo.complete_bn / complete_en), numbers 20 -> 21
    ("Complete", gv.complete_en(20), gv.complete_bn(20)),
    ("WrongNext", "Wrong answer. Read the reason for each option. Click Next to go to Scenario twenty-one.",
     "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % gv.TO_NUM[21]),
]


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--show", action="store_true")
    args = ap.parse_args()
    for key, en, bn in LINES:
        for lang, text, voice in (("bn", bn, gv.BN_VOICE), ("en", en, gv.EN_VOICE)):
            folder = os.path.join(OUT, lang)
            os.makedirs(folder, exist_ok=True)
            path = os.path.join(folder, key + ".mp3")
            say = gv.spoken(text) if lang == "bn" else text
            if args.show:
                print(key, lang, "->", say)
                continue
            if os.path.exists(path) and not args.force:
                continue
            for attempt in range(4):
                try:
                    film = key[0] == "n" and key[1:].isdigit()
                    await edge_tts.Communicate(say, voice, rate="+8%" if film else "+0%").save(path)
                    if film:  # the film lines must fit their shots: cut the silence edge-tts puts before and after
                        tmp = path + ".trim.mp3"
                        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-af",
                                        "silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.05,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.05,areverse",
                                        tmp], check=False)
                        if os.path.exists(tmp) and os.path.getsize(tmp) > 1000:
                            os.replace(tmp, path)
                    break
                except edge_tts.exceptions.NoAudioReceived:
                    if attempt < 3:
                        await asyncio.sleep(2 * (attempt + 1))
                    else:
                        print("FAILED", key, lang)
            if os.path.exists(path):
                print("wrote", path, os.path.getsize(path))


if __name__ == "__main__":
    asyncio.run(main())
