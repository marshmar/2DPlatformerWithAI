using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 2단계 테스트 맵 셋업: 충돌용 디버그 타일, Warped City 타일, Tile Palette 2개, 타일맵 구조를 갖춘 TestMap 씬.
    /// 이미 있는 타일·팔레트는 건드리지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class TestMapSetup
    {
        const string DebugArtFolder = "Assets/_Project/Art/Tiles/Debug";
        const string CollisionTileFolder = "Assets/_Project/Tiles/Collision";
        const string WarpedCityTileFolder = "Assets/_Project/Tiles/WarpedCity";
        const string PaletteFolder = "Assets/_Project/Tiles/Palettes";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";
        const string TilesetPath = "Assets/ThirdParty/warped city files/warped city files/Assets/ENVIRONMENT/tileset.png";
        const int TileSize = 16;

        // skyline 이미지 맨 윗줄 색. 하늘 스프라이트가 덮지 못하는 화면 위쪽을 카메라 배경색으로 이어 붙인다
        static readonly Color32 SkyTopColor = new(0, 0, 60, 255);

        [MenuItem("Tools/Project Setup/Step 2 (Test Map)", priority = 1)]
        static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestMapScenePath) != null &&
                !EditorUtility.DisplayDialog("TestMap 씬", "TestMap 씬이 이미 있습니다. 새로 만들면 칠해둔 맵이 사라집니다.", "새로 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SurfaceTile ground = EnsureSurfaceTile("Ground", false,
                new Color32(74, 82, 102, 255), new Color32(44, 49, 63, 255), (x, y) => false);
            SurfaceTile hookable = EnsureSurfaceTile("Hookable", true,
                new Color32(38, 166, 154, 255), new Color32(18, 96, 90, 255), (x, y) => x % 5 == 2 && y % 5 == 2);
            SurfaceTile hazard = EnsureSurfaceTile("Hazard", false,
                new Color32(204, 52, 64, 255), new Color32(120, 24, 36, 255), (x, y) => (x + y) % 6 < 2);

            EnsurePalette("Collision", new (Vector3Int, TileBase)[]
            {
                (new Vector3Int(0, 0, 0), ground),
                (new Vector3Int(1, 0, 0), hookable),
                (new Vector3Int(2, 0, 0), hazard),
            });
            EnsurePalette("WarpedCity", EnsureWarpedCityTiles());

            CreateTestMapScene();
        }

        static void CreateTestMapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene(Single) 이 쓰이지 않는 에셋을 언로드하므로, 앞에서 들고 있던 타일 참조는 여기서 다시 불러와야 한다
            var groundTile = AssetDatabase.LoadAssetAtPath<SurfaceTile>($"{CollisionTileFolder}/Ground.asset");

            Camera camera = ProjectSetup.CreateCamera();
            camera.backgroundColor = SkyTopColor;
            ProjectSetup.CreateGlobalLight();

            // 시작 바닥 윗면(y = -8)에 건물 밑동을 맞춘 배치. 하늘은 세로로 카메라에 붙여서 아래쪽 경계가 보이지 않게 한다
            Transform backgrounds = new GameObject("Backgrounds").transform;
            CreateParallaxLayer(backgrounds, "Sky", "skyline-a", "BG_Far", 0, new Vector2(0f, -3.75f), new Vector2(0.95f, 1f));
            CreateParallaxLayer(backgrounds, "Far Buildings", "buildings-bg", "BG_Far", 1, new Vector2(0f, -4.125f), new Vector2(0.75f, 0.85f));
            CreateParallaxLayer(backgrounds, "Near Buildings", "near-buildings-bg", "BG_Near", 0, new Vector2(0f, -1.5f), new Vector2(0.5f, 0.7f));

            var grid = new GameObject("Grid").AddComponent<Grid>();
            CreateTilemap(grid, "Visual_Back", "Tile_Back", 0);
            // Outlines: 타일 사이 내부 경계 없이 바깥 외곽선만 만들어 이음새에 걸리지 않는다
            Tilemap collision = CreateCollisionTilemap(grid, "Collision", "Ground", 100, false, CompositeCollider2D.GeometryType.Outlines);
            // Polygons: Outlines 트리거는 경계선만 감지해서 안쪽에 완전히 들어간 물체를 놓친다
            CreateCollisionTilemap(grid, "Hazard", "Hazard", 101, true, CompositeCollider2D.GeometryType.Polygons);
            CreateTilemap(grid, "Visual_Front", "Tile_Front", 0);

            // 콜라이더 생성 확인용 시작 바닥. 지우고 새로 칠해도 된다
            for (int x = -20; x < 20; x++)
                for (int y = -12; y < -8; y++)
                    collision.SetTile(new Vector3Int(x, y, 0), groundTile);

            EditorSceneManager.SaveScene(scene, TestMapScenePath);
            Debug.Log($"[Test Map] {TestMapScenePath} 생성 — Window > 2D > Tile Palette 에서 Collision / WarpedCity 팔레트로 칠하세요");
        }

        static void CreateParallaxLayer(Transform parent, string objectName, string spriteName, string sortingLayer,
            int sortingOrder, Vector2 position, Vector2 follow)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.BackgroundRoot + spriteName + ".png");

            var go = new GameObject(objectName);
            go.transform.SetParent(parent);
            go.transform.position = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            // 씬 뷰에서도 이어진 모습이 보이도록 미리 타일링 (런타임에는 ParallaxLayer 가 화면 폭에 맞춰 다시 늘린다)
            renderer.drawMode = SpriteDrawMode.Tiled;
            Vector2 size = sprite.bounds.size;
            renderer.size = new Vector2(size.x * Mathf.CeilToInt(60f / size.x), size.y);

            var parallax = new SerializedObject(go.AddComponent<ParallaxLayer>());
            parallax.FindProperty("follow").vector2Value = follow;
            parallax.ApplyModifiedPropertiesWithoutUndo();
        }

        static Tilemap CreateTilemap(Grid grid, string objectName, string sortingLayer, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(grid.transform);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        static Tilemap CreateCollisionTilemap(Grid grid, string objectName, string physicsLayer, int sortingOrder,
            bool isTrigger, CompositeCollider2D.GeometryType geometryType)
        {
            // 그래픽 타일 위에 반투명하게 겹쳐 보이도록 Tile_Front 맨 위에 그린다 (투명도는 Tilemap 의 Color 로 조절)
            Tilemap tilemap = CreateTilemap(grid, objectName, "Tile_Front", sortingOrder);
            tilemap.color = new Color(1f, 1f, 1f, 0.6f);
            GameObject go = tilemap.gameObject;
            go.layer = LayerMask.NameToLayer(physicsLayer);

            go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = go.AddComponent<CompositeCollider2D>(); // Rigidbody2D 가 같이 붙는다
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            composite.geometryType = geometryType;
            composite.isTrigger = isTrigger;
            return tilemap;
        }

        static SurfaceTile EnsureSurfaceTile(string name, bool hookable, Color32 fill, Color32 accent, Func<int, int, bool> pattern)
        {
            string tilePath = $"{CollisionTileFolder}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<SurfaceTile>(tilePath);
            if (tile != null)
                return tile;

            EnsureFolder(DebugArtFolder);
            EnsureFolder(CollisionTileFolder);
            string texturePath = $"{DebugArtFolder}/debug_{name.ToLowerInvariant()}.png";
            File.WriteAllBytes(texturePath, DrawDebugTile(fill, accent, pattern));
            // 새 텍스처라서 PixelArtTexturePostprocessor 가 PPU 16, Point 필터 등을 적용한다
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

            tile = ScriptableObject.CreateInstance<SurfaceTile>();
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, tilePath);

            var serialized = new SerializedObject(tile);
            serialized.FindProperty("hookable").boolValue = hookable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tile;
        }

        // 테두리 1px + 무늬. 색만이 아니라 무늬로도 구분되게 한다
        static byte[] DrawDebugTile(Color32 fill, Color32 accent, Func<int, int, bool> pattern)
        {
            var texture = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[TileSize * TileSize];
                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        bool edge = x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1;
                        pixels[y * TileSize + x] = edge || pattern(x, y) ? accent : fill;
                    }
                }
                texture.SetPixels32(pixels);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static List<(Vector3Int Cell, TileBase Tile)> EnsureWarpedCityTiles()
        {
            EnsureFolder(WarpedCityTileFolder);
            var placements = new List<(Vector3Int, TileBase)>();
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(TilesetPath).OfType<Sprite>())
            {
                string tilePath = $"{WarpedCityTileFolder}/{sprite.name}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None; // 그래픽 전용. 충돌은 Collision 타일맵이 담당
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                // 팔레트에서 원본 시트와 같은 모양으로 보이도록 시트 안 위치를 그대로 셀 좌표로 쓴다
                var cell = new Vector3Int((int)sprite.rect.x / TileSize, (int)sprite.rect.y / TileSize, 0);
                placements.Add((cell, tile));
            }
            return placements;
        }

        internal static void EnsurePalette(string name, IEnumerable<(Vector3Int Cell, TileBase Tile)> placements)
        {
            string path = $"{PaletteFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return; // 직접 편집했을 수 있으므로 덮어쓰지 않는다

            EnsureFolder(PaletteFolder);
            GridPaletteUtility.CreateNewPalette(PaletteFolder, name, GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual, new Vector3(1f, 1f, 0f), GridLayout.CellSwizzle.XYZ);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Tilemap tilemap = root.GetComponentInChildren<Tilemap>();
                foreach ((Vector3Int cell, TileBase tile) in placements)
                    tilemap.SetTile(cell, tile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
