"""Pulls the PBR textures out of the designer GLBs so the SC03 racks and drums can use them on procedural meshes.

Source: Downloads/Scene03 workflow-.../3D (old shelve 01.glb, Old drams.glb)
Output: Assets/Art/Presentable/2D/Sc03Tex/  (textures only; normal maps are set up by PresentableSceneBuilder)
  Rust_Albedo / Rust_Normal / Rust_MS      rusty shelf steel (metal_27); *_MS = metal in R, smoothness in A
  Barrel_Albedo / Barrel_Normal / Barrel_MS old barrel (the low-poly "Barrels" mesh of Old drams.glb)

    python Tools/extract_sc03_textures.py
"""
import io
import json
import os
import struct

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.expanduser("~"), "Downloads", "Scene03 workflow-20261020T164953Z-1-001", "Scene03 workflow", "3D")
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc03Tex")
MAX_EDGE = 1024


def read_glb(path):
    data = open(path, "rb").read()
    off = 12
    js = blob = None
    while off + 8 <= len(data):
        length, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8:off + 8 + length]
        if ctype == 0x4E4F534A:
            js = json.loads(chunk)
        elif ctype == 0x004E4942:
            blob = chunk
        off += 8 + length
    return js, blob


def image(js, blob, tex_index):
    src = js["textures"][tex_index]["source"]
    bv = js["bufferViews"][js["images"][src]["bufferView"]]
    start = bv.get("byteOffset", 0)
    return Image.open(io.BytesIO(blob[start:start + bv["byteLength"]]))


def save(im, name):
    im.thumbnail((MAX_EDGE, MAX_EDGE), Image.Resampling.LANCZOS)
    im = im.convert("RGB")
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def save_metal_smooth(im, name):
    """glTF packs roughness in G and metalness in B; URP Lit wants metalness in R and smoothness in A."""
    im.thumbnail((MAX_EDGE, MAX_EDGE), Image.Resampling.LANCZOS)
    r, g, b = im.convert("RGB").split()
    out = Image.merge("RGBA", (b, Image.new("L", im.size, 0), Image.new("L", im.size, 0), g.point(lambda v: 255 - v)))
    out.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name, im.size)


def main():
    js, blob = read_glb(os.path.join(SRC, "old shelve 01.glb"))
    m = js["materials"][0]
    save(image(js, blob, m["pbrMetallicRoughness"]["baseColorTexture"]["index"]), "Rust_Albedo")
    save(image(js, blob, m["normalTexture"]["index"]), "Rust_Normal")
    save_metal_smooth(image(js, blob, m["pbrMetallicRoughness"]["metallicRoughnessTexture"]["index"]), "Rust_MS")

    js, blob = read_glb(os.path.join(SRC, "Old drams.glb"))
    b = [x for x in js["materials"] if x.get("name") == "barrels"][0]
    save(image(js, blob, b["pbrMetallicRoughness"]["baseColorTexture"]["index"]), "Barrel_Albedo")
    save(image(js, blob, b["normalTexture"]["index"]), "Barrel_Normal")
    save_metal_smooth(image(js, blob, b["pbrMetallicRoughness"]["metallicRoughnessTexture"]["index"]), "Barrel_MS")


if __name__ == "__main__":
    main()
