#ifndef PLATFORMER_FX_COMMON_INCLUDED
#define PLATFORMER_FX_COMMON_INCLUDED

// ---------------------------------------------------------------------------------------------
// 전역 효과 값. FXGlobals.cs 가 렌더링 직전에 매 프레임 설정한다 (Shader.SetGlobal*).
// 셰이더는 읽기만 하고, 게임 코드는 FX API 만 호출한다 — 연출을 바꿀 때 게임 코드를 건드리지 않게.
// ---------------------------------------------------------------------------------------------
float  _FX_UnscaledTime;    // timeScale 영향을 받지 않는 시간. _Time 은 슬로우·히트스톱에 같이 느려진다
float  _FX_SlowAmount;      // 0 = 평소, 1 = 슬로우 연출 최대
float4 _FX_PlayerPosition;  // xy = 플레이어 몸 중심 (월드)
float4 _FX_HookTarget;      // xy = 갈고리 조준 대상 (월드), z = 1 이면 유효

#define FX_PIXELS_PER_UNIT 16.0

// ---------------------------------------------------------------------------------------------
// 해시 & 노이즈 — sin 을 쓰지 않는 해시 (Dave Hoskins, "Hash without Sine").
// sin 기반 해시는 GPU 마다 정밀도가 달라 큰 입력에서 패턴이 깨진다.
// ---------------------------------------------------------------------------------------------
float Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float Hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float Fbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    [unroll]
    for (int octave = 0; octave < 4; octave++)
    {
        value += amplitude * ValueNoise(p);
        p *= 2.03;
        amplitude *= 0.5;
    }
    return value;
}

// ---------------------------------------------------------------------------------------------
// 픽셀 양자화. 이펙트의 좌표·시간을 아트 픽셀 단위로 끊어서, 매끈한 효과가 픽셀아트 위에서 따로 놀지 않게 한다.
// ---------------------------------------------------------------------------------------------
float2 QuantizeToPixels(float2 value, float pixelsPerUnit)
{
    return floor(value * pixelsPerUnit) / pixelsPerUnit;
}

// 텍셀 중심으로 맞춘 UV — 포인트 샘플링과 같은 결과를 계산식에서도 얻기 위해
float2 QuantizeUV(float2 uv, float2 textureSize)
{
    return (floor(uv * textureSize) + 0.5) / textureSize;
}

// 애니메이션 시간을 fps 단위로 끊는다 — 손으로 그린 프레임처럼 "툭툭" 움직이게
float QuantizeTime(float time, float fps)
{
    return floor(time * fps) / fps;
}

half3 DesaturateColor(half3 color, half amount)
{
    half gray = dot(color, half3(0.2126, 0.7152, 0.0722));
    return lerp(color, gray.xxx, amount);
}

#endif
