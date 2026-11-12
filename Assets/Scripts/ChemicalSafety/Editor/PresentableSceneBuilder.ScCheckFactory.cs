#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenarios 13-18 in the client's factory room (the one of Scenarios 6 / 8 / 9 / 10): the room is a COPY of the finished Scenario 6 room
    /// (<see cref="ScCheckCaptureHall"/> clones it right after it is built and before the Scenario 8 / 9 / 10 mounts are added), moved to the
    /// Scenario 13-18 origin, with every script removed and the three Scenario 6 containers (acid can, water jug, mixture cup) taken off the table.
    /// Nothing of the Scenario 1-12 rooms is changed or shared, so none of them can break. On the table stands ONE chemical per scenario
    /// (<c>ScChemicals</c>, the runner shows the one of the running scenario): the designer's HCl bottle (13), nitric acid (14), a green gas cylinder
    /// (15), hydrogen peroxide (16, 17) and a bleaching powder sack (18).
    /// <c>ScCheckUseFactory = false</c> + Build Presentable Scene gives the previous Scenario 13-18 brick hall back (nothing of it is deleted).
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool ScCheckUseFactory = true;
        /// <summary>2026-11-12: the designer's glb models of nitric acid / chlorine gas / hydrogen peroxide (SC14-17), the new printed HCl label (SC13) and
        /// bleaching powder label (SC18). <c>false</c> + Build Presentable Scene = the earlier self-made props and label plates.</summary>
        private const bool ScCheckUseDesignerModels = true;
        private static GameObject _scCheckHall;

        /// <summary>Called once, right after the Scenario 6 room is built: keeps a hidden copy of it for the Scenario 13-18 hall.</summary>
        private static void ScCheckCaptureHall(Transform sc6Room)
        {
            _scCheckHall = null;
            if (!ScCheckUseFactory || sc6Room == null)
                return;
            _scCheckHall = Object.Instantiate(sc6Room.gameObject);
            _scCheckHall.name = "Room";
        }

        private static ScCheckRefs BuildScCheckFactory(Transform env, PresentableArt art)
        {
            var refs = new ScCheckRefs();
            var room = _scCheckHall.transform;
            _scCheckHall = null;
            room.SetParent(env, false);
            room.position = Vector3.zero;
            room.rotation = Quaternion.identity;

            // mood: the same bright dusty grey as the Scenario 6 factory
            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.80f, 0.82f, 0.84f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.66f, 0.67f, 0.68f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.34f, 0.34f, 0.35f);
            mso.FindProperty("fogColor").colorValue = new Color(0.72f, 0.74f, 0.74f);
            mso.FindProperty("fogStart").floatValue = 9f;
            mso.FindProperty("fogEnd").floatValue = 40f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.35f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // the copy is a static picture: no scripts (Scenario 6 logic must not run twice), no switched-off pieces of the old lab, no old probe
            for (var pass = 0; pass < 4; pass++)
                foreach (var mb in room.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null)
                        Object.DestroyImmediate(mb);
            var dead = new List<GameObject>();
            foreach (Transform child in room)
                if (!child.gameObject.activeSelf)
                    dead.Add(child.gameObject);
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
                if (t != room && (t.name == "MixtureJar" || t.name == "WaterJug" || t.name == "AcidCan" || t.name == "Sc06ReflectionProbe"
                    || t.name == "PosterSds" || t.name == "SdsPocket" || t.name == "ShadowItem"))
                    dead.Add(t.gameObject);
            foreach (var g in dead)
                if (g != null)
                    Object.DestroyImmediate(g);

            // move the copy to the Scenario 13-18 origin
            room.position = new Vector3(ScOx - Sc06Ox, 0f, 0f);

            // table top: centre and height, measured from the real table parts
            var tb = new Bounds();
            var has = false;
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "TableTopA" && r.name != "TableTopB")
                    continue;
                if (!has) { tb = r.bounds; has = true; }
                else tb.Encapsulate(r.bounds);
            }

            var topY = has ? tb.max.y : 0.92f;
            var cx = has ? tb.center.x : ScOx + 0.4f;
            var cz = has ? tb.center.z : 0.2f;
            Debug.Log("[ScCheck] factory table top " + topY.ToString("F3") + " at " + cx.ToString("F2") + ", " + cz.ToString("F2"));

            var chem = new GameObject("ScChemicals").transform;
            chem.SetParent(room, true);
            var props = new GameObject[6];
            for (var i = 0; i < 6; i++)
                props[i] = ScBuildChemical(13 + i, chem, new Vector3(cx - 0.15f, topY, cz), art);
            // the copied table spot light blows the steel top out to white and washes the labels: a gentler one
            foreach (var l in room.GetComponentsInChildren<Light>(true))
                if (l.name == "TableSpot")
                    l.intensity = 4.5f;

            // learner spot: the same as Scenarios 8 / 9 / 10 in this room
            var spawnPos = new Vector3(ScOx + 1.2f, 0f, 3.0f);
            // where the learner is taken after the instruction (close in front of the table) and the point above the chemical for the popups
            var tbMaxZ = has ? tb.max.z : 1.0f;
            var viewStand = Marker("ViewStand_SCCHECK", room, new Vector3(cx - 0.15f + 0.3f, 0f, tbMaxZ + 1.2f), 180f);
            var chemAnchor = Marker("ChemAnchor_SCCHECK", room, new Vector3(cx - 0.15f, topY + 0.46f, cz), 180f);
            var supStart = Marker("SupervisorStart", room, spawnPos, 0f);
            var supStand = Marker("SupervisorStand", room, spawnPos, 0f);
            var supSide = Marker("SupervisorSide", room, spawnPos, 0f);
            refs.spawn = Marker("Spawn_SCCHECK", room, spawnPos, 187f);
            refs.stand = Marker("Stand_SCCHECK", room, spawnPos, 187f);
            var host = new GameObject("ScCheckHost");
            host.transform.SetParent(room, false);
            host.transform.position = new Vector3(ScOx, 0f, 0f);
            refs.runner = host.AddComponent<ScCheckRunner>();
            var rso = new SerializedObject(refs.runner);
            rso.FindProperty("art").objectReferenceValue = art;
            rso.FindProperty("supStart").objectReferenceValue = supStart;
            rso.FindProperty("supStand").objectReferenceValue = supStand;
            rso.FindProperty("supSide").objectReferenceValue = supSide;
            rso.FindProperty("viewStand").objectReferenceValue = viewStand;
            rso.FindProperty("chemAnchor").objectReferenceValue = chemAnchor;
            var arr = rso.FindProperty("chemProps");
            arr.arraySize = props.Length;
            for (var i = 0; i < props.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = props[i];
            rso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }

        // ------------------------------------------------------------------ the chemicals

        private static GameObject ScBuildChemical(int n, Transform parent, Vector3 baseAt, PresentableArt art)
        {
            var g = new GameObject("Chem_SC" + n);
            g.transform.SetParent(parent, true);
            g.transform.position = baseAt;
            // the designer's own models of nitric acid (14), chlorine gas (15) and hydrogen peroxide (16, 17); a missing file falls back to the old props
            var designer = ScCheckUseDesignerModels && n >= 14 && n <= 17 && ScBuildDesignerChemical(n, g.transform, baseAt);
            if (designer)
            {
                // nothing else: the model is already standing on the table, label towards the learner
            }
            else if (n == 15)
                ScBuildCylinder(g.transform, baseAt);
            else if (n == 18)
                ScBuildSack(g.transform, baseAt);
            else
            {
                // the designer's HCl bottle (also the body of nitric acid and hydrogen peroxide when the designer models are switched off)
                var ok = Sc04HclModel(g.transform, baseAt.y, baseAt.x, baseAt.z);
                if (!ok)
                    Debug.LogWarning("[ScCheck] HCl bottle model missing");
                else if (n == 13 && ScCheckUseDesignerModels)
                    ScStickHclLabel(g.transform);
                else if (n != 13)
                    ScRelabel(g.transform, n);
                g.transform.localScale = Vector3.one * 1.3f; // about 0.42 m tall: all six chemicals read at the same, believable size
            }

            // scaling about the base centre keeps it standing on the table
            g.transform.position = baseAt;
            g.SetActive(false);
            return g;
        }

        // text on a label root: x = along the paper as the learner sees it, y = up; the root faces +Z (yaw 180), so the text sits at local -Z
        private static Transform ScLabelText(Transform plate, string text, float size, Color color, float x, float y, float width)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(plate, false);
            go.transform.localPosition = new Vector3(x, y, -0.0012f);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.05f;
            t.fontSizeMax = size * 0.1f;
            t.fontSize = size * 0.1f;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(width, size * 0.1f);
            return go.transform;
        }

        // a flat quad given in WORLD space (QuadObj takes local values), then put under parent keeping its world pose
        private static Transform ScQuadW(string name, Transform parent, Vector3 worldPos, float yaw, Vector2 size, Material mat, float roll = 0f)
        {
            var q = QuadObj(name, null, worldPos, new Vector3(0f, yaw, roll), size, mat);
            q.transform.SetParent(parent, true);
            return q.transform;
        }

        // a printed label: an empty root (unit scale, faces +Z towards the learner) holding the paper, a coloured band and a GHS diamond
        private static Transform ScPlate(string name, Transform parent, Vector3 worldCentre, Vector2 size, Color paperColor, Color bandColor, bool diamond)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = worldCentre;
            root.rotation = Quaternion.Euler(0f, 180f, 0f);
            var paper = Mat("ScCheckPaper_" + ColorUtility.ToHtmlStringRGB(paperColor), paperColor, 0.25f, 0f);
            var p = ScQuadW("Paper", root, worldCentre, 180f, size, paper);
            p.localPosition = Vector3.zero;
            var band = Mat("ScCheckBand_" + ColorUtility.ToHtmlStringRGB(bandColor), bandColor, 0.3f, 0f);
            var b = ScQuadW("Band", root, worldCentre, 180f, new Vector2(size.x, size.y * 0.2f), band);
            b.localPosition = new Vector3(0f, size.y * 0.4f, -0.0006f);
            if (diamond)
            {
                var d = Mathf.Min(size.x, size.y) * 0.26f;
                var red = Mat("ScCheckGhsRed", new Color(0.85f, 0.1f, 0.08f), 0.3f, 0f);
                var white = Mat("ScCheckGhsWhite", Color.white, 0.3f, 0f);
                var dx = -size.x * 0.28f;
                var dy = -size.y * 0.28f;
                var o = ScQuadW("GhsOuter", root, worldCentre, 180f, new Vector2(d, d), red, 45f);
                o.localPosition = new Vector3(dx, dy, -0.0006f);
                var inn = ScQuadW("GhsInner", root, worldCentre, 180f, new Vector2(d * 0.78f, d * 0.78f), white, 45f);
                inn.localPosition = new Vector3(dx, dy, -0.0009f);
                ScLabelText(root, "!", 1.4f, new Color(0.1f, 0.1f, 0.1f), dx, dy, d);
            }

            return root;
        }

        // a printed label plate covering the bottle's own HCl label (same place, same size, a little in front)
        private static void ScRelabel(Transform bottle, int n)
        {
            var have = false;
            var lb = new Bounds();
            foreach (var mf in bottle.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && mf.sharedMesh != null && Sc06SubBounds(mf, mr, "Logo", out var b))
                {
                    lb = b;
                    have = true;
                }
            }

            var bb = BoundsOfTree(bottle);
            var w = have ? lb.size.x : bb.size.x * 0.8f;
            var h = have ? lb.size.y : bb.size.y * 0.4f;
            var at = have ? lb.center : bb.center;
            var z = have ? lb.max.z : bb.max.z;
            Color band, ink = new Color(0.08f, 0.08f, 0.1f);
            string name, formula, note;
            if (n == 14) { name = "NITRIC ACID"; formula = "HNO3"; note = "CORROSIVE / OXIDIZER"; band = new Color(0.85f, 0.5f, 0.05f); }
            else { name = "HYDROGEN PEROXIDE"; formula = "H2O2"; note = "OXIDIZER / CORROSIVE"; band = new Color(0.1f, 0.35f, 0.75f); }
            var plate = ScPlate("ScChemLabel", bottle, new Vector3(at.x, at.y, z + 0.003f), new Vector2(w * 1.06f, h * 1.06f), new Color(0.97f, 0.97f, 0.95f), band, true);
            ScLabelText(plate, name, 0.55f, ink, 0f, h * 0.12f, w);
            ScLabelText(plate, formula, 0.9f, band * 0.8f, w * 0.12f, -h * 0.1f, w * 0.5f);
            ScLabelText(plate, note, 0.32f, ink, w * 0.08f, -h * 0.4f, w * 0.9f);
        }

        // ------------------------------------------------------------------ designer models and glued-on labels (2026-11-12)

        // a printed label texture: cut-out edge, clamped, mip-mapped, sharp at an angle
        private static Texture2D ScLabelTexture(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp || !ti.mipmapEnabled || ti.maxTextureSize != 1024))
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 8;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // A sticker that follows the surface: a grid of vertices, each one on the real surface of the body (ray from the front against the mesh,
        // temporary mesh colliders), lifted 1 mm. The picture is shown as printed on the object, never as a flat card in front of it.
        private static GameObject ScConformSticker(Transform parent, List<MeshFilter> bodies, Texture2D tex, string matName, float lw, float cx, float cy,
            float frontZ, string objName)
        {
            // the picture's true proportions (an imported texture may be stretched to a power of two)
            var aspect = (float)tex.height / tex.width;
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter si)
            {
                si.GetSourceTextureWidthAndHeight(out var sw, out var sh);
                if (sw > 0 && sh > 0)
                    aspect = (float)sh / sw;
            }

            var lh = lw * aspect;
            const int cols = 28;
            const int rows = 14;
            var added = new List<MeshCollider>();
            foreach (var mf in bodies)
            {
                if (mf == null || mf.sharedMesh == null || mf.GetComponent<MeshCollider>() != null)
                    continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                added.Add(mc);
            }

            Physics.SyncTransforms();
            var verts = new Vector3[(cols + 1) * (rows + 1)];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var hits = 0;
            for (var j = 0; j <= rows; j++)
            {
                var y = cy - lh * 0.5f + lh * j / rows;
                for (var i = 0; i <= cols; i++)
                {
                    var x = cx - lw * 0.5f + lw * i / cols; // x grows with i; the picture's left is +X (seen from the front), so u falls with i
                    var p = new Vector3(x, y, frontZ);
                    var nrm = Vector3.forward;
                    var best = float.MaxValue;
                    foreach (var h in Physics.RaycastAll(new Vector3(x, y, frontZ + 0.25f), Vector3.back, 0.7f))
                    {
                        var mc = h.collider as MeshCollider;
                        if (mc != null && added.Contains(mc) && h.distance < best)
                        {
                            best = h.distance;
                            p = h.point;
                            nrm = h.normal;
                        }
                    }

                    if (best < float.MaxValue)
                        hits++;
                    var idx = j * (cols + 1) + i;
                    verts[idx] = p + nrm * 0.0012f;
                    norms[idx] = nrm;
                    uvs[idx] = new Vector2(1f - i / (float)cols, j / (float)rows);
                }
            }

            foreach (var mc in added)
                Object.DestroyImmediate(mc);
            var tris = new List<int>();
            for (var j = 0; j < rows; j++)
            {
                for (var i = 0; i < cols; i++)
                {
                    var v00 = j * (cols + 1) + i;
                    var v10 = v00 + 1;
                    var v01 = v00 + (cols + 1);
                    var v11 = v01 + 1;
                    tris.AddRange(new[] { v00, v10, v01, v01, v10, v11 });
                }
            }

            var mesh = new Mesh { name = objName + "Mesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var mat = Mat(matName, Color.white, 0.25f, 0f, tex, null, false, true);
            mat.SetFloat("_Cull", 2f);
            var go = new GameObject(objName);
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Debug.Log("[ScCheck] " + objName + ": " + hits + " of " + verts.Length + " vertices lie on the surface, size " + lw.ToString("0.000") + " x " + lh.ToString("0.000"));
            if (hits < verts.Length / 2)
                Debug.LogWarning("[ScCheck] " + objName + ": most of the label found no surface to sit on");
            return go;
        }

        // Scenario 13: the designer's HCl bottle keeps its body, cap and water; its own printed label (the "Logo" sub-mesh) is replaced by the client's
        // 05_hydrochloric_acid label, glued onto the glass. (Scenario 4 uses the same bottle through its own call and is not touched.)
        private static void ScStickHclLabel(Transform bottle)
        {
            var tex = ScLabelTexture("Assets/Resources/ScCheck/HclLabel.png");
            if (tex == null)
                return;
            var has = false;
            var lb = new Bounds();
            var k = 0;
            foreach (var mf in bottle.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && mf.sharedMesh != null && Sc06SubBounds(mf, mr, "Logo", out var b))
                {
                    lb = b;
                    has = true;
                    Sc06DropSubmeshes(mf, mr, "ScChemHclNoLogo" + k++, "Logo");
                }
            }

            var wb = BoundsOfTree(bottle);
            var bodies = new List<MeshFilter>();
            foreach (var mf in bottle.GetComponentsInChildren<MeshFilter>(false))
            {
                if (Sc06BodyVertex(mf))
                    bodies.Add(mf);
            }

            var lw = Mathf.Min(wb.size.x * 0.74f, 0.2f);
            var cy = has ? lb.center.y : wb.min.y + wb.size.y * 0.45f;
            ScConformSticker(bottle, bodies, tex, "ScCheckHclLabelPaper", lw, wb.center.x, cy, wb.max.z, "HclLabelNew");
        }

        // SC14 / 15 / 16 / 17: the designer's glb. Stray parts are removed, the model stands on the table with its label towards the learner (+Z).
        private static bool ScBuildDesignerChemical(int n, Transform g, Vector3 baseAt)
        {
            string file, labelMat;
            float height;
            string[] drop;
            if (n == 14) { file = "NitricAcid.glb"; labelMat = "Material.005"; height = 0.42f; drop = new[] { "pTorus3.001" }; }
            else if (n == 15) { file = "ChlorineGas.glb"; labelMat = "plastic01"; height = 0.5f; drop = new string[0]; }
            else { file = "HydrogenPeroxide.glb"; labelMat = "Material.004"; height = 0.44f; drop = new[] { "pTorus3.001" }; }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/ScCheck/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[ScCheck] designer model missing: " + file);
                return false;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Designer_" + System.IO.Path.GetFileNameWithoutExtension(file);
            go.transform.SetParent(g, false);
            Sc04NoColliders(go);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t != go.transform && System.Array.IndexOf(drop, t.name) >= 0)
                    Object.DestroyImmediate(t.gameObject);
            }

            // turn the label towards the learner: it is the sub-mesh with the glb material labelMat
            var b = BoundsOfTree(go.transform);
            var has = false;
            var labelAt = Vector3.zero;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && mf.sharedMesh != null && Sc06SubBounds(mf, mr, labelMat, out var lb))
                {
                    labelAt = lb.center;
                    has = true;
                }
            }

            if (has)
            {
                var dir = new Vector3(labelAt.x - b.center.x, 0f, labelAt.z - b.center.z);
                if (dir.sqrMagnitude > 1e-6f)
                    go.transform.RotateAround(b.center, Vector3.up, Vector3.SignedAngle(dir, Vector3.forward, Vector3.up));
            }

            b = BoundsOfTree(go.transform);
            if (b.size.y > 0.01f)
                go.transform.localScale *= height / b.size.y;
            b = BoundsOfTree(go.transform);
            go.transform.position += new Vector3(baseAt.x - b.center.x, baseAt.y - b.min.y, baseAt.z - b.center.z);
            // the chlorine foot ring carries a "Metal rust" texture that shows as a red / black swirl: a plain dark steel instead
            var foot = Mat("ScCheckCylFoot", new Color(0.1f, 0.2f, 0.12f), 0.45f, 0.5f);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ms = r.sharedMaterials;
                var swap = false;
                for (var k = 0; k < ms.Length; k++)
                {
                    if (n == 15 && ms[k] != null && ms[k].name == "Metal rust")
                    {
                        ms[k] = foot;
                        swap = true;
                    }
                }

                if (swap)
                    r.sharedMaterials = ms;
            }

            b = BoundsOfTree(go.transform);
            // contact shadow (QuadObj takes LOCAL values, so it is made without a parent and then put under g keeping its world pose)
            var blobTex = ScTex("ScCheck_Blob");
            var blobMat = MatAlpha("ScCheckBlob", blobTex, new Color(0f, 0f, 0f, 0.5f), false);
            var blob = QuadObj("Blob", null, new Vector3(b.center.x, baseAt.y + 0.004f, b.center.z), new Vector3(90f, 0f, 0f),
                new Vector2(b.size.x * 1.35f, b.size.z * 1.35f), blobMat);
            blob.transform.SetParent(g, true);
            Debug.Log("[ScCheck] SC" + n + " designer model " + file + " size " + b.size.ToString("F3") + ", label found = " + has);
            return true;
        }

        // chlorine: a green steel gas cylinder with a valve, tag plate and a yellow band
        private static void ScBuildCylinder(Transform g, Vector3 baseAt)
        {
            var steel = Mat("ScCheckCylSteel", new Color(0.16f, 0.38f, 0.22f), 0.5f, 0.12f);
            var yellow = Mat("ScCheckCylBand", new Color(0.92f, 0.78f, 0.1f), 0.4f, 0.05f);
            var brass = Mat("ScCheckCylBrass", new Color(0.78f, 0.6f, 0.2f), 0.55f, 0.7f);
            var dark = Mat("ScCheckCylDark", new Color(0.12f, 0.13f, 0.14f), 0.4f, 0.3f);
            var body = ScLathe("ScChemCylBody", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.085f, 0f), new Vector2(0.105f, 0.012f), new Vector2(0.11f, 0.05f), new Vector2(0.11f, 0.42f),
                new Vector2(0.095f, 0.5f), new Vector2(0.06f, 0.55f), new Vector2(0.035f, 0.565f), new Vector2(0.035f, 0.58f), new Vector2(0f, 0.58f)
            }, 36);
            ScMeshObj("CylBody", g, body, steel, baseAt, Vector3.zero, false);
            var band = ScLathe("ScChemCylBand", new[] { new Vector2(0.1105f, 0.36f), new Vector2(0.1115f, 0.36f), new Vector2(0.1115f, 0.40f), new Vector2(0.1105f, 0.40f) }, 36);
            ScMeshObj("CylBand", g, band, yellow, baseAt, Vector3.zero, false);
            var valve = ScLathe("ScChemValve", new[] { new Vector2(0f, 0.58f), new Vector2(0.03f, 0.58f), new Vector2(0.03f, 0.64f), new Vector2(0.018f, 0.66f), new Vector2(0f, 0.66f) }, 18);
            ScMeshObj("CylValve", g, valve, brass, baseAt, Vector3.zero, false);
            ScMeshObj("CylWheel", g, ScLathe("ScChemWheel", new[] { new Vector2(0.0f, 0.66f), new Vector2(0.05f, 0.66f), new Vector2(0.05f, 0.67f), new Vector2(0f, 0.67f) }, 18), dark, baseAt, Vector3.zero, false);
            // the tag plate stands flat in front of the curved body
            var plate = ScPlate("CylTag", g, baseAt + new Vector3(0f, 0.25f, 0.118f), new Vector2(0.15f, 0.17f), Color.white, new Color(0.15f, 0.55f, 0.2f), true);
            ScLabelText(plate, "CHLORINE", 0.6f, new Color(0.08f, 0.35f, 0.1f), 0f, 0.012f, 0.15f);
            ScLabelText(plate, "Cl2", 1.0f, new Color(0.08f, 0.08f, 0.1f), 0.03f, -0.03f, 0.07f);
            g.localScale = Vector3.one * 0.68f;
        }

        // bleaching powder: a white plastic sack with a flat printed front
        private static void ScBuildSack(Transform g, Vector3 baseAt)
        {
            var sack = Mat("ScCheckSack", new Color(0.88f, 0.89f, 0.86f), 0.25f, 0f);
            var body = ScRoundedBox("ScChemSack", new Vector3(0.22f, 0.32f, 0.12f), 0.03f);
            var sackGo = ScMeshObj("Sack", g, body, sack, baseAt + new Vector3(0f, 0.16f, 0f), Vector3.zero, false);
            var top = ScRoundedBox("ScChemSackTop", new Vector3(0.23f, 0.03f, 0.06f), 0.012f);
            ScMeshObj("SackTop", g, top, sack, baseAt + new Vector3(0f, 0.335f, 0f), Vector3.zero, false);
            // the client's printed label (BleachingLabel.png), glued onto the sack: every vertex lies on the sack's real surface, so it hugs the rounded front
            var labelTex = ScCheckUseDesignerModels ? ScLabelTexture("Assets/Resources/ScCheck/BleachingLabel.png") : null;
            if (labelTex != null)
            {
                ScConformSticker(g, new List<MeshFilter> { sackGo.GetComponent<MeshFilter>() }, labelTex, "ScCheckBleachLabelPaper", 0.176f,
                    baseAt.x, baseAt.y + 0.17f, baseAt.z + 0.06f, "SackLabel");
                g.localScale = Vector3.one * 1.15f;
                return;
            }

            var plate = ScPlate("SackLabel", g, baseAt + new Vector3(0f, 0.17f, 0.0615f), new Vector2(0.19f, 0.23f), Color.white, new Color(0.85f, 0.15f, 0.1f), true);
            ScLabelText(plate, "BLEACHING POWDER", 0.5f, new Color(0.08f, 0.08f, 0.1f), 0f, 0.03f, 0.18f);
            ScLabelText(plate, "Ca(ClO)2", 0.7f, new Color(0.7f, 0.1f, 0.1f), 0.03f, -0.01f, 0.12f);
            g.localScale = Vector3.one * 1.15f;
        }
    }
}
#endif
