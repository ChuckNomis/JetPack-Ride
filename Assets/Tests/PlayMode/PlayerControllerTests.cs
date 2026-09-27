using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Player;

public class PlayerControllerTests
{
    private const int HeldSteps = 10;

    private (GameObject go, PlayerController controller, Rigidbody2D body, GameManager manager) Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);

        var playerGo = new GameObject("Player");
        var body = playerGo.AddComponent<Rigidbody2D>();
        var controller = playerGo.AddComponent<PlayerController>();
        typeof(PlayerController).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(controller, config);
        typeof(PlayerController).GetField("gameManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(controller, manager);
        controller.MinY = -4.5f;
        controller.MaxY = 4.5f;

        return (playerGo, controller, body, manager);
    }

    [UnityTest]
    public IEnumerator ApplyThrust_WhileRunning_AddsUpwardVelocity()
    {
        var (go, controller, body, manager) = Build();
        yield return null; // let Start apply config gravity
        manager.BeginRun();

        // Thrust must overcome the config's gravity, not just nudge a zero-gravity body.
        for (int i = 0; i < HeldSteps; i++)
        {
            controller.ApplyThrust(true);
            yield return new WaitForFixedUpdate();
        }

        Assert.Greater(body.linearVelocity.y, 0f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator ApplyThrust_WhileNotRunning_DoesNothing()
    {
        var (go, controller, body, manager) = Build();
        yield return null; // state is GetReady, not Running

        for (int i = 0; i < HeldSteps; i++)
        {
            controller.ApplyThrust(true);
            yield return new WaitForFixedUpdate();
        }

        // Only gravity acts: the body is falling (or resting on the floor), never rising.
        Assert.LessOrEqual(body.linearVelocity.y, 0f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator Position_ClampsWithinPlayBounds()
    {
        var (go, controller, body, manager) = Build();
        yield return null;
        manager.BeginRun();
        body.position = new Vector2(0f, 100f);

        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.LessOrEqual(go.transform.position.y, controller.MaxY + 0.001f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(manager.gameObject);
    }

    [UnityTest]
    public IEnumerator Position_ClampsToFloor_WhileGetReady()
    {
        var (go, controller, body, manager) = Build();
        yield return null; // GetReady: gravity pulls the player down before the run starts

        for (int i = 0; i < HeldSteps * 5; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        // The clamp runs before each physics step, so gravity can pull the body at most one
        // step (~g*dt^2, well under 0.05u) below the floor — it must not keep sinking.
        Assert.GreaterOrEqual(body.position.y, controller.MinY - 0.05f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(manager.gameObject);
    }
}
