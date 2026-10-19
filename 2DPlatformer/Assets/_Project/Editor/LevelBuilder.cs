using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 27단계: 텍스트 레이아웃(Levels/LevelXX.txt)으로 레벨 씬을 만든다. Levels 폴더의 Level*.txt 를 전부 만든다.
    ///   - 충돌: # / H / = → Collision 타일맵, ^ → Hazard 타일맵. 칠한 직후 콜라이더를 즉시 다시 만든다(아래 RebuildColliders)
    ///   - 그래픽: 지형 오토타일(주변 칸을 보고 윗면·측면·모서리·채움 선택) + 큰 지형 덩어리에 건물 파사드 스탬프 + 옥상 소품
    ///   - 배치: 플레이어·적·네온 라이트·간판·물웅덩이·카메라 방·체크포인트·데이터 칩·아레나 게이트·출구·조작 안내
    ///   - 섹터 목록(Resources/LevelCatalog)과 Build Settings 순서(Title → Level01 → ...)를 갱신한다
    /// TestMap 을 복사해서 시작하므로 카메라·볼륨·배경·비·전역 조명 설정을 그대로 물려받는다.
    /// 레이아웃이 원본이라 다시 실행하면 레벨 씬을 새로 만든다. 타이틀 배경(Levels/Title.txt)도 같은 방식으로 만든다 (28단계).
    /// </summary>
    public static class LevelBuilder
    {
        const string LevelFolder = "Assets/_Project/Levels";
        const string SceneFolder = "Assets/_Project/Scenes";
        const string TitleLayoutPath = LevelFolder + "/Title.txt";
        public const string TitleScenePath = SceneFolder + "/Title.unity";
        const string TestMapScenePath = SceneFolder + "/TestMap.unity";
        const string CatalogPath = "Assets/_Project/Resources/LevelCatalog.asset";
        const string TerrainTexturePath = "Assets/_Project/Art/Tiles/terrain.png";
        const string TerrainTileFolder = "Assets/_Project/Tiles/Terrain";
        const string WarpedCityTileFolder = "Assets/_Project/Tiles/WarpedCity";
        const string CollisionTileFolder = "Assets/_Project/Tiles/Collision";
        const string TerrainMaterialPath = "Assets/_Project/Materials/Terrain.mat";
        const string FacadeMaterialPath = "Assets/_Project/Materials/Facade.mat";
        const string FXMaterialFolder = "Assets/_Project/Materials/FX";
        const string PuddleMaterialPath = FXMaterialFolder + "/Puddle.mat";
        const string BeaconMaterialPath = FXMaterialFolder + "/Beacon.mat";
        const string ExitBeaconMaterialPath = FXMaterialFolder + "/BeaconExit.mat";
        const string GateMaterialPath = FXMaterialFolder + "/EnergyGate.mat";
        const string ChipMaterialPath = FXMaterialFolder + "/DataChip.mat";
        const string SquarePath = "Assets/_Project/Art/FX/fx_square.png";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemies";
        const string PropsRoot = "Assets/ThirdParty/warped city files/warped city files/Assets/ENVIRONMENT/props/";

        static readonly Color Magenta = new(1f, 0.25f, 0.85f);
        static readonly Color Cyan = new(0.2f, 0.9f, 1f);
        static readonly Color Amber = new(1f, 0.6f, 0.2f);
        static readonly Color PortalGreen = new(0.4f, 1f, 0.55f);
        static readonly Color GateMagenta = new(1f, 0.2f, 0.55f);

        // 건물 파사드 스탬프: Warped City 타일셋의 (열, 행) 왼쪽 위 기준, 폭·높이(칸)
        static readonly (string Name, int Col, int Row, int Width, int Height, int Weight)[] Stamps =
        {
            ("Window", 3, 1, 3, 4, 5),
            ("Shutter", 14, 6, 3, 4, 3),
            ("Garage", 14, 1, 3, 4, 2),
            ("WallPanel", 9, 1, 2, 4, 3),
            ("Pillar", 1, 1, 1, 3, 3),
            ("AirConditioner", 11, 9, 2, 2, 4),
            ("Machinery", 10, 6, 3, 2, 2),
            ("Vent", 18, 6, 1, 2, 3),
            ("Terminal", 20, 6, 1, 2, 2),
            ("Pipes", 22, 6, 1, 2, 2),
        };
        static readonly (int Col, int Row, int Width, int Height) Portal = (14, 11, 3, 4);

        sealed class Hint
        {
            public RectInt Area;
            public string Text;
        }

        sealed class Layout
        {
            public int Width;
            public int Height;
            public char[,] Cells;
            public Vector2Int Origin;
            public string Code = "SECTOR";
            public string Title = "";
            public Color? Ambient;
            public Color? BackgroundAmbient;
            public readonly List<RectInt> Rooms = new();
            public readonly List<Hint> Hints = new();

            public char this[int x, int y] => x < 0 || y < 0 || x >= Width || y >= Height ? '.' : Cells[x, y];

            public static Layout Parse(string[] lines)
            {
                var layout = new Layout();
                var rows = new List<string>();
                foreach (string raw in lines)
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length == 0 || line.StartsWith("//"))
                        continue;
                    if (line.StartsWith("@"))
                    {
                        ParseDirective(layout, line);
                        continue;
                    }
                    rows.Add(line);
                }

                layout.Height = rows.Count;
                layout.Width = rows.Max(r => r.Length);
                layout.Cells = new char[layout.Width, layout.Height];
                for (int row = 0; row < rows.Count; row++)
                    for (int x = 0; x < layout.Width; x++)
                        layout.Cells[x, layout.Height - 1 - row] = x < rows[row].Length ? rows[row][x] : '.';
                return layout;
            }

            static void ParseDirective(Layout layout, string line)
            {
                int space = line.IndexOf(' ');
                string name = space < 0 ? line : line.Substring(0, space);
                string rest = space < 0 ? "" : line.Substring(space + 1).Trim();
                float[] Numbers(int count) => rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Take(count).Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

                switch (name)
                {
                    case "@origin":
                        float[] origin = Numbers(2);
                        layout.Origin = new Vector2Int((int)origin[0], (int)origin[1]);
                        break;
                    case "@room":
                        float[] r = Numbers(4);
                        layout.Rooms.Add(new RectInt((int)r[0], (int)r[1], (int)(r[2] - r[0]), (int)(r[3] - r[1])));
                        break;
                    case "@name":
                        layout.Code = rest;
                        break;
                    case "@subtitle":
                        layout.Title = rest;
                        break;
                    case "@ambient":
                        float[] c = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        layout.Ambient = new Color(c[0], c[1], c[2]);
                        if (c.Length >= 6)
                            layout.BackgroundAmbient = new Color(c[3], c[4], c[5]);
                        break;
                    case "@hint":
                        // @hint x0 y0 x1 y1 문구
                        string[] parts = rest.Split(new[] { ' ', '\t' }, 5, StringSplitOptions.RemoveEmptyEntries);
                        int[] h = parts.Take(4).Select(int.Parse).ToArray();
                        layout.Hints.Add(new Hint { Area = new RectInt(h[0], h[1], h[2] - h[0], h[3] - h[1]), Text = parts.Length > 4 ? parts[4] : "" });
                        break;
                    default:
                        Debug.LogWarning($"[Level] 알 수 없는 지시어: {line}");
                        break;
                }
            }

            public bool IsTerrain(int x, int y) => this[x, y] is '#' or 'H' or '=';
            public Vector2 World(float x, float y) => new(x + Origin.x, y + Origin.y);

            /// <summary>칸 중심이 들어 있는 카메라 방 번호 (없으면 -1)</summary>
            public int RoomOf(int x, int y)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                for (int i = 0; i < Rooms.Count; i++)
                {
                    RectInt room = Rooms[i];
                    if (point.x > room.xMin && point.x < room.xMax && point.y > room.yMin && point.y < room.yMax)
                        return i;
                }
                return -1;
            }
        }

        // ================= 메뉴 =================

        [MenuItem("Tools/Project Setup/Step 27 (Build Levels)", priority = 26)]
        public static void BuildAll()
        {
            // 8~26단계가 안 돼 있으면(플레이어에 갈고리가 없으면) 먼저 전부 실행한다
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null || playerPrefab.GetComponent<GrappleHook>() == null)
                GameplaySetup.RunAll();

            string[] layouts = LevelLayouts();
            if (layouts.Length == 0)
            {
                Debug.LogError($"[Level] {LevelFolder} 에 Level*.txt 가 없습니다");
                return;
            }
            if (!Application.isBatchMode && layouts.Any(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePathFor(p)) != null) &&
                !EditorUtility.DisplayDialog("Build Levels", $"레이아웃으로 레벨 씬 {layouts.Length}개를 다시 만듭니다. 씬에서 직접 고친 내용은 사라집니다.", "다시 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            PrepareAssets();
            var entries = new List<LevelCatalog.Entry>();
            foreach (string layoutPath in layouts)
                entries.Add(BuildScene(layoutPath, ScenePathFor(layoutPath), titleMode: false));

            WriteCatalog(entries);
            UpdateBuildSettings();
            Debug.Log($"[Level] 레벨 {entries.Count}개 생성 완료: {string.Join(", ", entries.Select(e => $"{e.scene}({e.code})"))}");
        }

        /// <summary>28단계에서 부른다: Levels/Title.txt 로 타이틀 배경 씬을 만든다.</summary>
        public static void BuildTitle()
        {
            if (!File.Exists(TitleLayoutPath))
            {
                Debug.LogError($"[Level] {TitleLayoutPath} 가 없습니다");
                return;
            }
            PrepareAssets();
            BuildScene(TitleLayoutPath, TitleScenePath, titleMode: true);
            UpdateBuildSettings();
        }

        static string[] LevelLayouts() =>
            Directory.GetFiles(LevelFolder, "Level*.txt").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        static string ScenePathFor(string layoutPath) => $"{SceneFolder}/{Path.GetFileNameWithoutExtension(layoutPath)}.unity";

        static void PrepareAssets()
        {
            EnsureTerrainTiles();
            EnsureMaterials();
            UISetup.EnsureMaterials();
            AssetDatabase.SaveAssets();
        }

        // ================= 씬 하나 만들기 =================

        static LevelCatalog.Entry BuildScene(string layoutPath, string scenePath, bool titleMode)
        {
            Layout layout = Layout.Parse(File.ReadAllLines(layoutPath));

            AssetDatabase.DeleteAsset(scenePath);
            AssetDatabase.CopyAsset(TestMapScenePath, scenePath);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 씬을 열면 쓰이지 않는 에셋이 언로드되므로 타일·머티리얼은 씬을 연 뒤에 다시 불러온다
            Tile[] terrain = LoadTerrainTiles();
            ClearScene();
            var grid = GameObject.Find("Grid").GetComponent<Grid>();
            grid.transform.position = new Vector3(layout.Origin.x, layout.Origin.y, 0f);

            PaintTerrain(layout, grid, terrain);
            RebuildColliders(grid, layout, scenePath);
            PlaceStamps(layout, grid);
            PlaceRooftopProps(layout);
            List<(Enemy Enemy, int Room)> enemies = PlaceMarkers(layout, titleMode);
            PlacePuddles(layout);
            PlaceRooms(layout);
            PlaceGates(layout, enemies);
            PlaceHints(layout);
            ApplyAmbient(layout);
            LockBackgroundsVertically();
            int chips = Object.FindObjectsByType<DataChip>(FindObjectsSortMode.None).Length;

            if (titleMode)
                ConvertToTitle(layout);
            else
                PlaceLevelInfo(layout);

            EditorSceneManager.SaveScene(scene);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            Debug.Log($"[Level] {scenePath} 생성 ({layout.Width}x{layout.Height} 칸, 방 {layout.Rooms.Count}개, 적 {enemies.Count}, 데이터 칩 {chips})");
            return new LevelCatalog.Entry { scene = sceneName, code = layout.Code, title = layout.Title, chips = chips };
        }

        // ----- 지형 -----

        static void PaintTerrain(Layout layout, Grid grid, Tile[] terrain)
        {
            Tilemap collision = grid.transform.Find("Collision").GetComponent<Tilemap>();
            Tilemap hazard = grid.transform.Find("Hazard").GetComponent<Tilemap>();
            Tilemap visual = grid.transform.Find("Visual_Back").GetComponent<Tilemap>();
            var ground = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionTileFolder + "/Ground.asset");
            var hookable = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionTileFolder + "/Hookable.asset");
            var danger = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionTileFolder + "/Hazard.asset");

            // 충돌용 디버그 타일은 숨긴다 — 이제 그래픽 타일이 대신한다 (갈고리 하이라이트는 Terrain 머티리얼이 그린다)
            collision.GetComponent<TilemapRenderer>().enabled = false;
            hazard.GetComponent<TilemapRenderer>().enabled = false;
            visual.GetComponent<TilemapRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    char c = layout[x, y];
                    var cell = new Vector3Int(x, y, 0);
                    switch (c)
                    {
                        case '#':
                            collision.SetTile(cell, ground);
                            visual.SetTile(cell, terrain[(int)TerrainKind(layout, x, y)]);
                            break;
                        case 'H':
                            collision.SetTile(cell, hookable);
                            bool covered = layout.IsTerrain(x, y + 1);
                            visual.SetTile(cell, terrain[(int)(covered ? TerrainTileGenerator.Kind.HookFill : TerrainTileGenerator.Kind.HookTop)]);
                            break;
                        case '=':
                            collision.SetTile(cell, hookable);
                            visual.SetTile(cell, terrain[(int)TerrainTileGenerator.Kind.Beam]);
                            break;
                        case '^':
                            hazard.SetTile(cell, danger);
                            visual.SetTile(cell, terrain[(int)TerrainTileGenerator.Kind.Spikes]);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 타일을 칠한 직후 콜라이더를 즉시 다시 만든다.
        /// TilemapCollider2D 는 타일 변경을 다음 에디터 갱신 때 몰아서 처리하므로, 칠하자마자 씬을 저장하면
        /// CompositeCollider2D 에 복사해 온 TestMap 의 옛 형상이 그대로 저장된다 — 플레이하면 보이는 바닥과 실제 바닥이 달라서
        /// 시작하자마자 땅을 뚫고 떨어지던 버그의 원인. 저장 전에 변경을 처리하고 합성 형상을 새로 만든 뒤 크기를 검사한다.
        /// </summary>
        static void RebuildColliders(Grid grid, Layout layout, string scenePath)
        {
            foreach (var tilemapCollider in grid.GetComponentsInChildren<TilemapCollider2D>(true))
            {
                tilemapCollider.GetComponent<Tilemap>().CompressBounds();
                tilemapCollider.ProcessTilemapChanges();
                var composite = tilemapCollider.GetComponent<CompositeCollider2D>();
                if (composite != null)
                    composite.GenerateGeometry();
            }
            Physics2D.SyncTransforms();

            // 검사: 합성 콜라이더가 레이아웃의 지형 범위를 덮는가
            var solid = grid.transform.Find("Collision").GetComponent<CompositeCollider2D>();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                    if (layout.IsTerrain(x, y))
                    {
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x + 1);
                        maxY = Mathf.Max(maxY, y + 1);
                    }
            if (minX == int.MaxValue)
                return;
            Bounds bounds = solid.bounds;
            Vector2 expectedMin = layout.World(minX, minY);
            Vector2 expectedMax = layout.World(maxX, maxY);
            bool matches = solid.pathCount > 0 &&
                           Vector2.Distance(bounds.min, expectedMin) < 0.1f &&
                           Vector2.Distance(bounds.max, expectedMax) < 0.1f;
            if (!matches)
                Debug.LogError($"[Level] {scenePath}: 충돌 형상이 레이아웃과 다릅니다 (콜라이더 {bounds.min}~{bounds.max}, 레이아웃 {expectedMin}~{expectedMax}, 경로 {solid.pathCount}개)");
        }

        // 오토타일: 위가 비었으면 윗면(양옆도 비었으면 모서리), 옆이 비었으면 측면, 아니면 채움(위치 해시로 변형 섞기)
        static TerrainTileGenerator.Kind TerrainKind(Layout layout, int x, int y)
        {
            bool up = layout.IsTerrain(x, y + 1);
            bool left = x == 0 || layout.IsTerrain(x - 1, y);
            bool right = x == layout.Width - 1 || layout.IsTerrain(x + 1, y);
            if (!up)
                return !left ? TerrainTileGenerator.Kind.TopLeft : !right ? TerrainTileGenerator.Kind.TopRight : TerrainTileGenerator.Kind.Top;
            if (!left)
                return TerrainTileGenerator.Kind.Left;
            if (!right)
                return TerrainTileGenerator.Kind.Right;
            int hash = (x * 73856093) ^ (y * 19349663);
            int variant = Math.Abs(hash) % 10;
            return variant == 0 ? TerrainTileGenerator.Kind.FillVent : variant == 1 ? TerrainTileGenerator.Kind.FillSeam : TerrainTileGenerator.Kind.Fill;
        }

        // 큰 지형 덩어리 안쪽(윗면·측면에서 한 칸 이상 떨어진 곳)에 건물 파사드를 찍어서 지형이 "건물"로 읽히게 한다
        static void PlaceStamps(Layout layout, Grid grid)
        {
            Tilemap detail = EnsureDetailTilemap(grid);
            Dictionary<(int, int), TileBase> source = WarpedCityTilesByCell();
            var occupied = new bool[layout.Width, layout.Height];
            var random = new System.Random(20261013);
            int totalWeight = Stamps.Sum(s => s.Weight);

            for (int y = layout.Height - 2; y >= 1; y--)
            {
                for (int x = 1; x < layout.Width - 1; x++)
                {
                    if (layout[x, y] != '#' || random.NextDouble() > 0.22)
                        continue;

                    int pick = random.Next(totalWeight);
                    var stamp = Stamps.First(s => (pick -= s.Weight) < 0);
                    if (!CanStamp(layout, occupied, x, y, stamp.Width, stamp.Height))
                        continue;

                    for (int i = 0; i < stamp.Width; i++)
                        for (int j = 0; j < stamp.Height; j++)
                            if (source.TryGetValue((stamp.Col + i, stamp.Row + j), out TileBase tile))
                                detail.SetTile(new Vector3Int(x + i, y - j, 0), tile);

                    // 스탬프끼리 붙지 않게 한 칸 여유까지 점유
                    for (int i = -1; i <= stamp.Width; i++)
                        for (int j = -1; j <= stamp.Height; j++)
                        {
                            int cx = x + i, cy = y - j;
                            if (cx >= 0 && cy >= 0 && cx < layout.Width && cy < layout.Height)
                                occupied[cx, cy] = true;
                        }
                }
            }
        }

        // (x, y) 가 스탬프의 왼쪽 위. 스탬프 영역과 그 둘레 한 칸이 모두 '#' 이어야 한다 (지형 가장자리에 걸치지 않게)
        static bool CanStamp(Layout layout, bool[,] occupied, int x, int y, int width, int height)
        {
            for (int i = -1; i <= width; i++)
            {
                for (int j = -1; j <= height; j++)
                {
                    int cx = x + i, cy = y - j;
                    if (layout[cx, cy] != '#')
                        return false;
                    bool inside = i >= 0 && i < width && j >= 0 && j < height;
                    if (inside && occupied[cx, cy])
                        return false;
                }
            }
            return true;
        }

        static Tilemap EnsureDetailTilemap(Grid grid)
        {
            Transform existing = grid.transform.Find("Visual_Detail");
            if (existing != null)
                return existing.GetComponent<Tilemap>();
            var go = new GameObject("Visual_Detail");
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Props"; // 지형(Tile_Back) 위, 캐릭터 뒤
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FacadeMaterialPath);
            return tilemap;
        }

        // 타일셋 칸 (열, 행[위에서부터]) → 2단계에서 만든 WarpedCity 타일 에셋
        static Dictionary<(int, int), TileBase> WarpedCityTilesByCell()
        {
            var map = new Dictionary<(int, int), TileBase>();
            foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { WarpedCityTileFolder }))
            {
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
                if (tile == null || tile.sprite == null)
                    continue;
                Rect rect = tile.sprite.rect;
                int column = (int)rect.x / 16;
                int row = (tile.sprite.texture.height - (int)rect.y) / 16 - 1;
                map[(column, row)] = tile;
            }
            return map;
        }

        // ----- 소품·배치 -----

        // 옥상 소품: 위로 충분히 비어 있는 지형 윗면에 안테나·제어반·간판·모니터를 띄엄띄엄
        static void PlaceRooftopProps(Layout layout)
        {
            Transform parent = new GameObject("Props").transform;
            var facade = AssetDatabase.LoadAssetAtPath<Material>(FacadeMaterialPath);
            var random = new System.Random(7);
            (string File, int Clearance)[] props =
            {
                ("antenna.png", 7), ("control-box-1.png", 3), ("control-box-2.png", 3), ("control-box-3.png", 3),
                ("banner-small.png", 4), ("hotel-sign.png", 4), ("banner-open.png", 4),
            };
            Sprite[] monitor = Enumerable.Range(1, 4).Select(i => PropSprite($"monitorface/monitor-face-{i}.png")).ToArray();
            int lastX = -10;

            for (int x = 3; x < layout.Width - 3; x++)
            {
                for (int y = 0; y < layout.Height - 1; y++)
                {
                    if (layout[x, y] != '#' || layout[x, y + 1] != '.' || x - lastX < 6 || random.NextDouble() > 0.12)
                        continue;

                    var (file, clearance) = props[random.Next(props.Length)];
                    bool open = Enumerable.Range(1, clearance).All(dy => layout[x, y + dy] == '.' && layout[x + 1, y + dy] == '.' && layout[x - 1, y + dy] == '.');
                    if (!open)
                        continue;

                    Sprite sprite = PropSprite(file);
                    var go = new GameObject(Path.GetFileNameWithoutExtension(file));
                    go.transform.SetParent(parent, false);
                    // 피벗이 가운데라 높이의 절반만큼 올려서 바닥에 세운다 (픽셀 격자에 맞춤)
                    float halfHeight = Mathf.Floor(sprite.rect.height / 2f) / PixelGrid.PixelsPerUnit;
                    go.transform.position = layout.World(x + 0.5f, y + 1 + halfHeight);
                    var spriteRenderer = go.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.sharedMaterial = facade;
                    spriteRenderer.sortingLayerName = "Props";
                    spriteRenderer.sortingOrder = -5;

                    // 가끔 깜빡이는 모니터 얼굴을 위에 얹는다
                    if (file.StartsWith("control-box") && random.NextDouble() < 0.5)
                    {
                        var face = new GameObject("monitor-face");
                        face.transform.SetParent(go.transform, false);
                        face.transform.localPosition = new Vector3(0f, halfHeight + 0.6f, 0f);
                        var faceRenderer = face.AddComponent<SpriteRenderer>();
                        faceRenderer.sprite = monitor[0];
                        faceRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.NeonMaterialPath);
                        faceRenderer.sortingLayerName = "Props";
                        faceRenderer.sortingOrder = -4;
                        face.AddComponent<SpriteAnimator>().SetClips(new[]
                        {
                            new SpriteAnimator.Clip { name = "Loop", frames = monitor, fps = 4f, loop = true },
                        }, useUnscaledTime: true, randomStart: true);
                    }
                    lastX = x;
                    break;
                }
            }
        }

        static List<(Enemy, int)> PlaceMarkers(Layout layout, bool titleMode)
        {
            var placed = new List<(Enemy, int)>();
            Transform enemies = new GameObject("Enemies").transform;
            Transform lights = new GameObject("Lights").transform;
            Transform banners = GameObject.Find("Props").transform;
            Transform checkpoints = new GameObject("Checkpoints").transform;
            Transform pickups = new GameObject("Data Chips").transform;
            var punk = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/Punk.prefab");
            var turret = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/Turret.prefab");
            var drone = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/Drone.prefab");
            Sprite[] neonFrames = Enumerable.Range(1, 4).Select(i => PropSprite($"banner-neon/banner-neon-{i}.png")).ToArray();
            var neon = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.NeonMaterialPath);
            Tilemap detail = GameObject.Find("Grid/Visual_Detail").GetComponent<Tilemap>();
            Dictionary<(int, int), TileBase> source = WarpedCityTilesByCell();
            int lightIndex = 0;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    char c = layout[x, y];
                    Vector2 feet = layout.World(x + 0.5f, y);
                    Vector2 center = layout.World(x + 0.5f, y + 0.5f);
                    switch (c)
                    {
                        case 'P':
                            var player = Object.FindAnyObjectByType<PlayerController>();
                            player.transform.position = feet;
                            // 맵 아래로 떨어지면 안전한 발판으로 되돌린다 (맵 바닥보다 조금 아래)
                            var playerSettings = new SerializedObject(player);
                            playerSettings.FindProperty("fallOutY").floatValue = layout.Origin.y - 12f;
                            playerSettings.ApplyModifiedPropertiesWithoutUndo();
                            break;
                        case 'E': placed.Add((Spawn(punk, feet, enemies), layout.RoomOf(x, y))); break;
                        case 'T': placed.Add((Spawn(turret, feet, enemies), layout.RoomOf(x, y))); break;
                        case 'D': placed.Add((Spawn(drone, center, enemies), layout.RoomOf(x, y))); break;
                        case 'm':
                        case 'c':
                        case 'a':
                            Color color = c == 'm' ? Magenta : c == 'c' ? Cyan : Amber;
                            var light = RenderingSetup.CreatePointLight(lights, "Neon Light", center, color, 0.6f, 6.5f);
                            light.blendStyleIndex = 1; // 더하기 — 어두운 픽셀에도 색이 번지게
                            if (lightIndex++ % 3 == 1)
                                light.gameObject.AddComponent<NeonFlicker>();
                            break;
                        case 'n':
                            var banner = new GameObject("Neon Banner");
                            banner.transform.SetParent(banners, false);
                            banner.transform.position = center;
                            var bannerRenderer = banner.AddComponent<SpriteRenderer>();
                            bannerRenderer.sprite = neonFrames[0];
                            bannerRenderer.sharedMaterial = neon;
                            bannerRenderer.sortingLayerName = "Props";
                            banner.AddComponent<SpriteAnimator>().SetClips(new[]
                            {
                                new SpriteAnimator.Clip { name = "Loop", frames = neonFrames, fps = 8f, loop = true },
                            }, useUnscaledTime: true, randomStart: true);
                            break;
                        case 'G':
                            PlaceExit(layout, x, y, lights, detail, source, titleMode);
                            break;
                        case 'K':
                            PlaceCheckpoint(checkpoints, feet);
                            break;
                        case 'o':
                            PlaceChip(pickups, center);
                            break;
                    }
                }
            }
            return placed;
        }

        static Enemy Spawn(GameObject prefab, Vector2 position, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            // 레벨의 적은 다시 살아나지 않는다 (테스트 맵에서는 4초 뒤 다시 나타난다) — 아레나 게이트가 "모두 쓰러짐"을 셀 수 있게
            var enemy = instance.GetComponent<Enemy>();
            var settings = new SerializedObject(enemy);
            settings.FindProperty("respawnDelay").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        // 출구 포털: 이 칸이 왼쪽 아래인 3x4 스탬프 + 초록 빛 + 초록 빛기둥 + 들어가면 클리어되는 트리거
        static void PlaceExit(Layout layout, int x, int y, Transform lights, Tilemap detail, Dictionary<(int, int), TileBase> source, bool titleMode)
        {
            for (int i = 0; i < Portal.Width; i++)
                for (int j = 0; j < Portal.Height; j++)
                    if (source.TryGetValue((Portal.Col + i, Portal.Row + j), out TileBase tile))
                        detail.SetTile(new Vector3Int(x + i, y + Portal.Height - 1 - j, 0), tile);
            var portalLight = RenderingSetup.CreatePointLight(lights, "Portal Light", layout.World(x + 1.5f, y + 2f), PortalGreen, 0.9f, 6f);
            portalLight.blendStyleIndex = 1;
            if (titleMode)
                return;

            var exit = new GameObject("Exit") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            exit.transform.position = layout.World(x + 1.5f, y);
            var box = exit.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2.5f, 3.5f);
            box.offset = new Vector2(0f, 1.75f);
            exit.AddComponent<LevelExit>();
            CreateBeam(exit.transform, ExitBeaconMaterialPath, new Vector2(2f, 6f), 4);
        }

        static SpriteRenderer CreateBeam(Transform parent, string materialPath, Vector2 size, int sortingOrder)
        {
            var beam = new GameObject("Beam");
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            beam.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = beam.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            renderer.sortingLayerName = "Props";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static void PlaceCheckpoint(Transform parent, Vector2 feet)
        {
            var go = new GameObject("Checkpoint") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 3f);
            box.offset = new Vector2(0f, 1.5f);
            SpriteRenderer beam = CreateBeam(go.transform, BeaconMaterialPath, new Vector2(1.5f, 5f), 5);
            var light = RenderingSetup.CreatePointLight(go.transform, "Light", new Vector3(0f, 1.5f, 0f), Magenta, 0.4f, 5f);
            light.blendStyleIndex = 1;
            go.AddComponent<Checkpoint>().Bind(beam, light);
        }

        static void PlaceChip(Transform parent, Vector2 center)
        {
            var go = new GameObject("Data Chip") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ChipMaterialPath);
            renderer.sortingLayerName = "FX";
            go.AddComponent<CircleCollider2D>().radius = 0.6f;
            go.AddComponent<DataChip>();
        }

        // '|' 가 세로로 이어진 구간마다 게이트 하나. 같은 카메라 방에 배치된 적이 모두 쓰러지면 열린다
        static void PlaceGates(Layout layout, List<(Enemy Enemy, int Room)> enemies)
        {
            Transform parent = null;
            for (int x = 0; x < layout.Width; x++)
            {
                for (int y = 0; y < layout.Height; y++)
                {
                    if (layout[x, y] != '|' || layout[x, y - 1] == '|')
                        continue;
                    int height = 0;
                    while (layout[x, y + height] == '|')
                        height++;

                    if (parent == null)
                        parent = new GameObject("Gates").transform;
                    int room = layout.RoomOf(x, y);
                    Enemy[] guarded = enemies.Where(e => e.Room == room && room >= 0).Select(e => e.Enemy).ToArray();
                    if (guarded.Length == 0)
                        Debug.LogWarning($"[Level] ({x},{y}) 게이트의 방에 적이 없어 처음부터 열려 있습니다");

                    var go = new GameObject("Arena Gate") { layer = LayerMask.NameToLayer("Ground") };
                    go.transform.SetParent(parent, false);
                    go.transform.position = layout.World(x + 0.5f, y);
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(1f, height);
                    box.offset = new Vector2(0f, height * 0.5f);

                    var field = new GameObject("Field");
                    field.transform.SetParent(go.transform, false);
                    field.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                    field.transform.localScale = new Vector3(1f, height, 1f);
                    var renderer = field.AddComponent<SpriteRenderer>();
                    renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GateMaterialPath);
                    renderer.sortingLayerName = "Props";
                    renderer.sortingOrder = 20;

                    var light = RenderingSetup.CreatePointLight(go.transform, "Light", new Vector3(0f, height * 0.5f, 0f), GateMagenta, 0.8f, Mathf.Max(4f, height * 0.8f));
                    light.blendStyleIndex = 1;

                    Rect area = room >= 0
                        ? new Rect(layout.World(layout.Rooms[room].xMin, layout.Rooms[room].yMin), layout.Rooms[room].size)
                        : new Rect(layout.World(x - 10, y), new Vector2(20f, height));
                    go.AddComponent<ArenaGate>().Bind(guarded, renderer, light, area);
                }
            }
        }

        static void PlaceHints(Layout layout)
        {
            if (layout.Hints.Count == 0)
                return;
            Transform parent = new GameObject("Hints").transform;
            foreach (Hint hint in layout.Hints)
            {
                var go = new GameObject("Hint") { layer = LayerMask.NameToLayer("Ignore Raycast") };
                go.transform.SetParent(parent, false);
                go.transform.position = layout.World(hint.Area.center.x, hint.Area.center.y);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = hint.Area.size;
                go.AddComponent<HintZone>().SetMessage(hint.Text);
            }
        }

        // '~' 가 이어진 구간마다 물웅덩이 쿼드 하나. 윗변이 지형 윗면(= '~' 칸의 바닥)에 오도록
        static void PlacePuddles(Layout layout)
        {
            Transform parent = new GameObject("Puddles").transform;
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(PuddleMaterialPath);
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (layout[x, y] != '~' || layout[x - 1, y] == '~')
                        continue;
                    int length = 0;
                    while (layout[x + length, y] == '~')
                        length++;

                    var go = new GameObject("Puddle");
                    go.transform.SetParent(parent, false);
                    go.transform.position = layout.World(x + length * 0.5f, y - 0.5f);
                    go.transform.localScale = new Vector3(length, 1f, 1f);
                    var spriteRenderer = go.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = square;
                    spriteRenderer.sharedMaterial = material;
                    spriteRenderer.sortingLayerName = "Tile_Front";
                    spriteRenderer.sortingOrder = 200;
                }
            }
        }

        static void PlaceRooms(Layout layout)
        {
            Transform parent = new GameObject("Camera Regions").transform;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                RectInt room = layout.Rooms[i];
                var go = new GameObject($"Room_{i + 1}") { layer = LayerMask.NameToLayer("Ignore Raycast") };
                go.transform.SetParent(parent, false);
                go.transform.position = layout.World(room.center.x, room.center.y);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = room.size;
                go.AddComponent<CameraRoom>();
            }
        }

        // @ambient: 섹터마다 밤 조명 색을 바꿔 분위기를 나눈다 (게임 영역 전역 조명 + 선택적으로 배경 전역 조명)
        static void ApplyAmbient(Layout layout)
        {
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (light.lightType != Light2D.LightType.Global)
                    continue;
                if (light.name == "Global Light 2D" && layout.Ambient.HasValue)
                    light.color = layout.Ambient.Value;
                else if (light.name == "Global Light 2D (Background)" && layout.BackgroundAmbient.HasValue)
                    light.color = layout.BackgroundAmbient.Value;
            }
        }

        static void PlaceLevelInfo(Layout layout)
        {
            var info = new GameObject("Level Info").AddComponent<LevelInfo>();
            var serialized = new SerializedObject(info);
            serialized.FindProperty("code").stringValue = layout.Code;
            serialized.FindProperty("title").stringValue = layout.Title;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 타이틀 배경: 플레이어·카메라 추적·방을 빼고, 카메라가 'P' 높이에서 레이아웃 위를 천천히 왕복하게 한다
        static void ConvertToTitle(Layout layout)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 focus = player != null ? player.transform.position : (Vector3)layout.World(layout.Width * 0.5f, 6f);
            if (player != null)
                Object.DestroyImmediate(player.gameObject);
            foreach (string name in new[] { "Camera Regions", "Enemies", "Checkpoints", "Data Chips", "Gates", "Hints" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            Camera camera = Camera.main;
            var controller = camera.GetComponent<CameraController>();
            if (controller != null)
                Object.DestroyImmediate(controller);
            float halfView = camera.orthographicSize * 640f / 360f;
            float left = layout.World(0f, 0f).x + halfView;
            float right = layout.World(layout.Width, 0f).x - halfView;
            camera.transform.position = new Vector3((left + right) * 0.5f, focus.y + 2.5f, camera.transform.position.z);
            camera.gameObject.AddComponent<TitleCamera>().Configure((left + right) * 0.5f, Mathf.Max(0f, (right - left) * 0.5f));

            new GameObject("Title Screen").AddComponent<UI.TitleScreen>();
        }

        // 레벨은 세로로도 크게 움직이므로 배경을 세로로 카메라에 고정한다 — 안 그러면 건물 밑동이 화면 중간에 떠 보인다
        static void LockBackgroundsVertically()
        {
            foreach (var layer in Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(layer);
                SerializedProperty follow = serialized.FindProperty("follow");
                follow.vector2Value = new Vector2(follow.vector2Value.x, 1f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // TestMap 에서 물려받은 것 중 레이아웃이 다시 만드는 것들을 지운다
        static void ClearScene()
        {
            foreach (string name in new[] { "Enemies", "Lights", "Props", "Puddles", "Camera Regions" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                tilemap.ClearAllTiles();
        }

        // ----- 목록·빌드 설정 -----

        static void WriteCatalog(List<LevelCatalog.Entry> entries)
        {
            RenderingSetup.EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.levels = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Build Settings 맨 앞을 Title → Level01 → Level02 ... 순서로 맞춘다. 나머지 씬은 뒤에 그대로 둔다.</summary>
        public static void UpdateBuildSettings()
        {
            var ordered = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) != null)
                ordered.Add(TitleScenePath);
            ordered.AddRange(LevelLayouts().Select(ScenePathFor).Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null));

            var scenes = ordered.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !ordered.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ----- 에셋 -----

        static Tile[] EnsureTerrainTiles()
        {
            RenderingSetup.EnsureFolder(Path.GetDirectoryName(TerrainTexturePath).Replace('\\', '/'));
            RenderingSetup.EnsureFolder(TerrainTileFolder);
            if (!File.Exists(TerrainTexturePath))
            {
                TerrainTileGenerator.Write(TerrainTexturePath);
                AssetDatabase.ImportAsset(TerrainTexturePath, ImportAssetOptions.ForceSynchronousImport);
                NormalMapGenerator.Generate(TerrainTexturePath); // 철판 이음매·리벳이 네온 빛을 받아 입체로 보이게
            }

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(TerrainTexturePath).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
            var tiles = new Tile[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                string path = $"{TerrainTileFolder}/{(TerrainTileGenerator.Kind)i}.asset";
                tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tiles[i] != null)
                    continue;
                tiles[i] = ScriptableObject.CreateInstance<Tile>();
                tiles[i].sprite = sprites[i];
                tiles[i].colliderType = Tile.ColliderType.None; // 그래픽 전용
                AssetDatabase.CreateAsset(tiles[i], path);
            }
            TestMapSetup.EnsurePalette("Terrain", tiles.Select((tile, i) => (new Vector3Int(i, 0, 0), (TileBase)tile)).ToArray());
            return tiles;
        }

        static Tile[] LoadTerrainTiles() =>
            Enumerable.Range(0, TerrainTileGenerator.Count)
                .Select(i => AssetDatabase.LoadAssetAtPath<Tile>($"{TerrainTileFolder}/{(TerrainTileGenerator.Kind)i}.asset"))
                .ToArray();

        static void EnsureMaterials()
        {
            RenderingSetup.EnsureFolder(FXMaterialFolder);
            EnsureMaterial(TerrainMaterialPath, "Platformer/SpriteLitFX", terrain =>
            {
                terrain.SetFloat("_HookHighlight", 1f);                           // 갈고리 대상 주변 파동
                terrain.SetColor("_OutlineColor", new Color(0.2f, 2.5f, 2.2f, 1f));
                terrain.SetFloat("_EmissiveThreshold", 0.6f);                      // 스터드·가시만 스스로 빛난다
                terrain.SetFloat("_EmissiveStrength", 2.2f);
                terrain.SetFloat("_SlowResponse", 1f);
            });
            EnsureMaterial(FacadeMaterialPath, "Platformer/SpriteLitFX", facade =>
            {
                facade.SetFloat("_EmissiveThreshold", 0.55f);                      // 초록 창문·화면이 밤에 켜져 있게
                facade.SetFloat("_EmissiveStrength", 1.4f);
                facade.SetFloat("_SlowResponse", 1f);
            });
            EnsureMaterial(BeaconMaterialPath, "Platformer/Beacon");
            EnsureMaterial(ExitBeaconMaterialPath, "Platformer/Beacon", exit =>
            {
                exit.SetColor("_ActiveColor", new Color(0.5f, 2.8f, 1.1f, 1f));   // 출구는 처음부터 켜진 초록 기둥
                exit.SetFloat("_Activation", 1f);
            });
            EnsureMaterial(GateMaterialPath, "Platformer/EnergyGate");
            EnsureMaterial(ChipMaterialPath, "Platformer/DataChip");
        }

        static void EnsureMaterial(string path, string shaderName, Action<Material> configure = null)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            var material = new Material(Shader.Find(shaderName));
            configure?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
        }

        static Sprite PropSprite(string relativePath) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsRoot + relativePath);
    }
}
