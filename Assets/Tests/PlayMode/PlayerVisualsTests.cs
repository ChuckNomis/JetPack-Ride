using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Player;

public class PlayerVisualsTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private class Rig
    {
        public GameObject Go;
        public PlayerController Controller;
        public PlayerVisuals Visuals;
        public SpriteRenderer Renderer;
        public GameManager Manager;
        public Sprite Fly;
        public Sprite[] RunFrames;

        public void Thrust(bool on) => typeof(PlayerVisuals).GetMethod("HandleThrustingChanged", Flags).Invoke(Visuals, new object[] { on });

        public void Destroy()
        {
            Object.DestroyImmediate(Go);
            Object.DestroyImmediate(Manager.gameObject);
        }
    }

    private static Sprite MakeSprite() => Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

    private static Rig Build(bool withRunFrames = true, float startY = -3.5f, bool childVisual = false)
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, config);

        var go = new GameObject("Player");
        go.transform.position = new Vector3(-6f, startY, 0f);
        go.AddComponent<Rigidbody2D>();
        var visualGo = go;
        if (childVisual)
        {
            visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, false);
        }
        var renderer = visualGo.AddComponent<SpriteRenderer>();
        var fly = MakeSprite();
        renderer.sprite = fly;
        var controller = go.AddComponent<PlayerController>();
        typeof(PlayerController).GetField("config", Flags).SetValue(controller, config);
        typeof(PlayerController).GetField("gameManager", Flags).SetValue(controller, manager);
        controller.MinY = -3.5f;
        controller.MaxY = 3.5f;

        var visuals = go.AddComponent<PlayerVisuals>();
        typeof(PlayerVisuals).GetField("controller", Flags).SetValue(visuals, controller);
        typeof(PlayerVisuals).GetField("gameManager", Flags).SetValue(visuals, manager);
        typeof(PlayerVisuals).GetField("target", Flags).SetValue(visuals, renderer);
        var frames = withRunFrames ? new[] { MakeSprite(), MakeSprite(), MakeSprite(), MakeSprite() } : new Sprite[0];
        typeof(PlayerVisuals).GetField("runFrames", Flags).SetValue(visuals, frames);
        typeof(PlayerVisuals).GetField("runFps", Flags).SetValue(visuals, 30f);

        return new Rig { Go = go, Controller = controller, Visuals = visuals, Renderer = renderer, Manager = manager, Fly = fly, RunFrames = frames };
    }

    [UnityTest]
    public IEnumerator RunningOnFloor_NotThrusting_PlaysRunCycle()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();

        var seen = new System.Collections.Generic.HashSet<Sprite>();
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            yield return null;
            seen.Add(rig.Renderer.sprite);
        }

        Assert.AreEqual(PlayerPose.Run, rig.Visuals.Pose);
        Assert.IsFalse(seen.Contains(rig.Fly));
        Assert.Greater(seen.Count, 1, "frames cycle");
        foreach (var s in seen) Assert.Contains(s, rig.RunFrames);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator RunCycle_RaisesFootstep_OnlyOnFootstepFrames()
    {
        var rig = Build();
        typeof(PlayerVisuals).GetField("footstepFrames", Flags).SetValue(rig.Visuals, new[] { 0, 2 });
        var stepSprites = new System.Collections.Generic.List<Sprite>();
        rig.Visuals.Footstep += () => stepSprites.Add(rig.Renderer.sprite);
        yield return null;
        rig.Manager.BeginRun();

        for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;

        Assert.Greater(stepSprites.Count, 1, "steps while running");
        foreach (var s in stepSprites)
            Assert.IsTrue(s == rig.RunFrames[0] || s == rig.RunFrames[2], "step only lands on a footstep frame");

        stepSprites.Clear();
        rig.Thrust(true);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;
        Assert.AreEqual(0, stepSprites.Count, "no steps while flying");
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Thrusting_ShowsFlySprite()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();
        yield return null;

        rig.Thrust(true);
        yield return null;

        Assert.AreEqual(PlayerPose.Fly, rig.Visuals.Pose);
        Assert.AreSame(rig.Fly, rig.Renderer.sprite);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Airborne_ShowsFlySprite()
    {
        var rig = Build(startY: 1f);
        yield return null;
        rig.Manager.BeginRun();
        yield return null;

        Assert.AreEqual(PlayerPose.Fly, rig.Visuals.Pose);
        Assert.AreSame(rig.Fly, rig.Renderer.sprite);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator NotRunning_OnFloor_ShowsFlySprite()
    {
        var rig = Build();
        yield return null;
        yield return null;

        Assert.AreEqual(PlayerPose.Fly, rig.Visuals.Pose);
        Assert.AreSame(rig.Fly, rig.Renderer.sprite);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator GameOver_LeavesSpriteToDeathAnimation()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();
        yield return null;

        rig.Manager.EndRun();
        var dead = MakeSprite();
        rig.Renderer.sprite = dead;
        for (int i = 0; i < 5; i++) yield return null;

        Assert.AreSame(dead, rig.Renderer.sprite);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator NoRunFrames_FallsBackToFlySprite()
    {
        var rig = Build(withRunFrames: false);
        yield return null;
        rig.Manager.BeginRun();
        for (int i = 0; i < 5; i++) yield return null;

        Assert.AreEqual(PlayerPose.Run, rig.Visuals.Pose);
        Assert.AreSame(rig.Fly, rig.Renderer.sprite);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator BackToGetReady_AfterDeath_ResumesFlySprite()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();
        yield return new WaitForSeconds(0.1f);
        rig.Manager.EndRun();
        rig.Renderer.sprite = MakeSprite();
        rig.Manager.ReturnToGetReady();
        yield return null;

        Assert.AreSame(rig.Fly, rig.Renderer.sprite);
        rig.Destroy();
    }

    private static float Tilt(Rig rig) => Mathf.DeltaAngle(0f, rig.Renderer.transform.localEulerAngles.z);

    [UnityTest]
    public IEnumerator Falling_TiltsForward_WithinMax()
    {
        var rig = Build(startY: 2f, childVisual: true);
        yield return null;
        rig.Manager.BeginRun();

        yield return new WaitForSeconds(0.25f);

        float maxTilt = (float)typeof(PlayerVisuals).GetField("maxFallTilt", Flags).GetValue(rig.Visuals);
        Assert.Less(Tilt(rig), -1f, "nose tips forward (clockwise) while falling");
        Assert.GreaterOrEqual(Tilt(rig), -maxTilt - 1e-3f);
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, rig.Go.transform.eulerAngles.z), 1e-3f, "root (collider) never rotates");
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Thrusting_ReturnsTiltToZero()
    {
        var rig = Build(startY: 2f, childVisual: true);
        yield return null;
        rig.Manager.BeginRun();
        yield return new WaitForSeconds(0.25f);
        Assert.Less(Tilt(rig), -1f);

        rig.Thrust(true);
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(0f, Tilt(rig), 0.01f);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator Grounded_HasNoTilt()
    {
        var rig = Build(childVisual: true);
        yield return null;
        rig.Manager.BeginRun();
        rig.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);

        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(0f, Tilt(rig), 0.01f);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator GameOver_LeavesRotationToDeathTumble()
    {
        var rig = Build(startY: 2f, childVisual: true);
        yield return null;
        rig.Manager.BeginRun();
        yield return null;

        rig.Manager.EndRun();
        rig.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int i = 0; i < 5; i++) yield return null;

        Assert.AreEqual(90f, Tilt(rig), 0.01f);
        rig.Destroy();
    }
}
