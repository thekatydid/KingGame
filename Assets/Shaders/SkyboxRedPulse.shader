Shader "Custom/Skybox/RedPulse"
{
    Properties
    {
        _ColorA ("Dark Red", Color) = (0.08, 0.0, 0.0, 1)
        _ColorB ("Mid Red", Color) = (0.55, 0.03, 0.03, 1)
        _ColorC ("Hot Red", Color) = (1.0, 0.2, 0.12, 1)
        _RippleScale ("Ripple Scale", Range(0.5, 12)) = 4.5
        _RippleSpeed ("Ripple Speed", Range(0.0, 4.0)) = 1.2
        _SwirlStrength ("Swirl Strength", Range(0.0, 2.0)) = 0.55
        _NoiseScale ("Noise Scale", Range(0.2, 10.0)) = 2.2
        _Contrast ("Contrast", Range(0.5, 3.0)) = 1.45
        _HorizonGlow ("Horizon Glow", Range(0.0, 2.0)) = 0.75
        _PixelGrid ("Pixel Grid", Range(8.0, 512.0)) = 96.0
        _ColorSteps ("Color Steps", Range(2.0, 16.0)) = 6.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            float _RippleScale;
            float _RippleSpeed;
            float _SwirlStrength;
            float _NoiseScale;
            float _Contrast;
            float _HorizonGlow;
            float _PixelGrid;
            float _ColorSteps;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    v += a * noise2(p);
                    p = p * 2.07 + float2(19.1, 7.3);
                    a *= 0.5;
                }
                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float t = _Time.y * _RippleSpeed;

                // Projected coordinates for fluid-like sky distortion.
                float2 p = d.xz / (abs(d.y) + 0.35);
                float grid = max(1.0, _PixelGrid);
                p = floor(p * grid) / grid;

                float radius = length(p);
                float angle = atan2(p.y, p.x);
                angle += sin(radius * 2.3 - t * 1.8) * _SwirlStrength;
                float2 swirl = float2(cos(angle), sin(angle)) * radius;

                float n = fbm(swirl * _NoiseScale + float2(t * 0.35, -t * 0.27));
                float ripple = sin(radius * _RippleScale - t * 5.2 + n * 5.5) * 0.5 + 0.5;
                float pulse = sin(t * 1.7 + n * 6.0) * 0.5 + 0.5;

                float mask = saturate(pow(ripple * 0.65 + pulse * 0.35, _Contrast));
                float horizon = saturate(1.0 - abs(d.y));
                float glow = pow(horizon, 2.2) * _HorizonGlow;

                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, mask);
                col = lerp(col, _ColorC.rgb, saturate(mask * 0.8 + glow));
                col += _ColorC.rgb * glow * 0.28;
                float steps = max(2.0, _ColorSteps);
                col = floor(saturate(col) * steps) / steps;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
