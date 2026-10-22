"""Shrinks the Scenario 4 lab GLBs (about 48 MB of PNG textures) before Unity import.

Geometry is kept. Images over 180 KB are resized to a 1024 edge and saved as JPEG.
Output: Assets/Art/Presentable/3D/Sc04/. The Downloads originals are not modified.

    python Tools/prepare_sc04_models.py
"""
import io
import json
import os
import shutil
import struct

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.expanduser("~"), "Downloads",
                   "Scene04 workflow-20261021T185637Z-1-001", "Scene04 workflow")
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc04")
MAX_EDGE = 1024
JPEG_Q = 72
MODELS = (
    "gloves1.glb",
    "chemistry_glassware.glb",
    "fume_cupboards.glb",
    "mechanical_pipette.glb",
    "table02.glb",
    "tray.glb",
)


def read_glb(path):
    data = open(path, "rb").read()
    off = 12
    js = bin_chunk = None
    while off + 8 <= len(data):
        length, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8:off + 8 + length]
        if ctype == 0x4E4F534A:
            js = json.loads(chunk)
        elif ctype == 0x004E4942:
            bin_chunk = chunk
        off += 8 + length
    return js, bin_chunk


def shrink(blob, mime):
    if len(blob) < 180000:
        return blob, mime
    im = Image.open(io.BytesIO(blob)).convert("RGB")
    im.thumbnail((MAX_EDGE, MAX_EDGE), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=JPEG_Q, optimize=True)
    return buf.getvalue(), "image/jpeg"


def write_glb(path, js, views):
    out = bytearray()
    for i, view in enumerate(views):
        while len(out) % 4:
            out.append(0)
        js["bufferViews"][i]["byteOffset"] = len(out)
        js["bufferViews"][i]["byteLength"] = len(view)
        out.extend(view)
    js["buffers"][0]["byteLength"] = len(out)
    payload = json.dumps(js, separators=(",", ":")).encode("utf-8")
    while len(payload) % 4:
        payload += b" "
    total = 12 + 8 + len(payload) + 8 + len(out)
    with open(path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(payload), b"JSON"))
        f.write(payload)
        f.write(struct.pack("<I4s", len(out), b"BIN\x00"))
        f.write(out)


def one(name):
    src = os.path.join(SRC, "3D", name)
    js, blob = read_glb(src)
    views = []
    for bv in js["bufferViews"]:
        start = bv.get("byteOffset", 0)
        views.append(bytearray(blob[start:start + bv["byteLength"]]))
    for im in js.get("images", []):
        vi = im.get("bufferView")
        if vi is None:
            continue
        new, mime = shrink(bytes(views[vi]), im.get("mimeType", ""))
        views[vi] = bytearray(new)
        im["mimeType"] = mime
    dst = os.path.join(OUT, name)
    write_glb(dst, js, views)
    print(name, os.path.getsize(src), "->", os.path.getsize(dst))


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04"), exist_ok=True)
    label = os.path.join(SRC, "2D Resource", "bottle level.png")
    shutil.copyfile(label, os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc04", "BottleLabel.png"))
    for name in MODELS:
        one(name)


if __name__ == "__main__":
    main()
