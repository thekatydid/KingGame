Shader "Custom/URP/ToonSimple"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.5
        _ShadowSoftness("Shadow Softness", Range(0.001,0.5)) = 0.05
        _ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
        _AmbientStrength("Ambient Strength", Range(0,1)) = 0.25
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.1,8)) = 3
        _RimStrength("Rim Strength", Range(0,1)) = 0.15
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0,0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert_outline
            #pragma fragment frag_outline
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert_outline(Attributes IN)
            {
                Varyings OUT;
                float3 posOS = IN.positionOS.xyz + (IN.normalOS * _OutlineWidth);
                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag_outline(Varyings IN) : SV_Target
            {
                half3 color = MixFog(_OutlineColor.rgb, IN.fogFactor);
                return half4(color, _OutlineColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _ShadowThreshold;
                float _ShadowSoftness;
                float _ShadowStrength;
                float _AmbientStrength;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = nrmInputs.normalWS;
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                OUT.shadowCoord = GetShadowCoord(posInputs);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseTex.rgb * _BaseColor.rgb;
                half alpha = baseTex.a * _BaseColor.a;

                Light mainLight = GetMainLight(IN.shadowCoord);
                float ndl = saturate(dot(n, mainLight.direction));
                float toonStep = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                float litBand = lerp(1.0 - _ShadowStrength, 1.0, toonStep);

                float lightAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 mainLit = albedo * mainLight.color * (litBand * lightAtten);

                half3 additionalLit = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint i = 0u; i < count; i++)
                    {
                        Light l = GetAdditionalLight(i, IN.positionWS);
                        float n2 = saturate(dot(n, l.direction));
                        float s2 = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, n2);
                        float b2 = lerp(1.0 - _ShadowStrength, 1.0, s2);
                        additionalLit += albedo * l.color * (b2 * l.distanceAttenuation * l.shadowAttenuation);
                    }
                #endif

                half3 ambient = albedo * _AmbientStrength;
                float rim = pow(saturate(1.0 - dot(v, n)), _RimPower) * _RimStrength;
                half3 rimCol = _RimColor.rgb * rim;

                half3 color = ambient + mainLit + additionalLit + rimCol;
                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
