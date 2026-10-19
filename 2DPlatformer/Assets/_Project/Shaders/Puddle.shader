// 젖은 바닥 반사. 바닥 윗면에 깐 쿼드가, 자기 윗변(= 바닥선)을 거울축으로 화면을 뒤집어 그린다.
//   1) 화면 좌표의 y 를 바닥선 기준으로 뒤집으면 바닥 위 장면(캐릭터·네온)이 바닥 아래로 비친다
//      소스는 Camera Sorting Layer Texture (Player 레이어까지 그린 화면) — 그래서 이 쿼드는 그보다 뒤 레이어에 둔다
//   2) 깊이(바닥선에서 아래로 내려간 거리)에 따라 흐려지고, 물결 노이즈로 좌우가 일렁인다
//   3) 물웅덩이 모양은 월드 x 를 픽셀 단위로 끊은 노이즈로 정한다 — 바닥 전체가 아니라 군데군데 고인 물
Shader "Platformer/Puddle"
{
    Properties
    {
        _Tint("Tint", Color) = (0.65, 0.75, 1, 1)
        _Reflectivity("Reflectivity", Range(0, 1)) = 0.7
        _Depth("Fade Depth (units)", Float) = 1
        _Ripple("Ripple Strength", Range(0, 0.02)) = 0.004
        _PuddleCoverage("Puddle Coverage", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/FXCommon.hlsl"

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half  _Reflectivity;
                float _Depth;
                half  _Ripple;
                half  _PuddleCoverage;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 mirror     : TEXCOORD2; // x = 바닥선의 화면 y, y = 바닥선의 월드 y
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.screenPos = ComputeScreenPos(output.positionCS);

                // 쿼드 윗변 = 바닥선 (스프라이트 로컬 y 0.5)
                float3 topWS = TransformObjectToWorld(float3(0, 0.5, 0));
                float4 topCS = TransformWorldToHClip(topWS);
                float4 topScreen = ComputeScreenPos(topCS);
                output.mirror = float2(topScreen.y / topScreen.w, topWS.y);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 pixel = QuantizeToPixels(input.positionWS.xy, FX_PIXELS_PER_UNIT);
                float depth = input.mirror.y - pixel.y; // 바닥선에서 아래로 몇 유닛

                // 고인 물 모양: x 방향으로 길쭉한 덩어리들
                half puddle = step(1.0 - _PuddleCoverage, ValueNoise(float2(pixel.x * 0.45, 7.3)));
                half fade = saturate(1.0 - depth / _Depth);
                half alpha = puddle * fade * _Reflectivity;
                if (alpha <= 0.0)
                    discard;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 reflected = float2(screenUV.x, 2.0 * input.mirror.x - screenUV.y);
                // 물결: 깊을수록 크게 일렁인다. 시간은 게임 시간(_Time) — 슬로우 땐 물결도 느려진다
                reflected.x += (ValueNoise(float2(pixel.x * 3.0, _Time.y * 2.5 + depth * 10.0)) - 0.5) * _Ripple * (1.0 + depth * 3.0);

                half3 color = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, reflected).rgb * _Tint.rgb;
                // 짝수 줄을 살짝 어둡게 — 물 표면의 가로 결
                color *= fmod(floor(pixel.y * FX_PIXELS_PER_UNIT), 2.0) < 1.0 ? 1.0 : 0.75;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
