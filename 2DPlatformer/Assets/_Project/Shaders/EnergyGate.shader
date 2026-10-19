// 아레나 봉쇄 게이트(에너지 장벽). 정사각형 스프라이트를 게이트 칸 크기로 늘려 쓴다 (폭 1, 높이 N 유닛).
//   위아래 끝: 장벽을 쏘는 발생기 (밝은 막대)
//   가운데: 위로 흐르는 사선 격자 + 노이즈로 일렁이는 막. 가장자리 1px 는 밝은 경계선
//   열림(_Open 0→1): 노이즈 문턱으로 픽셀이 타들어 가듯 사라지고, 사라지는 경계가 하얗게 빛난다
Shader "Platformer/EnergyGate"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite (unused)", 2D) = "white" {}
        [HDR] _Color("Field Color", Color) = (2.6, 0.3, 0.9, 1)
        [HDR] _EdgeColor("Dissolve Edge", Color) = (4, 3, 4, 1)
        _Open("Open", Range(0, 1)) = 0
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
                half4 _EdgeColor;
                float _Open;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 sizePixels : TEXCOORD1; half4 color : COLOR; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                output.sizePixels = max(float2(scaleX, scaleY) * FX_PIXELS_PER_UNIT, 1.0);
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 sizePx = input.sizePixels;
                float2 pixel = floor(input.uv * sizePx);
                float t = _Time.y;

                // 열림: 픽셀마다 정해진 노이즈 값이 문턱(_Open)보다 작으면 사라진다. 아래에서 위로 조금 먼저 타들어 가게 높이를 섞는다
                float noise = ValueNoise(pixel * 0.45) * 0.75 + (pixel.y / sizePx.y) * 0.25;
                float threshold = _Open * 1.15;
                if (noise < threshold)
                    return 0;
                bool burning = noise < threshold + 0.08 && _Open > 0.0;

                half3 color = _Color.rgb * input.color.rgb;
                bool cap = pixel.y < 2.0 || pixel.y >= sizePx.y - 2.0;
                bool rim = pixel.x < 1.0 || pixel.x >= sizePx.x - 1.0;

                half4 result;
                if (cap)
                {
                    // 발생기: 어두운 금속에 빛나는 점 하나가 왔다 갔다
                    float lamp = abs(pixel.x - floor((sin(t * 3.0) * 0.5 + 0.5) * (sizePx.x - 1.0))) < 1.0 ? 1.0 : 0.0;
                    result = half4(lerp(half3(0.08, 0.08, 0.12), color * 1.5, lamp * 0.8 + 0.2 * step(pixel.y, 0.5)), 1.0);
                }
                else
                {
                    // 위로 흐르는 사선 격자 두 겹 + 천천히 일렁이는 노이즈 막
                    float scroll = floor(t * 12.0);
                    float diagonalA = step(fmod(pixel.x + pixel.y - scroll + 400.0, 5.0), 0.5);
                    float diagonalB = step(fmod(pixel.y - pixel.x - scroll + 400.0, 5.0), 0.5);
                    float shimmer = ValueNoise(float2(pixel.x * 0.3, pixel.y * 0.15 - t * 2.0));
                    float alpha = 0.18 + shimmer * 0.25 + (diagonalA + diagonalB) * 0.22;
                    float brightness = 1.0 + (diagonalA + diagonalB) * 0.5;
                    if (rim)
                    {
                        alpha = 0.95;
                        brightness = 1.8;
                    }
                    // 가끔 장벽 전체가 지지직 깜빡인다
                    alpha *= 0.75 + 0.25 * step(0.15, Hash11(floor(t * 15.0)));
                    result = half4(color * brightness, saturate(alpha));
                }

                if (burning)
                    result = half4(_EdgeColor.rgb, 1.0);
                result.a *= input.color.a;
                return result;
            }
            ENDHLSL
        }
    }
}
