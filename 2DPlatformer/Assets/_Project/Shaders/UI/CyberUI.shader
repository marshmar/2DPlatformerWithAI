// UGUI 용 사이버펑크 도형 셰이더. 텍스처 없이 부호 거리(SDF)로 패널·버튼·게이지·프레임·전환 화면을 그린다.
// CyberGraphic 이 꼭짓점 UV 채널에 도형 정보를 실어 보낸다 (캔버스 단위 = 1920x1080 기준 픽셀):
//   uv0.xy = 도형 사각형의 왼쪽 아래를 원점으로 한 위치. 바깥 빛 번짐용 여백(padding)만큼 음수·초과값이 있다
//   uv1    = (폭, 높이, 모서리 깎기, 테두리 두께)
//   uv2    = (도형 번호, 채움, 기울기, 보조 값)   보조 값 = 버튼 강조 / 게이지 잔상
//   uv3    = (칸 수, 시드, 플래시, 0)
// 모든 도형은 "아트 픽셀"(_PixelSize 캔버스 단위) 격자로 끊어서 판정한다. 1080p 에서 3px = 640x360 게임 화면의 1픽셀이라
// UI 테두리에도 게임 그림과 같은 크기의 계단이 생겨 픽셀아트와 어울린다.
// UGUI 는 렌더 파이프라인과 상관없이 캔버스가 직접 그리므로 UGUI 기본 셰이더(UI-Default) 구조를 따른다 (CG + UnityUI.cginc).
Shader "Platformer/UI/Cyber"
{
    Properties
    {
        [PerRendererData] _MainTex("Texture", 2D) = "white" {}
        _PixelSize("Art Pixel (canvas units)", Float) = 3
        _Scanlines("Scanlines", Range(0, 1)) = 0.22
        _GlowSize("Outer Glow (canvas units)", Float) = 10
        _SecondaryColor("Secondary (danger / glitch)", Color) = (1, 0.18, 0.42, 1)

        // UGUI 마스크(Mask 컴포넌트)가 스텐실을 쓴다
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

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

            #define SHAPE_PANEL    0
            #define SHAPE_BUTTON   1
            #define SHAPE_BAR      2
            #define SHAPE_SEGMENTS 3
            #define SHAPE_FRAME    4
            #define SHAPE_LINE     5
            #define SHAPE_BACKDROP 6
            #define SHAPE_WIPE     7
            #define SHAPE_DIAMOND  8
            #define SHAPE_GLOW     9

            float4 _ClipRect;
            float _PixelSize;
            float _Scanlines;
            float _GlowSize;
            fixed4 _SecondaryColor;

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float2 local : TEXCOORD0;
                float4 shape : TEXCOORD1;   // 폭, 높이, 모서리, 테두리
                float4 params : TEXCOORD2;  // 도형, 채움, 기울기, 보조
                float4 extra : TEXCOORD3;   // 칸 수, 시드, 플래시
                float4 canvasPosition : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.canvasPosition = input.vertex;
                output.color = input.color;
                output.local = input.uv0.xy;
                output.shape = input.uv1;
                output.params = input.uv2;
                output.extra = input.uv3;
                return output;
            }

            // 모서리 두 개(왼쪽 위·오른쪽 아래)를 45°로 깎은 사각형의 부호 거리. 안쪽이 음수.
            // 사각형 부분은 체비셰프 거리(max)라서 픽셀 격자에서 1px 두께 선이 정확히 1px 로 나온다
            float SdCyberBox(float2 q, float2 halfSize, float cut)
            {
                float2 a = abs(q);
                float box = max(a.x - halfSize.x, a.y - halfSize.y);
                float2 s = q * float2(-1.0, 1.0);                  // 왼쪽 위·오른쪽 아래 사분면에서 s.x·s.y > 0
                float corner = (a.x + a.y - (halfSize.x + halfSize.y - cut)) * 0.70710678;
                return s.x * s.y > 0.0 && cut > 0.0 ? max(box, corner) : box;
            }

            // 가로줄 무늬: 아트 픽셀 2줄마다 한 줄씩 어둡게
            float Scanline(float2 p)
            {
                return 1.0 - _Scanlines * fmod(floor(p.y / _PixelSize), 2.0);
            }

            // 패널·버튼 공통: 반투명 어두운 바탕 + 테두리 + 깎이지 않은 모서리의 꺾쇠 + 바깥 빛 번짐 + 위에서 아래로 훑는 띠
            fixed4 Panel(float2 p, float2 size, float cut, float border, fixed4 tint, float highlight, float seed, bool isButton)
            {
                float t = _FX_UnscaledTime;
                float2 q = p - size * 0.5;
                float d = SdCyberBox(q, size * 0.5, cut);

                // 바깥 빛 번짐 (강조될수록 넓고 밝게)
                if (d > 0.0)
                {
                    float glow = _GlowSize * (0.6 + highlight * 0.8);
                    float falloff = saturate(1.0 - d / glow);
                    return fixed4(tint.rgb, tint.a * falloff * falloff * (0.18 + highlight * 0.35));
                }

                float3 color = tint.rgb * (0.10 + 0.06 * p.y / max(size.y, 1.0)) + 0.015;
                float alpha = tint.a * 0.84;

                // 바탕 무늬: 8 아트 픽셀마다 점, 그리고 천천히 내려오는 밝은 띠
                float2 cell = floor(p / _PixelSize);
                if (fmod(cell.x, 8.0) == 0.0 && fmod(cell.y, 8.0) == 0.0)
                    color += tint.rgb * 0.18;
                float sweep = (1.0 - frac(t * 0.22 + seed)) * (size.y + 80.0) - 40.0;
                color += tint.rgb * 0.10 * saturate(1.0 - abs(p.y - sweep) / (_PixelSize * 3.0));

                if (isButton)
                {
                    // 강조되면 바탕이 밝아지고 사선 줄무늬가 흐른다
                    color += tint.rgb * 0.22 * highlight;
                    float stripe = step(0.5, frac((p.x + p.y) / (_PixelSize * 8.0) - t * 0.9));
                    color += tint.rgb * 0.07 * highlight * stripe;
                    // 왼쪽 끝 표시줄
                    if (p.x < _PixelSize * 2.0)
                        color = lerp(tint.rgb * 0.45, tint.rgb * 1.3 + 0.15, highlight);
                }
                color *= Scanline(p);

                // 테두리
                if (d > -border)
                {
                    color = tint.rgb * (0.75 + highlight * 0.5) + highlight * 0.12;
                    alpha = tint.a;
                }

                // 깎이지 않은 두 모서리(오른쪽 위·왼쪽 아래)에 밝은 꺾쇠
                float bracket = min(min(size.x, size.y) * 0.3, 26.0);
                float2 fromTopRight = size - p;
                bool topRight = (fromTopRight.x < bracket && fromTopRight.y < border * 2.0) || (fromTopRight.y < bracket && fromTopRight.x < border * 2.0);
                bool bottomLeft = (p.x < bracket && p.y < border * 2.0) || (p.y < bracket && p.x < border * 2.0);
                if (topRight || bottomLeft)
                {
                    color = tint.rgb * 1.2 + 0.2;
                    alpha = tint.a;
                }
                return fixed4(color, alpha);
            }

            // 기울어진 칸 게이지 (체력·설정 값). 채운 칸은 밝게, 방금 잃은 칸(잔상)은 붉게 깜빡인다
            fixed4 Segments(float2 p, float2 size, float count, float fill, float trail, float skew, fixed4 tint, float flash)
            {
                float t = _FX_UnscaledTime;
                p.x -= skew * (p.y - size.y * 0.5);                 // 평행사변형: 위로 갈수록 오른쪽으로
                count = max(count, 1.0);
                float gap = _PixelSize * 2.0;
                float width = (size.x - gap * (count - 1.0)) / count;
                float index = floor(p.x / (width + gap));
                float inSegment = p.x - index * (width + gap);
                if (index < 0.0 || index >= count || inSegment > width || p.y < 0.0 || p.y > size.y)
                    return 0;

                float filled = fill * count;
                float trailFilled = trail * count;
                bool edge = inSegment < _PixelSize || inSegment > width - _PixelSize || p.y < _PixelSize || p.y > size.y - _PixelSize;
                fixed4 result;
                if (index < floor(filled + 0.001))
                {
                    // 채운 칸: 위쪽이 밝은 그라디언트 + 오른쪽으로 흐르는 광택
                    float shine = saturate(1.0 - abs(frac(t * 0.5) * (size.x + 200.0) - 100.0 - p.x) / 40.0);
                    float3 color = tint.rgb * (0.85 + 0.35 * p.y / size.y) + shine * 0.25;
                    result = fixed4(color * Scanline(p), tint.a);
                }
                else if (index < trailFilled)
                {
                    float blink = 0.6 + 0.4 * step(0.5, frac(t * 8.0));
                    result = fixed4(_SecondaryColor.rgb * blink, tint.a);
                }
                else
                {
                    // 빈 칸은 테두리만
                    result = edge ? fixed4(tint.rgb * 0.45, tint.a * 0.8) : fixed4(tint.rgb * 0.08, tint.a * 0.55);
                }
                result.rgb = lerp(result.rgb, 1.0, flash);
                return result;
            }

            // 진행 막대: 기울어진 트랙 + 채움 + 10% 눈금
            fixed4 Bar(float2 p, float2 size, float fill, float skew, fixed4 tint)
            {
                p.x -= skew * (p.y - size.y * 0.5);
                if (p.x < 0.0 || p.x > size.x || p.y < 0.0 || p.y > size.y)
                    return 0;
                float u = p.x / size.x;
                bool tick = frac(u * 10.0) < _PixelSize / size.x * 10.0;
                if (u <= fill)
                    return fixed4(tint.rgb * (tick ? 0.6 : 1.0) * Scanline(p), tint.a);
                bool edge = p.y < _PixelSize || p.y > size.y - _PixelSize;
                return fixed4(tint.rgb * (edge ? 0.4 : 0.1), tint.a * 0.7);
            }

            // 네 모서리 꺾쇠만 (선택 표시 프레임). 강조되면 숨 쉬듯 조였다 풀린다
            fixed4 Frame(float2 p, float2 size, float border, fixed4 tint, float highlight)
            {
                float inset = highlight * _PixelSize * (1.0 + sin(_FX_UnscaledTime * 6.0));
                p -= inset;
                size -= inset * 2.0;
                if (p.x < 0.0 || p.y < 0.0 || p.x > size.x || p.y > size.y)
                    return 0;
                float bracket = min(min(size.x, size.y) * 0.25, 30.0);
                float2 nearest = min(p, size - p);
                bool horizontal = nearest.y < border && nearest.x < bracket;
                bool vertical = nearest.x < border && nearest.y < bracket;
                return horizontal || vertical ? tint : 0;
            }

            // 구분선: 양 끝이 희미해지고, 점선이 흐르고, 밝은 신호 하나가 지나간다
            fixed4 Line(float2 p, float2 size, fixed4 tint, float seed)
            {
                if (p.y < 0.0 || p.y > size.y || p.x < 0.0 || p.x > size.x)
                    return 0;
                float t = _FX_UnscaledTime;
                float fade = saturate(p.x / 60.0) * saturate((size.x - p.x) / 60.0);
                float dash = step(0.35, frac(p.x / (_PixelSize * 6.0) - t * 1.5));
                float packet = saturate(1.0 - abs(frac(t * 0.35 + seed) * size.x - p.x) / 24.0);
                float3 color = tint.rgb * (0.55 + 0.25 * dash) + packet * 0.6;
                return fixed4(color, tint.a * fade);
            }

            // 화면 전체 어둡게 (일시정지 등): 가장자리일수록 어둡고 스캔라인.
            // fadeRight > 0 이면 오른쪽 끝 그 비율만큼에서 점점 투명해진다 (화면 왼쪽만 어둡게 깔 때 경계선이 안 보이게)
            fixed4 Backdrop(float2 p, float2 size, fixed4 tint, float fadeRight)
            {
                float2 centered = p / size - 0.5;
                float vignette = saturate(dot(centered, centered) * 2.2);
                float3 color = tint.rgb * Scanline(p) * (1.0 - vignette * 0.6);
                float alpha = saturate(tint.a + vignette * 0.25);
                if (fadeRight > 0.0)
                {
                    float fade = saturate((size.x - p.x) / (size.x * fadeRight));
                    alpha *= fade * fade;
                }
                return fixed4(color, alpha);
            }

            // 화면 전환: 24px 블록이 왼쪽부터 무작위로 채워지고, 채워지는 경계 블록은 네온색으로 빛난다
            fixed4 Wipe(float2 p, float2 size, float progress, fixed4 tint, float seed)
            {
                float blockSize = _PixelSize * 8.0;
                float2 cell = floor(p / blockSize);
                float threshold = Hash21(cell + seed * 17.0) * 0.35 + (cell.x * blockSize / size.x) * 0.65;
                float v = progress * 1.45 - 0.1;
                if (threshold >= v)
                    return 0;
                float3 color = float3(0.02, 0.015, 0.05) * Scanline(p);
                if (threshold > v - 0.07)
                    color = Hash21(cell * 1.7 + 3.1) > 0.5 ? tint.rgb : _SecondaryColor.rgb;
                return fixed4(color, 1.0);
            }

            // 마름모 아이콘 (데이터 칩): 테두리 + 안쪽 점
            fixed4 Diamond(float2 p, float2 size, float border, fixed4 tint)
            {
                float2 q = abs(p - size * 0.5);
                float r = min(size.x, size.y) * 0.5;
                float d = q.x + q.y;
                if (d > r)
                    return 0;
                if (d > r - border * 1.5 || d < r * 0.3)
                    return tint;
                return fixed4(tint.rgb * 0.25, tint.a * 0.8);
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                float2 size = input.shape.xy;
                float cut = input.shape.z;
                float border = input.shape.w;
                int shape = (int)round(input.params.x);
                float fill = input.params.y;
                float skew = input.params.z;
                float value2 = input.params.w;
                float seed = input.extra.y;
                float flash = input.extra.z;
                fixed4 tint = input.color;

                float2 p = input.local;
                // 플래시(선택된 순간 등): 몇몇 가로 띠가 옆으로 튄다 — 격자로 끊기 전에 밀어야 블록째 움직인다
                if (flash > 0.0)
                {
                    float band = floor(p.y / (_PixelSize * 3.0));
                    float tick = floor(_FX_UnscaledTime * 30.0);
                    if (Hash11(band * 1.37 + tick + seed * 9.0) > 0.55)
                        p.x += (Hash11(band + tick * 1.9) - 0.5) * 30.0 * flash;
                }
                if (shape != SHAPE_GLOW)
                    p = (floor(p / _PixelSize) + 0.5) * _PixelSize;

                fixed4 color = 0;
                if (shape == SHAPE_PANEL || shape == SHAPE_BUTTON)
                {
                    color = Panel(p, size, cut, border, tint, value2, seed, shape == SHAPE_BUTTON);
                    color.rgb = lerp(color.rgb, 1.0, flash * 0.35);
                }
                else if (shape == SHAPE_BAR)
                    color = Bar(p, size, fill, skew, tint);
                else if (shape == SHAPE_SEGMENTS)
                    color = Segments(p, size, input.extra.x, fill, value2, skew, tint, flash);
                else if (shape == SHAPE_FRAME)
                    color = Frame(p, size, border, tint, value2);
                else if (shape == SHAPE_LINE)
                    color = Line(p, size, tint, seed);
                else if (shape == SHAPE_BACKDROP)
                    color = Backdrop(p, size, tint, value2);
                else if (shape == SHAPE_WIPE)
                    color = Wipe(p, size, fill, tint, seed);
                else if (shape == SHAPE_DIAMOND)
                    color = Diamond(p, size, border, tint);
                else if (shape == SHAPE_GLOW)
                {
                    float2 q = (p - size * 0.5) / (size * 0.5);
                    float falloff = saturate(1.0 - length(q));
                    color = fixed4(tint.rgb, tint.a * falloff * falloff);
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
