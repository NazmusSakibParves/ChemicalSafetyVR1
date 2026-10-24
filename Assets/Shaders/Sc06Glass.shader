// Cheap lab glass for Quest (URP, forward): clear in the middle, bright and reflective at grazing angles, a faint tint.
// Two-sided so the inside wall of a beaker or jar shows through the front wall. No grab pass, no refraction.
Shader "ChemicalSafety/Sc06Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.82, 0.92, 0.95, 1)
        _Clear ("Alpha when seen straight on", Range(0, 1)) = 0.07
        _Edge ("Alpha at grazing angles", Range(0, 1)) = 0.75
        _Power ("Fresnel power", Range(0.5, 6)) = 2.6
        _Reflect ("Reflection strength", Range(0, 2)) = 1.0
        _Spec ("Highlight strength", Range(0, 4)) = 1.6
        _Gloss ("Highlight sharpness", Range(8, 512)) = 160
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Clear;
                half _Edge;
                half _Power;
                half _Reflect;
                half _Spec;
                half _Gloss;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i, bool face : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                n = face ? n : -n;
                float3 v = normalize(i.viewWS);
                half ndv = saturate(dot(n, v));
                half fres = pow(1.0 - ndv, _Power);
                float3 r = reflect(-v, n);
                half3 env = DecodeHDREnvironment(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, r, 1.0), unity_SpecCube0_HDR);
                // the lab has no sun: the highlight comes from the ceiling light panels (a fixed direction from above and slightly behind the viewer)
                float3 h = normalize(normalize(float3(0.25, 1.0, 0.35)) + v);
                float3 h2 = normalize(normalize(float3(-0.4, 1.0, -0.2)) + v);
                half spec = (pow(saturate(dot(n, h)), _Gloss) + 0.6 * pow(saturate(dot(n, h2)), _Gloss)) * _Spec;
                half band = 4.0 * fres * (1.0 - fres);
                half3 col = (_Tint.rgb * 0.5 + env * _Reflect * (0.4 + fres)) * (1.0 - 0.38 * band) + spec;
                half a = saturate(lerp(_Clear, _Edge, fres) + spec * 0.35 + 0.14 * band);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
