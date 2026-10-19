using System;
using System.Collections;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 플레이어 몸체. 물리 상태(속도, 접지)와 이동 동작을 제공하고, 무엇을 할지는 현재 상태(PlayerState)가 정한다.
    /// 중력은 Rigidbody2D 대신 직접 적용한다 — 올라갈 때와 떨어질 때 중력을 다르게 주기 위해서.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] CombatConfig combat;
        // 보정용 검사 상자를 이만큼 줄여서, 옆에 붙어 있는 벽을 "막혔다"로 오인하지 않게 한다
        const float ProbeSkin = 0.01f;
        // 벽 슬라이드 중 벽 쪽으로 미는 속도. 마찰이 0 이라 밀지 않으면 벽과 떨어져 접촉 판정이 끊긴다
        const float WallPushSpeed = 0.5f;

        [SerializeField] MovementConfig config;
        [SerializeField] LayerMask groundMask;
        [Tooltip("이 거리 안에 바닥이 있으면 서 있는 것으로 본다 (1px = 0.0625)")]
        [SerializeField] float groundCheckDistance = 0.05f;
        [Tooltip("이 높이 아래로 떨어지면 피해를 입고 마지막으로 안전하게 서 있던 곳으로 돌아간다 (레벨 빌더가 맵 바닥에 맞춰 정한다)")]
        [SerializeField] float fallOutY = -30f;

        readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
        readonly RaycastHit2D[] probeHits = new RaycastHit2D[1];
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];
        ContactFilter2D groundFilter;
        ContactFilter2D solidFilter;
        BoxCollider2D box;
        Vector2 spawnPosition;
        Vector2 safePosition;
        int hazardMask;
        float lastGroundedTime = float.NegativeInfinity;
        float landedTime = float.NegativeInfinity;
        float lastWallTime = float.NegativeInfinity;
        float controlLockUntil = float.NegativeInfinity;
        float frozenUntil = float.NegativeInfinity;
        bool wasFrozen;
        Vector2 frozenVelocity;
        float lastAirVelocityY;
        bool respawning;

        public MovementConfig Config => config;
        public CombatConfig Combat => combat;
        public Health Health { get; private set; }
        public GrappleHook Hook { get; private set; }
        public SpriteFX SpriteFX { get; private set; }
        public Vector2 BodyCenter => Body.position + Vector2.up;

        // 선택 컴포넌트는 == null 로 확인한다. 에디터의 GetComponent 는 없을 때 "가짜 null" 을 돌려줄 수 있어서 ?. 는 안전하지 않다
        AfterimageEmitter afterimage;

        public void SetAfterimage(bool on)
        {
            if (afterimage != null)
                afterimage.SetActive(on);
        }

        public void DetachHook()
        {
            if (Hook != null)
                Hook.Detach();
        }

        /// <summary>착지 순간 (낙하 속도). 먼지 효과 등이 듣는다.</summary>
        public event Action<float> Landed;
        public event Action Jumped;
        public event Action<int> WallJumped;
        public PlayerInputReader Input { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public bool IsGrounded { get; private set; }
        public int Facing { get; private set; } = 1;
        public PlayerState State { get; private set; }

        /// <summary>점프로 떠 있는 중인가. 나중에 갈고리 반동·넉백으로 뜬 것과 구분해서 점프에만 가변 높이·정점 체공을 건다.</summary>
        public bool IsJumping { get; private set; }

        // 디버그 오버레이용: 마지막으로 발동한 보정
        public string LastAssist { get; private set; }
        public float LastAssistTime { get; private set; } = float.NegativeInfinity;

        /// <summary>마지막으로 붙어 있던 벽 방향 (벽 코요테용)</summary>
        public int LastWallDirection { get; private set; }

        public GroundedState GroundedState { get; private set; }
        public AirborneState AirborneState { get; private set; }
        public WallSlideState WallSlideState { get; private set; }
        public HookedState HookedState { get; private set; }
        public AttackState AttackState { get; private set; }
        public HurtState HurtState { get; private set; }
        public ExecutionState ExecutionState { get; private set; }

        public bool AttackRequested => combat != null && Time.time - Input.AttackPressedTime <= combat.InputBuffer;
        public bool DashRequested => Time.time - Input.DashPressedTime <= 0.12f;

        /// <summary>지금 갈고리를 쏠 수 있는 상태인가. 피격·처형 중이거나 이미 매달려 있으면 안 된다.</summary>
        public bool CanUseHook => !respawning && !IsFrozen && State != HurtState && State != ExecutionState && State != HookedState
                                  && (State != AttackState || AttackState.CanCancel);

        public Vector2 Velocity
        {
            get => Body.linearVelocity;
            set => Body.linearVelocity = value;
        }

        public bool JumpRequested => Time.time - Input.JumpPressedTime <= config.JumpBufferTime;
        public bool CanCoyoteJump => Time.time - lastGroundedTime <= config.CoyoteTime;
        public float CoyoteTimeLeft => Mathf.Max(0f, config.CoyoteTime - (Time.time - lastGroundedTime));
        public float JumpBufferLeft => Mathf.Max(0f, config.JumpBufferTime - (Time.time - Input.JumpPressedTime));
        public bool CanWallCoyoteJump => Time.time - lastWallTime <= config.WallCoyoteTime;
        public float WallCoyoteLeft => Mathf.Max(0f, config.WallCoyoteTime - (Time.time - lastWallTime));
        public bool IsControlLocked => Time.time < controlLockUntil;
        public bool IsFrozen => Time.time < frozenUntil;
        public float ControlLockLeft => Mathf.Max(0f, controlLockUntil - Time.time);

        /// <summary>벽 슬라이드를 유지하는 접촉 거리 (1px)</summary>
        public float WallContactDistance => ProbeSkin + PixelGrid.UnitsPerPixel;
        /// <summary>미끄러지지 않아도 벽 점프를 받아주는 거리</summary>
        public float WallJumpGraceDistance => ProbeSkin + config.WallJumpGracePixels * PixelGrid.UnitsPerPixel;

        Vector2 ProbeSize => box.size + Vector2.one * (box.edgeRadius * 2f - ProbeSkin * 2f);

        void Awake()
        {
            Input = GetComponent<PlayerInputReader>();
            Body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            spawnPosition = safePosition = Body.position;
            hazardMask = LayerMask.GetMask("Hazard");

            groundFilter.useTriggers = false;
            groundFilter.SetLayerMask(groundMask);
            groundFilter.SetNormalAngle(45f, 135f); // 위를 향한 면(경사 45° 이내)만 바닥으로 친다
            solidFilter.useTriggers = false;
            solidFilter.SetLayerMask(groundMask);

            GroundedState = new GroundedState(this);
            AirborneState = new AirborneState(this);
            WallSlideState = new WallSlideState(this);
            HookedState = new HookedState(this);
            AttackState = new AttackState(this);
            HurtState = new HurtState(this);
            ExecutionState = new ExecutionState(this);
            State = AirborneState;
            State.Enter();

            Hook = GetComponent<GrappleHook>();
            Health = GetComponent<Health>();
            SpriteFX = GetComponentInChildren<SpriteFX>();
            afterimage = GetComponent<AfterimageEmitter>();
            if (Health != null)
            {
                if (combat != null)
                    Health.SetMax(combat.PlayerMaxHealth);
                Health.Damaged += OnDamaged;
                Health.Died += OnDied;
            }
        }

        public void StartSwing() => ChangeState(HookedState);

        public void StartExecution(Enemy enemy) => ExecutionState.Begin(enemy);

        public void LockControl(float duration) => controlLockUntil = Mathf.Max(controlLockUntil, Time.time + duration);

        /// <summary>쓰러졌을 때 다시 시작할 위치 (체크포인트).</summary>
        public void SetCheckpoint(Vector2 position) => spawnPosition = safePosition = position;

        /// <summary>일시정지 메뉴의 "체크포인트에서 다시 시작".</summary>
        public void RestartAtCheckpoint()
        {
            StopAllCoroutines();
            respawning = false;
            if (SpriteFX != null)
                SpriteFX.SetDissolve(0f);
            if (Health != null)
                Health.Restore();
            Respawn();
        }

        /// <summary>
        /// 가시밭·전기 바닥에 닿거나 맵 밖으로 떨어졌을 때: 잠깐 지지직거리며 멈췄다가 마지막으로 안전하게 서 있던 곳으로 돌아온다.
        /// 깊은 구덩이에 빠져 튕기기만 반복하다 죽는 일이 없게 한다 (피해는 그대로 받는다).
        /// </summary>
        public void ReturnToSafeGround()
        {
            if (respawning || (Health != null && Health.IsDead))
                return;
            StartCoroutine(SafeGroundRoutine());
        }

        IEnumerator SafeGroundRoutine()
        {
            respawning = true;
            DetachHook();
            PostFX.Glitch(0.5f, 0.35f);
            if (SpriteFX != null)
                SpriteFX.Glitch(1f, 0.6f);
            Freeze(0.35f);
            yield return new WaitForSecondsRealtime(0.35f);
            respawning = false;
            if (Health != null && Health.IsDead)
                yield break; // 그 사이 쓰러졌으면 DieAndRespawn 이 체크포인트로 보낸다
            Teleport(safePosition);
            if (SpriteFX != null)
                SpriteFX.Glitch(0.8f, 0.4f);
        }

        void FellOut()
        {
            if (respawning)
                return;
            if (Health != null)
                Health.TakeDamage(new DamageInfo(1, Vector2.up, BodyCenter, 0f));
            ReturnToSafeGround();
        }

        // 위험 지대 바로 옆이나 발판 모서리는 기록하지 않는다 — 돌아오자마자 다시 닿거나 떨어지면 같은 일이 반복된다
        void TrackSafeGround()
        {
            if (State != GroundedState || Time.time - landedTime < 0.1f)
                return;
            Vector2 center = Body.position + box.offset;
            if (Physics2D.OverlapBox(center, box.size + new Vector2(1.5f, 0.5f), 0f, hazardMask) != null)
                return;
            float half = box.size.x * 0.5f;
            if (!Physics2D.Raycast(Body.position + new Vector2(-half, 0.1f), Vector2.down, 0.3f, groundMask) ||
                !Physics2D.Raycast(Body.position + new Vector2(half, 0.1f), Vector2.down, 0.3f, groundMask))
                return;
            safePosition = Body.position;
        }

        void OnDamaged(DamageInfo damage)
        {
            if (!Health.IsDead)
                HurtState.Begin(damage);
        }

        void OnDied(DamageInfo damage)
        {
            if (!respawning)
                StartCoroutine(DieAndRespawn());
        }

        // 쓰러지면 화면이 찢어지는 글리치와 함께 잠깐 멈췄다가 시작 위치에서 다시 시작한다
        IEnumerator DieAndRespawn()
        {
            respawning = true;
            DetachHook();
            PostFX.Glitch(1f, 0.8f);
            PostFX.DamagePulse(1f, 0.8f);
            if (SpriteFX != null)
                SpriteFX.SetDissolve(0.6f);
            Freeze(0.8f);
            yield return new WaitForSecondsRealtime(0.8f);
            if (SpriteFX != null)
                SpriteFX.SetDissolve(0f);
            Health.Restore();
            Health.SetInvincible(1f);
            Respawn();
            respawning = false;
        }

        void FixedUpdate()
        {
            if (IsFrozen)
            {
                Velocity = Vector2.zero;
                wasFrozen = true;
                return;
            }
            if (wasFrozen)
            {
                wasFrozen = false;
                Velocity = frozenVelocity; // 멈추기 전 속도를 그대로 이어서, 방을 넘어가도 흐름이 끊기지 않게
            }

            IsGrounded = CheckGround();
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                TrackSafeGround();
            }
            else
            {
                lastAirVelocityY = Velocity.y;
            }

            State.FixedTick();
            FXGlobals.PlayerPosition = BodyCenter;

            if (Body.position.y < fallOutY)
                FellOut();
        }

        /// <summary>duration 동안 제자리에 멈춘다 (룸 전환 등). 입력은 계속 기록되므로 그 사이 누른 점프는 버퍼로 남는다.</summary>
        public void Freeze(float duration)
        {
            if (!IsFrozen && !wasFrozen)
                frozenVelocity = Velocity;
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
        }

        public void ChangeState(PlayerState next)
        {
            State.Exit();
            State = next;
            State.Enter();
        }

        /// <summary>좌우 입력 쪽으로 가속. 입력이 없으면 감속, 반대로 누르면 방향 전환 가속을 쓴다.</summary>
        public void Run(float acceleration, float deceleration, float turnAcceleration)
        {
            // 벽 점프 직후에는 입력을 무시하고 튕겨 나간 속도를 그대로 유지한다
            if (IsControlLocked)
                return;

            int input = Input.MoveX;
            Vector2 velocity = Velocity;

            float rate;
            if (input == 0)
                rate = deceleration;
            else if (velocity.x != 0f && Mathf.Sign(velocity.x) != input)
                rate = turnAcceleration;
            else
                rate = acceleration;

            velocity.x = Mathf.MoveTowards(velocity.x, input * config.MaxRunSpeed, rate * Time.fixedDeltaTime);
            Velocity = velocity;

            if (input != 0)
                Facing = input;
        }

        public void ApplyGravity()
        {
            Vector2 velocity = Velocity;
            float multiplier = velocity.y < 0f ? config.FallGravityMultiplier : 1f;

            if (IsJumping)
            {
                if (velocity.y > 0f && !Input.JumpHeld)
                    multiplier = config.JumpReleaseGravityMultiplier; // 가변 점프: 일찍 떼면 빨리 멈춘다
                else if (Input.JumpHeld && Mathf.Abs(velocity.y) < config.ApexThreshold)
                    multiplier *= config.ApexGravityMultiplier;       // 정점 체공: 최고점에서 잠깐 떠 있는다
            }

            velocity.y = Mathf.Max(velocity.y - config.Gravity * multiplier * Time.fixedDeltaTime, -config.MaxFallSpeed);
            Velocity = velocity;
        }

        public void Jump()
        {
            if (!IsGrounded)
                ReportAssist("Coyote jump");
            else if (Input.JumpPressedTime < landedTime)
                ReportAssist("Buffered jump");

            Input.ConsumeJump();
            Velocity = new Vector2(Velocity.x, config.JumpVelocity);
            IsGrounded = false;
            IsJumping = true;
            lastGroundedTime = float.NegativeInfinity; // 점프로 떠난 직후 코요테 판정으로 한 번 더 뛰지 않게
            Jumped?.Invoke();
        }

        /// <summary>벽 반대쪽 위로 튕겨 나간다.</summary>
        public void WallJump(int wallDirection, string assist = null)
        {
            if (assist != null)
                ReportAssist(assist);

            Input.ConsumeJump();
            Velocity = new Vector2(-wallDirection * config.WallJumpHorizontalSpeed, config.WallJumpVelocity);
            Facing = -wallDirection;
            IsJumping = true;
            controlLockUntil = Time.time + config.WallJumpInputLockTime;
            lastWallTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            WallJumped?.Invoke(wallDirection);
        }

        /// <summary>벽에 붙어 미끄러진다. 빠르게 떨어지다 붙었으면 미끄럼 속도까지 서서히 감속한다.</summary>
        public void SlideOnWall(int wallDirection)
        {
            Vector2 velocity = Velocity;
            float maxSpeed = config.WallSlideSpeed;
            velocity.y = velocity.y < -maxSpeed
                ? Mathf.MoveTowards(velocity.y, -maxSpeed, config.WallSlideDeceleration * Time.fixedDeltaTime)
                : Mathf.Max(velocity.y - config.Gravity * Time.fixedDeltaTime, -maxSpeed);
            velocity.x = wallDirection * WallPushSpeed;
            Velocity = velocity;

            lastWallTime = Time.time;
            LastWallDirection = wallDirection;
        }

        public void OnLanded()
        {
            IsJumping = false;
            landedTime = Time.time;
            Landed?.Invoke(-lastAirVelocityY);
        }

        public void EndJump() => IsJumping = false;

        public void Face(int direction) => Facing = direction;

        /// <summary>direction(±1) 쪽 distance 안에 벽이 있는가. 몸 위쪽 절반만 검사해서 발목 높이 턱은 벽으로 치지 않는다.</summary>
        public bool HasWall(int direction, float distance)
        {
            Vector2 size = ProbeSize;
            size.y *= 0.5f;
            Vector2 center = Body.position + box.offset + new Vector2(0f, size.y * 0.5f);
            int count = Physics2D.BoxCast(center, size, 0f, new Vector2(direction, 0f), solidFilter, wallHits, distance);
            for (int i = 0; i < count; i++)
                if (Mathf.Abs(wallHits[i].normal.x) > 0.9f)
                    return true;
            return false;
        }

        /// <summary>distance 안에 있는 벽 방향. 양쪽 다 있으면 바라보는 쪽.</summary>
        public int FindWall(float distance)
        {
            if (HasWall(Facing, distance))
                return Facing;
            if (HasWall(-Facing, distance))
                return -Facing;
            return 0;
        }

        /// <summary>상승 중 천장 모서리에 머리가 살짝 걸리면 옆으로 밀어서 통과시킨다.</summary>
        public void TryCornerCorrection()
        {
            Vector2 velocity = Velocity;
            if (velocity.y <= 0f)
                return;

            float distance = velocity.y * Time.fixedDeltaTime + ProbeSkin;
            Vector2 position = Body.position;
            if (IsClear(position, Vector2.up, distance))
                return;

            // 적게 미는 쪽부터, 같은 거리면 바라보는 방향부터
            for (int pixels = 1; pixels <= config.CornerCorrectionPixels; pixels++)
            {
                for (int side = Facing, i = 0; i < 2; side = -side, i++)
                {
                    Vector2 shifted = position + new Vector2(side * pixels * PixelGrid.UnitsPerPixel, 0f);
                    if (IsClear(shifted, Vector2.up, distance))
                    {
                        Body.position = shifted;
                        ReportAssist("Corner correction");
                        return;
                    }
                }
            }
        }

        /// <summary>벽 쪽으로 누르며 떨어지는데 발판 윗면이 발보다 살짝 높으면 위로 올려서 착지시킨다.</summary>
        public void TryLedgeCorrection()
        {
            int direction = Input.MoveX;
            Vector2 velocity = Velocity;
            if (direction == 0 || velocity.y > 0f)
                return;

            var forward = new Vector2(direction, 0f);
            float distance = Mathf.Abs(velocity.x) * Time.fixedDeltaTime + ProbeSkin;
            Vector2 position = Body.position;
            if (IsClear(position, forward, distance))
                return;

            for (int pixels = 1; pixels <= config.LedgeCorrectionPixels; pixels++)
            {
                Vector2 lifted = position + new Vector2(0f, pixels * PixelGrid.UnitsPerPixel);
                if (IsClear(lifted, forward, distance))
                {
                    Body.position = lifted;
                    Velocity = new Vector2(velocity.x, 0f);
                    ReportAssist("Ledge correction");
                    return;
                }
            }
        }

        /// <summary>몸 상자를 position 에 두고 direction 으로 distance 만큼 밀었을 때 아무것도 안 닿는가 (시작 위치 겹침 포함).</summary>
        bool IsClear(Vector2 position, Vector2 direction, float distance) =>
            Physics2D.BoxCast(position + box.offset, ProbeSize, 0f, direction, solidFilter, probeHits, distance) == 0;

        bool CheckGround()
        {
            // 점프 직후에는 아직 바닥과 닿아 있으므로, 올라가는 중엔 바닥을 무시한다
            if (Velocity.y > 0.01f)
                return false;
            return Body.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance) > 0;
        }

        void ReportAssist(string assist)
        {
            LastAssist = assist;
            LastAssistTime = Time.time;
        }

        void Respawn()
        {
            Teleport(spawnPosition);
            safePosition = spawnPosition;
            if (CameraController.Instance != null)
                CameraController.Instance.SnapToTarget();
        }

        void Teleport(Vector2 position)
        {
            DetachHook();
            Body.position = position;
            transform.position = position; // 보간 중인 그래픽이 옛 위치에서 끌려오지 않게
            Velocity = Vector2.zero;
            frozenVelocity = Vector2.zero; // 멈춤이 풀릴 때 멈추기 전 속도(넉백 등)가 되살아나지 않게
            ChangeState(AirborneState);
        }
    }
}
