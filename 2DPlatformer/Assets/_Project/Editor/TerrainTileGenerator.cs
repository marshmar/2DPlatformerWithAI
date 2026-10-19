using System;
using System.IO;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// Warped City 타일셋에는 바닥·벽 타일이 없어서(건물 파사드 파츠뿐) 지형 타일을 코드로 그린다.
    /// 같은 팔레트(어두운 남색 철판 + 청록 테두리)를 써서 배경 에셋과 어울리게 하고,
    /// 갈고리가 걸리는 면은 빛나는 청록 스터드, 위험 지대는 마젠타 전기 가시로 한눈에 구분되게 한다.
    /// 밝은 색(스터드·가시)은 Terrain 머티리얼의 자체 발광 문턱을 넘어서 어두운 곳에서도 빛난다.
    /// </summary>
    public static class TerrainTileGenerator
    {
        public enum Kind { Fill, FillVent, FillSeam, Top, TopLeft, TopRight, Left, Right, HookTop, HookFill, Beam, Spikes }

        public const int Count = 12;
        const int Size = 16;

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 Seam = Rgb(5, 10, 18);
        static readonly Color32 Base = Rgb(12, 24, 38);
        static readonly Color32 BaseLight = Rgb(18, 36, 54);
        static readonly Color32 BaseDark = Rgb(8, 16, 27);
        static readonly Color32 Rivet = Rgb(40, 70, 90);
        static readonly Color32 Rim = Rgb(60, 190, 200);
        static readonly Color32 RimDark = Rgb(22, 100, 118);
        static readonly Color32 Grate = Rgb(16, 48, 60);
        static readonly Color32 GrateLight = Rgb(26, 80, 96);
        static readonly Color32 Stud = Rgb(120, 255, 230);
        static readonly Color32 HookRim = Rgb(70, 220, 210);
        static readonly Color32 Beam = Rgb(24, 42, 62);
        static readonly Color32 BeamLight = Rgb(40, 66, 92);
        static readonly Color32 SpikeDark = Rgb(90, 10, 60);
        static readonly Color32 Spike = Rgb(220, 40, 150);
        static readonly Color32 SpikeTip = Rgb(255, 190, 240);

        static Color32 Rgb(int r, int g, int b) => new((byte)r, (byte)g, (byte)b, 255);

        /// <summary>타일 12장을 가로로 이어 붙인 192x16 PNG 를 쓴다. 칸 순서 = Kind 순서.</summary>
        public static void Write(string path)
        {
            var pixels = new Color32[Size * Count * Size];
            for (int tile = 0; tile < Count; tile++)
            {
                Func<int, int, Color32> paint = Painter((Kind)tile);
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        pixels[y * Size * Count + tile * Size + x] = paint(x, y);
            }

            var texture = new Texture2D(Size * Count, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        // (x, y) 는 타일 안 픽셀 좌표, y = 15 가 맨 위 줄
        static Func<int, int, Color32> Painter(Kind kind) => kind switch
        {
            Kind.Fill => Panel,
            Kind.FillVent => (x, y) => x >= 4 && x <= 11 && y >= 5 && y <= 11 && y % 2 == 1 ? Seam
                                      : (x == 3 || x == 12) && y >= 4 && y <= 12 ? BaseDark : Panel(x, y),
            Kind.FillSeam => (x, y) => y == 8 ? Seam : y == 9 && (x == 3 || x == 12) ? Rivet : Panel(x, y),
            Kind.Top => TopEdge,
            Kind.TopLeft => (x, y) => x == 0 ? (y >= 14 ? Rim : RimDark) : TopEdge(x, y),
            Kind.TopRight => (x, y) => x == 15 ? (y >= 14 ? Rim : RimDark) : TopEdge(x, y),
            Kind.Left => (x, y) => x == 0 ? RimDark : x == 1 ? Seam : Panel(x, y),
            Kind.Right => (x, y) => x == 15 ? RimDark : x == 14 ? Seam : Panel(x, y),
            Kind.HookTop => (x, y) => y == 15 ? HookRim : y == 14 ? RimDark : y == 13 ? Seam
                                    : y == 11 && (x == 3 || x == 8 || x == 12) ? Stud : GrateCell(x, y),
            Kind.HookFill => GrateCell,
            Kind.Beam => BeamCell,
            Kind.Spikes => SpikeCell,
            _ => Panel,
        };

        // 철판: 왼쪽·위 밝은 테두리, 오른쪽·아래 이음매, 네 귀퉁이 리벳, 가운데 어두운 띠
        static Color32 Panel(int x, int y)
        {
            if (x == 15 || y == 0)
                return Seam;
            if (x == 0 || y == 15)
                return BaseLight;
            if ((x == 2 || x == 13) && (y == 2 || y == 13))
                return Rivet;
            return y == 7 || y == 8 ? BaseDark : Base;
        }

        // 윗면: 맨 위 2줄이 밝은 청록 테두리, 그 아래 이음매, 리벳 한 줄
        static Color32 TopEdge(int x, int y)
        {
            if (y == 15)
                return Rim;
            if (y == 14)
                return RimDark;
            if (y == 13)
                return Seam;
            if (y == 11 && (x == 3 || x == 12))
                return Rivet;
            return Panel(x, Math.Min(y, 12));
        }

        // 갈고리 면: 격자 철판 (4칸마다 밝은 선, 그 사이 구멍)
        static Color32 GrateCell(int x, int y)
        {
            if (x == 15 || y == 0)
                return Seam;
            if (x % 4 == 2 && y % 4 == 2)
                return Seam;
            return x % 4 == 0 || y % 4 == 0 ? GrateLight : Grate;
        }

        // 철골: 트러스 무늬 + 윗면 청록 테두리 + 아래쪽 발광 스터드(아래에서 걸 수 있다는 표시)
        static Color32 BeamCell(int x, int y)
        {
            if (y == 15)
                return HookRim;
            if (y == 14)
                return RimDark;
            if (y == 13 || y == 2)
                return Seam;
            if (y <= 1)
                return y == 1 && (x == 4 || x == 11) ? Stud : Beam;
            int diagonal = (x + y) % 8;
            int antiDiagonal = (x - y + 16) % 8;
            return diagonal == 0 || antiDiagonal == 0 ? BeamLight : Beam;
        }

        // 전기 가시: 4px 폭 이빨 4개, 끝은 밝게
        static Color32 SpikeCell(int x, int y)
        {
            if (y <= 2)
                return SpikeDark;
            int local = x % 4;
            int height = local == 1 || local == 2 ? 11 : 6;
            if (y > height)
                return Clear;
            return y == height ? SpikeTip : Spike;
        }
    }
}
