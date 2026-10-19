using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer
{
    /// <summary>
    /// 아레나 봉쇄 게이트. 같은 카메라 방 안의 적이 모두 쓰러질 때까지 길을 막는 에너지 장벽(EnergyGate.shader).
    /// 마지막 적이 쓰러지면 픽셀 단위로 타들어 가며 사라지고 충돌도 꺼진다. 갈고리는 장벽에 걸리지 않는다(갈고리 불가 면).
    /// 어느 적을 기다릴지와 방 영역은 레벨 빌더가 정해 준다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ArenaGate : MonoBehaviour
    {
        static readonly int OpenId = Shader.PropertyToID("_Open");
        static readonly List<ArenaGate> all = new();

        [SerializeField] Enemy[] enemies = System.Array.Empty<Enemy>();
        [SerializeField] SpriteRenderer field;
        [SerializeField] Light2D glow;
        [Tooltip("이 게이트가 지키는 방 (월드 좌표). 플레이어가 이 안에 있으면 HUD 에 남은 적 수를 보여 준다")]
        [SerializeField] Rect room;

        BoxCollider2D blocker;
        MaterialPropertyBlock block;
        bool open;

        public static IReadOnlyList<ArenaGate> All => all;
        public bool IsOpen => open;
        public Rect Room => room;

        public int Remaining
        {
            get
            {
                int count = 0;
                foreach (Enemy enemy in enemies)
                    if (enemy != null && !enemy.IsDead)
                        count++;
                return count;
            }
        }

        /// <summary>레벨 빌더용</summary>
        public void Bind(Enemy[] guarded, SpriteRenderer fieldRenderer, Light2D light, Rect area)
        {
            enemies = guarded;
            field = fieldRenderer;
            glow = light;
            room = area;
        }

        void Awake()
        {
            blocker = GetComponent<BoxCollider2D>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            all.Add(this);
            Enemy.Killed += OnEnemyKilled;
        }

        void OnDisable()
        {
            all.Remove(this);
            Enemy.Killed -= OnEnemyKilled;
        }

        void Start()
        {
            if (Remaining == 0)
                SetOpenAmount(1f, true);
        }

        void OnEnemyKilled(Enemy enemy)
        {
            if (!open && Remaining == 0)
                StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            open = true;
            blocker.enabled = false; // 길은 바로 열고, 사라지는 연출은 그 뒤에
            PostFX.Glitch(0.3f, 0.3f);
            CameraController.Shake(0.2f);
            UI.UISound.Play(UI.UISound.Kind.Open);
            if (GameFlow.Instance != null)
                GameFlow.Instance.Toast("LOCK RELEASED", "봉쇄 해제 — 길이 열렸다", UI.UIStyle.Green);

            const float duration = 0.9f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetOpenAmount(t / duration, false);
                yield return null;
            }
            SetOpenAmount(1f, true);
        }

        void SetOpenAmount(float amount, bool finished)
        {
            open |= finished;
            if (finished)
            {
                blocker.enabled = false;
                if (field != null)
                    field.enabled = false;
                if (glow != null)
                    glow.enabled = false;
                return;
            }
            if (field != null)
            {
                field.GetPropertyBlock(block);
                block.SetFloat(OpenId, amount);
                field.SetPropertyBlock(block);
            }
            if (glow != null)
                glow.intensity = Mathf.Lerp(0.8f, 0f, amount);
        }

        /// <summary>point 가 아직 닫힌 게이트의 방 안에 있으면 그 게이트 (HUD 의 남은 적 표시용)</summary>
        public static ArenaGate FindLocked(Vector2 point)
        {
            foreach (ArenaGate gate in all)
                if (!gate.open && gate.room.Contains(point))
                    return gate;
            return null;
        }
    }
}
