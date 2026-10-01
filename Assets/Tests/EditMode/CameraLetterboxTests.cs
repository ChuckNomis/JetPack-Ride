using NUnit.Framework;
using UnityEngine;
using JetpackRide.Environment;

public class CameraLetterboxTests
{
    private const float Wide = 16f / 9f;

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
        Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
        Assert.AreEqual(expected.width, actual.width, 1e-4f, "width");
        Assert.AreEqual(expected.height, actual.height, 1e-4f, "height");
    }

    [Test]
    public void MatchingAspect_FillsScreen()
    {
        AssertRect(new Rect(0f, 0f, 1f, 1f), CameraLetterbox.ComputeViewport(1920f / 1080f, Wide));
    }

    [Test]
    public void TallerScreen_AddsBarsTopAndBottom()
    {
        // 4:3 screen: 16:9 content uses 0.75 of the height, centered.
        AssertRect(new Rect(0f, 0.125f, 1f, 0.75f), CameraLetterbox.ComputeViewport(4f / 3f, Wide));
    }

    [Test]
    public void WiderScreen_AddsBarsLeftAndRight()
    {
        // 32:9 ultrawide: 16:9 content uses half the width, centered.
        AssertRect(new Rect(0.25f, 0f, 0.5f, 1f), CameraLetterbox.ComputeViewport(32f / 9f, Wide));
    }

    [Test]
    public void InvalidAspect_FallsBackToFullScreen()
    {
        AssertRect(new Rect(0f, 0f, 1f, 1f), CameraLetterbox.ComputeViewport(0f, Wide));
    }
}
