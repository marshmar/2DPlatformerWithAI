using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Platformer
{
    /// <summary>
    /// 고장 난 네온: 평소엔 미세하게 웅웅거리다가(펄린 노이즈) 가끔 툭 꺼졌다 켜진다.
    /// 히트스톱·슬로우와 무관하게 실제 시간으로 — 배경은 게임 시간이 멈춰도 살아 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class NeonFlicker : MonoBehaviour
    {
        [Tooltip("초당 깜빡임 확률")]
        [SerializeField] float flickerChance = 0.25f;
        [SerializeField] Vector2 flickerDuration = new(0.04f, 0.14f);
        [SerializeField, Range(0f, 0.3f)] float hum = 0.08f;

        Light2D neon;
        float baseIntensity;
        float offUntil;
        float seed;

        void Awake()
        {
            neon = GetComponent<Light2D>();
            baseIntensity = neon.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= offUntil && Random.value < flickerChance * Time.unscaledDeltaTime)
                offUntil = now + Random.Range(flickerDuration.x, flickerDuration.y);

            float humming = 1f - hum + hum * Mathf.PerlinNoise(seed, now * 6f) * 2f;
            neon.intensity = now < offUntil ? baseIntensity * 0.12f : baseIntensity * humming;
        }
    }
}
