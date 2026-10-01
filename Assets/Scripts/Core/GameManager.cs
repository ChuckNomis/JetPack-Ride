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
        public bool IsPaused { get; private set; }
        public GameConfig Config => config;

        public event Action<GameState> StateChanged;
        public event Action<float> DistanceChanged;
        public event Action<int> CoinsChanged;
        public event Action<int> ScoreChanged;
        public event Action<bool> PausedChanged;

        private const string HighScoreKey = "jetpack_ride_high_score";

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        // Never leave the editor (or the next scene) frozen.
        private void OnDestroy()
        {
            if (IsPaused) ApplyPause(false);
        }

        // Title -> intro (IntroSequence plays it, then calls BeginRun). Stats reset here so the HUD
        // never shows the previous run's numbers once Running starts.
        public void StartIntro()
        {
            ResetRunStats();
            SetState(GameState.Intro);
        }

        public void BeginRun()
        {
            ResetRunStats();
            SetState(GameState.Running);
        }

        private void ResetRunStats()
        {
            DistanceMeters = 0f;
            CoinsThisRun = 0;
            Score = 0;
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

        // Pause is a flag over the current state, not a GameState of its own: a state change would
        // cancel the intro and spawn loops. Stopping time freezes physics, movers and timers in place.
        public void Pause()
        {
            if (IsPaused || (CurrentState != GameState.Running && CurrentState != GameState.Intro)) return;
            SetPaused(true);
        }

        public void Resume()
        {
            if (IsPaused) SetPaused(false);
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        // Abandons the current run (no high score) and replays the intro. Goes through GetReady so
        // its observers clear hazards and reset the player, exactly as after a game over.
        public void RestartRun()
        {
            if (CurrentState != GameState.Running && CurrentState != GameState.Intro) return;
            ReturnToGetReady();
            StartIntro();
        }

        private void SetPaused(bool paused)
        {
            ApplyPause(paused);
            PausedChanged?.Invoke(paused);
        }

        private void ApplyPause(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        private void SetState(GameState newState)
        {
            if (IsPaused) SetPaused(false);
            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
