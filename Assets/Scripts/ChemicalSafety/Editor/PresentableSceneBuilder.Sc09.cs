#if UNITY_EDITOR
using System.Collections.Generic;
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Scenario 9: chemical splash on the face. The room is the designer's "chemical lab 02" model (bench run with sink, glass-door
    /// cabinets, shelves, whiteboard, emergency shower and eye wash), scaled to real size, with our own interactive props: three
    /// labelled flasks (B-Sample 2 is purple), the broken-flask pieces, a goggles/gloves tray, notebook, the eye wash bowl with its
    /// push plate and the water jets. Viewer faces -Z, viewer's left is +X (like the other labs); env origin x = 320.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private sealed class Sc09Refs
        {
            public Transform spawn;
            public Transform benchStand;
            public Transform showerStand;
            public Sc09Flask flask;
            public Sc09Shower shower;
            public Sc09Visor visor;
            public Sc09Guide guide;
            public Sc06Gate gate;
            public Sc06Ambience ambience;
        }

        private const float Sc09Ox = 320f;

        private static GameObject SpawnSc09(string file, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/3D/Sc09/" + file);
            if (asset == null)
            {
                Debug.LogWarning("[Presentable] Missing Sc09 model " + file);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        private static Sc09Refs BuildSc09Room(Transform env, PresentableArt art)
        {
            const float ox = Sc09Ox;
            var refs = new Sc09Refs();
            var room = new GameObject("Room").transform;
            room.SetParent(env, false);
            ConfigureSc06Textures();

            var mood = env.gameObject.AddComponent<ChemicalSafety.Presentable.Sc03Mood>();
            var mso = new SerializedObject(mood);
            mso.FindProperty("ambientSky").colorValue = new Color(0.78f, 0.80f, 0.84f);
            mso.FindProperty("ambientEquator").colorValue = new Color(0.64f, 0.66f, 0.68f);
            mso.FindProperty("ambientGround").colorValue = new Color(0.34f, 0.35f, 0.36f);
            mso.FindProperty("fogColor").colorValue = new Color(0.62f, 0.66f, 0.68f);
            mso.FindProperty("fogStart").floatValue = 9f;
            mso.FindProperty("fogEnd").floatValue = 40f;
            mso.FindProperty("sunIntensity").floatValue = 0.0f;
            mso.FindProperty("reflectionIntensity").floatValue = 0.4f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            Sc09Shell(room, ox, art, out var lab);
            Sc09Props(room, ox, art, lab);
            Sc09Shower(room, ox, art, lab, refs);
            Sc09Logic(room, ox, art, refs);

            refs.spawn = Marker("Spawn_SC09", room, new Vector3(ox + lab.spawn.x, 0f, lab.spawn.z), lab.spawnYaw);
            refs.benchStand = Marker("Stand_SC09_Bench", room, new Vector3(ox + lab.bench.x, 0f, lab.bench.z), lab.benchYaw);
            refs.showerStand = Marker("Stand_SC09_Shower", room, new Vector3(ox + lab.showerStand.x, 0f, lab.showerStand.z), lab.showerYaw);
            return refs;
        }
    }
}
#endif
