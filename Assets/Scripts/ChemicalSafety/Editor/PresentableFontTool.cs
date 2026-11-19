using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Builds the Times New Roman TextMeshPro font assets (regular / bold / italic / bold-italic) from the Windows fonts and links
    /// them in one family, so "bold" picks the real bold face. The Presentable builder uses them for ALL English text behind the
    /// single switch <c>PresentableSceneBuilder.EnglishFontTimes</c> (false = the old Liberation Sans). Bangla is untouched.
    /// </summary>
    public static class PresentableFontTool
    {
        public const string Dir = "Assets/Art/Fonts";
        public const string RegularName = "TimesNewRoman SDF";

        private static readonly (string win, string asset, string label)[] Faces =
        {
            ("times.ttf", "TimesNewRoman-Regular", "Regular"),
            ("timesbd.ttf", "TimesNewRoman-Bold", "Bold"),
            ("timesi.ttf", "TimesNewRoman-Italic", "Italic"),
            ("timesbi.ttf", "TimesNewRoman-BoldItalic", "BoldItalic"),
        };

        private static string Charset()
        {
            var sb = new System.Text.StringBuilder();
            for (var c = 32; c <= 126; c++)
                sb.Append((char)c);
            for (var c = 160; c <= 255; c++)
                sb.Append((char)c);
            sb.Append("–—‘’“”•…→←↑↓≤≥≠°±×÷−✓✔€™");
            return sb.ToString();
        }

        [MenuItem("Chemical Safety/Presentable/Create Times New Roman font")]
        public static void Create()
        {
            Directory.CreateDirectory(Dir);
            var winFonts = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            var fonts = new Font[Faces.Length];
            for (var i = 0; i < Faces.Length; i++)
            {
                var src = Path.Combine(winFonts, Faces[i].win);
                if (!File.Exists(src))
                {
                    Debug.LogError("[Font] missing " + src);
                    return;
                }

                var dst = Dir + "/" + Faces[i].asset + ".ttf";
                File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                fonts[i] = AssetDatabase.LoadAssetAtPath<Font>(dst);
            }

            var chars = Charset();
            var assets = new TMP_FontAsset[Faces.Length];
            for (var i = 0; i < Faces.Length; i++)
            {
                var path = Dir + "/" + Faces[i].asset + " SDF.asset";
                AssetDatabase.DeleteAsset(path);
                var fa = TMP_FontAsset.CreateFontAsset(fonts[i], 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                fa.name = i == 0 ? RegularName : Faces[i].asset + " SDF";
                AssetDatabase.CreateAsset(fa, i == 0 ? Dir + "/" + RegularName + ".asset" : path);
                if (fa.atlasTextures != null)
                    foreach (var t in fa.atlasTextures)
                    {
                        t.name = fa.name + " Atlas";
                        AssetDatabase.AddObjectToAsset(t, fa);
                    }

                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.TryAddCharacters(chars, out var missing);
                if (!string.IsNullOrEmpty(missing))
                    Debug.Log("[Font] " + fa.name + " missing chars: " + missing);
                EditorUtility.SetDirty(fa);
                assets[i] = fa;
            }

            // one family: the weight table maps normal / bold and the italic variants
            var reg = assets[0];
            var table = reg.fontWeightTable;
            table[4] = new TMP_FontWeightPair { regularTypeface = assets[0], italicTypeface = assets[2] };
            table[7] = new TMP_FontWeightPair { regularTypeface = assets[1], italicTypeface = assets[3] };
            EditorUtility.SetDirty(reg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Font] Times New Roman font assets built in " + Dir);
        }

        /// <summary>The bar's text: the same Times New Roman (regular + bold) but with the TextMeshPro Overlay shader (drawn on top of everything), so the
        /// timer bar text is never hidden behind the fume hood or a wall. Files go to Assets/Resources/Fonts (the bar loads it by name).</summary>
        [MenuItem("Chemical Safety/Presentable/Create HUD overlay font")]
        public static void CreateHudOverlay()
        {
            var overlay = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            if (overlay == null)
            {
                Debug.LogError("[Font] overlay shader not found");
                return;
            }

            const string dir = "Assets/Resources/Fonts";
            Directory.CreateDirectory(dir);
            var chars = Charset();
            var made = new TMP_FontAsset[2];
            for (var i = 0; i < 2; i++)
            {
                var ttf = AssetDatabase.LoadAssetAtPath<Font>(Dir + "/" + Faces[i].asset + ".ttf");
                if (ttf == null)
                {
                    Debug.LogError("[Font] run Create Times New Roman font first");
                    return;
                }

                var path = dir + "/" + (i == 0 ? "TimesNewRoman HudOverlay SDF" : "TimesNewRoman-Bold HudOverlay SDF") + ".asset";
                AssetDatabase.DeleteAsset(path);
                var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(fa, path);
                if (fa.atlasTextures != null)
                    foreach (var t in fa.atlasTextures)
                    {
                        t.name = fa.name + " Atlas";
                        AssetDatabase.AddObjectToAsset(t, fa);
                    }

                fa.material.shader = overlay;
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.TryAddCharacters(chars, out _);
                EditorUtility.SetDirty(fa);
                made[i] = fa;
            }

            var table = made[0].fontWeightTable;
            table[4] = new TMP_FontWeightPair { regularTypeface = made[0], italicTypeface = null };
            table[7] = new TMP_FontWeightPair { regularTypeface = made[1], italicTypeface = null };
            EditorUtility.SetDirty(made[0]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Font] HUD overlay font built in " + dir);
        }

        public static TMP_FontAsset Load()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + "/" + RegularName + ".asset");
        }
    }
}
