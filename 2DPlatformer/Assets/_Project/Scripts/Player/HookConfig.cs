using UnityEngine;

namespace Platformer
{
    /// <summary>갈고리 팔 튜닝 값. 단위는 유닛(타일 칸)과 초.</summary>
    [CreateAssetMenu(menuName = "Platformer/Hook Config")]
    public sealed class HookConfig : ScriptableObject
    {
        [field: Header("발사"), SerializeField, Tooltip("갈고리가 닿는 최대 거리")]
        public float Range { get; private set; } = 11f;
        [field: SerializeField, Tooltip("갈고리 끝이 날아가는 속도. 순간이동이 아니라 날아가는 게 보여야 손맛이 난다")]
        public float TipSpeed { get; private set; } = 75f;
        [field: SerializeField, Tooltip("빗나간 갈고리가 돌아오는 속도")]
        public float RetractSpeed { get; private set; } = 90f;

        [field: Header("조준 보정"), SerializeField, Tooltip("조준 방향에서 이 각도 안의 걸 수 있는 지점을 찾아 준다 (도)")]
        public float AssistAngle { get; private set; } = 12f;
        [field: SerializeField, Tooltip("부채꼴 검사 레이 간격 (도)")]
        public float AssistStep { get; private set; } = 3f;
        [field: SerializeField, Tooltip("적은 벽보다 이 각도만큼 더 넓게 찾고 우선한다 (처형 연결)")]
        public float EnemyAssistBonusAngle { get; private set; } = 8f;
        [field: SerializeField, Tooltip("발사 버튼을 갈고리를 쏠 수 없을 때 미리 눌렀어도 이 시간 안이면 받아 준다")]
        public float InputBuffer { get; private set; } = 0.12f;

        [field: Header("스윙"), SerializeField, Tooltip("로프 최소 길이 — 너무 짧으면 제자리에서 빙빙 돈다")]
        public float MinRopeLength { get; private set; } = 1.5f;
        [field: SerializeField, Tooltip("스윙 중 좌우 입력의 접선 가속")]
        public float SwingAcceleration { get; private set; } = 22f;
        [field: SerializeField, Tooltip("스윙 중 최대 속도")]
        public float MaxSwingSpeed { get; private set; } = 20f;
        [field: SerializeField, Tooltip("버튼을 떼며 놓을 때 속도에 곱하는 보너스 — 놓는 순간 살짝 튀어나가는 쾌감")]
        public float ReleaseBoost { get; private set; } = 1.12f;
        [field: SerializeField, Tooltip("스윙 중 점프로 놓을 때 보장하는 최소 상승 속도")]
        public float JumpReleaseVelocity { get; private set; } = 12f;

        [field: Header("당기기 (대시)"), SerializeField, Tooltip("Shift 로 로프를 감으며 날아가는 속도")]
        public float ReelSpeed { get; private set; } = 26f;
        [field: SerializeField, Tooltip("고정점에 이만큼 가까워지면 감기를 끝내고 운동량을 유지한 채 놓는다")]
        public float ReelArriveDistance { get; private set; } = 1.2f;
    }
}
