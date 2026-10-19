using UnityEngine;

namespace Platformer
{
    /// <summary>조작 안내 구역. 플레이어가 안에 있는 동안 HUD 아래쪽에 안내 문구를 띄운다. 레벨 텍스트의 @hint 로 만든다.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class HintZone : MonoBehaviour
    {
        [SerializeField, TextArea] string message = "";

        public string Message => message;

        public void SetMessage(string text) => message = text;

        void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null && UI.GameUI.Instance != null)
                UI.GameUI.Instance.ShowHint(this);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null && UI.GameUI.Instance != null)
                UI.GameUI.Instance.HideHint(this);
        }
    }
}
