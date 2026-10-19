using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 텍스처 없이 셰이더(Platformer/UI/Cyber)로 그리는 UI 도형. 사각형 하나를 만들고 도형 정보를 UV 채널에 실어 보낸다:
    ///   uv0 = 도형 왼쪽 아래 기준 위치(캔버스 단위), uv1 = (폭, 높이, 모서리, 테두리), uv2 = (도형, 채움, 기울기, 보조), uv3 = (칸 수, 시드, 플래시)
    /// 바깥 빛 번짐을 그릴 자리가 필요해서 사각형을 padding 만큼 키워서 만든다 (클릭 판정은 원래 RectTransform 크기 그대로).
    /// 캔버스의 additionalShaderChannels 에 TexCoord1~3 이 켜져 있어야 한다 (UIFactory.CreateCanvas 가 켠다).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CyberGraphic : MaskableGraphic
    {
        public enum Shape { Panel = 0, Button = 1, Bar = 2, Segments = 3, Frame = 4, Line = 5, Backdrop = 6, Wipe = 7, Diamond = 8, Glow = 9 }

        [SerializeField] Shape shape;
        [Tooltip("왼쪽 위·오른쪽 아래 모서리를 깎는 길이 (캔버스 단위)")]
        [SerializeField] float chamfer = 12f;
        [SerializeField] float border = 3f;
        [Tooltip("평행사변형 기울기 (높이 1 당 가로 이동)")]
        [SerializeField] float skew;
        [SerializeField, Range(0f, 1f)] float fill = 1f;
        [Tooltip("버튼 강조 / 게이지 잔상")]
        [SerializeField] float value2;
        [SerializeField] int segments = 5;
        [SerializeField] float flash;
        [Tooltip("바깥 빛 번짐용 여백")]
        [SerializeField] float padding = 14f;

        static Material sharedMaterial;
        float seed;

        public static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                    sharedMaterial = UIStyle.LoadMaterial("UI/CyberUI", "Platformer/UI/Cyber");
                return sharedMaterial;
            }
        }

        public override Material defaultMaterial => SharedMaterial;

        public Shape ShapeType { get => shape; set => Set(ref shape, value); }
        public float Chamfer { get => chamfer; set => Set(ref chamfer, value); }
        public float Border { get => border; set => Set(ref border, value); }
        public float Skew { get => skew; set => Set(ref skew, value); }
        public float Fill { get => fill; set => Set(ref fill, Mathf.Clamp01(value)); }
        public float Value2 { get => value2; set => Set(ref value2, value); }
        public int Segments { get => segments; set => Set(ref segments, Mathf.Max(1, value)); }
        public float Flash { get => flash; set => Set(ref flash, Mathf.Clamp01(value)); }
        public float Padding { get => padding; set => Set(ref padding, value); }

        protected override void Awake()
        {
            base.Awake();
            // 같은 모양이 여러 개 있어도 훑는 띠·신호가 똑같이 움직이지 않게
            seed = Mathf.Repeat(GetInstanceID() * 0.61803f, 1f);
        }

        void Set<T>(ref T field, T value)
        {
            if (Equals(field, value))
                return;
            field = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float pad = padding;
            if (shape == Shape.Segments || shape == Shape.Bar)
                pad = Mathf.Max(pad, Mathf.Abs(skew) * rect.height * 0.5f + 2f); // 기울어진 끝이 잘리지 않게

            var size = new Vector4(rect.width, rect.height, chamfer, border);
            var parameters = new Vector4((int)shape, fill, skew, value2);
            var extra = new Vector4(segments, seed, flash, 0f);
            Color32 tint = color;

            Vector2 min = rect.min - Vector2.one * pad;
            Vector2 max = rect.max + Vector2.one * pad;
            AddCorner(vh, new Vector2(min.x, min.y), rect.min, tint, size, parameters, extra);
            AddCorner(vh, new Vector2(min.x, max.y), rect.min, tint, size, parameters, extra);
            AddCorner(vh, new Vector2(max.x, max.y), rect.min, tint, size, parameters, extra);
            AddCorner(vh, new Vector2(max.x, min.y), rect.min, tint, size, parameters, extra);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        static void AddCorner(VertexHelper vh, Vector2 position, Vector2 origin, Color32 tint, Vector4 size, Vector4 parameters, Vector4 extra)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vertex.uv0 = position - origin;
            vertex.uv1 = size;
            vertex.uv2 = parameters;
            vertex.uv3 = extra;
            vh.AddVert(vertex);
        }
    }
}
