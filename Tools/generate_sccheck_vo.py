"""Voice-over of the Supervisor Pre-Task Safety Check (scenarios 13-18), Bangla and English, from Assets/Resources/Content/SCnn.json.

    python Tools/generate_sccheck_vo.py                  # every missing clip of SC13..SC18
    python Tools/generate_sccheck_vo.py --only SC14      # one scenario (comma separated list allowed)
    python Tools/generate_sccheck_vo.py --force          # regenerate even when the file exists
    python Tools/generate_sccheck_vo.py --show           # print what is read, write nothing

Output: Assets/Resources/ScCheckVo/{bn,en}/SCnn_<Key>.mp3, loaded at run time by ScCheckRunner. The same voices and the same
pronunciation table (PRON in Tools/generate_vo.py) as the other scenarios, so the whole game sounds like one person.
Scenarios that share a text share one synthesis (the file is copied), so six identical copies cost one request per line.
"""
import argparse
import asyncio
import json
import os
import shutil
import sys

import edge_tts

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import generate_vo as gv  # noqa: E402  (BN_VOICE, EN_VOICE, spoken)

ROOT = os.path.dirname(HERE)
CONTENT = os.path.join(ROOT, "Assets", "Resources", "Content")
OUT = os.path.join(ROOT, "Assets", "Resources", "ScCheckVo")


BN_NUM_WORDS = {"১৪": "চৌদ্দ", "১৫": "পনেরো", "১৬": "ষোলো", "১৭": "সতেরো", "১৮": "আঠারো", "১৩": "তেরো", "১": "এক", "২": "দুই", "৩": "তিন"}


def spoken_digits(text):
    """Bangla digits are read as words (the voice mispronounces numerals)."""
    for k in sorted(BN_NUM_WORDS, key=len, reverse=True):
        text = text.replace(k, BN_NUM_WORDS[k])
    return text


def lines(n):
    """(key, en, bn) of every spoken clip of scenario n."""
    with open(os.path.join(CONTENT, "SC%d.json" % n), encoding="utf8") as f:
        data = json.load(f)
    s = {x["key"]: x for x in data["strings"]}
    out = []

    def add(key, en, bn):
        out.append((key, en, bn))

    add("Instruction", s["InstructionEnglish"]["en"], s["InstructionEnglish"]["bn"])
    add("StartCard", s["StartCard"]["en"], s["StartCard"]["bn"])
    add("BottleName", s["BottleTitle"]["en"], s["BottleTitle"]["bn"])
    for i, q in enumerate(data["questions"], 1):
        add("Q%d" % i, q["prompt"]["en"], q["prompt"]["bn"])
    for key in ("Right", "Wrong", "ResultGood", "ResultLow", "ResultGoodLast", "ResultLowLast"):
        add(key, s[key]["en"], s[key]["bn"])
    return out


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--show", action="store_true")
    args = ap.parse_args()
    wanted = [int(x.replace("SC", "")) for x in args.only.split(",")] if args.only else list(range(13, 19))
    done = {}  # (lang, text) -> path of the first synthesis
    for n in wanted:
        for key, en, bn in lines(n):
            for lang, text, voice in (("bn", bn, gv.BN_VOICE), ("en", en, gv.EN_VOICE)):
                folder = os.path.join(OUT, lang)
                os.makedirs(folder, exist_ok=True)
                path = os.path.join(folder, "SC%d_%s.mp3" % (n, key))
                say = gv.spoken(spoken_digits(text)) if lang == "bn" else text
                if args.show:
                    print("SC%d_%s_%s -> %s" % (n, key, lang, say))
                    continue
                if os.path.exists(path) and not args.force:
                    done.setdefault((lang, text), path)
                    continue
                if (lang, text) in done and os.path.exists(done[(lang, text)]):
                    shutil.copyfile(done[(lang, text)], path)
                    print("copied", path)
                    continue
                for attempt in range(4):
                    try:
                        await edge_tts.Communicate(say, voice).save(path)
                        break
                    except edge_tts.exceptions.NoAudioReceived:
                        if attempt < 3:
                            print("retry %d for SC%d_%s_%s" % (attempt + 1, n, key, lang))
                            await asyncio.sleep(2 * (attempt + 1))
                        else:
                            print("FAILED SC%d_%s_%s (skipped)" % (n, key, lang))
                done[(lang, text)] = path
                if os.path.exists(path):
                    print("wrote", path, os.path.getsize(path), "bytes")


if __name__ == "__main__":
    asyncio.run(main())
