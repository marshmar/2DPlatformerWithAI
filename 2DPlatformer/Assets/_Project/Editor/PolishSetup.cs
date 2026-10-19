using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Platformer.EditorTools
{
    /// <summary>
    /// 26단계 폴리싱: 배경 깊이 안개, 비(네온을 받아 반짝이고 바닥에서 튀는 빗줄기), 네온 아래 물웅덩이 반사,
    /// 지형 그림자. 이미 있는 에셋·오브젝트는 건드리지 않는다.
    /// </summary>
    public static class PolishSetup
    {
        const string MaterialFolder = "Assets/_Project/Materials";
        const string RainPrefabPath = "Assets/_Project/Prefabs/FX/Rain.prefab";
        const string SquarePath = "Assets/_Project/Art/FX/fx_square.png";
        const string TestMapScenePath = "Assets/_Project/Scenes/TestMap.unity";
        static readonly Color FogColor = new(0.16f, 0.1f, 0.32f, 1f); // 밤하늘 아래쪽의 보랏빛

        [MenuItem("Tools/Project Setup/Step 26 (Polish)", priority = 25)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            RenderingSetup.EnsureFolder(MaterialFolder + "/FX");
            Material far = EnsureMaterial(MaterialFolder + "/SpriteBackgroundFar.mat", "Platformer/SpriteLitFX", m => Fog(m, 0.5f));
            Material near = EnsureMaterial(MaterialFolder + "/SpriteBackgroundNear.mat", "Platformer/SpriteLitFX", m => Fog(m, 0.22f));
            Material rain = EnsureMaterial(MaterialFolder + "/FX/Rain.mat", "Platformer/Rain");
            Material splash = EnsureMaterial(MaterialFolder + "/FX/Splash.mat", "Platformer/Dust", m =>
            {
                m.SetFloat("_GridSize", 2f);
                m.SetColor("_Emission", new Color(0.15f, 0.25f, 0.4f, 1f));
            });
            Material puddle = EnsureMaterial(MaterialFolder + "/FX/Puddle.mat", "Platformer/Puddle");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(RainPrefabPath) == null)
            {
                GameObject root = RainPrefab(rain, splash);
                PrefabUtility.SaveAsPrefabAsset(root, RainPrefabPath);
                Object.DestroyImmediate(root);
            }

            Scene scene = EditorSceneManager.OpenScene(TestMapScenePath, OpenSceneMode.Single);
            // 씬을 열면 쓰이지 않는 에셋이 언로드되므로 다시 불러온다
            far = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/SpriteBackgroundFar.mat");
            near = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/SpriteBackgroundNear.mat");
            puddle = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/FX/Puddle.mat");

            GameObject backgrounds = GameObject.Find("Backgrounds");
            if (backgrounds != null)
            {
                foreach (var spriteRenderer in backgrounds.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (spriteRenderer.name == "Far Buildings")
                        spriteRenderer.sharedMaterial = far;
                    else if (spriteRenderer.name == "Near Buildings")
                        spriteRenderer.sharedMaterial = near;
                }
            }

            if (GameObject.Find("Rain") == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RainPrefabPath), scene);
                instance.name = "Rain";
            }

            if (GameObject.Find("Puddles") == null)
            {
                Transform parent = new GameObject("Puddles").transform;
                var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
                // 네온 라이트 아래 바닥에 — 반사될 빛이 있는 곳이어야 물웅덩이가 산다
                foreach (float x in new[] { -12f, 2f, 18f, 35f, 52f, 70f, 88f })
                {
                    var go = new GameObject("Puddle");
                    go.transform.SetParent(parent, false);
                    go.transform.position = new Vector3(x, -7.5f, 0f); // 윗변이 바닥선(y = -7)
                    go.transform.localScale = new Vector3(8f, 1f, 1f);
                    var spriteRenderer = go.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = square;
                    spriteRenderer.sharedMaterial = puddle;
                    // 반사 소스(Player 레이어까지 그린 화면)보다 뒤, 디버그 충돌 타일(Tile_Front 100)보다 위
                    spriteRenderer.sortingLayerName = "Tile_Front";
                    spriteRenderer.sortingOrder = 200;
                }
            }

            // 지형이 네온 빛을 가린다. ShadowCaster2D 는 같은 오브젝트의 콜라이더를 그림자 모양으로 자동으로 쓴다
            GameObject collision = GameObject.Find("Grid/Collision");
            if (collision != null && collision.GetComponent<ShadowCaster2D>() == null)
                collision.AddComponent<ShadowCaster2D>();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Polish] 깊이 안개·비·물웅덩이·그림자 설정 완료");
        }

        static void Fog(Material material, float amount)
        {
            material.SetColor("_FogColor", FogColor);
            material.SetFloat("_FogAmount", amount);
            material.SetFloat("_SlowResponse", 1f);
        }

        static GameObject RainPrefab(Material rainMaterial, Material splashMaterial)
        {
            var root = new GameObject("Rain");
            var rain = root.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            root.AddComponent<RainFX>();

            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.prewarm = true; // 시작하자마자 이미 내리고 있게
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.startLifetime = 1.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(24f, 30f);
            // 폭 1px, 길이 8~14px 의 세로 막대
            main.startSize3D = true;
            main.startSizeX = 1f / PixelGrid.PixelsPerUnit;
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSizeZ = 1f;
            main.startRotation = -3f * Mathf.Deg2Rad; // 바람에 살짝 기울어진 빗줄기 (아래 바람 속도와 맞춤)
            main.startColor = new Color(1f, 1f, 1f, 0.8f);

            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 260f;

            // 화면 폭보다 넓은 선분에서 아래로 쏟아진다
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 28f;
            shape.rotation = new Vector3(0f, 0f, 180f);

            ParticleSystem.VelocityOverLifetimeModule wind = rain.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.World;
            wind.x = new ParticleSystem.MinMaxCurve(-1.5f);
            wind.y = new ParticleSystem.MinMaxCurve(0f);
            wind.z = new ParticleSystem.MinMaxCurve(0f);

            // 지형에 닿으면 사라지면서 튀김(서브 이미터)을 남긴다
            ParticleSystem.CollisionModule collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = LayerMask.GetMask("Ground", "Hookable");
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            collision.radiusScale = 0.1f;
            collision.quality = ParticleSystemCollisionQuality.Medium;

            var rainRenderer = root.GetComponent<ParticleSystemRenderer>();
            rainRenderer.sharedMaterial = rainMaterial;
            rainRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            rainRenderer.sortingLayerName = "FX";
            rainRenderer.sortingOrder = 5;

            var splashObject = new GameObject("Splash");
            splashObject.transform.SetParent(root.transform, false);
            var splash = splashObject.AddComponent<ParticleSystem>();
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule splashMain = splash.main;
            splashMain.loop = false;
            splashMain.playOnAwake = false;
            splashMain.duration = 0.1f;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.maxParticles = 500;
            splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.0625f, 0.125f);
            splashMain.startColor = new Color(0.7f, 0.8f, 1f, 0.9f);
            splashMain.gravityModifier = 1.2f;
            ParticleSystem.EmissionModule splashEmission = splash.emission;
            splashEmission.rateOverTime = 0f;
            splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });
            ParticleSystem.ShapeModule splashShape = splash.shape;
            splashShape.shapeType = ParticleSystemShapeType.Circle;
            splashShape.radius = 0.05f;
            splashShape.arc = 180f;
            var splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
            splashRenderer.sharedMaterial = splashMaterial;
            splashRenderer.sortingLayerName = "FX";
            splashRenderer.sortingOrder = 6;
            splashRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AgePercent,
                ParticleSystemVertexStream.StableRandomX,
            });

            ParticleSystem.SubEmittersModule subEmitters = rain.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);

            main.playOnAwake = true;
            return root;
        }

        static Material EnsureMaterial(string path, string shaderName, Action<Material> configure = null)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find(shaderName));
            configure?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
