using UnityEngine;

namespace Platformer
{
    public sealed class WallSlideState : PlayerState
    {
        float detachTimer;

        public WallSlideState(PlayerController player) : base(player) { }

        public int WallDirection { get; private set; }

        public void Attach(int wallDirection)
        {
            WallDirection = wallDirection;
            Player.ChangeState(this);
        }

        public override void Enter()
        {
            detachTimer = 0f;
            Player.EndJump();
            Player.Face(-WallDirection); // 벽을 등지고 미끄러진다 — 벽 점프로 튕겨 나갈 방향을 바라보게
        }

        public override void FixedTick()
        {
            if (Player.IsGrounded)
            {
                Player.ChangeState(Player.GroundedState);
                return;
            }

            if (Player.JumpRequested)
            {
                Player.WallJump(WallDirection);
                Player.ChangeState(Player.AirborneState);
                return;
            }

            if (!Player.HasWall(WallDirection, Player.WallContactDistance))
            {
                Player.ChangeState(Player.AirborneState);
                return;
            }

            // 벽 쪽을 누르지 않아도 바로 떨어지지 않고 잠깐 버틴다 — 반대 방향 + 점프를 넣을 여유 (벽 붙잡기 유예)
            if (Player.Input.MoveX != WallDirection)
            {
                detachTimer += Time.fixedDeltaTime;
                if (detachTimer >= Player.Config.WallStickTime)
                {
                    Player.ChangeState(Player.AirborneState);
                    return;
                }
            }
            else
            {
                detachTimer = 0f;
            }

            Player.SlideOnWall(WallDirection);
        }
    }
}
