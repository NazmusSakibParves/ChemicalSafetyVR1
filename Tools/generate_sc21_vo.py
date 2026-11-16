"""Voice-over of Scenario 21 (no JHA, chemical coating in a poorly ventilated room), Bangla and English -> Assets/Resources/Sc21Vo/{bn,en}/<Key>.mp3.

    python Tools/generate_sc21_vo.py            # every missing clip
    python Tools/generate_sc21_vo.py --force    # regenerate all
    python Tools/generate_sc21_vo.py --show     # print the spoken text, write nothing

Same voices and pronunciation table as every other scenario (generate_vo.py). Instruction, Question, Wrong and Right are the client's text, word for word.
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

OUT = os.path.join(os.path.dirname(HERE), "Assets", "Resources", "Sc21Vo")

LINES = [
    ('Instruction', 'You have been assigned a chemical coating task in a poorly ventilated area, but no JHA has been prepared for the job. Read Section 2 of the SDS carefully before starting work and choose the correct action.', 'আপনাকে কম বায়ু চলাচল রয়েছে এমন একটি স্থানে রাসায়নিক কোটিংয়ের কাজ করতে বলা হয়েছে, কিন্তু এই কাজের জন্য কোনো JHA তৈরি করা হয়নি। কাজ শুরুর আগে SDS এর সেকশন ২ এর নির্দেশনা পড়ুন ও সঠিক পদক্ষেপটি নির্বাচন করুন।'),
    ('Question', 'A supervisor assigns you a task to apply a chemical coating in a poorly ventilated area, but no Job Hazard Analysis (JHA) is available. What should you do before starting work?', 'একজন সুপারভাইজার আপনাকে কম বাতাস চলাচল করে এমন একটি জায়গায় রাসায়নিক প্রলেপ দেওয়ার কাজ দিয়েছেন। কিন্তু কাজটির জন্য কোনো Job Hazard Analysis (JHA) পাওয়া যাচ্ছে না। কাজ শুরু করার আগে আপনার কী করা উচিত?'),
    ('Sds', 'Read Section 2 of the Safety Data Sheet first: the hierarchy of controls.', 'প্রথমে নিরাপত্তা তথ্যপত্রের সেকশন ২ পড়ুন: নিয়ন্ত্রণ ব্যবস্থার ক্রম।'),
    ('n1', 'A supervisor orders the worker to start a chemical coating job in a poorly ventilated room.', 'একজন সুপারভাইজার কম বাতাস চলাচল করে এমন ঘরে রাসায়নিক প্রলেপের কাজ শুরু করতে বলছেন।'),
    ('n2', 'The worker asks for the Job Hazard Analysis. The form is blank: no JHA has been prepared.', 'কর্মী JHA দেখতে চাইলেন। ফরমটি খালি: কাজের জন্য কোনো JHA তৈরি করা হয়নি।'),
    ('n3', 'He refuses to start. Work without safety controls is not allowed.', 'তিনি কাজ শুরু করতে রাজি হলেন না। নিরাপত্তা ব্যবস্থা ছাড়া কাজ করা যাবে না।'),
    ('n4', 'Together they prepare the JHA: hazards, risks and controls.', 'দুজনে মিলে JHA তৈরি করলেন: ঝুঁকি, বিপদ ও নিয়ন্ত্রণ ব্যবস্থা।'),
    ('n5', 'The completed JHA lists ventilation, a respirator, gloves and an apron.', 'সম্পূর্ণ JHA-তে বায়ু চলাচল, রেসপিরেটর, গ্লাভস ও অ্যাপ্রনের কথা আছে।'),
    ('n6', 'Safety controls are in place. Only now does the work begin.', 'নিরাপত্তা ব্যবস্থা নিশ্চিত হলো। এখন কাজ শুরু।'),
    ('Gate', 'Safe work plan complete. Click Next to go on, or Back to watch the process again.', 'নিরাপদ কাজের পরিকল্পনা সম্পন্ন। এগিয়ে যেতে নেক্সট চাপুন, আবার দেখতে ব্যাক চাপুন।'),
    ('Wrong', "Applying chemical coatings in poorly ventilated areas can allow harmful vapors to accumulate, creating risks of breathing difficulties, poisoning, or fire. Do not rely only on the supervisor's instructions, previous work experience, or searching for a JHA binder. Work must remain on hold until the hazards are assessed, the JHA is approved, and the necessary safety controls are implemented.", 'কম বাতাস চলাচল করে এমন জায়গায় রাসায়নিক প্রলেপ দেওয়ার সময় ক্ষতিকর বাষ্প জমে থাকতে পারে। এতে শ্বাসকষ্ট, বিষক্রিয়া বা আগুনের ঝুঁকি তৈরি হতে পারে। শুধু সুপারভাইজারের নির্দেশ, পুরোনো কাজের অভিজ্ঞতা বা JHA ফাইল খোঁজার ওপর নির্ভর করে কাজ শুরু করা যাবে না। কাজের ঝুঁকি মূল্যায়ন, অনুমোদন এবং প্রয়োজনীয় নিরাপত্তা ব্যবস্থা নিশ্চিত না হওয়া পর্যন্ত কাজ বন্ধ রাখতে হবে।'),
    ('Right', 'Correct! Before applying a chemical coating, a Job Hazard Analysis (JHA) must be completed and approved. Special attention must be given to the risk of harmful vapor accumulation in poorly ventilated areas. Work must not begin until adequate ventilation, appropriate PPE, and all other necessary safety controls are in place.', 'সঠিক! রাসায়নিক প্রলেপ দেওয়ার আগে কাজের ঝুঁকি মূল্যায়ন (JHA) তৈরি ও অনুমোদন করতে হবে। বিশেষ করে কম বাতাস চলাচল করে এমন জায়গায় ক্ষতিকর বাষ্প জমার ঝুঁকি বিবেচনা করতে হবে। প্রয়োজনীয় বায়ু চলাচলের ব্যবস্থা, উপযুক্ত PPE এবং অন্যান্য নিরাপত্তা ব্যবস্থা নিশ্চিত না হওয়া পর্যন্ত কাজ শুরু করা যাবে না।'),
    # the ending of every scenario: the same template, numbers 21 -> 22
    ("Complete", gv.complete_en(21), gv.complete_bn(21)),
    ("WrongNext", "Wrong answer. Read the reason for each option. Click Next to go to Scenario twenty-two.",
     "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % gv.TO_NUM[22]),
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
