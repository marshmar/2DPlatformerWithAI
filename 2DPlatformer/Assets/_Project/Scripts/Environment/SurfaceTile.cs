using UnityEngine;
using UnityEngine.Tilemaps;

namespace Platformer
{
    /// <summary>
    /// 충돌 타일맵용 타일. 표면 속성을 타일 단위로 가진다.
    /// 갈고리 가능/불가 면을 타일맵 레이어로 나누면 두 콜라이더가 맞닿는 이음새에 캐릭터가 걸리므로,
    /// 콜라이더는 하나로 합치고 속성은 맞은 지점의 타일에서 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Platformer/Surface Tile")]
    public sealed class SurfaceTile : Tile
    {
        [SerializeField] bool hookable;

        public bool Hookable => hookable;
    }
}
