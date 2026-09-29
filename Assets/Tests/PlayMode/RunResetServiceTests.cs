using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Pooling;

public class RunResetServiceTests
{
    [UnityTest]
    public IEnumerator ReturnToGetReady_DespawnsAllPooledObjects()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);

        var prefab = new GameObject("Probe");
        prefab.SetActive(false);
        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var entryType = typeof(ObjectPoolManager).GetNestedType("PoolEntry");
        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
        var list = (System.Collections.IList)System.Activator.CreateInstance(listType);
        var entry = System.Activator.CreateInstance(entryType);
        entryType.GetField("id").SetValue(entry, "probe");
        entryType.GetField("prefab").SetValue(entry, prefab);
        entryType.GetField("defaultCapacity").SetValue(entry, 2);
        entryType.GetField("maxSize").SetValue(entry, 5);
        list.Add(entry);
        typeof(ObjectPoolManager).GetField("poolEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(pool, list);

        var resetGo = new GameObject("RunResetService");
        var reset = resetGo.AddComponent<RunResetService>();
        typeof(RunResetService).GetField("gameManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(reset, manager);
        typeof(RunResetService).GetField("pool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(reset, pool);
        yield return null;

        var instance = pool.Spawn("probe", Vector3.zero, Quaternion.identity);
        manager.BeginRun();
        manager.EndRun();
        manager.ReturnToGetReady();
        yield return null;

        Assert.IsFalse(instance.activeSelf);
    }
}
