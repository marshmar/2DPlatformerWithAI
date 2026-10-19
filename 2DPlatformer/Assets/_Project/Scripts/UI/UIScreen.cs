using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 메뉴 화면 하나 (일시정지, 설정, 섹터 선택 ...). 열린 화면은 스택으로 관리해서 맨 위 화면만 입력을 받는다.
    ///   - 열 때: 투명 → 불투명 + 왼쪽에서 살짝 밀려 들어오고, 안의 글리치 글자가 한 번 지지직거린다
    ///   - Esc / 패드 B: Back 동작 (보통 이전 화면으로)
    ///   - 마우스로 빈 곳을 눌러 선택이 풀려도, 방향키를 누르면 첫 항목이 다시 선택된다
    ///   - Space 도 결정 키로 받는다 (기본 UI 입력은 Enter 만 결정)
    /// 모든 시간은 실제 시간 — 게임이 멈춘 동안 열리는 화면이 많다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIScreen : MonoBehaviour
    {
        static readonly List<UIScreen> stack = new();

        CanvasGroup group;
        RectTransform rect;
        Vector2 restPosition;
        float visibility;
        float target;
        int openedFrame = -1;

        public Selectable FirstSelected { get; set; }
        public Action Back { get; set; }
        public bool IsOpen => target > 0f;

        /// <summary>맨 위에 열린 화면 (없으면 null)</summary>
        public static UIScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        /// <summary>이번 프레임에 Back 이 처리됐으면 그 프레임 번호 — 같은 Esc 로 일시정지가 다시 열리지 않게</summary>
        public static int LastBackFrame { get; private set; } = -1;

        bool hasRestPosition;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        void OnDestroy() => stack.Remove(this);

        public void Open()
        {
            // 조립이 끝난 뒤 처음 열 때의 위치를 기준으로 밀려 들어온다
            if (!hasRestPosition)
            {
                restPosition = rect.anchoredPosition;
                hasRestPosition = true;
            }
            gameObject.SetActive(true);
            stack.Remove(this);
            stack.Add(this);
            target = 1f;
            openedFrame = Time.frameCount;
            group.interactable = true;
            group.blocksRaycasts = true;
            foreach (TextGlitch glitch in GetComponentsInChildren<TextGlitch>())
                glitch.Pulse(0.8f, 0.35f);
            SelectFirst();
        }

        public void Close()
        {
            stack.Remove(this);
            target = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void CloseImmediate()
        {
            Close();
            visibility = 0f;
            Apply();
            gameObject.SetActive(false);
        }

        public void SelectFirst()
        {
            if (FirstSelected == null || EventSystem.current == null)
                return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(FirstSelected.gameObject);
        }

        void Update()
        {
            float speed = target > visibility ? 6f : 9f;
            visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime * speed);
            Apply();
            if (visibility <= 0f && target <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            if (Top != this || Time.frameCount == openedFrame)
                return;

            if (BackPressed() && Back != null)
            {
                LastBackFrame = Time.frameCount;
                UISound.Play(UISound.Kind.Back);
                Back();
                return;
            }

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool ownsSelection = selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(transform);
            if (!ownsSelection && NavigationPressed())
                SelectFirst();
            else if (ownsSelection && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        void Apply()
        {
            group.alpha = visibility;
            if (!hasRestPosition)
                return;
            float eased = 1f - (1f - visibility) * (1f - visibility);
            rect.anchoredPosition = restPosition + new Vector2((1f - eased) * -28f, 0f);
        }

        public static bool BackPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        static bool NavigationPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                                     keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ||
                                     keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                return true;
            var gamepad = Gamepad.current;
            return gamepad != null && (gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame ||
                                       gamepad.buttonSouth.wasPressedThisFrame || gamepad.leftStick.ReadValue().sqrMagnitude > 0.5f);
        }
    }
}
