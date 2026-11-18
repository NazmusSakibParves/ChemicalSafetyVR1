"""Voice-over of Scenario 19 (confined-space tank entry), Bangla and English -> Assets/Resources/Sc19Vo/{bn,en}/<Key>.mp3.

    python Tools/generate_sc19_vo.py            # every missing clip
    python Tools/generate_sc19_vo.py --force    # regenerate all
    python Tools/generate_sc19_vo.py --show     # print the spoken text, write nothing

Same voices and pronunciation table as every other scenario (generate_vo.py). The two answer lines are the client's text, word for word.
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

OUT = os.path.join(os.path.dirname(HERE), "Assets", "Resources", "Sc19Vo")

LINES = [
    ("Instruction",
     "A worker is about to enter this empty chemical container. What is the mandatory safety step before entry?",
     "একজন শ্রমিক একটি খালি রাসায়নিক ট্যাংকের ভেতরে প্রবেশ করতে যাচ্ছেন। প্রবেশের আগে বাধ্যতামূলকভাবে কোন নিরাপত্তামূলক ব্যবস্থা নিতে হবে?"),
    ("Sds", "Read the Safety Data Sheet first: the atmosphere limits and the confined space entry protocol.",
     "প্রথমে নিরাপত্তা তথ্যপত্র পড়ুন: বাতাসের নিরাপদ সীমা ও বদ্ধ স্থানে প্রবেশের ধাপগুলো।"),
    ("w1", "The worker inspects the tank. No air test.",
     "কর্মী ট্যাংক দেখছেন। বাতাস পরীক্ষা করা হয়নি।"),
    ("w2", "Down he goes: no harness, no blower, no attendant.",
     "হারনেস, বায়ু চলাচল বা সহকারী ছাড়াই তিনি নামছেন।"),
    ("w3", "Toxic gas. He coughs, gasps, and collapses.",
     "ভেতরে বিষাক্ত গ্যাস! তিনি কাশছেন, শ্বাস নিতে পারছেন না, অজ্ঞান হয়ে গেলেন।"),
    ("r1", "The correct way. First, observe the tank.",
     "সঠিক পদ্ধতি। প্রথমে ট্যাংক পর্যবেক্ষণ করুন।"),
    ("r2", "Test the air with a gas detector.",
     "গ্যাস ডিটেক্টরে বাতাস পরীক্ষা করুন।"),
    ("r3", "Keep the ventilation blower running.",
     "ভেন্টিলেশন ব্লোয়ার চালু রাখুন।"),
    ("r4", "Get the entry permit signed.",
     "প্রবেশের অনুমতিপত্র নিয়ে সই করুন।"),
    ("r5", "Wear the PPE and harness.",
     "পিপিই ও হারনেস পরুন।"),
    ("r6", "Set up the tripod. An attendant stays outside. Entry permitted.",
     "ট্রাইপড বসান। সহকারী বাইরে থাকবেন। এবার প্রবেশের অনুমতি।"),
    ("Gate", "Tank entry inspection complete. Click Next to go on, or Back to watch the process again.",
     "ট্যাংকে প্রবেশ পরিদর্শন সম্পন্ন। এগিয়ে যেতে নেক্সট চাপুন, আবার দেখতে ব্যাক চাপুন।"),
    ("Wrong",
     "Even an empty chemical tank may contain toxic gases or chemical residues, and oxygen levels may be low. The tank may look safe, but entering without proper precautions is dangerous. Atmospheric testing, an entry permit, appropriate PPE, rescue equipment, and an outside attendant must be ensured before entry.",
     "খালি রাসায়নিক ট্যাংকের ভেতরেও বিষাক্ত গ্যাস বা রাসায়নিকের অবশিষ্টাংশ থাকতে পারে এবং অক্সিজেনের পরিমাণ কম থাকতে পারে। শুধু ট্যাংক দেখে নিরাপদ মনে হলেও ভেতরে প্রবেশ করা বিপজ্জনক। তাই বাতাস পরীক্ষা, প্রবেশের অনুমতিপত্র, উপযুক্ত পিপিই, উদ্ধার সরঞ্জাম এবং বাইরে একজন সহকারী নিশ্চিত না করে প্রবেশ করা যাবে না।"),
    ("Right",
     "Correct! Before entering a confined space such as a chemical tank, the atmosphere must be tested, an entry permit obtained, appropriate PPE worn, and the tripod and rescue equipment prepared. A trained attendant must remain outside. Entry is allowed only after all safety requirements have been met.",
     "সঠিক! রাসায়নিক ট্যাংকের মতো বদ্ধ জায়গায় প্রবেশের আগে বাতাস পরীক্ষা করতে হবে, প্রবেশের অনুমতিপত্র নিতে হবে, উপযুক্ত পিপিই পরতে হবে, ট্রাইপড ও উদ্ধার সরঞ্জাম প্রস্তুত রাখতে হবে এবং বাইরে একজন প্রশিক্ষিত সহকারী রাখতে হবে। সব নিরাপত্তা ব্যবস্থা নিশ্চিত হওয়ার পরই প্রবেশ করা যাবে।"),
    # the ending of every scenario: the same template (generate_vo.complete_bn / complete_en), numbers 19 -> 20
    ("Complete", gv.complete_en(19), gv.complete_bn(19)),
    ("WrongNext", "Wrong answer. Read the reason for each option. Click Next to go to Scenario twenty.",
     "ভুল উত্তর। প্রতিটি অপশনের কারণ দেখে নিন। সিনারিও %s যেতে নেক্সট বাটনে ক্লিক করুন।" % gv.TO_NUM[20]),
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
                    film = key[0] in "wr" and key[1:].isdigit()
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
