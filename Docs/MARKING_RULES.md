# Marking rules / মার্কিং নিয়ম (final, client meeting 2026-11-10)

## English
- 22 scenarios, **40 minutes in total**. The clock starts after the login, the pause button stops it. At 00:00 a "Time's up" toast with voice-over appears, then the result card.
- **1 mark per MCQ.** Right on the first try = 1, wrong (or wrong first, then right) = 0.
  - Scenario 1, 3-12: 1 MCQ each (Scenario 12 is multi-select: exact right set = 1).
  - Scenario 2: 6 MCQs = 6 marks.
  - Scenarios 13-18: 3 MCQs each = 3 marks.
  - Scenarios 19-22 (not built yet): same rule, 1 mark per MCQ.
- Scenarios not reached count 0 and their MCQs are shown as incorrect on the result card.
- Same rule in Bangla and English (the language only changes the text, never the marks).
- Stored in `persistentDataPath/results/<id>.json` (sample schema `Docs/Login/giz_vr_mcq_submission_sample.json`): per scenario `score` / `max_score`, per answer `is_correct`, `wrong_attempts`, `tries`; session totals `total_score`, `total_max_score`, `total_correct`, `total_incorrect`, `completed_scenarios`, `elapsed_ms`, `timed_out`.
- Expected MCQ count per scenario: `Assets/Resources/Content/MARKING.json` (add SC19-SC22 there when they are built; the result card is shown where the director's chain ends, after the last built scenario).

## বাংলা
- মোট ২২টি সিনারিও, **সব মিলিয়ে ৪০ মিনিট**। লগইনের পর ঘড়ি শুরু হয়, পজ বাটনে ঘড়ি থামে। ০০:০০ হলে "সময় শেষ" টোস্ট ও ভয়েস-ওভার আসে, তারপর রেজাল্ট কার্ড।
- **প্রতিটি MCQ-তে ১ নম্বর।** প্রথমবারেই সঠিক হলে ১, ভুল হলে (বা আগে ভুল পরে সঠিক) ০।
  - সিনারিও ১, ৩-১২: প্রতিটিতে ১টি MCQ (সিনারিও ১২ মাল্টি-সিলেক্ট: ঠিক সঠিক সেট = ১)।
  - সিনারিও ২: ৬টি MCQ = ৬ নম্বর।
  - সিনারিও ১৩-১৮: প্রতিটিতে ৩টি MCQ = ৩ নম্বর।
  - সিনারিও ১৯-২২ (এখনো বানানো হয়নি): একই নিয়ম, প্রতি MCQ ১ নম্বর।
- যে সিনারিওতে পৌঁছানো যায়নি তার নম্বর ০, রেজাল্ট কার্ডে সেই MCQ গুলো ভুল হিসেবে দেখায়।
- বাংলা ও ইংরেজি দুই ভাষাতেই একই নিয়ম (ভাষা শুধু লেখা বদলায়, নম্বর নয়)।
- ফাইল: `persistentDataPath/results/<id>.json`। প্রতি সিনারিওতে `score` / `max_score`, প্রতি উত্তরে `is_correct`, `wrong_attempts`, `tries`; সেশনের মোট: `total_score`, `total_max_score`, `total_correct`, `total_incorrect`, `completed_scenarios`, `elapsed_ms`, `timed_out`।
