using UnityEngine;

namespace Platformer
{
    /// <summary>수집품 데이터 칩. 둥실거리며 도는 모습은 DataChip.shader 가 그리고, 여기서는 닿으면 줍기만 한다.</summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class DataChip : MonoBehaviour
    {
        void Awake() => GetComponent<CircleCollider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
                return;
            HitSparkFX.Spawn(transform.position, 1.2f);
            UI.UISound.Play(UI.UISound.Kind.Pickup);
            if (GameFlow.Instance != null)
                GameFlow.Instance.CollectChip();
            Destroy(gameObject);
        }
    }
}
