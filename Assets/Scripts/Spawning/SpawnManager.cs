using System.Threading;
using UnityEngine;
using JetpackRide.Core;
using JetpackRide.Pooling;
using JetpackRide.Hazards;

namespace JetpackRide.Spawning
{
    public class SpawnManager : MonoBehaviour
    {
        public const string ObstaclePoolId = "obstacle";
        public const string RocketPoolId = "rocket";
        public const string CoinPoolId = "coin";
        public const string RocketWarningPoolId = "rocketWarning";

        [SerializeField] private GameManager gameManager;
        [SerializeField] private ObjectPoolManager pool;
        [SerializeField] private GameConfig config;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform player;
        [SerializeField] private Vector2 spawnYRange = new(-3.5f, 3.5f);
        [SerializeField] private float despawnX = -14f;
        [SerializeField] private float warningLeadSeconds = 0.5f;
        [SerializeField] private float warningX = 8.5f;

        public int ActiveObstacleCount { get; private set; }
        public bool LastSpawnedRocketWasHoming { get; private set; }

        private CancellationTokenSource cts;

        // GameManager wiring lives in Start/OnDestroy, not OnEnable/OnDisable, so serialized
        // (or test-injected) references are guaranteed to be assigned before they're read.
        private void Start()
        {
            gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
            cts?.Cancel();
        }

        private void HandleStateChanged(GameState state)
        {
            cts?.Cancel();
            if (state == GameState.Running)
            {
                cts = new CancellationTokenSource();
                RunObstacleLoopAsync(cts.Token).Forget();
                RunRocketLoopAsync(cts.Token).Forget();
            }
        }

        private async Awaitable RunObstacleLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
                await Awaitable.WaitForSecondsAsync(snapshot.ObstacleSpawnInterval, token);
                if (token.IsCancellationRequested) break;
                SpawnObstacleNow();
            }
        }

        private async Awaitable RunRocketLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
                float waitBeforeWarning = Mathf.Max(0.05f, snapshot.RocketSpawnInterval - warningLeadSeconds);
                await Awaitable.WaitForSecondsAsync(waitBeforeWarning, token);
                if (token.IsCancellationRequested) break;

                float y = Random.Range(spawnYRange.x, spawnYRange.y);
                var warning = pool.Spawn(RocketWarningPoolId, new Vector3(warningX, y, 0f), Quaternion.identity);
                if (warning.TryGetComponent<RocketWarningIndicator>(out var indicator))
                {
                    indicator.Configure(pool, RocketWarningPoolId);
                    indicator.PlayAndDespawnAsync(warningLeadSeconds).Forget();
                }

                await Awaitable.WaitForSecondsAsync(warningLeadSeconds, token);
                if (token.IsCancellationRequested) break;
                SpawnRocketNow(spawnY: y);
            }
        }

        // GDD §3 "Pattern mixing": past the threshold, one spawn window yields a pair of obstacles.
        public static int DetermineObstacleClusterSize(float rampProgress01)
        {
            const float clusterThreshold = 0.65f;
            return rampProgress01 > clusterThreshold ? 2 : 1;
        }

        internal void SpawnObstacleNow()
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            int clusterSize = DetermineObstacleClusterSize(snapshot.RampProgress01);

            for (int i = 0; i < clusterSize; i++)
            {
                var pos = new Vector3(spawnPoint.position.x + i * 2.5f, Random.Range(spawnYRange.x, spawnYRange.y), 0f);
                var instance = pool.Spawn(ObstaclePoolId, pos, Quaternion.identity);
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, ObstaclePoolId, snapshot.ScrollSpeed, despawnX);
                }
                ActiveObstacleCount++;
            }
        }

        internal void SpawnRocketNow(float? spawnY = null)
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            bool homing = Random.value < snapshot.RocketAggression;
            LastSpawnedRocketWasHoming = homing;

            var pos = new Vector3(spawnPoint.position.x, spawnY ?? Random.Range(spawnYRange.x, spawnYRange.y), 0f);
            var instance = pool.Spawn(RocketPoolId, pos, Quaternion.identity);

            if (instance.TryGetComponent<RocketBehaviour>(out var rocket))
            {
                if (homing && player != null) rocket.SetTarget(player);
                rocket.Init(homing);
            }
            if (instance.TryGetComponent<HazardMover>(out var mover))
            {
                mover.Configure(pool, RocketPoolId, snapshot.ScrollSpeed * config.RocketSpeedMultiplier, despawnX);
            }
        }
    }
}
