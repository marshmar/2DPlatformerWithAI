// 잔상(유령). 원본 스프라이트의 실루엣만 가져와 단색으로 칠하고, 짝수 줄을 어둡게 해서 주사선 같은 디지털 느낌을 낸다.
// 더하기 블렌드라 겹칠수록 밝아지고, HDR 색이라 Bloom 이 번진다.
Shader "Platformer/Afterimage"
{
    Properties
    {
        _MainTex("Sprite", 2D) = "white" {}
        [HDR] _GhostColor("Ghost Color (set by script)", Color) = (0.3, 2.4, 3, 1)
        _Fade("Fade (set by script)", Range(0, 1)) = 1
        _ScanlineDarkness("Scanline Darkness", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _GhostColor;
                half  _Fade;
                half  _ScanlineDarkness;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                if (alpha <= 0.0)
                    discard;
                float width, height;
                _MainTex.GetDimensions(width, height);
                // 스프라이트의 픽셀 행 기준 주사선 — 화면 해상도와 무관하게 아트 픽셀 한 줄씩
                half scanline = fmod(floor(input.uv.y * height), 2.0) < 1.0 ? 1.0 : 1.0 - _ScanlineDarkness;
                return half4(_GhostColor.rgb * alpha * _Fade * scanline, 0);
            }
            ENDHLSL
        }
    }
}
