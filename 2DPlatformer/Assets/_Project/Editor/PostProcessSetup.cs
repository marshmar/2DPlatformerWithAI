using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 21단계 후처리 셋업:
    ///   - 2D 렌더러에 FXPostFeature 를 추가하고, 왜곡 효과가 읽을 Camera Sorting Layer Texture 를 켠다
    ///   - Bloom·Vignette·색보정 + FXPostSettings 가 담긴 볼륨 프로필을 만들어 TestMap 에 전역 볼륨으로 둔다
    ///   - 카메라 후처리를 켠다
    /// </summary>
    public static class PostProcessSetup
    {
        const string RendererPath = "Assets/Settings/Renderer2D.asset";
        const string ProfilePath = "Assets/_Project/Settings/GlobalVolumeProfile.asset";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";

        [MenuItem("Tools/Project Setup/Step 21 (Post Processing)", priority = 20)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            ConfigureRenderer();
            VolumeProfile profile = EnsureProfile();

            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (GameObject.Find("Global Volume") == null)
            {
                var volume = new GameObject("Global Volume").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath); // 씬을 연 뒤 다시 불러온다
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Post] 렌더러 피처·볼륨·카메라 후처리 설정 완료");
        }

        static void ConfigureRenderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            var serialized = new SerializedObject(data);

            // 칼 이펙트·쇼크웨이브 같은 왜곡이 "자기 뒤의 화면"을 읽을 수 있게, Player 레이어까지 그린 화면을 텍스처로 남긴다
            serialized.FindProperty("m_UseCameraSortingLayersTexture").boolValue = true;
            serialized.FindProperty("m_CameraSortingLayersTextureBound").intValue = SortingLayer.NameToID("Player");

            if (!data.rendererFeatures.OfType<FXPostFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<FXPostFeature>();
                feature.name = "FX Post";
                var featureSettings = new SerializedObject(feature);
                featureSettings.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/Platformer/FXPost");
                featureSettings.ApplyModifiedPropertiesWithoutUndo();

                // 렌더러 피처는 렌더러 에셋의 하위 에셋이고, 목록과 함께 "로컬 파일 ID 맵"도 맞춰 줘야 한다
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        static VolumeProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // 네온(HDR 1 이상)만 번지도록 문턱을 1 가까이
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.contrast.Override(8f);
            colorAdjustments.saturation.Override(10f);

            var fx = profile.Add<FXPostSettings>(true);
            fx.scanlines.Override(0.06f);
            fx.chromaticAberration.Override(0.15f);

            // 볼륨 컴포넌트는 프로필의 하위 에셋으로 저장해야 한다
            foreach (VolumeComponent component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
