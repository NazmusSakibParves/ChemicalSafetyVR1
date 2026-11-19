#if UNITY_EDITOR
using UnityEditor;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Textures under Resources/BnText (rendered Bangla text) and Resources/Login (login screen art) import as crisp,
    /// uncompressed sprites with transparency, so Tools/render_content_bn.py needs no .meta files.
    /// </summary>
    public sealed class ContentTextureImporter : AssetPostprocessor
    {
        private const string BnFolder = "Assets/Resources/BnText/";
        private const string LoginFolder = "Assets/Resources/Login/";
        private const string AutoFolder = "Assets/Resources/BnAuto/";

        // bump when the settings below change so Unity reimports every content texture
        public override uint GetVersion() => 4;

        // BnAuto keys (non-bold, bold) of the Scenario 10 reasons whose wide rungs must not be cut to 2048 px
        private static readonly string[] FullWidthKeys =
        {
            "d3eb0178d657a81a", "84aa31ba179e9dc6", "0fc57b5c8b80511b", "6b114fc3b3ba7c17",
            "24b0b87900f589f4", "e6cf8120cc16e6d4", "575734bbace21c19", "6a5de0ae6de0d565"
        };

        private static bool IsFullWidth(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (var k in FullWidthKeys)
                if (name.StartsWith(k))
                    return true;
            return false;
        }

        private void OnPreprocessTexture()
        {
            var isBn = assetPath.StartsWith(BnFolder) || assetPath.StartsWith(AutoFolder);
            if (!isBn && !assetPath.StartsWith(LoginFolder))
                return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = assetPath.StartsWith(AutoFolder); // text images are shown smaller than they are drawn: mips keep them clean
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = assetPath.StartsWith(AutoFolder) ? UnityEngine.FilterMode.Trilinear : UnityEngine.FilterMode.Bilinear;
            var maxSize = 2048;
            if (assetPath.StartsWith(AutoFolder) && IsFullWidth(assetPath))
                maxSize = 4096; // client 2026-11-19: these wide Bangla rungs keep their true size (a one-line reason in the Scenario 10 box)
            ti.maxTextureSize = maxSize;
            if (assetPath.StartsWith(AutoFolder))
            {
                // Bangla text is thin strokes: block compression (DXT / ASTC) turns them into visible blocks. The images carry only
                // alpha, so Alpha8 (1 byte per pixel, no compression) is both crisp and small.
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                foreach (var platform in new[] { "DefaultTexturePlatform", "Android", "Standalone" })
                {
                    var settings = ti.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.format = TextureImporterFormat.Alpha8;
                    settings.maxTextureSize = maxSize;
                    settings.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SetPlatformTextureSettings(settings);
                }

                return;
            }

            // Partner / BMQAS badges are small on screen: block compression makes them muddy.
            var crispLogo = assetPath.EndsWith("/Login_Bmqas.png") || assetPath.EndsWith("/Login_Logos.png");
            if (isBn || crispLogo)
            {
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                foreach (var platform in new[] { "DefaultTexturePlatform", "Android", "Standalone" })
                {
                    var settings = ti.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.format = TextureImporterFormat.RGBA32;
                    settings.maxTextureSize = 2048;
                    settings.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SetPlatformTextureSettings(settings);
                }

                return;
            }

            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
#endif
