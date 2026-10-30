#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>Login screen: the runtime object plus the four materials it needs (materials stay scene assets, no runtime Shader.Find).</summary>
    public static partial class PresentableSceneBuilder
    {
        private static LoginScreen BuildLogin(Transform flow, PresentableArt art)
        {
            var go = Child("LoginScreen", flow);
            var login = go.AddComponent<LoginScreen>();

            var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Login/Login_Backdrop.png");
            if (backdrop == null)
                Debug.LogWarning("[Presentable] Missing Assets/Resources/Login/Login_Backdrop.png (run Tools/generate_login_ui.py)");
            var backdropMat = Mat("LoginBackdrop", Color.white, tex: backdrop, unlit: true);
            backdropMat.SetFloat("_Cull", 0f);
            var domeMat = Mat("LoginDome", new Color(0.02f, 0.07f, 0.1f), unlit: true);
            domeMat.SetFloat("_Cull", 0f);
            var atomMat = Mat("LoginAtom", Color.white, 0.65f, 0.1f);

            const string glowPath = GenDir + "/M_LoginGlow.mat";
            var glow = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
            if (glow == null)
            {
                glow = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(glow, glowPath);
            }

            glow.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            glow.SetFloat("_Surface", 1f);
            glow.SetFloat("_Blend", 2f);
            glow.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            glow.SetFloat("_ZWrite", 0f);
            glow.renderQueue = 3000;
            glow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (art != null && art.radialGlow != null)
                glow.SetTexture("_BaseMap", art.radialGlow.texture);
            glow.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(glow);

            SetRef(login, "art", art);
            SetRef(login, "backdropMat", backdropMat);
            SetRef(login, "domeMat", domeMat);
            SetRef(login, "atomMat", atomMat);
            SetRef(login, "glowMat", glow);
            return login;
        }
    }
}
#endif
