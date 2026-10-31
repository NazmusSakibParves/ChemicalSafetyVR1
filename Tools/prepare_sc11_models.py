"""Shrinks the designer SC11 GLBs (gas cylinder rack, walkie-talkie) in place for the Quest APK.
Large images are resized and re-saved as JPEG.

    python Tools/prepare_sc11_models.py
"""
import io
import json
import os
import struct

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAX_EDGE = 1024
JPEG_Q = 72


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


def main():
    for name in ("GasCylinder", "Wireless"):
        run(os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc11", name + ".glb"))


def run(path):
    js, blob = read_glb(path)
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
        print("image", im.get("name", ""), len(new))

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
    print("wrote", path, os.path.getsize(path))


if __name__ == "__main__":
    main()
