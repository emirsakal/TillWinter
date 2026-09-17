// Sky behind the diorama, drawn on a quad parented to the camera (queue Background, no depth): a vertical gradient,
// a sun disc with a soft glow, slow stylised clouds in the upper sky and a pale haze along the horizon. Everything but
// the gradient defaults to off, so a material that sets only _Top/_Bottom stays a plain gradient.
Shader "TillWinter/TW_Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.62, 0.8, 0.95, 1)
        _Bottom ("Bottom", Color) = (0.9, 0.94, 0.9, 1)
        _Curve ("Curve", Range(0.2, 3)) = 1.2
        _SunColor ("Sun (a = strength)", Color) = (1, 0.95, 0.8, 0)
        _SunPos ("Sun position (uv)", Vector) = (0.8, 0.86, 0, 0)
        _SunSize ("Sun radius", Float) = 0.04
        _Aspect ("Width / height", Float) = 0.46
        _Clouds ("Cloud cover", Range(0, 1)) = 0
        _CloudColor ("Cloud colour", Color) = (1, 1, 1, 1)
        _Haze ("Horizon haze", Range(0, 1)) = 0
        _MoonColor ("Moon (a = strength)", Color) = (0.95, 0.96, 1, 0)
        _MoonPos ("Moon position (uv)", Vector) = (0.2, 0.78, 0, 0)
        _Rainbow ("Rainbow strength", Range(0, 1)) = 0
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
                float4 _SunColor;
                float4 _SunPos;
                float _SunSize;
                float _Aspect;
                float _Clouds;
                float4 _CloudColor;
                float _Haze;
                float4 _MoonColor;
                float4 _MoonPos;
                float _Rainbow;
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

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return Noise(p) * 0.5 + Noise(p * 2.03 + 7.1) * 0.28 + Noise(p * 4.1 + 3.7) * 0.14 + Noise(p * 8.3 + 1.9) * 0.08;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = pow(saturate(i.uv.y), _Curve);
                float3 col = lerp(_Bottom.rgb, _Top.rgb, t);

                // Sun: a flat disc with a wide soft glow around it.
                float2 d = (i.uv - _SunPos.xy) * float2(_Aspect, 1.0);
                float r = length(d);
                float disc = 1.0 - smoothstep(_SunSize * 0.92, _SunSize, r);
                float glow = exp(-(r - _SunSize) / (_SunSize * 1.4)) * 0.4;
                col = lerp(col, _SunColor.rgb, saturate(glow * _SunColor.a));
                col = lerp(col, _SunColor.rgb * 1.08 + 0.05, disc * _SunColor.a);

                // Moon: a pale disc with a bite taken out, for cold evenings.
                if (_MoonColor.a > 0.001)
                {
                    float2 md = (i.uv - _MoonPos.xy) * float2(_Aspect, 1.0);
                    float moon = 1.0 - smoothstep(_SunSize * 0.72, _SunSize * 0.8, length(md));
                    float bite = 1.0 - smoothstep(_SunSize * 0.62, _SunSize * 0.7, length(md - float2(_SunSize * 0.32, _SunSize * 0.18)));
                    float glowM = exp(-length(md) / (_SunSize * 1.5)) * 0.25;
                    col = lerp(col, _MoonColor.rgb, saturate(glowM * _MoonColor.a));
                    col = lerp(col, _MoonColor.rgb, saturate(moon - bite) * _MoonColor.a);
                }

                // Rainbow after rain: a faint arc low in the sky.
                if (_Rainbow > 0.001)
                {
                    float2 rd = (i.uv - float2(0.5, 0.42)) * float2(_Aspect, 1.0);
                    float rr = length(rd);
                    float band = saturate(1.0 - abs(rr - 0.36) / 0.035);
                    float hue = saturate((rr - 0.325) / 0.07);
                    float3 rainbow = saturate(float3(abs(hue * 6.0 - 3.0) - 1.0, 2.0 - abs(hue * 6.0 - 2.0), 2.0 - abs(hue * 6.0 - 4.0)));
                    float up = smoothstep(0.42, 0.5, i.uv.y);
                    col = lerp(col, rainbow, band * up * _Rainbow * 0.45);
                }

                // Clouds: puffy bands that only live in the upper sky and drift to the right.
                if (_Clouds > 0.001)
                {
                    float2 sp = float2(i.uv.x * _Aspect * 3.0 - _Time.y * 0.015, i.uv.y * 3.4);
                    // A turned domain keeps value noise from lining up with the screen axes.
                    float2 cp = float2(sp.x * 0.87 - sp.y * 0.5, sp.x * 0.5 + sp.y * 0.87);
                    float n = Fbm(cp);
                    float band = smoothstep(0.58, 0.7, i.uv.y) * (1.0 - smoothstep(0.78, 0.84, i.uv.y)); // between the island and the HUD
                    float cover = smoothstep(0.55 - _Clouds * 0.15, 0.8 - _Clouds * 0.15, n) * band;
                    float shade = smoothstep(0.0, 0.25, n - (0.55 - _Clouds * 0.15)); // lighter cores
                    float3 cloud = lerp(_CloudColor.rgb * 0.9, _CloudColor.rgb, shade);
                    col = lerp(col, cloud, cover * 0.75);
                }

                // Haze: a pale veil rising from the horizon (morning mist, summer heat).
                float haze = _Haze * (1.0 - smoothstep(0.0, 0.7, i.uv.y));
                col = lerp(col, float3(1, 1, 1) * 0.97, haze * 0.7);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
