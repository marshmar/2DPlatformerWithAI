using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 사이버펑크 메뉴 버튼. 마우스를 올리면 그 버튼이 "선택"된다 — 키보드·패드 이동과 같은 상태 하나로 다뤄서
    /// 마우스와 키보드를 섞어 써도 강조가 두 군데 생기지 않는다.
    /// 강조는 Selectable 의 색 전환 대신 직접 애니메이션한다: 바탕 밝기·왼쪽 표시줄·글자색·화살표·살짝 오른쪽으로 밀림.
    /// </summary>
    public sealed class CyberButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] CyberGraphic background;
        [SerializeField] Text label;
        [SerializeField] Text caption;
        [SerializeField] Text caret;
        [SerializeField] RectTransform content;
        [SerializeField] Color accent = UIStyle.Cyan;

        float highlight;
        float flash;
        bool selected;

        public event Action Clicked;
        public Text Label => label;
        public Text Caption => caption;

        public void Bind(CyberGraphic graphic, Text title, Text subtitle, Text arrow, RectTransform body, Color accentColor)
        {
            background = graphic;
            label = title;
            caption = subtitle;
            caret = arrow;
            content = body;
            accent = accentColor;
            targetGraphic = graphic;
            transition = Transition.None;
            Refresh(true);
        }

        public void SetAccent(Color color)
        {
            accent = color;
            Refresh(true);
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
                flash = 1f;
                UISound.Play(UISound.Kind.Move, 0.7f);
            }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            selected = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Press();
        }

        public void OnSubmit(BaseEventData eventData) => Press();

        public void Press()
        {
            if (!IsActive() || !IsInteractable())
            {
                UISound.Play(UISound.Kind.Error);
                return;
            }
            flash = 1f;
            UISound.Play(UISound.Kind.Confirm);
            Clicked?.Invoke();
        }

        void Update() => Refresh(false);

        void Refresh(bool instant)
        {
            bool interactable = IsInteractable();
            float target = selected && interactable ? 1f : 0f;
            float dt = Time.unscaledDeltaTime;
            highlight = instant ? target : Mathf.MoveTowards(highlight, target, dt * 7f);
            flash = instant ? 0f : Mathf.MoveTowards(flash, 0f, dt * 4f);

            if (background != null)
            {
                background.color = interactable ? accent : UIStyle.Disabled;
                background.Value2 = highlight;
                background.Flash = flash;
            }
            Color text = interactable ? Color.Lerp(UIStyle.Foreground * 0.85f, Color.white, highlight) : UIStyle.Disabled;
            if (label != null)
                label.color = new Color(text.r, text.g, text.b, 1f);
            if (caption != null)
                caption.color = interactable ? Color.Lerp(UIStyle.Dim, accent, highlight) : UIStyle.Disabled * 0.8f;
            if (caret != null)
                caret.color = new Color(accent.r, accent.g, accent.b, highlight);
            if (content != null)
                content.anchoredPosition = new Vector2(Mathf.SmoothStep(0f, 14f, highlight), 0f);
        }
    }
}
