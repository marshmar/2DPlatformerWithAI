namespace Platformer
{
    /// <summary>프로젝트 전체의 픽셀 기준. 스프라이트·타일 모두 16px = 1유닛.</summary>
    public static class PixelGrid
    {
        public const int PixelsPerUnit = 16;
        public const float UnitsPerPixel = 1f / PixelsPerUnit;
    }
}
