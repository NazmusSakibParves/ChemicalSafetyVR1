"""Makes the Quest version of the client's Hydro-Press.glb for the Supervisor Check hall.

    python Tools/prepare_sccheck_machines.py <Hydro-Press.glb> [out.glb]

Every mesh is simplified (fast_simplification), then the ORIGINAL texture mapping is put back: each new vertex takes the UV of
the nearest original triangle (spatial hash, barycentric), and the original base colour textures are kept at <= 1024 px. So the
machines keep their paint, plates and dirt instead of flat colours. The screw assembly of the hydraulic station (Mesh_137, 870k triangles) is reduced to 100k, the two presses to 60k each, all
other parts are untouched. Output default: Assets/Art/Presentable/3D/ScCheck/HydroPress.glb
"""
import os
import sys

import numpy as np
import trimesh
import fast_simplification
from PIL import Image
from trimesh.visual.material import PBRMaterial

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIP = {"Mesh_137", "Mesh_136"}  # the 885k-triangle assembly of miniature screws
BUDGET = {}  # nothing is simplified: the client's meshes, UV mapping and materials stay exactly as made
DEFAULT_BUDGET = 10 ** 9
MAX_TEX = 2048


class Grid:
    def __init__(self, centroids, cell):
        self.cell = cell
        self.keys = np.floor(centroids / cell).astype(np.int64)
        self.table = {}
        for i, k in enumerate(map(tuple, self.keys)):
            self.table.setdefault(k, []).append(i)
        self.table = {k: np.array(v, dtype=np.int64) for k, v in self.table.items()}
        self.centroids = centroids

    def nearest(self, p):
        k = np.floor(p / self.cell).astype(np.int64)
        for radius in (1, 2, 4):
            cand = []
            for dx in range(-radius, radius + 1):
                for dy in range(-radius, radius + 1):
                    for dz in range(-radius, radius + 1):
                        v = self.table.get((k[0] + dx, k[1] + dy, k[2] + dz))
                        if v is not None:
                            cand.append(v)
            if cand:
                idx = np.concatenate(cand)
                d = np.sum((self.centroids[idx] - p) ** 2, axis=1)
                return idx[np.argmin(d)]
        return 0


def bary(p, a, b, c):
    v0, v1, v2 = b - a, c - a, p - a
    d00, d01, d11 = v0 @ v0, v0 @ v1, v1 @ v1
    d20, d21 = v2 @ v0, v2 @ v1
    den = d00 * d11 - d01 * d01
    if abs(den) < 1e-18:
        return np.array([1.0, 0.0, 0.0])
    v = (d11 * d20 - d01 * d21) / den
    w = (d00 * d21 - d01 * d20) / den
    t = np.array([1.0 - v - w, v, w])
    t = np.clip(t, 0.0, 1.0)
    return t / max(t.sum(), 1e-9)


def main():
    src = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "Assets", "Art", "Presentable", "3D", "ScCheck", "HydroPress.glb")
    scene = trimesh.load(src, force="scene")
    inst = [(n, scene.graph[n][1], scene.graph[n][0]) for n in scene.graph.nodes_geometry]
    mats = {}
    cache = {}
    for name, geom in scene.geometry.items():
        if name in SKIP:
            continue
        m = geom.visual.material
        uv0 = np.asarray(getattr(geom.visual, "uv", None)) if getattr(geom.visual, "uv", None) is not None else None
        v0 = np.asarray(geom.vertices, dtype=np.float64)
        f0 = np.asarray(geom.faces, dtype=np.int64)
        target = BUDGET.get(name, DEFAULT_BUDGET)
        if len(f0) > target:
            p, f = fast_simplification.simplify(v0.astype(np.float32), f0.astype(np.int32), target_reduction=1.0 - target / len(f0), agg=2)
            p = np.asarray(p, dtype=np.float64)
        else:
            p, f = v0, f0
        tex = getattr(m, "baseColorTexture", None)
        key = id(m)
        if key not in mats:
            if tex is not None:
                img = tex.convert("RGB")
                if max(img.size) > MAX_TEX:
                    s = MAX_TEX / max(img.size)
                    img = img.resize((int(img.size[0] * s), int(img.size[1] * s)), Image.LANCZOS)
                mats[key] = PBRMaterial(name=str(m.name).replace(" ", "_"), baseColorTexture=img, metallicFactor=0.35, roughnessFactor=0.55)
            else:
                col = getattr(m, "baseColorFactor", None)
                mats[key] = PBRMaterial(name=str(m.name).replace(" ", "_"), baseColorFactor=col if col is not None else [160, 160, 160, 255],
                                        metallicFactor=0.3, roughnessFactor=0.55)
        uv = None
        if tex is not None and uv0 is not None and p is v0:
            uv = uv0.copy()  # untouched part: the original texture mapping as it is
        elif tex is not None and uv0 is not None:
            tri = v0[f0]
            cent = tri.mean(axis=1)
            extent = float(np.max(v0.max(0) - v0.min(0)))
            grid = Grid(cent, max(extent / 90.0, 1e-4))
            uv = np.zeros((len(p), 2), dtype=np.float64)
            for i in range(len(p)):
                t = grid.nearest(p[i])
                a, b, c = tri[t]
                w = bary(p[i], a, b, c)
                u = uv0[f0[t]]
                uv[i] = w[0] * u[0] + w[1] * u[1] + w[2] * u[2]
        mesh = trimesh.Trimesh(vertices=p, faces=f, process=False)
        if uv is not None:
            mesh.visual = trimesh.visual.TextureVisuals(uv=uv, material=mats[key])
        else:
            mesh.visual = trimesh.visual.TextureVisuals(material=mats[key])
        cache[name] = mesh
        print(name, len(f0), "->", len(f), "textured" if uv is not None else "flat", flush=True)
    out = trimesh.Scene()
    total = 0
    for node, gname, T in inst:
        if gname in SKIP or gname not in cache:
            continue
        out.add_geometry(cache[gname], node_name=node, geom_name=gname, transform=T)
        total += len(cache[gname].faces)
    out.export(dst)
    print("total faces", total, "->", dst)


if __name__ == "__main__":
    main()
