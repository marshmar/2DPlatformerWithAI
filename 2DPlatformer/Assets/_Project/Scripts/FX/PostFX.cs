using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 화면 전체 연출 API. 게임 코드는 이것만 부르고, 실제 효과는 FXPost.shader 가 그린다.
    /// 각 효과는 "시작 시각 + 길이 + 세기"만 기억하고, 매 프레임 지난 시간으로 세기를 계산한다 — 따로 Update 가 필요 없다.
    /// 시간은 실제 시간 기준이라 히트스톱(timeScale 0) 중에도 효과가 진행된다.
    /// </summary>
    public static class PostFX
    {
        static readonly int GlitchId = Shader.PropertyToID("_FX_Glitch");
        static readonly int ZoomId = Shader.PropertyToID("_FX_Zoom");
        static readonly int ShockwaveId = Shader.PropertyToID("_FX_Shockwave");
        static readonly int DamageId = Shader.PropertyToID("_FX_Damage");
        static readonly int RadialBlurId = Shader.PropertyToID("_FX_RadialBlur");

        struct Pulse
        {
            public float Start;
            public float Duration;
            public float Strength;
            public Vector2 World;

            public float Progress(float now) => Mathf.Clamp01((now - Start) / Mathf.Max(Duration, 0.0001f));
            public bool IsActive(float now) => Strength > 0f && now - Start < Duration;
        }

        static Pulse glitch;
        static Pulse zoom;
        static Pulse shockwave;
        static Pulse damage;
        static Pulse radialBlur;

        /// <summary>이번 프레임에 켜진 효과가 하나라도 있는가 (없으면 후처리 패스를 건너뛴다)</summary>
        public static bool IsActive { get; private set; }

        /// <summary>화면이 블록 단위로 찢어지고 색이 갈라진다. 해킹·피격·전환 연출.</summary>
        public static void Glitch(float strength, float duration) => Start(ref glitch, strength, duration, Vector2.zero);

        /// <summary>worldFocus 를 중심으로 순간 확대했다가 돌아온다. amount 0.1 = 10% 확대.</summary>
        public static void ZoomPunch(Vector2 worldFocus, float amount, float duration) => Start(ref zoom, amount, duration, worldFocus);

        /// <summary>worldCenter 에서 퍼져 나가는 굴절 링.</summary>
        public static void Shockwave(Vector2 worldCenter, float strength = 1f, float duration = 0.6f) => Start(ref shockwave, strength, duration, worldCenter);

        /// <summary>화면 가장자리가 붉게 맥동한다. 플레이어 피격.</summary>
        public static void DamagePulse(float strength = 1f, float duration = 0.45f) => Start(ref damage, strength, duration, Vector2.zero);

        /// <summary>화면 중앙(또는 마지막 줌 초점)으로 빨려 들어가는 방사형 블러. 대시·처형.</summary>
        public static void RadialBlur(float strength, float duration) => Start(ref radialBlur, strength, duration, Vector2.zero);

        static void Start(ref Pulse pulse, float strength, float duration, Vector2 world)
        {
            float now = Time.unscaledTime;
            // 이미 더 강한 효과가 진행 중이면 덮어쓰지 않는다
            if (pulse.IsActive(now) && CurrentStrength(pulse, now) > strength)
                return;
            pulse = new Pulse { Start = now, Duration = duration, Strength = strength, World = world };
        }

        static float CurrentStrength(Pulse pulse, float now)
        {
            float remaining = 1f - pulse.Progress(now);
            return pulse.Strength * remaining * remaining;
        }

        /// <summary>FXGlobals 가 렌더링 직전에 호출한다.</summary>
        internal static void Upload(Camera camera)
        {
            float now = Time.unscaledTime;

            float glitchValue = glitch.IsActive(now) ? CurrentStrength(glitch, now) : 0f;

            // 줌 펀치: 처음 10% 동안 확 들어갔다가 나머지 동안 천천히 빠진다
            float zoomValue = 0f;
            Vector2 focus = new(0.5f, 0.5f);
            if (zoom.IsActive(now) && camera != null)
            {
                float t = zoom.Progress(now);
                float attack = Mathf.Clamp01(t / 0.1f);
                float release = 1f - Mathf.Clamp01((t - 0.1f) / 0.9f);
                zoomValue = zoom.Strength * attack * release * release;
                focus = camera.WorldToViewportPoint(zoom.World);
            }

            // 쇼크웨이브: 반지름은 빠르게 퍼지다 느려지고(ease-out), 세기는 점점 약해진다. 반지름 단위는 화면 높이
            Vector4 shock = Vector4.zero;
            if (shockwave.IsActive(now) && camera != null)
            {
                float t = shockwave.Progress(now);
                Vector2 center = camera.WorldToViewportPoint(shockwave.World);
                shock = new Vector4(center.x, center.y, (1f - (1f - t) * (1f - t)) * 0.6f, shockwave.Strength * (1f - t));
            }

            float damageValue = damage.IsActive(now) ? CurrentStrength(damage, now) : 0f;
            float radialValue = (radialBlur.IsActive(now) ? CurrentStrength(radialBlur, now) : 0f) + TimeManager.SlowAmount * 0.15f;

            // 설정의 "화면 효과 강도" — 글리치·번쩍임에 민감한 사람을 위해 전부 한 비율로 줄인다
            float intensity = GameSettings.ScreenEffects;
            glitchValue *= intensity;
            zoomValue *= intensity;
            shock.w *= intensity;
            damageValue *= intensity;
            radialValue *= intensity;

            Shader.SetGlobalFloat(GlitchId, glitchValue);
            Shader.SetGlobalVector(ZoomId, new Vector4(focus.x, focus.y, zoomValue, 0f));
            Shader.SetGlobalVector(ShockwaveId, shock);
            Shader.SetGlobalFloat(DamageId, damageValue);
            Shader.SetGlobalFloat(RadialBlurId, radialValue);

            IsActive = glitchValue > 0f || zoomValue > 0f || shock.w > 0f || damageValue > 0f || radialValue > 0f;
        }
    }
}
