"""Prepares the Scenario 5 (dyeing factory) resources before Unity import.

Models: Mask3.glb, factory1.glb, fire extinguisher .glb (mashin01.glb is a plain rounded slab and is not used). Geometry is kept; images over 150 KB are resized
to a 1024 edge and saved as JPEG (about 80 MB of originals). Output: Assets/Art/Presentable/3D/Sc05/.
2D: the Vapor Hazard sign and the SDS clipboard (black background turned transparent) into Assets/Art/Presentable/2D/Sc05/.
The Downloads originals are not modified.

    python Tools/prepare_sc05_models.py
"""
import io
import json
import os
import struct

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.expanduser("~"), "Downloads",
                   "Scene05 workflow-20261023T120404Z-1-001", "Scene05 workflow")
OUT3D = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc05")
OUT2D = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc05")
MAX_EDGE = 1024
JPEG_Q = 74
MODELS = {
    "Mask3.glb": "Masks.glb",
    "factory1.glb": "Factory.glb",
    "fire extinguisher .glb": "Extinguisher.glb",
}


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
    if len(blob) < 150000:
        return blob, mime
    im = Image.open(io.BytesIO(blob))
    has_alpha = im.mode in ("RGBA", "LA") or "transparency" in im.info
    im.thumbnail((MAX_EDGE, MAX_EDGE), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    if has_alpha and mime == "image/png":
        alpha = np.asarray(im.convert("RGBA"))[..., 3]
        if alpha.min() < 250:
            im.convert("RGBA").save(buf, "PNG", optimize=True)
            return buf.getvalue(), "image/png"
    im.convert("RGB").save(buf, "JPEG", quality=JPEG_Q, optimize=True)
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


def one(src_name, dst_name):
    src = os.path.join(SRC, "3D", src_name)
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
    dst = os.path.join(OUT3D, dst_name)
    write_glb(dst, js, views)
    print(src_name, os.path.getsize(src) // 1024, "KB ->", dst_name, os.path.getsize(dst) // 1024, "KB")


def sds_cutout():
    """SDS.jpg is a clipboard on black. Black connected to the border becomes transparent (soft edge)."""
    im = Image.open(os.path.join(SRC, "2D resources", "SDS.jpg")).convert("RGB")
    a = np.asarray(im).astype(np.int16)
    dark = (a.max(axis=2) < 34)
    h, w = dark.shape
    seen = np.zeros_like(dark)
    stack = [(0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)]
    for x in range(0, w, 8):
        stack += [(0, x), (h - 1, x)]
    for y in range(0, h, 8):
        stack += [(y, 0), (y, w - 1)]
    while stack:
        y, x = stack.pop()
        if y < 0 or x < 0 or y >= h or x >= w or seen[y, x] or not dark[y, x]:
            continue
        seen[y, x] = True
        stack += [(y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)]
    alpha = Image.fromarray(((~seen) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    rgba = im.convert("RGBA")
    rgba.putalpha(alpha)
    rgba.save(os.path.join(OUT2D, "SDS_SC05.png"), optimize=True)
    print("SDS_SC05.png", rgba.size)


def main():
    os.makedirs(OUT3D, exist_ok=True)
    os.makedirs(OUT2D, exist_ok=True)
    for src_name, dst_name in MODELS.items():
        one(src_name, dst_name)
    sign = Image.open(os.path.join(SRC, "2D resources", "file_000000006ea482119c026796948f1c09.png")).convert("RGBA")
    sign.thumbnail((1024, 1024), Image.Resampling.LANCZOS)
    sign.save(os.path.join(OUT2D, "VaporSign.png"), optimize=True)
    print("VaporSign.png", sign.size)
    sds_cutout()


if __name__ == "__main__":
    main()
