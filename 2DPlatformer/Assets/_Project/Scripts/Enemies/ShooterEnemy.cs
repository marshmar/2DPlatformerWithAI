using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 원거리 적 (터렛·드론 공용). 플레이어가 보이면 일정 간격으로 총알을 쏜다.
    /// 드론은 hoverAmplitude 로 위아래로 떠다니고 patrolDistance 만큼 좌우로 오간다 — 공중의 갈고리 처형 대상.
    /// </summary>
    public sealed class ShooterEnemy : Enemy
    {
        [SerializeField] LayerMask solidMask;
        [SerializeField] EnemyProjectile projectilePrefab;
        [SerializeField] float sightRange = 10f;
        [SerializeField] float fireInterval = 1.6f;
        [SerializeField] Vector2 muzzleOffset = new(0f, 0.9f);
        [Header("드론 비행")]
        [SerializeField] float hoverAmplitude;
        [SerializeField] float patrolDistance;
        [SerializeField] float patrolSpeed = 1.5f;

        Vector3 origin;
        float nextFireTime;
        float patrolTime;

        protected override void Awake()
        {
            base.Awake();
            origin = transform.position;
            nextFireTime = Time.time + Random.Range(0.5f, fireInterval);
        }

        protected override void OnHurt(DamageInfo damage) => nextFireTime = Time.time + fireInterval;

        protected override void OnRespawn() => origin = transform.position;

        void Update()
        {
            if (IsDead)
                return;

            if (!IsExecutionTarget && (hoverAmplitude > 0f || patrolDistance > 0f))
            {
                patrolTime += Time.deltaTime;
                float x = patrolDistance > 0f ? Mathf.Sin(patrolTime * patrolSpeed / Mathf.Max(patrolDistance, 0.1f)) * patrolDistance : 0f;
                float y = Mathf.Sin(patrolTime * 2.2f) * hoverAmplitude;
                transform.position = origin + new Vector3(x, y, 0f);
            }

            if (IsExecutionTarget || IsStunned || !CanSeePlayer(sightRange, solidMask, out Vector2 toPlayer))
                return;

            facing = toPlayer.x >= 0f ? 1 : -1;
            if (Time.time < nextFireTime)
                return;
            nextFireTime = Time.time + fireInterval;
            animator.Play("Attack", restart: true);
            Vector2 muzzle = (Vector2)transform.position + muzzleOffset;
            EnemyProjectile.Fire(projectilePrefab, muzzle, (Player.BodyCenter - muzzle).normalized);
        }
    }
}
