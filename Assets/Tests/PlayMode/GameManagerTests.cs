using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;

public class GameManagerTests
{
    private GameManager NewManager()
    {
        var go = new GameObject("GameManager");
        var manager = go.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager)
            .GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);
        return manager;
    }

    [UnityTest]
    public IEnumerator BeginRun_ResetsStatsAndEntersRunning()
    {
        var manager = NewManager();
        yield return null; // let Awake run

        manager.BeginRun();

        Assert.AreEqual(GameState.Running, manager.CurrentState);
        Assert.AreEqual(0f, manager.DistanceMeters);
        Assert.AreEqual(0, manager.CoinsThisRun);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator CollectCoin_WhileRunning_IncrementsCoinsAndScore()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();

        manager.CollectCoin();

        Assert.AreEqual(1, manager.CoinsThisRun);
        Assert.Greater(manager.Score, 0);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator CollectCoin_AfterEndRun_IsIgnored()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        manager.EndRun();

        manager.CollectCoin();

        Assert.AreEqual(0, manager.CoinsThisRun);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator StateChanged_FiresOnEveryTransition()
    {
        var manager = NewManager();
        yield return null;
        var seen = new System.Collections.Generic.List<GameState>();
        manager.StateChanged += s => seen.Add(s);

        manager.BeginRun();
        manager.EndRun();
        manager.ReturnToGetReady();

        CollectionAssert.AreEqual(
            new[] { GameState.Running, GameState.GameOver, GameState.GetReady },
            seen);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator EndRun_UpdatesHighScore_WhenDistanceExceedsPrevious()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(1500f);

        manager.EndRun();

        Assert.GreaterOrEqual(manager.HighScore, 1500);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator StartIntro_ResetsStatsAndEntersIntro()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(50f);
        manager.CollectCoin();
        manager.EndRun();
        manager.ReturnToGetReady();

        GameState? raised = null;
        manager.StateChanged += s => raised = s;
        manager.StartIntro();

        Assert.AreEqual(GameState.Intro, manager.CurrentState);
        Assert.AreEqual(GameState.Intro, raised);
        Assert.AreEqual(0f, manager.DistanceMeters);
        Assert.AreEqual(0, manager.CoinsThisRun);
        Assert.AreEqual(0, manager.Score);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator DuringIntro_DistanceAndCoinsAreIgnored_ThenBeginRunEntersRunning()
    {
        var manager = NewManager();
        yield return null;
        manager.StartIntro();

        manager.AddDistance(10f);
        manager.CollectCoin();
        Assert.AreEqual(0f, manager.DistanceMeters);
        Assert.AreEqual(0, manager.CoinsThisRun);

        manager.BeginRun();
        Assert.AreEqual(GameState.Running, manager.CurrentState);
        Object.DestroyImmediate(manager.gameObject);
    }
}
