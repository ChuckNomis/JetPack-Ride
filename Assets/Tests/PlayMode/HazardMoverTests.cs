using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Hazards;
using JetpackRide.Pooling;

public class HazardMoverTests
{
    [UnityTest]
    public IEnumerator MovesLeftAtConfiguredSpeed()
    {
        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();

        var go = new GameObject("Hazard");
        var mover = go.AddComponent<HazardMover>();
        mover.Configure(pool, "obstacle", speed: 10f, despawnX: -50f);
        go.transform.position = Vector3.zero;
        yield return null;

        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.Less(go.transform.position.x, 0f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(poolGo);
    }

    [UnityTest]
    public IEnumerator OnSpawned_ResetsPastDespawn_ToVisible()
    {
        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var go = new GameObject("Hazard");
        var mover = go.AddComponent<HazardMover>();
        mover.Configure(pool, "obstacle", speed: 10f, despawnX: -50f);
        yield return null;

        mover.OnSpawned();

        Assert.IsFalse(mover.HasDespawned);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(poolGo);
    }

    [UnityTest]
    public IEnumerator PassingDespawnX_ReturnsInstanceToPool()
    {
        var prefab = new GameObject("HazardPrefab");
        prefab.AddComponent<HazardMover>();
        prefab.SetActive(false);

        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var entryType = typeof(ObjectPoolManager).GetNestedType("PoolEntry");
        var entry = System.Activator.CreateInstance(entryType);
        entryType.GetField("id").SetValue(entry, "obstacle");
        entryType.GetField("prefab").SetValue(entry, prefab);
        var list = (System.Collections.IList)System.Activator.CreateInstance(
            typeof(System.Collections.Generic.List<>).MakeGenericType(entryType));
        list.Add(entry);
        typeof(ObjectPoolManager).GetField("poolEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(pool, list);

        var instance = pool.Spawn("obstacle", Vector3.zero, Quaternion.identity);
        instance.GetComponent<HazardMover>().Configure(pool, "obstacle", speed: 10f, despawnX: -0.05f);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsFalse(instance.activeSelf);
        // Released to the pool, so the next Spawn reuses it.
        Assert.AreSame(instance, pool.Spawn("obstacle", Vector3.zero, Quaternion.identity));
        Object.DestroyImmediate(poolGo);
        Object.DestroyImmediate(prefab);
    }
}
