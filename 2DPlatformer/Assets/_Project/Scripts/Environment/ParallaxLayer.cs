using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Transform 기반 패럴랙스. 카메라 이동량의 일정 비율만큼 따라가고, 가로로는 무한 반복한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(100)] // 카메라가 움직인 다음에 따라가야 한 프레임 늦게 떨리지 않는다
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("카메라 이동을 따라가는 비율. 1 = 카메라에 붙어 있음(아주 먼 배경), 0 = 월드에 고정")]
        [SerializeField] Vector2 follow = new(0.5f, 0.5f);
        [SerializeField] bool loopX = true;

        Transform cameraTransform;
        Vector3 offset;
        float loopWidth;

        void Start()
        {
            Camera mainCamera = Camera.main;
            cameraTransform = mainCamera.transform;
            offset = transform.position - FollowOffset(cameraTransform.position);

            if (loopX)
            {
                var spriteRenderer = GetComponent<SpriteRenderer>();
                loopWidth = spriteRenderer.sprite.bounds.size.x;

                // 카메라가 한 칸 범위 안 어디에 있어도 화면 양 끝이 비지 않도록 (화면 폭 + 여유 2칸)만큼 이어 붙인다
                float viewWidth = 2f * mainCamera.orthographicSize * mainCamera.aspect;
                int copies = Mathf.CeilToInt(viewWidth / loopWidth) + 2;
                spriteRenderer.drawMode = SpriteDrawMode.Tiled;
                spriteRenderer.size = new Vector2(
                    Mathf.Max(spriteRenderer.size.x, loopWidth * copies),
                    spriteRenderer.sprite.bounds.size.y);
            }
        }

        void LateUpdate()
        {
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 position = offset + FollowOffset(cameraPosition);
            if (loopX)
                position.x += Mathf.Round((cameraPosition.x - position.x) / loopWidth) * loopWidth;
            transform.position = position;
        }

        Vector3 FollowOffset(Vector3 cameraPosition) =>
            new(cameraPosition.x * follow.x, cameraPosition.y * follow.y, 0f);
    }
}
