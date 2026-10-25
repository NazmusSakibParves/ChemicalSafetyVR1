"""Prepares the Scenario 7 (factory fire) resources before Unity import.

Models from `Scene07 workflow/3D`: electric panel, Factory02, Fire Alarm, fire extinguishers 1 keep their geometry and get their
images shrunk (1024 edge, JPEG) -> Assets/Art/Presentable/3D/Sc07/{ElectricPanel,Factory,FireAlarm,Extinguishers}.glb.

Waker.glb is a single static T-pose worker (386k triangles, 24 MB, no skeleton). It is cut into parts (head, hands/arms,
uniform, boots, helmet, eyes, lashes), decimated and written as Worker_Hi.glb (hero, near) and Worker_Lo.glb (crowd) with
plain meshes + UVs; the textures go to 2D/Sc07Tex (Worker_Head.jpg, Worker_Arms.jpg, Worker_Eye.png, Worker_Lash.png) so Unity
can tint them per person (skin tone). The skeleton and the bone weights are made in the Unity builder (Sc07HumanRig).
The body skin under the uniform (about 200k hidden triangles) is dropped. The Downloads originals are not modified.

    python Tools/prepare_sc07_models.py
Needs: pip install numpy pillow trimesh fast-simplification
"""
import io
import json
import os
import struct

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.expanduser("~"), "Downloads", "Scene07 workflow-20261025T065759Z-1-001", "Scene07 workflow", "3D")
OUT3D = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "Sc07")
OUT2D = os.path.join(ROOT, "Assets", "Art", "Presentable", "2D", "Sc07Tex")
MAX_EDGE = 1024
JPEG_Q = 76
MODELS = {
    "electric panel.glb": "ElectricPanel.glb",
    "Factory02.glb": "Factory.glb",
    "Fire Alarm Model 3D.glb": "FireAlarm.glb",
    "fire extinguishers 1.glb": "Extinguishers.glb",
}


# ----------------------------------------------------------------------------- glb helpers
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


def shrink_model(src_name, dst_name):
    js, blob = read_glb(os.path.join(SRC, src_name))
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
    print(src_name, os.path.getsize(os.path.join(SRC, src_name)) // 1024, "KB ->", dst_name, os.path.getsize(dst) // 1024, "KB")


# ----------------------------------------------------------------------------- the worker
def accessor(js, blob, idx):
    a = js["accessors"][idx]
    bv = js["bufferViews"][a["bufferView"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
    dt = {5126: "<f4", 5123: "<u2", 5125: "<u4"}[a["componentType"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    arr = np.frombuffer(blob, dtype=dt, count=a["count"] * n, offset=start)
    return arr.reshape(-1, n) if n > 1 else arr


def image_of(js, blob, material_name, key="baseColorTexture"):
    mat = next(m for m in js["materials"] if m["name"] == material_name)
    tex = mat["pbrMetallicRoughness"][key]["index"]
    im = js["images"][js["textures"][tex]["source"]]
    bv = js["bufferViews"][im["bufferView"]]
    start = bv.get("byteOffset", 0)
    return Image.open(io.BytesIO(blob[start:start + bv["byteLength"]]))


def decimate(v, f, tris):
    """Geometry-only quadric decimation on welded vertices; returns new v, f."""
    import fast_simplification as fs
    import trimesh
    if len(f) <= tris:
        return v, f
    tm = trimesh.Trimesh(vertices=v, faces=f, process=True)
    tm.merge_vertices()
    tm.update_faces(tm.nondegenerate_faces())
    red = 1.0 - tris / float(len(tm.faces))
    v2, f2 = fs.simplify(np.asarray(tm.vertices, np.float32), np.asarray(tm.faces, np.uint32), target_reduction=red, agg=6)
    return np.asarray(v2, np.float32), np.asarray(f2, np.int64)


def reproject_uv(v_new, v_old, f_old, uv_old):
    """UV of each decimated vertex = UV of the nearest original vertex (the originals are dense, millimetre spacing).
    Chunked brute force with numpy (no scipy/rtree needed)."""
    used = np.unique(f_old)
    pts = v_old[used].astype(np.float32)
    sq = (pts * pts).sum(axis=1)
    out = np.zeros((len(v_new), 2), np.float32)
    for i in range(0, len(v_new), 400):
        q = v_new[i:i + 400].astype(np.float32)
        d = sq[None, :] - 2.0 * q @ pts.T  # the |q|^2 term does not change the argmin
        out[i:i + 400] = uv_old[used[np.argmin(d, axis=1)]]
    return out


def worker():
    import trimesh
    js, blob = read_glb(os.path.join(SRC, "Waker.glb"))
    prims = {}
    for p in js["meshes"][0]["primitives"]:
        name = js["materials"][p["material"]]["name"]
        v = accessor(js, blob, p["attributes"]["POSITION"]).astype(np.float32)
        uv = accessor(js, blob, p["attributes"]["TEXCOORD_0"]).astype(np.float32)
        f = accessor(js, blob, p["indices"]).reshape(-1, 3).astype(np.int64)
        prims[name] = (v, uv, f)

    def keep_y(name, ymin):
        v, uv, f = prims[name]
        c = v[f].mean(axis=1)[:, 1]
        prims[name] = (v, uv, f[c >= ymin])

    keep_y("face", 2.78)   # head and neck only: the skin under the uniform is never seen
    # sizes: hi = hero/near, lo = crowd
    plan = {
        "Head": ("face", 5200, 2000, True),
        "Arms": ("arms", 2600, 900, True),
        "Uniform": ("pants", 5200, 2200, False),
        "Collar": ("Material.001", 900, 300, False),
        "Boots": ("boot", 1500, 500, False),
        "Helmet": ("helmet", 2200, 700, False),
        "Eyes": ("Material.002", 1520, 300, True),
        "Lashes": ("eyelashes", 104, 60, True),
    }
    for lod, col in (("Hi", 1), ("Lo", 2)):
        scene = trimesh.Scene()
        total = 0
        for part, spec in plan.items():
            src_name, tris_hi, tris_lo, keep_uv = spec[0], spec[1], spec[2], spec[3]
            v, uv, f = prims[src_name]
            tris = spec[col]
            v2, f2 = decimate(v, f, tris)
            uv2 = reproject_uv(v2, v, f, uv) if keep_uv else np.zeros((len(v2), 2), np.float32)
            tm = trimesh.Trimesh(vertices=v2, faces=f2, process=False)
            tm.visual = trimesh.visual.TextureVisuals(uv=uv2)
            scene.add_geometry(tm, node_name=part, geom_name=part)
            total += len(f2)
            print(" ", lod, part, len(f), "->", len(f2))
        dst = os.path.join(OUT3D, "Worker_%s.glb" % lod)
        scene.export(dst)
        print("Worker", lod, "tris", total, os.path.getsize(dst) // 1024, "KB")

    def save(name, material, size, rgba=False):
        im = image_of(js, blob, material)
        im.thumbnail((size, size), Image.Resampling.LANCZOS)
        if rgba:
            im.convert("RGBA").save(os.path.join(OUT2D, name), optimize=True)
        else:
            im.convert("RGB").save(os.path.join(OUT2D, name), quality=82)
        print(name, im.size)

    save("Worker_Head.jpg", "face", 1024)
    save("Worker_Arms.jpg", "arms", 512)
    save("Worker_Eye.png", "Material.002", 256, True)
    save("Worker_Lash.png", "eyelashes", 256, True)


def main():
    os.makedirs(OUT3D, exist_ok=True)
    os.makedirs(OUT2D, exist_ok=True)
    for s, d in MODELS.items():
        shrink_model(s, d)
    worker()


if __name__ == "__main__":
    main()
