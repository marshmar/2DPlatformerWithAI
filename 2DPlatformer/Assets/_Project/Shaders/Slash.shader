// 칼 궤적. 쿼드 하나에 극좌표로 초승달을 그린다:
//   반지름 r 이 바깥 원 근처의 띠이고, 각도 θ 를 따라 머리(head)가 휘둘러지며 꼬리(tail)가 따라온다.
//   _Progress(0→1)가 머리·꼬리 위치와 두께를 정하고, 노이즈가 끝부분을 갉아먹으며 사라지게 한다.
//   UV 를 아트 픽셀로 끊어서 픽셀아트와 같은 해상도로 보이게 한다.
// 두 단계로 그린다: 띠 안쪽은 뒤 화면(_CameraSortingLayerTexture)을 휘어서 보여 주고(공기 왜곡), 위에 빛을 더한다.
Shader "Platformer/Slash"
{
    Properties
    {
        [HDR] _Color("Core Color", Color) = (0.6, 2.6, 3.2, 1)
        [HDR] _EdgeColor("Edge Color", Color) = (2.5, 2.5, 2.8, 1)
        _Progress("Progress (set by script)", Range(0, 1)) = 0.3
        _ArcDegrees("Arc (degrees)", Range(30, 300)) = 200
        _Thickness("Thickness", Range(0.05, 0.6)) = 0.32
        _PixelResolution("Pixel Resolution", Float) = 40
        _Distortion("Background Distortion", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One OneMinusSrcAlpha // 프리멀티플라이드: 왜곡된 배경(알파로 덮음) + 빛(더함)
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

            // Renderer2D 의 Camera Sorting Layer Texture: FX 레이어 직전까지 그린 화면
            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                half  _Progress;
                half  _ArcDegrees;
                half  _Thickness;
                float _PixelResolution;
                half  _Distortion;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = QuantizeUV(input.uv, _PixelResolution.xx);
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                // 각도: +x(앞쪽) 이 0, 호의 절반으로 나눠 -1..1 로
                float halfArc = radians(_ArcDegrees) * 0.5;
                float t = atan2(p.y, p.x) / halfArc;

                // 머리는 위(+1)에서 아래(-1)로 휘두른다: ease-out 으로 빠르게 출발
                float progress = saturate(_Progress);
                float swing = 1.0 - (1.0 - progress) * (1.0 - progress);
                float head = lerp(1.1, -1.25, swing);
                float tail = head + lerp(0.5, 1.6, swing) * (1.0 - progress * 0.7);
                float along = (tail - t) / max(tail - head, 1e-3); // 꼬리 0 → 머리 1
                if (along < 0.0 || along > 1.0)
                    discard;

                // 초승달 단면: 바깥 반지름은 고정, 안쪽 반지름이 머리 쪽으로 갈수록 두꺼워진다
                float outer = 0.95;
                float width = _Thickness * smoothstep(0.0, 0.35, along) * (1.0 - progress * 0.5);
                float inner = outer - width;
                if (r < inner || r > outer)
                    discard;

                // 끝나 갈수록 노이즈로 갉아먹는다 (픽셀 단위 노이즈라 바스러지는 느낌)
                float erosion = ValueNoise(uv * _PixelResolution * 0.25 + progress * 3.0);
                if (erosion < progress * 1.2 - 0.25 + (1.0 - along) * 0.3)
                    discard;

                float band = saturate((r - inner) / max(width, 1e-3)); // 0 안쪽 → 1 바깥 날
                half3 glow = lerp(_Color.rgb, _EdgeColor.rgb, band * band) * (1.0 - progress * 0.6);

                // 띠 안쪽의 배경을 바깥 방향으로 살짝 밀어서 공기가 갈라지는 느낌
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 radial = r > 1e-3 ? p / r : 0;
                half3 background = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture,
                    screenUV + radial * _Distortion * (1.0 - band)).rgb;

                half coverage = 0.85;
                return half4(background * coverage + glow, coverage);
            }
            ENDHLSL
        }
    }
}
