using UnityEngine;

namespace Platformer
{
    /// <summary>적 총알. 매 프레임 이동 경로를 원형으로 훑어서 플레이어(체력) 또는 지형에 닿으면 터진다.</summary>
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] float speed = 9f;
        [SerializeField] float radius = 0.2f;
        [SerializeField] float lifetime = 3f;
        [SerializeField] LayerMask hitMask;
        [SerializeField] GameObject impactPrefab;

        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        ContactFilter2D filter;
        Vector2 direction;
        float age;

        public static void Fire(EnemyProjectile prefab, Vector2 position, Vector2 direction)
        {
            var projectile = FXPool.Spawn(prefab.gameObject, position).GetComponent<EnemyProjectile>();
            projectile.direction = direction;
            projectile.age = 0f;
            projectile.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        }

        void Awake()
        {
            filter.useTriggers = true;
            filter.SetLayerMask(hitMask);
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            age += Time.deltaTime;
            int count = Physics2D.CircleCast(transform.position, radius, direction, filter, hits, step);
            for (int i = 0; i < count; i++)
            {
                var health = hits[i].collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    if (health.GetComponent<Enemy>() != null)
                        continue; // 아군은 통과
                    health.TakeDamage(new DamageInfo(1, direction, hits[i].point, 5f));
                }
                Explode(hits[i].point);
                return;
            }

            transform.position += (Vector3)(direction * step);
            if (age >= lifetime)
                FXPool.Release(gameObject);
        }

        void Explode(Vector2 point)
        {
            if (impactPrefab != null)
                FXPool.Spawn(impactPrefab, point);
            FXPool.Release(gameObject);
        }
    }
}
