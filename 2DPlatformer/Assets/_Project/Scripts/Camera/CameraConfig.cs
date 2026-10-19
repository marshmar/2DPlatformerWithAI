using UnityEngine;

namespace Platformer
{
    /// <summary>카메라 추적 튜닝 값. 에셋이라서 플레이 중에 바꾼 값이 남는다. 단위는 유닛(타일 칸)과 초.</summary>
    [CreateAssetMenu(menuName = "Platformer/Camera Config")]
    public sealed class CameraConfig : ScriptableObject
    {
        [field: Header("프레이밍"), SerializeField, Tooltip("플레이어 발밑에서 카메라 중심까지. 위쪽을 더 보여준다")]
        public Vector2 FramingOffset { get; private set; } = new(0f, 3f);

        [field: Header("데드존"), SerializeField, Tooltip("플레이어가 이 가로 폭 안에서 움직이는 동안 카메라는 가만히 있다")]
        public float DeadzoneWidth { get; private set; } = 3f;
        [field: SerializeField, Tooltip("공중에서 이만큼 위로 올라가기 전까지 카메라가 따라 올라가지 않는다")]
        public float DeadzoneUp { get; private set; } = 3f;
        [field: SerializeField, Tooltip("공중에서 이만큼 아래로 떨어지기 전까지 카메라가 따라 내려가지 않는다")]
        public float DeadzoneDown { get; private set; } = 1.5f;

        [field: Header("룩어헤드"), SerializeField, Tooltip("이동 방향 앞쪽을 더 보여주도록 카메라를 앞당기는 거리")]
        public float LookAheadDistance { get; private set; } = 3f;
        [field: SerializeField]
        public float LookAheadSmoothTime { get; private set; } = 0.6f;
        [field: SerializeField, Tooltip("이보다 빠르게 움직여야 룩어헤드 방향을 바꾼다. 제자리에서 방향만 돌릴 때 카메라가 흔들리지 않게")]
        public float LookAheadMinSpeed { get; private set; } = 2f;

        [field: Header("감쇠"), SerializeField, Tooltip("목표 위치까지 따라붙는 데 걸리는 대략적인 시간 (초)")]
        public float SmoothTimeX { get; private set; } = 0.12f;
        [field: SerializeField]
        public float SmoothTimeY { get; private set; } = 0.2f;
    }
}
