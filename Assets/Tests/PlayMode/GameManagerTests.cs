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

    [UnityTest]
    public IEnumerator Pause_WhileRunning_StopsTimeAndAudio_ResumeRestores()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        var events = new System.Collections.Generic.List<bool>();
        manager.PausedChanged += events.Add;

        manager.Pause();
        Assert.IsTrue(manager.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(AudioListener.pause);
        Assert.AreEqual(GameState.Running, manager.CurrentState, "pause is a flag, not a state change");

        manager.Resume();
        Assert.IsFalse(manager.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(AudioListener.pause);
        CollectionAssert.AreEqual(new[] { true, false }, events);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator Pause_OutsideARun_IsIgnored()
    {
        var manager = NewManager();
        yield return null;

        manager.Pause(); // GetReady
        Assert.IsFalse(manager.IsPaused);
        manager.BeginRun();
        manager.EndRun();
        manager.TogglePause(); // GameOver
        Assert.IsFalse(manager.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator RestartRun_WhilePaused_UnpausesAndReplaysIntroViaGetReady()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        int highScoreBefore = manager.HighScore;
        manager.AddDistance(highScoreBefore + 50f);
        manager.Pause();
        var states = new System.Collections.Generic.List<GameState>();
        manager.StateChanged += states.Add;

        manager.RestartRun();

        Assert.IsFalse(manager.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
        CollectionAssert.AreEqual(new[] { GameState.GetReady, GameState.Intro }, states);
        Assert.AreEqual(0f, manager.DistanceMeters);
        Assert.AreEqual(highScoreBefore, manager.HighScore, "an abandoned run doesn't set a high score");
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator Destroyed_WhilePaused_RestoresTimeScale()
    {
        var manager = NewManager();
        yield return null;
        manager.BeginRun();
        manager.Pause();

        Object.DestroyImmediate(manager.gameObject);

        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(AudioListener.pause);
    }
}
