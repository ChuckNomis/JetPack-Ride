using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using JetpackRide.Core;
using JetpackRide.UI;

public class PauseMenuTests
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private class Rig
    {
        public GameManager Manager;
        public PauseMenu Menu;
        public GameObject Panel;
        public Button Continue;
        public Button Restart;

        public void Destroy()
        {
            Object.DestroyImmediate(Menu.gameObject);
            Object.DestroyImmediate(Panel);
            Object.DestroyImmediate(Manager.gameObject);
        }
    }

    private static Rig Build()
    {
        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        typeof(GameManager).GetField("config", Flags).SetValue(manager, ScriptableObject.CreateInstance<GameConfig>());

        var panel = new GameObject("PausePanel");
        var continueButton = new GameObject("Continue").AddComponent<Button>();
        var restartButton = new GameObject("Restart").AddComponent<Button>();
        continueButton.transform.SetParent(panel.transform);
        restartButton.transform.SetParent(panel.transform);

        var menu = new GameObject("PauseMenu").AddComponent<PauseMenu>();
        void Set(string field, object value) => typeof(PauseMenu).GetField(field, Flags).SetValue(menu, value);
        Set("gameManager", manager);
        Set("pausePanel", panel);
        Set("continueButton", continueButton);
        Set("restartButton", restartButton);

        return new Rig { Manager = manager, Menu = menu, Panel = panel, Continue = continueButton, Restart = restartButton };
    }

    [UnityTest]
    public IEnumerator Panel_FollowsPause_AndContinueResumes()
    {
        var rig = Build();
        yield return null; // Start: hides the panel, wires buttons
        Assert.IsFalse(rig.Panel.activeSelf);

        rig.Manager.BeginRun();
        rig.Manager.Pause();
        Assert.IsTrue(rig.Panel.activeSelf);

        rig.Continue.onClick.Invoke();
        Assert.IsFalse(rig.Manager.IsPaused);
        Assert.IsFalse(rig.Panel.activeSelf);
        rig.Destroy();
    }

    [UnityTest]
    public IEnumerator RestartButton_HidesPanel_AndStartsIntro()
    {
        var rig = Build();
        yield return null;
        rig.Manager.BeginRun();
        rig.Manager.Pause();

        rig.Restart.onClick.Invoke();

        Assert.IsFalse(rig.Manager.IsPaused);
        Assert.IsFalse(rig.Panel.activeSelf);
        Assert.AreEqual(GameState.Intro, rig.Manager.CurrentState);
        Assert.AreEqual(1f, Time.timeScale);
        rig.Destroy();
    }
}
