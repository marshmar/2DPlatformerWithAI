using UnityEngine;
using UnityEngine.Rendering;

namespace Platformer
{
    /// <summary>
    /// 셰이더 전역 값(FXCommon.hlsl 의 _FX_*)을 렌더링 직전에 매 프레임 올린다.
    /// 게임 코드는 이 클래스의 값만 바꾸고, 셰이더는 읽기만 한다.
    /// </summary>
    public static class FXGlobals
    {
        static readonly int UnscaledTimeId = Shader.PropertyToID("_FX_UnscaledTime");
        static readonly int SlowAmountId = Shader.PropertyToID("_FX_SlowAmount");
        static readonly int PlayerPositionId = Shader.PropertyToID("_FX_PlayerPosition");
        static readonly int HookTargetId = Shader.PropertyToID("_FX_HookTarget");

        public static Vector2 PlayerPosition { get; set; }
        public static Vector2 HookTarget { get; private set; }
        public static bool HasHookTarget { get; private set; }

        public static void SetHookTarget(Vector2 position)
        {
            HookTarget = position;
            HasHookTarget = true;
        }

        public static void ClearHookTarget() => HasHookTarget = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            // 도메인 리로드를 끈 설정에서도 중복 등록되지 않게 먼저 뺀다
            RenderPipelineManager.beginContextRendering -= Upload;
            RenderPipelineManager.beginContextRendering += Upload;
            HasHookTarget = false;
        }

        static void Upload(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            Shader.SetGlobalFloat(UnscaledTimeId, Time.unscaledTime);
            Shader.SetGlobalFloat(SlowAmountId, TimeManager.SlowAmount);
            Shader.SetGlobalVector(PlayerPositionId, PlayerPosition);
            Shader.SetGlobalVector(HookTargetId, new Vector4(HookTarget.x, HookTarget.y, HasHookTarget ? 1f : 0f, 0f));
            PostFX.Upload(Camera.main);
        }
    }
}
