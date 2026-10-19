using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 글자마다 빨강·청록 잔상 사본을 앞에 끼워 넣고, 셰이더(Platformer/UI/CyberText)가 읽을 표시를 uv1 에 싣는다.
    /// 잔상을 벌리고 글자를 튕기는 움직임은 셰이더의 정점 단계가 시간으로 계산하므로, 여기서는 세기가 바뀔 때만 메시를 다시 만든다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class TextGlitch : BaseMeshEffect
    {
        [Tooltip("평소 잔상 간격 (캔버스 단위)")]
        [SerializeField] float split = 2f;
        [Tooltip("평소 글리치 세기")]
        [SerializeField, Range(0f, 1f)] float baseIntensity;
        [Tooltip("이 간격(초)마다 잠깐 크게 지지직거린다. 0 이면 안 함")]
        [SerializeField] float burstInterval;

        readonly List<UIVertex> stream = new();
        readonly List<UIVertex> output = new();
        float pulse;
        float pulseDecay = 2f;
        float nextBurst;
        float applied = -1f;

        public float Split { get => split; set { split = value; Refresh(); } }
        public float BaseIntensity { get => baseIntensity; set => baseIntensity = value; }

        float Intensity => Mathf.Clamp01(baseIntensity + pulse);

        public void Configure(float splitAmount, float intensity, float interval)
        {
            split = splitAmount;
            baseIntensity = intensity;
            burstInterval = interval;
            nextBurst = Time.unscaledTime + Random.Range(0.5f, Mathf.Max(interval, 0.5f));
            Refresh();
        }

        /// <summary>잠깐 강하게 지지직거렸다가 duration 동안 가라앉는다 (화면 등장·선택 순간).</summary>
        public void Pulse(float strength, float duration)
        {
            pulse = Mathf.Max(pulse, strength);
            pulseDecay = strength / Mathf.Max(duration, 0.01f);
        }

        void Update()
        {
            if (burstInterval > 0f && Time.unscaledTime >= nextBurst)
            {
                Pulse(Random.Range(0.4f, 0.8f), 0.25f);
                nextBurst = Time.unscaledTime + burstInterval * Random.Range(0.6f, 1.4f);
            }
            if (pulse > 0f)
                pulse = Mathf.Max(0f, pulse - pulseDecay * Time.unscaledDeltaTime);

            // 세기를 메시에 실어 보내므로 눈에 띄게 바뀌었을 때만 다시 만든다
            float intensity = Intensity;
            if (Mathf.Abs(intensity - applied) > 0.02f || (intensity == 0f && applied != 0f))
                Refresh();
        }

        void Refresh()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;
            stream.Clear();
            output.Clear();
            vh.GetUIVertexStream(stream);
            float intensity = Intensity;
            applied = intensity;

            // 사각형(삼각형 6개 꼭짓점)마다 글자 하나. 잔상 두 벌을 먼저 넣어야 본체가 위에 그려진다
            int quadCount = stream.Count / 6;
            for (int ghost = 1; ghost <= 2; ghost++)
                for (int quad = 0; quad < quadCount; quad++)
                    AppendQuad(quad, ghost, intensity);
            for (int quad = 0; quad < quadCount; quad++)
                AppendQuad(quad, 0, intensity);

            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }

        void AppendQuad(int quad, int ghost, float intensity)
        {
            for (int i = 0; i < 6; i++)
            {
                UIVertex vertex = stream[quad * 6 + i];
                vertex.uv1 = new Vector4(ghost, quad + 1, intensity, Mathf.Max(split, 0.001f));
                output.Add(vertex);
            }
        }
    }
}
