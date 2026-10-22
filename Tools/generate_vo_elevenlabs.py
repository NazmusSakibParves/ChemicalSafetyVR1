"""Makes every Bangla voice-over clip with ElevenLabs (the voice "Priya" the user sent as Downloads/Priya.mp3; the
file's metadata says it was made by Eleven Labs Inc.).

NOTHING IS WRITTEN INTO Assets/. The clips go to Temp/VoElevenLabs/ (same file names as Assets/Art/Audio/VO) so they
can be listened to and compared first. Only after the user approves are they copied over the edge-tts clips.

Setup (the key stays in your own terminal, never in a file or in chat):
    PowerShell:  $env:ELEVENLABS_API_KEY = "<your key>"
    python Tools/generate_vo_elevenlabs.py --find-voice Priya     # prints the voice id(s) in your library
    python Tools/generate_vo_elevenlabs.py --dry-run              # lists the clips and their text, no network
    python Tools/generate_vo_elevenlabs.py --voice-id <id>        # makes all clips into Temp/VoElevenLabs
    python Tools/generate_vo_elevenlabs.py --voice-id <id> --only VO_Correct_bn,VO_SC03_Instruction_bn

Text: the normal Bangla spelling is sent (no respelling like "শঠিক" / "চমতকারর": that table is only for the edge-tts
voice). If ElevenLabs mispronounces a word, add it to FIX below, in one place, so it is the same in every scenario.
Models: eleven_v3 (default, supports Bengali) or eleven_multilingual_v2 / eleven_flash_v2_5 via --model.
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Temp", "VoElevenLabs")
API = "https://api.elevenlabs.io"

# word fixes for ElevenLabs only (empty until a listening check shows one is needed)
FIX = {}

# clips that are not in generate_vo.CLIPS: file name -> key of the Bangla text in render_bangla_text.TEXTS
EXTRA_FROM_IMAGES = {
    "VO_PickLabel_bn": "BN_PickLabel",
    "VO_WrongLabel_bn": "BN_WrongLabel",
    "VO_ContinueHint_bn": "BN_ContinueHint",
    "VO_TouchDrum_bn": "BN_TouchDrum",
    "VO_Carrier_bn": "BN_Carrier",
    "VO_Wrong_bn": "BN_WrongSub",
}


def clip_list():
    sys.path.insert(0, os.path.join(ROOT, "Tools"))
    import generate_vo as gv
    import render_bangla_text as rb

    clips = {}
    for name, entry in gv.CLIPS.items():
        if entry[0] == gv.BN_VOICE:  # Bangla clips only
            clips[name] = entry[1]
    for name, key in EXTRA_FROM_IMAGES.items():
        clips.setdefault(name, rb.TEXTS[key][0])
    return clips


def fixed(text):
    for a, b in FIX.items():
        text = text.replace(a, b)
    return text


def request(method, path, key, body=None):
    req = urllib.request.Request(API + path, method=method, data=json.dumps(body).encode("utf-8") if body else None)
    req.add_header("xi-api-key", key)
    if body:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def find_voice(name, key):
    found = []
    try:
        mine = json.loads(request("GET", "/v1/voices", key))
        for v in mine.get("voices", []):
            if name.lower() in v.get("name", "").lower():
                found.append(("my voices", v["voice_id"], v["name"], v.get("labels", {})))
    except urllib.error.HTTPError as e:
        print("could not read your voices:", e.code, e.read()[:200])
    try:
        q = urllib.parse.urlencode({"search": name, "page_size": 20})
        shared = json.loads(request("GET", "/v1/shared-voices?" + q, key))
        for v in shared.get("voices", []):
            found.append(("voice library", v.get("voice_id"), v.get("name"),
                          {"language": v.get("language"), "accent": v.get("accent"), "public_owner_id": v.get("public_owner_id")}))
    except urllib.error.HTTPError as e:
        print("could not search the voice library:", e.code, e.read()[:200])
    for where, vid, vname, info in found:
        print(where, "|", vid, "|", vname, "|", info)
    if not found:
        print("no voice named", name, "- open elevenlabs.io > Voices, add Priya to My Voices, then run this again")


def synth(text, voice_id, model, lang, key, path):
    body = {"text": text, "model_id": model,
            "voice_settings": {"stability": 0.5, "similarity_boost": 0.8, "style": 0.0, "use_speaker_boost": True}}
    if lang:
        body["language_code"] = lang
    url = "/v1/text-to-speech/%s?output_format=mp3_44100_128" % voice_id
    try:
        data = request("POST", url, key, body)
    except urllib.error.HTTPError as e:
        msg = e.read()[:300]
        if lang and e.code in (400, 422):  # some models do not take language_code
            body.pop("language_code")
            data = request("POST", url, key, body)
        else:
            raise SystemExit("ElevenLabs error %s: %s" % (e.code, msg))
    with open(path, "wb") as f:
        f.write(data)
    print("wrote", path, len(data), "bytes")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--find-voice", help="search your voices and the voice library by name (needs the key)")
    ap.add_argument("--voice-id")
    ap.add_argument("--model", default="eleven_v3")
    ap.add_argument("--lang", default="bn", help="language code sent with the text (empty to omit)")
    ap.add_argument("--only", help="comma separated clip names")
    ap.add_argument("--dry-run", action="store_true", help="list clips and text, no network")
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()

    clips = clip_list()
    if args.dry_run:
        for name, text in clips.items():
            print(name, "->", fixed(text))
        print(len(clips), "Bangla clips")
        return

    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        raise SystemExit("set the key first (PowerShell):  $env:ELEVENLABS_API_KEY = \"<your key>\"")
    if args.find_voice:
        find_voice(args.find_voice, key)
        return
    if not args.voice_id:
        raise SystemExit("pass --voice-id <id> (get it with --find-voice Priya)")

    os.makedirs(args.out, exist_ok=True)
    only = set(args.only.split(",")) if args.only else None
    with open(os.path.join(args.out, "INDEX.txt"), "w", encoding="utf-8") as idx:
        for name, text in clips.items():
            if only and name not in only:
                continue
            t = fixed(text)
            idx.write("%s\t%s\n" % (name, t))
            synth(t, args.voice_id, args.model, args.lang, key, os.path.join(args.out, name + ".mp3"))
    print("done. Listen in", args.out, "- nothing in Assets was changed.")


if __name__ == "__main__":
    main()
