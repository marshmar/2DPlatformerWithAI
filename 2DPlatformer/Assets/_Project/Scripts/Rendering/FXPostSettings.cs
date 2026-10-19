using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Platformer
{
    /// <summary>
    /// 상시 화면 분위기 값. 볼륨으로 블렌딩된다 — 예를 들어 해킹 구역에 Local Volume 을 두면 그 안에서만 글리치가 낀다.
    /// 순간 연출(피격, 줌 펀치 등)은 PostFX 가 담당하고, 둘 다 FXPostFeature 의 한 패스에서 합쳐 그린다.
    /// </summary>
    [Serializable, VolumeComponentMenu("Platformer/FX Post")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class FXPostSettings : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter scanlines = new(0f, 0f, 1f);
        public ClampedFloatParameter chromaticAberration = new(0f, 0f, 1f);
        public ClampedFloatParameter glitch = new(0f, 0f, 1f);

        public bool IsActive() => scanlines.value > 0f || chromaticAberration.value > 0f || glitch.value > 0f;
    }
}
