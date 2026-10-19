using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 게임 시간 연출. 히트스톱(완전 정지)과 슬로우모션을 요청 단위로 받아 Time.timeScale 하나로 합친다.
    /// 우선순위: 히트스톱 > 슬로우. 모든 계산은 실제 시간(unscaled) 기준 — 느려진 시간으로 재면 영원히 안 끝난다.
    /// 씬에 따로 둘 필요 없이 플레이 시작 시 자동으로 만들어진다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TimeManager : MonoBehaviour
    {
        static TimeManager instance;

        float baseFixedDeltaTime;
        float hitstopUntil = float.NegativeInfinity;
        float slowScale = 1f;
        float slowUntil = float.NegativeInfinity;
        float slowEaseIn = 0.08f;
        float slowEaseOut = 0.35f;
        float slowWeight;

        /// <summary>슬로우 연출 강도 0~1. 셰이더(_FX_SlowAmount)와 후처리가 이 값을 쓴다.</summary>
        public static float SlowAmount => instance != null ? Smooth(instance.slowWeight) : 0f;
        public static bool IsHitstopped => instance != null && Time.unscaledTime < instance.hitstopUntil;
        /// <summary>일시정지 메뉴·스테이지 클리어 화면. 켜져 있는 동안은 히트스톱·슬로우보다 우선해서 시간이 멈춘다.</summary>
        public static bool Paused { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateInstance()
        {
            if (instance != null)
                return;
            var go = new GameObject("[TimeManager]");
            DontDestroyOnLoad(go);
            go.AddComponent<TimeManager>();
        }

        /// <summary>duration(실제 초) 동안 시간을 완전히 멈춘다. 타격감의 핵심 — 맞는 순간 몇 프레임 멈춰야 "때렸다"는 느낌이 난다.</summary>
        public static void Hitstop(float duration)
        {
            if (instance != null)
                instance.hitstopUntil = Mathf.Max(instance.hitstopUntil, Time.unscaledTime + duration);
        }

        /// <summary>시간을 scale 배로 느리게 한다. easeIn 동안 느려지고, duration 뒤 easeOut 동안 돌아온다 (모두 실제 초).</summary>
        public static void SlowMotion(float scale, float duration, float easeIn = 0.08f, float easeOut = 0.35f)
        {
            if (instance == null)
                return;
            bool alreadySlow = Time.unscaledTime < instance.slowUntil;
            instance.slowScale = alreadySlow ? Mathf.Min(instance.slowScale, scale) : scale;
            instance.slowUntil = Mathf.Max(instance.slowUntil, Time.unscaledTime + duration);
            instance.slowEaseIn = easeIn;
            instance.slowEaseOut = easeOut;
        }

        void Awake()
        {
            instance = this;
            baseFixedDeltaTime = Time.fixedDeltaTime;
            Paused = false;
        }

        void OnDestroy()
        {
            if (instance != this)
                return;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
            instance = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            bool slowing = now < slowUntil;
            float easeTime = slowing ? slowEaseIn : slowEaseOut;
            slowWeight = Mathf.MoveTowards(slowWeight, slowing ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(easeTime, 0.001f));
            if (!slowing && slowWeight <= 0f)
                slowScale = 1f;

            float scale = Mathf.Lerp(1f, slowScale, Smooth(slowWeight));
            if (now < hitstopUntil || Paused)
                scale = 0f;

            Time.timeScale = scale;
            // 물리 스텝도 같은 비율로 촘촘하게 — 안 그러면 슬로우 중에 움직임이 50Hz 계단처럼 끊겨 보인다
            if (scale > 0f)
                Time.fixedDeltaTime = baseFixedDeltaTime * scale;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
