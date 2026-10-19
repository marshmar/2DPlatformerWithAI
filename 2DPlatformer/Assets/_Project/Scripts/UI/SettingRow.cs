using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 설정 한 줄: 왼쪽 이름, 오른쪽 값. 좌우 입력(키보드·패드)이나 클릭으로 값을 바꾼다.
    ///   칸 게이지형(볼륨 등): 값 0 ~ count-1 을 칸으로 보여 주고, 게이지를 클릭하면 그 위치 값으로
    ///   선택형(켜짐/꺼짐, 해상도 등): "◀ 값 ▶" — 왼쪽 반을 클릭하면 이전, 오른쪽 반이면 다음
    /// 위아래 이동은 Selectable 기본 동작(명시적 내비게이션)에 맡긴다.
    /// </summary>
    public sealed class SettingRow : Selectable, IPointerClickHandler, ISubmitHandler
    {
        CyberGraphic background;
        CyberGraphic gauge;
        Text label;
        Text valueText;
        Func<int> getter;
        Action<int> setter;
        Func<int, string> formatter;
        int count;
        bool wrap;
        float highlight;
        float flash;
        bool selected;

        public void Bind(CyberGraphic graphic, Text name, Text value, CyberGraphic segments,
            int optionCount, Func<int> get, Action<int> set, Func<int, string> format, bool wrapAround)
        {
            background = graphic;
            label = name;
            valueText = value;
            gauge = segments;
            count = Mathf.Max(1, optionCount);
            getter = get;
            setter = set;
            formatter = format;
            wrap = wrapAround;
            targetGraphic = graphic;
            transition = Transition.None;
            RefreshValue();
        }

        /// <summary>선택지 목록이 바뀌었을 때 (해상도 목록 등)</summary>
        public void SetOptionCount(int optionCount)
        {
            count = Mathf.Max(1, optionCount);
            RefreshValue();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (!selected)
            {
                selected = true;
                UISound.Play(UISound.Kind.Move, 0.7f);
            }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            selected = false;
        }

        public override void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    Step(-1);
                    eventData.Use();
                    break;
                case MoveDirection.Right:
                    Step(1);
                    eventData.Use();
                    break;
                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        public void OnSubmit(BaseEventData eventData) => Step(1, alwaysWrap: true);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable())
                return;

            // 게이지형: 클릭한 칸으로 바로
            if (gauge != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(gauge.rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Rect rect = gauge.rectTransform.rect;
                if (rect.Contains(local) || Mathf.Abs(local.y - rect.center.y) < rect.height)
                {
                    float u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
                    Apply(Mathf.Clamp(Mathf.RoundToInt(u * (count - 1)), 0, count - 1));
                    return;
                }
            }

            // 선택형: 값 영역 왼쪽 반이면 이전, 오른쪽 반이면 다음
            if (valueText != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(valueText.rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 point))
                Step(point.x < valueText.rectTransform.rect.center.x ? -1 : 1, alwaysWrap: true);
            else
                Step(1, alwaysWrap: true);
        }

        void Step(int direction, bool alwaysWrap = false)
        {
            if (!IsInteractable() || getter == null)
                return;
            int current = getter();
            int next = current + direction;
            if (wrap || alwaysWrap && gauge == null)
                next = (next % count + count) % count;
            else
                next = Mathf.Clamp(next, 0, count - 1);
            if (next == current)
            {
                UISound.Play(UISound.Kind.Error, 0.5f);
                return;
            }
            Apply(next);
        }

        void Apply(int value)
        {
            setter?.Invoke(value);
            flash = 1f;
            UISound.Play(UISound.Kind.Move);
            RefreshValue();
        }

        public void RefreshValue()
        {
            if (getter == null)
                return;
            int value = getter();
            if (gauge != null)
            {
                gauge.Segments = count - 1;
                gauge.Fill = count <= 1 ? 1f : value / (float)(count - 1);
                gauge.Value2 = 0f;
            }
            if (valueText != null)
                valueText.text = gauge != null ? formatter(value) : $"◀  {formatter(value)}  ▶";
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool interactable = IsInteractable();
            highlight = Mathf.MoveTowards(highlight, selected && interactable ? 1f : 0f, dt * 7f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 5f);
            Color accent = interactable ? UIStyle.Cyan : UIStyle.Disabled;
            if (background != null)
            {
                background.color = new Color(accent.r, accent.g, accent.b, 0.55f + highlight * 0.45f);
                background.Value2 = highlight;
            }
            if (gauge != null)
            {
                gauge.color = Color.Lerp(UIStyle.Cyan * 0.8f, UIStyle.Cyan, highlight);
                gauge.Flash = flash * 0.5f;
            }
            if (label != null)
                label.color = interactable ? Color.Lerp(UIStyle.Foreground * 0.8f, Color.white, highlight) : UIStyle.Disabled;
            if (valueText != null)
                valueText.color = interactable ? Color.Lerp(UIStyle.Dim, UIStyle.Amber, highlight) : UIStyle.Disabled;
        }
    }
}
