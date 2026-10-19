using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 잔상. 켜져 있는 동안 일정 간격으로 지금 스프라이트 프레임·위치·방향을 복사한 "유령"을 남기고, 유령은 단색으로 빛나다 사라진다.
    /// 색은 시안/마젠타를 번갈아 써서 디지털 잔상 느낌을 낸다. 시간은 게임 시간 — 히트스톱 중엔 잔상도 멈춘다.
    /// </summary>
    public sealed class AfterimageEmitter : MonoBehaviour
    {
        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        sealed class Ghost
        {
            public SpriteRenderer Renderer;
            public float Age;
            public Color Color;
        }

        [SerializeField] SpriteRenderer source;
        [SerializeField] Material material;
        [SerializeField] float interval = 0.035f;
        [SerializeField] float lifetime = 0.28f;
        [ColorUsage(false, true)]
        [SerializeField] Color[] colors = { new(0.3f, 2.4f, 3f, 1f), new(3f, 0.5f, 2.6f, 1f) };

        readonly Queue<Ghost> pool = new();
        readonly List<Ghost> live = new();
        MaterialPropertyBlock block;
        Transform root;
        bool active;
        float timer;
        int colorIndex;

        public bool IsActive => active;

        void Awake() => block = new MaterialPropertyBlock();

        public void SetActive(bool on)
        {
            if (on && !active)
                timer = 0f;
            active = on;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (active)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    Spawn();
                    timer = interval;
                }
            }

            for (int i = live.Count - 1; i >= 0; i--)
            {
                Ghost ghost = live[i];
                ghost.Age += dt;
                float fade = 1f - ghost.Age / lifetime;
                if (fade <= 0f)
                {
                    ghost.Renderer.enabled = false;
                    live.RemoveAt(i);
                    pool.Enqueue(ghost);
                    continue;
                }
                ghost.Renderer.GetPropertyBlock(block);
                block.SetColor(GhostColorId, ghost.Color);
                block.SetFloat(FadeId, fade * fade);
                ghost.Renderer.SetPropertyBlock(block);
            }
        }

        void Spawn()
        {
            if (source == null || source.sprite == null)
                return;
            Ghost ghost = pool.Count > 0 ? pool.Dequeue() : Create();
            Transform t = ghost.Renderer.transform;
            t.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            t.localScale = source.transform.lossyScale;
            ghost.Renderer.sprite = source.sprite;
            ghost.Renderer.sortingLayerID = source.sortingLayerID;
            ghost.Renderer.sortingOrder = source.sortingOrder - 1;
            ghost.Renderer.enabled = true;
            ghost.Age = 0f;
            ghost.Color = colors[colorIndex++ % colors.Length];
            live.Add(ghost);
        }

        Ghost Create()
        {
            if (root == null)
                root = new GameObject("[Afterimages]").transform;
            var go = new GameObject("Ghost");
            go.transform.SetParent(root, false);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sharedMaterial = material;
            return new Ghost { Renderer = spriteRenderer };
        }
    }
}
