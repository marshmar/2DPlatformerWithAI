using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// UI 를 코드로 조립하는 도우미. 메뉴는 런타임에 만들어진다 — 프리팹 없이도 모든 씬에서 같은 모양이 나오고,
    /// 색·도형은 머티리얼(Resources/UI)과 UIStyle 에서 한 번에 바꿀 수 있다.
    /// 좌표는 1920x1080 기준 캔버스 단위.
    /// </summary>
    public static class UIFactory
    {
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            // CyberGraphic 은 uv1~3, TextGlitch 는 uv1 에 정보를 싣는다. 켜지 않으면 셰이더에 0 이 들어간다
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                                               AdditionalCanvasShaderChannels.TexCoord2 |
                                               AdditionalCanvasShaderChannels.TexCoord3;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>anchor 한 점에 붙이고 pivot 기준 position, size 로 둔다.</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>부모를 가득 채운다 (가장자리 여백 지정 가능).</summary>
        public static RectTransform Stretch(this RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static CyberGraphic Shape(Transform parent, string name, CyberGraphic.Shape shape, Color color, float chamfer = 12f, float border = 3f)
        {
            RectTransform node = Node(name, parent);
            var graphic = node.gameObject.AddComponent<CyberGraphic>();
            graphic.ShapeType = shape;
            graphic.color = color;
            graphic.Chamfer = chamfer;
            graphic.Border = border;
            graphic.raycastTarget = false;
            return graphic;
        }

        public static Text Label(Transform parent, string name, string content, Font font, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            RectTransform node = Node(name, parent);
            var text = node.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.material = UIStyle.TextMaterial;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static TextGlitch Glitch(Text text, float split = 2f, float intensity = 0f, float burstInterval = 0f)
        {
            var glitch = text.gameObject.AddComponent<TextGlitch>();
            glitch.Configure(split, intensity, burstInterval);
            return glitch;
        }

        public static TextDecoder Decoder(Text text) => text.gameObject.AddComponent<TextDecoder>();

        /// <summary>메뉴 버튼: [▶ 제목 ........ 캡션]. 레이아웃 그룹 안에서는 LayoutElement 크기를 따른다.</summary>
        public static CyberButton Button(Transform parent, string title, string caption, Color accent, Vector2 size, Action onClick)
        {
            RectTransform root = Node("Button " + title, parent);
            root.sizeDelta = size;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            CyberGraphic background = Shape(root, "Background", CyberGraphic.Shape.Button, accent, 14f, 3f);
            background.rectTransform.Stretch();
            background.raycastTarget = true; // 클릭 판정은 배경이 받는다

            RectTransform content = Node("Content", root).Stretch();
            Text caret = Label(content, "Caret", "▶", UIStyle.Mono, Mathf.RoundToInt(size.y * 0.34f), accent, TextAnchor.MiddleLeft);
            caret.rectTransform.Stretch(left: 18f);
            Text label = Label(content, "Label", title, UIStyle.Body, Mathf.RoundToInt(size.y * 0.42f), UIStyle.Foreground, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.rectTransform.Stretch(left: 50f, right: 20f);
            Text captionText = Label(content, "Caption", caption, UIStyle.Mono, Mathf.RoundToInt(size.y * 0.26f), UIStyle.Dim, TextAnchor.MiddleRight);
            captionText.rectTransform.Stretch(left: 50f, right: 24f);

            var button = root.gameObject.AddComponent<CyberButton>();
            button.Bind(background, label, captionText, caret, content, accent);
            if (onClick != null)
                button.Clicked += onClick;
            return button;
        }

        /// <summary>칸 게이지 설정 줄 (0 ~ steps 단계).</summary>
        public static SettingRow GaugeRow(Transform parent, string title, int steps, Func<int> get, Action<int> set, Func<int, string> format, Vector2 size)
        {
            SettingRow row = RowBase(parent, title, size, out CyberGraphic background, out Text label);
            CyberGraphic gauge = Shape(row.transform, "Gauge", CyberGraphic.Shape.Segments, UIStyle.Cyan, 0f, 0f);
            gauge.Skew = 0.6f;
            gauge.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(320f, size.y * 0.36f));
            gauge.raycastTarget = true;
            Text value = Label(row.transform, "Value", "", UIStyle.Mono, Mathf.RoundToInt(size.y * 0.4f), UIStyle.Dim, TextAnchor.MiddleRight);
            value.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-380f, 0f), new Vector2(140f, size.y));
            row.Bind(background, label, value, gauge, steps + 1, get, set, format, false);
            return row;
        }

        /// <summary>선택형 설정 줄: ◀ 값 ▶</summary>
        public static SettingRow ChoiceRow(Transform parent, string title, int count, Func<int> get, Action<int> set, Func<int, string> format, Vector2 size)
        {
            SettingRow row = RowBase(parent, title, size, out CyberGraphic background, out Text label);
            Text value = Label(row.transform, "Value", "", UIStyle.Mono, Mathf.RoundToInt(size.y * 0.42f), UIStyle.Dim, TextAnchor.MiddleCenter);
            value.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(360f, size.y));
            value.raycastTarget = true;
            row.Bind(background, label, value, null, count, get, set, format, true);
            return row;
        }

        static SettingRow RowBase(Transform parent, string title, Vector2 size, out CyberGraphic background, out Text label)
        {
            RectTransform root = Node("Row " + title, parent);
            root.sizeDelta = size;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            background = Shape(root, "Background", CyberGraphic.Shape.Button, UIStyle.Cyan, 10f, 2f);
            background.rectTransform.Stretch();
            background.raycastTarget = true;
            background.Padding = 8f;
            label = Label(root, "Label", title, UIStyle.Body, Mathf.RoundToInt(size.y * 0.44f), UIStyle.Foreground);
            label.rectTransform.Stretch(left: 30f, right: 400f);
            return root.gameObject.AddComponent<SettingRow>();
        }

        /// <summary>세로 목록 컨테이너 (위에서 아래로 쌓는다).</summary>
        public static RectTransform Column(Transform parent, string name, float spacing)
        {
            RectTransform column = Node(name, parent);
            var group = column.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return column;
        }

        /// <summary>위아래 이동 순서를 명시적으로 잇는다 (끝에서 반대편으로 돌아간다). 상호작용 불가 항목은 건너뛴다.</summary>
        public static void LinkVertical(IList<Selectable> items)
        {
            var active = new List<Selectable>();
            foreach (Selectable item in items)
                if (item != null && item.IsInteractable())
                    active.Add(item);
            foreach (Selectable item in items)
            {
                if (item == null)
                    continue;
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                int index = active.IndexOf(item);
                if (index >= 0 && active.Count > 1)
                {
                    navigation.selectOnUp = active[(index - 1 + active.Count) % active.Count];
                    navigation.selectOnDown = active[(index + 1) % active.Count];
                }
                item.navigation = navigation;
            }
        }

        /// <summary>"클리어 시간 ........ 02:14.53" 처럼 점으로 이은 한 줄</summary>
        public static string Dotted(string left, string right, int width = 34)
        {
            int dots = Mathf.Max(3, width - VisibleLength(left) - VisibleLength(right));
            return $"{left} <color=#{UIStyle.Hex(UIStyle.Dim * 0.6f)}>{new string('.', dots)}</color> {right}";
        }

        // 리치 텍스트 태그를 뺀 글자 수. 한글은 고정폭 글꼴에서 두 칸을 차지하므로 2로 센다
        static int VisibleLength(string text)
        {
            int length = 0;
            bool inTag = false;
            foreach (char c in text)
            {
                if (c == '<')
                    inTag = true;
                else if (c == '>')
                    inTag = false;
                else if (!inTag)
                    length += c >= 0x1100 ? 2 : 1;
            }
            return length;
        }
    }
}
