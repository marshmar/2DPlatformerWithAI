using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 갈고리 체인을 LineRenderer 로 그린다. 모양(고리 무늬, 빛)은 Chain.shader 가, 휘어짐은 여기서:
    ///   - 날아가는 중: 채찍처럼 물결치다 끝에 닿을수록 펴진다
    ///   - 매달림: 줄이 남으면 아래로 처지고, 팽팽하면 곧게 펴지며 셰이더에 "긴장"을 알려 빛이 흐르게 한다
    /// </summary>
    [RequireComponent(typeof(GrappleHook))]
    public sealed class ChainRenderer : MonoBehaviour
    {
        static readonly int LengthId = Shader.PropertyToID("_ChainLength");
        static readonly int TensionId = Shader.PropertyToID("_Tension");

        [SerializeField] LineRenderer line;
        [SerializeField] int segments = 24;
        [SerializeField] float whipAmplitude = 0.35f;
        [SerializeField] float sagFactor = 0.45f;

        GrappleHook hook;
        MaterialPropertyBlock block;
        float tension;

        void Awake()
        {
            hook = GetComponent<GrappleHook>();
            block = new MaterialPropertyBlock();
            line.positionCount = segments + 1;
            line.useWorldSpace = true;
        }

        void LateUpdate()
        {
            if (hook.State == GrappleHook.HookState.Idle)
            {
                line.enabled = false;
                return;
            }
            line.enabled = true;

            Vector2 start = hook.HandPosition;
            Vector2 end = hook.TipPosition;
            Vector2 delta = end - start;
            float length = delta.magnitude;
            Vector2 direction = length > 0.001f ? delta / length : Vector2.right;
            var normal = new Vector2(-direction.y, direction.x);

            float slack = 0f;
            float whip = 0f;
            float targetTension = 0f;
            switch (hook.State)
            {
                case GrappleHook.HookState.Attached:
                    slack = Mathf.Max(0f, hook.RopeLength - length);
                    targetTension = slack < 0.1f ? 1f : 0f;
                    break;
                case GrappleHook.HookState.Flying:
                case GrappleHook.HookState.Retracting:
                    whip = whipAmplitude * Mathf.Clamp01(length / hook.Config.Range);
                    break;
            }
            tension = Mathf.MoveTowards(tension, targetTension, Time.unscaledDeltaTime * 8f);

            float time = Time.unscaledTime;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 point = start + delta * t;
                float envelope = Mathf.Sin(Mathf.PI * t); // 양 끝은 고정
                point.y -= envelope * slack * sagFactor;
                point += normal * (Mathf.Sin(t * Mathf.PI * 3f - time * 30f) * whip * envelope);
                line.SetPosition(i, point);
            }

            line.GetPropertyBlock(block);
            block.SetFloat(LengthId, length);
            block.SetFloat(TensionId, tension);
            line.SetPropertyBlock(block);
        }
    }
}
