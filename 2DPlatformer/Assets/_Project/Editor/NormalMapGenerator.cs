using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 픽셀아트 스프라이트에서 노말맵을 만든다. 원리:
    ///   1) 높이 = 가장자리에서 안쪽으로 들어갈수록 높아지는 경사(베벨) + 밝은 픽셀일수록 튀어나오는 디테일
    ///   2) 높이의 기울기(Sobel 필터) → 표면 법선
    ///   3) 법선 방향을 몇 단계로 끊는다 — 매끈한 법선은 픽셀아트 위에서 플라스틱처럼 보인다
    /// 스프라이트(프레임) 단위로 계산해서 시트의 옆 프레임과 섞이지 않는다.
    /// 결과는 원본 스프라이트의 Secondary Texture(_NormalMap)로 등록되어 2D 라이트가 자동으로 사용한다.
    /// </summary>
    public static class NormalMapGenerator
    {
        const string OutputFolder = "Assets/_Project/Art/Generated/Normals";
        const string CraftpixRoot = "Assets/ThirdParty/Free 3 Cyberpunk Sprites Pixel Art/";
        const string TilesetPath = "Assets/ThirdParty/warped city files/warped city files/Assets/ENVIRONMENT/tileset.png";

        const int BevelPixels = 3;        // 가장자리 경사 폭
        const float EdgeWeight = 0.65f;   // 높이 중 경사의 비중
        const float DetailWeight = 0.35f; // 높이 중 밝기 디테일의 비중
        const float Strength = 2.2f;      // 기울기를 법선으로 바꿀 때의 배율 — 클수록 입체감이 강하다
        const int QuantizeSteps = 4;      // 법선 x·y 를 1/4 단위로 끊는다

        [MenuItem("Tools/Project Setup/Step 9 (Generate Normal Maps)", priority = 8)]
        public static void GenerateAll()
        {
            var sources = new List<string>();
            foreach (string folder in new[] { "3 Cyborg", "2 Punk", "1 Biker" })
                sources.AddRange(Directory.GetFiles(CraftpixRoot + folder, "*.png").Select(p => p.Replace('\\', '/')));
            sources.Add(TilesetPath);

            EnsureFolder(OutputFolder);
            int count = 0;
            foreach (string source in sources)
            {
                if (Generate(source))
                    count++;
            }
            Debug.Log($"[Normal Maps] {count}개 생성 → {OutputFolder}");
        }

        public static bool Generate(string sourcePath)
        {
            var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            if (importer == null)
                return false;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(sourcePath)))
                    return false;

                int width = texture.width;
                int height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                var normals = new Color32[pixels.Length];
                Array.Fill(normals, new Color32(128, 128, 255, 255)); // 평평한 법선 (0, 0, 1)

                foreach (RectInt area in SpriteAreas(importer, width, height))
                    BakeArea(pixels, normals, width, area);

                string outputPath = $"{OutputFolder}/{Path.GetFileNameWithoutExtension(sourcePath)}_n.png";
                var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                output.SetPixels32(normals);
                File.WriteAllBytes(outputPath, output.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(output);

                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureNormalImporter(outputPath);
                RegisterSecondaryTexture(importer, AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath));
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static IEnumerable<RectInt> SpriteAreas(TextureImporter importer, int width, int height)
        {
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
                return new[] { new RectInt(0, 0, width, height) };

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            return provider.GetSpriteRects().Select(r => new RectInt((int)r.rect.x, (int)r.rect.y, (int)r.rect.width, (int)r.rect.height));
        }

        static void BakeArea(Color32[] pixels, Color32[] normals, int textureWidth, RectInt area)
        {
            int w = area.width;
            int h = area.height;
            var heights = new float[w * h];
            var distance = new int[w * h];

            // 1) 투명 픽셀(과 영역 바깥)에서의 거리 — 경사를 만들기 위해. BevelPixels 번 이웃 전파로 충분하다
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    distance[y * w + x] = Pixel(pixels, textureWidth, area, x, y).a > 127 ? BevelPixels : 0;
            for (int pass = 0; pass < BevelPixels; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (distance[i] == 0)
                            continue;
                        int nearest = Mathf.Min(
                            Mathf.Min(Distance(distance, w, h, x - 1, y), Distance(distance, w, h, x + 1, y)),
                            Mathf.Min(Distance(distance, w, h, x, y - 1), Distance(distance, w, h, x, y + 1)));
                        distance[i] = Mathf.Min(distance[i], nearest + 1);
                    }
                }
            }

            // 2) 높이 = 경사 + 밝기 디테일
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = Pixel(pixels, textureWidth, area, x, y);
                    if (c.a <= 127)
                        continue;
                    float bevel = Mathf.SmoothStep(0f, 1f, distance[y * w + x] / (float)BevelPixels);
                    float luminance = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                    heights[y * w + x] = EdgeWeight * bevel + DetailWeight * luminance;
                }
            }

            // 3) Sobel 기울기 → 법선 → 양자화
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (Pixel(pixels, textureWidth, area, x, y).a <= 127)
                        continue;

                    float gx = (Height(heights, w, h, x + 1, y - 1) + 2f * Height(heights, w, h, x + 1, y) + Height(heights, w, h, x + 1, y + 1))
                             - (Height(heights, w, h, x - 1, y - 1) + 2f * Height(heights, w, h, x - 1, y) + Height(heights, w, h, x - 1, y + 1));
                    float gy = (Height(heights, w, h, x - 1, y + 1) + 2f * Height(heights, w, h, x, y + 1) + Height(heights, w, h, x + 1, y + 1))
                             - (Height(heights, w, h, x - 1, y - 1) + 2f * Height(heights, w, h, x, y - 1) + Height(heights, w, h, x + 1, y - 1));

                    // 높이가 올라가는 쪽의 반대가 표면이 바라보는 쪽
                    var normal = new Vector3(-gx * Strength * 0.25f, -gy * Strength * 0.25f, 1f).normalized;
                    var flat = new Vector2(Mathf.Round(normal.x * QuantizeSteps), Mathf.Round(normal.y * QuantizeSteps)) / QuantizeSteps;
                    flat = Vector2.ClampMagnitude(flat, 0.95f);
                    normal = new Vector3(flat.x, flat.y, Mathf.Sqrt(1f - flat.sqrMagnitude));

                    int index = (area.y + y) * textureWidth + area.x + x;
                    normals[index] = new Color32(ToByte(normal.x), ToByte(normal.y), ToByte(normal.z), 255);
                }
            }
        }

        static Color32 Pixel(Color32[] pixels, int textureWidth, RectInt area, int x, int y) =>
            pixels[(area.y + y) * textureWidth + area.x + x];

        static int Distance(int[] distance, int w, int h, int x, int y) =>
            x < 0 || y < 0 || x >= w || y >= h ? 0 : distance[y * w + x];

        static float Height(float[] heights, int w, int h, int x, int y) =>
            x < 0 || y < 0 || x >= w || y >= h ? 0f : heights[y * w + x];

        static byte ToByte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt((value * 0.5f + 0.5f) * 255f), 0, 255);

        // 노말맵은 색이 아니라 벡터 데이터 — sRGB 감마 보정을 끄고, 픽셀아트라 Point 필터·밉맵 없음
        static void ConfigureNormalImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void RegisterSecondaryTexture(TextureImporter importer, Texture2D normalMap)
        {
            var secondary = importer.secondarySpriteTextures?.Where(t => t.name != "_NormalMap").ToList() ?? new List<SecondarySpriteTexture>();
            secondary.Add(new SecondarySpriteTexture { name = "_NormalMap", texture = normalMap });
            importer.secondarySpriteTextures = secondary.ToArray();
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
