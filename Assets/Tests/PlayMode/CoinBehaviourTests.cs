using NUnit.Framework;
using UnityEngine;
using JetpackRide.Pickups;

public class CoinBehaviourTests
{
    [Test]
    public void Collect_FirstTime_MarksCollectedAndDeactivates()
    {
        var go = new GameObject("Coin");
        var coin = go.AddComponent<CoinBehaviour>();

        coin.Collect();

        Assert.IsTrue(coin.Collected);
        Assert.IsFalse(go.activeSelf);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Collect_CalledTwice_OnlyCountsOnce()
    {
        var go = new GameObject("Coin");
        var coin = go.AddComponent<CoinBehaviour>();
        int collectCount = 0;
        coin.OnCollected += () => collectCount++;

        coin.Collect();
        coin.Collect();

        Assert.AreEqual(1, collectCount);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void OnSpawned_AfterCollect_CanBeCollectedAgain()
    {
        // Pooled coins are reused; a respawned coin must be collectable.
        var go = new GameObject("Coin");
        var coin = go.AddComponent<CoinBehaviour>();
        int collectCount = 0;
        coin.OnCollected += () => collectCount++;

        coin.Collect();
        coin.OnSpawned();
        coin.Collect();

        Assert.AreEqual(2, collectCount);
        Object.DestroyImmediate(go);
    }
}
