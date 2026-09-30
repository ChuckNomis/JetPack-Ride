using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JetpackRide.Core;
using JetpackRide.Environment;

public class CameraShakeTests
{
    private static readonly Vector3 Rest = new(1f, 2f, -10f);

    private static CameraShake Build()
    {
        var go = new GameObject("Camera");
        go.transform.localPosition = Rest;
        return go.AddComponent<CameraShake>();
    }

    [UnityTest]
    public IEnumerator Shake_MovesCamera_ThenRestoresExactRest()
    {
        var shake = Build();
        yield return null;

        shake.Shake(0.2f, 0.5f).Forget();
        float maxOffset = 0f;
        for (float t = 0f; t < 0.1f; t += Time.deltaTime)
        {
            yield return null;
            maxOffset = Mathf.Max(maxOffset, (shake.transform.localPosition - Rest).magnitude);
        }
        Assert.Greater(maxOffset, 0.01f, "camera moves while shaking");
        Assert.LessOrEqual(maxOffset, 0.5f + 1e-4f, "never further than strength");

        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(shake.IsShaking);
        Assert.AreEqual(Rest, shake.transform.localPosition);
        Object.DestroyImmediate(shake.gameObject);
    }

    [UnityTest]
    public IEnumerator Shake_Overlapping_RestoresOriginalRest()
    {
        var shake = Build();
        yield return null;

        shake.Shake(0.2f, 0.5f).Forget();
        yield return new WaitForSeconds(0.05f);
        shake.Shake(0.1f, 0.5f).Forget(); // starts while displaced

        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual(Rest, shake.transform.localPosition);
        Object.DestroyImmediate(shake.gameObject);
    }

    [UnityTest]
    public IEnumerator Disable_MidShake_RestoresRest()
    {
        var shake = Build();
        yield return null;

        shake.Shake(1f, 0.5f).Forget();
        yield return new WaitForSeconds(0.05f);
        shake.enabled = false;

        Assert.AreEqual(Rest, shake.transform.localPosition);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(Rest, shake.transform.localPosition, "stale shake loop leaves the camera alone");
        Object.DestroyImmediate(shake.gameObject);
    }
}
