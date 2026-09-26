using System;
using UnityEngine;

namespace JetpackRide.Core
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        public GameState CurrentState { get; private set; } = GameState.GetReady;
        public float DistanceMeters { get; private set; }
        public int CoinsThisRun { get; private set; }
        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public GameConfig Config => config;

        public event Action<GameState> StateChanged;
        public event Action<float> DistanceChanged;
        public event Action<int> CoinsChanged;
        public event Action<int> ScoreChanged;

        private const string HighScoreKey = "jetpack_ride_high_score";

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        public void BeginRun()
        {
            DistanceMeters = 0f;
            CoinsThisRun = 0;
            Score = 0;
            SetState(GameState.Running);
        }

        public void AddDistance(float deltaMeters)
        {
            if (CurrentState != GameState.Running) return;
            DistanceMeters += deltaMeters;
            Score = Mathf.FloorToInt(DistanceMeters) + CoinsThisRun * config.CoinValue;
            DistanceChanged?.Invoke(DistanceMeters);
            ScoreChanged?.Invoke(Score);
        }

        public void CollectCoin()
        {
            if (CurrentState != GameState.Running) return;
            CoinsThisRun++;
            Score = Mathf.FloorToInt(DistanceMeters) + CoinsThisRun * config.CoinValue;
            CoinsChanged?.Invoke(CoinsThisRun);
            ScoreChanged?.Invoke(Score);
        }

        public void EndRun()
        {
            if (CurrentState != GameState.Running) return;
            int finalDistance = Mathf.FloorToInt(DistanceMeters);
            if (finalDistance > HighScore)
            {
                HighScore = finalDistance;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
            }
            SetState(GameState.GameOver);
        }

        public void ReturnToGetReady()
        {
            SetState(GameState.GetReady);
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
