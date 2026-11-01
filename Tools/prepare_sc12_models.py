"""Cuts the designer's 83 MB 'PPE DONNING STATION.glb' (a kit of PPE parts + plant pieces laid out on a plane) into two small
GLBs for the Quest APK. Every part is baked, re-centred (pivot = bottom centre), textures shrunk to 1024 JPEG, heavy untextured
parts decimated. Original is never imported into Unity.

    python Tools/prepare_sc12_models.py [path to original glb]

Output: Assets/Art/Presentable/3D/Sc12/Sc12Ppe.glb and Sc12Plant.glb
"""
import io
import os
import re
import sys

import numpy as np
import trimesh
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = (sys.argv[1] if len(sys.argv) > 1 else
       r"C:\Users\msi\Downloads\scene12 workflow-20261031T155022Z-1-001\scene12 workflow\3D\PPE DONNING STATION.glb")
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc12")
MAX_EDGE = 1024

# output node name -> list of source node-name prefixes (all submeshes of the first matching instance are merged by name)
H = r"_[0-9a-f]{6}"
PPE = {
    "Helmet": [r"^Helmet_lambert1_0$"],
    "Apron": [r"^Object_2\.002$"],
    "Boot": [r"^Cube\.003_Hazmat_0\.001$"],
    "HazmatSuit": [r"^Cube_Hazmat_0\.003$"],
    "Glove": [r"^Retopo_Cube_Hazmat_0\.002$"],
    "CartMask": [r"^Cylinder\.002" + H + "$"],
    "FaceMask": [r"^Object_2" + H + "$"],
    "HalfMask": [r"^Cylinder_Facemask_0$"],
    "FaceShield": [r"^Cube\.016_Material\.001_0" + H + "$"],
}
PLANT = {
    "TankBig": [r"^SM_PipeEngine\.mo_PipeEngine_M1_0\.001" + H + "$"],
    "Panel": [r"^Duvar panosu_green_0" + H + "$"],
    "PipeWall": [r"^SM_obj_PipeWall_01 - Default_0$"],
    "ControlPanel": [r"^Object_4$"],
}
DECIMATE_OVER = 9000


def shrink_image(im):
    if im is None:
        return None
    im = im.convert("RGB")
    im.thumbnail((MAX_EDGE, MAX_EDGE), Image.Resampling.LANCZOS)
    return im


def world_parts(scene):
    parts = []
    for n in scene.graph.nodes_geometry:
        T, g = scene.graph[n]
        m = scene.geometry[g].copy()
        m.apply_transform(T)
        parts.append((n, m))
    return parts


def has_texture(m):
    mt = getattr(m.visual, "material", None)
    return mt is not None and getattr(mt, "baseColorTexture", None) is not None


def prep(m):
    mt = getattr(m.visual, "material", None)
    if mt is not None:
        for attr in ("baseColorTexture", "normalTexture", "metallicRoughnessTexture", "emissiveTexture", "occlusionTexture"):
            t = getattr(mt, attr, None)
            if t is not None:
                setattr(mt, attr, shrink_image(t))
    if len(m.faces) > DECIMATE_OVER and not has_texture(m):
        try:
            target = max(2500, len(m.faces) // 3)
            d = m.simplify_quadric_decimation(face_count=target)
            d.visual = m.visual.__class__(material=m.visual.material) if hasattr(m.visual, "material") else d.visual
            return d
        except Exception as e:  # keep the original on failure
            print("decimate failed", e)
    return m


def build(parts, table, path):
    out = trimesh.Scene()
    for name, prefixes in table.items():
        sel = []
        for n, m in parts:
            if any(re.search(p, n) for p in prefixes):
                sel.append((n, m))
        if not sel:
            print("MISSING", name)
            continue
        chosen = sel
        allb = np.array([m.bounds for _, m in chosen])
        lo = allb[:, 0].min(axis=0)
        hi = allb[:, 1].max(axis=0)
        pivot = np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])
        for i, (n, m) in enumerate(chosen):
            m = prep(m)
            m.apply_translation(-pivot)
            out.add_geometry(m, node_name=f"{name}_{i}", geom_name=f"{name}_{i}")
        print(name, "parts", len(chosen), "size", np.round(hi - lo, 3), "tris", sum(len(m.faces) for _, m in chosen))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.export(path)
    print("wrote", path, os.path.getsize(path) // 1024, "KB")
    repack(path)


def repack(path):
    """png -> jpeg, identical images shared, unused buffer views dropped."""
    import hashlib
    import json
    import struct
    d = open(path, "rb").read()
    cl = struct.unpack_from("<I", d, 12)[0]
    js = json.loads(d[20:20 + cl])
    off = 20 + cl
    bl = struct.unpack_from("<I", d, off)[0]
    blob = d[off + 8:off + 8 + bl]
    views = [bytes(blob[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]]) for v in js["bufferViews"]]
    seen = {}
    remap = {}
    for i, im in enumerate(js.get("images", [])):
        raw = views[im["bufferView"]]
        img = Image.open(io.BytesIO(raw))
        keep_alpha = img.mode in ("RGBA", "LA") and img.getextrema()[-1][0] < 250
        if keep_alpha:
            buf = io.BytesIO()
            img.save(buf, "PNG", optimize=True)
            mime = "image/png"
        else:
            buf = io.BytesIO()
            img.convert("RGB").save(buf, "JPEG", quality=78, optimize=True)
            mime = "image/jpeg"
        data = buf.getvalue()
        h = hashlib.md5(data).hexdigest()
        if h in seen:
            remap[i] = seen[h]
            continue
        seen[h] = i
        remap[i] = i
        views[im["bufferView"]] = data
        im["mimeType"] = mime
    # redirect textures to the first identical image
    for t in js.get("textures", []):
        if "source" in t:
            t["source"] = remap[t["source"]]
    used_views = {js["images"][i]["bufferView"] for i in set(remap.values())}
    image_views = {im["bufferView"] for im in js.get("images", [])}
    out = bytearray()
    for i, v in enumerate(views):
        if i in image_views and i not in used_views:
            js["bufferViews"][i]["byteLength"] = 0
            js["bufferViews"][i]["byteOffset"] = len(out)
            continue
        while len(out) % 4:
            out.append(0)
        js["bufferViews"][i]["byteOffset"] = len(out)
        js["bufferViews"][i]["byteLength"] = len(v)
        out.extend(v)
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
    print("repacked", path, os.path.getsize(path) // 1024, "KB")


def main():
    scene = trimesh.load(SRC)
    parts = world_parts(scene)
    build(parts, PPE, os.path.join(OUT, "Sc12Ppe.glb"))
    build(parts, PLANT, os.path.join(OUT, "Sc12Plant.glb"))


if __name__ == "__main__":
    main()
