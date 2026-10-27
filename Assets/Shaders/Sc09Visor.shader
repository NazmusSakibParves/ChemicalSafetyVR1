// Scenario 9: chemical droplets on the face shield, drawn on a quad that rides on the camera (URP, no grab pass: Quest friendly).
// The mask texture is made at runtime by Sc09Visor: R = droplet body, G = highlight, B = how well the droplet sticks (0 washes off first).
// _Clean 0..1 removes droplets (lowest B first), _Wet adds a water film with running streaks, _Slide lets the remaining drops creep down.
Shader "ChemicalSafety/Sc09Visor"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "black" {}
        _Tint ("Liquid colour", Color) = (0.30, 0.04, 0.46, 1)
        _Water ("Water colour", Color) = (0.72, 0.84, 0.95, 1)
        _Strength ("Overall alpha", Range(0, 1)) = 1
        _Clean ("Clean", Range(0, 1)) = 0
        _Wet ("Wet film", Range(0, 1)) = 0
        _Slide ("Slide", Float) = 0
        _Flash ("Splash flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _Water;
                half _Strength;
                half _Clean;
                half _Wet;
                float _Slide;
                half _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            float hash(float n) { return frac(sin(n) * 43758.5453); }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                // the drops that remain creep downwards a little, the big sticky ones less
                half4 m0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                uv.y += _Slide * (0.02 + 0.10 * (1.0 - m0.b));
                half4 m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half keep = smoothstep(_Clean - 0.04, _Clean + 0.06, m.b);
                half body = m.r * keep;
                half hi = saturate(m.g - 0.5) * 2.0 * keep;

                // water film: soft vertical streaks that run down, strongest at the start of the wash
                float col = floor(i.uv.x * 90.0);
                float speed = 0.5 + hash(col) * 0.8;
                float streak = frac(i.uv.y * (1.0 + hash(col + 3.0) * 2.0) + _Time.y * speed * _Wet);
                half film = _Wet * smoothstep(0.0, 0.5, streak) * (1.0 - smoothstep(0.5, 1.0, streak)) * (0.10 + 0.22 * hash(col + 7.0));
                half cleanFade = 1.0 - _Clean;

                // purple drops lose their colour as the water takes it
                half3 liquid = lerp(_Tint.rgb, _Water.rgb, saturate(_Wet * 0.45 + _Clean * 1.3));
                liquid = liquid * (0.60 + 0.40 * (1.0 - m.g * 0.5)) + hi * 0.22;
                half a = saturate(body * (0.80 + 0.2 * hi) + film * cleanFade) * _Strength;
                half3 rgb = lerp(liquid, _Water.rgb, saturate(film * 2.0 * (1.0 - body)));
                // splash flash: a short purple tint over the whole shield when the liquid hits
                a = saturate(a + _Flash * 0.30);
                rgb = lerp(rgb, _Tint.rgb, _Flash * (1.0 - body));
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
