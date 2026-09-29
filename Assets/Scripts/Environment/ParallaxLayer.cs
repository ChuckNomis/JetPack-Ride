using UnityEngine;
using JetpackRide.Core;

namespace JetpackRide.Environment
{
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        public float ScrollSpeedMultiplier = 1f;
        public float TileWidth = 20f;

        private void Update()
        {
            if (gameManager == null || gameManager.CurrentState != GameState.Running) return;

            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, gameManager.Config);
            transform.position = ComputeWrappedPosition(transform.position, snapshot.ScrollSpeed, Time.deltaTime);
        }

        internal Vector3 ComputeWrappedPosition(Vector3 current, float baseScrollSpeed, float deltaTime)
        {
            float moved = current.x - baseScrollSpeed * ScrollSpeedMultiplier * deltaTime;
            if (moved <= -TileWidth)
            {
                moved += TileWidth;
            }
            return new Vector3(moved, current.y, current.z);
        }
    }
}
