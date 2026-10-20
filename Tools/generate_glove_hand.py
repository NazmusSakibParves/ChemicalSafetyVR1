"""Builds a smooth, plump rubber-glove hand mesh that is skinned to the XR Hands skeleton.

The stock XR Hands visualiser mesh is a thin 1.6k-vertex hand. This replaces its surface (the skeleton, the bone
names and the bone bending code stay the same) with a smooth glove shape: slim rounded fingers fused at the base,
a flat palm and a wrist block, made from a signed distance field of tapered capsules (smooth union) that is turned
into a mesh with marching tetrahedra, decimated and skinned to the nearest two bones.

    python Tools/generate_glove_hand.py

Output: Assets/Art/Presentable/3D/GloveHand_L.bytes and GloveHand_R.bytes (read by the editor menu
"Chemical Safety/Presentable/Import Glove Hand Meshes").  Joint positions are the rest pose of
Assets/Samples/XR Hands/.../LeftHand.fbx in mesh space (right hand = mirrored in x).
"""
import os
import struct

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Presentable", "3D")

J = {  # left hand rest pose joints, mesh space (metres)
    "Wrist": (0.0000, 0.0000, -0.0800), "Palm": (0.0009, -0.0013, -0.0322),
    "IndexMetacarpal": (0.0198, -0.0095, -0.0436), "IndexProximal": (0.0236, -0.0073, 0.0160),
    "IndexIntermediate": (0.0288, -0.0189, 0.0517), "IndexDistal": (0.0314, -0.0321, 0.0720),
    "IndexTip": (0.0324, -0.0432, 0.0914),
    "MiddleMetacarpal": (0.0036, -0.0076, -0.0457), "MiddleProximal": (0.0017, -0.0025, 0.0156),
    "MiddleIntermediate": (0.0009, -0.0185, 0.0555), "MiddleDistal": (0.0007, -0.0382, 0.0748),
    "MiddleTip": (-0.0002, -0.0540, 0.0941),
    "RingMetacarpal": (-0.0150, -0.0060, -0.0452), "RingProximal": (-0.0175, -0.0065, 0.0087),
    "RingIntermediate": (-0.0238, -0.0132, 0.0466), "RingDistal": (-0.0244, -0.0318, 0.0656),
    "RingTip": (-0.0231, -0.0504, 0.0812),
    "LittleMetacarpal": (-0.0230, -0.0094, -0.0459), "LittleProximal": (-0.0351, -0.0137, -0.0021),
    "LittleIntermediate": (-0.0471, -0.0138, 0.0261), "LittleDistal": (-0.0504, -0.0273, 0.0410),
    "LittleTip": (-0.0499, -0.0434, 0.0559),
    "ThumbMetacarpal": (0.0302, -0.0161, -0.0455), "ThumbProximal": (0.0511, -0.0278, -0.0235),
    "ThumbDistal": (0.0592, -0.0419, 0.0061), "ThumbTip": (0.0610, -0.0546, 0.0271),
}

# (start joint, end joint, radius at start, radius at end, owner bone, flatten y)
SEGS = []


def add(a, b, r0, r1, owner, flat=1.0):
    SEGS.append((a, b, r0, r1, owner, flat))


# wrist block and palm
add("Wrist", "Palm", 0.031, 0.030, "Wrist", 1.5)
add("Palm", "MiddleProximal", 0.029, 0.024, "Palm", 1.9)
for f in ("Index", "Middle", "Ring", "Little"):
    add(f + "Metacarpal", f + "Proximal", 0.0135, 0.0125, "Palm", 1.2)
add("IndexProximal", "LittleProximal", 0.0125, 0.0115, "Palm", 1.25)  # knuckle bar
add("ThumbMetacarpal", "ThumbProximal", 0.0185, 0.0150, "ThumbMetacarpal", 1.15)
add("Palm", "ThumbProximal", 0.0185, 0.0150, "Palm", 1.35)  # thenar pad
# fingers
for f, radii in (("Index", (0.0108, 0.0099, 0.0092, 0.0078)), ("Middle", (0.0110, 0.0101, 0.0094, 0.0079)),
                 ("Ring", (0.0104, 0.0095, 0.0088, 0.0075)), ("Little", (0.0092, 0.0085, 0.0078, 0.0066))):
    add(f + "Proximal", f + "Intermediate", radii[0], radii[1], f + "Proximal")
    add(f + "Intermediate", f + "Distal", radii[1], radii[2], f + "Intermediate")
    add(f + "Distal", f + "Tip", radii[2], radii[3], f + "Distal")
add("ThumbProximal", "ThumbDistal", 0.0140, 0.0122, "ThumbProximal")
add("ThumbDistal", "ThumbTip", 0.0122, 0.0098, "ThumbDistal")
# a short cuff so the glove starts at the wrist and the sleeve can continue it
add("Wrist", "Wrist", 0.0, 0.0, "Wrist")  # placeholder replaced below
SEGS.pop()
CUFF = ((0.0, -0.002, -0.118), (0.0, -0.002, -0.080), 0.0345, 0.0335, "Wrist", 1.0)
SEGS.append(("__cuff_a", "__cuff_b", CUFF[2], CUFF[3], CUFF[4], CUFF[5]))
J["__cuff_a"] = CUFF[0]
J["__cuff_b"] = CUFF[1]

SMOOTH_K = 0.0028
STEP = 0.0028


def seg_dist(p, a, b, r0, r1, flat):
    q = p.copy()
    a = np.array(a, dtype=np.float32)
    b = np.array(b, dtype=np.float32)
    if flat != 1.0:
        q[:, 1] *= flat
        a = a * np.array([1, flat, 1], dtype=np.float32)
        b = b * np.array([1, flat, 1], dtype=np.float32)
    ab = b - a
    denom = float(ab @ ab)
    if denom < 1e-12:
        t = np.zeros(len(q), dtype=np.float32)
    else:
        t = np.clip(((q - a) @ ab) / denom, 0.0, 1.0)
    c = a + t[:, None] * ab
    d = np.linalg.norm(q - c, axis=1) - (r0 + (r1 - r0) * t)
    return d / (1.0 if flat == 1.0 else (flat if flat > 1 else 1.0))


def smin(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


PALM_OWNERS = ("Wrist", "Palm", "ThumbMetacarpal")


def sdf(p):
    # the palm and wrist volumes blend softly (no ridges on the back of the hand), the fingers stay crisp
    palm = np.full(len(p), 1e3, dtype=np.float32)
    fing = np.full(len(p), 1e3, dtype=np.float32)
    for a, b, r0, r1, o, flat in SEGS:
        d = seg_dist(p, J[a], J[b], r0, r1, flat)
        if o in PALM_OWNERS:
            palm = smin(palm, d, 0.016)
        else:
            fing = smin(fing, d, SMOOTH_K)
    return smin(palm, fing, 0.007)


def march_tets(values, origin, step):
    nx, ny, nz = values.shape
    ix, iy, iz = np.meshgrid(np.arange(nx - 1), np.arange(ny - 1), np.arange(nz - 1), indexing="ij")
    base = np.stack([ix.ravel(), iy.ravel(), iz.ravel()], axis=1)
    corner = np.array([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)])
    tets = [(0, 5, 1, 6), (0, 1, 2, 6), (0, 2, 3, 6), (0, 3, 7, 6), (0, 7, 4, 6), (0, 4, 5, 6)]
    cases = {
        1: [(0, 1), (0, 2), (0, 3)], 2: [(0, 1), (1, 2), (1, 3)], 4: [(0, 2), (1, 2), (2, 3)],
        8: [(0, 3), (1, 3), (2, 3)],
        3: [(0, 2), (0, 3), (1, 3), (0, 2), (1, 3), (1, 2)],
        5: [(0, 1), (1, 2), (2, 3), (0, 1), (2, 3), (0, 3)],
        6: [(0, 1), (0, 2), (2, 3), (0, 1), (2, 3), (1, 3)],
    }
    full = {}
    for c, tri in cases.items():
        full[c] = tri
        full[15 - c] = tri
    edge_keys, edge_pos, tri_edges = [], [], []
    gid = lambda pts: (pts[:, 0] * ny + pts[:, 1]) * nz + pts[:, 2]
    for tet in tets:
        pts = [base + corner[i] for i in tet]
        vals = np.stack([values[p[:, 0], p[:, 1], p[:, 2]] for p in pts], axis=1)
        mask = (vals < 0).astype(np.int32)
        idx = mask[:, 0] | (mask[:, 1] << 1) | (mask[:, 2] << 2) | (mask[:, 3] << 3)
        for c, tri in full.items():
            sel = np.nonzero(idx == c)[0]
            if len(sel) == 0:
                continue
            for k in range(0, len(tri), 3):
                ids = []
                for (i, j) in tri[k:k + 3]:
                    pi, pj = pts[i][sel], pts[j][sel]
                    vi, vj = vals[sel, i], vals[sel, j]
                    t = vi / (vi - vj)
                    gi, gj = gid(pi), gid(pj)
                    key = np.minimum(gi, gj) * (nx * ny * nz) + np.maximum(gi, gj)
                    pos = (pi + (pj - pi) * t[:, None]) * step + origin
                    edge_keys.append(key)
                    edge_pos.append(pos)
                    ids.append(len(edge_keys) - 1)
                tri_edges.append(ids)
    return edge_keys, edge_pos, tri_edges


def build(prefix_mirror):
    lo = np.array([-0.085, -0.075, -0.13], dtype=np.float32)
    hi = np.array([0.095, 0.03, 0.115], dtype=np.float32)
    dims = np.ceil((hi - lo) / STEP).astype(int) + 1
    gx, gy, gz = np.meshgrid(*[np.arange(n) for n in dims], indexing="ij")
    pts = np.stack([gx.ravel(), gy.ravel(), gz.ravel()], axis=1).astype(np.float32) * STEP + lo
    vals = sdf(pts).reshape(dims)
    ek, ep, te = march_tets(vals, lo, STEP)
    # weld vertices that sit on the same grid edge
    all_keys = np.concatenate(ek)
    all_pos = np.concatenate(ep)
    uniq, first, inv = np.unique(all_keys, return_index=True, return_inverse=True)
    verts = all_pos[first].astype(np.float32)
    offsets = np.cumsum([0] + [len(k) for k in ek])
    tris = []
    for ids in te:
        tri = np.stack([inv[offsets[i]:offsets[i + 1]] for i in ids], axis=1)
        tris.append(tri)
    tris = np.concatenate(tris, axis=0).astype(np.int64)
    ok = (tris[:, 0] != tris[:, 1]) & (tris[:, 1] != tris[:, 2]) & (tris[:, 0] != tris[:, 2])
    tris = tris[ok]
    # outward winding from the field gradient
    eps = 0.0008
    def grad(p):
        g = np.zeros_like(p)
        for ax in range(3):
            o = np.zeros(3, dtype=np.float32)
            o[ax] = eps
            g[:, ax] = sdf(p + o) - sdf(p - o)
        return g
    a, b, c = verts[tris[:, 0]], verts[tris[:, 1]], verts[tris[:, 2]]
    n = np.cross(b - a, c - a)
    g = grad((a + b + c) / 3.0)
    flip = np.einsum("ij,ij->i", n, g) < 0
    tris[flip] = tris[flip][:, ::-1]
    # decimate to keep it light
    try:
        import fast_simplification
        verts2, faces2 = fast_simplification.simplify(verts.astype(np.float32), tris.astype(np.int32),
                                                      target_reduction=0.78)
        verts, tris = verts2.astype(np.float32), faces2.astype(np.int64)
    except Exception as e:  # keep the full mesh if the simplifier is missing
        print("decimation skipped:", e)
    nrm = grad(verts)
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    return verts, nrm.astype(np.float32), tris.astype(np.int32)


def skin(verts):
    owners = sorted({s[4] for s in SEGS})
    dist = np.zeros((len(verts), len(SEGS)), dtype=np.float32)
    for i, (a, b, r0, r1, _o, flat) in enumerate(SEGS):
        dist[:, i] = np.abs(seg_dist(verts, J[a], J[b], r0, r1, flat)) + 0.0
    # per owner bone: nearest segment distance
    per_owner = np.stack([np.min(dist[:, [i for i, s in enumerate(SEGS) if s[4] == o]], axis=1) for o in owners], axis=1)
    order = np.argsort(per_owner, axis=1)[:, :4]
    d = np.take_along_axis(per_owner, order, axis=1)
    w = 1.0 / (d + 0.0025) ** 3
    w[:, 2:] *= 0.0  # only the two nearest bones
    w /= w.sum(axis=1, keepdims=True)
    return owners, order.astype(np.int32), w.astype(np.float32)


def write(path, verts, nrm, tris, owners, bidx, bw):
    uv = np.stack([verts[:, 0] * 14.0, verts[:, 2] * 14.0], axis=1).astype(np.float32)
    with open(path, "wb") as f:
        f.write(b"GLV1")
        f.write(struct.pack("<iii", len(verts), len(tris), len(owners)))
        for name in owners:
            raw = name.encode("utf-8")
            f.write(struct.pack("<i", len(raw)))
            f.write(raw)
        f.write(verts.astype("<f4").tobytes())
        f.write(nrm.astype("<f4").tobytes())
        f.write(uv.astype("<f4").tobytes())
        f.write(bidx.astype("<i4").tobytes())
        f.write(bw.astype("<f4").tobytes())
        f.write(tris.astype("<i4").tobytes())
    print("wrote", path, len(verts), "verts", len(tris), "tris")


def main():
    verts, nrm, tris = build(False)
    owners, bidx, bw = skin(verts)
    write(os.path.join(OUT, "GloveHand_L.bytes"), verts, nrm, tris, owners, bidx, bw)
    # right hand = mirror in x (flip the winding back)
    mv = verts * np.array([-1, 1, 1], dtype=np.float32)
    mn = nrm * np.array([-1, 1, 1], dtype=np.float32)
    write(os.path.join(OUT, "GloveHand_R.bytes"), mv, mn, tris[:, ::-1].copy(), owners, bidx, bw)


if __name__ == "__main__":
    main()
