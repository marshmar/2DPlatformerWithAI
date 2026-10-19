using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 위험 지대(Hazard 타일맵 등). 닿으면 피해를 입고, 마지막으로 안전하게 서 있던 곳으로 돌아간다
    /// (끄면 위로 튕겨 나간다). 무적 시간 동안은 다시 맞지 않는다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class HazardZone : MonoBehaviour
    {
        [SerializeField] int damage = 1;
        [SerializeField] float bounce = 13f;
        [Tooltip("닿으면 안전한 발판으로 되돌린다. 깊은 가시 구덩이에서 튕기기만 반복하다 갇히지 않게")]
        [SerializeField] bool returnToSafeGround = true;

        void OnTriggerStay2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.Health == null)
                return;

            // 바라보던 반대쪽 위로 밀어낸다 — 가시밭에 들어간 방향으로 계속 밀려 들어가지 않게
            var direction = new Vector2(-player.Facing, 1f).normalized;
            if (!player.Health.TakeDamage(new DamageInfo(damage, direction, player.BodyCenter, 0f)))
                return;
            if (returnToSafeGround)
                player.ReturnToSafeGround();
            else
                player.Velocity = new Vector2(player.Velocity.x, bounce);
        }
    }
}
