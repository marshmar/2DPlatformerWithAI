using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 정해진 시간 동안 재생되고 풀로 돌아가는 이펙트. 셰이더 이펙트는 _Progress(0→1)를 받아 스스로 모양을 바꾸고,
    /// 스프라이트 이펙트는 SpriteAnimator 를 처음부터 다시 튼다.
    /// </summary>
    public sealed class TimedFX : MonoBehaviour
    {
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] float duration = 0.2f;
        [Tooltip("히트스톱 중에도 진행할지 (불꽃은 멈춰 있어야 타격 순간이 강조된다 — 기본은 게임 시간)")]
        [SerializeField] bool unscaledTime;

        Renderer[] renderers;
        MaterialPropertyBlock block;
        SpriteAnimator spriteAnimator;
        float age;
        Color? tint;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            spriteAnimator = GetComponent<SpriteAnimator>();
        }

        void OnEnable()
        {
            age = 0f;
            tint = null;
            if (spriteAnimator != null)
                spriteAnimator.Restart();
            Apply(0f);
        }

        public void SetTint(Color color)
        {
            tint = color;
            Apply(age / duration);
        }

        void Update()
        {
            age += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = age / duration;
            Apply(Mathf.Min(progress, 1f));
            if (progress >= 1f)
                FXPool.Release(gameObject);
        }

        void Apply(float progress)
        {
            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetFloat(ProgressId, progress);
                if (tint.HasValue)
                    block.SetColor(ColorId, tint.Value);
                r.SetPropertyBlock(block);
            }
        }
    }
}
