using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 비 파티클 발생기를 화면 위쪽에 붙여 둔다. 파티클은 월드 공간에서 시뮬레이션되므로 발생기만 따라다니고
    /// 이미 떨어지는 빗줄기는 카메라를 따라 끌려가지 않는다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class RainFX : MonoBehaviour
    {
        [Tooltip("카메라 중심에서 발생기까지 (화면 위쪽 바깥)")]
        [SerializeField] Vector2 offset = new(2f, 13f);

        void LateUpdate()
        {
            Camera main = Camera.main;
            if (main != null)
                transform.position = (Vector2)main.transform.position + offset;
        }
    }
}
