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
    public IEnumerator SpawnObstacleNow_HighRampCluster_KeepsSharedOpenLane()
    {
        var (spawner, manager, pool) = Build();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(5000f); // past the ramp: clusters of 2

        // Independent random Ys can put one zapper high and the next low 2.5 units later, which
        // is unfair at max scroll speed. The pair must stay within MaxClusterYDelta of each other.
        for (int trial = 0; trial < 50; trial++)
        {
            pool.DespawnAll();
            spawner.SpawnObstacleNow();
            var ys = new System.Collections.Generic.List<float>();
            foreach (Transform child in pool.transform)
            {
                if (child.gameObject.activeSelf) ys.Add(child.position.y);
            }
            Assert.AreEqual(2, ys.Count);
            Assert.LessOrEqual(Mathf.Abs(ys[0] - ys[1]), SpawnManager.MaxClusterYDelta + 1e-4f);
        }
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
        Assert.GreaterOrEqual(spawned, 1);
        Assert.AreEqual(spawned, active);
    }

    [Test]
    public void CoinPatternOffsets_Chain_IsEvenlySpacedArc()
    {
        var offsets = SpawnManager.CoinPatternOffsets(5, spacing: 0.8f, arcHeight: 1f);

        Assert.AreEqual(5, offsets.Length);
        for (int i = 1; i < offsets.Length; i++) Assert.AreEqual(0.8f, offsets[i].x - offsets[i - 1].x, 1e-4f);
        Assert.AreEqual(0f, offsets[0].y, 1e-4f);
        Assert.AreEqual(0f, offsets[4].y, 1e-4f);
        Assert.AreEqual(1f, offsets[2].y, 1e-4f, "middle coin is the arc's peak");
    }

    [Test]
    public void DetermineRocketVolleySize_SingleEarly_PairLate()
    {
        Assert.AreEqual(1, SpawnManager.DetermineRocketVolleySize(0.3f));
        Assert.AreEqual(1, SpawnManager.DetermineRocketVolleySize(0.5f));
        Assert.AreEqual(2, SpawnManager.DetermineRocketVolleySize(0.7f));
    }

    [Test]
    public void PickSecondRocketY_StaysInRange_AndSeparated()
    {
        var range = new Vector2(-3.5f, 3.5f);
        foreach (float firstY in new[] { -3.5f, -1f, 0f, 2f, 3.5f })
        foreach (float r in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            float y = SpawnManager.PickSecondRocketY(firstY, range, SpawnManager.MinRocketPairGap, r);
            Assert.GreaterOrEqual(y, range.x - 1e-4f);
            Assert.LessOrEqual(y, range.y + 1e-4f);
            Assert.GreaterOrEqual(Mathf.Abs(y - firstY), SpawnManager.MinRocketPairGap - 1e-4f, $"first={firstY} r={r}");
        }
    }
}
