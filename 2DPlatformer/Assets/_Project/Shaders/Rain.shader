// 빗줄기. 파티클 하나 = 폭 1px 짜리 세로 쿼드.
//   - 정점의 x 를 아트 픽셀 격자에 맞춰서 빗줄기가 반 픽셀에 걸쳐 흐려지지 않게 한다
//   - 2D 라이트를 받는다: 어두운 곳에선 거의 안 보이다가 네온 옆을 지날 때 그 색으로 반짝인다
//   - 더하기 블렌드라 겹칠수록 밝아지고, 머리(아래)는 진하고 꼬리(위)로 갈수록 흐려진다
Shader "Platformer/Rain"
{
    Properties
    {
        _Color("Base Color", Color) = (0.35, 0.4, 0.6, 1)
        _LightResponse("Light Response", Range(0, 4)) = 1.6
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"
            #include "Include/ShapeLights2D.hlsl"
            #include "Include/FXCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _LightResponse;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                positionWS.x = round(positionWS.x * FX_PIXELS_PER_UNIT) / FX_PIXELS_PER_UNIT;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.uv = input.uv;
                output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // 아래쪽(머리)이 진하고 위쪽(꼬리)으로 갈수록 사라진다. 3px 단위로 끊어서 픽셀 계단
                float along = floor(input.uv.y * 8.0) / 8.0;
                half fade = 1.0 - along;

                half4 modulate, additive;
                SampleShapeLights(input.lightingUV, half4(1, 1, 1, 1), modulate, additive);
                half3 light = modulate.rgb + additive.rgb;
                half3 color = _Color.rgb * (0.3 + light * _LightResponse);
                return half4(color * fade * input.color.a, 0);
            }
            ENDHLSL
        }
    }
}
