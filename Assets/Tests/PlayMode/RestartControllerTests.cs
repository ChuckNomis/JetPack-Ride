using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Player;

public class RestartControllerTests
{
    private (RestartController controller, GameManager manager) Build(float lockoutSeconds)
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameConfig).GetField("restartLockoutSeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(config, lockoutSeconds);
        typeof(GameManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);

        var controllerGo = new GameObject("RestartController");
        var controller = controllerGo.AddComponent<RestartController>();
        typeof(RestartController).GetField("gameManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(controller, manager);

        return (controller, manager);
    }

    [UnityTest]
    public IEnumerator RestartPressed_DuringLockout_IsIgnored()
    {
        var (controller, manager) = Build(1.0f);
        yield return null;
        manager.BeginRun();
        manager.EndRun(); // enters GameOver, starts lockout

        controller.HandleRestartPressed(); // immediate press, inside lockout window

        Assert.AreEqual(GameState.GameOver, manager.CurrentState);
        Object.DestroyImmediate(controller.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator RestartPressed_AfterLockoutElapses_ReturnsToGetReady()
    {
        var (controller, manager) = Build(0.05f);
        yield return null;
        manager.BeginRun();
        manager.EndRun();

        yield return new WaitForSeconds(0.15f);
        controller.HandleRestartPressed();
        yield return null;

        Assert.AreEqual(GameState.GetReady, manager.CurrentState);
        Object.DestroyImmediate(controller.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator RestartPressed_FromGetReady_BeginsRun()
    {
        var (controller, manager) = Build(0f);
        yield return null;

        yield return new WaitForSeconds(0.05f);
        controller.HandleRestartPressed();
        yield return null;

        Assert.AreEqual(GameState.Running, manager.CurrentState);
        Object.DestroyImmediate(controller.gameObject);
        Object.DestroyImmediate(manager.gameObject);
    }
}
