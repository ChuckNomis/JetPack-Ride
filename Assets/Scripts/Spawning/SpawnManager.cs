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

        [SerializeField] private GameManager gameManager;
        [SerializeField] private ObjectPoolManager pool;
        [SerializeField] private GameConfig config;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform player;
        [SerializeField] private Vector2 spawnYRange = new(-3.5f, 3.5f);
        [SerializeField] private float despawnX = -14f;

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
                await Awaitable.WaitForSecondsAsync(snapshot.RocketSpawnInterval, token);
                if (token.IsCancellationRequested) break;
                SpawnRocketNow();
            }
        }

        internal void SpawnObstacleNow()
        {
            var pos = new Vector3(spawnPoint.position.x, Random.Range(spawnYRange.x, spawnYRange.y), 0f);
            var instance = pool.Spawn(ObstaclePoolId, pos, Quaternion.identity);
            if (instance.TryGetComponent<HazardMover>(out var mover))
            {
                var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
                mover.Configure(pool, ObstaclePoolId, snapshot.ScrollSpeed, despawnX);
            }
            ActiveObstacleCount++;
        }

        internal void SpawnRocketNow()
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            bool homing = Random.value < snapshot.RocketAggression;
            LastSpawnedRocketWasHoming = homing;

            var pos = new Vector3(spawnPoint.position.x, Random.Range(spawnYRange.x, spawnYRange.y), 0f);
            var instance = pool.Spawn(RocketPoolId, pos, Quaternion.identity);

            if (instance.TryGetComponent<RocketBehaviour>(out var rocket))
            {
                if (homing && player != null) rocket.SetTarget(player);
                rocket.Init(homing);
            }
            if (instance.TryGetComponent<HazardMover>(out var mover))
            {
                mover.Configure(pool, RocketPoolId, snapshot.ScrollSpeed, despawnX);
            }
        }
    }
}
