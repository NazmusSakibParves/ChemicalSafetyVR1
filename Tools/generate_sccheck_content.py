"""Writes Assets/Resources/Content/SC13.json .. SC18.json, the six "Supervisor Pre-Task Safety Check" scenarios.

    python Tools/generate_sccheck_content.py            # writes the six files (overwrites)

SC14..SC18 start as copies of SC13 (hydrochloric acid) with only the round number changed. The client changes the chemical,
label, bottle, SDS sheets and the questions by editing the CHEMICALS table below (or the JSON files directly, same keys), then:
    python Tools/render_bn_auto.py        # Bangla text images
    python Tools/generate_sccheck_vo.py   # Bangla + English voice-over
Art (optional, per scenario, falls back to the SC13 pictures): Assets/Resources/ScCheck/SC14/{label,poster,sds1,sds2,sds3}.png
"""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Content")

BN_DIGITS = "০১২৩৪৫৬৭৮৯"


def bn_num(n):
    return "".join(BN_DIGITS[int(c)] for c in str(n))


# ---- the chemical of each scenario (SC14..SC18 are copies of SC13 for now) -------------------------------------------------
HCL = {
    "name": ("Hydrochloric Acid (HCL)", "হাইড্রোক্লোরিক অ্যাসিড (এইচসিএল)"),
    "questions": [
        {
            "prompt": ("Which protective equipment does the SDS recommend for a corrosive liquid like hydrochloric acid?",
                       "হাইড্রোক্লোরিক অ্যাসিডের মতো ক্ষয়কারী তরলের জন্য নিরাপত্তা তথ্যপত্র কোন সুরক্ষা সরঞ্জাম সুপারিশ করে?"),
            "options": [
                ("Safety glasses + cotton gloves", "সেফটি চশমা + সুতির গ্লাভস"),
                ("Goggles + face shield, neoprene gloves, apron", "ফেস শিল্ডসহ গগলস, নিওপ্রিন গ্লাভস, অ্যাপ্রন"),
                ("Normal glasses + cloth mask", "সাধারণ চশমা + কাপড়ের মাস্ক"),
                ("Nitrile gloves only", "শুধু নাইট্রাইল গ্লাভস"),
            ],
            "correct": 1,
            "sds": "sds1",
        },
        {
            "prompt": ("What to do if hydrochloric acid touches your skin?", "হাইড্রোক্লোরিক অ্যাসিড ত্বকে লাগলে কী করবেন?"),
            "options": [
                ("Apply burn cream and continue", "পোড়ার ক্রিম লাগিয়ে কাজ চালান"),
                ("Flush with water 15+ min, remove clothing, get help", "১৫ মিনিটের বেশি পানিতে ধুয়ে কাপড় খুলুন, সাহায্য নিন"),
                ("Wipe with dry cloth", "শুকনো কাপড়ে মুছুন"),
                ("Rinse briefly, return to work", "অল্প ধুয়ে কাজে ফিরুন"),
            ],
            "correct": 1,
            "sds": "sds2",
        },
        {
            "prompt": ("At what temperature should hydrochloric acid be stored?", "হাইড্রোক্লোরিক অ্যাসিড কোন তাপমাত্রায় সংরক্ষণ করতে হয়?"),
            "options": [
                ("2 to 8 °C, cold room", "২ থেকে ৮ °সে, ঠান্ডা ঘরে"),
                ("Below 15 °C, cool and dark", "১৫ °সে-এর নিচে, ঠান্ডা ও অন্ধকারে"),
                ("15 to 25 °C, room temperature", "১৫ থেকে ২৫ °সে, ঘরের তাপমাত্রায়"),
                ("In a freezer", "ফ্রিজারে"),
            ],
            "correct": 2,
            "sds": "sds3",
        },
    ],
}

CHEMICALS = {n: HCL for n in range(13, 19)}


def S(key, en, bn):
    return {"key": key, "en": en, "bn": bn}


def build(n):
    chem = CHEMICALS[n]
    rnd = n - 12
    last = n == 18
    strings = [
        S("Title", "CHEMICAL SAFETY ASSESSMENT TEST", "রাসায়নিক নিরাপত্তা মূল্যায়ন পরীক্ষা"),
        S("InstructionEnglish",
          "Before starting any chemical task, you must pass a 'pre-task safety check' with the supervisor. He asks three questions from the SDS. You have SDS access — this is open-book. Answers vary by chemical.",
          "যেকোনো রাসায়নিক কাজ শুরুর আগে আপনাকে সুপারভাইজারের সঙ্গে একটি ‘কাজ-পূর্ব নিরাপত্তা পরীক্ষা’ পাস করতে হবে। তিনি এসডিএস থেকে তিনটি প্রশ্ন করবেন। আপনি এসডিএস দেখতে পারবেন, এটি ওপেন-বুক। রাসায়নিক অনুযায়ী উত্তর ভিন্ন হয়।"),
        S("Instructions", "INSTRUCTIONS", "নির্দেশনা"),
        S("StartTitle", "Chemical safety assessment test", "রাসায়নিক নিরাপত্তা মূল্যায়ন পরীক্ষা"),
        S("StartSub", "Test your chemical knowledge", "আপনার রাসায়নিক জ্ঞান পরীক্ষা করুন"),
        S("BottleMcq", "MCQ Three", "এমসিকিউ তিন"),
        S("BottleHint", "Check SDS before answer", "উত্তরের আগে এসডিএস দেখুন"),
        S("StartCard",
          "Chemical safety assessment test. Press START when you are ready.",
          "রাসায়নিক নিরাপত্তা মূল্যায়ন পরীক্ষা। প্রস্তুত হলে শুরু করুন চাপুন।"),
        S("BottleCard",
          "Look at the chemical on the table. Three questions follow. Check the SDS before you answer.",
          "টেবিলের রাসায়নিকটি দেখুন। এরপর তিনটি প্রশ্ন আসবে। উত্তর দেওয়ার আগে এসডিএস দেখে নিন।"),
        S("ChemName", chem["name"][0], chem["name"][1]),
        S("BottleTitle", "Chemical Name: " + chem["name"][0], "রাসায়নিকের নাম: " + chem["name"][1]),
        S("ChemLabel", "Chemical Name:", "রাসায়নিকের নাম:"),
        S("Mcq1", "MCQ 1:", "প্রশ্ন ১:"),
        S("Mcq2", "MCQ 2:", "প্রশ্ন ২:"),
        S("Mcq3", "MCQ 3:", "প্রশ্ন ৩:"),
        S("RefSds", "Reference SDS", "রেফারেন্স এসডিএস"),
        S("CheckSds", "CHECK SDS", "এসডিএস দেখুন"),
        S("HideSds", "HIDE SDS", "এসডিএস লুকান"),
        S("Next", "NEXT", "পরবর্তী"),
        S("Start", "START", "শুরু করুন"),
        S("Right", "Correct!", "সঠিক!"),
        S("Wrong", "Not quite. The right answer is shown in green.", "ঠিক হয়নি। সঠিক উত্তরটি সবুজ রঙে দেখানো হয়েছে।"),
        S("RoundTitle", "ROUND %d RESULTS" % rnd, "রাউন্ড %s ফলাফল" % bn_num(rnd)),
        S("Score0", "0 OUT OF 3 CORRECT", "৩টির মধ্যে ০টি সঠিক"),
        S("Score1", "1 OUT OF 3 CORRECT", "৩টির মধ্যে ১টি সঠিক"),
        S("Score2", "2 OUT OF 3 CORRECT", "৩টির মধ্যে ২টি সঠিক"),
        S("Score3", "3 OUT OF 3 CORRECT", "৩টির মধ্যে ৩টি সঠিক"),
        S("McqCorrect1", "MCQ 1 - Correct", "প্রশ্ন ১ - সঠিক"),
        S("McqCorrect2", "MCQ 2 - Correct", "প্রশ্ন ২ - সঠিক"),
        S("McqCorrect3", "MCQ 3 - Correct", "প্রশ্ন ৩ - সঠিক"),
        S("McqWrong1", "MCQ 1 - Incorrect", "প্রশ্ন ১ - ভুল"),
        S("McqWrong2", "MCQ 2 - Incorrect", "প্রশ্ন ২ - ভুল"),
        S("McqWrong3", "MCQ 3 - Incorrect", "প্রশ্ন ৩ - ভুল"),
        S("ResultGood", "Great effort! Proceed to the next step: Scenario %d." % (n + 1), "চমৎকার চেষ্টা! পরবর্তী ধাপ: সিনারিও %s।" % bn_num(n + 1)) if not last else S("ResultGood", "Great effort!", "চমৎকার চেষ্টা!"),
        S("ResultLow", "Review the safety data sheet. Proceed to the next step: Scenario %d." % (n + 1), "নিরাপত্তা তথ্যপত্র ভালোভাবে পড়ুন। পরবর্তী ধাপ: সিনারিও %s।" % bn_num(n + 1)) if not last else S("ResultLow", "Review the safety data sheet carefully.", "নিরাপত্তা তথ্যপত্র ভালোভাবে পড়ুন।"),
        S("ResultGoodLast", "Great effort! You completed this check.", "চমৎকার চেষ্টা! আপনি এই পরীক্ষাটি শেষ করেছেন।"),
        S("ResultLowLast", "Review the safety data sheet carefully. You completed this check.", "নিরাপত্তা তথ্যপত্রটি ভালোভাবে পড়ে নিন। আপনি এই পরীক্ষাটি শেষ করেছেন।"),
        S("NextRound", "FINISH" if last else "NEXT ROUND", "শেষ করুন" if last else "পরবর্তী রাউন্ড"),
        S("ModuleComplete", "Pre-task safety checks complete. Well done!", "কাজ শুরুর আগের নিরাপত্তা পরীক্ষা সম্পন্ন হয়েছে। চমৎকার!"),
        S("Sds1", chem["questions"][0]["sds"], chem["questions"][0]["sds"]),
        S("Sds2", chem["questions"][1]["sds"], chem["questions"][1]["sds"]),
        S("Sds3", chem["questions"][2]["sds"], chem["questions"][2]["sds"]),
    ]
    questions = []
    for qi, q in enumerate(chem["questions"], 1):
        opts = []
        for oi, (en, bn) in enumerate(q["options"]):
            opts.append({"id": "ABCD"[oi], "text": {"en": en, "bn": bn}})
        questions.append({
            "id": "SC%d.Q%d" % (n, qi),
            "header": {"en": "CHEMICAL SAFETY ASSESSMENT TEST", "bn": "রাসায়নিক নিরাপত্তা মূল্যায়ন পরীক্ষা"},
            "prompt": {"en": q["prompt"][0], "bn": q["prompt"][1]},
            "options": opts,
            "correct": "ABCD"[q["correct"]],
            "shuffle": True,
        })
    return {"scenario": "SC%d" % n, "strings": strings, "arrays": [], "questions": questions}


def main():
    for n in range(13, 19):
        path = os.path.join(OUT, "SC%d.json" % n)
        with open(path, "w", encoding="utf8", newline="\n") as f:
            json.dump(build(n), f, ensure_ascii=False, indent=2)
        print("wrote", path)


if __name__ == "__main__":
    main()
