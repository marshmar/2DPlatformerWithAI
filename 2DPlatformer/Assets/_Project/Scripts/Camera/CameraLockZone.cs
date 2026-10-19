using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 플레이어가 이 구역 안에 있는 동안 카메라를 고정 위치(Anchor)에 묶어 둔다. 축마다 따로 고정할 수 있다.
    /// 시작 지점에 두면 "어느 지점에 닿기 전까지 카메라가 따라오지 않는" 무빙 트리거가 되고,
    /// 보스방·막다른 곳에 두면 그 자리에서 카메라가 멈춘다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DefaultExecutionOrder(-100)] // CameraController.Awake 보다 먼저 등록되도록
    public sealed class CameraLockZone : MonoBehaviour
    {
        static readonly List<CameraLockZone> zones = new();

        [SerializeField] bool lockX = true;
        [SerializeField] bool lockY = true;
        [Tooltip("카메라 중심을 둘 위치 (이 오브젝트 기준). Scene 뷰에 화면 크기 상자로 표시된다")]
        [SerializeField] Vector2 anchorOffset;
        [Tooltip("들어오고 나갈 때 카메라가 옮겨가는 시간 (초)")]
        [SerializeField] float blendTime = 0.6f;
        [Tooltip("여러 구역이 겹치면 큰 값이 우선")]
        [SerializeField] int priority;

        BoxCollider2D area;

        public bool LockX => lockX;
        public bool LockY => lockY;
        public Vector2 Anchor => (Vector2)transform.position + anchorOffset;
        public float BlendTime => blendTime;

        public bool Contains(Vector2 point)
        {
            Bounds bounds = area.bounds;
            return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y).Contains(point);
        }

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
        }

        void OnEnable() => zones.Add(this);
        void OnDisable() => zones.Remove(this);

        public static CameraLockZone Find(Vector2 point)
        {
            CameraLockZone best = null;
            foreach (CameraLockZone zone in zones)
                if (zone.Contains(point) && (best == null || zone.priority > best.priority))
                    best = zone;
            return best;
        }

        void OnDrawGizmos()
        {
            Bounds bounds = GetComponent<BoxCollider2D>().bounds;
            Gizmos.color = new Color(1f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // 고정됐을 때 카메라가 비추는 화면
            Camera mainCamera = Camera.main;
            Vector2 viewSize = mainCamera != null
                ? new Vector2(mainCamera.orthographicSize * 2f * mainCamera.aspect, mainCamera.orthographicSize * 2f)
                : new Vector2(40f, 22.5f);
            Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.35f);
            Gizmos.DrawWireCube(Anchor, viewSize);
            Gizmos.DrawLine(bounds.center, Anchor);
        }
    }
}
