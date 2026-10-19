// 화면 전체 연출을 한 번의 블릿으로 처리한다. 순서가 중요하다:
//   UV 를 휘게 하는 효과(줌 펀치 → 쇼크웨이브 → 글리치)를 먼저 계산하고, 그 UV 로 한 번만 샘플링한 다음
//   색을 바꾸는 효과(색수차 → 방사형 블러 → 블록 반전 → 비네트 → 스캔라인)를 얹는다.
Shader "Hidden/Platformer/FXPost"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "FXPost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Include/FXCommon.hlsl"

            // 볼륨(FXPostSettings)에서 오는 상시 값
            float _Scanlines;
            float _ChromaticBase;
            float _GlitchBase;

            // PostFX.cs 가 올리는 순간 연출 값
            float  _FX_Glitch;
            float4 _FX_Zoom;      // xy 초점 UV, z 확대량
            float4 _FX_Shockwave; // xy 중심 UV, z 반지름(화면 높이 단위), w 세기
            float  _FX_Damage;
            float  _FX_RadialBlur;

            // 픽셀아트가 번지지 않도록 포인트 샘플링
            half3 SampleColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
            }

            // 색수차: R 과 B 를 반대 방향으로 밀어 샘플링. 화면 가장자리일수록 더 벌어지는 렌즈 느낌 + 글리치용 가로 분리
            half3 SampleSplit(float2 uv, float split)
            {
                if (split <= 0.0)
                    return SampleColor(uv);
                float2 offset = float2(split, 0.0) + (uv - 0.5) * split;
                half r = SampleColor(uv + offset).r;
                half g = SampleColor(uv).g;
                half b = SampleColor(uv - offset).b;
                return half3(r, g, b);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;
                float2 uv = screenUV;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 1) 줌 펀치: 초점을 기준으로 UV 를 줄이면 화면이 확대된다.
                //    카메라 orthoSize 를 바꾸지 않으므로 Pixel Perfect Camera 의 고정 배율과 충돌하지 않는다
                float2 focus = _FX_Zoom.xy;
                uv = focus + (uv - focus) / (1.0 + _FX_Zoom.z);

                // 2) 쇼크웨이브: 링 위의 픽셀을 링 바깥쪽에서 가져와 굴절처럼 보이게 한다 (가로세로 비율 보정해서 원형으로)
                if (_FX_Shockwave.w > 0.0)
                {
                    float2 delta = (uv - _FX_Shockwave.xy) * float2(aspect, 1.0);
                    float dist = length(delta);
                    float ring = 1.0 - saturate(abs(dist - _FX_Shockwave.z) / 0.035);
                    float2 direction = dist > 1e-4 ? delta / dist : 0.0;
                    uv -= direction / float2(aspect, 1.0) * ring * ring * 0.03 * _FX_Shockwave.w;
                }

                // 3) 글리치: 18fps 로 끊은 시간마다 일부 블록을 옆으로 밀고, 몇몇 가로줄을 흔든다
                float glitch = saturate(_GlitchBase + _FX_Glitch);
                float split = _ChromaticBase * 0.004 + _FX_SlowAmount * 0.003 + _FX_Damage * 0.006;
                float tick = floor(_FX_UnscaledTime * 18.0);
                if (glitch > 0.0)
                {
                    float2 block = floor(uv * float2(12.0, 24.0));
                    if (Hash21(block + tick) < glitch * 0.35)
                        uv.x += (Hash21(block.yx + tick * 1.7) - 0.5) * 0.06 * glitch;

                    float row = floor(uv.y * _ScreenParams.y / 4.0);
                    if (Hash11(row + tick * 3.1) > 1.0 - glitch * 0.25)
                        uv.x += (Hash11(row * 1.3 + tick) - 0.5) * 0.04 * glitch;

                    split += glitch * 0.012;
                }

                // 4) 방사형 블러: 초점 쪽으로 8번 겹쳐 샘플링 — 슬로우·대시 때 속도감
                half3 color;
                if (_FX_RadialBlur > 0.0)
                {
                    color = 0;
                    float2 toFocus = focus - uv;
                    [unroll]
                    for (int i = 0; i < 8; i++)
                        color += SampleSplit(uv + toFocus * (i / 8.0) * _FX_RadialBlur * 0.08, split);
                    color /= 8.0;
                }
                else
                {
                    color = SampleSplit(uv, split);
                }

                // 5) 글리치 블록 일부는 색 채널을 뒤섞는다. (반전은 어두운 밤하늘을 거의 흰색으로 만들고
                //    그게 Bloom 까지 받아서 화면이 하얗게 번쩍인다 — 밝기는 유지하고 색만 바꾼다)
                if (glitch > 0.0 && Hash21(floor(uv * float2(8.0, 16.0)) + tick * 2.3) < glitch * 0.05)
                    color = color.brg * 1.4;

                // 6) 비네트: 피격이면 가장자리가 붉게, 슬로우면 가장자리가 어둡게
                float2 centered = (screenUV - 0.5) * float2(aspect, 1.0);
                float vignette = smoothstep(0.25, 1.0, dot(centered, centered));
                color = lerp(color, half3(0.8, 0.04, 0.12), _FX_Damage * vignette * 0.65);
                color *= 1.0 - _FX_SlowAmount * vignette * 0.5;

                // 7) 스캔라인: 화면 2px 마다 한 줄씩 어둡게
                if (_Scanlines > 0.0)
                    color *= 1.0 - _Scanlines * fmod(floor(screenUV.y * _ScreenParams.y * 0.5), 2.0);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
