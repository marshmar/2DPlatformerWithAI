using System;
using UnityEngine;

namespace Platformer
{
    /// <summary>한 번의 공격이 전하는 정보. 받는 쪽이 이것만 보고 반응(넉백·히트스톱·연출)을 정한다.</summary>
    public readonly struct DamageInfo
    {
        public readonly int Amount;
        public readonly Vector2 Direction;   // 맞는 쪽이 밀려날 방향
        public readonly Vector2 HitPoint;
        public readonly float Knockback;
        public readonly bool IsExecution;

        public DamageInfo(int amount, Vector2 direction, Vector2 hitPoint, float knockback, bool isExecution = false)
        {
            Amount = amount;
            Direction = direction;
            HitPoint = hitPoint;
            Knockback = knockback;
            IsExecution = isExecution;
        }
    }

    /// <summary>체력과 무적 시간. 피격 반응 자체는 이벤트를 받는 쪽(Enemy, PlayerController)이 한다.</summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] int maxHealth = 3;

        float invincibleUntil = float.NegativeInfinity;

        public int Max => maxHealth;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool IsInvincible => Time.time < invincibleUntil;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        void Awake() => Current = maxHealth;

        public void SetMax(int value)
        {
            maxHealth = value;
            Current = value;
        }

        /// <summary>실제로 데미지가 들어갔으면 true.</summary>
        public bool TakeDamage(in DamageInfo damage)
        {
            if (IsDead || IsInvincible)
                return false;

            Current = Mathf.Max(0, Current - damage.Amount);
            Damaged?.Invoke(damage);
            if (IsDead)
                Died?.Invoke(damage);
            return true;
        }

        public void SetInvincible(float duration) => invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);

        public void Restore()
        {
            Current = maxHealth;
            invincibleUntil = float.NegativeInfinity;
        }
    }
}
