using UnityEngine;
using TMPro;
using JetpackRide.Core;

namespace JetpackRide.UI
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text finalDistanceText;
        [SerializeField] private TMP_Text finalCoinsText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text titleHighScoreText;

        // GameManager wiring lives in Start, not Awake, so serialized (or test-injected)
        // references are guaranteed to be assigned before they're read.
        private void Start()
        {
            gameManager.StateChanged += HandleStateChanged;
            gameManager.DistanceChanged += HandleDistanceChanged;
            gameManager.CoinsChanged += HandleCoinsChanged;

            ShowPanelFor(gameManager.CurrentState);
            titleHighScoreText.text = $"High Score: {gameManager.HighScore} m";
        }

        private void OnDestroy()
        {
            if (gameManager == null) return;
            gameManager.StateChanged -= HandleStateChanged;
            gameManager.DistanceChanged -= HandleDistanceChanged;
            gameManager.CoinsChanged -= HandleCoinsChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            ShowPanelFor(state);

            if (state == GameState.GameOver)
            {
                finalDistanceText.text = $"Distance: {Mathf.FloorToInt(gameManager.DistanceMeters)} m";
                finalCoinsText.text = $"Coins: {gameManager.CoinsThisRun}";
                highScoreText.text = $"High Score: {gameManager.HighScore} m";
            }
            else if (state == GameState.GetReady)
            {
                titleHighScoreText.text = $"High Score: {gameManager.HighScore} m";
            }
        }

        private void HandleDistanceChanged(float distance)
        {
            distanceText.text = $"{Mathf.FloorToInt(distance)} m";
        }

        private void HandleCoinsChanged(int coins)
        {
            coinsText.text = coins.ToString();
        }

        private void ShowPanelFor(GameState state)
        {
            titlePanel.SetActive(state == GameState.GetReady);
            hudPanel.SetActive(state == GameState.Running);
            gameOverPanel.SetActive(state == GameState.GameOver);
        }
    }
}
