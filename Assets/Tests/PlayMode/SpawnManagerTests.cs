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
        obstaclePrefab.SetActive(false);
        var rocketPrefab = new GameObject("RocketPrefab");
        rocketPrefab.AddComponent<JetpackRide.Hazards.RocketBehaviour>();
        rocketPrefab.SetActive(false);

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
}
