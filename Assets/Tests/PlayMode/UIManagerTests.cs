using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using JetpackRide.Core;
using JetpackRide.UI;

public class UIManagerTests
{
    private (UIManager ui, GameManager manager) Build()
    {
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<GameManager>();
        var config = ScriptableObject.CreateInstance<GameConfig>();
        typeof(GameManager).GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, config);

        var uiGo = new GameObject("UIManager");
        var ui = uiGo.AddComponent<UIManager>();

        GameObject MakePanel(string name) { var p = new GameObject(name); return p; }
        TMP_Text MakeText(string name) { var t = new GameObject(name).AddComponent<TextMeshProUGUI>(); return t; }

        var title = MakePanel("Title");
        var hud = MakePanel("HUD");
        var gameOver = MakePanel("GameOver");

        void Set(string field, object value) =>
            typeof(UIManager).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(ui, value);

        Set("gameManager", manager);
        Set("titlePanel", title);
        Set("hudPanel", hud);
        Set("gameOverPanel", gameOver);
        Set("distanceText", MakeText("Distance"));
        Set("coinsText", MakeText("Coins"));
        Set("finalDistanceText", MakeText("FinalDistance"));
        Set("finalCoinsText", MakeText("FinalCoins"));
        Set("highScoreText", MakeText("HighScore"));
        Set("titleHighScoreText", MakeText("TitleHighScore"));

        return (ui, manager);
    }

    [UnityTest]
    public IEnumerator OnAwake_TitlePanelVisible_OthersHidden()
    {
        var (ui, manager) = Build();
        yield return null;

        Assert.IsTrue(GetPanel(ui, "titlePanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "hudPanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "gameOverPanel").activeSelf);
    }

    [UnityTest]
    public IEnumerator BeginRun_ShowsHud_HidesTitle()
    {
        var (ui, manager) = Build();
        yield return null;

        manager.BeginRun();
        yield return null;

        Assert.IsTrue(GetPanel(ui, "hudPanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "titlePanel").activeSelf);
    }

    [UnityTest]
    public IEnumerator EndRun_ShowsGameOverPanel_WithFinalStats()
    {
        var (ui, manager) = Build();
        yield return null;
        manager.BeginRun();
        manager.AddDistance(42f);
        manager.CollectCoin();

        manager.EndRun();
        yield return null;

        Assert.IsTrue(GetPanel(ui, "gameOverPanel").activeSelf);
        var finalDistance = GetText(ui, "finalDistanceText");
        StringAssert.Contains("42", finalDistance.text);
        var finalCoins = GetText(ui, "finalCoinsText");
        StringAssert.Contains("1", finalCoins.text);
    }

    private static GameObject GetPanel(UIManager ui, string field) =>
        (GameObject)typeof(UIManager).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ui);

    private static TMP_Text GetText(UIManager ui, string field) =>
        (TMP_Text)typeof(UIManager).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ui);

    [UnityTest]
    public IEnumerator StartIntro_HidesAllPanels()
    {
        var (ui, manager) = Build();
        yield return null;

        manager.StartIntro();

        Assert.IsFalse(GetPanel(ui, "titlePanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "hudPanel").activeSelf);
        Assert.IsFalse(GetPanel(ui, "gameOverPanel").activeSelf);
    }
}
