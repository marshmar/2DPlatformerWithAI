// 갈고리 체인. 텍스처 없이 UV 로 고리 무늬를 그린다:
//   길이 방향(uv.x × 체인 길이)을 아트 픽셀 단위로 끊고, 4px 마다 "O" 고리(정면)와 "—" 고리(옆면)를 번갈아 그린다.
//   폭 방향은 3px (LineRenderer 폭 = 3/16 유닛).
// 팽팽할 때(_Tension) 시안 에너지가 체인을 따라 흐르고, HDR 이라 Bloom 이 번진다.
Shader "Platformer/Chain"
{
    Properties
    {
        [HDR] _MetalColor("Metal", Color) = (0.55, 0.6, 0.72, 1)
        [HDR] _HighlightColor("Highlight", Color) = (1.1, 1.15, 1.3, 1)
        [HDR] _EnergyColor("Energy", Color) = (0.4, 2.8, 3.2, 1)
        _ChainLength("Chain Length (set by script)", Float) = 1
        _Tension("Tension (set by script)", Range(0, 1)) = 0
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
                half4 _MetalColor;
                half4 _HighlightColor;
                half4 _EnergyColor;
                float _ChainLength;
                half  _Tension;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float along = floor(input.uv.x * _ChainLength * FX_PIXELS_PER_UNIT); // 아트 픽셀 단위
                int across = (int)clamp(floor(input.uv.y * 3.0), 0.0, 2.0);           // 0, 1, 2
                int link = (int)floor(along / 4.0);
                int local = (int)fmod(along, 4.0);

                bool ring = (link & 1) == 0;
                bool filled;
                if (ring)
                    filled = across == 1 ? (local == 0 || local == 3) : (local == 1 || local == 2); // "O" 의 테두리
                else
                    filled = across == 1;                                                           // 옆에서 본 "—"
                if (!filled)
                    discard;

                half3 color = across == 2 ? _HighlightColor.rgb : _MetalColor.rgb; // 위쪽 줄은 밝게
                // 팽팽할 때 에너지 펄스가 손 → 갈고리 끝 방향으로 흐른다
                half pulse = 0.5 + 0.5 * sin(along * 0.45 - _FX_UnscaledTime * 22.0);
                color += _EnergyColor.rgb * _Tension * pulse * pulse;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
