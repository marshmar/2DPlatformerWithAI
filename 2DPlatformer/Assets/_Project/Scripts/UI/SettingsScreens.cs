using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>설정 화면과 조작법 화면. 타이틀과 일시정지 메뉴가 같은 것을 만들어 쓴다.</summary>
    public static class SettingsScreens
    {
        static readonly (string Action, string Keys)[] Controls =
        {
            ("이동", "A / D            ·  왼쪽 스틱"),
            ("점프 · 벽 점프", "SPACE            ·  A"),
            ("갈고리 발사 (누르고 있으면 매달림)", "마우스 왼쪽      ·  RT"),
            ("조준", "마우스 커서      ·  오른쪽 스틱"),
            ("감기 대시 (매달린 상태에서)", "SHIFT            ·  RB"),
            ("공격 (3연타)", "마우스 오른쪽    ·  X"),
            ("처형 — 적에게 갈고리를 걸면 날아가 일격", "갈고리 → 적"),
            ("일시정지", "ESC              ·  START"),
        };

        /// <summary>설정 화면. onBack 은 Esc·돌아가기 버튼에서 부른다.</summary>
        public static UIScreen BuildSettings(Transform parent, Action onBack, Action onControls)
        {
            UIScreen screen = Frame(parent, "Settings", "SETTINGS", "시스템 설정", new Vector2(980f, 860f), out RectTransform body);
            RectTransform column = UIFactory.Column(body, "Rows", 10f);
            column.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(860f, 640f));

            var rowSize = new Vector2(860f, 60f);
            string Percent(int v) => $"{v * 10}%";
            string OnOff(int v) => v == 0 ? "켜짐" : "꺼짐";
            var items = new List<Selectable>
            {
                UIFactory.GaugeRow(column, "마스터 볼륨", 10, () => Mathf.RoundToInt(GameSettings.MasterVolume * 10f), v => GameSettings.SetMasterVolume(v / 10f), Percent, rowSize),
                UIFactory.ChoiceRow(column, "화면 모드", 2, () => GameSettings.Fullscreen ? 0 : 1, v => GameSettings.SetFullscreen(v == 0), v => v == 0 ? "전체 화면" : "창 모드", rowSize),
                UIFactory.ChoiceRow(column, "해상도", GameSettings.Resolutions.Count, () => GameSettings.ResolutionIndex, GameSettings.SetResolution,
                    v => $"{GameSettings.Resolutions[v].x} × {GameSettings.Resolutions[v].y}", rowSize),
                UIFactory.ChoiceRow(column, "수직 동기화", 2, () => GameSettings.VSync ? 0 : 1, v => GameSettings.SetVSync(v == 0), OnOff, rowSize),
                UIFactory.GaugeRow(column, "화면 흔들림", 10, () => Mathf.RoundToInt(GameSettings.ScreenShake * 10f), v => GameSettings.SetScreenShake(v / 10f), Percent, rowSize),
                UIFactory.GaugeRow(column, "화면 효과 (글리치·번쩍임)", 10, () => Mathf.RoundToInt(GameSettings.ScreenEffects * 10f), v => GameSettings.SetScreenEffects(v / 10f), Percent, rowSize),
                UIFactory.ChoiceRow(column, "타이머 표시", 2, () => GameSettings.ShowTimer ? 0 : 1, v => GameSettings.SetShowTimer(v == 0), OnOff, rowSize),
            };
            Spacer(column, 14f);
            var buttonSize = new Vector2(860f, 62f);
            items.Add(UIFactory.Button(column, "조작법", "CONTROLS", UIStyle.Amber, buttonSize, onControls));
            items.Add(UIFactory.Button(column, "돌아가기", "BACK", UIStyle.Magenta, buttonSize, onBack));
            UIFactory.LinkVertical(items);

            Footer(screen.transform, "[↑↓] 이동    [←→] 값 변경    [ESC] 뒤로", -460f);
            screen.FirstSelected = items[0];
            screen.Back = onBack;
            return screen;
        }

        public static UIScreen BuildControls(Transform parent, Action onBack)
        {
            UIScreen screen = Frame(parent, "Controls", "CONTROLS", "조작법", new Vector2(1180f, 780f), out RectTransform body);
            for (int i = 0; i < Controls.Length; i++)
            {
                float y = -160f - i * 62f;
                CyberGraphic stripe = UIFactory.Shape(body, "Stripe", CyberGraphic.Shape.Line, i % 2 == 0 ? UIStyle.Cyan * 0.6f : UIStyle.Magenta * 0.5f, 0f, 0f);
                stripe.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y - 26f), new Vector2(1060f, 3f));
                stripe.Padding = 0f;
                Text action = UIFactory.Label(body, "Action", Controls[i].Action, UIStyle.Body, 27, UIStyle.Foreground);
                action.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, y), new Vector2(560f, 50f));
                Text keys = UIFactory.Label(body, "Keys", Controls[i].Keys, UIStyle.Mono, 25, UIStyle.Amber);
                keys.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(660f, y), new Vector2(480f, 50f));
            }
            CyberButton back = UIFactory.Button(body, "돌아가기", "BACK", UIStyle.Magenta, new Vector2(520f, 62f), onBack);
            back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(520f, 62f));
            back.navigation = new Navigation { mode = Navigation.Mode.None };
            Footer(screen.transform, "[ESC] 뒤로", -420f);
            screen.FirstSelected = back;
            screen.Back = onBack;
            return screen;
        }

        /// <summary>가운데 패널 + 제목 + 구분선. 화면(UIScreen)은 전체를 덮는 투명 노드이고 패널은 그 안에 있다.</summary>
        public static UIScreen Frame(Transform parent, string name, string heading, string subheading, Vector2 size, out RectTransform body)
        {
            RectTransform root = UIFactory.Node(name, parent).Stretch();
            var screen = root.gameObject.AddComponent<UIScreen>();

            CyberGraphic panel = UIFactory.Shape(root, "Panel", CyberGraphic.Shape.Panel, new Color(UIStyle.Cyan.r, UIStyle.Cyan.g, UIStyle.Cyan.b, 0.95f), 28f, 3f);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            panel.raycastTarget = true; // 패널 뒤의 메뉴가 눌리지 않게
            body = panel.rectTransform;

            Text system = UIFactory.Label(body, "System", $"// SYS.{heading}", UIStyle.Mono, 18, UIStyle.Cyan * 0.9f);
            system.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -26f), new Vector2(400f, 24f));
            Text title = UIFactory.Label(body, "Heading", heading, UIStyle.Heading, 60, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -48f), new Vector2(600f, 70f));
            UIFactory.Glitch(title, 2.5f, 0.05f, 4f);
            Text sub = UIFactory.Label(body, "Subheading", subheading, UIStyle.Body, 26, UIStyle.Magenta, TextAnchor.UpperRight);
            sub.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -70f), new Vector2(400f, 40f));
            CyberGraphic line = UIFactory.Shape(body, "Divider", CyberGraphic.Shape.Line, UIStyle.Cyan, 0f, 0f);
            line.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -128f), new Vector2(size.x - 100f, 4f));
            line.Padding = 0f;
            return screen;
        }

        public static void Footer(Transform parent, string text, float y)
        {
            Text footer = UIFactory.Label(parent, "Footer", text, UIStyle.Mono, 20, UIStyle.Dim, TextAnchor.MiddleCenter);
            footer.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1200f, 30f));
        }

        static void Spacer(Transform parent, float height)
        {
            RectTransform spacer = UIFactory.Node("Spacer", parent);
            spacer.sizeDelta = new Vector2(10f, height);
            spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }
    }
}
