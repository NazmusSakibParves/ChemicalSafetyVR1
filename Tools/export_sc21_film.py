# Exports the animator's Sc_21_v4.blend (680 frames, 24 fps) to Assets/Art/Presentable/3D/Sc21/Sc21Film.glb
# Run:  blender-launcher.exe -b "<...>/3D/Sc_21_v4.blend" --python Tools/export_sc20_film.py -- "<...>/3D/textures" "<out.glb>" "<log.txt>"
import bpy, os, sys

argv = sys.argv[sys.argv.index("--") + 1:]
tex_dir, out_glb, log_path = argv[0], argv[1], argv[2]
log = open(log_path, "w", encoding="utf-8")


def w(*a):
    log.write(" ".join(str(x) for x in a) + "\n")
    log.flush()


# 1. re-link every picture from the textures folder, cap at 1024 px in memory
by_name = {}
for f in os.listdir(tex_dir):
    by_name[f.lower()] = os.path.join(tex_dir, f)
missing = []
for img in bpy.data.images:
    if img.type != "IMAGE" or img.name in ("Render Result", "Viewer Node"):
        continue
    base = os.path.basename(img.filepath.replace("\\", "/")) if img.filepath else img.name
    p = by_name.get(base.lower()) or by_name.get(img.name.lower())
    if p is None:
        for k, v in by_name.items():
            if os.path.splitext(k)[0] == os.path.splitext(img.name.lower())[0]:
                p = v
                break
    if p is None:
        # names of the blend are cut short (e.g. '..._SafetyVest_Safety_Vest_Ba') or carry '.001': match by prefix, then the 'Worker_material' set
        stem = os.path.splitext(img.name.lower())[0]
        for suf in (".001", ".002"):
            if stem.endswith(suf):
                stem = stem[: -len(suf)]
        stem2 = stem.replace("worker_material_", "worker_head_material_")
        for k, v in by_name.items():
            ks = os.path.splitext(k)[0]
            if ks.startswith(stem) or ks.startswith(stem2) or stem.startswith(ks):
                p = v
                break
    if p is None:
        missing.append(img.name)
        continue
    try:
        img.filepath = p
        img.reload()
        cap = 2048 if any(w in img.name.lower() for w in ("worker", "vest", "helmet", "head")) else 1024   # characters stay sharp
        if max(img.size) > cap:
            k = float(cap) / max(img.size)
            img.scale(max(1, int(img.size[0] * k)), max(1, int(img.size[1] * k)))
    except Exception as e:
        w("img fail", img.name, e)
w("images", len(bpy.data.images), "missing", missing)

# 2. export the whole timeline
sc = bpy.context.scene
sc.frame_start, sc.frame_end = 1, 680
bpy.ops.export_scene.gltf(
    filepath=out_glb,
    export_format="GLB",
    export_animations=True,
    export_animation_mode="SCENE",
    export_frame_range=True,
    export_frame_step=1,
    export_def_bones=True,
    export_cameras=True,
    export_lights=False,
    export_apply=False,
    export_image_format="AUTO",
)
w("exported", out_glb, os.path.getsize(out_glb))
log.close()
