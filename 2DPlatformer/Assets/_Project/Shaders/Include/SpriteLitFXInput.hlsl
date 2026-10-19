#ifndef SPRITE_LIT_FX_INPUT_INCLUDED
#define SPRITE_LIT_FX_INPUT_INCLUDED

// 모든 패스가 같은 상수 버퍼 배치를 써야 SRP Batcher 가 묶을 수 있다 — 그래서 한 파일에서 선언한다.
// 값은 머티리얼 기본값이고, 오브젝트별 순간 값(피격 플래시 등)은 SpriteFX.cs 가 MaterialPropertyBlock 으로 덮어쓴다.

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MaskTex);
SAMPLER(sampler_MaskTex);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _FlashColor;
    half  _FlashAmount;
    half4 _RimColor;
    half  _RimIntensity;
    half  _RimWidth;
    half  _RimThreshold;
    half4 _OutlineColor;
    half  _OutlineAmount;
    half  _OutlineWidth;
    half  _GlitchAmount;
    half  _DissolveAmount;
    half4 _DissolveEdgeColor;
    half  _SlowResponse;
    half  _EmissiveThreshold;
    half  _EmissiveStrength;
    half  _HookHighlight;
    half4 _FogColor;
    half  _FogAmount;
CBUFFER_END

#endif
