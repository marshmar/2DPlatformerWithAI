// 화면 공간 빌보드: 조준점·대상 마커용.
// 정사영 2D 에서도 카메라가 흔들리며 기울거나(쉐이크 롤) 확대되면 월드 오브젝트는 같이 기울고 커진다.
// 정점 셰이더에서 오브젝트 중심만 클립 공간으로 보낸 뒤, 꼭짓점을 "화면 픽셀" 단위로 다시 펼쳐서
// 항상 똑바로, 화면에서 같은 아트 픽셀 크기로 보이게 한다 (오브젝트의 회전·스케일은 무시).
Shader "Platformer/Billboard"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0.4, 2, 2.4, 1)
        _SizePixels("Size (art pixels)", Float) = 13
        _Shape("Shape (0 = reticle, 1 = target)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

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
                float _SizePixels;
                float _Shape;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float4 centerCS = TransformWorldToHClip(centerWS);

                // 아트 픽셀 1개가 화면 픽셀 몇 개인지: 화면 높이 / (세로로 보이는 유닛 수 × 16)
                float screenPixelsPerArtPixel = _ScreenParams.y / (unity_OrthoParams.y * 2.0 * FX_PIXELS_PER_UNIT);
                float2 corner = input.uv * 2.0 - 1.0;
                float2 offsetPixels = corner * _SizePixels * 0.5 * screenPixelsPerArtPixel;
                // 클립 공간은 -1..1 = 화면 전체 → 픽셀을 클립 단위로 (w 를 곱해 원근 나눗셈을 상쇄)
                centerCS.xy += offsetPixels * 2.0 / _ScreenParams.xy * centerCS.w;

                output.positionCS = centerCS;
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // 아트 픽셀 격자로 끊어서 모양을 그린다
                float n = max(_SizePixels, 1.0);
                float2 p = (floor(input.uv * n) + 0.5) / n * 2.0 - 1.0;
                float r = length(p);
                float pixel = 2.0 / n;
                half shape;

                if (_Shape < 0.5)
                {
                    // 조준점: 끊어진 원 4조각이 천천히 돌고, 가운데 점
                    float angle = atan2(p.y, p.x) + _FX_UnscaledTime * 1.5;
                    half ring = step(abs(r - 0.8), pixel * 0.75) * step(0.25, frac(angle / (PI * 0.5)));
                    half centerDot = step(r, pixel * 0.8);
                    shape = saturate(ring + centerDot);
                }
                else
                {
                    // 대상 마커: 네 모서리 꺾쇠가 숨 쉬듯 조였다 풀린다
                    float breathe = 0.85 + 0.1 * sin(_FX_UnscaledTime * 8.0);
                    float2 q = abs(p) / breathe;
                    half edge = step(q.x, 1.0) * step(q.y, 1.0) * (step(1.0 - pixel * 1.2, q.x) + step(1.0 - pixel * 1.2, q.y));
                    half corner = step(0.55, q.x) * step(0.55, q.y);
                    shape = saturate(edge) * corner;
                }

                if (shape <= 0.0)
                    discard;
                return half4(_Color.rgb, _Color.a);
            }
            ENDHLSL
        }
    }
}
