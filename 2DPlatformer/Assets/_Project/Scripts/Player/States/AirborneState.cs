namespace Platformer
{
    public sealed class AirborneState : PlayerState
    {
        public AirborneState(PlayerController player) : base(player) { }

        public override void FixedTick()
        {
            if (Player.IsGrounded)
            {
                Player.ChangeState(Player.GroundedState);
                return;
            }

            if (Player.JumpRequested)
                TryAirJump();

            if (Player.AttackRequested)
            {
                Player.AttackState.Begin(0);
                return;
            }

            // 떨어지는 중에 벽 쪽을 누르고 있으면 벽에 붙는다. 벽 점프 직후 입력 잠금 중에는 붙지 않는다
            int input = Player.Input.MoveX;
            if (input != 0 && Player.Velocity.y <= 0f && !Player.IsControlLocked &&
                Player.HasWall(input, Player.WallContactDistance))
            {
                Player.WallSlideState.Attach(input);
                return;
            }

            MovementConfig config = Player.Config;
            Player.Run(config.AirAcceleration, config.AirDeceleration, config.AirTurnAcceleration);
            Player.ApplyGravity();

            // 이번 스텝 속도가 정해진 뒤, 그 속도로 움직였을 때 걸리는지를 보고 보정한다
            Player.TryCornerCorrection();
            Player.TryLedgeCorrection();
        }

        void TryAirJump()
        {
            // 발판에서 떨어진 직후 CoyoteTime 안이면 공중에서도 점프 (코요테 타임)
            if (Player.CanCoyoteJump)
            {
                Player.Jump();
                return;
            }

            // 벽에 붙어 있지 않아도 몇 픽셀 안에 벽이 있으면 벽 점프
            int wall = Player.FindWall(Player.WallJumpGraceDistance);
            if (wall != 0)
            {
                Player.WallJump(wall, "Wall jump (near)");
                return;
            }

            // 벽에서 떨어진 직후 WallCoyoteTime 안이면 벽 점프 (벽 코요테)
            if (Player.CanWallCoyoteJump)
                Player.WallJump(Player.LastWallDirection, "Wall coyote");
        }
    }
}
