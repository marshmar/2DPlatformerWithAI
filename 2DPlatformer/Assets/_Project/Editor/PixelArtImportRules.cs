using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 픽셀아트 텍스처 임포트 규칙.
    /// 새로 들어온 텍스처는 임포트 시 자동 적용되고, 기존 텍스처는 Tools/Project Setup 메뉴로 일괄 적용한다.
    /// </summary>
    public static class PixelArtImportRules
    {
        public const int PixelsPerUnit = PixelGrid.PixelsPerUnit;

        static readonly string[] ManagedRoots = { "Assets/ThirdParty/", "Assets/_Project/Art/" };

        // 경로에 Match 가 포함된 텍스처를 그리드로 자른다. 위에서부터 처음 맞는 규칙 하나만 적용.
        static readonly SliceRule[] SliceRules =
        {
            // Craftpix 캐릭터: 48x48 프레임. 세 캐릭터 모두 발은 셀 바닥, 몸 중심은 x=12px 에 있다.
            new("Free 3 Cyberpunk Sprites Pixel Art/", new Vector2Int(48, 48), new Vector2Int(12, 0)),
            // Warped City 타일셋: 16x16, 빈 셀은 건너뛴다.
            new("ENVIRONMENT/tileset.png", new Vector2Int(16, 16), new Vector2Int(8, 8)),
            // 레벨 생성기가 만드는 지형 타일 (LevelBuilder)
            new("_Project/Art/Tiles/terrain.png", new Vector2Int(16, 16), new Vector2Int(8, 8)),
        };

        sealed class SliceRule
        {
            public readonly string Match;
            public readonly Vector2Int CellSize;
            public readonly Vector2Int PivotPixel; // 셀 왼쪽 아래 기준

            public SliceRule(string match, Vector2Int cellSize, Vector2Int pivotPixel)
            {
                Match = match;
                CellSize = cellSize;
                PivotPixel = pivotPixel;
            }
        }

        // 생성된 노말맵은 스프라이트가 아니라 데이터 텍스처라서 규칙에서 뺀다 (NormalMapGenerator 가 직접 설정)
        public static bool IsManaged(string assetPath) =>
            assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) &&
            ManagedRoots.Any(assetPath.StartsWith) &&
            !assetPath.Contains("/Generated/");

        public static void ApplyToAll()
        {
            string[] folders = ManagedRoots.Select(root => root.TrimEnd('/')).Where(AssetDatabase.IsValidFolder).ToArray();
            string[] paths = AssetDatabase.FindAssets("t:Texture2D", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsManaged)
                .ToArray();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Configure(importer);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[Import Rules] 텍스처 {paths.Length}개에 픽셀아트 규칙 적용");
        }

        public static void Configure(TextureImporter importer)
        {
            string path = importer.assetPath;
            SliceRule rule = SliceRules.FirstOrDefault(r => path.Contains(r.Match));
            if (!TryLoadPixels(path, out Color32[] pixels, out int width, out int height))
            {
                Debug.LogWarning($"[Import Rules] PNG 를 읽지 못해 건너뜀: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = rule != null ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // 림 라이트·아웃라인 셰이더는 실루엣 바깥 픽셀까지 그려야 하므로, 투명 영역을 잘라내는 Tight 대신 Full Rect
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (rule == null)
            {
                // 홀수 크기의 정중앙 피벗은 반 픽셀에 걸려 픽셀 그리드와 어긋나므로 내림한 픽셀 위치를 쓴다
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(width / 2 / (float)width, height / 2 / (float)height);
            }
            importer.SetTextureSettings(settings);

            if (rule != null)
                ApplyGridSlices(importer, rule, pixels, width, height);
        }

        static void ApplyGridSlices(TextureImporter importer, SliceRule rule, Color32[] pixels, int width, int height)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // 같은 이름의 기존 스프라이트 ID 를 재사용해야 다시 적용해도 애니메이션·타일 참조가 끊기지 않는다
            Dictionary<string, GUID> existingIds = provider.GetSpriteRects()
                .GroupBy(r => r.name)
                .ToDictionary(g => g.Key, g => g.First().spriteID);

            string baseName = Path.GetFileNameWithoutExtension(importer.assetPath);
            Vector2Int cell = rule.CellSize;
            var pivot = new Vector2((float)rule.PivotPixel.x / cell.x, (float)rule.PivotPixel.y / cell.y);
            var rects = new List<SpriteRect>();

            // Sprite Editor 의 Grid 슬라이스와 같은 순서: 위 줄부터 왼쪽 → 오른쪽
            for (int row = 0; row < height / cell.y; row++)
            {
                for (int col = 0; col < width / cell.x; col++)
                {
                    var area = new RectInt(col * cell.x, height - (row + 1) * cell.y, cell.x, cell.y);
                    if (IsEmpty(pixels, width, area))
                        continue;

                    string name = $"{baseName}_{rects.Count}";
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                        rect = new Rect(area.x, area.y, area.width, area.height),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
        }

        static bool IsEmpty(Color32[] pixels, int textureWidth, RectInt area)
        {
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                    if (pixels[y * textureWidth + x].a > 0)
                        return false;
            return true;
        }

        // 임포터 설정과 무관하게 원본 PNG 를 직접 디코딩 (픽셀 배열은 왼쪽 아래부터)
        static bool TryLoadPixels(string assetPath, out Color32[] pixels, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(assetPath)))
                {
                    pixels = null;
                    width = height = 0;
                    return false;
                }
                pixels = texture.GetPixels32();
                width = texture.width;
                height = texture.height;
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    sealed class PixelArtTexturePostprocessor : AssetPostprocessor
    {
        // 처음 들어오는 텍스처(.meta 없음)에만 적용 — Inspector 에서 손으로 바꾼 값이 재임포트 때 덮어써지지 않게
        void OnPreprocessTexture()
        {
            if (assetImporter.importSettingsMissing && PixelArtImportRules.IsManaged(assetPath))
                PixelArtImportRules.Configure((TextureImporter)assetImporter);
        }
    }
}
