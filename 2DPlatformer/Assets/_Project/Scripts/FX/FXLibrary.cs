using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 게임 코드가 이펙트를 이름 대신 이 목록으로 찾는다. Resources/FXLibrary 에 하나만 둔다 (GameplaySetup 이 만든다).
    /// </summary>
    [CreateAssetMenu(menuName = "Platformer/FX Library")]
    public sealed class FXLibrary : ScriptableObject
    {
        static FXLibrary instance;

        public GameObject slash;
        public GameObject hitSpark;
        public GameObject shotImpact;
        public GameObject explosion;

        public static FXLibrary Instance => instance != null ? instance : instance = Resources.Load<FXLibrary>("FXLibrary");
    }

    /// <summary>칼 궤적. variant 0~2 는 콤보 1~3타, 3 은 처형.</summary>
    public static class SlashFX
    {
        static readonly Color[] Tints =
        {
            new(0.6f, 2.6f, 3.2f, 1f),  // 1타: 시안
            new(0.6f, 2.6f, 3.2f, 1f),  // 2타
            new(3.2f, 1.2f, 3.0f, 1f),  // 3타(마무리): 마젠타
            new(4.0f, 0.8f, 1.4f, 1f),  // 처형: 붉은 마젠타
        };

        public static void Spawn(Vector2 position, int facing, int variant)
        {
            FXLibrary library = FXLibrary.Instance;
            if (library == null || library.slash == null)
                return;

            GameObject slash = FXPool.Spawn(library.slash, position);
            // 1타는 위에서 아래로, 2타는 아래에서 위로(세로 뒤집기), 3타·처형은 크고 수평으로
            float angle = variant switch { 0 => -20f, 1 => 20f, _ => 0f };
            float size = variant switch { 0 => 2.6f, 1 => 2.6f, 2 => 3.4f, _ => 4.4f };
            slash.transform.rotation = Quaternion.Euler(0f, 0f, angle * facing);
            slash.transform.localScale = new Vector3(size * facing, variant == 1 ? -size : size, 1f);
            slash.GetComponent<TimedFX>().SetTint(Tints[Mathf.Clamp(variant, 0, Tints.Length - 1)]);
        }
    }

    /// <summary>타격 지점의 별 모양 불꽃.</summary>
    public static class HitSparkFX
    {
        public static void Spawn(Vector2 position, float scale = 1f)
        {
            FXLibrary library = FXLibrary.Instance;
            if (library == null || library.hitSpark == null)
                return;
            GameObject spark = FXPool.Spawn(library.hitSpark, position);
            spark.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            spark.transform.localScale = Vector3.one * (1.6f * scale);
        }
    }
}
