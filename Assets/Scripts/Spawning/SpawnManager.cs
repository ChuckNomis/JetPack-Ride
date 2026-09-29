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
        // Free vertical stretch a zapper cluster must leave: player diameter (0.67) plus a margin.
        public const float MinZapperLane = 1.3f;
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
        [SerializeField] private float rocketPairsFromMeters = 250f;
        [SerializeField] private float rocketTriplesFromMeters = 500f;
        [SerializeField] private Vector2 coinSpawnIntervalRange = new(2.5f, 4f);
        [SerializeField] private float coinSpacing = 0.8f;
        [SerializeField] private float coinArcHeight = 1.2f;
        [Tooltip("World-unit zapper lengths, short..long; the longest unlocks later in a run.")]
        [SerializeField] private float[] zapperLengths = { 2.4f, 3.3f, 4.4f };
        [Tooltip("Collider thickness used for layout; must be >= the prefab's real beam thickness.")]
        [SerializeField] private float zapperThickness = 0.85f;
        [SerializeField] private float zapperClusterGap = 1f;

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

                int volley = DetermineRocketVolleySize(gameManager.DistanceMeters, Random.value, rocketPairsFromMeters, rocketTriplesFromMeters);
                var ys = PickVolleyYs(volley, spawnYRange, MinRocketPairGap, () => Random.value);
                foreach (float y in ys) ShowWarning(y);

                await Awaitable.WaitForSecondsAsync(warningLeadSeconds, token);
                if (token.IsCancellationRequested) break;
                // Only the first rocket of a volley may home, or they'd converge into one.
                for (int i = 0; i < ys.Length; i++) SpawnRocketNow(spawnY: ys[i], allowHoming: i == 0);
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

        // Random volley size by distance: always 1 before pairsFrom; 1 or 2 (50/50) until triplesFrom;
        // then 1/2/3 at 30/40/30.
        public static int DetermineRocketVolleySize(float distanceMeters, float random01, float pairsFromMeters, float triplesFromMeters)
        {
            if (distanceMeters < pairsFromMeters) return 1;
            if (distanceMeters < triplesFromMeters) return random01 < 0.5f ? 1 : 2;
            return random01 < 0.3f ? 1 : random01 < 0.7f ? 2 : 3;
        }

        // Picks `count` Ys in the band, each at least minGap from its neighbours so there's always a
        // lane to fly through. The band's slack beyond the mandatory gaps is split randomly between
        // them, so every result is valid by construction. If the band can't fit `count`, fewer are
        // returned. Order is shuffled so the (possibly homing) first rocket isn't always the lowest.
        public static float[] PickVolleyYs(int count, Vector2 yRange, float minGap, System.Func<float> random01)
        {
            float span = yRange.y - yRange.x;
            while (count > 1 && (count - 1) * minGap > span) count--;
            float slack = span - (count - 1) * minGap;

            var cuts = new float[count];
            for (int i = 0; i < count; i++) cuts[i] = random01() * slack;
            System.Array.Sort(cuts);

            var ys = new float[count];
            for (int i = 0; i < count; i++) ys[i] = yRange.x + cuts[i] + i * minGap;

            for (int i = count - 1; i > 0; i--)
            {
                int j = Mathf.Min(i, (int)(random01() * (i + 1)));
                (ys[i], ys[j]) = (ys[j], ys[i]);
            }
            return ys;
        }

        // Coins only come in batches (CoinPatterns); bigger and more varied shapes later in a run.
        internal int SpawnCoinsNow()
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            var pattern = CoinPatterns.Pick(snapshot.RampProgress01, Random.value, coinSpacing, coinArcHeight);

            // Keep the whole pattern inside the play band.
            float maxBaseY = spawnYRange.y - pattern.Bounds.height;
            float baseY = maxBaseY < spawnYRange.x
                ? (spawnYRange.x + spawnYRange.y - pattern.Bounds.height) * 0.5f
                : Random.Range(spawnYRange.x, maxBaseY);
            foreach (var offset in pattern.Offsets)
            {
                var pos = new Vector3(spawnPoint.position.x + offset.x, baseY + offset.y, 0f);
                var instance = pool.Spawn(CoinPoolId, pos, Quaternion.identity);
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, CoinPoolId, snapshot.ScrollSpeed, despawnX);
                }
            }
            return pattern.Offsets.Length;
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

            // The layout keeps every beam inside the band and the cluster's combined vertical span
            // leaves at least MinZapperLane free, so there is always a lane through the whole cluster.
            var cluster = ZapperLayout.PlanCluster(clusterSize, snapshot.RampProgress01, spawnYRange,
                zapperThickness, zapperLengths, MinZapperLane, zapperClusterGap, () => Random.value);
            foreach (var zapper in cluster)
            {
                var pos = new Vector3(spawnPoint.position.x + zapper.Center.x, zapper.Center.y, 0f);
                var rotation = Quaternion.Euler(0f, 0f, ZapperLayout.AngleDegrees(zapper.Orientation));
                var instance = pool.Spawn(ObstaclePoolId, pos, rotation);
                if (instance.TryGetComponent<ZapperShape>(out var shape)) shape.Configure(zapper.Orientation, zapper.Length);
                else instance.transform.rotation = rotation;
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, ObstaclePoolId, snapshot.ScrollSpeed * config.ZapperSpeedMultiplier, despawnX);
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
