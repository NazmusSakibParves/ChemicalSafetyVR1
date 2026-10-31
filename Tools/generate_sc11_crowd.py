"""Scenario 11 crowd shouts while the workers evacuate: short panicked lines, Bangla clips for Bangla mode and English clips for
English mode (Assets/Art/Audio/Sc11Crowd/bn_N.mp3, en_N.mp3). Several men's voices (same Bangladeshi voice at different pitch)."""
import asyncio
import os
import sys

import edge_tts

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_vo as g  # noqa: E402

OUT = os.path.join(g.ROOT, "Assets", "Art", "Audio", "Sc11Crowd")
BN = [
    ("পালাও! গ্যাস লিক হয়েছে!", "+0Hz", "+10%"),
    ("সবাই বেরিয়ে যান, তাড়াতাড়ি!", "+10Hz", "+12%"),
    ("দৌড়াও, দৌড়াও!", "-8Hz", "+18%"),
    ("গ্যাস লিক! সবাই বাইরে চলুন!", "+4Hz", "+10%"),
    ("ধাক্কাধাক্কি করবেন না, বের হন!", "-12Hz", "+8%"),
    ("এক্সিট গেটের দিকে যান!", "+14Hz", "+10%"),
]
EN = [
    ("Gas leak! Everybody out!", "en-IN-PrabhatNeural"),
    ("Move, move, go go go!", "en-GB-RyanNeural"),
    ("Get to the exit gate!", "en-US-GuyNeural"),
    ("Run! Leak at the cylinders!", "en-IN-PrabhatNeural"),
    ("Don't push, keep moving!", "en-GB-RyanNeural"),
    ("Everyone evacuate now!", "en-US-GuyNeural"),
]


async def main():
    os.makedirs(OUT, exist_ok=True)
    for i, (t, pitch, rate) in enumerate(BN):
        await edge_tts.Communicate(g.spoken(t), "bn-BD-PradeepNeural", rate=rate, pitch=pitch).save(os.path.join(OUT, "bn_%d.mp3" % i))
    for i, (t, v) in enumerate(EN):
        await edge_tts.Communicate(t, v, rate="+15%").save(os.path.join(OUT, "en_%d.mp3" % i))
    print("wrote crowd clips")


if __name__ == "__main__":
    asyncio.run(main())
