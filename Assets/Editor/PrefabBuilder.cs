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
