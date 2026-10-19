// 체크포인트·출구 표시용 홀로그램 빛기둥. 정사각형 스프라이트를 세로로 늘려 쓴다 (폭 1.5, 높이 5 유닛 정도).
//   아래: 어두운 받침대 + 빛나는 띠
//   가운데: 빛기둥 — 대기 중에는 가늘고 끊기며 깜빡이고, 활성화되면 굵고 밝아진다
//   위로 흐르는 고리 무늬, 올라가는 빛 알갱이, 활성화 순간 위로 치솟는 펄스
// 모든 계산은 아트 픽셀(16px = 1유닛) 격자에서 한다 — 오브젝트 크기를 행렬에서 읽어 UV 를 픽셀 단위로 끊는다.
// 시간은 게임 시간(_Time)을 써서 히트스톱·일시정지 때 같이 멈춘다.
Shader "Platformer/Beacon"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite (unused)", 2D) = "white" {}
        [HDR] _IdleColor("Idle Color", Color) = (2.2, 0.35, 1.2, 1)
        [HDR] _ActiveColor("Active Color", Color) = (0.4, 2.4, 2.8, 1)
        _Activation("Activation", Range(0, 1)) = 0
        _Pulse("Pulse", Range(0, 1)) = 0
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
            #include "Include/FXCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _IdleColor;
                half4 _ActiveColor;
                float _Activation;
                float _Pulse;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 sizePixels : TEXCOORD1; half4 color : COLOR; float seed : TEXCOORD2; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                // 오브젝트 스케일 = 행렬의 열 길이. 스프라이트가 1유닛이므로 스케일 × 16 = 아트 픽셀 수
                float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                output.sizePixels = max(float2(scaleX, scaleY) * FX_PIXELS_PER_UNIT, 1.0);
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                output.seed = Hash21(floor(originWS.xy));
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 sizePx = input.sizePixels;
                float2 pixel = floor(input.uv * sizePx);         // 아트 픽셀 좌표 (0 ~ 크기-1)
                float x = pixel.x - (sizePx.x - 1.0) * 0.5;       // 가운데 기준 가로 픽셀
                float y = pixel.y;
                float height = sizePx.y;
                float t = _Time.y + input.seed * 10.0;
                float activation = _Activation;
                half3 color = lerp(_IdleColor.rgb, _ActiveColor.rgb, activation) * input.color.rgb;

                // 받침대: 아래 5픽셀, 위로 갈수록 좁아지는 사다리꼴 + 빛나는 띠 한 줄
                const float baseHeight = 5.0;
                if (y < baseHeight)
                {
                    float halfWidth = sizePx.x * 0.5 - (y * 0.6);
                    if (abs(x) > halfWidth)
                        return 0;
                    if (y == 2.0)
                        return half4(color * (0.8 + 0.6 * activation), 1.0);
                    return half4(0.06, 0.07, 0.11, 1.0);
                }

                float v = (y - baseHeight) / max(height - baseHeight, 1.0); // 기둥 높이 0~1
                float fade = pow(saturate(1.0 - v), 1.5);

                // 기둥 굵기: 대기 1px 심 → 활성 2px 심 + 넓은 후광
                float core = abs(x) <= 0.5 + activation ? 1.0 : 0.0;
                float halo = saturate(1.0 - abs(x) / (2.0 + activation * 4.0));

                // 대기 중엔 기둥이 군데군데 끊기고 깜빡인다 (고장 난 홀로그램)
                float broken = step(0.35, Hash21(float2(floor(y / 3.0), floor(t * 8.0))));
                float presence = lerp(broken * (0.55 + 0.45 * step(0.5, frac(t * 1.3))), 1.0, activation);

                // 위로 흐르는 고리: 6픽셀 간격의 밝은 줄
                float ring = step(fmod(y - floor(t * 18.0), 6.0), 0.5);
                // 활성화 펄스: 아래에서 위로 치솟는 밝은 띠
                float pulseY = (1.0 - _Pulse) * height;
                float pulse = _Pulse * saturate(1.0 - abs(y - pulseY) / 4.0);
                // 올라가는 빛 알갱이: 가로 픽셀마다 다른 속도의 점 하나
                float speed = 10.0 + Hash11(x * 3.7 + input.seed) * 14.0;
                float sparkY = fmod(t * speed + Hash11(x + 11.0) * height, height);
                float spark = abs(y - sparkY) < 0.5 && abs(x) < 3.0 + activation * 2.0 && Hash11(x * 1.3 + 2.0) > 0.4 ? 1.0 : 0.0;

                float alpha = (core * 0.9 + halo * 0.35 + ring * halo * 0.4) * presence * fade;
                alpha = max(alpha, spark * fade * (0.5 + activation * 0.5));
                alpha = max(alpha, pulse * halo);
                half3 lit = color * (1.0 + core * 0.8 + ring * 0.6 + pulse * 2.0);
                return half4(lit, saturate(alpha) * input.color.a);
            }
            ENDHLSL
        }
    }
}
