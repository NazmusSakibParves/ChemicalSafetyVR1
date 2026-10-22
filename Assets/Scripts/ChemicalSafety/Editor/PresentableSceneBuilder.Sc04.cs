#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 4 lab, modelled on the Scene 04 workflow. Hidden until Scenario 3 finishes.
    /// Viewer stands at Spawn_SC04 facing -Z, so the viewer's left is +X.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc04Refs
        {
            public Transform spawn;
            public GloveInspect inspect;
        }

        private const float Sc04Ox = 120f;

        private static Sc04Refs BuildSc04Room(Transform env, PresentableArt art)
        {
            const float ox = Sc04Ox;
            var refs = new Sc04Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            var solid = SolidLayer;

            var floorMat = Mat("Sc04Floor", new Color(0.62f, 0.66f, 0.68f), 0.28f, 0.08f);
            var wallMat = Mat("Sc04Wall", new Color(0.78f, 0.8f, 0.82f), 0.08f, 0f);
            var ceilMat = Mat("Sc04Ceil", new Color(0.7f, 0.72f, 0.74f), 0.12f, 0.05f);
            var steel = Mat("Sc04Steel", new Color(0.55f, 0.58f, 0.6f), 0.42f, 0.72f);
            var cloth = Mat("Sc04Cloth", new Color(0.9f, 0.91f, 0.9f), 0.08f, 0f);
            var yellow = Mat("Sc04Tray", new Color(0.86f, 0.72f, 0.12f), 0.25f, 0.05f);
            var tube = Mat("Sc04Tube", new Color(0.9f, 0.93f, 0.88f), 0f, 0f, null, new Color(0.7f, 0.78f, 0.7f), true);
            var arrowMat = Mat("Sc04Arrow", Color.white, 0f, 0f, art.arrowDown != null ? art.arrowDown.texture : null,
                new Color(0.35f, 0.9f, 0.45f), true);

            void Shell(string name, Vector3 pos, Vector3 size, Material mat)
            {
                var go = Box(name, room, pos, size, mat);
                if (solid >= 0)
                    go.layer = solid;
            }

            var floor = Box("Floor", room, new Vector3(ox, -0.05f, 0.2f), new Vector3(8f, 0.1f, 7.2f), floorMat);
            floor.AddComponent<TeleportationArea>();
            Shell("Wall_Back", new Vector3(ox, 1.6f, -3.2f), new Vector3(8f, 3.3f, 0.12f), wallMat);
            Shell("Wall_Front", new Vector3(ox, 1.6f, 3.6f), new Vector3(8f, 3.3f, 0.12f), wallMat);
            Shell("Wall_Left", new Vector3(ox + 3.94f, 1.6f, 0.2f), new Vector3(0.12f, 3.3f, 6.8f), wallMat);
            Shell("Wall_Right", new Vector3(ox - 3.94f, 1.6f, 0.2f), new Vector3(0.12f, 3.3f, 6.8f), wallMat);
            Shell("Ceiling", new Vector3(ox, 3.22f, 0.2f), new Vector3(8f, 0.08f, 7.2f), ceilMat);
            Box("Tube", room, new Vector3(ox, 3.05f, 0.4f), new Vector3(2.4f, 0.04f, 0.16f), tube, false);
            Box("TubeB", room, new Vector3(ox - 2.2f, 3.05f, -1.2f), new Vector3(1.6f, 0.04f, 0.16f), tube, false);

            var lightGo = new GameObject("LabLight");
            lightGo.transform.SetParent(room, false);
            lightGo.transform.position = new Vector3(ox, 2.9f, 0.3f);
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(0.85f, 0.9f, 0.84f);
            lamp.intensity = 1.6f;
            lamp.range = 9f;

            // Near bench: the three gloves. Far bench: glassware, tray, bottle. Hood on the back wall.
            Shell("BenchNear", new Vector3(ox, 0.42f, 0.55f), new Vector3(2.3f, 0.84f, 0.9f), steel);
            Shell("BenchSide", new Vector3(ox + 1.7f, 0.42f, -0.85f), new Vector3(1.5f, 0.84f, 2.2f), steel);
            Box("Cloth", room, new Vector3(ox, 0.86f, 0.55f), new Vector3(1.15f, 0.02f, 0.48f), cloth, false);

            var table = SpawnSc04("table02.glb", room, new Vector3(ox + 1.7f, 0f, -0.85f), 0f, 1.45f);
            if (table != null)
                SitOn(table, 0.84f);

            var hood = SpawnSc04("fume_cupboards.glb", room, new Vector3(ox - 0.2f, 0f, -2.3f), 180f, 2.3f);
            if (hood != null)
                SitOn(hood, 0f);
            var glass = SpawnSc04("chemistry_glassware.glb", room, new Vector3(ox + 1.55f, 0.86f, -0.7f), 20f, 0.55f);
            if (glass != null)
                SitOn(glass, 0.86f);
            var pipettes = SpawnSc04("mechanical_pipette.glb", room, new Vector3(ox + 2.05f, 0.86f, -1.15f), -30f, 0.28f);
            if (pipettes != null)
                SitOn(pipettes, 0.86f);
            var tray = SpawnSc04("tray.glb", room, new Vector3(ox + 1.35f, 0.86f, -0.35f), 0f, 0.42f);
            if (tray != null)
                SitOn(tray, 0.86f);
            else
                Box("Tray", room, new Vector3(ox + 1.35f, 0.9f, -0.35f), new Vector3(0.42f, 0.08f, 0.3f), yellow, false);

            var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "HclBottle";
            bottle.transform.SetParent(room, true);
            bottle.transform.position = new Vector3(ox + 1.35f, 1.08f, -0.35f);
            bottle.transform.localScale = new Vector3(0.1f, 0.16f, 0.1f);
            bottle.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04Bottle", new Color(0.85f, 0.9f, 0.88f, 0.45f), 0.6f, 0.05f);
            if (solid >= 0)
                bottle.layer = solid;
            var labelTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/BottleLabel.png");
            var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
            label.name = "BottleLabel";
            Object.DestroyImmediate(label.GetComponent<Collider>());
            label.transform.SetParent(bottle.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.15f, 0.55f);
            label.transform.localScale = new Vector3(1.6f, 1.1f, 1f);
            label.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04BottleLabel", Color.white, 0.2f, 0f, labelTex, null, true);

            BuildAc(room, new Vector3(ox + 3.82f, 2.15f, 0.4f));

            var glovesModel = SpawnSc04("gloves1.glb", room, new Vector3(ox, 0.9f, 0.55f), 0f, 0.95f);
            Transform[] gloveGroups = null;
            if (glovesModel != null)
                gloveGroups = SplitGloves(glovesModel, room);

            var arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrow.name = "GloveArrow";
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(room, true);
            arrow.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
            arrow.SetActive(false);

            var inspectGo = new GameObject("GloveInspect");
            inspectGo.transform.SetParent(room, false);
            var inspect = inspectGo.AddComponent<GloveInspect>();
            var so = new SerializedObject(inspect);
            so.FindProperty("art").objectReferenceValue = art;
            so.FindProperty("arrow").objectReferenceValue = arrow.transform;
            var arr = so.FindProperty("gloves");
            arr.arraySize = 3;
            for (var i = 0; i < 3; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue =
                    gloveGroups != null && i < gloveGroups.Length ? gloveGroups[i] : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            refs.inspect = inspect;
            refs.spawn = Marker("Spawn_SC04", room, new Vector3(ox, 0f, 2.15f), 180f);
            return refs;
        }

        private static void BuildAc(Transform room, Vector3 pos)
        {
            var body = Mat("Sc04Ac", new Color(0.75f, 0.77f, 0.78f), 0.35f, 0.2f);
            var vent = Mat("Sc04Vent", new Color(0.25f, 0.28f, 0.3f), 0.2f, 0.4f);
            Box("AC", room, pos, new Vector3(0.12f, 0.32f, 0.9f), body);
            Box("ACVent", room, pos + new Vector3(-0.04f, -0.06f, 0f), new Vector3(0.04f, 0.08f, 0.7f), vent, false);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Sc04/AC_Display.png");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "ACDisplay";
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(room, true);
            screen.transform.position = pos + new Vector3(-0.07f, 0.06f, 0f);
            screen.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            screen.transform.localScale = new Vector3(0.22f, 0.08f, 1f);
            screen.GetComponent<MeshRenderer>().sharedMaterial =
                Mat("Sc04AcDisplay", Color.white, 0f, 0f, tex, new Color(0.15f, 0.35f, 0.28f), true);
        }

        private static GameObject SpawnSc04(string file, Transform parent, Vector3 pos, float yaw, float targetSize)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc04/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc04 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var b = WorldBounds(go);
            var wide = Mathf.Max(b.size.x, b.size.z);
            if (wide > 0.01f)
                go.transform.localScale *= targetSize / wide;
            return go;
        }

        private static void SitOn(GameObject go, float y)
        {
            var b = WorldBounds(go);
            go.transform.position += new Vector3(0f, y - b.min.y, 0f);
        }

        // Left to right in the player's view (+X to -X): cotton, nitrile, neoprene.
        private static Transform[] SplitGloves(GameObject model, Transform room)
        {
            var rends = model.GetComponentsInChildren<Renderer>();
            System.Array.Sort(rends, (a, b) => b.bounds.center.x.CompareTo(a.bounds.center.x));
            var groups = new Transform[3];
            var n = rends.Length;
            for (var g = 0; g < 3; g++)
            {
                var from = g * n / 3;
                var to = (g + 1) * n / 3;
                if (to <= from)
                    continue;
                var parent = new GameObject("Glove" + g).transform;
                parent.SetParent(room, true);
                var center = Vector3.zero;
                for (var i = from; i < to; i++)
                    center += rends[i].bounds.center;
                parent.position = center / (to - from);
                var parts = new Transform[to - from];
                var bb = rends[from].bounds;
                for (var i = from; i < to; i++)
                {
                    parts[i - from] = rends[i].transform;
                    bb.Encapsulate(rends[i].bounds);
                }

                var follow = parent.gameObject.AddComponent<GloveParts>();
                var fso = new SerializedObject(follow);
                var partProp = fso.FindProperty("parts");
                partProp.arraySize = parts.Length;
                for (var i = 0; i < parts.Length; i++)
                    partProp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                fso.ApplyModifiedPropertiesWithoutUndo();
                var col = parent.gameObject.AddComponent<BoxCollider>();
                col.center = parent.InverseTransformPoint(bb.center);
                col.size = bb.size;
                if (SolidLayer >= 0)
                    parent.gameObject.layer = SolidLayer;
                var poke = parent.gameObject.AddComponent<PokeTarget>();
                var pso = new SerializedObject(poke);
                pso.FindProperty("reach").floatValue = 0.07f;
                pso.ApplyModifiedPropertiesWithoutUndo();
                groups[g] = parent;
            }

            return groups;
        }
    }
}
#endif
