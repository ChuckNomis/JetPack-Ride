using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Pooling;

public class ObjectPoolManagerTests
{
    private (ObjectPoolManager pool, GameObject prefab) Build(int poolableComponents = 1)
    {
        var prefab = new GameObject("Probe");
        for (int i = 0; i < poolableComponents; i++) prefab.AddComponent<ProbePoolable>();
        prefab.SetActive(false);

        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();

        var entryType = typeof(ObjectPoolManager).GetNestedType("PoolEntry");
        var entry = System.Activator.CreateInstance(entryType);
        entryType.GetField("id").SetValue(entry, "probe");
        entryType.GetField("prefab").SetValue(entry, prefab);
        entryType.GetField("defaultCapacity").SetValue(entry, 2);
        entryType.GetField("maxSize").SetValue(entry, 5);

        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
        var list = (System.Collections.IList)System.Activator.CreateInstance(listType);
        list.Add(entry);

        typeof(ObjectPoolManager).GetField("poolEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(pool, list);

        return (pool, prefab);
    }

    [UnityTest]
    public IEnumerator Spawn_ReturnsActiveInstance_WithOnSpawnedCalled()
    {
        var (pool, prefab) = Build();
        yield return null;

        var instance = pool.Spawn("probe", Vector3.zero, Quaternion.identity);

        Assert.IsTrue(instance.activeSelf);
        Assert.AreEqual(1, instance.GetComponent<ProbePoolable>().SpawnedCount);
        Object.DestroyImmediate(pool.gameObject);
        Object.DestroyImmediate(prefab);
    }

    [UnityTest]
    public IEnumerator Despawn_ThenSpawnAgain_ReusesSameInstance()
    {
        var (pool, prefab) = Build();
        yield return null;

        var first = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        pool.Despawn("probe", first);
        var second = pool.Spawn("probe", Vector3.zero, Quaternion.identity);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, second.GetComponent<ProbePoolable>().DespawnedCount);
        Object.DestroyImmediate(pool.gameObject);
        Object.DestroyImmediate(prefab);
    }

    [UnityTest]
    public IEnumerator Despawn_DeactivatesInstance()
    {
        var (pool, prefab) = Build();
        yield return null;

        var instance = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        pool.Despawn("probe", instance);

        Assert.IsFalse(instance.activeSelf);
        Object.DestroyImmediate(pool.gameObject);
        Object.DestroyImmediate(prefab);
    }

    [UnityTest]
    public IEnumerator DespawnAll_DeactivatesEverySpawnedInstance()
    {
        var (pool, prefab) = Build();
        yield return null;

        var a = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        var b = pool.Spawn("probe", Vector3.zero, Quaternion.identity);

        pool.DespawnAll();

        Assert.IsFalse(a.activeSelf);
        Assert.IsFalse(b.activeSelf);
        Object.DestroyImmediate(pool.gameObject);
        Object.DestroyImmediate(prefab);
    }

    [UnityTest]
    public IEnumerator Despawn_Twice_DoesNotHandOutSameInstanceTwice()
    {
        var (pool, prefab) = Build();
        yield return null;

        var instance = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        pool.Despawn("probe", instance);
        pool.Despawn("probe", instance); // e.g. self-despawn racing DespawnAll

        var a = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        var b = pool.Spawn("probe", Vector3.zero, Quaternion.identity);

        Assert.AreNotSame(a, b);
        Assert.AreEqual(1, instance.GetComponent<ProbePoolable>().DespawnedCount);
        Object.DestroyImmediate(pool.gameObject);
        Object.DestroyImmediate(prefab);
    }

    [UnityTest]
    public IEnumerator Spawn_NotifiesEveryPoolableComponent()
    {
        // Pooled prefabs combine HazardMover with RocketBehaviour/CoinBehaviour; all must be reset.
        var (pool, prefab) = Build(poolableComponents: 2);
        yield return null;

        var instance = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        pool.Despawn("probe", instance);

        foreach (var probe in instance.GetComponents<ProbePoolable>())
        {
            Assert.AreEqual(1, probe.SpawnedCount);
            Assert.AreEqual(1, probe.DespawnedCount);
        }
        Object.DestroyImmediate(pool.gameObject);
        Object.DestroyImmediate(prefab);
    }
}
