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
        public const float MaxClusterYDelta = 2.5f;
        public const float MinRocketPairGap = 3f;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private ObjectPoolManager pool;
        [SerializeField] private GameConfig config;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform player;
        [SerializeField] private Vector2 spawnYRange = new(-3.5f, 3.5f);
        [SerializeField] private float despawnX = -14f;
        [SerializeField] private float warningLeadSeconds = 0.5f;
        [SerializeField] private float warningX = 8.5f;
        [SerializeField] private Vector2 coinSpawnIntervalRange = new(2.5f, 4f);
        [SerializeField, Range(0f, 1f)] private float coinChainChance = 0.6f;
        [SerializeField] private float coinSpacing = 0.8f;
        [SerializeField] private float coinArcHeight = 1.2f;

        public int ActiveObstacleCount { get; private set; }
        public bool LastSpawnedRocketWasHoming { get; private set; }
        public event System.Action RocketSpawned;

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
                RunCoinLoopAsync(cts.Token).Forget();
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

                snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
                float firstY = Random.Range(spawnYRange.x, spawnYRange.y);
                bool pair = DetermineRocketVolleySize(snapshot.RampProgress01) == 2;
                float secondY = pair ? PickSecondRocketY(firstY, spawnYRange, MinRocketPairGap, Random.value) : 0f;

                ShowWarning(firstY);
                if (pair) ShowWarning(secondY);

                await Awaitable.WaitForSecondsAsync(warningLeadSeconds, token);
                if (token.IsCancellationRequested) break;
                SpawnRocketNow(spawnY: firstY);
                // Only the first of a pair may home, or both would converge into one rocket.
                if (pair) SpawnRocketNow(spawnY: secondY, allowHoming: false);
            }
        }

        private async Awaitable RunCoinLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Awaitable.WaitForSecondsAsync(Random.Range(coinSpawnIntervalRange.x, coinSpawnIntervalRange.y), token);
                if (token.IsCancellationRequested) break;
                SpawnCoinsNow();
            }
        }

        private void ShowWarning(float y)
        {
            var warning = pool.Spawn(RocketWarningPoolId, new Vector3(warningX, y, 0f), Quaternion.identity);
            if (warning.TryGetComponent<RocketWarningIndicator>(out var indicator))
            {
                indicator.Configure(pool, RocketWarningPoolId);
                indicator.PlayAndDespawnAsync(warningLeadSeconds).Forget();
            }
        }

        // Past the midpoint of the ramp, rockets come in simultaneous pairs.
        public static int DetermineRocketVolleySize(float rampProgress01)
        {
            const float pairThreshold = 0.5f;
            return rampProgress01 > pairThreshold ? 2 : 1;
        }

        // Picks the pair's second Y at least minGap from the first, above or below, mapping random01
        // across both allowed bands so a lane between/around the two rockets always stays open.
        public static float PickSecondRocketY(float firstY, Vector2 yRange, float minGap, float random01)
        {
            float belowLen = Mathf.Max(0f, (firstY - minGap) - yRange.x);
            float aboveLen = Mathf.Max(0f, yRange.y - (firstY + minGap));
            float total = belowLen + aboveLen;
            if (total <= 0f)
            {
                // Range too tight for the gap: take the farthest edge.
                return firstY - yRange.x > yRange.y - firstY ? yRange.x : yRange.y;
            }

            float t = Mathf.Clamp01(random01) * total;
            return belowLen > 0f && t <= belowLen ? yRange.x + t : firstY + minGap + (t - belowLen);
        }

        // GDD §3: coins come as single pickups or short arcing chains.
        public static Vector2[] CoinPatternOffsets(int count, float spacing, float arcHeight)
        {
            var offsets = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float y = count > 1 ? arcHeight * Mathf.Sin(Mathf.PI * i / (count - 1)) : 0f;
                offsets[i] = new Vector2(i * spacing, y);
            }
            return offsets;
        }

        internal int SpawnCoinsNow()
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            bool chain = Random.value < coinChainChance;
            int count = chain ? Random.Range(5, 8) : 1;
            var offsets = CoinPatternOffsets(count, coinSpacing, chain ? coinArcHeight : 0f);

            // Keep the whole arc inside the play band.
            float baseY = Random.Range(spawnYRange.x, spawnYRange.y - (chain ? coinArcHeight : 0f));
            foreach (var offset in offsets)
            {
                var pos = new Vector3(spawnPoint.position.x + offset.x, baseY + offset.y, 0f);
                var instance = pool.Spawn(CoinPoolId, pos, Quaternion.identity);
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, CoinPoolId, snapshot.ScrollSpeed, despawnX);
                }
            }
            return count;
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

            // Later cluster members stay within MaxClusterYDelta of the first, so a single open lane
            // runs through the whole pair instead of demanding a full-height swerve in ~0.1s.
            float firstY = Random.Range(spawnYRange.x, spawnYRange.y);
            for (int i = 0; i < clusterSize; i++)
            {
                float y = i == 0
                    ? firstY
                    : Random.Range(Mathf.Max(spawnYRange.x, firstY - MaxClusterYDelta), Mathf.Min(spawnYRange.y, firstY + MaxClusterYDelta));
                var pos = new Vector3(spawnPoint.position.x + i * 2.5f, y, 0f);
                var instance = pool.Spawn(ObstaclePoolId, pos, Quaternion.identity);
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, ObstaclePoolId, snapshot.ScrollSpeed, despawnX);
                }
                ActiveObstacleCount++;
            }
        }

        internal void SpawnRocketNow(float? spawnY = null, bool allowHoming = true)
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            bool homing = allowHoming && Random.value < snapshot.RocketAggression;
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
            RocketSpawned?.Invoke();
        }
    }
}
