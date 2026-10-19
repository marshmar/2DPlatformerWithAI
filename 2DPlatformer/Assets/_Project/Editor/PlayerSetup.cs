using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 3단계 플레이어 셋업: 애니메이션 클립, Animator Controller, 물리 재질, 이동 설정, 입력 연결, Player 프리팹,
    /// TestMap 배치. 이미 있는 에셋은 건드리지 않으므로(직접 튜닝한 값 보존) 여러 번 실행해도 안전하다.
    /// </summary>
    public static class PlayerSetup
    {
        const string CyborgRoot = "Assets/ThirdParty/Free 3 Cyberpunk Sprites Pixel Art/3 Cyborg/";
        const string AnimationFolder = "Assets/_Project/Animations/Player";
        const string SettingsFolder = "Assets/_Project/Settings";
        const string ControllerPath = AnimationFolder + "/Player.controller";
        const string InputActionsPath = SettingsFolder + "/PlayerControls.inputactions";
        const string MovementConfigPath = SettingsFolder + "/PlayerMovement.asset";
        const string NoFrictionPath = SettingsFolder + "/NoFriction.physicsMaterial2D";
        const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";

        // 시작 바닥 윗면(y = -7) 위
        static readonly Vector3 SpawnPosition = new(0f, -7f, 0f);

        [MenuItem("Tools/Project Setup/Step 3 (Player)", priority = 2)]
        static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(AnimationFolder);
            EnsureFolder(SettingsFolder);
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

            AnimatorController controller = EnsureAnimatorController(EnsureClips());

            var config = EnsureAsset(MovementConfigPath, () => ScriptableObject.CreateInstance<MovementConfig>());
            var noFriction = EnsureAsset(NoFrictionPath, () => new PhysicsMaterial2D { friction = 0f, bounciness = 0f });
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                CreatePrefab(controller, config, noFriction, inputActions);
            AssetDatabase.SaveAssets();

            PlaceInTestMap();
        }

        // 기존 Animator 에 빠진 상태(WallSlide)만 추가한다
        [MenuItem("Tools/Project Setup/Step 5 (Wall Slide Animation)", priority = 4)]
        static void AddWallSlideAnimation()
        {
            EnsureAnimatorController(EnsureClips());
            AssetDatabase.SaveAssets();
            Debug.Log("[Player] WallSlide 애니메이션 추가 완료");
        }

        // 첫 번째 클립이 새 Animator 의 기본 상태가 되므로 Idle 을 맨 앞에 둔다
        internal static AnimationClip[] EnsureClips() => new[]
        {
            EnsureClip("Idle", "Cyborg_idle.png", new[] { 0, 1, 2, 3 }, 8f, loop: true),
            EnsureClip("Run", "Cyborg_run.png", new[] { 0, 1, 2, 3, 4, 5 }, 12f, loop: true),
            // 점프 시트 4장 중 앞 2장(도약·상승)은 올라갈 때, 뒤 2장(펼침·착지 준비)은 떨어질 때
            EnsureClip("Jump", "Cyborg_jump.png", new[] { 0, 1 }, 12f, loop: false),
            EnsureClip("Fall", "Cyborg_jump.png", new[] { 2, 3 }, 12f, loop: false),
            // 벽 매달림 전용 모션이 없어서 낙하 포즈를 벽을 등진 방향으로 쓴다
            EnsureClip("WallSlide", "Cyborg_jump.png", new[] { 3 }, 12f, loop: false),
            // 전투 (11~24단계): 공격 3타 18fps, 피격, 갈고리 감기·처형 돌진용 회전(더블점프 시트)
            EnsureClip("Attack1", "Cyborg_attack1.png", new[] { 0, 1, 2, 3, 4, 5 }, 18f, loop: false),
            EnsureClip("Attack2", "Cyborg_attack2.png", new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 18f, loop: false),
            EnsureClip("Attack3", "Cyborg_attack3.png", new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 18f, loop: false),
            EnsureClip("Hurt", "Cyborg_hurt.png", new[] { 0, 1 }, 10f, loop: false),
            EnsureClip("Spin", "Cyborg_doublejump.png", new[] { 0, 1, 2, 3, 4, 5 }, 20f, loop: true),
        };

        // 3단계에서 만든 프리팹에는 오버레이가 없으므로 추가만 한다 (튜닝한 값은 그대로)
        [MenuItem("Tools/Project Setup/Step 4 (Debug Overlay)", priority = 3)]
        static void AddDebugOverlay()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.GetComponent<PlayerDebugOverlay>() == null)
                {
                    root.AddComponent<PlayerDebugOverlay>();
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                Debug.Log("[Player] 디버그 오버레이 준비 완료 — 플레이 중 F1 로 켜고 끕니다");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void CreatePrefab(AnimatorController controller, MovementConfig config, PhysicsMaterial2D noFriction,
            InputActionAsset inputActions)
        {
            var root = new GameObject("Player") { tag = "Player", layer = LayerMask.NameToLayer("Player") };
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f; // 중력은 PlayerController 가 직접 준다
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate; // 50Hz 물리를 화면 프레임 사이에서 부드럽게
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;

                // 피벗이 발밑이라 콜라이더를 위로 올린다. 몸통 12x32px, 모서리를 살짝 둥글려 벽 모서리에 덜 걸리게
                var box = root.AddComponent<BoxCollider2D>();
                box.edgeRadius = 0.05f;
                box.size = new Vector2(0.75f, 2f) - Vector2.one * (box.edgeRadius * 2f);
                box.offset = new Vector2(0f, 1f);
                box.sharedMaterial = noFriction; // 공중에서 벽을 밀 때 마찰로 달라붙지 않게

                var input = new SerializedObject(root.AddComponent<PlayerInputReader>());
                input.FindProperty("actions").objectReferenceValue = inputActions;
                input.ApplyModifiedPropertiesWithoutUndo();

                var player = new SerializedObject(root.AddComponent<PlayerController>());
                player.FindProperty("config").objectReferenceValue = config;
                player.FindProperty("groundMask").intValue = LayerMask.GetMask("Ground", "Hookable");
                player.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<PlayerDebugOverlay>();

                // 그래픽은 자식으로 분리 — 뒤집기·찌그러짐 같은 연출이 콜라이더에 영향을 주지 않게
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var spriteRenderer = visual.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = LoadSheet("Cyborg_idle.png")[0];
                spriteRenderer.sortingLayerName = "Player";
                visual.AddComponent<Animator>().runtimeAnimatorController = controller;
                visual.AddComponent<PlayerAnimator>();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void PlaceInTestMap()
        {
            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<PlayerController>() != null)
            {
                Debug.Log("[Player] TestMap 에 이미 Player 가 있어서 배치는 건너뜀");
                return;
            }

            // 씬을 열면 쓰이지 않는 에셋이 언로드되므로 프리팹은 씬을 연 뒤에 불러온다
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = SpawnPosition;
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Player] TestMap 에 Player 배치 완료 — 플레이해서 A/D 이동, Space 점프를 확인하세요");
        }

        static AnimationClip EnsureClip(string name, string sheetFile, int[] frames, float fps, bool loop)
        {
            string path = $"{AnimationFolder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
                return existing;

            Sprite[] sheet = LoadSheet(sheetFile);
            // 반복 클립은 마지막 프레임도 1/fps 만큼 보이도록 끝에 같은 프레임을 한 번 더 찍는다
            int keyCount = loop ? frames.Length + 1 : frames.Length;
            var keys = new ObjectReferenceKeyframe[keyCount];
            for (int i = 0; i < keyCount; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sheet[frames[Mathf.Min(i, frames.Length - 1)]] };

            var clip = new AnimationClip { frameRate = fps };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // 상태 이름 = 클립 이름. 전이는 만들지 않고 PlayerAnimator 가 Play 로 직접 지정한다.
        // 이미 있는 컨트롤러는 빠진 상태만 추가한다
        internal static AnimatorController EnsureAnimatorController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (AnimationClip clip in clips)
                if (stateMachine.states.All(s => s.state.name != clip.name))
                    stateMachine.AddState(clip.name).motion = clip;
            if (stateMachine.defaultState == null)
                stateMachine.defaultState = stateMachine.states[0].state;
            return controller;
        }

        static Sprite[] LoadSheet(string fileName) =>
            AssetDatabase.LoadAllAssetsAtPath(CyborgRoot + fileName).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();

        static T EnsureAsset<T>(string path, System.Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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
