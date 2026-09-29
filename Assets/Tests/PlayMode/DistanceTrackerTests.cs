using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;

public class DistanceTrackerTests
{
    private (DistanceTracker tracker, GameManager manager) Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);

        var trackerGo = new GameObject("DistanceTracker");
        var tracker = trackerGo.AddComponent<DistanceTracker>();
        typeof(DistanceTracker).GetField("gameManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(tracker, manager);
        typeof(DistanceTracker).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(tracker, config);

        return (tracker, manager);
    }

    [UnityTest]
    public IEnumerator Tick_WhileRunning_AdvancesDistanceByBaseScrollSpeed()
    {
        var (tracker, manager) = Build();
        yield return null;
        manager.BeginRun();

        tracker.Tick(1f);

        Assert.AreEqual(manager.Config.BaseScrollSpeed, manager.DistanceMeters, 0.001f);
    }

    [UnityTest]
    public IEnumerator Tick_WhileNotRunning_DoesNotAdvanceDistance()
    {
        var (tracker, manager) = Build();
        yield return null;

        tracker.Tick(1f);

        Assert.AreEqual(0f, manager.DistanceMeters, 0.001f);
    }
}
