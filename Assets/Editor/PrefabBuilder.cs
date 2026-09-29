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
                typeof(HazardMover), typeof(ObstacleBehaviour));

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
