// Soft vertical gradient behind the diorama. Drawn on a quad parented to the camera (queue Background, no depth).
Shader "TillWinter/TW_Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.62, 0.8, 0.95, 1)
        _Bottom ("Bottom", Color) = (0.9, 0.94, 0.9, 1)
        _Curve ("Curve", Range(0.2, 3)) = 1.2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Background" "Queue" = "Background" }
        Pass
        {
            Name "Sky"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Top;
                float4 _Bottom;
                float _Curve;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = pow(saturate(i.uv.y), _Curve);
                return half4(lerp(_Bottom.rgb, _Top.rgb, t), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
