using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Pooling;
using JetpackRide.Spawning;

public class SpawnManagerTests
{
    private (SpawnManager spawner, GameManager manager, ObjectPoolManager pool) Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);

        var obstaclePrefab = new GameObject("ObstaclePrefab");
        var zapperSprite = Sprite.Create(new Texture2D(46, 110), new Rect(0, 0, 46, 110), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(0, 32, 0, 32));
        obstaclePrefab.transform.localScale = new Vector3(3f, 3f, 1f);
        obstaclePrefab.AddComponent<SpriteRenderer>().sprite = zapperSprite;
        obstaclePrefab.AddComponent<BoxCollider2D>().isTrigger = true;
        obstaclePrefab.AddComponent<JetpackRide.Hazards.ZapperShape>();
        obstaclePrefab.AddComponent<JetpackRide.Hazards.HazardMover>();
        obstaclePrefab.SetActive(false);
        var rocketPrefab = new GameObject("RocketPrefab");
        rocketPrefab.AddComponent<JetpackRide.Hazards.RocketBehaviour>();
        rocketPrefab.AddComponent<JetpackRide.Hazards.HazardMover>();
        rocketPrefab.SetActive(false);
        var coinPrefab = new GameObject("CoinPrefab");
        coinPrefab.AddComponent<JetpackRide.Pickups.CoinBehaviour>();
        coinPrefab.AddComponent<JetpackRide.Hazards.HazardMover>();
        coinPrefab.SetActive(false);

        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var entryType = typeof(ObjectPoolManager).GetNestedType("PoolEntry");
        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
        var list = (System.Collections.IList)System.Activator.CreateInstance(listType);

        void AddEntry(string id, GameObject prefab)
        {
            var entry = System.Activator.CreateInstance(entryType);
            entryType.GetField("id").SetValue(entry, id);
            entryType.GetField("prefab").SetValue(entry, prefab);
            entryType.GetField("defaultCapacity").SetValue(entry, 2);
            entryType.GetField("maxSize").SetValue(entry, 10);
            list.Add(entry);
        }
        AddEntry("obstacle", obstaclePrefab);
        AddEntry("rocket", rocketPrefab);
        AddEntry("coin", coinPrefab);
        typeof(ObjectPoolManager).GetField("poolEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(pool, list);

        var spawnerGo = new GameObject("SpawnManager");
        var spawner = spawnerGo.AddComponent<SpawnManager>();
        typeof(SpawnManager).GetField("gameManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(spawner, manager);
        typeof(SpawnManager).GetField("pool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(spawner, pool);
        typeof(SpawnManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(spawner, config);
        var spawnPoint = new GameObject("SpawnPoint").transform;
        spawnPoint.position = new Vector3(12f, 0f, 0f);
        typeof(SpawnManager).GetField("spawnPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(spawner, spawnPoint);

        return (spawner, manager, pool);
    }

    [UnityTest]
    public IEnumerator SpawnObstacleNow_CreatesInstanceAtSpawnPointX()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();

        spawner.SpawnObstacleNow();
        yield return null;

        Assert.AreEqual(1, spawner.ActiveObstacleCount);
    }

    [UnityTest]
    public IEnumerator SpawnRocketNow_AtHighDistance_CanProduceHomingRocket()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(100000f); // far past ramp distance -> rocketAggression == 1

        bool sawHoming = false;
        for (int i = 0; i < 20; i++)
        {
            spawner.SpawnRocketNow();
            if (spawner.LastSpawnedRocketWasHoming) sawHoming = true;
        }

        Assert.IsTrue(sawHoming);
    }

    [UnityTest]
    public IEnumerator SpawnRocketNow_ConfiguresFasterThanObstacle()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();

        // Isolate from leftover HazardMover instances other tests spawned into the shared scene.
        foreach (var stale in Object.FindObjectsByType<JetpackRide.Hazards.HazardMover>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(stale.gameObject);
        }

        spawner.SpawnObstacleNow();
        spawner.SpawnRocketNow();
        yield return null;

        var speedField = typeof(JetpackRide.Hazards.HazardMover).GetField("speed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var poolIdField = typeof(JetpackRide.Hazards.HazardMover).GetField("poolId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        float obstacleSpeed = -1f, rocketSpeed = -1f;
        foreach (var mover in Object.FindObjectsByType<JetpackRide.Hazards.HazardMover>(FindObjectsSortMode.None))
        {
            var id = (string)poolIdField.GetValue(mover);
            var speed = (float)speedField.GetValue(mover);
            if (id == SpawnManager.ObstaclePoolId) obstacleSpeed = speed;
            if (id == SpawnManager.RocketPoolId) rocketSpeed = speed;
        }

        Assert.Greater(rocketSpeed, obstacleSpeed);
    }

    [UnityTest]
    public IEnumerator SpawnObstacleNow_SpeedIsScrollSpeedTimesZapperMultiplier()
    {
        var (spawner, manager, pool) = Build();
        var config = (GameConfig)typeof(SpawnManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(spawner);
        typeof(GameConfig).GetField("zapperSpeedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, 0.7f);
        yield return null;
        manager.BeginRun();

        pool.DespawnAll();
        spawner.SpawnObstacleNow();

        var speedField = typeof(JetpackRide.Hazards.HazardMover).GetField("speed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float expected = DifficultyEvaluator.Evaluate(manager.DistanceMeters, config).ScrollSpeed * 0.7f;
        int checkedCount = 0;
        foreach (Transform child in pool.transform)
        {
            if (!child.gameObject.activeSelf || !child.TryGetComponent<JetpackRide.Hazards.HazardMover>(out var mover)) continue;
            Assert.AreEqual(expected, (float)speedField.GetValue(mover), 1e-4f);
            checkedCount++;
        }
        Assert.Greater(checkedCount, 0);
    }

    [UnityTest]
    public IEnumerator StateChangedToGameOver_StopsFurtherAutomaticSpawns()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();
        manager.EndRun();

        int before = spawner.ActiveObstacleCount;
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual(before, spawner.ActiveObstacleCount);
    }
    [UnityTest]
    public IEnumerator SpawnObstacleNow_HighRampCluster_LeavesFlyableLane_AndMixesOrientations()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(5000f); // past the ramp: clusters of 2, all orientations unlocked

        bool sawRotated = false;
        for (int trial = 0; trial < 50; trial++)
        {
            pool.DespawnAll();
            spawner.SpawnObstacleNow();
            Physics2D.SyncTransforms();
            var spans = new System.Collections.Generic.List<Vector2>();
            foreach (Transform child in pool.transform)
            {
                if (!child.gameObject.activeSelf || !child.TryGetComponent<BoxCollider2D>(out var box)) continue;
                spans.Add(new Vector2(box.bounds.min.y, box.bounds.max.y));
                if (Mathf.Abs(Mathf.DeltaAngle(child.eulerAngles.z, 0f)) > 1f) sawRotated = true;
            }
            Assert.AreEqual(2, spans.Count);
            Assert.GreaterOrEqual(JetpackRide.Spawning.ZapperLayout.LargestGap(spans, new Vector2(-3.5f, 3.5f)), SpawnManager.MinZapperLane - 1e-3f);
        }
        Assert.IsTrue(sawRotated, "horizontal/diagonal zappers appear late in a run");
    }

    [UnityTest]
    public IEnumerator SpawnCoinsNow_SpawnsWholePatternAtOrBeyondSpawnPoint()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();

        int spawned = spawner.SpawnCoinsNow();
        yield return null;

        int active = 0;
        foreach (Transform child in pool.transform)
        {
            if (!child.gameObject.activeSelf || child.GetComponent<JetpackRide.Pickups.CoinBehaviour>() == null) continue;
            active++;
            Assert.GreaterOrEqual(child.position.x, 12f - 1f, "coins enter from the right");
        }
        Assert.GreaterOrEqual(spawned, 2);
        Assert.AreEqual(spawned, active);
    }

    [UnityTest]
    public IEnumerator SpawnCoinsNow_AlwaysABatch_InsideTheBand()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();

        foreach (float distance in new[] { 0f, 1000f, 5000f })
        {
            manager.AddDistance(distance);
            for (int i = 0; i < 30; i++)
            {
                pool.DespawnAll();
                int spawned = spawner.SpawnCoinsNow();
                Assert.GreaterOrEqual(spawned, 2, "coins only come in batches");
                foreach (Transform child in pool.transform)
                {
                    if (!child.gameObject.activeSelf || child.GetComponent<JetpackRide.Pickups.CoinBehaviour>() == null) continue;
                    Assert.GreaterOrEqual(child.position.y, -3.5f - 1e-4f);
                    Assert.LessOrEqual(child.position.y, 3.5f + 1e-4f);
                }
            }
        }
    }

    [Test]
    public void DetermineRocketVolleySize_ByDistance()
    {
        // Before 250m: always single.
        foreach (float r in new[] { 0f, 0.5f, 0.99f })
            Assert.AreEqual(1, SpawnManager.DetermineRocketVolleySize(100f, r, 250f, 500f));

        // 250-500m: sometimes 1, sometimes 2, never 3.
        Assert.AreEqual(1, SpawnManager.DetermineRocketVolleySize(300f, 0.1f, 250f, 500f));
        Assert.AreEqual(2, SpawnManager.DetermineRocketVolleySize(300f, 0.9f, 250f, 500f));

        // From 500m: 1, 2 or 3.
        Assert.AreEqual(1, SpawnManager.DetermineRocketVolleySize(600f, 0.1f, 250f, 500f));
        Assert.AreEqual(2, SpawnManager.DetermineRocketVolleySize(600f, 0.5f, 250f, 500f));
        Assert.AreEqual(3, SpawnManager.DetermineRocketVolleySize(600f, 0.9f, 250f, 500f));
    }

    [Test]
    public void PickVolleyYs_StaysInRange_AndSeparated()
    {
        var range = new Vector2(-3.5f, 3.5f);
        var rng = new System.Random(1234);
        for (int trial = 0; trial < 200; trial++)
        {
            int count = 1 + trial % 3;
            var ys = SpawnManager.PickVolleyYs(count, range, SpawnManager.MinRocketPairGap, () => (float)rng.NextDouble());

            Assert.AreEqual(count, ys.Length, "a 7-unit band fits 3 rockets 3 units apart");
            for (int i = 0; i < ys.Length; i++)
            {
                Assert.GreaterOrEqual(ys[i], range.x - 1e-4f);
                Assert.LessOrEqual(ys[i], range.y + 1e-4f);
                for (int j = i + 1; j < ys.Length; j++)
                    Assert.GreaterOrEqual(Mathf.Abs(ys[i] - ys[j]), SpawnManager.MinRocketPairGap - 1e-4f);
            }
        }
    }
}
