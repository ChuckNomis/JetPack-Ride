using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Audio;
using JetpackRide.Core;

public class AudioManagerTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private (AudioManager audio, GameManager manager, AudioClip menu, AudioClip gameplay) Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, ScriptableObject.CreateInstance<GameConfig>());

        var menu = AudioClip.Create("menu", 441, 1, 44100, false);
        var gameplay = AudioClip.Create("gameplay", 441, 1, 44100, false);

        var audioGo = new GameObject("AudioManager");
        var audio = audioGo.AddComponent<AudioManager>();
        typeof(AudioManager).GetField("gameManager", Flags).SetValue(audio, manager);
        typeof(AudioManager).GetField("menuMusic", Flags).SetValue(audio, menu);
        typeof(AudioManager).GetField("gameplayMusic", Flags).SetValue(audio, gameplay);
        return (audio, manager, menu, gameplay);
    }

    private static void Cleanup(AudioManager audio, GameManager manager)
    {
        Object.DestroyImmediate(audio.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator Music_FollowsGameState()
    {
        var (audio, manager, menu, gameplay) = Build();
        yield return null; // Start subscribes and applies the initial GetReady state

        Assert.AreSame(menu, audio.MusicSource.clip, "title screen plays menu music");

        manager.BeginRun();
        Assert.AreSame(gameplay, audio.MusicSource.clip, "a run plays gameplay music");

        manager.EndRun();
        Assert.IsNull(audio.MusicSource.clip, "music stops on game over");

        manager.ReturnToGetReady();
        Assert.AreSame(menu, audio.MusicSource.clip);

        Cleanup(audio, manager);
    }

    [UnityTest]
    public IEnumerator CoinCollected_PlaysCoinSfx_OnlyWhenCountRises()
    {
        var (audio, manager, _, _) = Build();
        var coinClip = AudioClip.Create("coin", 441, 1, 44100, false);
        typeof(AudioManager).GetField("coinSfx", Flags).SetValue(audio, coinClip);
        yield return null;

        manager.BeginRun(); // resets coins to 0 without raising CoinsChanged
        manager.CollectCoin();
        Assert.AreSame(coinClip, audio.LastSfx);

        Cleanup(audio, manager);
    }

    [UnityTest]
    public IEnumerator JetpackLoop_TracksThrusting()
    {
        var (audio, manager, _, _) = Build();
        yield return null;

        audio.SetJetpackActive(true);
        Assert.IsTrue(audio.JetpackLoopActive);
        audio.SetJetpackActive(false);
        Assert.IsFalse(audio.JetpackLoopActive);

        Cleanup(audio, manager);
    }
}
