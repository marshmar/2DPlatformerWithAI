using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 6단계: CameraConfig 에셋을 만들고 TestMap 의 Main Camera 에 CameraController 를 붙인다.
    /// 7단계: TestMap 에 예시 방 2개와 시작 고정 구역을 배치하고, 테스트할 수 있게 바닥을 오른쪽으로 늘린다.
    /// </summary>
    public static class CameraSetup
    {
        const string ConfigPath = "Assets/_Project/Settings/CameraFollow.asset";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";
        const string GroundTilePath = "Assets/_Project/Tiles/Collision/Ground.asset";

        [MenuItem("Tools/Project Setup/Step 7 (Camera Zones)", priority = 6)]
        static void SetupZones()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            GameObject regions = GameObject.Find("Camera Regions");
            Transform parent = regions != null ? regions.transform : new GameObject("Camera Regions").transform;

            // 방 A: 기존 맵 + 오른쪽 확장, 화면 2장 폭 / 방 B: 그 오른쪽 화면 1장 폭. 바닥 아래(y < -8)는 비추지 않는다
            EnsureRegion<CameraRoom>(parent, "Room_A", Rect.MinMaxRect(-20f, -8f, 60f, 18f), out _);
            EnsureRegion<CameraRoom>(parent, "Room_B", Rect.MinMaxRect(60f, -8f, 100f, 18f), out _);

            // 시작 구역: 플레이어 몸이 x = 10 을 넘기 전까지 카메라 가로를 x = 0 에 고정 (마리오식 무빙 트리거)
            var startLock = EnsureRegion<CameraLockZone>(parent, "StartLock", Rect.MinMaxRect(-20f, -8f, 10f, 18f), out bool created);
            if (created)
            {
                var lockSettings = new SerializedObject(startLock);
                lockSettings.FindProperty("lockX").boolValue = true;
                lockSettings.FindProperty("lockY").boolValue = false;
                lockSettings.FindProperty("anchorOffset").vector2Value = new Vector2(-startLock.transform.position.x, 0f);
                lockSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            ExtendFloor();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Camera] 방 2개(Room_A, Room_B)와 시작 고정 구역(StartLock) 배치 완료 — 바닥이 x = 100 까지 늘어났습니다");
        }

        static T EnsureRegion<T>(Transform parent, string objectName, Rect area, out bool created) where T : Component
        {
            Transform existing = parent.Find(objectName);
            created = existing == null;
            if (!created)
                return existing.GetComponent<T>(); // 이미 있으면 직접 조절한 크기·설정을 그대로 둔다

            var go = new GameObject(objectName) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.SetParent(parent, false);
            go.transform.position = area.center;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = area.size;
            return go.AddComponent<T>();
        }

        // 비어 있는 칸만 채운다 — 직접 칠한 타일은 건드리지 않는다
        static void ExtendFloor()
        {
            GameObject collisionObject = GameObject.Find("Grid/Collision");
            var ground = AssetDatabase.LoadAssetAtPath<TileBase>(GroundTilePath);
            if (collisionObject == null || ground == null)
            {
                Debug.LogWarning("[Camera] Grid/Collision 타일맵이나 Ground 타일을 찾지 못해 바닥 연장을 건너뜀");
                return;
            }

            var collision = collisionObject.GetComponent<Tilemap>();
            for (int x = 20; x < 100; x++)
                SetIfEmpty(collision, new Vector3Int(x, -8, 0), ground);
            for (int y = -7; y <= 2; y++)
                SetIfEmpty(collision, new Vector3Int(99, y, 0), ground); // 방 B 오른쪽 끝 벽
        }

        static void SetIfEmpty(Tilemap tilemap, Vector3Int cell, TileBase tile)
        {
            if (!tilemap.HasTile(cell))
                tilemap.SetTile(cell, tile);
        }

        [MenuItem("Tools/Project Setup/Step 6 (Camera Follow)", priority = 5)]
        static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<CameraConfig>(ConfigPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CameraConfig>(), ConfigPath);

            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (camera == null || player == null)
            {
                Debug.LogError("[Camera] TestMap 에서 Main Camera 또는 Player 를 찾지 못했습니다");
                return;
            }

            var controller = camera.GetComponent<CameraController>();
            if (controller == null)
                controller = camera.gameObject.AddComponent<CameraController>();

            // 씬을 연 뒤에 불러온다 — 씬을 열면 쓰이지 않는 에셋이 언로드된다
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CameraConfig>(ConfigPath);
            serialized.FindProperty("target").objectReferenceValue = player;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Camera] Main Camera 에 CameraController 연결 완료 — 플레이 중 Gizmos 를 켜면 데드존이 보입니다");
        }
    }
}
