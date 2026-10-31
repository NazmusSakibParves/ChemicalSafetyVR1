"""Scenario 11 radio call: two MEN talk (the hero technician and the supervisor).

Bangla: bn-BD-PradeepNeural (the only Bangladeshi male voice); the supervisor is the same voice a little lower and slower, so the
two men are told apart. English: en-IN-PrabhatNeural (technician) and en-GB-RyanNeural (supervisor).
Writes VO_Sc11RadioPlayer1/RadioReply1/RadioPlayer/RadioReply _bn into Art/Audio/VO and the English twins into Resources/VoEn.

    python Tools/generate_sc11_radio.py
"""
import asyncio
import os
import sys

import edge_tts

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_vo as g  # noqa: E402

BN_MAN = "bn-BD-PradeepNeural"
EN_HERO = "en-IN-PrabhatNeural"
EN_SUP = "en-GB-RyanNeural"

LINES = {
    "VO_Sc11RadioPlayer1": ("সুপারভাইজার স্যার, সুপারভাইজার স্যার, আমি সিলিন্ডার ইয়ার্ড থেকে টেকনিশিয়ান বলছি। জরুরি! শুনতে পাচ্ছেন? ওভার।",
                            "Supervisor, Supervisor, this is the technician at the cylinder yard. Urgent! Do you copy? Over.", True),
    "VO_Sc11RadioReply1": ("হ্যাঁ, শুনতে পাচ্ছি। বলো, কী অবস্থা? ওভার।", "Copy, go ahead. What is the situation? Over.", False),
    "VO_Sc11RadioPlayer": ("স্যার, সিলিন্ডার ইয়ার্ডে গ্যাস লিক! ভালভের কাছে বরফ জমেছে। আমি বাইরে নিরাপদে আছি, রিপোর্ট করছি। সেকশন বি খালি করা হয়েছে। ওভার।",
                           "Gas leak at the cylinder yard, frost at the valve! Secure outside, calling in report. Section B evacuated. Over.", True),
    "VO_Sc11RadioReply": ("ঠিক আছে। তুমি দূরে থাকো, সিলিন্ডারের কাছে যাবে না। আমি এখনই রেসপন্স টিম পাঠাচ্ছি। গেটের কাছে অপেক্ষা করো। ওভার।",
                          "Copy that. Stay well clear, do not go near the cylinders. I am sending the response team now. Wait at the gate. Over.", False),
    "VO_Sc11RadioPlayer3": ("বুঝেছি স্যার। আমি গেটের বাইরেই আছি। ওভার অ্যান্ড আউট।", "Understood. I am waiting outside the gate. Over and out.", True),
}


async def main():
    os.makedirs(g.OUT_EN, exist_ok=True)
    for name, (bn, en, hero) in LINES.items():
        bn_kw = dict(rate="+16%", pitch="+4Hz") if hero else dict(rate="+2%", pitch="-16Hz")
        await edge_tts.Communicate(g.spoken(bn), BN_MAN, **bn_kw).save(os.path.join(g.OUT, name + "_bn.mp3"))
        await edge_tts.Communicate(en, EN_HERO if hero else EN_SUP, rate="+12%" if hero else "+0%").save(os.path.join(g.OUT_EN, name + ".mp3"))
        print("wrote", name)


if __name__ == "__main__":
    asyncio.run(main())
