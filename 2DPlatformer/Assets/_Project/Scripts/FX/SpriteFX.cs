using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 스프라이트 한 개의 순간 효과(피격 플래시, 아웃라인, 글리치, 디졸브)를 MaterialPropertyBlock 으로 제어한다.
    /// 머티리얼을 복제하지 않아서 같은 머티리얼을 쓰는 다른 스프라이트에 영향이 없다.
    /// (대신 이 렌더러는 SRP Batcher 묶음에서 빠진다 — 효과가 모두 꺼지면 블록을 비워서 다시 묶이게 한다)
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class SpriteFX : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int OutlineAmountId = Shader.PropertyToID("_OutlineAmount");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");
        static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

        [Tooltip("피격 플래시가 사라지는 시간 (초, 게임 시간)")]
        [SerializeField] float flashDuration = 0.12f;
        [ColorUsage(false, true)]
        [SerializeField] Color flashColor = new(2f, 2f, 2f, 1f);

        Renderer target;
        MaterialPropertyBlock block;
        float flash;
        float glitch;
        float glitchDecay;
        float outline;
        Color outlineColor = new(0f, 3f, 3f, 1f);
        bool hasOutlineColor;
        float dissolve;
        bool applied;

        public float Dissolve => dissolve;

        void Awake()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        /// <summary>하얗게 번쩍인다. 피격 순간.</summary>
        public void Flash(float strength = 1f) => flash = Mathf.Max(flash, strength);

        /// <summary>duration 동안 글리치가 서서히 약해진다. 화면이 멈춘 히트스톱 중에도 줄어들도록 실제 시간 기준.</summary>
        public void Glitch(float strength, float duration)
        {
            glitch = Mathf.Max(glitch, strength);
            glitchDecay = strength / Mathf.Max(duration, 0.01f);
        }

        public void SetOutline(float amount) => outline = amount;

        public void SetOutline(float amount, Color color)
        {
            outline = amount;
            outlineColor = color;
            hasOutlineColor = true;
        }

        public void SetDissolve(float amount) => dissolve = Mathf.Clamp01(amount);

        void LateUpdate()
        {
            if (flash > 0f)
                flash = Mathf.Max(0f, flash - Time.deltaTime / Mathf.Max(flashDuration, 0.01f));
            if (glitch > 0f)
                glitch = Mathf.Max(0f, glitch - glitchDecay * Time.unscaledDeltaTime);

            bool active = flash > 0f || glitch > 0f || outline > 0f || dissolve > 0f;
            if (!active)
            {
                if (applied)
                {
                    target.SetPropertyBlock(null); // 블록을 비워야 SRP Batcher 로 다시 묶인다
                    applied = false;
                }
                return;
            }

            target.GetPropertyBlock(block);
            block.SetFloat(FlashAmountId, flash);
            block.SetColor(FlashColorId, flashColor);
            block.SetFloat(GlitchAmountId, glitch);
            block.SetFloat(OutlineAmountId, outline);
            if (hasOutlineColor)
                block.SetColor(OutlineColorId, outlineColor);
            block.SetFloat(DissolveAmountId, dissolve);
            target.SetPropertyBlock(block);
            applied = true;
        }
    }
}
