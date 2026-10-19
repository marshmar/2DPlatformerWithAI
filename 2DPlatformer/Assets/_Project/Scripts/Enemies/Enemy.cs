using System.Collections;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 적 공통: 피격 반응(플래시·찌그러짐·넉백·불꽃), 사망 연출(글리치 + 디졸브), 테스트용 재생성, 갈고리 하이라이트.
    /// 행동(순찰·추적·사격)은 파생 클래스가 정한다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public abstract class Enemy : MonoBehaviour
    {
        static readonly Color HookOutline = new(3.5f, 0.5f, 0.7f, 1f);

        [SerializeField] protected Transform visual;
        [SerializeField] protected SpriteAnimator animator;
        [SerializeField] protected SpriteFX spriteFX;
        [Tooltip("갈고리가 걸리는 지점 (발밑 기준)")]
        [SerializeField] Vector2 hookPointOffset = new(0f, 1f);
        [Tooltip("죽은 뒤 다시 나타나기까지 (테스트 맵용, 0 이면 다시 안 나타남)")]
        [SerializeField] float respawnDelay = 4f;
        [Tooltip("죽을 때 남기는 이펙트 (기계 적의 폭발 등)")]
        [SerializeField] GameObject deathEffect;

        protected Health health;
        protected Rigidbody2D body;
        protected int facing = 1;
        Collider2D[] colliders;
        Vector3 spawnPosition;
        float squash;

        public bool IsDead => health.IsDead;
        public SpriteFX SpriteFX => spriteFX;
        public Vector2 HookPoint => (Vector2)transform.position + hookPointOffset;
        /// <summary>처형으로 잡힌 동안은 행동을 멈춘다</summary>
        public bool IsExecutionTarget { get; set; }
        protected bool IsStunned => Time.time < stunnedUntil;
        protected PlayerController Player => player != null ? player : player = FindAnyObjectByType<PlayerController>();

        float stunnedUntil;
        PlayerController player;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<Rigidbody2D>();
            colliders = GetComponentsInChildren<Collider2D>();
            spawnPosition = transform.position;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        public void SetHookHighlight(bool on)
        {
            if (!IsExecutionTarget)
                spriteFX.SetOutline(on ? 1f : 0f, HookOutline);
        }

        void OnDamaged(DamageInfo damage)
        {
            spriteFX.Flash();
            squash = 1f;
            stunnedUntil = Time.time + 0.35f;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
                body.linearVelocity = new Vector2(damage.Direction.x * damage.Knockback, 3f);
            OnHurt(damage);
        }

        /// <summary>어느 적이든 쓰러진 순간. 처치 수 집계·아레나 게이트가 듣는다.</summary>
        public static event System.Action<Enemy> Killed;

        void OnDied(DamageInfo damage)
        {
            Killed?.Invoke(this);
            StartCoroutine(DieRoutine(damage));
        }

        // 사망: 글리치로 지지직거리다가 픽셀 단위로 타들어 가며 사라진다
        IEnumerator DieRoutine(DamageInfo damage)
        {
            foreach (var c in colliders)
                c.enabled = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            spriteFX.SetOutline(0f);
            spriteFX.Glitch(damage.IsExecution ? 1f : 0.6f, 0.6f);
            if (deathEffect != null)
                FXPool.Spawn(deathEffect, HookPoint);
            animator.Play("Death", restart: true);

            const float dissolveTime = 0.6f;
            for (float t = 0f; t < dissolveTime; t += Time.deltaTime)
            {
                spriteFX.SetDissolve(t / dissolveTime);
                yield return null;
            }
            visual.gameObject.SetActive(false);

            if (respawnDelay <= 0f)
                yield break;
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
        }

        void Respawn()
        {
            transform.position = spawnPosition;
            health.Restore();
            visual.gameObject.SetActive(true);
            spriteFX.SetDissolve(0f);
            spriteFX.Glitch(1f, 0.5f); // 홀로그램처럼 지지직거리며 나타난다
            foreach (var c in colliders)
                c.enabled = true;
            if (body != null)
                body.simulated = true;
            animator.Play("Idle", restart: true);
            OnRespawn();
        }

        protected virtual void LateUpdate()
        {
            // 맞으면 납작해졌다가 0.12초에 걸쳐 돌아온다
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime / 0.12f);
            visual.localScale = new Vector3(facing * (1f + 0.25f * squash), 1f - 0.2f * squash, 1f);
        }

        protected abstract void OnHurt(DamageInfo damage);
        protected virtual void OnRespawn() { }

        /// <summary>플레이어가 range 안에 있고 사이에 벽이 없는가</summary>
        protected bool CanSeePlayer(float range, LayerMask solidMask, out Vector2 toPlayer)
        {
            toPlayer = default;
            if (Player == null || Player.Health == null || Player.Health.IsDead)
                return false;
            Vector2 eye = HookPoint;
            toPlayer = Player.BodyCenter - eye;
            if (toPlayer.magnitude > range)
                return false;
            return !Physics2D.Raycast(eye, toPlayer.normalized, toPlayer.magnitude, solidMask);
        }
    }
}
