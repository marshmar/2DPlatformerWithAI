using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 해독 연출: 글자가 무작위 기호로 지글거리다가 왼쪽부터 하나씩 제자리를 찾는다. 시간은 실제 시간 기준(일시정지 중에도 동작).
    /// 리치 텍스트 태그(&lt;color=...&gt;)는 글자로 세지 않고 통째로 내보내며, 중간까지만 나온 문장에서는 열린 태그를 닫아 준다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class TextDecoder : MonoBehaviour
    {
        // < > 는 넣지 않는다 — 우연히 리치 텍스트 태그가 만들어질 수 있다
        const string Glyphs = "!-_\\/[]{}=+*^?#$%&01ABCDEFX";

        // 보이는 글자 하나, 또는 태그 하나
        readonly struct Token
        {
            public readonly string Text;
            public readonly bool IsTag;

            public Token(string text, bool isTag)
            {
                Text = text;
                IsTag = isTag;
            }
        }

        readonly List<Token> tokens = new();
        readonly Stack<string> openTags = new();
        readonly StringBuilder builder = new();
        Text label;
        string target = "";
        int visibleCount;
        float startTime;
        float duration;
        bool running;

        public bool IsDone => !running;

        void Awake() => label = GetComponent<Text>();

        /// <summary>text 를 delay 뒤에 charsPerSecond 속도로 해독하며 보여 준다.</summary>
        public void Play(string text, float delay = 0f, float charsPerSecond = 40f)
        {
            if (label == null)
                label = GetComponent<Text>();
            target = text ?? "";
            Tokenize();
            startTime = Time.unscaledTime + delay;
            duration = visibleCount / Mathf.Max(charsPerSecond, 1f);
            running = true;
            label.text = "";
        }

        public void Finish()
        {
            running = false;
            if (label != null)
                label.text = target;
        }

        void Tokenize()
        {
            tokens.Clear();
            visibleCount = 0;
            for (int i = 0; i < target.Length; i++)
            {
                if (target[i] == '<')
                {
                    int close = target.IndexOf('>', i);
                    if (close > i)
                    {
                        tokens.Add(new Token(target.Substring(i, close - i + 1), true));
                        i = close;
                        continue;
                    }
                }
                tokens.Add(new Token(target[i].ToString(), false));
                visibleCount++;
            }
        }

        void Update()
        {
            if (!running)
                return;
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < 0f)
                return;
            float progress = duration <= 0f ? 1f : elapsed / duration;
            if (progress >= 1f)
            {
                Finish();
                return;
            }

            int resolved = Mathf.FloorToInt(progress * visibleCount);
            int noisy = 6; // 해독된 글자 뒤로 몇 글자만 지글거린다
            builder.Clear();
            openTags.Clear();
            int shown = 0;
            foreach (Token token in tokens)
            {
                if (token.IsTag)
                {
                    if (shown > resolved)
                        continue; // 지글거리는 부분에는 태그를 넣지 않는다
                    builder.Append(token.Text);
                    if (token.Text.StartsWith("</"))
                    {
                        if (openTags.Count > 0)
                            openTags.Pop();
                    }
                    else
                    {
                        int end = token.Text.IndexOfAny(new[] { '=', '>' });
                        openTags.Push(token.Text.Substring(1, end - 1));
                    }
                    continue;
                }
                if (shown < resolved)
                    builder.Append(token.Text);
                else if (shown < resolved + noisy)
                    builder.Append(char.IsWhiteSpace(token.Text[0]) ? ' ' : Glyphs[Random.Range(0, Glyphs.Length)]);
                else
                    break;
                shown++;
            }
            while (openTags.Count > 0)
                builder.Append("</").Append(openTags.Pop()).Append('>');
            label.text = builder.ToString();
        }
    }
}
