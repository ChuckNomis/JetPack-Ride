using UnityEngine;

namespace JetpackRide.Core
{
    public class DistanceTracker : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameConfig config;

        private void Update()
        {
            if (gameManager == null || gameManager.CurrentState != GameState.Running) return;
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            gameManager.AddDistance(snapshot.ScrollSpeed * deltaTime);
        }
    }
}
