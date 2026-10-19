using UnityEngine;

namespace Platformer
{
    /// <summary>플레이어 공격·피격 튜닝 값.</summary>
    [CreateAssetMenu(menuName = "Platformer/Combat Config")]
    public sealed class CombatConfig : ScriptableObject
    {
        [field: Header("콤보"), SerializeField, Tooltip("공격 애니메이션 속도 (fps)")]
        public float AttackFps { get; private set; } = 18f;
        [field: SerializeField, Tooltip("각 타의 판정이 켜지는 프레임 [시작, 끝)")]
        public Vector2Int[] ActiveFrames { get; private set; } = { new(2, 4), new(3, 5), new(3, 6) };
        [field: SerializeField]
        public int[] Damage { get; private set; } = { 1, 1, 2 };
        [field: SerializeField, Tooltip("다음 타 입력을 미리 받아 두는 시간")]
        public float InputBuffer { get; private set; } = 0.18f;

        [field: Header("판정"), SerializeField, Tooltip("몸 중심에서 앞쪽으로의 판정 상자 위치")]
        public Vector2 HitboxOffset { get; private set; } = new(0.95f, 1.05f);
        [field: SerializeField]
        public Vector2 HitboxSize { get; private set; } = new(1.6f, 1.5f);

        [field: Header("공격 보정"), SerializeField, Tooltip("이 거리 안의 적에게는 공격 시작 때 살짝 끌려간다")]
        public float SnapRange { get; private set; } = 3.2f;
        [field: SerializeField]
        public float SnapSpeed { get; private set; } = 9f;
        [field: SerializeField, Tooltip("공중 공격 중 낙하 속도 상한 — 공중에서 잠깐 떠서 때리게")]
        public float AirAttackMaxFall { get; private set; } = 2.5f;

        [field: Header("타격감"), SerializeField, Tooltip("일반 타격 히트스톱 (실제 초)")]
        public float HitstopNormal { get; private set; } = 0.055f;
        [field: SerializeField]
        public float HitstopFinisher { get; private set; } = 0.1f;
        [field: SerializeField]
        public float ShakeNormal { get; private set; } = 0.25f;
        [field: SerializeField]
        public float ShakeFinisher { get; private set; } = 0.45f;
        [field: SerializeField]
        public float Knockback { get; private set; } = 7f;

        [field: Header("피격"), SerializeField]
        public int PlayerMaxHealth { get; private set; } = 5;
        [field: SerializeField, Tooltip("맞은 뒤 무적 시간")]
        public float InvincibleTime { get; private set; } = 1f;
        [field: SerializeField, Tooltip("맞고 조작을 잃는 시간")]
        public float HurtStunTime { get; private set; } = 0.3f;
        [field: SerializeField]
        public Vector2 HurtKnockback { get; private set; } = new(6f, 7f);

        [field: Header("처형"), SerializeField, Tooltip("갈고리로 잡은 적에게 날아가는 속도")]
        public float ExecutionDashSpeed { get; private set; } = 34f;
        [field: SerializeField]
        public float ExecutionSlowScale { get; private set; } = 0.15f;
        [field: SerializeField, Tooltip("처형 후 튕겨 오르는 속도")]
        public float ExecutionBounce { get; private set; } = 12f;
    }
}
