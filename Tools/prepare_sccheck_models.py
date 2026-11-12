"""Copies the designer's SC14/15/16-17 glb models into Assets/Art/Presentable/3D/ScCheck and shrinks oversized
textures (max 1024 px, PNG stays PNG, JPEG stays JPEG) so the APK does not balloon. Also copies the bleaching label.
Run: python -I Tools/prepare_sccheck_models.py
"""
import io
import json
import os
import shutil
import struct

from PIL import Image

SRC = r"C:/Users/msi/Downloads/drive-download-20261112T092045Z-1-001/"
LABEL = r"C:/Users/msi/Downloads/separate_chemical_labels_transparent_png (1)/04_bleaching_powder.png"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "ScCheck")
MAXPX = 1024
JOBS = [("Chlorine Gas.glb", "ChlorineGas.glb"), ("Hydrogen Pearoxide.glb", "HydrogenPeroxide.glb"), ("Nitric Acid.glb", "NitricAcid.glb")]


def shrink(path_in, path_out):
    data = open(path_in, "rb").read()
    jl = struct.unpack("<I", data[12:16])[0]
    j = json.loads(data[20:20 + jl])
    bo = 20 + jl
    bl = struct.unpack("<I", data[bo:bo + 4])[0]
    bin0 = data[bo + 8:bo + 8 + bl]
    views = j["bufferViews"]
    new = {}
    for im in j.get("images", []):
        bv = im["bufferView"]
        raw = bin0[views[bv].get("byteOffset", 0):views[bv].get("byteOffset", 0) + views[bv]["byteLength"]]
        img = Image.open(io.BytesIO(raw))
        if max(img.size) > MAXPX:
            s = MAXPX / max(img.size)
            img = img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.LANCZOS)
            b = io.BytesIO()
            if im.get("mimeType") == "image/jpeg":
                img.convert("RGB").save(b, "JPEG", quality=88)
            else:
                img.save(b, "PNG", optimize=True)
            new[bv] = b.getvalue()
    # rebuild the binary chunk with the replaced views (all views re-packed, 4-byte aligned)
    out = bytearray()
    for i, v in enumerate(views):
        o = v.get("byteOffset", 0)
        raw = new.get(i, bin0[o:o + v["byteLength"]])
        while len(out) % 4:
            out.append(0)
        v["byteOffset"] = len(out)
        v["byteLength"] = len(raw)
        out += raw
    while len(out) % 4:
        out.append(0)
    j["buffers"][0]["byteLength"] = len(out)
    js = json.dumps(j, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    total = 12 + 8 + len(js) + 8 + len(out)
    with open(path_out, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(js), b"JSON") + js)
        f.write(struct.pack("<I4s", len(out), b"BIN\0") + bytes(out))
    print(os.path.basename(path_out), os.path.getsize(path_in) // 1024, "KB ->", os.path.getsize(path_out) // 1024, "KB")


os.makedirs(OUT, exist_ok=True)
for a, b in JOBS:
    shrink(SRC + a, os.path.join(OUT, b))
dst = os.path.join(ROOT, "Assets", "Resources", "ScCheck", "BleachingLabel.png")
shutil.copyfile(LABEL, dst)
print("label", Image.open(dst).size, "->", dst)
