using UnityEngine;

namespace Platformer.UI
{
    /// <summary>
    /// UI 공통 색·글꼴·머티리얼. 색은 게임 안 네온(마젠타·시안·앰버)과 맞춘다.
    /// 글꼴은 OS 글꼴을 동적으로 쓴다 — 한글 글꼴 파일을 프로젝트에 넣지 않아도 되고, 없는 글자는 다음 글꼴로 넘어간다.
    ///   제목: Bahnschrift (DIN 계열 좁은 글꼴, Windows 10+ 기본) → 맑은 고딕
    ///   본문: 맑은 고딕
    ///   숫자·시스템 문구: Consolas → 맑은 고딕
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color Cyan = new(0.2f, 0.92f, 1f);
        public static readonly Color Magenta = new(1f, 0.22f, 0.72f);
        public static readonly Color Amber = new(1f, 0.78f, 0.22f);
        public static readonly Color Red = new(1f, 0.25f, 0.32f);
        public static readonly Color Green = new(0.4f, 1f, 0.6f);
        public static readonly Color Foreground = new(0.9f, 0.94f, 1f);
        public static readonly Color Dim = new(0.55f, 0.62f, 0.78f);
        public static readonly Color Disabled = new(0.32f, 0.34f, 0.42f);
        public static readonly Color Shadow = new(0.02f, 0.02f, 0.06f, 0.75f);

        /// <summary>캔버스 기준 해상도. 1080p 에서 캔버스 1단위 = 화면 1픽셀, 게임 화면 1픽셀 = 3단위</summary>
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        static Font heading;
        static Font body;
        static Font mono;
        static Material textMaterial;

        // OS 글꼴로 만든 Font 는 씬과 상관없이 살아 있지만, 혹시 정리됐으면 다시 만든다 (Unity 오브젝트라 == null 로 확인)
        public static Font Heading
        {
            get
            {
                if (heading == null)
                    heading = CreateFont("Bahnschrift", "Malgun Gothic", "맑은 고딕", "Arial");
                return heading;
            }
        }

        public static Font Body
        {
            get
            {
                if (body == null)
                    body = CreateFont("Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial");
                return body;
            }
        }

        public static Font Mono
        {
            get
            {
                if (mono == null)
                    mono = CreateFont("Consolas", "Malgun Gothic", "맑은 고딕", "Courier New");
                return mono;
            }
        }

        public static Material TextMaterial
        {
            get
            {
                if (textMaterial == null)
                    textMaterial = LoadMaterial("UI/CyberText", "Platformer/UI/CyberText");
                return textMaterial;
            }
        }

        /// <summary>
        /// Resources 의 머티리얼(셋업 메뉴가 만든다 — 빌드에 셰이더가 포함되게 하는 역할도 한다)을 쓰고,
        /// 없으면 셰이더 이름으로 임시 머티리얼을 만든다.
        /// </summary>
        public static Material LoadMaterial(string resourcePath, string shaderName)
        {
            var material = Resources.Load<Material>(resourcePath);
            if (material != null)
                return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[UI] 셰이더 {shaderName} 을 찾지 못했습니다 — Tools > Project Setup > Step 28 을 실행하세요");
                return null;
            }
            return new Material(shader) { name = shaderName + " (Runtime)" };
        }

        static Font CreateFont(params string[] names)
        {
            Font font = null;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(names, 32);
            }
            catch (System.Exception)
            {
                // OS 글꼴을 못 쓰는 환경이면 내장 글꼴로
            }
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>"01:23.45" 형식</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f || float.IsInfinity(seconds))
                return "--:--.--";
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
