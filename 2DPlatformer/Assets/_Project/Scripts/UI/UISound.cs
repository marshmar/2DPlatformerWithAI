using System.Collections.Generic;
using UnityEngine;

namespace Platformer.UI
{
    /// <summary>
    /// 메뉴·알림 효과음. 오디오 파일 없이 짧은 파형을 코드로 합성한다 (전자음 "삑" 소리가 사이버펑크 UI 와 잘 맞는다).
    /// 일시정지 중에도 나야 하므로 AudioListener.pause 를 무시하는 전용 소스로 재생한다. 볼륨은 AudioListener.volume(마스터)을 따른다.
    /// </summary>
    public static class UISound
    {
        public enum Kind { Move, Confirm, Back, Error, Checkpoint, Pickup, Clear, Glitch, Open }

        const int SampleRate = 44100;
        static readonly Dictionary<Kind, AudioClip> clips = new();
        static AudioSource source;

        public static void Play(Kind kind, float volume = 1f)
        {
            if (source == null)
            {
                var go = new GameObject("[UISound]");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.ignoreListenerPause = true;
                source.spatialBlend = 0f;
                source.playOnAwake = false;
            }
            if (!clips.TryGetValue(kind, out AudioClip clip) || clip == null)
                clips[kind] = clip = Build(kind);
            source.PlayOneShot(clip, volume);
        }

        static AudioClip Build(Kind kind) => kind switch
        {
            Kind.Move => Tone("ui_move", 0.035f, 1900f, 1700f, Wave.Square, 0.12f),
            Kind.Confirm => Sequence("ui_confirm", (0.05f, 900f), (0.08f, 1400f)),
            Kind.Back => Tone("ui_back", 0.09f, 750f, 420f, Wave.Square, 0.14f),
            Kind.Error => Tone("ui_error", 0.16f, 170f, 150f, Wave.Square, 0.16f),
            Kind.Checkpoint => Sequence("ui_checkpoint", (0.08f, 660f), (0.08f, 880f), (0.22f, 1320f)),
            Kind.Pickup => Tone("ui_pickup", 0.12f, 1500f, 2600f, Wave.Sine, 0.3f),
            Kind.Clear => Sequence("ui_clear", (0.09f, 523f), (0.09f, 659f), (0.09f, 784f), (0.4f, 1047f)),
            Kind.Glitch => Noise("ui_glitch", 0.18f, 0.18f),
            Kind.Open => Tone("ui_open", 0.25f, 300f, 1200f, Wave.Saw, 0.12f),
            _ => Tone("ui_blip", 0.05f, 1000f, 1000f, Wave.Sine, 0.2f),
        };

        enum Wave { Sine, Square, Saw }

        // 주파수를 from → to 로 미끄러뜨리는 짧은 음. 끝으로 갈수록 작아진다
        static AudioClip Tone(string name, float duration, float from, float to, Wave wave, float amplitude)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += Mathf.Lerp(from, to, t) / SampleRate;
                float p = (float)(phase % 1.0);
                float sample = wave switch
                {
                    Wave.Square => p < 0.5f ? 1f : -1f,
                    Wave.Saw => p * 2f - 1f,
                    _ => Mathf.Sin(p * Mathf.PI * 2f),
                };
                float envelope = Mathf.Min(1f, i / (SampleRate * 0.002f)) * (1f - t) * (1f - t);
                data[i] = sample * envelope * amplitude;
            }
            return Clip(name, data);
        }

        // 음 여러 개를 이어 붙인 아르페지오 (마지막 음이 길게 울린다)
        static AudioClip Sequence(string name, params (float duration, float frequency)[] notes)
        {
            var data = new List<float>();
            foreach (var (duration, frequency) in notes)
            {
                int count = Mathf.CeilToInt(duration * SampleRate);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / count;
                    float sample = Mathf.Sin(2f * Mathf.PI * frequency * i / SampleRate);
                    sample += 0.3f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * i / SampleRate)); // 살짝 거친 8비트 맛
                    float envelope = Mathf.Min(1f, i / (SampleRate * 0.003f)) * (1f - t * 0.7f);
                    data.Add(sample * envelope * 0.16f);
                }
            }
            return Clip(name, data.ToArray());
        }

        static AudioClip Noise(string name, float duration, float amplitude)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            var random = new System.Random(7);
            float held = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i % 40 == 0)
                    held = (float)random.NextDouble() * 2f - 1f; // 샘플을 붙잡아 두면 지직거리는 비트크러시 소리가 난다
                float t = (float)i / count;
                data[i] = held * (1f - t) * amplitude;
            }
            return Clip(name, data);
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
