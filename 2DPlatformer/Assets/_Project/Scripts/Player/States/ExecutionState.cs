using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 갈고리 처형 (산나비의 핵심 손맛). 지금까지 만든 연출을 순서대로 엮는다:
    ///   잡는 순간  : 짧은 슬로우 + 줌 펀치 + 약한 글리치 + 적 아웃라인 → "잡았다"는 정지 컷
    ///   돌진       : 체인을 따라 직선으로 날아간다 + 잔상 + 방사형 블러
    ///   도착(타격) : 처형 데미지 + 큰 칼 이펙트 + 히트스톱 + 쇼크웨이브 + 글리치 + 쉐이크 → 적은 디졸브
    ///   이탈       : 위로 튕겨 올라 바로 다음 동작으로 이어간다
    /// </summary>
    public sealed class ExecutionState : PlayerState
    {
        const float ArriveDistance = 1.1f;
        const float MaxDuration = 0.8f; // 장애물에 막혀 못 닿으면 포기

        Enemy target;
        float elapsed;

        public ExecutionState(PlayerController player) : base(player) { }

        public Enemy Target => target;

        public void Begin(Enemy enemy)
        {
            target = enemy;
            enemy.IsExecutionTarget = true;
            Player.ChangeState(this);

            CombatConfig combat = Player.Combat;
            TimeManager.SlowMotion(combat.ExecutionSlowScale, 0.12f, 0.02f, 0.15f);
            PostFX.ZoomPunch(enemy.HookPoint, 0.18f, 0.55f);
            PostFX.Glitch(0.25f, 0.25f);
            enemy.SpriteFX.SetOutline(1f, new Color(4f, 0.6f, 0.8f, 1f));
        }

        public override void Enter()
        {
            elapsed = 0f;
            Player.EndJump();
            Player.SetAfterimage(true);
        }

        public override void Exit()
        {
            Player.SetAfterimage(false);
            if (target != null)
            {
                target.IsExecutionTarget = false;
                target.SpriteFX.SetOutline(0f);
            }
            target = null;
        }

        public override void FixedTick()
        {
            elapsed += Time.fixedDeltaTime;
            if (target == null || target.IsDead || elapsed > MaxDuration)
            {
                Player.Hook.Detach();
                Player.ChangeState(Player.AirborneState);
                return;
            }

            Vector2 toTarget = target.HookPoint - Player.BodyCenter;
            if (toTarget.magnitude <= ArriveDistance)
            {
                Execute(toTarget);
                return;
            }

            Player.Velocity = toTarget.normalized * Player.Combat.ExecutionDashSpeed;
            Player.Face(toTarget.x >= 0f ? 1 : -1);
            PostFX.RadialBlur(0.35f, 0.1f);
        }

        void Execute(Vector2 toTarget)
        {
            CombatConfig combat = Player.Combat;
            Vector2 point = target.HookPoint;
            int facing = toTarget.x >= 0f ? 1 : -1;

            var damage = new DamageInfo(999, toTarget.normalized, point, combat.Knockback * 1.5f, isExecution: true);
            target.GetComponent<Health>().TakeDamage(damage);

            TimeManager.Hitstop(0.12f);
            CameraController.Shake(0.65f);
            PostFX.Shockwave(point, 1.2f, 0.6f);
            PostFX.Glitch(0.6f, 0.35f);
            SlashFX.Spawn(point, facing, 3);
            HitSparkFX.Spawn(point, 2f);

            Player.Hook.Detach();
            Player.Velocity = new Vector2(-facing * 3f, combat.ExecutionBounce);
            Player.ChangeState(Player.AirborneState);
        }
    }
}
