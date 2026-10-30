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
        public override uint GetVersion() => 3;

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
            ti.maxTextureSize = 2048;
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
                    settings.maxTextureSize = 2048;
                    settings.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SetPlatformTextureSettings(settings);
                }

                return;
            }

            ti.textureCompression = isBn ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        }
    }
}
#endif
