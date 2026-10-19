using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 플레이어 설정. PlayerPrefs 에 저장하고, 게임이 시작될 때 자동으로 불러와 적용한다.
    /// 게임 코드는 값만 읽는다 — 화면 흔들림은 CameraController.Shake, 화면 효과 강도는 PostFX / FXPostFeature 가 곱해서 쓴다.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "settings.";
        static readonly Vector2Int[] FallbackResolutions = { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440) };

        public static float MasterVolume { get; private set; } = 0.8f;
        /// <summary>화면 흔들림 배율 0~1</summary>
        public static float ScreenShake { get; private set; } = 1f;
        /// <summary>글리치·색수차·번쩍임 같은 화면 효과 배율 0~1</summary>
        public static float ScreenEffects { get; private set; } = 1f;
        public static bool ShowTimer { get; private set; } = true;
        public static bool Fullscreen { get; private set; } = true;
        public static bool VSync { get; private set; } = true;
        public static int ResolutionIndex { get; private set; }
        public static IReadOnlyList<Vector2Int> Resolutions => resolutions;

        static Vector2Int[] resolutions = FallbackResolutions;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            // 모니터가 지원하는 해상도 (주사율만 다른 중복 제거, 720p 이상)
            Vector2Int[] available = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Where(r => r.x >= 1280 && r.y >= 720)
                .Distinct()
                .OrderBy(r => r.x * r.y)
                .ToArray();
            resolutions = available.Length > 0 ? available : FallbackResolutions;

            MasterVolume = PlayerPrefs.GetFloat(Prefix + "volume", 0.8f);
            ScreenShake = PlayerPrefs.GetFloat(Prefix + "shake", 1f);
            ScreenEffects = PlayerPrefs.GetFloat(Prefix + "effects", 1f);
            ShowTimer = PlayerPrefs.GetInt(Prefix + "timer", 1) == 1;
            Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;

            int saved = PlayerPrefs.GetInt(Prefix + "resolution", -1);
            ResolutionIndex = saved >= 0 && saved < resolutions.Length ? saved : NearestResolution(new Vector2Int(Screen.width, Screen.height));

            AudioListener.volume = MasterVolume;
            ApplyDisplay();
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            Save();
        }

        public static void SetScreenShake(float value)
        {
            ScreenShake = Mathf.Clamp01(value);
            Save();
        }

        public static void SetScreenEffects(float value)
        {
            ScreenEffects = Mathf.Clamp01(value);
            Save();
        }

        public static void SetShowTimer(bool value)
        {
            ShowTimer = value;
            Save();
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            ApplyDisplay();
            Save();
        }

        public static void SetVSync(bool value)
        {
            VSync = value;
            ApplyDisplay();
            Save();
        }

        public static void SetResolution(int index)
        {
            ResolutionIndex = Mathf.Clamp(index, 0, resolutions.Length - 1);
            ApplyDisplay();
            Save();
        }

        static void ApplyDisplay()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 144; // 수직 동기화를 끄면 무제한 대신 144 로 묶는다
#if !UNITY_EDITOR
            // 에디터의 Game 뷰는 창 크기를 바꿀 수 없으므로 빌드에서만 적용한다
            Vector2Int size = resolutions[Mathf.Clamp(ResolutionIndex, 0, resolutions.Length - 1)];
            Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }

        static int NearestResolution(Vector2Int size)
        {
            int best = resolutions.Length - 1;
            int bestDifference = int.MaxValue;
            for (int i = 0; i < resolutions.Length; i++)
            {
                int difference = Mathf.Abs(resolutions[i].x - size.x) + Mathf.Abs(resolutions[i].y - size.y);
                if (difference < bestDifference)
                {
                    best = i;
                    bestDifference = difference;
                }
            }
            return best;
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "shake", ScreenShake);
            PlayerPrefs.SetFloat(Prefix + "effects", ScreenEffects);
            PlayerPrefs.SetInt(Prefix + "timer", ShowTimer ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "resolution", ResolutionIndex);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
