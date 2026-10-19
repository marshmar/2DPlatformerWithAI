using UnityEngine;

namespace Platformer
{
    /// <summary>출구 포털. 플레이어가 들어오면 섹터 클리어 — 기록을 남기고 결과 화면을 띄운다 (GameFlow).</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        bool used;

        void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (used)
                return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || (player.Health != null && player.Health.IsDead))
                return;
            used = true;
            if (GameFlow.Instance != null)
                GameFlow.Instance.CompleteStage(transform.position);
        }
    }
}
