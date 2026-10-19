using UnityEngine;

namespace Platformer
{
    /// <summary>근접 적: 발판 끝·벽에서 돌아서며 순찰하다가, 플레이어를 보면 쫓아와서 주먹을 휘두른다.</summary>
    public sealed class MeleeEnemy : Enemy
    {
        enum Mode { Patrol, Chase, Attack }

        [SerializeField] LayerMask solidMask;
        [SerializeField] float walkSpeed = 2f;
        [SerializeField] float chaseSpeed = 4.2f;
        [SerializeField] float sightRange = 8f;
        [SerializeField] float attackRange = 1.4f;
        [SerializeField] float attackCooldown = 0.9f;
        [Tooltip("공격 애니메이션(12fps) 중 판정이 켜지는 프레임")]
        [SerializeField] Vector2Int attackActiveFrames = new(3, 5);
        [SerializeField] Vector2 attackHitboxOffset = new(0.9f, 1f);
        [SerializeField] Vector2 attackHitboxSize = new(1.2f, 1.2f);

        readonly Collider2D[] hitBuffer = new Collider2D[4];
        ContactFilter2D playerFilter;
        Mode mode;
        float attackStart;
        float nextAttackTime;
        bool attackLanded;

        protected override void Awake()
        {
            base.Awake();
            playerFilter.useTriggers = true;
            playerFilter.SetLayerMask(LayerMask.GetMask("Player"));
        }

        protected override void OnHurt(DamageInfo damage)
        {
            mode = Mode.Chase;
            animator.Play("Hurt", restart: true);
        }

        protected override void OnRespawn() => mode = Mode.Patrol;

        void FixedUpdate()
        {
            if (IsDead)
                return;
            if (IsExecutionTarget || IsStunned)
            {
                body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, 0f, 30f * Time.fixedDeltaTime), body.linearVelocity.y);
                return;
            }

            bool seesPlayer = CanSeePlayer(sightRange, solidMask, out Vector2 toPlayer);
            switch (mode)
            {
                case Mode.Patrol:
                    if (seesPlayer)
                        mode = Mode.Chase;
                    else if (BlockedAhead())
                        facing = -facing;
                    Move(walkSpeed, "Run");
                    break;

                case Mode.Chase:
                    if (!seesPlayer)
                    {
                        mode = Mode.Patrol;
                        break;
                    }
                    facing = toPlayer.x >= 0f ? 1 : -1;
                    if (Mathf.Abs(toPlayer.x) <= attackRange && Mathf.Abs(toPlayer.y) < 1.5f && Time.time >= nextAttackTime)
                    {
                        mode = Mode.Attack;
                        attackStart = Time.time;
                        attackLanded = false;
                        animator.Play("Attack", restart: true);
                        break;
                    }
                    if (BlockedAhead() || Mathf.Abs(toPlayer.x) <= attackRange * 0.8f)
                        Move(0f, "Idle");
                    else
                        Move(chaseSpeed, "Run");
                    break;

                case Mode.Attack:
                    Move(0f, null);
                    int frame = (int)((Time.time - attackStart) * 12f);
                    if (!attackLanded && frame >= attackActiveFrames.x && frame < attackActiveFrames.y)
                        attackLanded = TryHitPlayer();
                    if (frame >= 6)
                    {
                        mode = Mode.Chase;
                        nextAttackTime = Time.time + attackCooldown;
                    }
                    break;
            }
        }

        void Move(float speed, string clip)
        {
            body.linearVelocity = new Vector2(facing * speed, body.linearVelocity.y);
            if (clip != null && !IsStunned)
                animator.Play(clip);
        }

        bool TryHitPlayer()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(attackHitboxOffset.x * facing, attackHitboxOffset.y);
            int count = Physics2D.OverlapBox(center, attackHitboxSize, 0f, playerFilter, hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var target = hitBuffer[i].GetComponentInParent<Health>();
                if (target == null)
                    continue;
                target.TakeDamage(new DamageInfo(1, new Vector2(facing, 0.5f).normalized, center, 6f));
                return true;
            }
            return false;
        }

        // 앞이 벽이거나 발밑 앞이 낭떠러지면 막힌 것
        bool BlockedAhead()
        {
            Vector2 position = transform.position;
            Vector2 forward = new(facing, 0f);
            bool wall = Physics2D.Raycast(position + new Vector2(0f, 0.5f), forward, 0.6f, solidMask);
            bool ground = Physics2D.Raycast(position + new Vector2(facing * 0.6f, 0.2f), Vector2.down, 0.8f, solidMask);
            return wall || !ground;
        }
    }
}
