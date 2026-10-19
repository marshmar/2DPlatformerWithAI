using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer
{
    /// <summary>
    /// 입력 장치를 게임 동작 단위로 읽는다. 버튼을 누른 순간은 Update 에서만 정확히 잡히므로
    /// 시각을 기록해 두고, 물리(FixedUpdate)에서는 그 시각을 보고 판단한다 (= 입력 버퍼의 기반).
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [Tooltip("스틱을 이 정도 이상 기울여야 좌우 입력으로 친다")]
        [SerializeField, Range(0f, 1f)] float moveThreshold = 0.3f;
        [Tooltip("오른쪽 스틱을 이 정도 이상 기울이면 마우스 대신 스틱으로 조준한다")]
        [SerializeField, Range(0f, 1f)] float aimStickThreshold = 0.35f;

        InputActionMap map;
        InputAction move;
        InputAction jump;
        InputAction hook;
        InputAction attack;
        InputAction dash;
        InputAction aimPointer;
        InputAction aimStick;
        Vector2 lastStickAim = Vector2.right;
        bool usingStick;

        /// <summary>좌우 입력을 -1, 0, 1 로 디지털화. 스틱을 살짝 기울였을 때 느리게 걷지 않도록.</summary>
        public int MoveX { get; private set; }
        public float MoveY { get; private set; }
        public bool JumpHeld => jump.IsPressed() || SimulatedJumpHeld;
        /// <summary>검증·AI 용: 점프 버튼을 누르고 있는 것처럼 취급한다 (가변 점프 높이).</summary>
        public bool SimulatedJumpHeld { get; set; }
        public bool HookHeld => hook.IsPressed() || SimulatedHookHeld;
        /// <summary>검증·AI 용: 갈고리 버튼을 누르고 있는 것처럼 취급한다.</summary>
        public bool SimulatedHookHeld { get; set; }
        /// <summary>검증·AI 용: 0 이 아니면 좌우 입력을 이 값(-1/1)으로 덮어쓴다.</summary>
        public int SimulatedMoveX { get; set; }
        public float JumpPressedTime { get; private set; } = float.NegativeInfinity;
        public float HookPressedTime { get; private set; } = float.NegativeInfinity;
        public float AttackPressedTime { get; private set; } = float.NegativeInfinity;
        public float DashPressedTime { get; private set; } = float.NegativeInfinity;

        void Awake()
        {
            map = actions.FindActionMap("Player", throwIfNotFound: true);
            move = map.FindAction("Move", throwIfNotFound: true);
            jump = map.FindAction("Jump", throwIfNotFound: true);
            hook = map.FindAction("Hook", throwIfNotFound: true);
            attack = map.FindAction("Attack", throwIfNotFound: true);
            dash = map.FindAction("Dash", throwIfNotFound: true);
            aimPointer = map.FindAction("AimPointer", throwIfNotFound: true);
            aimStick = map.FindAction("AimStick", throwIfNotFound: true);
        }

        bool inputEnabled = true;

        /// <summary>
        /// 일시정지·클리어 화면 동안 게임 입력을 끈다. 꺼져 있을 때 누른 버튼은 기록되지 않으므로
        /// 메뉴에서 누른 점프·공격이 돌아온 순간 버퍼에서 튀어나오지 않는다.
        /// </summary>
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                if (!isActiveAndEnabled)
                    return;
                if (value)
                    map.Enable();
                else
                    map.Disable();
            }
        }

        void OnEnable()
        {
            if (inputEnabled)
                map.Enable();
        }

        void OnDisable() => map.Disable();

        void Update()
        {
            Vector2 moveValue = move.ReadValue<Vector2>();
            MoveX = Mathf.Abs(moveValue.x) < moveThreshold ? 0 : (int)Mathf.Sign(moveValue.x);
            if (SimulatedMoveX != 0)
                MoveX = SimulatedMoveX;
            MoveY = moveValue.y;

            if (jump.WasPressedThisFrame())
                JumpPressedTime = Time.time;
            if (hook.WasPressedThisFrame())
                HookPressedTime = Time.time;
            if (attack.WasPressedThisFrame())
                AttackPressedTime = Time.time;
            if (dash.WasPressedThisFrame())
                DashPressedTime = Time.time;

            // 마지막으로 만진 장치로 조준한다: 스틱을 기울이면 스틱, 마우스를 움직이면 마우스
            Vector2 stick = aimStick.ReadValue<Vector2>();
            if (stick.magnitude > aimStickThreshold)
            {
                lastStickAim = stick.normalized;
                usingStick = true;
            }
            else if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f)
            {
                usingStick = false;
            }
        }

        /// <summary>origin(월드)에서 조준하는 방향. 마우스면 커서 쪽, 스틱이면 스틱 방향.</summary>
        public Vector2 AimDirection(Vector2 origin)
        {
            if (usingStick || Camera.main == null)
                return lastStickAim;
            Vector2 cursor = Camera.main.ScreenToWorldPoint(aimPointer.ReadValue<Vector2>());
            Vector2 direction = cursor - origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }

        /// <summary>조준점 표시용: 마우스면 커서 위치, 스틱이면 origin 에서 distance 떨어진 곳.</summary>
        public Vector2 AimPoint(Vector2 origin, float distance)
        {
            if (usingStick || Camera.main == null)
                return origin + lastStickAim * distance;
            return Camera.main.ScreenToWorldPoint(aimPointer.ReadValue<Vector2>());
        }

        /// <summary>입력을 한 번 쓰고 나면 지워서 같은 입력으로 두 번 반응하지 않게 한다.</summary>
        public void ConsumeJump() => JumpPressedTime = float.NegativeInfinity;
        public void ConsumeHook() => HookPressedTime = float.NegativeInfinity;
        public void ConsumeAttack() => AttackPressedTime = float.NegativeInfinity;
        public void ConsumeDash() => DashPressedTime = float.NegativeInfinity;

        /// <summary>검증·AI 용: 버튼을 누른 것처럼 기록한다.</summary>
        public void SimulatePress(string action)
        {
            switch (action)
            {
                case "Jump": JumpPressedTime = Time.time; break;
                case "Hook": HookPressedTime = Time.time; break;
                case "Attack": AttackPressedTime = Time.time; break;
                case "Dash": DashPressedTime = Time.time; break;
            }
        }
    }
}
