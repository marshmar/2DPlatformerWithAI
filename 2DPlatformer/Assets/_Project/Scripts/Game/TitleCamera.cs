using UnityEngine;

namespace Platformer
{
    /// <summary>타이틀 배경 카메라: 도시 위를 천천히 좌우로 흘러간다. 패럴랙스 레이어·비가 카메라를 따라 움직인다.</summary>
    [DefaultExecutionOrder(50)] // 패럴랙스(100) 앞
    public sealed class TitleCamera : MonoBehaviour
    {
        [SerializeField] float centerX;
        [SerializeField] float range = 14f;
        [Tooltip("한 번 왕복하는 데 걸리는 시간 (초)")]
        [SerializeField] float period = 70f;

        float startTime;

        public void Configure(float center, float halfRange)
        {
            centerX = center;
            range = halfRange;
        }

        void Awake()
        {
            startTime = Time.unscaledTime;
            Apply(0f);
        }

        void LateUpdate() => Apply(Time.unscaledTime - startTime);

        void Apply(float time)
        {
            Vector3 position = transform.position;
            // 가운데에서 출발해서 왼쪽 → 오른쪽으로 부드럽게 왕복
            position.x = centerX + Mathf.Sin(time / period * Mathf.PI * 2f) * range;
            transform.position = position;
        }
    }
}
