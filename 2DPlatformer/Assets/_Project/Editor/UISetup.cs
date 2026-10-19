using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 28단계: 타이틀·UI.
    ///   - UI 머티리얼(Resources/UI/CyberUI, CyberText)을 만든다. 런타임 UI 가 이것을 불러 쓰고, 빌드에 UI 셰이더가 포함되게 하는 역할도 한다
    ///   - Levels/Title.txt 로 타이틀 배경 씬(Scenes/Title.unity)을 만들고 TitleScreen 을 둔다
    ///   - Build Settings 를 Title → Level01 → ... 순서로 맞춘다 (게임은 타이틀에서 시작한다)
    /// HUD·일시정지·설정·결과 화면은 레벨 씬이 열릴 때 GameFlow 가 런타임에 만든다 — 씬에 따로 둘 것이 없다.
    /// </summary>
    public static class UISetup
    {
        const string ResourceFolder = "Assets/_Project/Resources/UI";
        const string CatalogPath = "Assets/_Project/Resources/LevelCatalog.asset";

        [MenuItem("Tools/Project Setup/Step 28 (Title & UI)", priority = 27)]
        public static void Run()
        {
            // 섹터 목록이 없으면 레벨부터 만든다 (타이틀의 섹터 선택이 목록을 읽는다)
            if (AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath) == null)
                LevelBuilder.BuildAll();

            if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelBuilder.TitleScenePath) != null &&
                !EditorUtility.DisplayDialog("Title", "Levels/Title.txt 로 타이틀 씬을 다시 만듭니다.", "다시 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureMaterials();
            LevelBuilder.BuildTitle();
            Debug.Log("[UI] 타이틀 씬·UI 머티리얼·빌드 순서 설정 완료 — Scenes/Title 을 열고 플레이하세요");
        }

        public static void EnsureMaterials()
        {
            RenderingSetup.EnsureFolder(ResourceFolder);
            EnsureMaterial(ResourceFolder + "/CyberUI.mat", "Platformer/UI/Cyber");
            EnsureMaterial(ResourceFolder + "/CyberText.mat", "Platformer/UI/CyberText");
        }

        static void EnsureMaterial(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[UI] 셰이더 {shaderName} 를 찾지 못했습니다");
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
        }
    }
}
