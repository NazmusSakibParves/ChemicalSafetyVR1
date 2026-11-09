#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 12 on the controller ray: the client's PPE hall (reference picture). ONE switch, <see cref="Sc12UseRay"/>:
    /// true  = aged off-white walls, worn concrete floor with a yellow safety line, stained concrete ceiling with beams, a plain right wall
    ///         with the "wear PPE" poster, the SDS board and hazard signs, an open section in the left wall onto the plant whose machines are
    ///         light grey / blue-white, the yellow mesh-door PPE cabinet; the flow runs on the ray with automatic rounds.
    /// false = the previous finger-touch Scenario 12 (window on the right, wooden lockers). Set false + Build Presentable Scene.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc12UseRay = true;

        // left wall opening (z range) and height
        private const float Sc12OpenZ0 = -3.7f, Sc12OpenZ1 = -0.9f, Sc12OpenH = 2.5f;

        private static Texture2D Sc12EnvTex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/2D/Env/" + n + ".png");

        // left wall with a wide opening, a steel frame and a half-closed sliding door; right wall plain
        private static void Sc12HallWalls(Transform room, float ox, float midZ, float len, Material wallMatSide, Material steelMat)
        {
            var lx = ox + Sc12HalfW + 0.1f;
            var zf = Sc12FarZ;
            Sc12Wall("LeftWall_Far", room, new Vector3(lx, Sc12H * 0.5f, (zf + Sc12OpenZ0) * 0.5f), new Vector3(0.2f, Sc12H, Sc12OpenZ0 - zf), wallMatSide);
            Sc12Wall("LeftWall_Near", room, new Vector3(lx, Sc12H * 0.5f, (Sc12OpenZ1 + Sc12BackZ) * 0.5f), new Vector3(0.2f, Sc12H, Sc12BackZ - Sc12OpenZ1), wallMatSide);
            Sc12Wall("LeftWall_Above", room, new Vector3(lx, (Sc12OpenH + Sc12H) * 0.5f, (Sc12OpenZ0 + Sc12OpenZ1) * 0.5f),
                new Vector3(0.2f, Sc12H - Sc12OpenH, Sc12OpenZ1 - Sc12OpenZ0), wallMatSide);
            // the right wall: whitish plaster, old (stains and streaks are added in Sc12HallDress), a warm light lifts it
            var rightMat = Sc12Tiled("Sc12WallRightMat", Sc12Tex("Sc12_Wall"), new Color(1.55f, 1.52f, 1.42f), 0.08f, 0f, new Vector2(4.4f, 1.5f),
                Sc12Tex("Sc12_Stucco_Normal"));
            Sc12Wall("RightWall", room, new Vector3(ox - Sc12HalfW - 0.1f, Sc12H * 0.5f, midZ), new Vector3(0.2f, Sc12H, len), rightMat);
            var rl = new GameObject("RightWallLight").AddComponent<Light>();
            rl.transform.SetParent(room, true);
            rl.transform.position = new Vector3(ox - 1.0f, 2.3f, -0.8f);
            rl.type = LightType.Point;
            rl.color = new Color(1f, 0.96f, 0.88f);
            rl.intensity = 3.2f;
            rl.range = 6f;
            rl.shadows = LightShadows.None;

            // steel door frame
            var fx = ox + Sc12HalfW - 0.04f;
            var cz = (Sc12OpenZ0 + Sc12OpenZ1) * 0.5f;
            Box("DoorFrameTop", room, new Vector3(fx, Sc12OpenH + 0.05f, cz), new Vector3(0.22f, 0.12f, Sc12OpenZ1 - Sc12OpenZ0 + 0.2f), steelMat, false);
            Box("DoorFrameFar", room, new Vector3(fx, Sc12OpenH * 0.5f, Sc12OpenZ0 - 0.05f), new Vector3(0.22f, Sc12OpenH, 0.12f), steelMat, false);
            Box("DoorFrameNear", room, new Vector3(fx, Sc12OpenH * 0.5f, Sc12OpenZ1 + 0.05f), new Vector3(0.22f, Sc12OpenH, 0.12f), steelMat, false);
            // sliding door leaf pushed half open to the near side, with rail and handles (as in the picture)
            var door = Mat("Sc12DoorLeaf", new Color(0.52f, 0.58f, 0.57f), 0.35f, 0.4f);
            var dark = Mat("Sc12DoorDark", new Color(0.20f, 0.22f, 0.23f), 0.4f, 0.6f);
            Box("DoorRail", room, new Vector3(fx - 0.12f, Sc12OpenH + 0.2f, cz - 0.4f), new Vector3(0.06f, 0.06f, 3.6f), dark, false);
            Box("DoorLeaf", room, new Vector3(fx - 0.16f, Sc12OpenH * 0.5f, Sc12OpenZ1 - 0.5f), new Vector3(0.07f, Sc12OpenH - 0.05f, 1.0f), door, false);
            Box("DoorHandle", room, new Vector3(fx - 0.24f, 1.05f, Sc12OpenZ1 - 0.95f), new Vector3(0.05f, 0.5f, 0.05f), dark, false);
            foreach (var n in new[] { "LeftWall_Far", "LeftWall_Near", "LeftWall_Above", "RightWall" })
                if (SolidLayer >= 0)
                    room.Find(n).gameObject.layer = SolidLayer;
        }

        // aged look: stains, grime, rust, the yellow safety line, ceiling beams; right wall poster / SDS board / signs
        private static void Sc12HallDress(Transform room, float ox, float midZ, Material steelMat)
        {
            var oil = MatAlpha("Sc12StainOil", Sc12EnvTex("STAIN_Oil"), new Color(0.10f, 0.09f, 0.08f, 0.42f), false);
            var rust = MatAlpha("Sc12StainRust", Sc12EnvTex("STAIN_Rust"), new Color(0.45f, 0.26f, 0.14f, 0.45f), false);
            var rs = new System.Random(12);
            // floor: oil blots, rust, a few wet patches
            for (var i = 0; i < 60; i++)
                QuadObj("FloorBlot" + i, room, new Vector3(ox - 2.2f + (float)rs.NextDouble() * 4.4f, 0.004f + i * 0.0002f, Sc12FarZ + 0.6f + (float)rs.NextDouble() * 8f),
                    new Vector3(90f, (float)rs.NextDouble() * 360f, 0f), Vector2.one * (0.5f + (float)rs.NextDouble() * 1.4f), i % 3 == 2 ? rust : oil);
            var blotTex = Sc03Tex("Stain_Blot");
            if (blotTex != null)
            {
                var smudge = MatAlpha("Sc12WallSmudge", blotTex, new Color(0.36f, 0.32f, 0.26f, 0.30f), false);
                for (var i = 0; i < 44; i++)
                {
                    var y = 0.3f + (float)rs.NextDouble() * 2.5f;
                    var sz = 0.7f + (float)rs.NextDouble() * 1.4f;
                    var z = Sc12FarZ + 0.5f + (float)rs.NextDouble() * (Sc12BackZ - Sc12FarZ - 1f);
                    if (i % 3 == 0)
                        QuadObj("SmudgeFar" + i, room, new Vector3(ox - 2.2f + (float)rs.NextDouble() * 4.4f, y, Sc12FarZ + 0.012f), new Vector3(0f, 180f, (float)rs.NextDouble() * 360f), Vector2.one * sz, smudge);
                    else if (i % 3 == 1)
                        QuadObj("SmudgeR" + i, room, new Vector3(ox - Sc12HalfW + 0.012f, y, z), new Vector3(0f, -90f, (float)rs.NextDouble() * 360f), Vector2.one * sz, smudge);
                    else if (z < Sc12OpenZ0 || z > Sc12OpenZ1 + 0.4f || y > Sc12OpenH)
                        QuadObj("SmudgeL" + i, room, new Vector3(ox + Sc12HalfW - 0.012f, y, z), new Vector3(0f, 90f, (float)rs.NextDouble() * 360f), Vector2.one * sz, smudge);
                }
            }

            var grimeTex = Sc03Tex("Grime_Drip");
            if (grimeTex != null)
            {
                var drip = MatAlpha("Sc12WallDrip", grimeTex, new Color(0.30f, 0.26f, 0.21f, 0.38f), false);
                for (var i = 0; i < 5; i++)
                    QuadObj("WallDripFar" + i, room, new Vector3(ox - 2.0f + i * 1.0f, 2.1f, Sc12FarZ + 0.013f), new Vector3(0f, 180f, 0f), new Vector2(1.4f, 2.2f), drip);
                for (var i = 0; i < 4; i++)
                    QuadObj("WallDripR" + i, room, new Vector3(ox - Sc12HalfW + 0.013f, 2.0f, -3.2f + i * 2.2f), new Vector3(0f, -90f, 0f), new Vector2(1.4f, 2.2f), drip);
            }

            // the right wall is the oldest: long rust streaks from the top, yellowed patches, grime at the foot of the wall
            var rightStreak = MatAlpha("Sc12RightRustStreak", Sc12EnvTex("STAIN_Rust"), new Color(0.50f, 0.32f, 0.18f, 0.40f), false);
            for (var i = 0; i < 9; i++)
                QuadObj("RightStreak" + i, room, new Vector3(ox - Sc12HalfW + 0.014f, 1.5f + (float)rs.NextDouble() * 0.9f, Sc12FarZ + 0.6f + i * 0.95f),
                    new Vector3(0f, -90f, 0f), new Vector2(0.5f + (float)rs.NextDouble() * 0.4f, 1.6f + (float)rs.NextDouble() * 1.0f), rightStreak);
            if (blotTex != null)
            {
                var yellowed = MatAlpha("Sc12RightYellowed", blotTex, new Color(0.62f, 0.52f, 0.30f, 0.30f), false);
                for (var i = 0; i < 12; i++)
                    QuadObj("RightYellowed" + i, room, new Vector3(ox - Sc12HalfW + 0.015f, 0.5f + (float)rs.NextDouble() * 2.2f, Sc12FarZ + 0.4f + i * 0.72f),
                        new Vector3(0f, -90f, (float)rs.NextDouble() * 360f), Vector2.one * (1.0f + (float)rs.NextDouble() * 1.3f), yellowed);
                var foot = MatAlpha("Sc12RightFootGrime", blotTex, new Color(0.18f, 0.16f, 0.13f, 0.45f), false);
                for (var i = 0; i < 8; i++)
                    QuadObj("RightFoot" + i, room, new Vector3(ox - Sc12HalfW + 0.016f, 0.2f, Sc12FarZ + 0.5f + i * 1.05f),
                        new Vector3(0f, -90f, (float)rs.NextDouble() * 360f), new Vector2(1.5f, 0.7f), foot);
            }

            // yellow safety line in front of the cabinet and along the corridor, a worn painted step edge
            var line = Mat("Sc12FloorLine", new Color(0.85f, 0.66f, 0.06f), 0.2f, 0f);
            Box("FloorLineFront", room, new Vector3(ox, 0.006f, Sc12FarZ + 0.95f), new Vector3(3.5f, 0.012f, 0.12f), line, false);
            Box("FloorLineLeft", room, new Vector3(ox + 1.0f, 0.006f, midZ + 1.5f), new Vector3(0.12f, 0.012f, 6.8f), line, false);

            // ceiling beams and a duct (concrete, dark)
            var beam = Mat("Sc12CeilBeam", new Color(0.42f, 0.42f, 0.41f), 0.1f, 0f);
            for (var z = -3.2f; z < Sc12BackZ; z += 2.2f)
                Box("CeilBeam", room, new Vector3(ox, Sc12H - 0.12f, z), new Vector3(Sc12HalfW * 2f, 0.24f, 0.22f), beam, false);
            Box("CeilDuct", room, new Vector3(ox - 1.5f, Sc12H - 0.3f, midZ), new Vector3(0.5f, 0.3f, Sc12BackZ - Sc12FarZ - 0.4f), Mat("Sc12CeilDuct", new Color(0.55f, 0.57f, 0.58f), 0.3f, 0.5f), false);

            // ---- right wall (x = ox - 2.5, faces +X, so a quad turns -90): the blue "wear PPE" poster, hazard signs, red emergency box, SDS board
            var rx = ox - Sc12HalfW;
            var signMat = Mat("Sc12SignMat", new Color(1.1f, 1.1f, 1.1f), 0.4f, 0.1f, Sc12Art("Sc12_Sign"));
            var frame = Mat("Sc12SignFrame", new Color(0.78f, 0.80f, 0.82f), 0.45f, 0.6f);
            // frames and prints stand in front of the stain layers (0.012 .. 0.018 off the wall) so nothing dirty is drawn over them
            Box("SignFrame", room, new Vector3(rx + 0.005f, 2.0f, -2.5f), new Vector3(0.05f, 0.84f, 2.34f), frame, false);
            Sc12Poster("Sign_DonPpe", room, new Vector3(rx + 0.034f, 2.0f, -2.5f), -90f, new Vector2(2.25f, 0.75f), signMat);
            var boardMat = Mat("Sc12SdsBoardMat", Color.white, 0.4f, 0.05f, Sc12Art("Sc12_SdsBoard"));
            var darkFrame = Mat("Sc12SdsFrame", new Color(0.16f, 0.14f, 0.13f), 0.4f, 0.3f);
            Box("SdsFrame", room, new Vector3(rx + 0.008f, 1.25f, -0.55f), new Vector3(0.06f, 1.28f, 1.07f), darkFrame, false);
            Sc12Poster("SdsBoard", room, new Vector3(rx + 0.04f, 1.25f, -0.55f), -90f, new Vector2(0.98f, 1.19f), boardMat);
            var hazard = Mat("Sc12HazardYellow", new Color(0.95f, 0.75f, 0.05f), 0.3f, 0f);
            var red = Mat("Sc12EmergencyRed", new Color(0.78f, 0.08f, 0.06f), 0.4f, 0.1f);
            var white = Mat("Sc12SignWhite", new Color(0.92f, 0.92f, 0.9f), 0.3f, 0f);
            Box("HazardSign1", room, new Vector3(rx + 0.01f, 1.75f, 1.55f), new Vector3(0.02f, 0.34f, 0.28f), hazard, false);
            Box("HazardSign2", room, new Vector3(rx + 0.01f, 1.30f, 1.55f), new Vector3(0.02f, 0.34f, 0.28f), hazard, false);
            Box("EmergencyCall", room, new Vector3(rx + 0.01f, 1.0f, 1.95f), new Vector3(0.02f, 0.30f, 0.20f), white, false);
            Box("EmergencySign", room, new Vector3(rx + 0.01f, 1.75f, 1.95f), new Vector3(0.02f, 0.34f, 0.22f), red, false);
            Box("EmergencyBox", room, new Vector3(rx + 0.08f, 1.30f, 2.6f), new Vector3(0.16f, 0.45f, 0.30f), red, false);
            Box("EmergencyBoxPlate", room, new Vector3(rx + 0.165f, 1.30f, 2.6f), new Vector3(0.01f, 0.30f, 0.20f), white, false);
        }

        // plant outside the opening: mirrored to the +X side and recoloured like the picture's machines (light grey / blue-white, blue pipes)
        private static void Sc12HallPlant(Transform room, float ox)
        {
            var plant = room.Find("Plant");
            if (plant == null)
                return;
            // a half turn about the vertical line (ox, -8): x -> 2 ox - x, z -> -16 - z, so nothing is mirrored (no reversed text)
            plant.SetPositionAndRotation(new Vector3(2f * ox, 0f, -16f), Quaternion.Euler(0f, 180f, 0f));

            // Real-machine finish: the designer's textures stay (brushed stainless, painted shell, pipes, panel print), lifted so they do not
            // read black: brushed stainless with a real metal sheen, an off-white painted shell, matt dark-grey pump bodies, blue wall pipes.
            // The green / yellow / red lamp materials keep their own colour.
            foreach (var r in plant.GetComponentsInChildren<Renderer>(true))
            {
                var root = r.transform;
                while (root.parent != null && root.parent != plant)
                    root = root.parent;
                var part = root.name;
                if (!(part.StartsWith("Tank") || part.StartsWith("ControlBox") || part.StartsWith("PipeWall") || part.StartsWith("ControlPanel")))
                    continue;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null)
                        continue;
                    var n = src.name;
                    if (n == "green" || n == "yellow" || n == "material_10")
                        continue;
                    Texture tex = null;
                    foreach (var prop in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                    {
                        if (src.HasProperty(prop))
                        {
                            tex = src.GetTexture(prop);
                            if (tex != null)
                                break;
                        }
                    }

                    var key = "Sc12Mach_" + part.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9') + "_" + n.Replace(' ', '_').Replace('.', '_');
                    Color col;
                    float smooth, metal;
                    if (part.StartsWith("PipeWall"))
                    {
                        col = new Color(0.45f, 0.66f, 1.15f); // blue process pipes, the texture's shading kept
                        smooth = 0.55f;
                        metal = 0.15f;
                    }
                    else if (n.StartsWith("Stainless") || n == "metal")
                    {
                        col = new Color(1.30f, 1.32f, 1.36f); // brushed stainless
                        smooth = 0.58f;
                        metal = 0.55f;
                    }
                    else if (n == "Gvde")
                    {
                        col = new Color(1.30f, 1.32f, 1.30f); // painted shell, off-white
                        smooth = 0.40f;
                        metal = 0.12f;
                    }
                    else if (n == "PipeEngine_M2")
                    {
                        col = new Color(0.70f, 0.74f, 0.80f); // motor / pump housing, matt blue-grey
                        smooth = 0.30f;
                        metal = 0.05f;
                    }
                    else if (n == "Pano_yazs")
                    {
                        col = Color.white; // the printed panel face stays as drawn
                        smooth = 0.30f;
                        metal = 0f;
                    }
                    else
                    {
                        col = new Color(1.15f, 1.17f, 1.2f);
                        smooth = 0.45f;
                        metal = 0.30f;
                    }

                    mats[i] = Mat(key, col, smooth, metal, tex);
                }

                r.sharedMaterials = mats;
            }
        }

        // the yellow cabinet: frame, shelves, back and the two open mesh doors, in place of the wooden lockers
        private static void Sc12HallCabinetDoors(Transform room, float ox, float front)
        {
            var yellow = Mat("Sc12CabYellow", new Color(0.92f, 0.70f, 0.05f), 0.35f, 0.25f);
            var mesh = Mat("Sc12CabMesh", new Color(0.74f, 0.56f, 0.04f), 0.3f, 0.5f);
            for (var s = -1; s <= 1; s += 2)
            {
                var hinge = new GameObject(s > 0 ? "CabDoor_L" : "CabDoor_R").transform;
                hinge.SetParent(room, false);
                hinge.position = new Vector3(ox + s * 1.58f, 0f, front + 0.02f);
                hinge.rotation = Quaternion.Euler(0f, 100f * s, 0f);
                const float w = 0.78f, h = 1.95f;
                // frame (local x runs from the hinge over the cabinet, so the panel sits at -s * w/2 in local space of an un-rotated hinge)
                var cx = -s * w * 0.5f;
                void Part(string n, Vector3 c, Vector3 size, Material m)
                {
                    var b = Box(n, hinge, hinge.TransformPoint(c), size, m, false);
                    b.transform.SetParent(hinge, true);
                    b.transform.localRotation = Quaternion.identity;
                    b.transform.localPosition = c;
                    b.transform.localScale = size;
                }

                Part("FrameTop", new Vector3(cx, h, 0f), new Vector3(w, 0.05f, 0.04f), yellow);
                Part("FrameBottom", new Vector3(cx, 0.08f, 0f), new Vector3(w, 0.05f, 0.04f), yellow);
                Part("FrameIn", new Vector3(-s * 0.02f, h * 0.5f, 0f), new Vector3(0.05f, h, 0.04f), yellow);
                Part("FrameOut", new Vector3(-s * (w - 0.02f), h * 0.5f, 0f), new Vector3(0.05f, h, 0.04f), yellow);
                for (var k = 1; k < 10; k++)
                    Part("MeshV" + k, new Vector3(-s * w * k / 10f, h * 0.5f, 0f), new Vector3(0.012f, h - 0.1f, 0.012f), mesh);
                for (var k = 1; k < 24; k++)
                    Part("MeshH" + k, new Vector3(cx, 0.08f + (h - 0.08f) * k / 24f, 0.004f), new Vector3(w - 0.06f, 0.012f, 0.012f), mesh);
            }
        }
    }
}
#endif
