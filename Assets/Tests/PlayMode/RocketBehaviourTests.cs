using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Hazards;

public class RocketBehaviourTests
{
    [UnityTest]
    public IEnumerator Init_Homing_TurnsTowardTargetY()
    {
        var targetGo = new GameObject("Target");
        targetGo.transform.position = new Vector3(-5f, 5f, 0f);

        var go = new GameObject("Rocket");
        var rocket = go.AddComponent<RocketBehaviour>();
        typeof(RocketBehaviour).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(rocket, targetGo.transform);
        go.transform.position = Vector3.zero;
        yield return null;

        rocket.Init(homing: true);
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        yield return null;

        Assert.Greater(go.transform.position.y, 0f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(targetGo);
    }

    [UnityTest]
    public IEnumerator Init_NotHoming_StaysOnStraightLine()
    {
        var go = new GameObject("Rocket");
        var rocket = go.AddComponent<RocketBehaviour>();
        go.transform.position = Vector3.zero;
        yield return null;

        rocket.Init(homing: false);
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        yield return null;

        Assert.AreEqual(0f, go.transform.position.y, 0.001f);
        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator Homing_DoesNotOvershootTargetY()
    {
        var targetGo = new GameObject("Target");
        targetGo.transform.position = new Vector3(-5f, 0.01f, 0f);

        var go = new GameObject("Rocket");
        var rocket = go.AddComponent<RocketBehaviour>();
        rocket.SetTarget(targetGo.transform);
        yield return null;

        rocket.Init(homing: true);
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(0.01f, go.transform.position.y, 0.0001f);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(targetGo);
    }
}
