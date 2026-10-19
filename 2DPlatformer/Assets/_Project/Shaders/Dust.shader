// 먼지 파티클. 파티클 시스템의 커스텀 버텍스 스트림으로 수명 비율(AgePercent)과 고정 난수(StableRandom.x)를 받는다:
//   TEXCOORD0.xy = UV, TEXCOORD0.z = 수명 비율 0→1, TEXCOORD0.w = 파티클마다 다른 난수
//   - 모양: 6×6 픽셀 격자로 끊은 원이 수명에 따라 작아지고, 노이즈로 가장자리부터 갉아먹힌다
//   - 정점을 아트 픽셀 격자에 맞춰서 파티클이 반 픽셀에 걸쳐 흐려지지 않게 한다
//   - 2D 라이트를 받는다 — 네온 옆에서 일어난 먼지는 그 색으로 물든다
Shader "Platformer/Dust"
{
    Properties
    {
        _GridSize("Pixel Grid", Float) = 6
        [HDR] _Emission("Emission", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"
            #include "Include/ShapeLights2D.hlsl"
            #include "Include/FXCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _GridSize;
                half4 _Emission;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4  color      : COLOR;
                float4 uvAgeRandom : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float4 uvAgeRandom : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                // 파티클 정점은 이미 월드 좌표(시뮬레이션 공간 World) — 아트 픽셀 격자에 스냅
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                positionWS.xy = round(positionWS.xy * FX_PIXELS_PER_UNIT) / FX_PIXELS_PER_UNIT;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.uvAgeRandom = input.uvAgeRandom;
                output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uvAgeRandom.xy;
                float age = input.uvAgeRandom.z;
                float random = input.uvAgeRandom.w;

                float2 cell = floor(uv * _GridSize);
                float2 p = (cell + 0.5) / _GridSize * 2.0 - 1.0;
                float radius = lerp(0.95, 0.25, age);
                float erosion = Hash21(cell + floor(random * 97.0)) * 0.6;
                if (length(p) > radius - erosion * age)
                    discard;

                half3 color = ApplyShapeLights(input.color.rgb, input.lightingUV, half4(1, 1, 1, 1)) + _Emission.rgb;
                return half4(color, input.color.a * (1.0 - age * age));
            }
            ENDHLSL
        }
    }
}
