using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 플레이어 추적 카메라. 목표 위치는 세 단계로 정한다:
    ///   1) focus — 데드존의 중심. 플레이어가 데드존 밖으로 나간 만큼만 움직인다. (+ 룩어헤드)
    ///   2) 제약 — 고정 구역(CameraLockZone)이 축을 묶고, 방(CameraRoom)이 화면을 방 안에 가둔다.
    ///   3) 이동 — 평소엔 SmoothDamp 로 따라가고, 구역·방이 바뀔 때는 정해진 시간 동안 블렌드한다.
    /// 위치는 소수 그대로 둔다. 픽셀 스냅은 Pixel Perfect Camera(Pixel Snapping)가 렌더 직전에 view 행렬로 처리한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(50)] // 플레이어 이동 뒤, 패럴랙스(100) 앞
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] CameraConfig config;
        [SerializeField] PlayerController target;
        [Header("쉐이크")]
        [SerializeField] float maxShakeOffset = 0.5f;
        [SerializeField] float maxShakeRoll = 2.5f;
        [Tooltip("초당 줄어드는 트라우마")]
        [SerializeField] float traumaDecay = 1.8f;

        Camera viewCamera;
        float trauma;
        Vector2 shakeOffset;
        float shakeRoll;
        Vector2 focus;
        Vector2 position;
        Vector2 smoothVelocity;
        float lookAhead;
        float lookAheadTarget;
        float lookAheadVelocity;

        CameraRoom room;
        CameraLockZone lockZone;
        Vector2 blendFrom;
        float blendElapsed;
        float blendDuration; // 0 이면 블렌드 중이 아님

        Vector2 TargetPoint => (Vector2)target.transform.position + config.FramingOffset;
        // 방·구역 판정 기준은 발이 아니라 몸 중심 — 문턱에 발만 걸쳤을 때 넘어가지 않게
        Vector2 BodyPoint => (Vector2)target.transform.position + Vector2.up;

        public static CameraController Instance { get; private set; }

        // Start 보다 먼저 자리를 잡아야 패럴랙스 레이어가 올바른 기준 위치를 기억한다
        void Awake()
        {
            Instance = this;
            viewCamera = GetComponent<Camera>();
            SnapToTarget();
        }

        /// <summary>
        /// 화면을 흔든다. 흔들림 세기는 트라우마의 제곱이라 작은 충격은 은은하고 큰 충격은 확 흔들린다.
        /// 히트스톱 중에도 흔들려야 타격감이 살기 때문에 실제 시간으로 줄어든다.
        /// </summary>
        public static void Shake(float amount)
        {
            if (Instance != null)
                Instance.trauma = Mathf.Clamp01(Instance.trauma + amount * GameSettings.ScreenShake);
        }

        /// <summary>감쇠·블렌드 없이 즉시 플레이어 위치로 옮긴다 (시작, 리스폰, 씬 전환).</summary>
        public void SnapToTarget()
        {
            room = CameraRoom.Find(BodyPoint);
            lockZone = CameraLockZone.Find(BodyPoint);
            focus = TargetPoint;
            smoothVelocity = Vector2.zero;
            lookAhead = lookAheadTarget = lookAheadVelocity = 0f;
            blendDuration = 0f;
            position = Constrain(focus);
            Apply();
        }

        void LateUpdate()
        {
            UpdateShake(Time.unscaledDeltaTime);

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                Apply(); // 히트스톱 중엔 위치는 그대로, 흔들림만
                return;
            }

            UpdateFocus();
            UpdateLookAhead(deltaTime);
            UpdateRegions();

            Vector2 desired = Constrain(focus + new Vector2(lookAhead, 0f));
            if (blendDuration > 0f)
            {
                blendElapsed += deltaTime;
                float t = Mathf.Clamp01(blendElapsed / blendDuration);
                position = Vector2.Lerp(blendFrom, desired, t * t * (3f - 2f * t)); // smoothstep: 천천히 출발해서 천천히 도착
                if (t >= 1f)
                {
                    blendDuration = 0f;
                    smoothVelocity = Vector2.zero;
                }
            }
            else
            {
                position.x = Mathf.SmoothDamp(position.x, desired.x, ref smoothVelocity.x, config.SmoothTimeX, Mathf.Infinity, deltaTime);
                position.y = Mathf.SmoothDamp(position.y, desired.y, ref smoothVelocity.y, config.SmoothTimeY, Mathf.Infinity, deltaTime);
                position = ClampToRoom(position); // 감쇠가 살짝 넘치더라도 방 밖은 비추지 않게
            }
            Apply();
        }

        void UpdateFocus()
        {
            Vector2 point = TargetPoint;

            // 가로: 플레이어가 데드존 가장자리를 밀고 나간 만큼만 focus 를 끌고 간다
            float halfWidth = config.DeadzoneWidth * 0.5f;
            focus.x = Mathf.Clamp(focus.x, point.x - halfWidth, point.x + halfWidth);

            // 세로: 서 있을 때만 발 높이에 맞춘다 — 점프할 때마다 화면이 출렁이지 않게.
            // 공중·벽에서는 위아래 데드존을 벗어난 만큼만 따라간다 (높이 떨어지면 결국 따라 내려감)
            if (target.IsGrounded)
                focus.y = point.y;
            else
                focus.y = Mathf.Clamp(focus.y, point.y - config.DeadzoneUp, point.y + config.DeadzoneDown);
        }

        void UpdateLookAhead(float deltaTime)
        {
            float velocityX = target.Velocity.x;
            if (Mathf.Abs(velocityX) > config.LookAheadMinSpeed)
                lookAheadTarget = Mathf.Sign(velocityX) * config.LookAheadDistance;

            // 멈춰도 마지막 방향을 유지한다 — 멈출 때마다 카메라가 되돌아오면 어지럽다
            lookAhead = Mathf.SmoothDamp(lookAhead, lookAheadTarget, ref lookAheadVelocity,
                config.LookAheadSmoothTime, Mathf.Infinity, deltaTime);
        }

        void UpdateRegions()
        {
            Vector2 point = BodyPoint;

            // 지금 방 안에 있는 동안은 방을 바꾸지 않는다 — 출입구에서 두 방이 겹쳐도 왔다 갔다 하지 않게.
            // 어느 방에도 없으면(방 사이 틈) 마지막 방을 유지한다
            if (room == null || !room.Contains(point))
            {
                CameraRoom next = CameraRoom.Find(point);
                if (next != null && next != room)
                {
                    bool isTransition = room != null;
                    room = next;
                    if (isTransition)
                    {
                        StartBlend(next.TransitionDuration);
                        if (next.FreezePlayer)
                            target.Freeze(next.TransitionDuration);
                    }
                }
            }

            CameraLockZone zone = CameraLockZone.Find(point);
            if (zone != lockZone)
            {
                // 들어갈 땐 새 구역의, 나올 땐 떠나는 구역의 블렌드 시간을 쓴다
                StartBlend(zone != null ? zone.BlendTime : lockZone.BlendTime);
                lockZone = zone;
            }
        }

        void StartBlend(float duration)
        {
            blendFrom = position;
            blendElapsed = 0f;
            blendDuration = duration;
        }

        Vector2 Constrain(Vector2 desired)
        {
            if (lockZone != null)
            {
                Vector2 anchor = lockZone.Anchor;
                if (lockZone.LockX)
                    desired.x = anchor.x;
                if (lockZone.LockY)
                    desired.y = anchor.y;
            }
            return ClampToRoom(desired);
        }

        // 화면 사각형이 방 안에 들어오도록 카메라 중심을 제한한다. 방이 화면보다 좁은 축은 방 가운데에 고정
        Vector2 ClampToRoom(Vector2 center)
        {
            if (room == null)
                return center;

            Rect bounds = room.Bounds;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            center.x = bounds.width <= halfWidth * 2f
                ? bounds.center.x
                : Mathf.Clamp(center.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            center.y = bounds.height <= halfHeight * 2f
                ? bounds.center.y
                : Mathf.Clamp(center.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
            return center;
        }

        // 펄린 노이즈: 무작위 값보다 부드럽게 이어져서 "덜덜"이 아니라 "쿵" 하고 흔들린다
        void UpdateShake(float deltaTime)
        {
            float strength = trauma * trauma;
            float t = Time.unscaledTime * 25f;
            shakeOffset = new Vector2(Mathf.PerlinNoise(t, 0f) * 2f - 1f, Mathf.PerlinNoise(0f, t) * 2f - 1f) * (maxShakeOffset * strength);
            shakeRoll = (Mathf.PerlinNoise(t, t) * 2f - 1f) * maxShakeRoll * strength;
            trauma = Mathf.Max(0f, trauma - traumaDecay * deltaTime);
        }

        void Apply()
        {
            transform.SetPositionAndRotation(
                new Vector3(position.x + shakeOffset.x, position.y + shakeOffset.y, transform.position.z),
                Quaternion.Euler(0f, 0f, shakeRoll));
        }

        // 플레이어 발이 이 상자 안에 있는 동안 카메라가 움직이지 않는다 (공중 기준. 서 있을 땐 세로가 발 높이에 맞춰짐)
        void OnDrawGizmos()
        {
            if (!Application.isPlaying || config == null || target == null)
                return;

            float halfWidth = config.DeadzoneWidth * 0.5f;
            Vector2 feetFocus = focus - config.FramingOffset;
            Vector2 min = feetFocus + new Vector2(-halfWidth, -config.DeadzoneDown);
            Vector2 max = feetFocus + new Vector2(halfWidth, config.DeadzoneUp);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

            // 카메라가 따라가려는 목표 (focus + 룩어헤드)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(focus + new Vector2(lookAhead, 0f), 0.25f);
        }
    }
}
