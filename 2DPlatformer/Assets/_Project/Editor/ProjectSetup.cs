using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 1단계 프로젝트 셋업. Tools/Project Setup/Run All 한 번이면 끝나고, 여러 번 실행해도 안전하다.
    /// </summary>
    public static class ProjectSetup
    {
        // 뒤에 있을수록 위에 그려진다
        static readonly string[] SortingLayerOrder =
        {
            "BG_Far", "BG_Near", "Tile_Back", "Props", "Default", "Enemy", "Player", "FX", "Tile_Front", "UI",
        };

        static readonly (int Index, string Name)[] PhysicsLayers =
        {
            (6, "Ground"), (7, "Hookable"), (8, "Player"), (9, "Enemy"), (10, "Hazard"),
        };

        internal const string BackgroundRoot = "Assets/ThirdParty/warped city files/warped city files/Assets/ENVIRONMENT/background/";
        const string SandboxScenePath = "Assets/_Project/Scenes/Sandbox.unity";
        const string CraftpixRoot = "Assets/ThirdParty/Free 3 Cyberpunk Sprites Pixel Art/";
        const int ReferenceWidth = 640;
        const int ReferenceHeight = 360;

        [MenuItem("Tools/Project Setup/Step 1 (Run All)", priority = 0)]
        static void RunAll()
        {
            // 순서 중요: Light2D 는 생성 시점에 존재하는 소팅 레이어만 비추므로 레이어 → 씬 순서
            SetupLayers();
            PixelArtImportRules.ApplyToAll();
            CreateSandboxScene();
        }

        [MenuItem("Tools/Project Setup/1. Layers", priority = 20)]
        static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");
            foreach ((int index, string name) in PhysicsLayers)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(layer.stringValue))
                    layer.stringValue = name;
                else if (layer.stringValue != name)
                    Debug.LogWarning($"[Project Setup] Layer {index} 는 이미 '{layer.stringValue}' 라서 '{name}' 을 건너뜀");
            }

            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");
            var usedIds = new HashSet<uint>();
            for (int i = 0; i < sortingLayers.arraySize; i++)
                usedIds.Add(sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").uintValue);

            var random = new System.Random();
            foreach (string name in SortingLayerOrder)
            {
                if (IndexOfSortingLayer(sortingLayers, name) >= 0)
                    continue;

                sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
                SerializedProperty layer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
                uint id;
                do id = (uint)random.Next(1, int.MaxValue); while (!usedIds.Add(id));
                layer.FindPropertyRelative("name").stringValue = name;
                layer.FindPropertyRelative("uniqueID").uintValue = id;
            }

            for (int target = 0; target < SortingLayerOrder.Length; target++)
            {
                int current = IndexOfSortingLayer(sortingLayers, SortingLayerOrder[target]);
                if (current != target)
                    sortingLayers.MoveArrayElement(current, target);
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Project Setup] Physics / Sorting Layer 설정 완료");
        }

        [MenuItem("Tools/Project Setup/2. Pixel Art Import Rules", priority = 21)]
        static void ApplyImportRules() => PixelArtImportRules.ApplyToAll();

        [MenuItem("Tools/Project Setup/3. Sandbox Scene", priority = 22)]
        static void CreateSandboxScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxScenePath) != null &&
                !EditorUtility.DisplayDialog("Sandbox 씬", $"{SandboxScenePath} 가 이미 있습니다. 새로 만들까요?", "새로 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateGlobalLight();

            // 스케일·선명도 확인용 배치. 발 높이 y = -4
            CreateSprite("Skyline A", BackgroundRoot + "skyline-a.png", "skyline-a", "BG_Far", new Vector2(-10f, 3.5f));
            CreateSprite("Skyline B", BackgroundRoot + "skyline-b.png", "skyline-b", "BG_Far", new Vector2(10f, 3.5f));
            CreateSprite("Near Buildings", BackgroundRoot + "near-buildings-bg.png", "near-buildings-bg", "BG_Near", new Vector2(0f, 2.5f));
            CreateSprite("Biker", CraftpixRoot + "1 Biker/Biker_idle.png", "Biker_idle_0", "Enemy", new Vector2(-3f, -4f));
            CreateSprite("Cyborg (Player)", CraftpixRoot + "3 Cyborg/Cyborg_idle.png", "Cyborg_idle_0", "Player", new Vector2(0f, -4f));
            CreateSprite("Punk", CraftpixRoot + "2 Punk/Punk_idle.png", "Punk_idle_0", "Enemy", new Vector2(3f, -4f), flipX: true);

            EditorSceneManager.SaveScene(scene, SandboxScenePath);
            Debug.Log($"[Project Setup] {SandboxScenePath} 생성 — Game 뷰 해상도를 1920x1080 으로 두고 확인하세요");
        }

        internal static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ReferenceHeight * 0.5f / PixelArtImportRules.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.05f, 0.1f);
            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();

            var pixelPerfect = go.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = PixelArtImportRules.PixelsPerUnit;
            pixelPerfect.refResolutionX = ReferenceWidth;
            pixelPerfect.refResolutionY = ReferenceHeight;
            // Upscale Render Texture 는 포스트 이펙트까지 저해상도로 만들기 때문에,
            // 네이티브 해상도로 그리고 스프라이트 위치만 픽셀 그리드에 스냅한다 (이펙트 쪽 픽셀감은 셰이더에서 양자화)
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            return camera;
        }

        internal static void CreateGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
        }

        static void CreateSprite(string objectName, string texturePath, string spriteName, string sortingLayer,
            Vector2 position, bool flipX = false)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogWarning($"[Project Setup] 스프라이트를 찾지 못함: {texturePath} / {spriteName}");
                return;
            }

            var go = new GameObject(objectName);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.flipX = flipX;
        }

        static int IndexOfSortingLayer(SerializedProperty sortingLayers, string name)
        {
            for (int i = 0; i < sortingLayers.arraySize; i++)
                if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                    return i;
            return -1;
        }
    }
}
