using NUnit.Framework;
using UnityEngine;
using JetpackRide.Core;

// Many tests end a run (GameManager.EndRun), which persists the high score to PlayerPrefs. Without
// this, running the suite overwrites the developer's real high score (e.g. with 100000 m).
// Global (no namespace) SetUpFixture: runs once around every PlayMode test in this assembly.
[SetUpFixture]
public class PlayerPrefsGuard
{
    private static readonly string HighScoreKey = (string)typeof(GameManager)
        .GetField("HighScoreKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        .GetValue(null);

    private bool hadKey;
    private int savedHighScore;

    [OneTimeSetUp]
    public void Snapshot()
    {
        hadKey = PlayerPrefs.HasKey(HighScoreKey);
        savedHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    [OneTimeTearDown]
    public void Restore()
    {
        if (hadKey) PlayerPrefs.SetInt(HighScoreKey, savedHighScore);
        else PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();
    }
}
