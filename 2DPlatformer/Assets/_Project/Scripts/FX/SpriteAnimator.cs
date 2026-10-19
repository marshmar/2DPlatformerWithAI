using System;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 스프라이트 배열을 순서대로 넘기는 플립북. 적·네온 간판·이펙트처럼 상태가 단순한 것에 쓴다.
    /// (플레이어는 상태가 많아서 Animator 를 쓴다)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [Serializable]
        public sealed class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 10f;
            public bool loop = true;
        }

        [SerializeField] Clip[] clips = Array.Empty<Clip>();
        [Tooltip("히트스톱·슬로우의 영향을 받지 않는다 (네온 간판처럼 배경 연출용)")]
        [SerializeField] bool unscaledTime;
        [Tooltip("시작 프레임을 무작위로 — 같은 간판 여러 개가 똑같이 깜빡이지 않게")]
        [SerializeField] bool randomStartTime;

        SpriteRenderer spriteRenderer;
        // 같은 오브젝트의 다른 컴포넌트(TimedFX)가 이 Awake 보다 먼저 Restart 를 부를 수 있어서 필요할 때 가져온다
        SpriteRenderer Renderer => spriteRenderer != null ? spriteRenderer : spriteRenderer = GetComponent<SpriteRenderer>();
        Clip current;
        float time;

        public string CurrentClip => current?.name;
        public bool IsFinished => current != null && !current.loop && time >= current.frames.Length / current.fps;
        public float NormalizedTime => current == null ? 0f : Mathf.Clamp01(time * current.fps / current.frames.Length);

        /// <summary>에디터 셋업 도구용</summary>
        public void SetClips(Clip[] newClips, bool useUnscaledTime = false, bool randomStart = false)
        {
            clips = newClips;
            unscaledTime = useUnscaledTime;
            randomStartTime = randomStart;
        }

        void Awake()
        {
            if (clips.Length > 0)
                Play(clips[0].name);
            if (randomStartTime && current != null)
                time = UnityEngine.Random.value * current.frames.Length / current.fps;
        }

        /// <summary>첫 번째 클립을 처음부터 (풀에서 다시 꺼낸 일회성 이펙트용)</summary>
        public void Restart()
        {
            if (clips.Length > 0)
                Play(clips[0].name, restart: true);
        }

        public void Play(string clipName, bool restart = false)
        {
            if (!restart && current != null && current.name == clipName)
                return;
            current = Array.Find(clips, c => c.name == clipName);
            time = 0f;
            ApplyFrame();
        }

        void Update()
        {
            if (current == null || current.frames.Length == 0)
                return;
            time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            ApplyFrame();
        }

        void ApplyFrame()
        {
            if (current == null || current.frames.Length == 0)
                return;
            int frame = (int)(time * current.fps);
            frame = current.loop ? frame % current.frames.Length : Mathf.Min(frame, current.frames.Length - 1);
            Renderer.sprite = current.frames[frame];
        }
    }
}
