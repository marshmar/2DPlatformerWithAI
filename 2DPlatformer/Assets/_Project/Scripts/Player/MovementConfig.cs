using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 이동 튜닝 값. 에셋이라서 플레이 중에 바꾼 값이 플레이를 끝내도 남는다.
    /// 단위는 유닛(= 타일 1칸 = 16px)과 초.
    /// </summary>
    [CreateAssetMenu(menuName = "Platformer/Movement Config")]
    public sealed class MovementConfig : ScriptableObject
    {
        [field: Header("달리기"), SerializeField]
        public float MaxRunSpeed { get; private set; } = 8.5f;
        [field: SerializeField]
        public float GroundAcceleration { get; private set; } = 85f;
        [field: SerializeField]
        public float GroundDeceleration { get; private set; } = 120f;
        [field: SerializeField, Tooltip("반대 방향을 눌렀을 때. 가속보다 크면 방향 전환이 날렵해진다")]
        public float GroundTurnAcceleration { get; private set; } = 170f;

        [field: Header("공중 제어"), SerializeField]
        public float AirAcceleration { get; private set; } = 55f;
        [field: SerializeField]
        public float AirDeceleration { get; private set; } = 25f;
        [field: SerializeField]
        public float AirTurnAcceleration { get; private set; } = 80f;

        [field: Header("점프"), SerializeField, Tooltip("최고 높이 (타일 칸 수)")]
        public float JumpHeight { get; private set; } = 3.25f;
        [field: SerializeField, Tooltip("최고점까지 걸리는 시간 (초)")]
        public float TimeToApex { get; private set; } = 0.36f;
        [field: SerializeField, Tooltip("떨어질 때 중력 배수. 1보다 크면 올라갈 때보다 빨리 떨어져서 무게감이 생긴다")]
        public float FallGravityMultiplier { get; private set; } = 1.8f;
        [field: SerializeField]
        public float MaxFallSpeed { get; private set; } = 22f;

        [field: Header("점프 보정"), SerializeField, Tooltip("발판에서 떨어진 뒤에도 점프를 받아주는 시간 (초)")]
        public float CoyoteTime { get; private set; } = 0.1f;
        [field: SerializeField, Tooltip("착지 전에 미리 누른 점프를 기억해 두는 시간 (초)")]
        public float JumpBufferTime { get; private set; } = 0.1f;
        [field: SerializeField, Tooltip("버튼을 바로 뗐을 때의 높이. 누르고 있는 시간에 따라 이 값과 JumpHeight 사이로 정해진다")]
        public float MinJumpHeight { get; private set; } = 1.25f;
        [field: SerializeField, Tooltip("세로 속도가 이보다 느리면 최고점 근처로 본다")]
        public float ApexThreshold { get; private set; } = 2f;
        [field: SerializeField, Tooltip("최고점 근처에서 버튼을 누르고 있으면 중력에 곱한다. 1보다 작으면 잠깐 떠 있는다")]
        public float ApexGravityMultiplier { get; private set; } = 0.5f;

        [field: Header("벽"), SerializeField, Tooltip("벽에 붙어 미끄러질 때 최대 낙하 속도")]
        public float WallSlideSpeed { get; private set; } = 4f;
        [field: SerializeField, Tooltip("빠르게 떨어지다 벽에 붙었을 때 미끄럼 속도까지 줄이는 감속")]
        public float WallSlideDeceleration { get; private set; } = 60f;
        [field: SerializeField, Tooltip("벽 점프 높이 (타일 칸 수)")]
        public float WallJumpHeight { get; private set; } = 2.75f;
        [field: SerializeField, Tooltip("벽 점프로 튕겨 나가는 가로 속도")]
        public float WallJumpHorizontalSpeed { get; private set; } = 9f;
        [field: SerializeField, Tooltip("벽 점프 직후 좌우 입력을 무시하는 시간. 같은 벽에 바로 다시 붙지 않게 한다")]
        public float WallJumpInputLockTime { get; private set; } = 0.15f;

        [field: Header("벽 보정"), SerializeField, Tooltip("벽 쪽 입력을 놓거나 반대로 눌러도 이 시간 동안은 붙어 있는다. 반대 방향+점프를 넣을 여유")]
        public float WallStickTime { get; private set; } = 0.12f;
        [field: SerializeField, Tooltip("벽에서 떨어진 뒤에도 벽 점프를 받아주는 시간 (초)")]
        public float WallCoyoteTime { get; private set; } = 0.1f;
        [field: SerializeField, Tooltip("미끄러지는 중이 아니어도 벽이 이만큼 안에 있으면 벽 점프를 받아준다 (픽셀)")]
        public int WallJumpGracePixels { get; private set; } = 3;

        [field: Header("충돌 보정 (픽셀)"), SerializeField, Tooltip("상승 중 천장 모서리에 머리가 이만큼 이하로 걸리면 옆으로 밀어서 통과시킨다")]
        public int CornerCorrectionPixels { get; private set; } = 4;
        [field: SerializeField, Tooltip("발판 윗면이 발보다 이만큼 이하로 높으면 위로 올려서 착지시킨다")]
        public int LedgeCorrectionPixels { get; private set; } = 4;

        // 높이 h 를 시간 t 만에 찍는 포물선: h = v·t − g·t²/2, 최고점에서 v − g·t = 0 → g = 2h/t², v = 2h/t
        // 중력과 초속을 직접 만지면 둘이 얽혀서 튜닝이 어렵기 때문에 "높이"와 "시간"으로 조절한다
        public float Gravity => 2f * JumpHeight / (TimeToApex * TimeToApex);
        public float JumpVelocity => 2f * JumpHeight / TimeToApex;

        // 버튼을 떼면 상승 중 중력을 k배로: 초속 v 에서 v²/(2·g·k) = JumpHeight / k 만큼 더 올라간다.
        // 바로 뗐을 때 MinJumpHeight 에서 멈추도록 k = JumpHeight / MinJumpHeight
        public float JumpReleaseGravityMultiplier => JumpHeight / MinJumpHeight;

        // 같은 중력으로 높이 h 까지 올라가는 초속: v²/(2g) = h
        public float WallJumpVelocity => Mathf.Sqrt(2f * Gravity * WallJumpHeight);
    }
}
