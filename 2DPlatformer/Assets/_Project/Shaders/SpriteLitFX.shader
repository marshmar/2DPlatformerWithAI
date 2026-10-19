// URP 2D 의 Sprite-Lit-Default 를 바탕으로 만든 스프라이트 셰이더. 구조는 원본과 같은 3 패스:
//   Universal2D      — 2D 라이트가 그려 둔 화면 공간 라이트 텍스처를 읽어서 조명을 입힌다 (+ 이펙트)
//   NormalsRendering — 노말맵을 월드 노말로 바꿔 노말 버퍼에 쓴다 (노말맵을 쓰는 라이트가 있을 때만 실행)
//   UniversalForward — 2D 렌더러가 아닌 경우를 위한 조명 없는 버전
// 이펙트는 키워드 없이 값이 0 이면 건너뛰는 분기로 처리한다 — 변형(variant) 수를 늘리지 않고, 값만 바꿔서 켜고 끌 수 있게.
Shader "Platformer/SpriteLitFX"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        [Header(Hit Flash)]
        [HDR] _FlashColor("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount("Flash Amount", Range(0, 1)) = 0

        [Header(Rim Light (light gradient))]
        [HDR] _RimColor("Rim Tint", Color) = (1, 1, 1, 1)
        _RimIntensity("Rim Intensity", Range(0, 8)) = 0
        _RimWidth("Rim Width (px)", Range(1, 3)) = 1
        _RimThreshold("Light Gradient Threshold", Range(0, 1)) = 0.04

        [Header(Outline)]
        [HDR] _OutlineColor("Outline Color", Color) = (0, 3, 3, 1)
        _OutlineAmount("Outline Amount", Range(0, 1)) = 0
        _OutlineWidth("Outline Width (px)", Range(1, 3)) = 1

        [Header(Glitch and Dissolve)]
        _GlitchAmount("Glitch Amount", Range(0, 1)) = 0
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        [HDR] _DissolveEdgeColor("Dissolve Edge", Color) = (4, 1, 6, 1)

        [Header(World)]
        _SlowResponse("Slow-Mo Desaturation", Range(0, 1)) = 1
        _EmissiveThreshold("Emissive Threshold", Range(0, 1)) = 0.7
        _EmissiveStrength("Emissive Strength", Range(0, 8)) = 0
        [ToggleUI] _HookHighlight("Hook Target Highlight", Float) = 0
        _FogColor("Depth Fog Color", Color) = (0.16, 0.1, 0.32, 1)
        _FogAmount("Depth Fog Amount", Range(0, 1)) = 0

        // 원본 셰이더와의 호환용
        [HideInInspector] _Color("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex Vertex
            #pragma fragment Fragment

            // 블렌드 스타일 4개 중 어느 라이트 텍스처가 쓰이는지에 따른 변형 (URP 가 켜고 끈다)
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"
            #include "Include/ShapeLights2D.hlsl"
            #include "Include/FXCommon.hlsl"
            #include "Include/SpriteLitFXInput.hlsl"
            #include "Include/SpriteFXCore.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 positionOS : TEXCOORD3;
                float2 uvAxis     : TEXCOORD4; // 월드 x/y 방향이 UV 에서 +인지 −인지 (좌우 반전 대응)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 월드 위치 → 라이트 텍스처 UV. 정점 셰이더의 lightingUV 와 같은 식이라 플랫폼별 y 뒤집힘도 똑같이 따라간다.
            half2 WorldToLightingUV(float3 positionWS)
            {
                float4 positionCS = TransformWorldToHClip(positionWS);
                return half2(ComputeScreenPos(positionCS / positionCS.w).xy);
            }

            // 림은 더하기(Additive) 라이트, 즉 네온의 색 번짐에서만 계산한다.
            // 곱하기 라이트(전역 조명, 캐릭터를 따라다니는 라이트)까지 넣으면, 몸 한가운데 있는 캐릭터 라이트의
            // 기울기가 네온을 압도해서 림이 엉뚱한 쪽에 생긴다.
            half3 LightColorAt(float3 positionWS, half4 mask)
            {
                half4 modulate, additive;
                SampleShapeLights(WorldToLightingUV(positionWS), mask, modulate, additive);
                return additive.rgb;
            }

            // 림 라이트: 2D 라이트에는 "방향"이 없지만, 라이트 텍스처의 밝기가 증가하는 쪽이 곧 광원 쪽이다.
            // 주변 4점의 밝기 차로 기울기를 구하고, 그 방향으로 몇 픽셀 옆이 투명하면(= 광원을 향한 가장자리) 빛나게 한다.
            // 고르게 퍼진 빛은 기울기가 0 이라 림이 생기지 않는다 — 가까운 광원에만 반응한다.
            half3 RimLight(Varyings input, float2 uv, float2 textureSize, half4 mask)
            {
                const float probe = 4.0 / FX_PIXELS_PER_UNIT;
                float3 p = input.positionWS;
                half3 right = LightColorAt(p + float3(probe, 0, 0), mask);
                half3 left  = LightColorAt(p - float3(probe, 0, 0), mask);
                half3 up    = LightColorAt(p + float3(0, probe, 0), mask);
                half3 down  = LightColorAt(p - float3(0, probe, 0), mask);

                const half3 lum = half3(0.2126, 0.7152, 0.0722);
                float2 gradient = float2(dot(right - left, lum), dot(up - down, lum));
                float strength = length(gradient);
                if (strength <= _RimThreshold)
                    return 0;

                float2 towardLight = gradient / strength;
                // 픽셀 단위 오프셋: 월드 방향을 UV 방향으로 바꾸고(반전 반영) 가장 가까운 정수 픽셀로 반올림
                float2 offsetPixels = round(towardLight * input.uvAxis * _RimWidth);
                half outsideAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offsetPixels / textureSize).a;
                half edge = 1.0 - outsideAlpha;

                half3 lightColor = (right + left + up + down) * 0.25;
                half fade = saturate((strength - _RimThreshold) * 8.0);
                return edge * fade * lightColor * _RimColor.rgb * _RimIntensity;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionOS = input.positionOS.xy;
                output.uv = input.uv;
                output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                // 회전 없는 스프라이트 기준: 스케일 −1 이나 flipX 로 뒤집히면 해당 축의 UV 방향이 반대
                output.uvAxis = sign(float2(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m11)) * sign(unity_SpriteProps.xy);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 textureSize = MainTexSize();
                float2 uv = input.uv;
                if (_GlitchAmount > 0.0)
                    uv = GlitchUV(uv, textureSize, _GlitchAmount);

                half4 albedo = input.color * SampleMainFX(uv, textureSize, _GlitchAmount);
                half outline = _OutlineAmount > 0.0 ? OutlineMask(uv, textureSize, albedo.a, _OutlineWidth) * _OutlineAmount : 0.0;
                if (albedo.a <= 0.0 && outline <= 0.0)
                    discard;

                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
                half3 lit = albedo.rgb;
                #if HAS_SHAPE_LIGHT
                {
                    half4 modulate, additive;
                    SampleShapeLights(input.lightingUV, mask, modulate, additive);
                    lit = _HDREmulationScale * (albedo.rgb * modulate.rgb + additive.rgb);
                    if (_RimIntensity > 0.0 && albedo.a > 0.0)
                        lit += RimLight(input, uv, textureSize, mask);
                }
                #endif

                half dissolveNoise = _DissolveAmount > 0.0 ? DissolveNoise(input.positionOS) : 1.0;
                half4 color = ComposeSpriteFX(albedo, lit, outline, dissolveNoise, input.positionWS.xy);
                if (color.a <= 0.0)
                    discard;
                return max(0, color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Include/FXCommon.hlsl"
            #include "Include/SpriteLitFXInput.hlsl"

            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                half4  color       : COLOR;
                float2 uv          : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half3  tangentWS   : TEXCOORD2;
                half3  bitangentWS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings NormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 flip = unity_SpriteProps.xy;
                input.positionOS = UnityFlipSprite(input.positionOS, flip);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;

                // 원본은 bitangent 를 cross(normal, tangent) 로 만들어서, 스케일 −1 로 뒤집으면 tangent 와 함께
                // bitangent 까지 뒤집혀 조명의 위아래가 반대가 된다. 오브젝트의 X·Y 축을 각각 월드로 보내면
                // 반전(스케일·flipX/Y)이 축마다 올바르게 반영된다.
                output.normalWS = -GetViewForwardDir();
                output.tangentWS = TransformObjectToWorldDir(float3(flip.x, 0, 0));
                output.bitangentWS = TransformObjectToWorldDir(float3(0, flip.y, 0));
                return output;
            }

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                half4 albedo = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS, input.bitangentWS, input.normalWS));
                return half4(0.5 * (normalWS + 1.0), albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Include/FXCommon.hlsl"
            #include "Include/SpriteLitFXInput.hlsl"
            #include "Include/SpriteFXCore.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings UnlitVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionOS = input.positionOS.xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float2 textureSize = MainTexSize();
                float2 uv = input.uv;
                if (_GlitchAmount > 0.0)
                    uv = GlitchUV(uv, textureSize, _GlitchAmount);

                half4 albedo = input.color * SampleMainFX(uv, textureSize, _GlitchAmount);
                half outline = _OutlineAmount > 0.0 ? OutlineMask(uv, textureSize, albedo.a, _OutlineWidth) * _OutlineAmount : 0.0;
                if (albedo.a <= 0.0 && outline <= 0.0)
                    discard;

                half dissolveNoise = _DissolveAmount > 0.0 ? DissolveNoise(input.positionOS) : 1.0;
                half4 color = ComposeSpriteFX(albedo, albedo.rgb, outline, dissolveNoise, input.positionWS.xy);
                if (color.a <= 0.0)
                    discard;
                return max(0, color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
