// 타격 불꽃. 쿼드 위에 극좌표로 4갈래 빛줄기 + 퍼지는 링을 그린다. _Progress 가 0→1 로 가며 커지고 사라진다.
// 더하기 블렌드 + HDR 이라 Bloom 이 번진다.
Shader "Platformer/HitSpark"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (3, 2.6, 2, 1)
        [HDR] _RingColor("Ring Color", Color) = (0.6, 2.4, 3, 1)
        _Progress("Progress (set by script)", Range(0, 1)) = 0.2
        _PixelResolution("Pixel Resolution", Float) = 24
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
            #include "Include/FXCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RingColor;
                half  _Progress;
                float _PixelResolution;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = QuantizeUV(input.uv, _PixelResolution.xx) * 2.0 - 1.0;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float progress = saturate(_Progress);

                // 4갈래 빛줄기: cos 의 높은 거듭제곱 = 특정 각도에서만 뾰족하게 남는다
                float spikes = pow(abs(cos(angle * 2.0)), 24.0);
                float reach = lerp(0.35, 1.0, sqrt(progress));
                float star = spikes * saturate(1.0 - r / reach) + saturate(1.0 - r / (reach * 0.25)); // 줄기 + 중심 섬광
                star *= 1.0 - progress;

                // 바깥으로 퍼지는 얇은 링
                float ringRadius = progress * 0.95;
                float ring = step(abs(r - ringRadius), 1.5 / _PixelResolution) * (1.0 - progress);

                half3 color = _Color.rgb * star + _RingColor.rgb * ring;
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
