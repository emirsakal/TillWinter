// Till Winter particle: an unlit, alpha-blended billboard that multiplies a soft mask by the particle's own colour.
// The feel pass built every effect from opaque primitives, which read as confetti at any size; sparkles, splashes and
// dust want a soft edge. Hand-written like the rest, so no stock shader ends up on a material under Assets/Art.
Shader "TillWinter/TW_Particle"
{
    Properties
    {
        _BaseMap ("Particle mask", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        /// Brightens the core so a sparkle still reads against a lit field without a separate additive pass.
        _Boost ("Core boost", Range(1, 3)) = 1.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "PreviewType" = "Plane" }

        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Boost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 c = mask * _BaseColor * i.color;
                c.rgb *= _Boost;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
