using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer
{
    /// <summary>
    /// 플레이어 상태와 보정 발동 여부를 화면 왼쪽 위에 보여준다. F1 로 켜고 끈다.
    /// 보정은 잘 작동할수록 플레이어가 눈치채지 못하므로, 튜닝할 때는 여기서 발동 여부를 확인한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] bool visible = true;
        [Tooltip("보정 발동 표시를 유지하는 시간 (초)")]
        [SerializeField] float assistDisplayTime = 1f;

        PlayerController player;

        void Awake() => player = GetComponent<PlayerController>();

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            Vector2 velocity = player.Velocity;
            float sinceAssist = Time.time - player.LastAssistTime;
            string assist = sinceAssist <= assistDisplayTime ? $"<color=#7CFC9A>{player.LastAssist}</color>" : "-";

            GUILayout.BeginArea(new Rect(10f, 10f, 300f, 240f), GUI.skin.box);
            GUILayout.Label("<b>Player</b>  (F1)", RichLabel());
            GUILayout.Label($"State      {player.State.GetType().Name}", RichLabel());
            GUILayout.Label($"Grounded   {player.IsGrounded}", RichLabel());
            GUILayout.Label($"Velocity   ({velocity.x:0.00}, {velocity.y:0.00})", RichLabel());
            GUILayout.Label($"Jumping    {player.IsJumping}  held={player.Input.JumpHeld}", RichLabel());
            GUILayout.Label($"Coyote     {player.CoyoteTimeLeft:0.000}s   Buffer {player.JumpBufferLeft:0.000}s", RichLabel());
            GUILayout.Label($"Wall       coyote {player.WallCoyoteLeft:0.000}s   lock {player.ControlLockLeft:0.000}s", RichLabel());
            GUILayout.Label($"Assist     {assist}", RichLabel());
            if (player.Health != null)
                GUILayout.Label($"HP         {player.Health.Current}/{player.Health.Max}{(player.Health.IsInvincible ? "  (invincible)" : "")}", RichLabel());
            if (player.Hook != null)
            {
                string target = player.Hook.Preview.Valid ? (player.Hook.Preview.Enemy != null ? "enemy" : "terrain") : "-";
                GUILayout.Label($"Hook       {player.Hook.State}  target={target}  rope={player.Hook.RopeLength:0.0}", RichLabel());
            }
            GUILayout.Label($"Time       x{Time.timeScale:0.00}  slow={TimeManager.SlowAmount:0.00}", RichLabel());
            GUILayout.EndArea();
        }

        static GUIStyle richLabel;

        // GUI.skin 은 OnGUI 안에서만 접근할 수 있어서 처음 그릴 때 만든다
        static GUIStyle RichLabel() => richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}
