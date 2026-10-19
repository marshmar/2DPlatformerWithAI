using UnityEngine;

namespace Platformer
{
    /// <summary>피격: 맞은 반대쪽 위로 튕겨 나가고, 잠깐 조작을 잃고, 일정 시간 무적이 된다.</summary>
    public sealed class HurtState : PlayerState
    {
        float elapsed;

        public HurtState(PlayerController player) : base(player) { }

        public void Begin(DamageInfo damage)
        {
            CombatConfig combat = Player.Combat;
            Player.DetachHook();
            Player.ChangeState(this);

            int away = damage.Direction.x >= 0f ? 1 : -1;
            Player.Velocity = new Vector2(away * combat.HurtKnockback.x, combat.HurtKnockback.y);
            Player.Face(-away);
            Player.Health.SetInvincible(combat.InvincibleTime);

            if (Player.SpriteFX != null)
                Player.SpriteFX.Flash();
            TimeManager.Hitstop(0.05f);
            CameraController.Shake(0.4f);
            PostFX.DamagePulse(1f, 0.45f);
            PostFX.Glitch(0.25f, 0.2f);
        }

        public override void Enter()
        {
            elapsed = 0f;
            Player.EndJump();
        }

        public override void FixedTick()
        {
            elapsed += Time.fixedDeltaTime;
            Player.ApplyGravity();
            if (elapsed >= Player.Combat.HurtStunTime)
                Player.ChangeState(Player.IsGrounded ? Player.GroundedState : Player.AirborneState);
        }
    }
}
