#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 21: a supervisor orders a chemical coating job in a poorly ventilated room, there is no JHA, the worker refuses, the two prepare the JHA,
    /// safe work starts. The film is the client animator's own work (Sc_21_v4.blend, exported by Blender to Assets/Art/Presentable/3D/Sc21/Sc21Film.glb, baked
    /// to Resources/Sc21/Sc21FilmBake.bytes). The blend has no room (the video plays in an empty dark box), so the room is built here after the client's
    /// reference picture BG.jpg: grimy concrete walls, brick pier at the corner, notice board, one fluorescent tube, dark stained floor, a doorway onto the
    /// factory hall. The wall, the brick, the board and the hall behind the doorway are cut from that photo (Tools/generate_sc21_textures.py). What the
    /// last workflow picture (scene06.jpg) adds for the safe-work step (extraction arm, shelf with bottles, green floor markings, safety shower sign) appears
    /// at the last shot. Nothing of the other scenarios is touched. <c>Sc21Enabled = false</c> + Build Presentable Scene leaves the scenario out.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool Sc21Enabled = true;
        private const float Sc21Ox = 700f;       // the room stands far from every other scenario (x = 700)
        private const float Sc21Scale = 0.5f;    // the animator's characters are ~3.5 units tall in Blender
        private const string Sc21Dir = ArtRoot + "/3D/Sc21";

        // room box (metres, relative to the room origin): x -4.5 .. 4.5, z -3.0 .. 5.6, height 3.0
        private const float Sc21X0 = -4.5f, Sc21X1 = 4.5f, Sc21Zb = -3.0f, Sc21Zf = 5.6f, Sc21H = 3.0f;
        private const float Sc21DoorX0 = 1.2f, Sc21DoorH = 2.55f;

        private sealed class Sc21Refs
        {
            public Transform room;
            public Sc06Gate gate;
            public Sc21Cinematic cine;
            public Transform viewing;
        }

        private static Texture2D Sc21Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Sc21Dir + "/Tex/" + n + ".png");

        // the room is mirrored in X: the film camera looks along -Z, so +X is on the left of the picture and the client's picture (BG.jpg) has the
        // notice board on the left, the doorway on the right
        private static Vector3 Sc21P(float x, float y, float z) => new Vector3(Sc21Ox - x, y, z);

        private static Sc21Refs BuildSc21Room(Transform env, PresentableArt art)
        {
            var refs = new Sc21Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            refs.room = room;
            if (!Sc21Enabled)
                return refs;
            Sc21TextureSettings();

            // dim indoor mood: one fluorescent tube, grey haze (the shared lighting is bright)
            var mood = env.gameObject.AddComponent<Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.51f, 0.52f, 0.53f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.44f, 0.44f, 0.44f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.22f, 0.22f, 0.22f);
            mso.FindProperty("fogColor").colorValue = new Color(0.30f, 0.32f, 0.33f);
            mso.FindProperty("fogStart").floatValue = 7f;
            mso.FindProperty("fogEnd").floatValue = 42f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.25f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ---- materials (the client's reference picture: sage-grey grimy concrete, dark floor, rust-brown brick)
            var wallBack = Mat("Sc21WallBack", new Color(1.26f, 1.26f, 1.25f), 0.08f, 0f, Sc21Tex("Sc21_WallAged"));
            wallBack.SetTextureScale("_BaseMap", new Vector2(2.2f, 1f));
            var wallSide = Mat("Sc21WallSide", new Color(1.22f, 1.22f, 1.21f), 0.08f, 0f, Sc21Tex("Sc21_WallAged"));
            wallSide.SetTextureScale("_BaseMap", new Vector2(2.8f, 1f));
            var wallFar = Mat("Sc21WallFar", new Color(0.55f, 0.56f, 0.57f), 0.12f, 0f, Sc21Tex("Sc21_Wall"));
            wallFar.SetTextureScale("_BaseMap", new Vector2(3f, 1.5f));
            var floorMat = Mat("Sc21Floor", Color.white, 0.30f, 0f, Sc21Tex("Sc21_Floor"));
            floorMat.SetTextureScale("_BaseMap", new Vector2(4.5f, 4.5f));
            var ceilMat = Mat("Sc21Ceiling", Color.white, 0.08f, 0f, Sc21Tex("Sc21_CeilingLight"));
            ceilMat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            var brickMat = Mat("Sc21Brick", Color.white, 0.05f, 0f, Sc21Tex("Sc21_Brick"));
            var boardMat = Mat("Sc21Board", Color.white, 0.04f, 0f, Sc21Tex("Sc21_Board"));
            var hallBg = Mat("Sc21HallBg", new Color(0.62f, 0.64f, 0.68f), 0f, 0f, Sc21Tex("Sc21_HallBG"), null, true);
            var tableMat = Mat("Sc21Table", new Color(1.45f, 1.42f, 1.36f), 0.12f, 0f, Sc21Tex("Sc21_TableTop"));
            tableMat.SetTextureScale("_BaseMap", new Vector2(1.5f, 1f));
            var tubeMat = Mat("Sc21Tube", new Color(0.95f, 0.97f, 1f), 0.5f, 0f, null, new Color(4.2f, 4.4f, 4.7f));
            var greyMetal = Mat("Sc21GreyMetal", new Color(0.50f, 0.52f, 0.54f), 0.45f, 0.65f);
            var darkMetal = Mat("Sc21DarkMetal", new Color(0.16f, 0.17f, 0.18f), 0.40f, 0.6f);
            var conduit = Mat("Sc21Conduit", new Color(0.46f, 0.47f, 0.47f), 0.35f, 0.5f);
            var machineMat = Mat("Sc21Machine", new Color(0.46f, 0.48f, 0.49f), 0.30f, 0.4f);
            var hallFloor = Mat("Sc21HallFloor", new Color(2.0f, 2.05f, 2.1f), 0.40f, 0f, Sc21Tex("Sc21_Floor"));
            hallFloor.SetTextureScale("_BaseMap", new Vector2(5f, 5f));

            GameObject Wall(string n, Vector3 centre, Vector3 size, Material m) => Box(n, room, centre, size, m);

            // ---- shell
            Wall("Sc21Floor", Sc21P(0f, -0.1f, (Sc21Zb + Sc21Zf) * 0.5f), new Vector3(Sc21X1 - Sc21X0, 0.2f, Sc21Zf - Sc21Zb), floorMat);
            Wall("Sc21Ceiling", Sc21P(0f, Sc21H + 0.1f, (Sc21Zb + Sc21Zf) * 0.5f), new Vector3(Sc21X1 - Sc21X0 + 0.4f, 0.2f, Sc21Zf - Sc21Zb + 0.4f), ceilMat);
            Wall("Sc21WallLeft", Sc21P(Sc21X0 - 0.1f, Sc21H * 0.5f, (Sc21Zb + Sc21Zf) * 0.5f), new Vector3(0.2f, Sc21H, Sc21Zf - Sc21Zb + 0.4f), wallSide);
            Wall("Sc21WallRight", Sc21P(Sc21X1 + 0.1f, Sc21H * 0.5f, (Sc21Zb + Sc21Zf) * 0.5f), new Vector3(0.2f, Sc21H, Sc21Zf - Sc21Zb + 0.4f), wallSide);
            Wall("Sc21WallFront", Sc21P(0f, Sc21H * 0.5f, Sc21Zf + 0.1f), new Vector3(Sc21X1 - Sc21X0 + 0.4f, Sc21H, 0.2f), wallSide);
            // back wall with the doorway on the right: wall | brick pier | doorway | lintel
            var pierX0 = Sc21DoorX0 - 0.42f;
            Wall("Sc21WallBack", Sc21P((Sc21X0 + pierX0) * 0.5f, Sc21H * 0.5f, Sc21Zb - 0.1f), new Vector3(pierX0 - Sc21X0, Sc21H, 0.2f), wallBack);
            Wall("Sc21Pier", Sc21P((pierX0 + Sc21DoorX0) * 0.5f, Sc21H * 0.5f, Sc21Zb - 0.02f), new Vector3(Sc21DoorX0 - pierX0, Sc21H, 0.34f), wallBack);
            {
                var br = GameObject.CreatePrimitive(PrimitiveType.Quad);
                br.name = "Sc21PierBrick";
                br.transform.SetParent(room, false);
                br.transform.position = Sc21P((pierX0 + Sc21DoorX0) * 0.5f, 2.32f, Sc21Zb + 0.153f);
                br.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                br.transform.localScale = new Vector3(Sc21DoorX0 - pierX0, 1.30f, 1f);
                Object.DestroyImmediate(br.GetComponent<Collider>());
                br.GetComponent<MeshRenderer>().sharedMaterial = brickMat;
            }
            Wall("Sc21WallBackR", Sc21P((Sc21DoorX0 + 2.6f + Sc21X1) * 0.5f, Sc21H * 0.5f, Sc21Zb - 0.1f), new Vector3(Sc21X1 - Sc21DoorX0 - 2.6f, Sc21H, 0.2f), wallBack);
            Wall("Sc21Lintel", Sc21P((Sc21DoorX0 + Sc21X1) * 0.5f, (Sc21DoorH + Sc21H) * 0.5f, Sc21Zb - 0.1f), new Vector3(Sc21X1 - Sc21DoorX0, Sc21H - Sc21DoorH, 0.2f), wallBack);

            // black skirting / dado, 22 cm high, along the walls of the room (client picture: dark border at the foot of the walls)
            var skirtMat = Mat("Sc21Skirting", new Color(0.055f, 0.055f, 0.058f), 0.18f, 0f);
            const float skH = 0.22f, skT = 0.014f;
            Wall("Sc21SkirtBack", Sc21P((Sc21X0 + pierX0) * 0.5f, skH * 0.5f, Sc21Zb + skT * 0.5f), new Vector3(pierX0 - Sc21X0, skH, skT), skirtMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SkirtBackR", Sc21P((Sc21DoorX0 + 2.6f + Sc21X1) * 0.5f, skH * 0.5f, Sc21Zb + skT * 0.5f), new Vector3(Sc21X1 - Sc21DoorX0 - 2.6f, skH, skT), skirtMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SkirtLeft", Sc21P(Sc21X0 + skT * 0.5f, skH * 0.5f, (Sc21Zb + Sc21Zf) * 0.5f), new Vector3(skT, skH, Sc21Zf - Sc21Zb), skirtMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SkirtRight", Sc21P(Sc21X1 - skT * 0.5f, skH * 0.5f, (Sc21Zb + Sc21Zf) * 0.5f), new Vector3(skT, skH, Sc21Zf - Sc21Zb), skirtMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SkirtFront", Sc21P(0f, skH * 0.5f, Sc21Zf - skT * 0.5f), new Vector3(Sc21X1 - Sc21X0, skH, skT), skirtMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SkirtPier", Sc21P((pierX0 + Sc21DoorX0) * 0.5f, skH * 0.5f, Sc21Zb + 0.16f + skT * 0.5f), new Vector3(Sc21DoorX0 - pierX0, skH, skT), skirtMat).GetComponent<Collider>().enabled = false;

            // ---- the factory hall behind the doorway (the client's picture on the far wall, a few machines for depth, the fluorescent run along the ceiling)
            var hallZ1 = -14.5f;
            Wall("Sc21HallFloor", Sc21P(7.3f, -0.1f, (Sc21Zb + hallZ1) * 0.5f), new Vector3(12.4f, 0.2f, Sc21Zb - hallZ1), hallFloor);
            Wall("Sc21HallCeiling", Sc21P(7.3f, 3.4f, (Sc21Zb + hallZ1) * 0.5f), new Vector3(12.4f, 0.2f, Sc21Zb - hallZ1), ceilMat);
            Wall("Sc21HallWallL", Sc21P(Sc21DoorX0 - 0.1f, 1.7f, (Sc21Zb + hallZ1) * 0.5f), new Vector3(0.2f, 3.4f, Sc21Zb - hallZ1), wallFar);
            Wall("Sc21HallWallR", Sc21P(13.6f, 1.7f, (Sc21Zb + hallZ1) * 0.5f), new Vector3(0.2f, 3.4f, Sc21Zb - hallZ1), wallFar);
            var far = GameObject.CreatePrimitive(PrimitiveType.Quad);
            far.name = "Sc21HallBackdrop";
            far.transform.SetParent(room, false);
            far.transform.position = Sc21P(7.3f, 1.8f, hallZ1 + 0.05f);
            far.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            far.transform.localScale = new Vector3(12.4f, 3.6f, 1f);
            Object.DestroyImmediate(far.GetComponent<Collider>());
            far.GetComponent<MeshRenderer>().sharedMaterial = hallBg;
            for (var i = 0; i < 6; i++)
            {
                var zz = -5.5f - i * 1.5f;
                Wall("Sc21HallTubeA" + i, Sc21P(2.9f, 3.28f, zz), new Vector3(0.16f, 0.06f, 1.4f), tubeMat).GetComponent<Collider>().enabled = false;
                Wall("Sc21HallTubeB" + i, Sc21P(4.9f, 3.28f, zz - 0.7f), new Vector3(0.16f, 0.06f, 1.4f), tubeMat).GetComponent<Collider>().enabled = false;
            }

            // ---- the left part of the back wall: the notice board (the client's picture), a conduit run with a socket, the hanging fluorescent tube
            // notice board built in layers (it looked like a flat picture): aluminium frame with depth, cork, loose papers with tilt and soft contact shadows, pins
            var boardRoot = new GameObject("Sc21NoticeBoard").transform;
            boardRoot.SetParent(room, false);
            boardRoot.position = Sc21P(-3.15f, 1.72f, Sc21Zb + 0.02f);
            boardRoot.rotation = Quaternion.Euler(0f, 180f, 0f);        // local +x = the viewer's right, local -z = towards the viewer
            const float bw = 1.75f, bh = 1.52f;
            var corkMat = Mat("Sc21Cork", new Color(0.74f, 0.72f, 0.70f), 0f, 0f, Sc21Tex("Sc21_Cork"));
            corkMat.SetFloat("_SpecularHighlights", 0f);
            corkMat.SetFloat("_EnvironmentReflections", 0f);
            corkMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            corkMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            var frameMat = Mat("Sc21BoardFrame", new Color(0.62f, 0.64f, 0.66f), 0.40f, 0.6f);
            var shadowMat = MatAlpha("Sc21PaperShadow", Sc21Tex("Sc21_Smudge"), new Color(0f, 0f, 0f, 0.55f), false);
            GameObject BQuad(string n, Vector3 local, Vector2 size, Material m, Vector3 euler)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = n;
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(boardRoot, false);
                q.transform.localPosition = local;
                q.transform.localRotation = Quaternion.Euler(euler);
                q.transform.localScale = new Vector3(size.x, size.y, 1f);
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                if (m == shadowMat)
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                return q;
            }

            GameObject BBox(string n, Vector3 local, Vector3 size, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(boardRoot, false);
                g.transform.localPosition = local;
                g.transform.localScale = size;
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g;
            }

            BQuad("Cork", new Vector3(0f, 0f, -0.006f), new Vector2(bw, bh), corkMat, Vector3.zero);
            BBox("FrameTop", new Vector3(0f, bh * 0.5f + 0.012f, -0.014f), new Vector3(bw + 0.056f, 0.036f, 0.034f), frameMat);
            BBox("FrameBottom", new Vector3(0f, -bh * 0.5f - 0.012f, -0.014f), new Vector3(bw + 0.056f, 0.036f, 0.034f), frameMat);
            BBox("FrameLeft", new Vector3(-bw * 0.5f - 0.012f, 0f, -0.014f), new Vector3(0.036f, bh + 0.056f, 0.034f), frameMat);
            BBox("FrameRight", new Vector3(bw * 0.5f + 0.012f, 0f, -0.014f), new Vector3(0.036f, bh + 0.056f, 0.034f), frameMat);
            BBox("BackBoard", new Vector3(0f, 0f, 0.006f), new Vector3(bw + 0.05f, bh + 0.05f, 0.012f), Mat("Sc21BoardBack", new Color(0.35f, 0.25f, 0.15f), 0.1f, 0f));
            var pinCols = new[] { new Color(0.75f, 0.10f, 0.08f), new Color(0.10f, 0.25f, 0.65f), new Color(0.85f, 0.70f, 0.08f) };
            // name, texture, pixel box on the 2048 x 1780 layout of the client picture, tilt (degrees)
            var papers = new (string n, string tex, float x0, float y0, float x1, float y1, float tilt)[]
            {
                ("A", "Sc21_PaperA", 120, 190, 640, 1120, 0.8f), ("B", "Sc21_PaperB", 700, 200, 1190, 1130, -0.6f), ("C", "Sc21_PaperC", 1240, 200, 1800, 470, 1.0f),
                ("D", "Sc21_PaperD", 1330, 560, 1760, 1110, -0.8f), ("E", "Sc21_PaperE", 140, 1210, 640, 1500, -1.2f), ("F", "Sc21_PaperF", 720, 1300, 1170, 1660, 0.9f)
            };
            for (var pi = 0; pi < papers.Length; pi++)
            {
                var pp = papers[pi];
                var cx = ((pp.x0 + pp.x1) * 0.5f / 2048f - 0.5f) * bw;
                var cy = (0.5f - (pp.y0 + pp.y1) * 0.5f / 1780f) * bh;
                var pw = (pp.x1 - pp.x0) / 2048f * bw;
                var ph = (pp.y1 - pp.y0) / 1780f * bh;
                var zz = -0.016f - 0.005f * pi;   // generous gaps between the layers: no depth fighting (shimmer) at a distance
                BQuad("Shadow" + pp.n, new Vector3(cx + 0.010f, cy - 0.014f, zz + 0.0045f), new Vector2(pw * 1.22f, ph * 1.18f), shadowMat, new Vector3(0f, 0f, -pp.tilt));
                var paperMat = Mat("Sc21" + pp.tex, new Color(0.78f, 0.78f, 0.77f), 0f, 0f, Sc21Tex(pp.tex));
                paperMat.SetFloat("_SpecularHighlights", 0f);
                paperMat.SetFloat("_EnvironmentReflections", 0f);
                paperMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                paperMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                BQuad("Paper" + pp.n, new Vector3(cx, cy, zz), new Vector2(pw, ph), paperMat, new Vector3(0f, 0f, -pp.tilt));
                var pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pin.name = "Pin" + pp.n;
                Object.DestroyImmediate(pin.GetComponent<Collider>());
                pin.transform.SetParent(boardRoot, false);
                pin.transform.localPosition = new Vector3(cx, cy + ph * 0.5f - 0.035f, zz - 0.012f);
                pin.transform.localScale = new Vector3(0.026f, 0.026f, 0.020f);
                pin.GetComponent<MeshRenderer>().sharedMaterial = Mat("Sc21Pin" + (pi % 3), pinCols[pi % 3], 0.8f, 0.2f);
            }

            // electrical wiring in galvanised conduit pipes with clamps: along the back wall under the board, up the corner and along the ceiling, a branch to the
            // tube light, along the left wall; a junction box with a socket and a cable hanging from it (as in the client's picture)
            var galv = Mat("Sc21ConduitGalv", new Color(0.64f, 0.66f, 0.67f), 0.5f, 0.75f);
            var clampMat = Mat("Sc21Clamp", new Color(0.30f, 0.31f, 0.32f), 0.4f, 0.6f);
            var cableMat = Mat("Sc21CableBlack", new Color(0.03f, 0.03f, 0.03f), 0.35f, 0f);
            void Conduit(string n, Vector3 a0, Vector3 b0, float r, int clamps, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = n;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(room, false);
                var dir = b0 - a0;
                g.transform.position = (a0 + b0) * 0.5f;
                g.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                g.transform.localScale = new Vector3(r * 2f, dir.magnitude * 0.5f, r * 2f);
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                for (var ci = 1; ci <= clamps; ci++)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    c.name = n + "Clamp" + ci;
                    Object.DestroyImmediate(c.GetComponent<Collider>());
                    c.transform.SetParent(room, false);
                    c.transform.position = Vector3.Lerp(a0, b0, ci / (float)(clamps + 1));
                    c.transform.rotation = g.transform.rotation;
                    c.transform.localScale = new Vector3(r * 3.1f, 0.016f, r * 3.1f);
                    c.GetComponent<MeshRenderer>().sharedMaterial = clampMat;
                }
            }

            var wz = Sc21Zb + 0.05f;
            Conduit("Sc21ConduitBack", Sc21P(-4.5f, 1.0f, wz), Sc21P(0.95f, 1.0f, wz), 0.022f, 11, galv);
            Conduit("Sc21ConduitRiser", Sc21P(-4.3f, 1.0f, wz), Sc21P(-4.3f, 2.97f, wz), 0.022f, 4, galv);
            Conduit("Sc21ConduitCeiling", Sc21P(-4.3f, 2.95f, wz + 0.01f), Sc21P(0.95f, 2.95f, wz + 0.01f), 0.022f, 10, galv);
            Conduit("Sc21ConduitTubeBranch", Sc21P(-0.1f, 2.95f, wz + 0.01f), Sc21P(-0.1f, 2.95f, -1.35f), 0.020f, 3, galv);
            Conduit("Sc21ConduitLeftWall", Sc21P(-4.47f, 1.0f, Sc21Zb + 0.12f), Sc21P(-4.47f, 1.0f, 3.6f), 0.022f, 12, galv);
            Conduit("Sc21ConduitLeftDrop", Sc21P(-4.47f, 1.0f, 3.6f), Sc21P(-4.47f, 0.2f, 3.6f), 0.022f, 2, galv);
            // junction box + socket plate on the back conduit, and a cable hanging from it with a plug box
            Wall("Sc21JunctionBox", Sc21P(-1.7f, 1.0f, wz + 0.02f), new Vector3(0.17f, 0.13f, 0.085f), clampMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SocketPlate", Sc21P(-1.7f, 1.0f, wz + 0.067f), new Vector3(0.105f, 0.105f, 0.012f), Mat("Sc21SocketWhite", new Color(0.86f, 0.86f, 0.82f), 0.4f, 0f)).GetComponent<Collider>().enabled = false;
            Wall("Sc21SocketHole1", Sc21P(-1.73f, 1.0f, wz + 0.075f), new Vector3(0.012f, 0.03f, 0.006f), cableMat).GetComponent<Collider>().enabled = false;
            Wall("Sc21SocketHole2", Sc21P(-1.67f, 1.0f, wz + 0.075f), new Vector3(0.012f, 0.03f, 0.006f), cableMat).GetComponent<Collider>().enabled = false;
            Conduit("Sc21CableDropA", Sc21P(-1.7f, 0.93f, wz + 0.05f), Sc21P(-1.64f, 0.62f, wz + 0.11f), 0.009f, 0, cableMat);
            Conduit("Sc21CableDropB", Sc21P(-1.64f, 0.62f, wz + 0.11f), Sc21P(-1.58f, 0.36f, wz + 0.15f), 0.009f, 0, cableMat);
            Wall("Sc21PlugBox", Sc21P(-1.57f, 0.30f, wz + 0.16f), new Vector3(0.07f, 0.11f, 0.05f), cableMat).GetComponent<Collider>().enabled = false;
            // a second box with a switch near the doorway pier
            Wall("Sc21SwitchBox", Sc21P(0.62f, 1.3f, wz + 0.02f), new Vector3(0.10f, 0.14f, 0.06f), clampMat).GetComponent<Collider>().enabled = false;
            Conduit("Sc21ConduitSwitch", Sc21P(0.62f, 1.0f, wz), Sc21P(0.62f, 1.3f, wz), 0.020f, 0, galv);

            var tube = Wall("Sc21Tube", Sc21P(-0.1f, 2.78f, -1.4f), new Vector3(1.9f, 0.07f, 0.2f), tubeMat);
            tube.GetComponent<Collider>().enabled = false;
            Wall("Sc21TubeHousing", Sc21P(-0.1f, 2.84f, -1.4f), new Vector3(1.95f, 0.06f, 0.26f), darkMetal).GetComponent<Collider>().enabled = false;
            foreach (var dx in new[] { -0.8f, 0.8f })
                Wall("Sc21TubeWire", Sc21P(-0.1f + dx, 2.92f, -1.4f), new Vector3(0.015f, 0.2f, 0.015f), darkMetal).GetComponent<Collider>().enabled = false;

            // glow halo of the burning tube: one sprite across the picture side, one below (what the learner sees looking up); additive, no shadow, lights nothing
            var glowMat = MatAlpha("Sc21TubeGlow", Sc21SoftDot(), new Color(0.72f, 0.85f, 1f, 0.55f), true);
            void Glow(string n, Vector3 at, Vector3 euler, Vector2 size)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = n;
                q.transform.SetParent(room, false);
                q.transform.position = at;
                q.transform.rotation = Quaternion.Euler(euler);
                q.transform.localScale = new Vector3(size.x, size.y, 1f);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = glowMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            Glow("Sc21TubeGlowFront", Sc21P(-0.1f, 2.78f, -1.28f), new Vector3(0f, 180f, 0f), new Vector2(3.4f, 0.75f));
            Glow("Sc21TubeGlowBack", Sc21P(-0.1f, 2.78f, -1.52f), new Vector3(0f, 0f, 0f), new Vector2(3.4f, 0.75f));
            Glow("Sc21TubeGlowDown", Sc21P(-0.1f, 2.735f, -1.4f), new Vector3(90f, 0f, 0f), new Vector2(3.0f, 0.95f));

            // ---- lights: the tube is the key (cool white), a weak warm bounce on the table, a dim cool fill in the hall
            void Lamp(string n, Vector3 at, float range, float intensity, Color col, LightType type = LightType.Point, float spot = 100f)
            {
                var g = new GameObject(n);
                g.transform.SetParent(room, false);
                g.transform.position = at;
                var l = g.AddComponent<Light>();
                l.type = type;
                l.range = range;
                l.intensity = intensity;
                l.color = col;
                l.shadows = LightShadows.None;
                if (type == LightType.Spot)
                {
                    l.spotAngle = spot;
                    g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            Lamp("Sc21LampKey", Sc21P(-0.1f, 2.6f, -1.4f), 10f, 12.5f, new Color(0.88f, 0.94f, 1f), LightType.Spot, 140f);
            Lamp("Sc21LampTable", Sc21P(-0.3f, 1.9f, 1.4f), 6f, 4.3f, new Color(0.96f, 0.97f, 1f));
            Lamp("Sc21LampBoard", Sc21P(-3.1f, 2.0f, -0.6f), 4f, 1.6f, new Color(0.97f, 0.98f, 1f));
            Lamp("Sc21LampFront", Sc21P(1.2f, 2.2f, 3.9f), 9f, 5.6f, new Color(0.95f, 0.96f, 1f));
            for (var li = 0; li < 5; li++)
            {
                Lamp("Sc21HallLampA" + li, Sc21P(3.0f, 3.0f, -5.0f - li * 2.2f), 9f, 9f, new Color(0.86f, 0.93f, 1f));
                Lamp("Sc21HallLampB" + li, Sc21P(4.8f, 3.0f, -6.1f - li * 2.2f), 9f, 9f, new Color(0.86f, 0.93f, 1f));
            }

            Lamp("Sc21LampHall", Sc21P(-7f, 2.9f, -7f), 14f, 4.5f, new Color(0.85f, 0.92f, 1f));

            // ---- dust floating in the light of the tube (slow, soft)
            var dustGo = new GameObject("Sc21Dust");
            dustGo.transform.SetParent(room, false);
            dustGo.transform.position = Sc21P(0.4f, 1.6f, 0.2f);
            var dust = dustGo.AddComponent<ParticleSystem>();
            var dm = dust.main;
            dm.loop = true;
            dm.startLifetime = 9f;
            dm.startSpeed = 0.03f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f);
            dm.startColor = new Color(0.9f, 0.92f, 0.95f, 0.16f);
            dm.maxParticles = 160;
            dm.simulationSpace = ParticleSystemSimulationSpace.World;
            var de = dust.emission;
            de.rateOverTime = 18f;
            var ds = dust.shape;
            ds.shapeType = ParticleSystemShapeType.Box;
            ds.scale = new Vector3(4.5f, 2.2f, 3.6f);
            var dn = dust.noise;
            dn.enabled = true;
            dn.strength = 0.08f;
            dn.frequency = 0.25f;
            var dr = dustGo.GetComponent<ParticleSystemRenderer>();
            dr.sharedMaterial = MatAlpha("Sc21Dust", Sc21SoftDot(), Color.white, false);
            dr.shadowCastingMode = ShadowCastingMode.Off;

            // ---- age: wet dark patches on the floor, smudges in the corners and under the board, an exposed brick patch (client picture) above the board
            var puddleMat = MatAlpha("Sc21Puddles", Sc21Tex("Sc21_Puddles"), Color.white, false);
            puddleMat.SetFloat("_Smoothness", 0.9f);
            void Decal(string n, Vector3 centre, Vector3 euler, Vector2 size, Material m)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = n;
                q.transform.SetParent(room, false);
                q.transform.position = centre;
                q.transform.rotation = Quaternion.Euler(euler);
                q.transform.localScale = new Vector3(size.x, size.y, 1f);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            Decal("Sc21FloorPuddles", Sc21P(0.2f, 0.004f, 1.3f), new Vector3(90f, 0f, 0f), new Vector2(8f, 7.6f), puddleMat);
            Decal("Sc21HallPuddles", Sc21P(8f, 0.004f, -8.5f), new Vector3(90f, 0f, 0f), new Vector2(10.5f, 10f), puddleMat);
            var smudge = MatAlpha("Sc21Smudge", Sc21Tex("Sc21_Smudge"), Color.white, false);
            Decal("Sc21SmudgeBoard", Sc21P(-3.15f, 0.82f, Sc21Zb + 0.015f), new Vector3(0f, 180f, 0f), new Vector2(3.2f, 1.1f), smudge);
            Decal("Sc21SmudgeCorner", Sc21P(-4.45f, 1.5f, 0.0f), new Vector3(0f, 90f, 0f), new Vector2(4.5f, 3.2f), smudge);
            Decal("Sc21SmudgeBackLow", Sc21P(0.2f, 0.45f, Sc21Zb + 0.015f), new Vector3(0f, 180f, 0f), new Vector2(4.2f, 1.2f), smudge);
            // broken plaster with the bricks showing (client: 2-3 places on the walls), each an irregular patch
            Material PatchMat(string n) { var m = MatAlpha("Sc21" + n, Sc21Tex("Sc21_" + n), Color.white, false); return m; }
            Decal("Sc21BrokenBrickA", Sc21P(-4.18f, 2.25f, Sc21Zb + 0.013f), new Vector3(0f, 180f, 0f), new Vector2(0.95f, 0.85f), PatchMat("BrickPatchA"));
            Decal("Sc21BrokenBrickB", Sc21P(-1.25f, 2.35f, Sc21Zb + 0.013f), new Vector3(0f, 180f, 0f), new Vector2(0.80f, 0.60f), PatchMat("BrickPatchB"));
            Decal("Sc21BrokenBrickC", Sc21P(-4.485f, 1.85f, 1.2f), new Vector3(0f, 90f, 0f), new Vector2(1.15f, 0.95f), PatchMat("BrickPatchC"));
            Decal("Sc21BrokenBrickD", Sc21P(0.9f, 0.55f, Sc21Zb + 0.013f), new Vector3(0f, 180f, 0f), new Vector2(0.80f, 0.65f), PatchMat("BrickPatchA"));
            Decal("Sc21BrokenBrickE", Sc21P(4.485f, 1.55f, 1.8f), new Vector3(0f, -90f, 0f), new Vector2(1.0f, 0.85f), PatchMat("BrickPatchB"));
            // ---- the table of the client picture (third picture): steel trays with bottles and jars with labels, a brush rack, rusty tins, a grey bucket
            var tableAdd = new GameObject("Sc21TableAdditions").transform;
            tableAdd.SetParent(room, false);
            var ty = 0.90f;                       // table top height (film cube 1.8 x 0.5)
            var tx = -0.35f;                      // table centre x in the mirrored room frame
            var labelTex = Sc21Tex("Sc21_Label");
            var rustTex = Sc21Tex("Sc21_Rust");
            var wood = Mat("Sc21Wood", new Color(0.55f, 0.36f, 0.18f), 0.25f, 0f);
            var trayMat = Mat("Sc21Tray", new Color(0.66f, 0.68f, 0.70f), 0.6f, 0.75f);
            var hdpeBlue = Mat("Sc21HdpeBlue", new Color(0.07f, 0.20f, 0.52f), 0.55f, 0f);
            var hdpeWhite = Mat("Sc21HdpeWhite", new Color(0.90f, 0.91f, 0.90f), 0.40f, 0f);
            var glassGreen = Mat("Sc21GlassGreen", new Color(0.16f, 0.36f, 0.24f, 1f), 0.9f, 0f);
            var glassBrown = Mat("Sc21GlassBrown", new Color(0.20f, 0.09f, 0.04f), 0.88f, 0f);
            var creamJar = Mat("Sc21CreamJar", new Color(0.90f, 0.88f, 0.80f), 0.40f, 0f);
            var lidRed = Mat("Sc21LidRed", new Color(0.66f, 0.10f, 0.08f), 0.45f, 0f);
            var lidBlack = Mat("Sc21LidBlack", new Color(0.05f, 0.05f, 0.05f), 0.5f, 0f);
            var capWhite = Mat("Sc21CapWhite", new Color(0.93f, 0.93f, 0.92f), 0.5f, 0f);
            var tinMat = Mat("Sc21TinRust", Color.white, 0.30f, 0.55f, rustTex);
            var bucketMat = Mat("Sc21Bucket", new Color(0.46f, 0.52f, 0.48f), 0.45f, 0.6f);
            var labelMat = Mat("Sc21LabelMat", Color.white, 0.25f, 0f, labelTex);
            var inkDark = Mat("Sc21TinInside", new Color(0.03f, 0.02f, 0.02f), 0.2f, 0f);

            void Place(string n, Mesh mesh, Material m, Vector3 at, float yaw = 0f, float scale = 1f)
            {
                var g = new GameObject(n);
                g.transform.SetParent(tableAdd, false);
                g.transform.position = at;
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                g.transform.localScale = Vector3.one * scale;
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = m;
            }

            void Box3(string n, Vector3 at, Vector3 size, Material m, float yaw = 0f, float tilt = 0f)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(tableAdd, false);
                g.transform.position = at;
                g.transform.rotation = Quaternion.Euler(tilt, yaw, 0f);
                g.transform.localScale = size;
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            void Cyl(string n, Vector3 at, float radius, float height, Material m, float yaw = 0f)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = n;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(tableAdd, false);
                g.transform.position = at + Vector3.up * height * 0.5f;
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                g.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            Vector3 T(float dx, float dz, float lift = 0f) => Sc21P(tx + dx, ty + lift, dz);
            // profiles: radius, height pairs from the bottom centre round to the top centre
            var bottleProf = new[] { 0f, 0f, 0.030f, 0f, 0.034f, 0.008f, 0.034f, 0.115f, 0.029f, 0.148f, 0.013f, 0.166f, 0.013f, 0.184f, 0f, 0.184f };
            var jarProf = new[] { 0f, 0f, 0.039f, 0f, 0.043f, 0.008f, 0.043f, 0.080f, 0.038f, 0.090f, 0.031f, 0.094f, 0f, 0.094f };
            var bigJarProf = new[] { 0f, 0f, 0.062f, 0f, 0.067f, 0.012f, 0.067f, 0.120f, 0.060f, 0.135f, 0.050f, 0.141f, 0f, 0.141f };
            var tinProf = new[] { 0f, 0f, 0.044f, 0f, 0.047f, 0.008f, 0.047f, 0.112f, 0.051f, 0.117f, 0.051f, 0.122f, 0.045f, 0.122f, 0.043f, 0.108f, 0f, 0.108f };
            var bucketProf = new[] { 0f, 0f, 0.072f, 0f, 0.088f, 0.17f, 0.092f, 0.176f, 0.085f, 0.176f, 0.071f, 0.012f, 0f, 0.012f };
            var bottleMesh = Sc21Lathe("Sc21Bottle", bottleProf, 28);
            var jarMesh = Sc21Lathe("Sc21Jar", jarProf, 28);
            var bigJarMesh = Sc21Lathe("Sc21BigJar", bigJarProf, 28);
            var tinMesh = Sc21Lathe("Sc21Tin", tinProf, 28);
            var bucketMesh = Sc21Lathe("Sc21BucketMesh", bucketProf, 32);

            void Labelled(string n, float r, float baseY, float h, Vector3 at, float yaw)
            {
                Cyl(n + "Label", at + Vector3.up * baseY, r * 1.012f, h, labelMat, yaw);
            }

            // two steel trays with raised edges
            void Tray(string n, float dx, float dz, float w, float d, float yaw)
            {
                var c = T(dx, dz);
                Box3(n + "Base", c + Vector3.up * 0.006f, new Vector3(w, 0.012f, d), trayMat, yaw);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Box3(n + "EdgeF", c + rot * new Vector3(0f, 0.022f, d * 0.5f), new Vector3(w, 0.04f, 0.012f), trayMat, yaw);
                Box3(n + "EdgeB", c + rot * new Vector3(0f, 0.022f, -d * 0.5f), new Vector3(w, 0.04f, 0.012f), trayMat, yaw);
                Box3(n + "EdgeL", c + rot * new Vector3(-w * 0.5f, 0.022f, 0f), new Vector3(0.012f, 0.04f, d), trayMat, yaw);
                Box3(n + "EdgeR", c + rot * new Vector3(w * 0.5f, 0.022f, 0f), new Vector3(0.012f, 0.04f, d), trayMat, yaw);
            }

            Tray("Sc21TrayA", 0.24f, 0.50f, 0.50f, 0.28f, 3f);
            Tray("Sc21TrayB", 0.40f, 0.18f, 0.30f, 0.20f, -8f);
            // bottles and jars in the first tray (blue HDPE bottle, cream jar with a red lid, glass bottle, two brown jars)
            Place("Sc21BottleBlue", bottleMesh, hdpeBlue, T(0.10f, 0.52f, 0.012f), 20f);
            Cyl("Sc21BottleBlueCap", T(0.10f, 0.52f, 0.012f + 0.184f), 0.0165f, 0.02f, capWhite);
            Place("Sc21CreamJar", bigJarMesh, creamJar, T(0.26f, 0.57f, 0.012f), 0f);
            Cyl("Sc21CreamJarLid", T(0.26f, 0.57f, 0.012f + 0.141f), 0.052f, 0.026f, lidRed);
            Labelled("Sc21CreamJar", 0.067f, 0.012f + 0.03f, 0.075f, T(0.26f, 0.57f), 200f);
            Place("Sc21GlassBottle", bottleMesh, glassGreen, T(0.17f, 0.40f, 0.012f), 0f, 0.82f);
            Cyl("Sc21GlassBottleCap", T(0.17f, 0.40f, 0.012f + 0.151f), 0.0135f, 0.016f, lidBlack);
            for (var ji = 0; ji < 2; ji++)
            {
                var jx = ji == 0 ? 0.37f : 0.34f;
                var jz = ji == 0 ? 0.44f : 0.60f;
                Place("Sc21BrownJar" + ji, jarMesh, glassBrown, T(jx, jz, 0.012f), ji * 50f);
                Cyl("Sc21BrownJarLid" + ji, T(jx, jz, 0.012f + 0.094f), 0.034f, 0.018f, lidBlack);
                Labelled("Sc21BrownJar" + ji, 0.043f, 0.012f + 0.022f, 0.05f, T(jx, jz), 90f + ji * 120f);
            }

            // white jerrycans (HDPE, handle, neck and blue cap, label) behind the tray
            void Jerry(string n, float dx, float dz, float yaw, float sc)
            {
                var at = T(dx, dz);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Box3(n + "Body", at + Vector3.up * 0.115f * sc, new Vector3(0.150f, 0.215f, 0.090f) * sc, hdpeWhite, yaw);
                Box3(n + "Shoulder", at + rot * new Vector3(0.035f * sc, 0.235f * sc, 0f), new Vector3(0.075f, 0.03f, 0.09f) * sc, hdpeWhite, yaw, 0f);
                Cyl(n + "Neck", at + rot * new Vector3(0.050f * sc, 0.245f * sc, 0f), 0.022f * sc, 0.03f * sc, hdpeWhite, yaw);
                Cyl(n + "Cap", at + rot * new Vector3(0.050f * sc, 0.275f * sc, 0f), 0.026f * sc, 0.014f * sc, hdpeBlue, yaw);
                Box3(n + "HandleA", at + rot * new Vector3(-0.045f * sc, 0.235f * sc, 0f), new Vector3(0.05f, 0.012f, 0.07f) * sc, hdpeWhite, yaw);
                Box3(n + "HandleB", at + rot * new Vector3(-0.045f * sc, 0.262f * sc, 0f), new Vector3(0.05f, 0.014f, 0.07f) * sc, hdpeWhite, yaw);
                Box3(n + "Label", at + rot * new Vector3(0f, 0.12f * sc, -0.0455f * sc), new Vector3(0.10f, 0.08f, 0.003f) * sc, creamJar, yaw);
            }

            // rusty tins, a stirring cup and the grey bucket on the other side (as in the picture)
            Place("Sc21TinA", tinMesh, tinMat, T(-0.04f, 0.38f, 0f), 0f);
            Place("Sc21TinB", tinMesh, tinMat, T(0.05f, 0.26f, 0f), 40f, 0.95f);
            Place("Sc21TinC", tinMesh, tinMat, T(-0.12f, 0.24f, 0f), 80f, 0.9f);
            Place("Sc21BucketGrey", bucketMesh, bucketMat, T(-0.24f, 0.46f, 0f), 0f);
            Cyl("Sc21BucketPaint", T(-0.24f, 0.46f, 0.138f), 0.074f, 0.004f, inkDark);
            // wooden rack with brushes standing in it
            Box3("Sc21RackBox", T(-0.12f, 0.64f, 0.045f), new Vector3(0.20f, 0.09f, 0.10f), wood, 3f);
            for (var bi = 0; bi < 5; bi++)
            {
                var bx = -0.19f + bi * 0.035f;
                Cyl("Sc21RackBrushH" + bi, T(bx, 0.64f, 0.09f), 0.006f, 0.10f, wood);
                Cyl("Sc21RackBrushB" + bi, T(bx, 0.64f, 0.19f), 0.010f, 0.035f, Mat("Sc21Bristle", new Color(0.20f, 0.13f, 0.08f), 0.2f, 0f));
            }

            // paint brushes and wooden stirrers laid on the table front (the client picture)
            var bristle = Mat("Sc21Bristle", new Color(0.20f, 0.13f, 0.08f), 0.2f, 0f);
            var ferrule = Mat("Sc21Ferrule", new Color(0.72f, 0.74f, 0.76f), 0.6f, 0.8f);
            void Brush(string n, float x, float z, float yaw)
            {
                var c = Sc21P(tx + x, ty + 0.012f, z);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                void Seg(string sn, float along, float len, float thick, Material m, PrimitiveType t = PrimitiveType.Cylinder)
                {
                    var g = GameObject.CreatePrimitive(t);
                    g.name = n + sn;
                    Object.DestroyImmediate(g.GetComponent<Collider>());
                    g.transform.SetParent(tableAdd, false);
                    g.transform.position = c + dir * along;
                    g.transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * (t == PrimitiveType.Cylinder ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
                    g.transform.localScale = t == PrimitiveType.Cylinder ? new Vector3(thick, len * 0.5f, thick) : new Vector3(thick, thick * 0.6f, len);
                    g.GetComponent<MeshRenderer>().sharedMaterial = m;
                }

                Seg("Handle", 0.00f, 0.22f, 0.018f, wood);
                Seg("Ferrule", 0.13f, 0.045f, 0.026f, ferrule);
                Seg("Bristles", 0.185f, 0.07f, 0.034f, bristle, PrimitiveType.Cube);
            }

            Brush("Sc21Brush1", 0.18f, 0.70f, 62f);
            Brush("Sc21Brush2", 0.40f, 0.78f, 100f);
            Brush("Sc21Brush3", 0.62f, 0.72f, 84f);
            foreach (var st in new[] { new Vector3(0.05f, 0.68f, 18f), new Vector3(0.10f, 0.80f, 28f), new Vector3(0.00f, 0.86f, 12f) })
                Box3("Sc21Stirrer", Sc21P(tx + st.x, ty + 0.008f, st.y), new Vector3(0.022f, 0.012f, 0.36f), wood, st.z);

            // ---- the factory hall: machines on the far side and along the end, people working at them and walking between them (workflow picture)
            Sc21BuildHallLife(room, greyMetal, darkMetal, machineMat);

            // ---- the animator's film scene
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Sc21Dir + "/Sc21Film.glb");
            if (asset == null)
            {
                Debug.LogError("[Sc21] Sc21Film.glb missing in " + Sc21Dir);
                return refs;
            }

            var film = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(film, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            film.name = "Sc21Film";
            film.transform.SetParent(room, false);
            film.transform.position = new Vector3(Sc21Ox, 0f, 0f);
            film.transform.rotation = Quaternion.identity;
            film.transform.localScale = Vector3.one * Sc21Scale;
            foreach (var a in film.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);
            Sc04NoColliders(film);
            foreach (var sk in film.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sk.updateWhenOffscreen = true; // the bones are moved by code each frame: the bind-pose bounds would cull the characters

            // the animator's dark box (Cube.005), rig control shapes and helper markers are not part of the picture: the room is the one above
            foreach (var r in film.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("cs_") || r.name == "Cube.005" || r.name == "Plane" || r.name.StartsWith("empty"))
                    r.enabled = false;

            Sc21Recolour(film, tableMat);

            // ---- the cinematic (player of the film)
            var cineGo = new GameObject("Sc21Cinematic");
            cineGo.transform.SetParent(room, false);
            var cine = cineGo.AddComponent<Sc21Cinematic>();
            var cso = new SerializedObject(cine);
            cso.FindProperty("filmRoot").objectReferenceValue = film.transform;

            AudioSource Src(string n, bool loop, float vol)
            {
                var g = new GameObject(n);
                g.transform.SetParent(cineGo.transform, false);
                var s = g.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = loop;
                s.volume = vol;
                s.spatialBlend = 0.85f;
                s.minDistance = 2.5f;
                s.maxDistance = 40f;
                return s;
            }

            cso.FindProperty("fxSource").objectReferenceValue = Src("FxSource", false, 1f);
            cso.FindProperty("loopSource").objectReferenceValue = Src("LoopSource", true, 0.4f);

            // ---- what the safe-work step adds (scene06.jpg): extraction arm over the table, shelf with bottles, green floor markings, safety-shower sign
            var extras = new GameObject("Sc21SafeExtras").transform;
            extras.SetParent(room, false);
            Sc21BuildExtras(extras, room, greyMetal, conduit);
            // the shelf with the bottles is part of the room from the start (client): only the rest of the safe-work picture comes later
            foreach (var ch in new List<Transform>(extras.GetComponentsInChildren<Transform>(true)))
                if (ch != null && ch.parent == extras && (ch.name == "ShelfBoard" || ch.name == "ShelfPost" || ch.name == "Bottle"))
                    ch.SetParent(room, true);
            var showerRoot = Sc21BuildShower(room, greyMetal);
            showerRoot.gameObject.SetActive(false);
            cso.FindProperty("safeShower").objectReferenceValue = showerRoot.gameObject;
            tableAdd.SetParent(extras, true);
            extras.gameObject.SetActive(false);
            cso.FindProperty("safeExtras").objectReferenceValue = extras.gameObject;
            cso.ApplyModifiedPropertiesWithoutUndo();
            refs.cine = cine;

            var amb = new GameObject("HallAmbience");
            amb.transform.SetParent(room, false);
            var asrc = amb.AddComponent<AudioSource>();
            asrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Sc20/Hall_Loop.wav");
            asrc.loop = true;
            asrc.playOnAwake = true;
            asrc.spatialBlend = 0f;
            asrc.volume = 0.22f;

            // where the learner stands before / after the film: in front of the table, looking along -Z at the back wall (the first film camera is here too)
            refs.viewing = Marker("Sc21Viewing", room, Sc21P(1.4f, 0f, 3.4f), 180f);

            // ---- "safe work plan complete" card
            var gateGo = new GameObject("Sc21GateHost");
            gateGo.transform.SetParent(room, false);
            refs.gate = gateGo.AddComponent<Sc06Gate>();
            SetRef(refs.gate, "art", art);
            var gso = new SerializedObject(refs.gate);
            gso.FindProperty("titleText").stringValue = Sc21Content.GateTitle;
            gso.FindProperty("questionText").stringValue = Sc21Content.GateQuestion;
            gso.FindProperty("bodyText").stringValue = Sc21Content.GateBody;
            gso.ApplyModifiedPropertiesWithoutUndo();
            return refs;
        }

        /// <summary>Emergency shower in the green of the workflow picture against the left wall (pole, arm, shower head, pull rod with triangle handle, eye-wash
        /// bowl), a green floor mat with a yellow border, a floor drain with a grating under the head, the safety-shower sign on the wall.</summary>
        private static Transform Sc21BuildShower(Transform parent, Material steel0)
        {
            var root = new GameObject("Sc21SafetyShower").transform;
            root.SetParent(parent, false);
            var green = Mat("Sc21PipeGreen", new Color(0.04f, 0.42f, 0.20f), 0.55f, 0.35f);
            var steel = Mat("Sc21ShowerSteel", new Color(0.70f, 0.72f, 0.74f), 0.6f, 0.8f);
            var drainDark = Mat("Sc21DrainDark", new Color(0.03f, 0.035f, 0.04f), 0.3f, 0f);
            var matGreen = Mat("Sc21FloorGreen", new Color(0.04f, 0.38f, 0.17f), 0.55f, 0f);
            var matYellow = Mat("Sc21FloorYellow", new Color(0.95f, 0.78f, 0.08f), 0.5f, 0f);

            GameObject Part(string n, PrimitiveType t, Vector3 p, Vector3 euler, Vector3 size, Material m)
            {
                var g = GameObject.CreatePrimitive(t);
                g.name = n;
                g.transform.SetParent(root, false);
                g.transform.position = p;
                g.transform.rotation = Quaternion.Euler(euler);
                g.transform.localScale = size;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g;
            }

            const float zS = -0.6f;                // along the left wall (the red box of the client: the middle of the left wall as seen from the standing place)
            var poleX = -4.18f;                    // mirrored frame: the left wall of the picture is at -4.5
            var headX = -3.55f;                    // the shower head hangs here (towards the room)
            // floor mat and drain first (they decide where the head hangs)
            Part("MatYellow", PrimitiveType.Cube, Sc21P(-3.75f, 0.005f, zS), Vector3.zero, new Vector3(1.9f, 0.008f, 2.3f), matYellow);
            Part("MatGreen", PrimitiveType.Cube, Sc21P(-3.75f, 0.010f, zS), Vector3.zero, new Vector3(1.72f, 0.008f, 2.12f), matGreen);
            Part("DrainBody", PrimitiveType.Cylinder, Sc21P(headX, 0.016f, zS), Vector3.zero, new Vector3(0.50f, 0.006f, 0.50f), steel);
            Part("DrainHole", PrimitiveType.Cylinder, Sc21P(headX, 0.0215f, zS), Vector3.zero, new Vector3(0.44f, 0.004f, 0.44f), drainDark);
            for (var i = -3; i <= 3; i++)
                Part("DrainBar" + i, PrimitiveType.Cube, Sc21P(headX + i * 0.055f, 0.027f, zS), Vector3.zero, new Vector3(0.012f, 0.008f, 0.42f - Mathf.Abs(i) * 0.035f), steel);
            Part("DrainRing", PrimitiveType.Cylinder, Sc21P(headX, 0.0205f, zS), Vector3.zero, new Vector3(0.50f, 0.002f, 0.50f), steel);
            // pole, base plate, top arm, head, pull rod
            Part("Pole", PrimitiveType.Cylinder, Sc21P(poleX, 1.20f, zS), Vector3.zero, new Vector3(0.085f, 1.20f, 0.085f), green);
            Part("BasePlate", PrimitiveType.Cylinder, Sc21P(poleX, 0.02f, zS), Vector3.zero, new Vector3(0.30f, 0.02f, 0.30f), steel);
            Part("WallBracketLow", PrimitiveType.Cube, Sc21P(-4.42f, 0.55f, zS), Vector3.zero, new Vector3(0.22f, 0.07f, 0.10f), green);
            Part("WallBracketHigh", PrimitiveType.Cube, Sc21P(-4.42f, 1.90f, zS), Vector3.zero, new Vector3(0.22f, 0.07f, 0.10f), green);
            Part("Elbow", PrimitiveType.Sphere, Sc21P(poleX, 2.42f, zS), Vector3.zero, new Vector3(0.13f, 0.13f, 0.13f), green);
            Part("TopArm", PrimitiveType.Cylinder, Sc21P((poleX + headX) * 0.5f, 2.42f, zS), new Vector3(0f, 0f, 90f), new Vector3(0.075f, Mathf.Abs(headX - poleX) * 0.5f, 0.075f), green);
            Part("ArmEnd", PrimitiveType.Sphere, Sc21P(headX, 2.42f, zS), Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), green);
            Part("HeadNeck", PrimitiveType.Cylinder, Sc21P(headX, 2.33f, zS), Vector3.zero, new Vector3(0.05f, 0.07f, 0.05f), green);
            var head = new GameObject("ShowerHead");
            head.transform.SetParent(root, false);
            head.transform.position = Sc21P(headX, 2.26f, zS);
            head.AddComponent<MeshFilter>().sharedMesh = Sc21Cone(0.045f, 0.18f, 0.13f);
            head.AddComponent<MeshRenderer>().sharedMaterial = green;
            Part("HeadDisc", PrimitiveType.Cylinder, Sc21P(headX, 2.13f, zS), Vector3.zero, new Vector3(0.36f, 0.006f, 0.36f), steel);
            Part("PullRod", PrimitiveType.Cylinder, Sc21P(headX + 0.14f * (headX < poleX ? -1f : 1f), 1.85f, zS), Vector3.zero, new Vector3(0.012f, 0.57f, 0.012f), steel);
            var hx = headX + 0.14f * (headX < poleX ? -1f : 1f);
            Part("PullBarTop", PrimitiveType.Cylinder, Sc21P(hx, 1.27f, zS), new Vector3(90f, 0f, 90f), new Vector3(0.012f, 0.11f, 0.012f), steel);
            Part("PullSideA", PrimitiveType.Cylinder, Sc21P(hx - 0.045f, 1.215f, zS), new Vector3(0f, 0f, 28f), new Vector3(0.011f, 0.065f, 0.011f), steel);
            Part("PullSideB", PrimitiveType.Cylinder, Sc21P(hx + 0.045f, 1.215f, zS), new Vector3(0f, 0f, -28f), new Vector3(0.011f, 0.065f, 0.011f), steel);
            // eye-wash bowl on a short green pipe
            Part("EyePipe", PrimitiveType.Cylinder, Sc21P(poleX + 0.18f * (headX < poleX ? -1f : 1f), 0.92f, zS), new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.18f, 0.05f), green);
            Part("EyeBowl", PrimitiveType.Cylinder, Sc21P(poleX + 0.40f * (headX < poleX ? -1f : 1f), 0.96f, zS), Vector3.zero, new Vector3(0.30f, 0.03f, 0.30f), steel);
            Part("EyeBowlRim", PrimitiveType.Cylinder, Sc21P(poleX + 0.40f * (headX < poleX ? -1f : 1f), 0.995f, zS), Vector3.zero, new Vector3(0.33f, 0.008f, 0.33f), green);
            // the sign, on the wall above the pole, facing the room
            var sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sign.name = "SafetyShowerSign";
            sign.transform.SetParent(root, false);
            sign.transform.position = Sc21P(-4.485f, 1.55f, zS + 0.0f);
            sign.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            sign.transform.localScale = new Vector3(0.40f, 0.57f, 1f);
            Object.DestroyImmediate(sign.GetComponent<Collider>());
            sign.GetComponent<MeshRenderer>().sharedMaterial = Mat("Sc21SignShower", Color.white, 0.3f, 0f, Sc21Tex("Sc21_ShowerSign"));
            // a little wet shine round the drain
            var lampGo = new GameObject("ShowerLamp");
            lampGo.transform.SetParent(root, false);
            lampGo.transform.position = Sc21P(-3.4f, 2.5f, zS);
            var l = lampGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4.5f;
            l.intensity = 2.4f;
            l.color = new Color(0.95f, 1f, 0.98f);
            l.shadows = LightShadows.None;
            return root;
        }

        /// <summary>Machines (frames, rollers, hoppers, cabinets) and a conveyor in the hall behind the doorway; they are RUNNING (rollers spin, bodies tremble, lamps blink, boxes travel). No people (client).</summary>
        private static void Sc21BuildHallLife(Transform room, Material steel, Material dark, Material machine)
        {
            var hall = new GameObject("Sc21HallLife").transform;
            hall.SetParent(room, false);
            var white = Mat("Sc21MachineWhite", new Color(0.78f, 0.80f, 0.80f), 0.40f, 0.2f);
            var blueM = Mat("Sc21MachineBlue", new Color(0.18f, 0.32f, 0.52f), 0.35f, 0.2f);
            var rubber = Mat("Sc21Rubber", new Color(0.08f, 0.08f, 0.08f), 0.2f, 0f);

            GameObject Pr(string n, PrimitiveType t, Vector3 at, Vector3 size, Material m, Vector3? euler = null)
            {
                var g = GameObject.CreatePrimitive(t);
                g.name = n;
                g.transform.SetParent(hall, false);
                g.transform.position = at;
                g.transform.rotation = Quaternion.Euler(euler ?? Vector3.zero);
                g.transform.localScale = size;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g;
            }

            void Move(GameObject g, Sc21MachineMotion.Kind k, Vector3 ax, float spd, float amp, float len, float ph)
            {
                g.AddComponent<Sc21MachineMotion>().Setup(k, ax, spd, amp, len, ph);
            }

            // machines along the far side wall (x 11..13.4) and a second row beyond the doorway (x 6.5..8)
            var rows = new[] { 1.95f, 5.6f };
            for (var r = 0; r < rows.Length; r++)
                for (var i = 0; i < 4; i++)
                {
                    var x = rows[r];
                    var z = -4.8f - i * 2.5f - r * 0.9f;
                    var big = (i + r) % 2 == 0;
                    var m = big ? white : machine;
                    Move(Pr("MachBody", PrimitiveType.Cube, Sc21P(x, 0.55f, z), new Vector3(big ? 1.6f : 1.2f, 1.1f, 1.9f), m), Sc21MachineMotion.Kind.Vibrate, Vector3.up, 0f, 0.0035f, 0f, i * 0.7f + r);
                    Pr("MachTop", PrimitiveType.Cube, Sc21P(x, 1.25f, z), new Vector3(1.0f, 0.3f, 1.5f), steel);
                    Move(Pr("MachRoller", PrimitiveType.Cylinder, Sc21P(x, 1.55f, z), new Vector3(0.28f, 0.7f, 0.28f), dark, new Vector3(0f, 0f, 90f)), Sc21MachineMotion.Kind.Spin, Vector3.up, 220f + i * 40f, 0f, 0f, i * 0.3f);
                    Move(Pr("MachDrum", PrimitiveType.Cylinder, Sc21P(x, 1.02f, z + 0.35f), new Vector3(0.32f, 0.5f, 0.32f), steel, new Vector3(0f, 0f, 90f)), Sc21MachineMotion.Kind.Spin, Vector3.up, -140f, 0f, 0f, i * 0.2f);
                    Move(Pr("MachHopper", PrimitiveType.Cylinder, Sc21P(x + (r == 0 ? -0.2f : 0.2f), 1.9f, z + 0.25f), new Vector3(0.5f, 0.35f, 0.5f), big ? blueM : steel), Sc21MachineMotion.Kind.Vibrate, Vector3.up, 0f, 0.006f, 0f, i * 0.4f);
                    Pr("MachPipe", PrimitiveType.Cylinder, Sc21P(x, 2.65f, z), new Vector3(0.14f, 1.0f, 0.14f), steel, new Vector3(90f, 0f, 0f));
                    Pr("MachPanel", PrimitiveType.Cube, Sc21P(x + (r == 0 ? 0.82f : -0.82f), 1.0f, z - 0.4f), new Vector3(0.06f, 0.5f, 0.4f), dark);
                    Move(Pr("MachLightG", PrimitiveType.Sphere, Sc21P(x + (r == 0 ? 0.86f : -0.86f), 1.18f, z - 0.4f), new Vector3(0.05f, 0.05f, 0.05f), Mat("Sc21LampGreen", new Color(0.1f, 0.9f, 0.3f), 0.6f, 0f, null, new Color(0.2f, 2f, 0.5f))), Sc21MachineMotion.Kind.Blink, Vector3.up, 0.8f + 0.25f * i, 0f, 0f, i * 0.37f);
                }

            // a conveyor between the rows, shelves of boxes along the near wall of the hall
            Pr("ConveyorBed", PrimitiveType.Cube, Sc21P(8.4f, 0.7f, -9.5f), new Vector3(0.7f, 0.12f, 9f), rubber);
            Pr("ConveyorLegL", PrimitiveType.Cube, Sc21P(8.1f, 0.35f, -9.5f), new Vector3(0.05f, 0.7f, 9f), steel);
            Pr("ConveyorLegR", PrimitiveType.Cube, Sc21P(8.7f, 0.35f, -9.5f), new Vector3(0.05f, 0.7f, 9f), steel);
            var box = Mat("Sc21CardboardBox", new Color(0.55f, 0.42f, 0.27f), 0.15f, 0f);
            for (var i = 0; i < 7; i++)
                Move(Pr("Box" + i, PrimitiveType.Cube, Sc21P(8.4f, 0.9f, -5.6f - i * 1.1f), new Vector3(0.45f, 0.28f, 0.45f), box), Sc21MachineMotion.Kind.Travel, new Vector3(0f, 0f, -1f), 0.55f, 0f, 7.7f, 0f);
        }

        /// <summary>Extraction arm (flexible hose + clear hood) over the table, a steel shelf with bottles, green floor markings, the safety-shower sign.</summary>
        private static void Sc21BuildExtras(Transform extras, Transform room, Material steel, Material hose)
        {
            Transform Part(string n, PrimitiveType t, Vector3 p, Vector3 euler, Vector3 s, Material m)
            {
                var g = GameObject.CreatePrimitive(t);
                g.name = n;
                g.transform.SetParent(extras, false);
                g.transform.position = p;
                g.transform.rotation = Quaternion.Euler(euler);
                g.transform.localScale = s;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g.transform;
            }

            // the extraction arm (LEV): the designer's own model of Scenario 6 (LevArm.glb), a little smaller than there, as in the workflow picture:
            // it stands behind the left end of the table, the hose reaches over the table, a duct goes up into the ceiling
            var lev = SpawnSc06("LevArm.glb", extras);
            if (lev != null)
            {
                lev.name = "Sc21LevArm";
                var lb0 = BoundsOfTree(lev.transform);
                lev.transform.localScale *= 1.40f / Mathf.Max(0.01f, lb0.size.y);
                Vector3 baseP = Vector3.zero, nozP = Vector3.zero;
                var nozB = new Bounds(Vector3.zero, Vector3.zero);
                var minYv = 0f;
                void Measure()
                {
                    minYv = float.MaxValue;
                    foreach (var mf0 in lev.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf0.sharedMesh == null)
                            continue;
                        foreach (var v0 in mf0.sharedMesh.vertices)
                            minYv = Mathf.Min(minYv, mf0.transform.TransformPoint(v0).y);
                    }

                    var sum = Vector3.zero;
                    var cnt = 0;
                    nozP = BoundsOfTree(lev.transform).center;
                    foreach (var mf0 in lev.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf0.sharedMesh == null)
                            continue;
                        foreach (var v0 in mf0.sharedMesh.vertices)
                        {
                            var wv0 = mf0.transform.TransformPoint(v0);
                            if (wv0.y < minYv + 0.04f)
                            {
                                sum += wv0;
                                cnt++;
                            }
                        }

                        var mr0 = mf0.GetComponent<MeshRenderer>();
                        if (mr0 == null)
                            continue;
                        foreach (var m0 in mr0.sharedMaterials)
                            if (m0 != null && m0.name.Contains("Iron Old") && Sc06SubBounds(mf0, mr0, m0.name, out var nb0))
                            {
                                nozP = nb0.center;
                                nozB = nb0;
                            }
                    }

                    baseP = cnt > 0 ? sum / cnt : BoundsOfTree(lev.transform).center;
                }

                Measure();
                var reach = new Vector3(nozP.x - baseP.x, 0f, nozP.z - baseP.z);
                // the nozzle reaches toward -X (the right of the picture, over the table)
                if (reach.sqrMagnitude > 1e-4f)
                    lev.transform.RotateAround(baseP, Vector3.up, Vector3.SignedAngle(reach, Vector3.left, Vector3.up));
                Measure();
                var wantBase = Sc21P(-3.40f, 0f, -2.45f);
                const float levBaseY = 1.30f;
                var levShift = new Vector3(wantBase.x - baseP.x, levBaseY - minYv, wantBase.z - baseP.z);
                lev.transform.position += levShift;
                nozB.center += levShift;
                // a wall bracket carries the base of the arm
                Part("LevBracket", PrimitiveType.Cylinder, new Vector3(wantBase.x, levBaseY + 0.04f, (Sc21Zb + wantBase.z) * 0.5f), new Vector3(90f, 0f, 0f), new Vector3(0.11f, (wantBase.z - Sc21Zb) * 0.5f, 0.11f), steel);
                Part("LevWallPlate", PrimitiveType.Cube, new Vector3(wantBase.x, levBaseY + 0.04f, Sc21Zb + 0.03f), Vector3.zero, new Vector3(0.34f, 0.34f, 0.04f), steel);
                var armMat = Mat("Sc21LevArmSilver", new Color(0.80f, 0.82f, 0.84f), 0.62f, 0.7f);
                var armDark = Mat("Sc06LevArmDark", new Color(0.22f, 0.23f, 0.25f), 0.35f, 0f);
                foreach (var r in lev.GetComponentsInChildren<Renderer>(true))
                {
                    var mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    r.sharedMaterial = mn.Contains("Black") ? armDark : armMat;
                    Object.DestroyImmediate(r.GetComponent<Collider>());
                }

                foreach (var col in lev.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(col);
                // the duct from the highest point of the arm up into the ceiling
                var topY = float.MinValue;
                var topPos = Vector3.zero;
                foreach (var mf2 in lev.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf2.sharedMesh == null)
                        continue;
                    foreach (var v2 in mf2.sharedMesh.vertices)
                    {
                        var w2 = mf2.transform.TransformPoint(v2);
                        if (w2.y > topY)
                        {
                            topY = w2.y;
                            topPos = w2;
                        }
                    }
                }

                var h = Sc21H - topY;
                if (h > 0.05f)
                {
                    var silver = Mat("Sc21DuctSilver", new Color(0.80f, 0.82f, 0.84f), 0.65f, 0.75f);
                    Part("LevDuct", PrimitiveType.Cylinder, new Vector3(topPos.x, topY + h * 0.5f, topPos.z), Vector3.zero, new Vector3(0.17f, h * 0.5f, 0.17f), silver);
                    Part("LevCollarLow", PrimitiveType.Cylinder, new Vector3(topPos.x, topY + 0.07f, topPos.z), Vector3.zero, new Vector3(0.215f, 0.035f, 0.215f), silver);
                    Part("LevCollarMid", PrimitiveType.Cylinder, new Vector3(topPos.x, topY + h * 0.5f, topPos.z), Vector3.zero, new Vector3(0.20f, 0.012f, 0.20f), silver);
                    Part("LevCeilingFlange", PrimitiveType.Cylinder, new Vector3(topPos.x, Sc21H - 0.03f, topPos.z), Vector3.zero, new Vector3(0.30f, 0.03f, 0.30f), silver);
                }
            }

            // shelf with bottles on the back wall
            var shelfX = -0.2f;
            foreach (var y in new[] { 0.9f, 1.4f })
                Part("ShelfBoard", PrimitiveType.Cube, Sc21P(shelfX, y, Sc21Zb + 0.22f), Vector3.zero, new Vector3(1.4f, 0.03f, 0.42f), steel);
            foreach (var dx in new[] { -0.68f, 0.68f })
                Part("ShelfPost", PrimitiveType.Cube, Sc21P(shelfX + dx, 1.2f, Sc21Zb + 0.22f), Vector3.zero, new Vector3(0.04f, 1.55f, 0.04f), steel);
            var bottleCols = new[] { new Color(0.9f, 0.9f, 0.88f), new Color(0.2f, 0.45f, 0.75f), new Color(0.85f, 0.65f, 0.12f), new Color(0.15f, 0.15f, 0.16f), new Color(0.92f, 0.92f, 0.9f) };
            for (var row = 0; row < 2; row++)
                for (var i = 0; i < 8; i++)
                {
                    var c = bottleCols[(i + row * 2) % bottleCols.Length];
                    var m = Mat("Sc21Bottle" + ((i + row * 2) % bottleCols.Length), c, 0.7f, 0f);
                    var h = 0.2f + 0.05f * ((i * 7) % 3);
                    Part("Bottle", PrimitiveType.Cylinder, Sc21P(shelfX - 0.58f + i * 0.165f, (row == 0 ? 0.915f : 1.415f) + h * 0.5f, Sc21Zb + 0.22f), Vector3.zero, new Vector3(0.09f, h * 0.5f, 0.09f), m);
                }

            // green floor mats with a yellow border, laid in front of the doorway and in the corridor (workflow picture)
            var green = Mat("Sc21FloorGreen", new Color(0.04f, 0.38f, 0.17f), 0.55f, 0f);
            var yellow = Mat("Sc21FloorYellow", new Color(0.95f, 0.78f, 0.08f), 0.5f, 0f);
            void Mat2(Vector3 c, float w, float d)
            {
                Part("MatYellow", PrimitiveType.Cube, c + Vector3.up * 0.004f, Vector3.zero, new Vector3(w, 0.006f, d), yellow);
                Part("MatGreen", PrimitiveType.Cube, c + Vector3.up * 0.008f, Vector3.zero, new Vector3(w - 0.16f, 0.006f, d - 0.16f), green);
            }

        }

        /// <summary>Open cone (top radius r0 at y = 0, bottom radius r1 at y = -h), both sides drawn.</summary>
        private static Mesh Sc21Cone(float r0, float r1, float h)
        {
            const int seg = 28;
            var vs = new List<Vector3>();
            var ts = new List<int>();
            for (var i = 0; i <= seg; i++)
            {
                var a = i / (float)seg * Mathf.PI * 2f;
                vs.Add(new Vector3(Mathf.Cos(a) * r0, 0f, Mathf.Sin(a) * r0));
                vs.Add(new Vector3(Mathf.Cos(a) * r1, -h, Mathf.Sin(a) * r1));
            }

            for (var i = 0; i < seg; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = i * 2 + 2, b1 = i * 2 + 3;
                ts.AddRange(new[] { a0, a1, b0, b0, a1, b1, a0, b0, a1, b0, b1, a1 });   // both faces
            }

            var m = new Mesh { name = "Sc21Cone" };
            m.SetVertices(vs);
            m.SetTriangles(ts, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Surface of revolution from a profile of (radius, height) pairs, drawn on both sides (open tins show their inside).</summary>
        private static Mesh Sc21Lathe(string name, float[] prof, int seg)
        {
            var n = prof.Length / 2;
            var vs = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (var i = 0; i < n; i++)
                for (var k = 0; k <= seg; k++)
                {
                    var a = k / (float)seg * Mathf.PI * 2f;
                    vs.Add(new Vector3(Mathf.Cos(a) * prof[i * 2], prof[i * 2 + 1], Mathf.Sin(a) * prof[i * 2]));
                    uvs.Add(new Vector2(k / (float)seg, i / (float)(n - 1)));
                }

            var ts = new List<int>();
            for (var i = 0; i < n - 1; i++)
                for (var k = 0; k < seg; k++)
                {
                    int a0 = i * (seg + 1) + k, a1 = a0 + 1, b0 = (i + 1) * (seg + 1) + k, b1 = b0 + 1;
                    ts.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }

            var m1 = new Mesh();
            m1.SetVertices(vs);
            m1.SetUVs(0, uvs);
            m1.SetTriangles(ts, 0);
            m1.RecalculateNormals();
            var nrm = m1.normals;
            // second copy with flipped normals and winding: both sides lit correctly
            var vs2 = new List<Vector3>(vs);
            var uv2 = new List<Vector2>(uvs);
            var nr2 = new List<Vector3>(nrm);
            for (var i = 0; i < nrm.Length; i++)
                nr2.Add(-nrm[i]);
            vs2.AddRange(vs);
            uv2.AddRange(uvs);
            var ts2 = new List<int>(ts);
            for (var i = 0; i + 2 < ts.Count; i += 3)
                ts2.AddRange(new[] { ts[i] + vs.Count, ts[i + 2] + vs.Count, ts[i + 1] + vs.Count });
            Object.DestroyImmediate(m1);
            var m = new Mesh { name = name };
            m.SetVertices(vs2);
            m.SetUVs(0, uv2);
            m.SetNormals(nr2);
            m.SetTriangles(ts2, 0);
            m.RecalculateBounds();
            return m;
        }

        private static void Sc21Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(m);
        }

        private static Texture2D Sc21SoftDot()
        {
            var path = Sc21Dir + "/Tex/Sc21_SoftDot.png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null)
                return t;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 64; x++)
                {
                    var r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f)));
                }

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The glb has no colour picture for some node-group materials (vest, helmet, worker body): the animator's own pictures (textures folder of the
        /// blend) are put back by material name where they exist; the table gets the grimy room material.</summary>
        private static void Sc21Recolour(GameObject film, Material tableMat)
        {
            // The worker on the left (rig.002: red cap, red vest, navy overall) keeps the animator's pictures. The supervisor (rig.001, 'body low') has NO
            // colour picture in the files the animator sent (the 'Worker_material_*' set is missing; the glb only holds a face atlas), so his body picture is
            // painted from the mesh itself (bone weights + height -> orange hi-vis top, navy trousers, grey gloves, black boots; Sc21SupervisorTexture) and a
            // white hard hat is put on his head, as in the animator's video.
            Renderer body = null, head = null;
            Transform headBone = null;
            foreach (var r in film.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Cube")
                    r.sharedMaterial = tableMat;
                else if (r.name == "body low")
                    body = r;
                else if (r.name == "Helmet 1_H2_0")
                    r.enabled = false;   // the worker (rig.002) has nothing on his head (client)
                else if (r.name == "MALE_ANATOMY_LOW_POLY_DELIVERY")
                    head = r;
            }

            foreach (var t in film.GetComponentsInChildren<Transform>(true))
                if (t.name == "head.x" && t.IsChildOf(body != null ? body.transform.parent : film.transform))
                {
                    headBone = t;
                    break;
                }

            if (body is SkinnedMeshRenderer sk && sk.sharedMesh != null && sk.sharedMesh.isReadable)
            {
                var tex = Sc21SupervisorTexture(sk);
                body.sharedMaterial = Mat("Sc21SupBody", Color.white, 0.22f, 0f, tex);
            }

        }

        /// <summary>Paints the supervisor body picture from the mesh. The animator picture (Worker_material_Base_Color.png) is not in the files he sent,
        /// so the dress of his video is rebuilt: orange hi-vis top with a navy collar and yoke, a silver-teal reflective band at the waist, navy below it,
        /// navy trousers, orange upper arms with a teal band above the elbow and navy forearms, grey gloves, black boots, a yellow hard hat.</summary>
        private static Texture2D Sc21SupervisorTexture(SkinnedMeshRenderer sk)
        {
            const int N = 1024;
            var path = Sc21Dir + "/Tex/Sc21_SupervisorBody.png";
            var mesh = sk.sharedMesh;
            var uv = mesh.uv;
            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var bw = mesh.boneWeights;
            var bones = sk.bones;
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var v in verts)
            {
                y0 = Mathf.Min(y0, v.y);
                y1 = Mathf.Max(y1, v.y);
            }

            var skin = new Color(0.60f, 0.40f, 0.31f);
            var orange = new Color(0.90f, 0.42f, 0.08f);
            var navy = new Color(0.045f, 0.07f, 0.15f);
            var glove = new Color(0.46f, 0.47f, 0.49f);
            var boot = new Color(0.05f, 0.05f, 0.05f);
            var teal = new Color(0.62f, 0.86f, 0.84f);
            var hardHat = new Color(1.0f, 0.80f, 0.03f);
            // 1 = colour fixed by the bone, 0 = by height (torso, legs)
            int KindOf(int i, out Color fixedCol)
            {
                var n = bones[bw[i].boneIndex0].name;
                fixedCol = orange;
                if (n.StartsWith("head"))
                {
                    fixedCol = hardHat;   // the cap of this mesh follows the head bone: yellow (client)
                    return 1;
                }

                if (n.StartsWith("neck"))
                {
                    fixedCol = skin;
                    return 1;
                }

                if (n.StartsWith("hand") || n.StartsWith("c_index") || n.StartsWith("c_middle") || n.StartsWith("c_ring") || n.StartsWith("c_pinky") || n.StartsWith("c_thumb")
                    || n.StartsWith("index") || n.StartsWith("middle") || n.StartsWith("ring") || n.StartsWith("pinky") || n.StartsWith("thumb"))
                {
                    fixedCol = glove;
                    return 1;
                }

                if (n.StartsWith("forearm"))
                {
                    fixedCol = navy;
                    return 1;
                }

                if (n.StartsWith("c_arm"))
                {
                    fixedCol = orange;   // upper sleeve
                    return 1;
                }

                if (n.StartsWith("arm_stretch"))
                {
                    fixedCol = orange;
                    return 1;
                }

                return 0;
            }

            Color ByHeight(float h)
            {
                if (h < 0.075f)
                    return boot;
                if (h < 0.535f)
                    return navy;
                if (h < 0.565f)
                    return teal;
                if (h < 0.585f)
                    return navy;
                if (h < 0.800f)
                    return orange;
                return navy;          // collar and yoke
            }

            var cols = new Color[N * N];
            var set = new bool[N * N];
            for (var t = 0; t + 2 < tris.Length; t += 3)
            {
                int a2 = tris[t], b2 = tris[t + 1], c2 = tris[t + 2];
                var ka = KindOf(a2, out var fa);
                var kb = KindOf(b2, out var fb);
                var kc = KindOf(c2, out var fc);
                var useFixed = ka + kb + kc >= 2;      // a triangle is bone-coloured when two of its vertices are
                var fixedCol = ka == 1 ? fa : kb == 1 ? fb : fc;
                float ha = (verts[a2].y - y0) / (y1 - y0), hb = (verts[b2].y - y0) / (y1 - y0), hc = (verts[c2].y - y0) / (y1 - y0);
                Vector2 pa = uv[a2] * N, pb = uv[b2] * N, pc = uv[c2] * N;
                var minx = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
                var maxx = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
                var miny = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
                var maxy = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
                var den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                if (Mathf.Abs(den) < 1e-6f)
                    continue;
                for (var y = miny; y <= maxy; y++)
                    for (var x = minx; x <= maxx; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        var l1 = ((pb.y - pc.y) * (px - pc.x) + (pc.x - pb.x) * (py - pc.y)) / den;
                        var l2 = ((pc.y - pa.y) * (px - pc.x) + (pa.x - pc.x) * (py - pc.y)) / den;
                        var l3 = 1f - l1 - l2;
                        if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f)
                            continue;
                        cols[y * N + x] = useFixed ? fixedCol : ByHeight(l1 * ha + l2 * hb + l3 * hc);
                        set[y * N + x] = true;
                    }
            }

            // grow the painted islands a few pixels (no seams at the island borders), then a little fabric grain
            for (var pass = 0; pass < 6; pass++)
            {
                var nc = (Color[])cols.Clone();
                var ns = (bool[])set.Clone();
                for (var y = 1; y < N - 1; y++)
                    for (var x = 1; x < N - 1; x++)
                    {
                        var i = y * N + x;
                        if (set[i])
                            continue;
                        foreach (var d in new[] { i - 1, i + 1, i - N, i + N })
                            if (set[d])
                            {
                                nc[i] = cols[d];
                                ns[i] = true;
                                break;
                            }
                    }

                cols = nc;
                set = ns;
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px2 = new Color[N * N];
            for (var i = 0; i < px2.Length; i++)
            {
                var g = 0.93f + 0.14f * Mathf.PerlinNoise((i % N) * 0.35f, (i / N) * 0.35f);
                var c = set[i] ? cols[i] : orange;
                px2[i] = new Color(c.r * g, c.g * g, c.b * g, 1f);
            }

            tex.SetPixels(px2);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Sc21TextureSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sc21Dir + "/Tex" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null)
                    continue;
                if (imp.mipmapEnabled && imp.anisoLevel == 8 && imp.filterMode == FilterMode.Trilinear)
                    continue;
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }

            var sds = AssetImporter.GetAtPath("Assets/Resources/Sc21/Sc21_Sds.png") as TextureImporter;
            if (sds != null && (sds.textureType != TextureImporterType.Sprite || !sds.mipmapEnabled || sds.anisoLevel != 8 || sds.maxTextureSize != 4096))
            {
                sds.textureType = TextureImporterType.Sprite;
                sds.spriteImportMode = SpriteImportMode.Single;
                sds.mipmapEnabled = true;
                sds.filterMode = FilterMode.Trilinear;
                sds.anisoLevel = 8;
                sds.maxTextureSize = 4096;
                sds.textureCompression = TextureImporterCompression.CompressedHQ;
                sds.SaveAndReimport();
            }
        }
    }
}
#endif
