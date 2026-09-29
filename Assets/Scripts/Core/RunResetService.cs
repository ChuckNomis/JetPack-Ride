using UnityEngine;
using JetpackRide.Pooling;

namespace JetpackRide.Core
{
    public class RunResetService : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ObjectPoolManager pool;

        // GameManager wiring lives in Start/OnDestroy, not OnEnable/OnDisable, so serialized
        // (or test-injected) references are guaranteed to be assigned before they're read.
        private void Start()
        {
            gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GetReady)
            {
                pool.DespawnAll();
            }
        }
    }
}
