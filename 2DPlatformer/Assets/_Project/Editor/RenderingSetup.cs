using System;
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
    /// 8단계: SpriteLitFX 머티리얼을 만들어 플레이어·지형·배경에 적용한다.
    /// 10단계: 밤 분위기 조명(전역 2개 + 네온 포인트 라이트 + 플레이어 라이트)과 네온 간판을 배치한다.
    /// 이미 있는 에셋·오브젝트는 건드리지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class RenderingSetup
    {
        const string ShaderName = "Platformer/SpriteLitFX";
        const string MaterialFolder = "Assets/_Project/Materials";
        public const string CharacterMaterialPath = MaterialFolder + "/SpriteCharacter.mat";
        public const string EnvironmentMaterialPath = MaterialFolder + "/SpriteEnvironment.mat";
        public const string BackgroundMaterialPath = MaterialFolder + "/SpriteBackground.mat";
        public const string SkyMaterialPath = MaterialFolder + "/SpriteSky.mat";
        public const string NeonMaterialPath = MaterialFolder + "/SpriteNeon.mat";
        public const string TileCollisionMaterialPath = MaterialFolder + "/TileCollision.mat";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";
        const string NeonBannerFolder = "Assets/ThirdParty/warped city files/warped city files/Assets/ENVIRONMENT/props/";

        static readonly Color Magenta = new(1f, 0.25f, 0.85f);
        static readonly Color Cyan = new(0.2f, 0.9f, 1f);
        static readonly Color Amber = new(1f, 0.6f, 0.2f);

        [MenuItem("Tools/Project Setup/Step 8 (Sprite Shader)", priority = 7)]
        public static void SetupSpriteShader()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureMaterials();
            EditPrefab(PlayerPrefabPath, root =>
            {
                var visual = root.transform.Find("Visual");
                visual.GetComponent<SpriteRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
                if (visual.GetComponent<SpriteFX>() == null)
                    visual.gameObject.AddComponent<SpriteFX>();
            });

            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            var background = AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);
            var environment = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentMaterialPath);
            var tileCollision = AssetDatabase.LoadAssetAtPath<Material>(TileCollisionMaterialPath);

            // 하늘은 슬로우 때 탈색하지 않는다 — 하늘 스프라이트 위쪽은 카메라 배경색이 채우는데, 하늘만 회색이 되면 경계선이 드러난다
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            GameObject backgrounds = GameObject.Find("Backgrounds");
            if (backgrounds != null)
                foreach (var spriteRenderer in backgrounds.GetComponentsInChildren<SpriteRenderer>())
                    spriteRenderer.sharedMaterial = spriteRenderer.name == "Sky" ? sky : background;
            foreach (var tilemapRenderer in Object.FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None))
                tilemapRenderer.sharedMaterial = tilemapRenderer.name == "Collision" ? tileCollision : environment;

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Rendering] SpriteLitFX 머티리얼 적용 완료 (플레이어, 배경, 타일맵)");
        }

        [MenuItem("Tools/Project Setup/Step 10 (Lights & Rim)", priority = 9)]
        public static void SetupLights()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureMaterials();
            EditPrefab(PlayerPrefabPath, root =>
            {
                if (root.transform.Find("Light") != null)
                    return;
                // 캐릭터 주변을 비추는 약한 빛 — 어두운 곳에서도 실루엣과 노말맵 입체감이 보이게
                var light = CreatePointLight(root.transform, "Light", new Vector3(0f, 1.2f, 0f), new Color(0.7f, 0.85f, 1f), 0.6f, 5f);
                light.shadowsEnabled = false;
            });

            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            int[] backgroundLayers = { SortingLayer.NameToID("BG_Far"), SortingLayer.NameToID("BG_Near") };
            int[] worldLayers = SortingLayer.layers.Select(l => l.id).Where(id => !backgroundLayers.Contains(id)).ToArray();

            // 전역 조명을 둘로 나눈다: 게임 영역은 어두운 보라 밤, 배경은 비교적 밝게 (배경이 묻히지 않도록)
            Light2D worldGlobal = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.lightType == Light2D.LightType.Global && l.name == "Global Light 2D");
            if (worldGlobal != null)
            {
                worldGlobal.color = new Color(0.52f, 0.48f, 0.82f);
                worldGlobal.intensity = 0.5f;
                SetTargetLayers(worldGlobal, worldLayers);
            }
            if (GameObject.Find("Global Light 2D (Background)") == null)
            {
                var backgroundGlobal = new GameObject("Global Light 2D (Background)").AddComponent<Light2D>();
                // 대상 레이어를 먼저 좁혀야 한다 — 생성 직후엔 모든 레이어를 비추므로, 이 상태로 Global 이 되면
                // 같은 레이어에 전역 조명이 둘이라는 오류가 난다
                SetTargetLayers(backgroundGlobal, backgroundLayers);
                backgroundGlobal.lightType = Light2D.LightType.Global;
                backgroundGlobal.color = new Color(0.8f, 0.75f, 1f);
                backgroundGlobal.intensity = 0.85f;
                SetTargetLayers(backgroundGlobal, backgroundLayers);
            }

            if (GameObject.Find("Lights") == null)
            {
                Transform lights = new GameObject("Lights").transform;
                (float x, float y, Color color)[] placements =
                {
                    (-12f, -3f, Magenta), (2f, -2f, Cyan), (18f, -3f, Magenta), (35f, -2f, Cyan),
                    (52f, -3f, Amber), (70f, -2f, Magenta), (88f, -3f, Cyan),
                };
                // 곱하기 라이트는 어두운 픽셀을 거의 밝히지 못한다(어두운 색 × 빛 = 여전히 어두움).
                // 네온의 색 번짐은 블렌드 스타일 1(Additive)로 색을 "더해서" 만든다
                for (int i = 0; i < placements.Length; i++)
                {
                    var (x, y, color) = placements[i];
                    var neonLight = CreatePointLight(lights, "Neon Light", new Vector3(x, y, 0f), color, 0.6f, 6.5f);
                    neonLight.blendStyleIndex = 1;
                    if (i % 3 == 1)
                        neonLight.gameObject.AddComponent<NeonFlicker>(); // 몇 개만 고장 난 네온
                }
            }

            if (GameObject.Find("Props") == null)
            {
                Transform props = new GameObject("Props").transform;
                Sprite[] frames = Enumerable.Range(1, 4)
                    .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{NeonBannerFolder}banner-neon/banner-neon-{i}.png"))
                    .ToArray();
                var neon = AssetDatabase.LoadAssetAtPath<Material>(NeonMaterialPath);
                foreach (float x in new[] { -12f, 18f, 52f, 70f })
                {
                    var banner = new GameObject("Neon Banner");
                    banner.transform.SetParent(props, false);
                    banner.transform.position = new Vector3(x, -2.5f, 0f);
                    var spriteRenderer = banner.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = frames[0];
                    spriteRenderer.sharedMaterial = neon;
                    spriteRenderer.sortingLayerName = "Props";
                    banner.AddComponent<SpriteAnimator>().SetClips(new[]
                    {
                        new SpriteAnimator.Clip { name = "Loop", frames = frames, fps = 8f, loop = true },
                    }, useUnscaledTime: true, randomStart: true);
                }
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Rendering] 조명·네온 간판 배치 완료");
        }

        public static void EnsureMaterials()
        {
            EnsureFolder(MaterialFolder);
            EnsureMaterial(CharacterMaterialPath, m =>
            {
                m.SetFloat("_RimIntensity", 4f);
                m.SetFloat("_RimThreshold", 0.02f);
                m.SetFloat("_RimWidth", 1f);
                m.SetFloat("_SlowResponse", 0f); // 슬로우 중에도 캐릭터는 원색 — 탈색된 배경 위에서 눈에 띄게
            });
            EnsureMaterial(EnvironmentMaterialPath, m => m.SetFloat("_SlowResponse", 1f));
            EnsureMaterial(BackgroundMaterialPath, m => m.SetFloat("_SlowResponse", 1f));
            EnsureMaterial(SkyMaterialPath, m => m.SetFloat("_SlowResponse", 0f));
            EnsureMaterial(NeonMaterialPath, m =>
            {
                m.SetFloat("_EmissiveThreshold", 0.45f);
                m.SetFloat("_EmissiveStrength", 2.5f);
                m.SetFloat("_SlowResponse", 0.5f);
            });
            EnsureMaterial(TileCollisionMaterialPath, m =>
            {
                m.SetFloat("_HookHighlight", 1f);
                m.SetColor("_OutlineColor", new Color(0.2f, 2.5f, 2.2f, 1f));
                m.SetFloat("_SlowResponse", 1f);
            });
        }

        static void EnsureMaterial(string path, Action<Material> configure)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            var material = new Material(Shader.Find(ShaderName));
            configure(material);
            AssetDatabase.CreateAsset(material, path);
        }

        public static Light2D CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = 0.3f;
            light.falloffIntensity = 0.6f;
            light.blendStyleIndex = 0;

            // 노말맵을 쓰려면 라이트마다 켜야 한다. 거리(z)가 작을수록 빛이 비스듬히 들어와 입체감이 강해진다
            var serialized = new SerializedObject(light);
            serialized.FindProperty("m_NormalMapQuality").intValue = (int)Light2D.NormalMapQuality.Accurate;
            serialized.FindProperty("m_NormalMapDistance").floatValue = 2.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return light;
        }

        static void SetTargetLayers(Light2D light, int[] layerIds)
        {
            var serialized = new SerializedObject(light);
            SerializedProperty layers = serialized.FindProperty("m_ApplyToSortingLayers");
            layers.arraySize = layerIds.Length;
            for (int i = 0; i < layerIds.Length; i++)
                layers.GetArrayElementAtIndex(i).intValue = layerIds[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EditPrefab(string path, Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
