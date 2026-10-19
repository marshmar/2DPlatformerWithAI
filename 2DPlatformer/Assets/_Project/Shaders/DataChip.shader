// 수집품 데이터 칩. 1유닛(16px) 정사각형 하나로 그린다:
//   정점: 위아래로 둥실둥실 (오브젝트 위치로 위상을 달리해서 여러 개가 똑같이 움직이지 않게)
//   픽셀: 세로축으로 도는 마름모 — 가로 폭을 |cos| 로 줄였다 늘려 3D 회전처럼 보이게 하고,
//         테두리는 밝게, 안쪽엔 회로 무늬, 둘레엔 은은한 후광, 가끔 꼭대기에서 반짝
Shader "Platformer/DataChip"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite (unused)", 2D) = "white" {}
        [HDR] _Color("Chip Color", Color) = (2.6, 1.7, 0.4, 1)
        _BobHeight("Bob Height (units)", Float) = 0.15
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
                half4 _Color;
                float _BobHeight;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; half4 color : COLOR; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float phase = Hash21(floor(originWS.xy * 2.0)) * 6.2831;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                // 둥실거림도 픽셀 단위로 끊는다 — 매끈하게 미끄러지면 주변 픽셀아트와 따로 논다
                positionWS.y += QuantizeToPixels(sin(_Time.y * 2.4 + phase) * _BobHeight, FX_PIXELS_PER_UNIT);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.phase = phase;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                const float size = 16.0;
                float2 pixel = floor(input.uv * size);
                float2 q = pixel - 7.5;                           // 가운데 기준 (-7.5 ~ 7.5)
                float t = _Time.y;
                half3 color = _Color.rgb * input.color.rgb;

                // 회전: 가로 폭 배율. 너무 얇아지지 않게 하한을 둔다 (옆면이 보이는 느낌)
                float turn = cos(t * 2.2 + input.phase);
                float widthScale = max(abs(turn), 0.18);
                float radius = 6.0;
                float d = abs(q.x) / widthScale + abs(q.y);       // 세로로 긴 마름모의 L1 거리
                float glow = saturate(1.0 - length(q) / 8.0);

                if (d > radius + 0.5)
                {
                    // 후광 + 꼭대기 반짝임 (가끔 십자 모양으로)
                    float sparkle = step(0.93, frac(t * 0.7 + input.phase)) * (abs(q.x) < 0.6 && abs(q.y - 6.5) < 1.5 ? 1.0 : 0.0);
                    return half4(color, saturate(glow * glow * 0.35 + sparkle));
                }

                bool rim = d > radius - 1.0;
                // 앞면/뒷면에 따라 밝기 차이 — 돌 때 반짝이는 느낌
                float face = turn > 0.0 ? 1.0 : 0.7;
                half3 body = color * 0.35 * face;
                // 회로 무늬: 가운데 세로선 + 가로 가지
                if (abs(q.x) < 0.6 * widthScale + 0.1 || (abs(q.y) < 0.6 && abs(q.x) < 2.5 * widthScale))
                    body = color * 0.9 * face;
                if (rim)
                    body = color * (1.3 + 0.5 * face);
                return half4(body, input.color.a);
            }
            ENDHLSL
        }
    }
}
