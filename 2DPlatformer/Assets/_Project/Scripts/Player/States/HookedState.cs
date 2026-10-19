using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 갈고리에 매달린 상태. 진자 운동을 직접 구현한다 (DistanceJoint2D 는 튜닝할 여지가 적어서):
    ///   1) 중력 + 좌우 입력의 접선 방향 가속으로 속도를 정하고
    ///   2) 로프가 팽팽하면 바깥으로 향하는 속도 성분을 없앤다 — 줄은 당길 수는 있어도 밀 수는 없다
    ///   3) 수치 오차로 줄이 늘어난 만큼은 안쪽으로 되돌린다
    /// 버튼을 떼면 운동량을 유지한 채 놓고, 점프로 놓으면 위로 튀어 오르고, Shift 를 누르면 고정점으로 감아 날아간다.
    /// </summary>
    public sealed class HookedState : PlayerState
    {
        bool reeling;

        public HookedState(PlayerController player) : base(player) { }

        public bool IsReeling => reeling;

        public override void Enter()
        {
            reeling = false;
            Player.EndJump();
        }

        public override void Exit()
        {
            Player.SetAfterimage(false);
        }

        public override void FixedTick()
        {
            GrappleHook hook = Player.Hook;
            HookConfig config = hook.Config;
            if (hook.State != GrappleHook.HookState.Attached)
            {
                Player.ChangeState(Player.AirborneState);
                return;
            }

            if (Player.JumpRequested)
            {
                Player.Input.ConsumeJump();
                hook.Detach();
                Vector2 v = Player.Velocity;
                v.y = Mathf.Max(v.y, config.JumpReleaseVelocity);
                Player.Velocity = v;
                Player.ChangeState(Player.AirborneState);
                return;
            }

            if (!Player.Input.HookHeld && !reeling)
            {
                hook.Detach();
                Player.Velocity *= config.ReleaseBoost;
                Player.ChangeState(Player.AirborneState);
                return;
            }

            if (!reeling && Player.DashRequested)
            {
                Player.Input.ConsumeDash();
                reeling = true;
                Player.SetAfterimage(true);
                PostFX.RadialBlur(0.5f, 0.3f);
            }

            Vector2 anchor = hook.AnchorPoint;
            Vector2 center = Player.BodyCenter;
            Vector2 toAnchor = anchor - center;
            float distance = toAnchor.magnitude;
            Vector2 inward = distance > 0.0001f ? toAnchor / distance : Vector2.up;
            float dt = Time.fixedDeltaTime;

            if (reeling)
            {
                // 고정점에 닿으면 감기를 끝내고 지금 속도 그대로 날아간다 — 줄을 "발사대"처럼 쓰는 산나비식 대시
                if (distance <= config.ReelArriveDistance)
                {
                    hook.Detach();
                    Player.ChangeState(Player.AirborneState);
                    return;
                }
                Player.Velocity = inward * config.ReelSpeed;
                hook.RopeLength = distance;
                Player.Face(inward.x >= 0f ? 1 : -1);
                return;
            }

            Vector2 velocity = Player.Velocity;
            if (Player.IsGrounded)
            {
                MovementConfig move = Player.Config;
                Player.Run(move.GroundAcceleration, move.GroundDeceleration, move.GroundTurnAcceleration);
                velocity = Player.Velocity;
            }
            else
            {
                velocity.y -= Player.Config.Gravity * dt;

                // 좌우 입력은 원의 접선 방향으로 — 오른쪽을 누르면 원을 따라 오른쪽으로 민다
                int input = Player.Input.MoveX;
                if (input != 0)
                {
                    var tangent = new Vector2(inward.y, -inward.x);
                    if (tangent.x < 0f)
                        tangent = -tangent;
                    velocity += tangent * (input * config.SwingAcceleration * dt);
                }
            }

            // 로프 구속: 이번 스텝에 줄 길이를 넘어가면 바깥으로 가는 성분을 없앤다
            Vector2 next = center + velocity * dt;
            if ((next - anchor).magnitude > hook.RopeLength)
            {
                Vector2 outward = -inward;
                float radial = Vector2.Dot(velocity, outward);
                if (radial > 0f)
                    velocity -= outward * radial;

                float stretch = distance - hook.RopeLength;
                if (stretch > 0f)
                    velocity += inward * (stretch / dt * 0.5f);
            }

            velocity = Vector2.ClampMagnitude(velocity, config.MaxSwingSpeed);
            Player.Velocity = velocity;
            if (Mathf.Abs(velocity.x) > 0.5f)
                Player.Face(velocity.x > 0f ? 1 : -1);
        }
    }
}
