using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 컨트롤러 상태를 보고 재생할 애니메이션을 고른다. Animator 의 전이(Transition) 그래프 대신 코드에서 직접
    /// 상태를 지정하는 방식 — 픽셀 스프라이트는 블렌딩이 없어서 전이선이 하는 일이 없고, 조건이 코드에 모여 있어야 고치기 쉽다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        static readonly int Idle = Animator.StringToHash("Idle");
        static readonly int Run = Animator.StringToHash("Run");
        static readonly int Jump = Animator.StringToHash("Jump");
        static readonly int Fall = Animator.StringToHash("Fall");
        static readonly int WallSlide = Animator.StringToHash("WallSlide");
        static readonly int Hurt = Animator.StringToHash("Hurt");
        static readonly int Spin = Animator.StringToHash("Spin");
        static readonly int[] Attacks = { Animator.StringToHash("Attack1"), Animator.StringToHash("Attack2"), Animator.StringToHash("Attack3") };

        [Tooltip("이 속도보다 빠르면 달리기 애니메이션")]
        [SerializeField] float runThreshold = 0.5f;

        PlayerController player;
        Animator animator;
        int current;
        int lastSwingId;

        void Awake()
        {
            player = GetComponentInParent<PlayerController>();
            animator = GetComponent<Animator>();
        }

        void LateUpdate()
        {
            // 멈춰 있는 동안은 포즈도 그대로 — 속도가 0 이라 Idle 로 바뀌었다 돌아오는 깜빡임 방지
            animator.speed = player.IsFrozen ? 0f : 1f;
            if (player.IsFrozen)
                return;

            // flipX 대신 스케일로 뒤집는다 — 노말맵 조명과 자식 오브젝트(이펙트, 판정)도 같이 뒤집히게
            transform.localScale = new Vector3(player.Facing, 1f, 1f);

            Vector2 velocity = player.Velocity;
            PlayerState state = player.State;
            int next;
            bool restart = false;
            if (state is AttackState attack)
            {
                next = Attacks[attack.ComboIndex];
                restart = attack.SwingId != lastSwingId; // 같은 타가 연속돼도 처음부터
                lastSwingId = attack.SwingId;
            }
            else if (state is HurtState)
                next = Hurt;
            else if (state is ExecutionState || (state is HookedState hooked && hooked.IsReeling))
                next = Spin;
            else if (state is WallSlideState)
                next = WallSlide;
            else if (player.IsGrounded)
                next = Mathf.Abs(velocity.x) > runThreshold ? Run : Idle;
            else
                next = velocity.y > 0f ? Jump : Fall;

            if (next != current || restart)
            {
                animator.Play(next, 0, 0f);
                current = next;
            }
        }
    }
}
