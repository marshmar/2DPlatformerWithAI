#ifndef PLATFORMER_SHAPE_LIGHTS_2D_INCLUDED
#define PLATFORMER_SHAPE_LIGHTS_2D_INCLUDED

// URP 2D 라이트가 그려 둔 화면 공간 라이트 텍스처(블렌드 스타일 4개)를 읽는다.
// 필요한 선행 include: Core.hlsl, LightingUtility.hlsl(SHAPE_LIGHT 매크로), ShapeLightVariables.hlsl
// 필요한 pragma: #include_with_pragmas ".../ShapeLightShared.hlsl"

#if USE_SHAPE_LIGHT_TYPE_0
SHAPE_LIGHT(0)
#endif
#if USE_SHAPE_LIGHT_TYPE_1
SHAPE_LIGHT(1)
#endif
#if USE_SHAPE_LIGHT_TYPE_2
SHAPE_LIGHT(2)
#endif
#if USE_SHAPE_LIGHT_TYPE_3
SHAPE_LIGHT(3)
#endif

#define HAS_SHAPE_LIGHT (USE_SHAPE_LIGHT_TYPE_0 || USE_SHAPE_LIGHT_TYPE_1 || USE_SHAPE_LIGHT_TYPE_2 || USE_SHAPE_LIGHT_TYPE_3)

// URP 의 CombinedShapeLightShared 와 같은 합성. 원본은 알파 0 픽셀을 discard 해서 아웃라인 같은 걸 그릴 수 없어서 직접 쓴다.
#define ACCUMULATE_SHAPE_LIGHT(index) \
{ \
    half4 light = SAMPLE_TEXTURE2D(_ShapeLightTexture##index, sampler_ShapeLightTexture##index, lightingUV); \
    if (any(_ShapeLightMaskFilter##index)) \
    { \
        half4 processedMask = (1 - _ShapeLightInvertedFilter##index) * mask + _ShapeLightInvertedFilter##index * (1 - mask); \
        light *= dot(processedMask, _ShapeLightMaskFilter##index); \
    } \
    modulate += light * _ShapeLightBlendFactors##index.x; \
    additive += light * _ShapeLightBlendFactors##index.y; \
}

// modulate = 곱하기 라이트 합, additive = 더하기 라이트 합. 최종색 = _HDREmulationScale * (albedo * modulate + additive)
void SampleShapeLights(half2 lightingUV, half4 mask, out half4 modulate, out half4 additive)
{
    modulate = 0;
    additive = 0;
    #if USE_SHAPE_LIGHT_TYPE_0
    ACCUMULATE_SHAPE_LIGHT(0)
    #endif
    #if USE_SHAPE_LIGHT_TYPE_1
    ACCUMULATE_SHAPE_LIGHT(1)
    #endif
    #if USE_SHAPE_LIGHT_TYPE_2
    ACCUMULATE_SHAPE_LIGHT(2)
    #endif
    #if USE_SHAPE_LIGHT_TYPE_3
    ACCUMULATE_SHAPE_LIGHT(3)
    #endif
}

half3 ApplyShapeLights(half3 albedo, half2 lightingUV, half4 mask)
{
    #if HAS_SHAPE_LIGHT
    half4 modulate, additive;
    SampleShapeLights(lightingUV, mask, modulate, additive);
    return _HDREmulationScale * (albedo * modulate.rgb + additive.rgb);
    #else
    return albedo;
    #endif
}

#endif
