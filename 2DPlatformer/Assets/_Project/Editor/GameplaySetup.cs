using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 11~25단계 셋업: 갈고리·전투 설정, 이펙트(셰이더 머티리얼·프리팹·파티클), 플레이어 컴포넌트,
    /// 적 프리팹 3종, 물리 레이어 충돌, TestMap 의 적 배치. 이미 있는 에셋·오브젝트는 건드리지 않는다.
    /// </summary>
    public static class GameplaySetup
    {
        const string SettingsFolder = "Assets/_Project/Settings";
        const string HookConfigPath = SettingsFolder + "/HookConfig.asset";
        const string CombatConfigPath = SettingsFolder + "/CombatConfig.asset";
        const string FXArtFolder = "Assets/_Project/Art/FX";
        const string SquarePath = FXArtFolder + "/fx_square.png";
        const string MaterialFolder = "Assets/_Project/Materials/FX";
        const string FXPrefabFolder = "Assets/_Project/Prefabs/FX";
        const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemies";
        const string LibraryPath = "Assets/_Project/Resources/FXLibrary.asset";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";
        const string CraftpixRoot = "Assets/ThirdParty/Free 3 Cyberpunk Sprites Pixel Art/";
        const string MiscRoot = "Assets/ThirdParty/warped city files/warped city files/Assets/SPRITES/misc/";

        /// <summary>8~26단계 셋업을 순서대로 모두 실행한다. 각 단계는 이미 된 부분을 건너뛰므로 다시 실행해도 안전하다.</summary>
        [MenuItem("Tools/Project Setup/Step 8~26 (Run All)", priority = 6)]
        public static void RunAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            RenderingSetup.SetupSpriteShader();
            NormalMapGenerator.GenerateAll();
            RenderingSetup.SetupLights();
            PostProcessSetup.Run();
            Run();
            PolishSetup.Run();
            Debug.Log("[Setup] 8~26단계 셋업 완료 — TestMap 을 열고 플레이하세요");
        }

        [MenuItem("Tools/Project Setup/Step 11~25 (Hook, Combat, FX)", priority = 21)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (string folder in new[] { SettingsFolder, FXArtFolder, MaterialFolder, FXPrefabFolder, EnemyPrefabFolder, "Assets/_Project/Resources" })
                RenderingSetup.EnsureFolder(folder);
            RenderingSetup.EnsureMaterials();

            var hookConfig = EnsureAsset(HookConfigPath, () => ScriptableObject.CreateInstance<HookConfig>());
            var combatConfig = EnsureAsset(CombatConfigPath, () => ScriptableObject.CreateInstance<CombatConfig>());
            Sprite square = EnsureSquareSprite();

            Material chain = EnsureMaterial("Chain", "Platformer/Chain");
            Material slash = EnsureMaterial("Slash", "Platformer/Slash");
            Material spark = EnsureMaterial("HitSpark", "Platformer/HitSpark");
            Material afterimage = EnsureMaterial("Afterimage", "Platformer/Afterimage");
            Material dust = EnsureMaterial("Dust", "Platformer/Dust");
            Material sparkDust = EnsureMaterial("SparkDust", "Platformer/Dust", m =>
            {
                m.SetFloat("_GridSize", 3f);
                m.SetColor("_Emission", new Color(0.4f, 2.2f, 2.6f, 1f)); // 갈고리 불꽃은 스스로 빛난다
            });
            Material billboard = EnsureMaterial("Billboard", "Platformer/Billboard");
            var character = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.CharacterMaterialPath);

            GameObject slashPrefab = EnsurePrefab(FXPrefabFolder + "/Slash.prefab", () => QuadFX("Slash", square, slash, 0.2f));
            GameObject sparkPrefab = EnsurePrefab(FXPrefabFolder + "/HitSpark.prefab", () => QuadFX("HitSpark", square, spark, 0.16f));
            GameObject shotImpact = EnsurePrefab(FXPrefabFolder + "/ShotImpact.prefab", () => SpriteFXPrefab("ShotImpact", Frames(MiscRoot + "shot-hit/shot-hit-", 3), 18f));
            GameObject explosion = EnsurePrefab(FXPrefabFolder + "/Explosion.prefab", () => SpriteFXPrefab("Explosion", Frames(MiscRoot + "enemy-explosion/enemy-explosion-", 6), 14f));
            GameObject dustPrefab = EnsurePrefab(FXPrefabFolder + "/Dust.prefab", () => ParticlePrefab("Dust", dust, sparks: false));
            GameObject sparksPrefab = EnsurePrefab(FXPrefabFolder + "/Sparks.prefab", () => ParticlePrefab("Sparks", sparkDust, sparks: true));
            GameObject shotPrefab = EnsurePrefab(FXPrefabFolder + "/EnemyShot.prefab", () => ShotPrefab(shotImpact));

            var library = EnsureAsset(LibraryPath, () => ScriptableObject.CreateInstance<FXLibrary>());
            library.slash = slashPrefab;
            library.hitSpark = sparkPrefab;
            library.shotImpact = shotImpact;
            library.explosion = explosion;
            EditorUtility.SetDirty(library);

            PlayerSetup.EnsureAnimatorController(PlayerSetup.EnsureClips());
            ConfigurePlayer(hookConfig, combatConfig, chain, afterimage, billboard, square, dustPrefab, sparksPrefab);

            GameObject punk = EnsurePrefab(EnemyPrefabFolder + "/Punk.prefab", () => MeleePrefab(character));
            GameObject turret = EnsurePrefab(EnemyPrefabFolder + "/Turret.prefab", () => ShooterPrefab("Turret", character, shotPrefab, explosion, flying: false));
            GameObject drone = EnsurePrefab(EnemyPrefabFolder + "/Drone.prefab", () => ShooterPrefab("Drone", character, shotPrefab, explosion, flying: true));

            // 몸끼리는 서로 막지 않는다 (판정은 Overlap 질의로 따로 한다 — 질의는 충돌 매트릭스와 무관)
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);

            AssetDatabase.SaveAssets();
            PlaceEnemies();
            Debug.Log("[Gameplay] 갈고리·전투·이펙트·적 셋업 완료");
        }

        static void ConfigurePlayer(HookConfig hookConfig, CombatConfig combatConfig, Material chain, Material afterimage,
            Material billboard, Sprite square, GameObject dustPrefab, GameObject sparksPrefab)
        {
            RenderingSetup.EditPrefab(PlayerPrefabPath, root =>
            {
                int solid = LayerMask.GetMask("Ground", "Hookable");
                int enemies = LayerMask.GetMask("Enemy");
                Transform visual = root.transform.Find("Visual");

                SetFields(root.GetComponent<PlayerController>(), ("combat", combatConfig));
                if (root.GetComponent<Health>() == null)
                    root.AddComponent<Health>();

                // 에디터의 GetComponent 는 없을 때 "가짜 null" 을 돌려줄 수 있어서 ?? 대신 == null 로 비교한다
                var hook = root.GetComponent<GrappleHook>();
                if (hook == null)
                    hook = root.AddComponent<GrappleHook>();
                SetFields(hook, ("config", hookConfig), ("solidMask", solid), ("enemyMask", enemies));

                if (root.GetComponent<ChainRenderer>() == null)
                {
                    var chainObject = new GameObject("Chain");
                    chainObject.transform.SetParent(root.transform, false);
                    var line = chainObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = chain;
                    line.widthMultiplier = 3f / PixelGrid.PixelsPerUnit; // 3px
                    line.numCapVertices = 0;
                    line.alignment = LineAlignment.View;
                    line.textureMode = LineTextureMode.Stretch;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.sortingLayerName = "Player";
                    line.sortingOrder = 3;
                    line.enabled = false;
                    SetFields(root.AddComponent<ChainRenderer>(), ("line", line));
                }

                if (root.GetComponent<AimReticle>() == null)
                {
                    SpriteRenderer Billboard(string name)
                    {
                        var go = new GameObject(name);
                        go.transform.SetParent(root.transform, false);
                        var spriteRenderer = go.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = square;
                        spriteRenderer.sharedMaterial = billboard;
                        spriteRenderer.sortingLayerName = "UI";
                        spriteRenderer.sortingOrder = 10;
                        return spriteRenderer;
                    }
                    SetFields(root.AddComponent<AimReticle>(), ("reticle", Billboard("Reticle")), ("marker", Billboard("Target Marker")));
                }

                if (root.GetComponent<DustFX>() == null)
                    SetFields(root.AddComponent<DustFX>(),
                        ("dustPrefab", dustPrefab.GetComponent<ParticleSystem>()),
                        ("sparkPrefab", sparksPrefab.GetComponent<ParticleSystem>()));

                if (root.GetComponent<AfterimageEmitter>() == null)
                    SetFields(root.AddComponent<AfterimageEmitter>(),
                        ("source", visual.GetComponent<SpriteRenderer>()), ("material", afterimage));
            });
        }

        // ----- 적 -----

        static GameObject MeleePrefab(Material material)
        {
            string punk = CraftpixRoot + "2 Punk/Punk_";
            var root = EnemyRoot("Punk", material, out Transform visual, out SpriteAnimator animator, out SpriteFX spriteFX);
            animator.SetClips(new[]
            {
                Clip("Idle", Sheet(punk + "idle.png"), 8f, true),
                Clip("Run", Sheet(punk + "run.png"), 12f, true),
                Clip("Attack", Sheet(punk + "attack1.png"), 12f, false),
                Clip("Hurt", Sheet(punk + "hurt.png"), 10f, false),
                Clip("Death", Sheet(punk + "death.png"), 10f, false),
            });

            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 4f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 1.9f);
            box.offset = new Vector2(0f, 0.95f);
            box.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/_Project/Settings/NoFriction.physicsMaterial2D");
            SetFields(root.GetComponent<Health>(), ("maxHealth", 3));

            var enemy = root.AddComponent<MeleeEnemy>();
            SetFields(enemy, ("visual", visual), ("animator", animator), ("spriteFX", spriteFX), ("solidMask", LayerMask.GetMask("Ground", "Hookable")));
            return root;
        }

        static GameObject ShooterPrefab(string name, Material material, GameObject shot, GameObject explosion, bool flying)
        {
            var root = EnemyRoot(name, material, out Transform visual, out SpriteAnimator animator, out SpriteFX spriteFX);
            Sprite[] frames = flying ? Frames(MiscRoot + "drone/drone-", 4) : Frames(MiscRoot + "turret/turret-", 6);
            Sprite[] idle = flying ? frames : new[] { frames[0] };
            animator.SetClips(new[]
            {
                Clip("Idle", idle, 10f, true),
                Clip("Attack", frames, 14f, false),
                Clip("Death", idle, 10f, true),
            });

            // 터렛은 바닥에 서 있도록 그림을 올리고, 드론은 중심에 둔다
            visual.localPosition = flying ? Vector3.zero : new Vector3(0f, 0.75f, 0f);
            var box = root.AddComponent<BoxCollider2D>();
            box.size = flying ? new Vector2(2.2f, 1.8f) : new Vector2(1.2f, 1.3f);
            box.offset = flying ? Vector2.zero : new Vector2(0f, 0.65f);
            box.isTrigger = true;
            SetFields(root.GetComponent<Health>(), ("maxHealth", 2));

            var enemy = root.AddComponent<ShooterEnemy>();
            SetFields(enemy,
                ("visual", visual), ("animator", animator), ("spriteFX", spriteFX),
                ("deathEffect", explosion),
                ("hookPointOffset", flying ? Vector2.zero : new Vector2(0f, 0.75f)),
                ("solidMask", LayerMask.GetMask("Ground", "Hookable")),
                ("projectilePrefab", shot.GetComponent<EnemyProjectile>()),
                ("muzzleOffset", flying ? new Vector2(0f, -0.4f) : new Vector2(0f, 1.1f)),
                ("hoverAmplitude", flying ? 0.5f : 0f),
                ("patrolDistance", flying ? 3f : 0f),
                ("fireInterval", flying ? 2.2f : 1.6f));
            return root;
        }

        static GameObject EnemyRoot(string name, Material material, out Transform visual, out SpriteAnimator animator, out SpriteFX spriteFX)
        {
            var root = new GameObject(name) { layer = LayerMask.NameToLayer("Enemy") };
            root.AddComponent<Health>();
            var visualObject = new GameObject("Visual") { layer = root.layer };
            visualObject.transform.SetParent(root.transform, false);
            var spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.sortingLayerName = "Enemy";
            animator = visualObject.AddComponent<SpriteAnimator>();
            spriteFX = visualObject.AddComponent<SpriteFX>();
            visual = visualObject.transform;
            return root;
        }

        static void PlaceEnemies()
        {
            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);

            // 2단계에서 만든 Hazard 타일맵이 실제로 피해를 주게 한다
            GameObject hazard = GameObject.Find("Grid/Hazard");
            if (hazard != null && hazard.GetComponent<HazardZone>() == null)
                hazard.AddComponent<HazardZone>();

            if (GameObject.Find("Enemies") != null)
            {
                EditorSceneManager.SaveScene(scene);
                return;
            }

            // 씬을 연 뒤에 불러온다 — 씬을 열면 쓰이지 않는 에셋이 언로드된다
            var punk = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/Punk.prefab");
            var turret = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/Turret.prefab");
            var drone = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/Drone.prefab");
            Transform parent = new GameObject("Enemies").transform;
            (GameObject prefab, Vector2 position)[] placements =
            {
                (punk, new Vector2(12f, -7f)),
                (drone, new Vector2(26f, 2f)),   // 공중: 갈고리 처형 연습
                (punk, new Vector2(38f, -7f)),
                (turret, new Vector2(50f, -7f)),
                (drone, new Vector2(66f, 1f)),
                (punk, new Vector2(80f, -7f)),
                (turret, new Vector2(92f, -7f)),
            };
            foreach (var (prefab, position) in placements)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(parent, false);
                instance.transform.position = position;
            }
            EditorSceneManager.SaveScene(scene);
        }

        // ----- 이펙트 -----

        static GameObject QuadFX(string name, Sprite square, Material material, float duration)
        {
            var root = new GameObject(name);
            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = square;
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.sortingLayerName = "FX";
            SetFields(root.AddComponent<TimedFX>(), ("duration", duration));
            return root;
        }

        static GameObject SpriteFXPrefab(string name, Sprite[] frames, float fps)
        {
            var root = new GameObject(name);
            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames[0];
            spriteRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.NeonMaterialPath); // 폭발은 스스로 빛난다
            spriteRenderer.sortingLayerName = "FX";
            root.AddComponent<SpriteAnimator>().SetClips(new[] { Clip("Play", frames, fps, false) });
            SetFields(root.AddComponent<TimedFX>(), ("duration", frames.Length / fps));
            return root;
        }

        static GameObject ShotPrefab(GameObject impact)
        {
            var root = new GameObject("EnemyShot");
            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            Sprite[] frames = Frames(MiscRoot + "shot/shot-", 3);
            spriteRenderer.sprite = frames[0];
            spriteRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.NeonMaterialPath);
            spriteRenderer.sortingLayerName = "FX";
            root.AddComponent<SpriteAnimator>().SetClips(new[] { Clip("Fly", frames, 12f, true) });
            SetFields(root.AddComponent<EnemyProjectile>(),
                ("hitMask", LayerMask.GetMask("Ground", "Hookable", "Player")),
                ("impactPrefab", impact));
            return root;
        }

        static GameObject ParticlePrefab(string name, Material material, bool sparks)
        {
            var root = new GameObject(name);
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.startLifetime = sparks ? new ParticleSystem.MinMaxCurve(0.15f, 0.3f) : new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = sparks ? new ParticleSystem.MinMaxCurve(6f, 12f) : new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startSize = sparks ? new ParticleSystem.MinMaxCurve(0.15f, 0.3f) : new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = sparks
                ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.85f, 1f))
                : new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.58f, 0.72f, 0.9f), new Color(0.45f, 0.42f, 0.55f, 0.8f));
            main.gravityModifier = sparks ? 1.5f : -0.05f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            // 위쪽 반원으로 퍼진다 — 바닥에서 일어나는 먼지
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = sparks ? 0.1f : 0.35f;
            shape.arc = sparks ? 360f : 180f;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "FX";
            // Dust.shader 가 TEXCOORD0.z/.w 로 수명 비율과 난수를 받는다
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AgePercent,
                ParticleSystemVertexStream.StableRandomX,
            });
            return root;
        }

        // ----- 에셋 도우미 -----

        // 이펙트 쿼드용 흰 정사각형 (16px = 1유닛). 셰이더가 UV 만 쓰므로 내용은 상관없다
        static Sprite EnsureSquareSprite()
        {
            if (!File.Exists(SquarePath))
            {
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                texture.SetPixels32(Enumerable.Repeat(new Color32(255, 255, 255, 255), 256).ToArray());
                File.WriteAllBytes(SquarePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SquarePath, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        }

        static Material EnsureMaterial(string name, string shaderName, Action<Material> configure = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find(shaderName));
            configure?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject EnsurePrefab(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;
            GameObject root = build();
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static T EnsureAsset<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Sprite[] Sheet(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();

        static Sprite[] Frames(string prefix, int count) =>
            Enumerable.Range(1, count).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{prefix}{i}.png")).ToArray();

        static SpriteAnimator.Clip Clip(string name, Sprite[] frames, float fps, bool loop) =>
            new() { name = name, frames = frames, fps = fps, loop = loop };

        // 직렬화 필드를 이름으로 설정 (private [SerializeField] 포함)
        static void SetFields(Object target, params (string name, object value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (name, value) in fields)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property == null)
                {
                    Debug.LogError($"[Gameplay] {target.GetType().Name}.{name} 필드를 찾지 못했습니다");
                    continue;
                }
                switch (value)
                {
                    case Object reference: property.objectReferenceValue = reference; break;
                    case LayerMask mask: property.intValue = mask.value; break;
                    case int number when property.propertyType == SerializedPropertyType.LayerMask: property.intValue = number; break;
                    case int number: property.intValue = number; break;
                    case float number: property.floatValue = number; break;
                    case bool flag: property.boolValue = flag; break;
                    case Vector2 vector: property.vector2Value = vector; break;
                    default: Debug.LogError($"[Gameplay] {name}: 지원하지 않는 값 {value}"); break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
