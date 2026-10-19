using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer
{
    /// <summary>
    /// 체크포인트(동기화 비콘). 닿으면 쓰러졌을 때 여기서 다시 시작하고 체력이 가득 찬다.
    /// 빛기둥(Beacon.shader)이 끊기며 깜빡이는 자홍색 → 굵고 밝은 청록색으로 바뀌고, 아래에서 위로 펄스가 치솟는다.
    /// 마지막으로 닿은 체크포인트만 "현재"이고, 이전 것은 약하게 켜진 채로 남는다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Checkpoint : MonoBehaviour
    {
        static readonly int ActivationId = Shader.PropertyToID("_Activation");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly Color IdleLight = new(1f, 0.25f, 0.7f);
        static readonly Color ActiveLight = new(0.25f, 0.95f, 1f);

        static Checkpoint current;

        [SerializeField] SpriteRenderer beam;
        [SerializeField] Light2D glow;

        MaterialPropertyBlock block;
        float activation;
        float pulse;
        bool reached;

        public static Checkpoint Current => current;

        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        /// <summary>레벨 빌더용</summary>
        public void Bind(SpriteRenderer beamRenderer, Light2D light)
        {
            beam = beamRenderer;
            glow = light;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (current == this)
                return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || (player.Health != null && player.Health.IsDead))
                return;

            current = this;
            bool first = !reached;
            reached = true;
            pulse = 1f;
            player.SetCheckpoint(transform.position);
            if (player.Health != null)
                player.Health.Restore();

            PostFX.Shockwave((Vector2)transform.position + Vector2.up * 1.5f, 0.5f, 0.5f);
            CameraController.Shake(0.12f);
            UI.UISound.Play(UI.UISound.Kind.Checkpoint);
            if (GameFlow.Instance != null)
                GameFlow.Instance.NotifyCheckpoint(first);
        }

        void Update()
        {
            float target = current == this ? 1f : reached ? 0.45f : 0f;
            activation = Mathf.MoveTowards(activation, target, Time.deltaTime * 2.5f);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 1.4f);

            if (beam != null)
            {
                beam.GetPropertyBlock(block);
                block.SetFloat(ActivationId, activation);
                block.SetFloat(PulseId, pulse);
                beam.SetPropertyBlock(block);
            }
            if (glow != null)
            {
                glow.color = Color.Lerp(IdleLight, ActiveLight, activation);
                glow.intensity = 0.35f + activation * 0.5f + pulse * 1.2f;
            }
        }
    }
}
