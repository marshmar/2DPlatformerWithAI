#ifndef SPRITE_FX_CORE_INCLUDED
#define SPRITE_FX_CORE_INCLUDED

// SpriteLitFX 의 패스들이 공유하는 스프라이트 효과 함수들.
// 필요한 선언: SpriteLitFXInput.hlsl, FXCommon.hlsl

float2 MainTexSize()
{
    // _MainTex_TexelSize 는 스프라이트가 렌더러마다 텍스처를 바꿔 끼우는 구조라 SRP Batcher 와 함께 쓸 때 믿기 어렵다.
    // 텍스처에서 직접 크기를 읽는다.
    float width, height;
    _MainTex.GetDimensions(width, height);
    return float2(width, height);
}

// 글리치: 2px 높이의 가로 띠 단위로 UV 를 밀어낸다. 시간을 12fps 로 끊어서 블록이 툭툭 튀게.
float2 GlitchUV(float2 uv, float2 textureSize, half amount)
{
    float tick = floor(_FX_UnscaledTime * 12.0);
    float row = floor(uv.y * textureSize.y * 0.5);
    float pick = Hash21(float2(row, tick));
    float shift = pick > 1.0 - amount * 0.6 ? (Hash21(float2(row, tick + 7.0)) - 0.5) * 8.0 * amount : 0.0; // 최대 ±4px
    uv.x += round(shift) / textureSize.x;
    return uv;
}

// 글리치일 때 R/B 채널을 좌우로 1px 씩 벌려서 샘플링한다
half4 SampleMainFX(float2 uv, float2 textureSize, half glitch)
{
    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    if (glitch > 0.0)
    {
        float2 split = float2(ceil(glitch * 2.0) / textureSize.x, 0.0);
        half4 red = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + split);
        half4 blue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - split);
        color = half4(red.r, color.g, blue.b, max(color.a, max(red.a, blue.a)));
    }
    return color;
}

// 아웃라인: 주변 8칸 알파의 최댓값 − 내 알파. 실루엣 바로 바깥 픽셀만 1 이 된다.
// (임포트 규칙에서 Full Rect 메시를 쓰는 이유 — Tight 메시는 이 바깥 픽셀을 잘라낸다)
half OutlineMask(float2 uv, float2 textureSize, half centerAlpha, half widthInPixels)
{
    float2 offset = widthInPixels / textureSize;
    half neighbor = 0.0;
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( offset.x, 0)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-offset.x, 0)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0,  offset.y)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -offset.y)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( offset.x,  offset.y)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-offset.x,  offset.y)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( offset.x, -offset.y)).a);
    neighbor = max(neighbor, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-offset.x, -offset.y)).a);
    return saturate(neighbor - centerAlpha);
}

// 디졸브 노이즈: 오브젝트 로컬 좌표를 픽셀 단위로 끊어서 쓴다.
// 아틀라스 UV 를 쓰면 애니메이션 프레임이 바뀔 때마다 무늬가 달라져 깜빡이고, 월드 좌표를 쓰면 움직일 때 무늬가 미끄러진다.
half DissolveNoise(float2 positionOS)
{
    float2 pixel = floor(positionOS * FX_PIXELS_PER_UNIT);
    return (half)(ValueNoise(pixel * 0.35) * 0.75 + Hash21(pixel) * 0.25);
}

// 네온처럼 밝은 픽셀만 조명과 무관하게 스스로 빛나게 한다
half3 EmissiveColor(half3 albedo)
{
    if (_EmissiveStrength <= 0.0)
        return 0;
    half luminance = dot(albedo, half3(0.2126, 0.7152, 0.0722));
    half glow = saturate((luminance - _EmissiveThreshold) / max(1.0 - _EmissiveThreshold, 1e-3));
    return albedo * glow * _EmissiveStrength;
}

// 갈고리 조준 대상 주변 타일에 픽셀 원 모양 파동을 그린다. 타일 하나하나가 아니라 월드 좌표로 계산하므로
// 어떤 타일맵에든 머티리얼만 바꾸면 적용된다.
half HookHighlight(float2 positionWS)
{
    if (_HookHighlight <= 0.0 || _FX_HookTarget.z <= 0.0)
        return 0;

    float dist = length(QuantizeToPixels(positionWS, FX_PIXELS_PER_UNIT) - QuantizeToPixels(_FX_HookTarget.xy, FX_PIXELS_PER_UNIT));
    float wave = frac(_FX_UnscaledTime * 1.5) * 2.0;                      // 0.67초마다 반지름 0 → 2 로 퍼진다
    half ring = step(abs(dist - wave), 1.5 / FX_PIXELS_PER_UNIT);     // 두께 약 3px 의 픽셀 링
    half glow = saturate(1.0 - dist / 2.0) * 0.35;
    return (ring * saturate(1.0 - wave / 2.0) + glow) * _HookHighlight;
}

// 이펙트를 모두 적용한 최종 색. lit 은 조명까지 계산된 색(조명이 없는 패스는 albedo 그대로).
// 반환 알파가 0 이면 그리지 않는다.
half4 ComposeSpriteFX(half4 albedo, half3 lit, half outline, half dissolveNoise, float2 positionWS)
{
    half3 color = lit;

    color += EmissiveColor(albedo.rgb);

    // 깊이 안개(공기 원근): 먼 배경 레이어일수록 하늘색에 섞여 흐려진다 — 레이어마다 머티리얼에서 양을 정한다
    color = lerp(color, _FogColor.rgb, _FogAmount);

    color = lerp(color, _FlashColor.rgb, _FlashAmount);

    // 슬로우 연출: 배경·지형만 탈색 (플레이어·적은 _SlowResponse = 0 으로 색을 유지해서 눈에 띄게)
    half slow = _FX_SlowAmount * _SlowResponse;
    color = DesaturateColor(color, slow * 0.85) * lerp(half3(1, 1, 1), half3(0.75, 0.85, 1.15), slow);

    color += _OutlineColor.rgb * HookHighlight(positionWS) * albedo.a;

    half alpha = albedo.a;
    if (_DissolveAmount > 0.0)
    {
        // 노이즈가 문턱값보다 낮은 픽셀부터 사라지고, 경계 2단계는 HDR 색으로 타오르게
        half threshold = _DissolveAmount * 1.1;
        if (dissolveNoise < threshold)
            alpha = 0;
        else if (dissolveNoise < threshold + 0.08)
            color = _DissolveEdgeColor.rgb;
    }

    // 실루엣 바깥 1~2px 은 아웃라인 색으로. 펄스는 슬로우와 무관하게 돌도록 _FX_UnscaledTime
    if (alpha <= 0.0 && outline > 0.0)
    {
        half pulse = 0.8 + 0.2 * sin(_FX_UnscaledTime * 10.0);
        return half4(_OutlineColor.rgb * pulse, outline);
    }
    return half4(color, alpha);
}

#endif
