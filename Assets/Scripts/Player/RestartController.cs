using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using JetpackRide.Core;
using JetpackRide.Input;

namespace JetpackRide.Player
{
    public class RestartController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        public bool LockoutActive { get; private set; }

        private PlayerInputActions actions;
        private CancellationTokenSource cts;

        private void Awake()
        {
            actions = new PlayerInputActions();
            actions.Gameplay.Restart.performed += OnRestartInput;
        }

        // GameManager wiring lives in Start/OnDestroy, not OnEnable/OnDisable, so serialized
        // (or test-injected) references are guaranteed to be assigned before they're read.
        private void Start()
        {
            gameManager.StateChanged += HandleStateChanged;

            if (gameManager.CurrentState == GameState.GameOver || gameManager.CurrentState == GameState.GetReady)
            {
                RestartLockoutAsync().Forget();
            }
        }

        private void OnEnable()
        {
            actions.Gameplay.Enable();
        }

        private void OnDisable()
        {
            actions.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
            cts?.Cancel();
            cts?.Dispose();
            actions.Gameplay.Restart.performed -= OnRestartInput;
            actions.Dispose();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                RestartLockoutAsync().Forget();
            }
        }

        private async Awaitable RestartLockoutAsync()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            LockoutActive = true;
            await Awaitable.WaitForSecondsAsync(gameManager.Config.RestartLockoutSeconds, token);
            if (!token.IsCancellationRequested) LockoutActive = false;
        }

        private void OnRestartInput(InputAction.CallbackContext ctx) => HandleRestartPressed();

        public void HandleRestartPressed()
        {
            if (LockoutActive) return;

            switch (gameManager.CurrentState)
            {
                case GameState.GameOver:
                    gameManager.ReturnToGetReady();
                    break;
                case GameState.GetReady:
                    gameManager.BeginRun();
                    break;
            }
        }
    }
}
