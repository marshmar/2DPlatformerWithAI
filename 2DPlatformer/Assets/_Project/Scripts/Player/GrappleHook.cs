using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Platformer
{
    /// <summary>
    /// 갈고리 팔. 매 프레임 "지금 쏘면 어디에 걸릴지"를 미리 계산해서(조준 보정) 하이라이트·조준점에 보여 주고,
    /// 발사하면 갈고리 끝이 실제로 날아가 닿은 뒤에야 걸린다. 걸리면 플레이어를 스윙(지형) 또는 처형(적) 상태로 보낸다.
    /// 걸 수 있는 지형은 SurfaceTile.Hookable 또는 Hookable 레이어로 판단한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class GrappleHook : MonoBehaviour
    {
        public enum HookState { Idle, Flying, Attached, Retracting }

        public struct Target
        {
            public bool Valid;      // 걸 수 있는 대상인가
            public Vector2 Point;
            public Enemy Enemy;
        }

        [SerializeField] HookConfig config;
        [SerializeField] LayerMask solidMask;
        [SerializeField] LayerMask enemyMask;
        [Tooltip("몸(발밑) 기준 팔 위치. x 는 바라보는 방향으로 뒤집힌다")]
        [SerializeField] Vector2 handOffset = new(0.3f, 1.25f);

        readonly RaycastHit2D[] rayHits = new RaycastHit2D[4];
        readonly Collider2D[] enemyHits = new Collider2D[16];
        PlayerController player;
        ContactFilter2D solidFilter;
        ContactFilter2D enemyFilter;
        Target preview;
        Target flying;
        Vector2 anchor;
        Enemy attachedEnemy;
        Vector2 enemyAnchorOffset;
        Enemy highlighted;
        int hookableLayer;

        public HookConfig Config => config;
        public HookState State { get; private set; }
        public Vector2 TipPosition { get; private set; }
        public float RopeLength { get; set; }
        public Target Preview => preview;
        public Enemy AttachedEnemy => attachedEnemy;
        public Vector2 HandPosition => (Vector2)transform.position + new Vector2(handOffset.x * player.Facing, handOffset.y);
        public Vector2 AnchorPoint => attachedEnemy != null ? (Vector2)attachedEnemy.transform.position + enemyAnchorOffset : anchor;

        /// <summary>갈고리가 어딘가에 걸린 순간 (먼지·불꽃 연출용)</summary>
        public event Action<Vector2> Attached;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            solidFilter.useTriggers = false;
            solidFilter.SetLayerMask(solidMask);
            enemyFilter.useTriggers = true;
            enemyFilter.SetLayerMask(enemyMask);
            hookableLayer = LayerMask.NameToLayer("Hookable");
        }

        void Update()
        {
            Vector2 origin = player.BodyCenter;
            Vector2 aim = player.Input.AimDirection(origin);
            preview = player.CanUseHook && State == HookState.Idle ? FindTarget(origin, aim) : default;
            UpdateHighlight();

            if (State == HookState.Idle && player.CanUseHook && Time.time - player.Input.HookPressedTime <= config.InputBuffer)
                Fire(origin, aim);

            float dt = Time.deltaTime;
            switch (State)
            {
                case HookState.Flying:
                    Vector2 destination = flying.Enemy != null ? flying.Enemy.HookPoint : flying.Point;
                    TipPosition = Vector2.MoveTowards(TipPosition, destination, config.TipSpeed * dt);
                    if ((TipPosition - destination).sqrMagnitude < 0.0001f)
                        Arrive(destination);
                    break;

                case HookState.Attached:
                    if (attachedEnemy != null && attachedEnemy.IsDead)
                        Detach();
                    else
                        TipPosition = AnchorPoint;
                    break;

                case HookState.Retracting:
                    TipPosition = Vector2.MoveTowards(TipPosition, HandPosition, config.RetractSpeed * dt);
                    if ((TipPosition - HandPosition).sqrMagnitude < 0.01f)
                        State = HookState.Idle;
                    break;
            }
        }

        /// <summary>검증·AI 용: 지정한 방향으로 즉시 발사한다.</summary>
        public void FireTowards(Vector2 direction) => Fire(player.BodyCenter, direction.normalized);

        void Fire(Vector2 origin, Vector2 aim)
        {
            player.Input.ConsumeHook();
            Target target = FindTarget(origin, aim);
            if (!target.Valid)
            {
                // 걸 곳이 없으면 조준 방향으로 날아갔다가 돌아온다 (헛손질도 보여야 한다)
                int count = Physics2D.Raycast(origin, aim, solidFilter, rayHits, config.Range);
                target.Point = count > 0 ? rayHits[0].point : origin + aim * config.Range;
            }

            flying = target;
            TipPosition = HandPosition;
            State = HookState.Flying;
        }

        void Arrive(Vector2 point)
        {
            if (!flying.Valid)
            {
                State = HookState.Retracting;
                return;
            }

            State = HookState.Attached;
            Attached?.Invoke(point);
            CameraController.Shake(0.12f);

            if (flying.Enemy != null && !flying.Enemy.IsDead)
            {
                attachedEnemy = flying.Enemy;
                enemyAnchorOffset = point - (Vector2)attachedEnemy.transform.position;
                player.StartExecution(attachedEnemy);
            }
            else
            {
                anchor = point;
                RopeLength = Mathf.Max(config.MinRopeLength, Vector2.Distance(player.BodyCenter, anchor));
                player.StartSwing();
            }
        }

        public void Detach()
        {
            if (State == HookState.Idle)
                return;
            attachedEnemy = null;
            State = HookState.Retracting;
        }

        /// <summary>
        /// 조준 보정: 조준 방향 ±AssistAngle 부채꼴 안에서 걸 수 있는 지점을 찾는다.
        /// 점수 = 각도 차이 + 거리(조금). 적은 더 넓은 각도로 찾고 점수에 우선권을 준다 — 처형으로 이어지게.
        /// </summary>
        Target FindTarget(Vector2 origin, Vector2 aim)
        {
            Target best = default;
            float bestScore = float.MaxValue;

            float enemyAngle = config.AssistAngle + config.EnemyAssistBonusAngle;
            int enemyCount = Physics2D.OverlapCircle(origin, config.Range, enemyFilter, enemyHits);
            for (int i = 0; i < enemyCount; i++)
            {
                var enemy = enemyHits[i].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead)
                    continue;
                Vector2 toEnemy = enemy.HookPoint - origin;
                float angle = Vector2.Angle(aim, toEnemy);
                if (angle > enemyAngle)
                    continue;
                // 사이에 벽이 있으면 못 건다
                if (Physics2D.Raycast(origin, toEnemy.normalized, solidFilter, rayHits, toEnemy.magnitude) > 0)
                    continue;
                float score = angle / enemyAngle + toEnemy.magnitude / config.Range * 0.3f - 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new Target { Valid = true, Point = enemy.HookPoint, Enemy = enemy };
                }
            }

            for (float angle = 0f; angle <= config.AssistAngle + 0.01f; angle += config.AssistStep)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (angle == 0f && sign < 0)
                        continue;
                    Vector2 direction = Rotate(aim, angle * sign);
                    if (Physics2D.Raycast(origin, direction, solidFilter, rayHits, config.Range) == 0)
                        continue;
                    RaycastHit2D hit = rayHits[0];
                    if (!IsHookable(hit))
                        continue;
                    float score = angle / config.AssistAngle + hit.distance / config.Range * 0.3f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new Target { Valid = true, Point = hit.point, Enemy = null };
                    }
                }
            }
            return best;
        }

        // 맞은 지점에서 표면 안쪽으로 살짝 들어간 칸의 타일을 본다 (경계 위의 점은 어느 칸인지 애매하므로)
        bool IsHookable(RaycastHit2D hit)
        {
            if (hit.collider.gameObject.layer == hookableLayer)
                return true;
            var tilemap = hit.collider.GetComponent<Tilemap>();
            if (tilemap == null)
                return false;
            Vector3Int cell = tilemap.WorldToCell(hit.point - hit.normal * 0.05f);
            var tile = tilemap.GetTile<SurfaceTile>(cell);
            return tile != null && tile.Hookable;
        }

        void UpdateHighlight()
        {
            if (preview.Valid && preview.Enemy == null)
                FXGlobals.SetHookTarget(preview.Point);
            else
                FXGlobals.ClearHookTarget();

            Enemy next = preview.Valid ? preview.Enemy : null;
            if (next == highlighted)
                return;
            if (highlighted != null)
                highlighted.SetHookHighlight(false);
            if (next != null)
                next.SetHookHighlight(true);
            highlighted = next;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
