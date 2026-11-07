"""Renders the Bangla images of ONE scenario (BnAuto + BnText/<SCENARIO>) without pruning anything else.

    python Tools/render_bn_only.py SC05

The unfiltered tools (render_bn_auto.py, render_content_bn.py) re-render everything and delete 'stale' files of other scenarios
(handoff 14.23): do not run them on this checkout. This wrapper only adds or updates files of the given scenario.
"""
import hashlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_bn_auto as auto  # noqa: E402
import render_content_bn as content  # noqa: E402

scenario = sys.argv[1] if len(sys.argv) > 1 else ""
if not scenario:
    print("usage: python Tools/render_bn_only.py SC05")
    sys.exit(2)
path = os.path.join(auto.CONTENT, scenario + ".json")
data = json.load(open(path, encoding="utf8"))


def only_strings():
    for s in data.get("strings", []):
        if s.get("bn"):
            yield s["key"], s["bn"]
    for a in data.get("arrays", []):
        for i, t in enumerate(a.get("bn", [])):
            if t:
                yield f"{a['key']}[{i}]", t


auto.content_strings = only_strings
auto.REQ = os.path.join(os.path.dirname(path), "_none_.jsonl")
real_remove = os.remove
os.remove = lambda p: None  # never prune
try:
    print("BnAuto:", auto.main())
finally:
    os.remove = real_remove

folder = os.path.join(content.OUT, scenario)
os.makedirs(folder, exist_ok=True)
hash_path = os.path.join(folder, "_hashes.json")
hashes = json.load(open(hash_path, encoding="utf8")) if os.path.exists(hash_path) else {}
n = 0
for key, bn in content.key_names(scenario, data):
    bold = any(k in key for k in content.BOLD_KEYS)
    h = hashlib.sha1(f"{content.RENDER_PX}|{bold}|{content.wrap_for(key)}|{bn}".encode("utf8")).hexdigest()
    p = os.path.join(folder, key + ".png")
    if hashes.get(key) == h and os.path.exists(p):
        continue
    content.render(bn, bold, content.wrap_for(key)).save(p)
    hashes[key] = h
    n += 1
json.dump(hashes, open(hash_path, "w", encoding="utf8"), ensure_ascii=False, indent=1)
print(f"BnText/{scenario} rendered {n}")
