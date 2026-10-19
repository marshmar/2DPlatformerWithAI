namespace Platformer
{
    public sealed class GroundedState : PlayerState
    {
        public GroundedState(PlayerController player) : base(player) { }

        public override void Enter() => Player.OnLanded();

        public override void FixedTick()
        {
            MovementConfig config = Player.Config;
            Player.Run(config.GroundAcceleration, config.GroundDeceleration, config.GroundTurnAcceleration);

            // 착지 전에 미리 눌러 둔 입력도 JumpBufferTime 안이면 여기서 발동한다 (점프 버퍼)
            if (Player.JumpRequested)
            {
                Player.Jump();
                Player.ChangeState(Player.AirborneState);
                return;
            }

            if (Player.AttackRequested)
            {
                Player.AttackState.Begin(0);
                return;
            }

            if (!Player.IsGrounded)
            {
                Player.ChangeState(Player.AirborneState);
                return;
            }

            // 서 있을 때도 중력을 줘서 바닥에 눌려 있게 한다 (모서리를 넘어가는 순간 바로 떨어지도록)
            Player.ApplyGravity();
        }
    }
}
