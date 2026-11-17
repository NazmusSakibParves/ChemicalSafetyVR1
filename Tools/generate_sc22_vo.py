"""Voice-over of Scenario 22 (acetone in a drinking-water bottle, the LAST scenario), Bangla and English -> Assets/Resources/Sc22Vo/{bn,en}/<Key>.mp3.

    python Tools/generate_sc22_vo.py            # every missing clip
    python Tools/generate_sc22_vo.py --force    # regenerate all
    python Tools/generate_sc22_vo.py --show     # print the spoken text, write nothing

Same voices and pronunciation table as every other scenario (generate_vo.py). Instruction, Question, Wrong, Right and Congrats are the client's text, word for word. Congrats replaces the usual "go to next scenario" ending (no Complete / WrongNext).
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

OUT = os.path.join(os.path.dirname(HERE), "Assets", "Resources", "Sc22Vo")

LINES = [
    ('Instruction', 'You notice a coworker transferring solvent from a drum into an empty drinking-water bottle to carry it to another room. Storing or carrying chemicals in drinking-water bottles is dangerous and may lead to accidental ingestion. Follow the SDS and workplace safety procedures, and advise your coworker to take the appropriate corrective action.', 'আপনি দেখছেন, একজন সহকর্মী ড্রাম থেকে রাসায়নিক পদার্থ একটি খালি পানির বোতলে ঢালছেন। পানির বোতলে রাসায়নিক রাখা বা বহন করা বিপজ্জনক। এতে ভুল করে পান করার ঝুঁকি তৈরি হতে পারে। সঠিক পদক্ষেপ জানতে SDS এর সেকশন ৭ (Handling and Storage) পড়ুন এবং নিরাপত্তা নির্দেশনা অনুসরণ করুন।'),
    ('Question', 'A worker is transferring solvent from a drum into an empty drinking-water bottle. What should you advise the worker to do?', 'একজন শ্রমিক একটি ড্রাম থেকে সলভেন্ট (দ্রাবক রাসায়নিক) একটি খালি পানির বোতলে ঢালছেন। কর্মীকে আপনি কী পরামর্শ দিবেন?'),
    ('Sds', 'Read Section 7 of the Safety Data Sheet first: Handling and Storage.', 'প্রথমে নিরাপত্তা তথ্যপত্র (SDS) এর সেকশন ৭ পড়ুন: হ্যান্ডলিং ও স্টোরেজ।'),
    ('Gate', 'Solvent transfer reviewed. Click Next to go on, or Back to watch the process again.', 'রাসায়নিক স্থানান্তর পর্যালোচনা সম্পন্ন। এগিয়ে যেতে নেক্সট চাপুন, আবার দেখতে ব্যাক চাপুন।'),
    ('Wrong', 'Drinking-water bottles are not designed for storing or carrying chemicals. If solvent is placed in one, someone may accidentally drink it. Since the bottle looks like a drinking-water container, it can cause serious poisoning or accidents. Adding a label or placing it in a secondary tray does not eliminate this risk. Always use an approved, properly labeled container for transferring solvents.', 'পানির বোতল রাসায়নিক সংরক্ষণ বা বহনের জন্য তৈরি নয়। এতে সলভেন্ট রাখলে কেউ ভুল করে পান করতে পারে। বোতলটি দেখতে পানির বোতলের মতো হওয়ায় মারাত্মক বিষক্রিয়া বা দুর্ঘটনা ঘটতে পারে। শুধু লেবেল লাগানো বা অতিরিক্ত ট্রেতে রাখলেই এই ঝুঁকি দূর হয় না। রাসায়নিক স্থানান্তরের জন্য সবসময় অনুমোদিত ও সঠিকভাবে লেবেলযুক্ত পাত্র ব্যবহার করতে হবে।'),
    ('Right', 'Correct! Never store or carry chemicals in drinking-water bottles or food containers. Always use an approved, properly labeled container for chemical transfer. Also ensure appropriate PPE, adequate ventilation, and spill-prevention measures according to the SDS.', 'সঠিক! পানির বোতল বা খাবারের পাত্রে কখনো রাসায়নিক রাখা বা বহন করা যাবে না। রাসায়নিক স্থানান্তরের আগে কাজটি নিরাপদভাবে করার জন্য অনুমোদিত ও সঠিক লেবেলযুক্ত পাত্র ব্যবহার করতে হবে। পাশাপাশি SDS অনুযায়ী উপযুক্ত PPE, পর্যাপ্ত বায়ু চলাচল এবং রাসায়নিক ছড়িয়ে পড়া রোধের ব্যবস্থা নিশ্চিত করতে হবে।'),
    ('Caution', 'Caution, do not carry solvent in a drinking water bottle.', 'সতর্কতা, পানির বোতলে সলভেন্ট বহন করবেন না।'),
    ('Congrats', 'Congratulations! You have completed all the scenarios. Click the Finish button to see your result or scorecard.', 'অভিনন্দন আপনি সব গুলো সিনারিও সম্পন্ন করেছেন। ফলাফল বা স্কোরকার্ড দেখতে ফিনিশ বাটনে ক্লিক করুন।'),
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
