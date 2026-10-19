using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 씬 전환 화면. 24px 블록이 왼쪽부터 무작위로 화면을 덮고(경계 블록은 네온색으로 번쩍), 다 덮이면 가운데에
    /// 다음 목적지가 해독되듯 나타난다. 새 씬이 준비되면 반대로 걷힌다. GameFlow 와 함께 씬이 바뀌어도 남는다.
    /// </summary>
    public sealed class ScreenWipe : MonoBehaviour
    {
        const float CloseTime = 0.45f;
        const float OpenTime = 0.5f;

        CyberGraphic cover;
        Text label;
        TextDecoder decoder;

        public static ScreenWipe Create(Transform parent)
        {
            Canvas canvas = UIFactory.CreateCanvas("[Screen Wipe]", 1000, parent);
            var wipe = canvas.gameObject.AddComponent<ScreenWipe>();
            wipe.cover = UIFactory.Shape(canvas.transform, "Cover", CyberGraphic.Shape.Wipe, UIStyle.Cyan, 0f, 0f);
            wipe.cover.rectTransform.Stretch();
            wipe.cover.Padding = 0f;
            wipe.cover.Fill = 0f;
            wipe.label = UIFactory.Label(canvas.transform, "Label", "", UIStyle.Mono, 30, UIStyle.Cyan, TextAnchor.MiddleCenter);
            wipe.label.rectTransform.Stretch();
            UIFactory.Glitch(wipe.label, 2f, 0.1f);
            wipe.decoder = UIFactory.Decoder(wipe.label);
            wipe.SetProgress(0f);
            return wipe;
        }

        void SetProgress(float progress)
        {
            cover.Fill = progress;
            cover.enabled = progress > 0f;
            cover.raycastTarget = progress > 0f; // 덮인 동안은 뒤의 메뉴를 누를 수 없게
        }

        public IEnumerator Close(string text)
        {
            label.text = "";
            label.color = UIStyle.Cyan;
            for (float t = 0f; t < CloseTime; t += Time.unscaledDeltaTime)
            {
                SetProgress(Mathf.SmoothStep(0f, 1f, t / CloseTime));
                yield return null;
            }
            SetProgress(1f);
            if (!string.IsNullOrEmpty(text))
                decoder.Play(text, 0f, 70f);
        }

        public IEnumerator Open()
        {
            // 목적지 이름을 잠깐 읽을 시간
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                yield return null;
            decoder.Finish();
            for (float t = 0f; t < OpenTime; t += Time.unscaledDeltaTime)
            {
                float progress = 1f - Mathf.SmoothStep(0f, 1f, t / OpenTime);
                SetProgress(progress);
                label.color = new Color(UIStyle.Cyan.r, UIStyle.Cyan.g, UIStyle.Cyan.b, progress);
                yield return null;
            }
            SetProgress(0f);
            label.text = "";
        }
    }
}
