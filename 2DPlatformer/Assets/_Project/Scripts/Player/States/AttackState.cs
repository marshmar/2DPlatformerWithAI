using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 3타 콤보. 보정:
    ///   - 입력 버퍼: 다음 타를 미리 눌러 두면 지금 타가 끝나는 즉시 이어진다
    ///   - 타깃 스냅: 공격 시작 때 앞쪽 가까운 적에게 살짝 끌려간다 (헛치기 방지)
    ///   - 공중 체공: 공중에서 때리는 동안 낙하 속도를 묶는다
    ///   - 캔슬: 판정이 지나간 뒤(또는 맞혔으면 바로) 점프·갈고리로 끊을 수 있다
    /// </summary>
    public sealed class AttackState : PlayerState
    {
        static readonly int[] FrameCounts = { 6, 8, 8 }; // Cyborg_attack1~3 시트의 프레임 수

        readonly HashSet<Health> hitThisSwing = new();
        readonly Collider2D[] hitBuffer = new Collider2D[8];
        ContactFilter2D enemyFilter;
        float elapsed;
        bool connected;
        bool queuedNext;
        Enemy snapTarget;

        public AttackState(PlayerController player) : base(player)
        {
            enemyFilter.useTriggers = true;
            enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
        }

        public int ComboIndex { get; private set; }
        /// <summary>애니메이션을 처음부터 다시 틀기 위한 공격 번호 (같은 타가 연속돼도 구분)</summary>
        public int SwingId { get; private set; }

        int Frame => (int)(elapsed * Player.Combat.AttackFps);
        Vector2Int Active => Player.Combat.ActiveFrames[ComboIndex];
        public bool CanCancel => connected || Frame >= Active.y;

        public void Begin(int comboIndex)
        {
            ComboIndex = comboIndex;
            SwingId++;
            if (Player.State == this)
                Enter();
            else
                Player.ChangeState(this);
        }

        public override void Enter()
        {
            elapsed = 0f;
            connected = false;
            queuedNext = false;
            hitThisSwing.Clear();
            Player.Input.ConsumeAttack();

            // 조준 방향 쪽으로 돌아서 때린다 (마우스 조준이면 커서 쪽)
            Vector2 aim = Player.Input.AimDirection(Player.BodyCenter);
            if (Mathf.Abs(aim.x) > 0.2f)
                Player.Face(aim.x > 0f ? 1 : -1);

            snapTarget = FindSnapTarget();
            SlashFX.Spawn(Player.BodyCenter + new Vector2(Player.Facing * 0.7f, 0.1f), Player.Facing, ComboIndex);
        }

        public override void FixedTick()
        {
            CombatConfig combat = Player.Combat;
            float dt = Time.fixedDeltaTime;
            elapsed += dt;

            // 움직임: 처음 0.1초는 타깃 쪽으로 미끄러지고, 그 뒤엔 서서히 멈춘다
            Vector2 velocity = Player.Velocity;
            if (snapTarget != null && elapsed < 0.1f)
            {
                float toTarget = snapTarget.transform.position.x - Player.Body.position.x;
                velocity.x = Mathf.Abs(toTarget) > 0.9f ? Mathf.Sign(toTarget) * combat.SnapSpeed : 0f;
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, Player.Config.GroundDeceleration * 0.5f * dt);
            }

            if (!Player.IsGrounded)
            {
                Player.Velocity = velocity;
                Player.ApplyGravity();
                velocity = Player.Velocity;
                velocity.y = Mathf.Max(velocity.y, -combat.AirAttackMaxFall);
            }
            Player.Velocity = velocity;

            int frame = Frame;
            if (frame >= Active.x && frame < Active.y)
                HitCheck();

            if (Player.AttackRequested && frame >= Active.x)
                queuedNext = true;

            if (CanCancel && Player.JumpRequested && (Player.IsGrounded || Player.CanCoyoteJump))
            {
                Player.Jump();
                Player.ChangeState(Player.AirborneState);
                return;
            }

            if (elapsed >= FrameCounts[ComboIndex] / combat.AttackFps)
            {
                if (queuedNext && ComboIndex < FrameCounts.Length - 1)
                    Begin(ComboIndex + 1);
                else
                    Player.ChangeState(Player.IsGrounded ? Player.GroundedState : Player.AirborneState);
            }
        }

        void HitCheck()
        {
            CombatConfig combat = Player.Combat;
            Vector2 offset = new(combat.HitboxOffset.x * Player.Facing, combat.HitboxOffset.y);
            Vector2 center = Player.Body.position + offset;
            int count = Physics2D.OverlapBox(center, combat.HitboxSize, 0f, enemyFilter, hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var health = hitBuffer[i].GetComponentInParent<Health>();
                if (health == null || !hitThisSwing.Add(health))
                    continue;

                bool finisher = ComboIndex == FrameCounts.Length - 1;
                Vector2 hitPoint = hitBuffer[i].ClosestPoint(center);
                // 1·2타는 살짝만 밀어야 다음 타가 닿는다. 마무리만 크게 날린다
                var damage = new DamageInfo(combat.Damage[ComboIndex], new Vector2(Player.Facing, 0.3f).normalized, hitPoint, combat.Knockback * (finisher ? 1.6f : 0.6f));
                if (!health.TakeDamage(damage))
                    continue;

                connected = true;
                TimeManager.Hitstop(finisher ? combat.HitstopFinisher : combat.HitstopNormal);
                CameraController.Shake(finisher ? combat.ShakeFinisher : combat.ShakeNormal);
                HitSparkFX.Spawn(hitPoint, finisher ? 1.4f : 1f);
            }
        }

        Enemy FindSnapTarget()
        {
            CombatConfig combat = Player.Combat;
            int count = Physics2D.OverlapCircle(Player.BodyCenter, combat.SnapRange, enemyFilter, hitBuffer);
            Enemy best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var enemy = hitBuffer[i].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead)
                    continue;
                float dx = enemy.transform.position.x - Player.Body.position.x;
                if (Mathf.Sign(dx) != Player.Facing || Mathf.Abs(enemy.transform.position.y - Player.Body.position.y) > 2f)
                    continue;
                if (Mathf.Abs(dx) < bestDistance)
                {
                    bestDistance = Mathf.Abs(dx);
                    best = enemy;
                }
            }
            return best;
        }
    }
}
