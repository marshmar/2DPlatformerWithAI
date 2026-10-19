using UnityEngine;

namespace Platformer
{
    /// <summary>레벨 씬의 이름표. 레벨 빌더가 텍스트 레이아웃의 @name / @subtitle 로 채운다. HUD·인트로 배너가 읽는다.</summary>
    public sealed class LevelInfo : MonoBehaviour
    {
        [SerializeField] string code = "SECTOR";
        [SerializeField] string title = "";

        public string Code => code;
        public string Title => title;
    }
}
