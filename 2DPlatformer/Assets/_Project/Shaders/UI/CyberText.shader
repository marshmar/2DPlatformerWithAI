// UGUI Text 용 글리치 글자 셰이더.
// TextGlitch(BaseMeshEffect)가 글자마다 사본 2개를 앞에 끼워 넣고 uv1 에 표시를 실어 보낸다:
//   uv1.x = 0 본체 / 1 빨강 잔상 / 2 청록 잔상
//   uv1.y = 글자 번호 (글자마다 다르게 튀도록 해시 시드로 쓴다)
//   uv1.z = 글리치 세기 (0~1, 연출 중에 C# 이 올렸다 내린다)
//   uv1.w = 잔상 기본 간격 (캔버스 단위). 0 이면 효과 없는 일반 글자로 취급한다
// 정점 셰이더에서 잔상을 좌우로 벌리고 일부 글자를 통째로 튕긴다 — 조각난 모양은 픽셀 셰이더가 아니라 꼭짓점 이동으로 만든다.
Shader "Platformer/UI/CyberText"
{
    Properties
    {
        [PerRendererData] _MainTex("Font Texture", 2D) = "white" {}
        _GhostAlpha("Ghost Alpha", Range(0, 1)) = 0.55
        _GhostRed("Ghost A", Color) = (1, 0.12, 0.4, 1)
        _GhostCyan("Ghost B", Color) = (0.1, 0.95, 1, 1)
        _Scanlines("Scanlines", Range(0, 1)) = 0.16

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../Include/FXCommon.hlsl"

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd; // Text 가 (1,1,1,0) 을 넣는다 — 알파 전용 폰트 텍스처의 RGB 를 흰색으로
            float4 _ClipRect;
            float _GhostAlpha;
            fixed4 _GhostRed;
            fixed4 _GhostCyan;
            float _Scanlines;

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 effect : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 effect : TEXCOORD1;
                float4 canvasPosition : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                float ghost = input.effect.x;
                float seed = input.effect.y;
                float glitch = input.effect.z;
                float split = input.effect.w;
                float tick = floor(_FX_UnscaledTime * 14.0);
                float4 position = input.vertex;

                if (split > 0.0)
                {
                    // 잔상: 기본 간격 + 글리치 중엔 글자마다 다른 폭으로 크게 벌어진다
                    if (ghost > 0.5)
                    {
                        float side = ghost < 1.5 ? -1.0 : 1.0;
                        position.x += side * (split + glitch * (2.0 + Hash11(seed * 13.1 + tick) * 12.0));
                    }
                    // 몇몇 글자는 본체·잔상이 함께 옆으로 튄다
                    if (glitch > 0.0 && Hash11(seed * 7.3 + tick * 1.7) > 1.0 - glitch * 0.35)
                    {
                        position.x += (Hash11(seed + tick) - 0.5) * 36.0 * glitch;
                        position.y += (Hash11(seed * 3.0 + tick) - 0.5) * 8.0 * glitch;
                    }
                }

                Varyings output;
                output.positionCS = UnityObjectToClipPos(position);
                output.canvasPosition = input.vertex;
                output.color = input.color;
                output.uv = input.uv;
                output.effect = input.effect;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;
                float ghost = input.effect.x;
                float glitch = input.effect.z;
                float split = input.effect.w;

                if (split > 0.0)
                {
                    if (ghost > 0.5)
                    {
                        fixed3 tint = ghost < 1.5 ? _GhostRed.rgb : _GhostCyan.rgb;
                        color.rgb = tint;
                        color.a *= _GhostAlpha * saturate(0.5 + glitch);
                    }
                    // 화면 2px 마다 한 줄 어둡게 + 글리치 중엔 전체가 가끔 꺼질 듯 깜빡인다
                    color.rgb *= 1.0 - _Scanlines * fmod(floor(input.positionCS.y * 0.5), 2.0);
                    float tick = floor(_FX_UnscaledTime * 20.0);
                    color.a *= 1.0 - glitch * 0.5 * step(0.82, Hash11(tick * 5.31));
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.canvasPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
