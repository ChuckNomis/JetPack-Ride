using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Environment;
using JetpackRide.Player;

public class IntroSequenceTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private class Rig
    {
        public GameManager Manager;
        public PlayerController Player;
        public Rigidbody2D Body;
        public CameraShake Shake;
        public IntroSequence Intro;

        public void Destroy()
        {
            if (Intro != null) Object.DestroyImmediate(Intro.gameObject);
            if (Shake != null) Object.DestroyImmediate(Shake.gameObject);
            Object.DestroyImmediate(Player.gameObject);
            Object.DestroyImmediate(Manager.gameObject);
        }
    }

    // Player starts in the air (as after a mid-air death) at the scene home X.
    private static Rig Build(bool withShake = true)
    {
        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, config);

        var playerGo = new GameObject("Player");
        playerGo.transform.position = new Vector3(-6f, 2f, 0f);
        var body = playerGo.AddComponent<Rigidbody2D>();
        var player = playerGo.AddComponent<PlayerController>();
        typeof(PlayerController).GetField("config", Flags).SetValue(player, config);
        typeof(PlayerController).GetField("gameManager", Flags).SetValue(player, manager);
        player.MinY = -3.5f;
        player.MaxY = 3.5f;

        CameraShake shake = null;
        if (withShake)
        {
            var cam = new GameObject("Camera");
            cam.transform.position = new Vector3(0f, 0f, -10f);
            shake = cam.AddComponent<CameraShake>();
        }

        var intro = new GameObject("Intro").AddComponent<IntroSequence>();
        void Set(string field, object value) => typeof(IntroSequence).GetField(field, Flags).SetValue(intro, value);
        Set("gameManager", manager);
        Set("player", player);
        Set("cameraShake", shake);
        Set("startX", -11f);
        Set("shakeSeconds", 0.05f);
        Set("shakeStrength", 0.3f);
        Set("pauseSeconds", 0.05f);
        Set("walkSeconds", 0.2f);

        return new Rig { Manager = manager, Player = player, Body = body, Shake = shake, Intro = intro };
    }

    private static IEnumerator WaitForState(GameManager manager, GameState state, float timeout)
    {
        for (float t = 0f; t < timeout && manager.CurrentState != state; t += Time.deltaTime) yield return null;
    }

    [UnityTest]
    public IEnumerator StartIntro_PlacesPlayerOffscreen_WalksHome_ThenBeginsRun()
    {
        var rig = Build();
        yield return null; // Start: captures HomeX = -6, subscribes

        rig.Manager.StartIntro();
        Assert.AreEqual(-11f, rig.Body.position.x, 1e-3f, "starts off-screen left");
        Assert.AreEqual(-3.5f, rig.Body.position.y, 1e-3f, "on the floor, even after a mid-air death");

        yield return new WaitForSeconds(0.2f); // mid-walk
        Assert.AreEqual(GameState.Intro, rig.Manager.CurrentState);
        Assert.Greater(rig.Body.position.x, -11f);
        Assert.Less(rig.Body.position.x, -6f);

        yield return WaitForState(rig.Manager, GameState.Running, 2f);
        Assert.AreEqual(GameState.Running, rig.Manager.CurrentState);
        Assert.AreEqual(-6f, rig.Intro.HomeX, 1e-3f);
        Assert.AreEqual(-6f, rig.Body.position.x, 1e-3f, "ends at home X");
        Assert.AreEqual(-3.5f, rig.Body.position.y, 0.05f);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator NoCameraShake_StillBeginsRun()
    {
        var rig = Build(withShake: false);
        yield return null;

        rig.Manager.StartIntro();
        yield return WaitForState(rig.Manager, GameState.Running, 2f);

        Assert.AreEqual(GameState.Running, rig.Manager.CurrentState);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator LeavingIntro_MidSequence_DoesNotBeginRun()
    {
        var rig = Build();
        yield return null;

        rig.Manager.StartIntro();
        yield return new WaitForSeconds(0.02f);
        rig.Manager.ReturnToGetReady();

        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(GameState.GetReady, rig.Manager.CurrentState);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Destroyed_MidSequence_DoesNotBeginRun()
    {
        var rig = Build();
        yield return null;

        rig.Manager.StartIntro();
        yield return new WaitForSeconds(0.12f); // inside the walk
        Object.DestroyImmediate(rig.Intro.gameObject);

        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(GameState.Intro, rig.Manager.CurrentState);
        LogAssert.NoUnexpectedReceived();
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator SecondIntro_AfterFirst_PlaysAgain()
    {
        var rig = Build();
        yield return null;

        rig.Manager.StartIntro();
        yield return WaitForState(rig.Manager, GameState.Running, 2f);
        rig.Manager.EndRun();
        rig.Manager.ReturnToGetReady();

        rig.Manager.StartIntro();
        Assert.AreEqual(-11f, rig.Body.position.x, 1e-3f);
        yield return WaitForState(rig.Manager, GameState.Running, 2f);
        Assert.AreEqual(GameState.Running, rig.Manager.CurrentState);
        Assert.AreEqual(-6f, rig.Body.position.x, 1e-3f);
        rig.Destroy();
    }
}
