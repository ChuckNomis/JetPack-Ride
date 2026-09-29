using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Hazards;
using JetpackRide.Pooling;

public class RocketWarningIndicatorTests
{
    [UnityTest]
    public IEnumerator PlayAndDespawnAsync_DespawnsAfterLifetime()
    {
        var poolGo = new GameObject("Pool");
        var pool = poolGo.AddComponent<ObjectPoolManager>();
        var go = new GameObject("Warning");
        var indicator = go.AddComponent<RocketWarningIndicator>();
        indicator.Configure(pool, "rocketWarning");
        yield return null;

        bool completed = false;
        RunAsync();
        async void RunAsync()
        {
            await indicator.PlayAndDespawnAsync(0.05f);
            completed = true;
        }

        yield return new WaitForSeconds(0.2f);

        Assert.IsTrue(completed);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(poolGo);
    }
}
