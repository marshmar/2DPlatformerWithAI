using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 카메라가 보여줄 수 있는 영역(방). 화면이 이 상자 밖을 비추지 않도록 카메라를 가둔다 — 오른쪽 벽이나
    /// 다음 맵 입구에서 카메라가 더 따라가지 않게 하는 "멈춤 트리거". 다른 방으로 넘어가면 카메라가 그 방으로 옮겨간다.
    /// BoxCollider2D 는 Scene 뷰에서 영역을 끌어 조절하기 위한 용도이고, 판정은 CameraController 가 직접 한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DefaultExecutionOrder(-100)] // CameraController.Awake 보다 먼저 등록되도록
    public sealed class CameraRoom : MonoBehaviour
    {
        static readonly List<CameraRoom> rooms = new();

        [Tooltip("이 방으로 넘어올 때 카메라가 옮겨가는 시간 (초)")]
        [SerializeField] float transitionDuration = 0.5f;
        [Tooltip("넘어오는 동안 플레이어를 멈춘다. 멈추기 전 속도는 끝난 뒤 그대로 이어진다")]
        [SerializeField] bool freezePlayer = true;

        BoxCollider2D area;

        public float TransitionDuration => transitionDuration;
        public bool FreezePlayer => freezePlayer;
        public Rect Bounds => ToRect(area.bounds);

        public bool Contains(Vector2 point) => Bounds.Contains(point);

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
        }

        void OnEnable() => rooms.Add(this);
        void OnDisable() => rooms.Remove(this);

        public static CameraRoom Find(Vector2 point)
        {
            foreach (CameraRoom room in rooms)
                if (room.Contains(point))
                    return room;
            return null;
        }

        static Rect ToRect(Bounds bounds) => Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);

        void OnDrawGizmos()
        {
            Bounds bounds = GetComponent<BoxCollider2D>().bounds;
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
