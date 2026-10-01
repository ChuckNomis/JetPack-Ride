using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using JetpackRide.Hazards;
using JetpackRide.Pickups;

namespace JetpackRide.EditorTools
{
    // Regenerates the pooled entity prefabs (and the tags/sorting layers they rely on) from code,
    // so they're reproducible without hand-assembly in the editor.
    // Batchmode: Unity.exe -batchmode -quit -projectPath <path> -executeMethod JetpackRide.EditorTools.PrefabBuilder.BuildAll
    public static class PrefabBuilder
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string SpriteDir = "Assets/Art/Sprites";
        private const string MainScenePath = "Assets/Scenes/MainGame.unity";
        // Jetpack nozzle, just behind/below the character (player local space, player scale 2).
        private static readonly Vector3 SparkLocalPosition = new(-0.08f, -0.3f, 0f);

        // GDD §7 back-to-front order.
        private static readonly string[] SortingLayers = { "Background", "Decals", "Hazards", "Player", "ForegroundUI" };
        private static readonly string[] Tags = { "Hazard", "Coin" };

        [MenuItem("Jetpack Ride/Build Prefabs")]
        public static void BuildAll()
        {
            EnsureTagsAndSortingLayers();

            BuildPrefab("Obstacle_Zapper", "Zapper1", "Hazards", "Hazard",
                go => go.AddComponent<BoxCollider2D>(),
                typeof(HazardMover), typeof(ObstacleBehaviour), typeof(ZapperShape));

            BuildPrefab("Rocket", "Rocket", "Hazards", "Hazard",
                go => go.AddComponent<BoxCollider2D>(),
                typeof(HazardMover), typeof(RocketBehaviour));

            BuildPrefab("Coin", "Coin", "Decals", "Coin",
                go => go.AddComponent<CircleCollider2D>(),
                typeof(HazardMover), typeof(CoinBehaviour));

            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] Prefabs rebuilt.");
        }

        // Separate from BuildAll so it doesn't overwrite the hand-scaled hazard/coin prefabs.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.BuildRocketWarning
        [MenuItem("Jetpack Ride/Build Rocket Warning")]
        public static void BuildRocketWarning()
        {
            var go = new GameObject("RocketWarning");
            try
            {
                go.transform.localScale = new Vector3(2.5f, 2.5f, 1f); // ~rocket height (43px sprite @ PPU 100)
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite("RocketWarning");
                renderer.sortingLayerName = "Hazards";
                go.AddComponent<RocketWarningIndicator>();
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/RocketWarning.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            RegisterPoolEntry("rocketWarning", $"{PrefabDir}/RocketWarning.prefab", 3, 10);
            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] RocketWarning prefab built and pool entry registered.");
        }

        // 9-slices the four zapper frames (orb end caps as borders) and turns Obstacle_Zapper into a
        // sliced, resizable, flickering beam driven by ZapperShape. Idempotent.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.BuildZapperVariants
        [MenuItem("Jetpack Ride/Build Zapper Variants")]
        public static void BuildZapperVariants()
        {
            var frames = new Sprite[ZapperFrameCount];
            for (int i = 0; i < ZapperFrameCount; i++)
            {
                SliceZapperSprite($"{SpriteDir}/Zapper{i + 1}.png");
                frames[i] = LoadSprite($"Zapper{i + 1}");
            }

            var path = $"{PrefabDir}/Obstacle_Zapper.prefab";
            var zapper = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderer = zapper.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = frames[0].bounds.size;

                if (!zapper.TryGetComponent<ZapperShape>(out var shape)) shape = zapper.AddComponent<ZapperShape>();
                var so = new SerializedObject(shape);
                var framesProp = so.FindProperty("frames");
                framesProp.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(zapper, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(zapper);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] Zapper sprites 9-sliced and Obstacle_Zapper updated.");
        }

        private const int ZapperFrameCount = 4;
        // Orb caps are the top/bottom ~32px of the 46x~110 sprite; only the bolt between stretches.
        private const float ZapperCapPixels = 32f;

        private static void SliceZapperSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // required for sliced drawing
            importer.SetTextureSettings(settings);

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            foreach (var rect in rects) rect.border = new Vector4(0f, ZapperCapPixels, 0f, ZapperCapPixels);
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        // Particle FX (GDD §8.2). Not pooled: one-shots destroy themselves via StopAction.Destroy.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.BuildFx
        [MenuItem("Jetpack Ride/Build Particle FX")]
        public static void BuildFx()
        {
            var material = EnsureParticleMaterial();

            var sparkPath = BuildParticlePrefab("FX_JetpackSpark", material, "Player", ps =>
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.4f), new Color(1f, 0.5f, 0.1f));
                main.gravityModifier = 0.5f;
                var emission = ps.emission;
                emission.rateOverTime = 70f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 15f;
                shape.radius = 0.05f;
                shape.rotation = new Vector3(90f, 0f, 0f); // cone faces -Y: sparks shoot down out of the jetpack
                FadeOut(ps, new Color(1f, 0.6f, 0.1f));
            });

            var sparklePath = BuildParticlePrefab("FX_CoinSparkle", material, "Decals", ps =>
            {
                ConfigureOneShot(ps, burstCount: 18, lifetime: 0.4f, speed: new ParticleSystem.MinMaxCurve(1.5f, 3.5f),
                    size: new ParticleSystem.MinMaxCurve(0.25f, 0.5f),
                    color: new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.8f, 0.2f)));
                FadeOut(ps, new Color(1f, 0.85f, 0.3f));
            });

            var explosionPath = BuildParticlePrefab("FX_Explosion", material, "Player", ps =>
            {
                ConfigureOneShot(ps, burstCount: 45, lifetime: 0.6f, speed: new ParticleSystem.MinMaxCurve(3f, 8f),
                    size: new ParticleSystem.MinMaxCurve(0.5f, 1.2f),
                    color: new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.05f)));
                FadeOut(ps, new Color(0.35f, 0.1f, 0.05f));
            });

            // Coin prefab: sparkle on collect.
            var coinPath = $"{PrefabDir}/Coin.prefab";
            var coin = PrefabUtility.LoadPrefabContents(coinPath);
            try
            {
                var so = new SerializedObject(coin.GetComponent<CoinBehaviour>());
                so.FindProperty("sparklePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(sparklePath);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(coin, coinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(coin);
            }

            // Player in MainGame: spark child + explosion reference.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var player = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Player.PlayerController>()
                ?? throw new InvalidOperationException("No PlayerController in " + MainScenePath);
            var existing = player.transform.Find("FX_JetpackSpark");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var spark = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sparkPath), player.transform);
            spark.transform.localPosition = SparkLocalPosition;

            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("jetpackSpark").objectReferenceValue = spark.GetComponent<ParticleSystem>();
            playerSo.FindProperty("explosionPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(explosionPath);
            playerSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] Particle FX built and wired.");
        }

        private static string BuildParticlePrefab(string prefabName, Material material, string sortingLayer, Action<ParticleSystem> configure)
        {
            var go = new GameObject(prefabName);
            try
            {
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                configure(ps);
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingLayerName = sortingLayer;
                var path = $"{PrefabDir}/{prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, path);
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void ConfigureOneShot(ParticleSystem ps, int burstCount, float lifetime,
            ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size, ParticleSystem.MinMaxGradient color)
        {
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = lifetime;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
        }

        private static void FadeOut(ParticleSystem ps, Color endColor)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(endColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
        }

        // Soft round dot texture + sprite-unlit material, so particles render as glowing dots under
        // the URP 2D renderer (the default particle material isn't drawn by it).
        private static Material EnsureParticleMaterial()
        {
            const string texPath = "Assets/Art/Sprites/FX_SoftDot.png";
            const string matDir = "Assets/Art/Materials";
            const string matPath = matDir + "/FX_Particle.mat";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? throw new InvalidOperationException("URP 2D Sprite-Unlit-Default shader not found");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        // GDD §6 audio: adds/updates the AudioManager in MainGame with its clips and event sources,
        // streams the two long music tracks, and makes MainGame the build's only scene.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.BuildAudio
        [MenuItem("Jetpack Ride/Wire Audio And Build Settings")]
        public static void BuildAudio()
        {
            const string audioDir = "Assets/Audio";
            foreach (var music in new[] { "Gameplay.wav", "mainmenu.wav" })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath($"{audioDir}/{music}");
                var settings = importer.defaultSampleSettings;
                if (settings.loadType == AudioClipLoadType.Streaming) continue;
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var audio = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Audio.AudioManager>();
            if (audio == null) audio = new GameObject("AudioManager").AddComponent<JetpackRide.Audio.AudioManager>();

            var so = new SerializedObject(audio);
            so.FindProperty("gameManager").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.GameManager>();
            so.FindProperty("player").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Player.PlayerController>();
            so.FindProperty("spawner").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Spawning.SpawnManager>();
            AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/{file}")
                ?? throw new InvalidOperationException($"Missing audio clip {audioDir}/{file}");
            so.FindProperty("menuMusic").objectReferenceValue = Clip("mainmenu.wav");
            so.FindProperty("gameplayMusic").objectReferenceValue = Clip("Gameplay.wav");
            so.FindProperty("coinSfx").objectReferenceValue = Clip("right.wav");
            so.FindProperty("deathSfx").objectReferenceValue = Clip("DiedEletricity.wav");
            so.FindProperty("rocketLaunchSfx").objectReferenceValue = Clip("367987__chrisbutler99__launch.wav");
            so.FindProperty("jetpackLoop").objectReferenceValue = Clip("FlyTest.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] Audio wired; build settings set to MainGame.");
        }

        // Death animation on the Player, coin prefab sized to read at game scale (13px sprite),
        // and the spark nozzle position. Idempotent.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.WirePlayerAndCoins
        [MenuItem("Jetpack Ride/Wire Death Animation And Coins")]
        public static void WirePlayerAndCoins()
        {
            var coinPath = $"{PrefabDir}/Coin.prefab";
            var coin = PrefabUtility.LoadPrefabContents(coinPath);
            try
            {
                coin.transform.localScale = new Vector3(4f, 4f, 1f);
                PrefabUtility.SaveAsPrefabAsset(coin, coinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(coin);
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var player = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Player.PlayerController>()
                ?? throw new InvalidOperationException("No PlayerController in " + MainScenePath);

            var animator = player.GetComponent<JetpackRide.Player.PlayerDeathAnimator>()
                ?? player.gameObject.AddComponent<JetpackRide.Player.PlayerDeathAnimator>();
            var so = new SerializedObject(animator);
            so.FindProperty("gameManager").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.GameManager>();
            so.FindProperty("deadSprite").objectReferenceValue = LoadSprite("PlayerDead");
            so.ApplyModifiedPropertiesWithoutUndo();

            var spark = player.transform.Find("FX_JetpackSpark");
            if (spark != null) spark.localPosition = SparkLocalPosition;

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] Death animation, coin scale and spark position wired.");
        }

        // Moves the Player's sprite onto a child "Visual" (so run/tilt/tumble rotate the sprite, never
        // the collider), adds PlayerVisuals, and imports + assigns the run cycle from
        // Art/Sprites/PlayerRun/PlayerRun_<n>.png (sliced from Source/sprites/character-running-frames.png).
        // Idempotent. Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.WirePlayerVisuals
        [MenuItem("Jetpack Ride/Wire Player Visuals")]
        public static void WirePlayerVisuals()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var player = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Player.PlayerController>()
                ?? throw new InvalidOperationException("No PlayerController in " + MainScenePath);

            var visualTransform = player.transform.Find("Visual");
            if (visualTransform == null)
            {
                visualTransform = new GameObject("Visual").transform;
                visualTransform.SetParent(player.transform, false);
                visualTransform.SetAsFirstSibling();
            }
            // TryGetComponent, not GetComponent + ??: a missing component is a Unity fake-null in the editor.
            if (!visualTransform.TryGetComponent<SpriteRenderer>(out var visualRenderer))
                visualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
            if (player.TryGetComponent<SpriteRenderer>(out var rootRenderer))
            {
                EditorUtility.CopySerialized(rootRenderer, visualRenderer);
                UnityEngine.Object.DestroyImmediate(rootRenderer);
            }

            if (player.TryGetComponent<JetpackRide.Player.PlayerDeathAnimator>(out var deathAnimator))
            {
                var deathSo = new SerializedObject(deathAnimator);
                deathSo.FindProperty("target").objectReferenceValue = visualRenderer;
                deathSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!player.TryGetComponent<JetpackRide.Player.PlayerVisuals>(out var visuals))
                visuals = player.gameObject.AddComponent<JetpackRide.Player.PlayerVisuals>();
            var so = new SerializedObject(visuals);
            so.FindProperty("controller").objectReferenceValue = player;
            so.FindProperty("gameManager").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.GameManager>();
            so.FindProperty("target").objectReferenceValue = visualRenderer;
            var runFrames = ImportRunFrames();
            var framesProp = so.FindProperty("runFrames");
            framesProp.arraySize = runFrames.Length;
            for (int i = 0; i < runFrames.Length; i++) framesProp.GetArrayElementAtIndex(i).objectReferenceValue = runFrames[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PrefabBuilder] PlayerVisuals wired ({runFrames.Length} run frames).");
        }

        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.WireStartIntro
        [MenuItem("Jetpack Ride/Wire Start Intro")]
        public static void WireStartIntro()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var player = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Player.PlayerController>()
                ?? throw new InvalidOperationException("No PlayerController in " + MainScenePath);
            var gameManager = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.GameManager>()
                ?? throw new InvalidOperationException("No GameManager in " + MainScenePath);
            var audio = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Audio.AudioManager>()
                ?? throw new InvalidOperationException("No AudioManager in " + MainScenePath);
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>()
                ?? throw new InvalidOperationException("No Camera in " + MainScenePath);

            // TryGetComponent, not GetComponent + ??: a missing component is a Unity fake-null in the editor.
            if (!camera.TryGetComponent<JetpackRide.Environment.CameraShake>(out var shake))
                shake = camera.gameObject.AddComponent<JetpackRide.Environment.CameraShake>();
            if (!player.TryGetComponent<JetpackRide.Core.IntroSequence>(out var intro))
                intro = player.gameObject.AddComponent<JetpackRide.Core.IntroSequence>();

            var so = new SerializedObject(intro);
            so.FindProperty("gameManager").objectReferenceValue = gameManager;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("cameraShake").objectReferenceValue = shake;
            so.ApplyModifiedPropertiesWithoutUndo();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/startExplosion1.mp3")
                ?? throw new InvalidOperationException("Missing Assets/Audio/startExplosion1.mp3");
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("startExplosionSfx").objectReferenceValue = clip;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[PrefabBuilder] Start intro wired (IntroSequence, CameraShake, startExplosionSfx).");
        }

        // Intro wall blast: tumbling steel wall chunks that fly out of the left edge, fall and bounce on
        // the floor, over a fire flash and a smoke puff. Wires it into MainGame's IntroSequence. Idempotent.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.BuildWallBlast
        [MenuItem("Jetpack Ride/Build Intro Wall Blast")]
        public static void BuildWallBlast()
        {
            var dotMaterial = EnsureParticleMaterial();
            var chunkMaterial = EnsureChunkMaterial();
            const string prefabName = "FX_WallBlast";
            // Floor (the player's feet at MinY) relative to the blast centre, which sits blastHeight above MinY.
            const float floorLocalY = -1.1f;

            var root = new GameObject(prefabName);
            try
            {
                // Chunks on the root: it lives longest, so StopAction.Destroy cleans up the children too.
                var chunks = AddParticleSystem(root, chunkMaterial, "Player", sortingOrder: 5);
                var main = chunks.main;
                main.loop = false;
                main.playOnAwake = true;
                main.duration = 0.1f;
                main.stopAction = ParticleSystemStopAction.Destroy;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 17f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.78f, 0.85f), new Color(0.22f, 0.3f, 0.45f));
                main.gravityModifier = 2.2f;
                var emission = chunks.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
                var shape = chunks.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 40f;
                shape.radius = 1.2f;                       // a wall-height section breaking at once
                shape.rotation = new Vector3(-15f, 90f, 0f); // cone faces +X, tipped slightly upward
                var spin = chunks.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-4f * Mathf.PI, 4f * Mathf.PI);
                var fade = chunks.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;

                var floor = new GameObject("Floor").transform; // collision plane normal = its +Y
                floor.SetParent(root.transform, false);
                floor.localPosition = new Vector3(0f, floorLocalY, 0f);
                var collision = chunks.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, floor);
                collision.dampen = 0.45f;
                collision.bounce = 0.35f;
                collision.radiusScale = 0.5f;

                var fire = AddParticleSystem(new GameObject("Fire"), dotMaterial, "Player", sortingOrder: 4, root.transform);
                ConfigureChildBurst(fire, burstCount: 30, lifetime: new ParticleSystem.MinMaxCurve(0.3f, 0.55f),
                    speed: new ParticleSystem.MinMaxCurve(4f, 11f), size: new ParticleSystem.MinMaxCurve(0.8f, 2f),
                    color: new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.35f, 0.05f)), coneAngle: 55f);
                FadeOut(fire, new Color(0.35f, 0.1f, 0.05f));

                var smoke = AddParticleSystem(new GameObject("Smoke"), dotMaterial, "Player", sortingOrder: 3, root.transform);
                ConfigureChildBurst(smoke, burstCount: 18, lifetime: new ParticleSystem.MinMaxCurve(0.9f, 1.4f),
                    speed: new ParticleSystem.MinMaxCurve(1.5f, 4f), size: new ParticleSystem.MinMaxCurve(1.5f, 3f),
                    color: new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.58f, 0.62f, 0.55f), new Color(0.3f, 0.32f, 0.36f, 0.55f)), coneAngle: 70f);
                var grow = smoke.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
                FadeOut(smoke, new Color(0.4f, 0.42f, 0.45f));

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{prefabName}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var intro = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.IntroSequence>()
                ?? throw new InvalidOperationException("No IntroSequence in " + MainScenePath + " (run Wire Start Intro first)");
            var so = new SerializedObject(intro);
            so.FindProperty("wallBlastPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabBuilder] Intro wall blast built and wired.");
        }

        // Pause menu: a dimmed full-screen PausePanel on top of the Canvas with a "Paused" title and
        // Continue / Restart buttons (Art/Sprites/ButtonBlank, the Play Game button with its text
        // removed, 9-sliced so it stretches), driven by a PauseMenu on the UIManager. Also adds the HUD
        // PauseButton (Art/Sprites/pauseButton) at the top right, level with DistanceText. Idempotent.
        // Batchmode: ... -executeMethod JetpackRide.EditorTools.PrefabBuilder.WirePauseMenu
        [MenuItem("Jetpack Ride/Wire Pause Menu")]
        public static void WirePauseMenu()
        {
            var buttonSprite = ImportButtonBlank();
            var pauseSprite = ImportPauseButton();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var gameManager = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Core.GameManager>()
                ?? throw new InvalidOperationException("No GameManager in " + MainScenePath);
            var uiManager = UnityEngine.Object.FindAnyObjectByType<JetpackRide.UI.UIManager>()
                ?? throw new InvalidOperationException("No UIManager in " + MainScenePath);
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>()
                ?? throw new InvalidOperationException("No Canvas in " + MainScenePath);
            // Reuse the HUD's font so the menu matches the rest of the UI.
            var font = canvas.GetComponentInChildren<TMPro.TMP_Text>(true).font;

            var existing = canvas.transform.Find("PausePanel");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var panel = NewUiObject("PausePanel", canvas.transform);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.sizeDelta = Vector2.zero;
            panel.SetAsLastSibling(); // drawn over the HUD
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0.05f, 0.6f);

            var title = NewLabel("Title", panel, font, "Paused", 110f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            title.rectTransform.sizeDelta = new Vector2(900f, 140f);

            var continueButton = NewMenuButton("ContinueButton", panel, buttonSprite, font, "Continue", new Vector2(0f, 20f));
            var restartButton = NewMenuButton("RestartButton", panel, buttonSprite, font, "Restart", new Vector2(0f, -130f));
            panel.gameObject.SetActive(false);

            // Directly under the Canvas, not in HUDPanel: HUDPanel is hidden during the intro, and the
            // button must show there too. Placed just below PausePanel so the dimmed overlay covers it.
            var existingPauseButton = canvas.transform.Find("PauseButton");
            if (existingPauseButton != null) UnityEngine.Object.DestroyImmediate(existingPauseButton.gameObject);
            var pauseButton = NewPauseButton(canvas.transform, pauseSprite);
            pauseButton.transform.SetSiblingIndex(panel.GetSiblingIndex());

            if (!uiManager.TryGetComponent<JetpackRide.UI.PauseMenu>(out var pauseMenu))
                pauseMenu = uiManager.gameObject.AddComponent<JetpackRide.UI.PauseMenu>();
            var so = new SerializedObject(pauseMenu);
            so.FindProperty("gameManager").objectReferenceValue = gameManager;
            so.FindProperty("pausePanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[PrefabBuilder] Pause menu wired.");
        }

        // Caps are the button's rounded ends (17px), so only the plain middle stretches.
        private static Sprite ImportButtonBlank()
        {
            var path = $"{SpriteDir}/ButtonBlank.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path)
                ?? throw new InvalidOperationException("Missing " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(17f, 0f, 17f, 0f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // required for sliced drawing
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return LoadSprite("ButtonBlank");
        }

        // Pixel art drawn far below its 1216px source size, so mipmaps keep it from shimmering. Single
        // mode: the auto-slicer splits the icon into its two bars.
        private static Sprite ImportPauseButton()
        {
            var path = $"{SpriteDir}/pauseButton.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path)
                ?? throw new InvalidOperationException("Missing " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return LoadSprite("pauseButton");
        }

        // Top-right, centred on DistanceText's row (its centre sits 86.5 below the top edge).
        private static UnityEngine.UI.Button NewPauseButton(Transform parent, Sprite sprite)
        {
            const float size = 80f;
            var rect = NewUiObject("PauseButton", parent);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-40f, -86.5f + size / 2f);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.82f, 0.82f, 0.82f); // a click leaves it selected; don't stay lit
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            // Keyboard/gamepad focus never lands here, so Space/Enter can't press it.
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            return button;
        }

        private static RectTransform NewUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMPro.TextMeshProUGUI NewLabel(string name, Transform parent, TMPro.TMP_FontAsset font, string text, float size)
        {
            var label = NewUiObject(name, parent).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Button NewMenuButton(string name, Transform parent, Sprite sprite,
            TMPro.TMP_FontAsset font, string text, Vector2 position)
        {
            var rect = NewUiObject(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(440f, 118f);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f; // caps drawn at 2x, matching the button's height scale
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            // Tint multiplies the sprite, so rest slightly dimmed and light up on hover/selection.
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
            button.colors = colors;

            var label = NewLabel("Label", rect, font, text, 64f);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static ParticleSystem AddParticleSystem(GameObject go, Material material, string sortingLayer,
            int sortingOrder, Transform parent = null)
        {
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return ps;
        }

        // Rightward cone burst for the wall blast's child systems (the root owns the Destroy stop action).
        private static void ConfigureChildBurst(ParticleSystem ps, int burstCount, ParticleSystem.MinMaxCurve lifetime,
            ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size, ParticleSystem.MinMaxGradient color, float coneAngle)
        {
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 0.1f;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 1f;
            shape.rotation = new Vector3(0f, 90f, 0f); // cone faces +X
        }

        // Hard-edged bevelled square (light top-left, dark bottom-right) so debris reads as metal plates.
        private static Material EnsureChunkMaterial()
        {
            const string texPath = "Assets/Art/Sprites/FX_Chunk.png";
            const string matPath = "Assets/Art/Materials/FX_Chunk.mat";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                const int n = 16;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float shade = 0.85f;
                    if (x < 2 || y >= n - 2) shade = 1f;          // lit edges
                    if (x >= n - 2 || y < 2) shade = 0.55f;       // shadowed edges
                    tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
                System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? throw new InvalidOperationException("URP 2D Sprite-Unlit-Default shader not found");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        private const string RunFrameDir = SpriteDir + "/PlayerRun";

        // Imports PlayerRun_0..n with the same settings as PlayerFly (PPU 100, bilinear, no mips,
        // uncompressed, centre pivot) and returns them in frame order.
        private static Sprite[] ImportRunFrames()
        {
            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; ; i++)
            {
                var path = $"{RunFrameDir}/PlayerRun_{i}.png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) break;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                frames.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            }
            return frames.ToArray();
        }

        // Adds (or updates) a pool entry on MainGame's ObjectPoolManager and saves the scene.
        private static void RegisterPoolEntry(string id, string prefabPath, int defaultCapacity, int maxSize)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScenePath);
            var pool = UnityEngine.Object.FindAnyObjectByType<JetpackRide.Pooling.ObjectPoolManager>()
                ?? throw new InvalidOperationException("No ObjectPoolManager in " + MainScenePath);

            var so = new SerializedObject(pool);
            var entries = so.FindProperty("poolEntries");
            int index = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id) index = i;
            }
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }

            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            entry.FindPropertyRelative("defaultCapacity").intValue = defaultCapacity;
            entry.FindPropertyRelative("maxSize").intValue = maxSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        private static void BuildPrefab(string prefabName, string spriteName, string sortingLayer, string tag,
            Func<GameObject, Collider2D> addCollider, params Type[] components)
        {
            var go = new GameObject(prefabName) { tag = tag };
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite(spriteName);
                renderer.sortingLayerName = sortingLayer;

                // Added after the sprite is assigned so the collider auto-sizes to it.
                addCollider(go).isTrigger = true;

                foreach (var component in components) go.AddComponent(component);

                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{prefabName}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static Sprite LoadSprite(string spriteName)
        {
            var path = $"{SpriteDir}/{spriteName}.png";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()
                ?? throw new InvalidOperationException($"No sprite found at {path}");
        }

        private static void EnsureTagsAndSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            var tags = tagManager.FindProperty("tags");
            foreach (var tag in Tags)
            {
                if (Enumerable.Range(0, tags.arraySize).Any(i => tags.GetArrayElementAtIndex(i).stringValue == tag)) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }

            var layers = tagManager.FindProperty("m_SortingLayers");
            foreach (var layer in SortingLayers)
            {
                if (Enumerable.Range(0, layers.arraySize)
                    .Any(i => layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layer)) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var element = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = layer;
                element.FindPropertyRelative("uniqueID").longValue = (uint)layer.GetHashCode();
                element.FindPropertyRelative("locked").boolValue = false;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
