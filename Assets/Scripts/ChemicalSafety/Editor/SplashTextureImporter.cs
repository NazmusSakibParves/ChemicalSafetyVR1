#if UNITY_EDITOR
using UnityEditor;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// The two credits-splash logos (Resources/Ui/Splash): plain textures, no mipmaps (shown flat at one size), clamped, and ASTC 6x6 on Android
    /// so the two pictures add only a few hundred KB to the APK.
    /// </summary>
    public sealed class SplashTextureImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Ui/Splash/";

        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
                return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.isReadable = false;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(android);
        }
    }
}
#endif
