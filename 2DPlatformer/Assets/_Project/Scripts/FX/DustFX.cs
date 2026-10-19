using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 플레이어 움직임에 맞춰 먼지·불꽃 파티클을 뿜는다. 파티클 시스템은 "어디서 몇 개"만 받고(Emit),
    /// 모양·소멸은 Dust.shader 가 수명(AgePercent)을 보고 처리한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class DustFX : MonoBehaviour
    {
        [SerializeField] ParticleSystem dustPrefab;
        [SerializeField] ParticleSystem sparkPrefab;
        [Tooltip("이 속도 이상으로 떨어져 착지하면 먼지가 최대")]
        [SerializeField] float heavyLandingSpeed = 20f;
        [SerializeField] float wallSlideInterval = 0.06f;

        PlayerController player;
        ParticleSystem dust;
        ParticleSystem sparks;
        float wallTimer;
        float skidTimer;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            if (dustPrefab != null)
                dust = Instantiate(dustPrefab);
            if (sparkPrefab != null)
                sparks = Instantiate(sparkPrefab);
        }

        void OnEnable()
        {
            player.Landed += OnLanded;
            player.Jumped += OnJumped;
            player.WallJumped += OnWallJumped;
            var hook = GetComponent<GrappleHook>();
            if (hook != null)
                hook.Attached += OnHookAttached;
        }

        void OnDisable()
        {
            player.Landed -= OnLanded;
            player.Jumped -= OnJumped;
            player.WallJumped -= OnWallJumped;
            var hook = GetComponent<GrappleHook>();
            if (hook != null)
                hook.Attached -= OnHookAttached;
        }

        void OnLanded(float speed)
        {
            float weight = Mathf.Clamp01(speed / heavyLandingSpeed);
            if (weight > 0.15f)
                Emit(dust, player.Body.position, Mathf.RoundToInt(Mathf.Lerp(3f, 14f, weight)));
        }

        void OnJumped() => Emit(dust, player.Body.position, 4);

        void OnWallJumped(int wallDirection) =>
            Emit(dust, player.Body.position + new Vector2(wallDirection * 0.4f, 1f), 6);

        void OnHookAttached(Vector2 point) => Emit(sparks, point, 10);

        void Update()
        {
            float dt = Time.deltaTime;

            // 벽 슬라이드: 벽에 닿은 손 쪽에서 계속 조금씩
            if (player.State is WallSlideState wallSlide)
            {
                wallTimer -= dt;
                if (wallTimer <= 0f)
                {
                    wallTimer = wallSlideInterval;
                    Emit(dust, player.Body.position + new Vector2(wallSlide.WallDirection * 0.4f, 1.6f), 1);
                }
            }

            // 방향 전환 브레이크: 빠르게 달리다 반대로 누르면 발밑에서 끌리는 먼지
            Vector2 velocity = player.Velocity;
            int input = player.Input.MoveX;
            if (player.IsGrounded && Mathf.Abs(velocity.x) > 5f && input != 0 && input != (int)Mathf.Sign(velocity.x))
            {
                skidTimer -= dt;
                if (skidTimer <= 0f)
                {
                    skidTimer = 0.05f;
                    Emit(dust, player.Body.position, 2);
                }
            }
        }

        static void Emit(ParticleSystem system, Vector2 position, int count)
        {
            if (system == null || count <= 0)
                return;
            var parameters = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            system.Emit(parameters, count);
        }
    }
}
