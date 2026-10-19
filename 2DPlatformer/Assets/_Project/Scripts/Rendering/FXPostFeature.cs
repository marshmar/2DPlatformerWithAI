using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Platformer
{
    /// <summary>
    /// 화면 전체 효과(글리치, 줌 펀치, 쇼크웨이브, 방사형 블러, 피격 비네트, 스캔라인)를 한 번의 블릿으로 그리는 렌더러 피처.
    /// URP 의 Bloom·Vignette 보다 먼저(BeforeRenderingPostProcessing) 실행해서, 글리치로 갈라진 HDR 색에도 Bloom 이 먹게 한다.
    /// </summary>
    public sealed class FXPostFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;

        Material material;
        FXPostPass pass;

        public override void Create()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/Platformer/FXPost");
            if (shader == null)
                return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new FXPostPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 씬 뷰·프리뷰에는 연출을 넣지 않는다
            if (material != null && renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class FXPostPass : ScriptableRenderPass
        {
            static readonly int ScanlinesId = Shader.PropertyToID("_Scanlines");
            static readonly int ChromaticId = Shader.PropertyToID("_ChromaticBase");
            static readonly int GlitchBaseId = Shader.PropertyToID("_GlitchBase");

            readonly Material material;

            public FXPostPass(Material material)
            {
                this.material = material;
                profilingSampler = new ProfilingSampler("FX Post");
                // 화면 버퍼를 읽어야 하므로 백버퍼에 바로 그리지 말고 중간 텍스처를 쓰게 한다
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var settings = VolumeManager.instance.stack.GetComponent<FXPostSettings>();
                bool ambient = settings != null && settings.IsActive();
                if (!ambient && !PostFX.IsActive)
                    return; // 켜진 효과가 없으면 블릿 비용도 쓰지 않는다

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                float intensity = GameSettings.ScreenEffects;
                material.SetFloat(ScanlinesId, ambient ? settings.scanlines.value : 0f);
                material.SetFloat(ChromaticId, ambient ? settings.chromaticAberration.value * intensity : 0f);
                material.SetFloat(GlitchBaseId, ambient ? settings.glitch.value * intensity : 0f);

                // 같은 텍스처를 읽으면서 쓸 수는 없어서, 새 텍스처에 그린 뒤 그걸 카메라 색 버퍼로 바꿔 끼운다
                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "_FXPostColor";
                descriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(descriptor);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "FX Post");
                resourceData.cameraColor = destination;
            }
        }
    }
}
