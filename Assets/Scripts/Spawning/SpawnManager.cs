using System.Collections.Generic;
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
        // Minimum distance kept between any coin and any zapper for as long as both are on screen.
        public const float CoinZapperClearance = 1f;

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
        [SerializeField] private float coinRadius = 0.3f;
        [Tooltip("Base-Y candidates tried for a coin batch before skipping it (it would touch a zapper).")]
        [SerializeField] private int coinPlacementAttempts = 6;
        [Tooltip("Vertical shifts tried for a zapper cluster before it evicts the coins in its way.")]
        [SerializeField] private int zapperShiftAttempts = 6;

        public int ActiveObstacleCount { get; private set; }
        public bool LastVolleyTracked { get; private set; }
        public event System.Action RocketSpawned;
        public event System.Action<int> RocketVolleyStarted;
        public event System.Action RocketWarningBlinked;

        private CancellationTokenSource cts;

        // ObjectPoolManager has no active-object query, so live coins/zappers are tracked here and
        // pruned (once despawned) at the start of every spawn.
        private struct Tracked
        {
            public HazardMover Mover;
            public Vector2 HalfExtents;
            public float Speed;

            public Rect Bounds
            {
                get
                {
                    Vector2 c = Mover.transform.position;
                    return new Rect(c - HalfExtents, 2f * HalfExtents);
                }
            }
        }

        private readonly List<Tracked> liveZappers = new();
        private readonly List<Tracked> liveCoins = new();

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
                int volley = DetermineRocketVolleySize(gameManager.DistanceMeters, Random.value, rocketPairsFromMeters, rocketTriplesFromMeters);
                var ys = PickVolleyYs(volley, spawnYRange, MinRocketPairGap, () => Random.value);
                RocketVolleyStarted?.Invoke(ys.Length);

                // Lock-on: every volley tracks from the start of a run; difficulty comes from the
                // track time shrinking (GameConfig.RocketTrackSeconds). Only the first rocket's warning
                // follows the player (two trackers would converge into one); the rest are fixed.
                bool tracked = player != null;
                LastVolleyTracked = tracked;
                int fixedFrom = tracked ? 1 : 0;
                Awaitable<float> trackTask = null;
                if (tracked)
                {
                    float trackSeconds = config.RocketTrackSeconds(snapshot.RampProgress01);
                    trackTask = TrackWarningAsync(ys[0], trackSeconds, token);
                }

                // Fixed rockets keep the normal warning lead and launch before the tracker locks, so
                // they never line up with it into a wall and none has to be dropped.
                if (ys.Length > fixedFrom)
                {
                    for (int i = fixedFrom; i < ys.Length; i++) ShowWarning(ys[i], warningLeadSeconds);
                    await Awaitable.WaitForSecondsAsync(warningLeadSeconds, token);
                    if (token.IsCancellationRequested) break;
                    for (int i = fixedFrom; i < ys.Length; i++) SpawnRocketNow(ys[i]);
                }

                if (!tracked) continue;
                float lockedY = await trackTask;
                if (token.IsCancellationRequested) break;
                if (float.IsNaN(lockedY)) continue;
                SpawnRocketNow(lockedY);
            }
        }

        private async Awaitable<float> TrackWarningAsync(float startY, float trackSeconds, CancellationToken token)
        {
            var warning = pool.Spawn(RocketWarningPoolId, new Vector3(warningX, startY, 0f), Quaternion.identity);
            if (warning.TryGetComponent<RocketWarningIndicator>(out var indicator))
            {
                indicator.Configure(pool, RocketWarningPoolId);
                // Forward blinks only for this use; the pooled indicator is handed out again later.
                indicator.LockBlinked += RaiseWarningBlinked;
                try
                {
                    return await indicator.TrackAndLockAsync(player, trackSeconds, config.RocketLockSeconds, config.RocketTrackSpeed, spawnYRange);
                }
                finally
                {
                    if (indicator != null) indicator.LockBlinked -= RaiseWarningBlinked;
                }
            }
            await Awaitable.WaitForSecondsAsync(trackSeconds + config.RocketLockSeconds, token);
            pool.Despawn(RocketWarningPoolId, warning);
            return startY;
        }

        private void RaiseWarningBlinked() => RocketWarningBlinked?.Invoke();

        private async Awaitable RunCoinLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Awaitable.WaitForSecondsAsync(Random.Range(coinSpawnIntervalRange.x, coinSpawnIntervalRange.y), token);
                if (token.IsCancellationRequested) break;
                SpawnCoinsNow();
            }
        }

        private void ShowWarning(float y, float seconds)
        {
            var warning = pool.Spawn(RocketWarningPoolId, new Vector3(warningX, y, 0f), Quaternion.identity);
            if (warning.TryGetComponent<RocketWarningIndicator>(out var indicator))
            {
                indicator.Configure(pool, RocketWarningPoolId);
                indicator.PlayAndDespawnAsync(seconds).Forget();
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
        // returned. Order is shuffled so the (possibly tracking) first rocket isn't always the lowest.
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
        // A batch is placed clear of every live zapper (CoinZapperClearance, including drift); if no
        // candidate height works it is skipped. Returns the number of coins spawned.
        internal int SpawnCoinsNow()
        {
            PruneTracked();
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            var pattern = CoinPatterns.Pick(snapshot.RampProgress01, Random.value, coinSpacing, coinArcHeight);
            float speed = snapshot.ScrollSpeed;
            float x = spawnPoint.position.x;

            // Keep the whole pattern inside the play band.
            float maxBaseY = spawnYRange.y - pattern.Bounds.height;
            float? baseY = null;
            for (int attempt = 0; attempt < coinPlacementAttempts && baseY == null; attempt++)
            {
                float candidate = maxBaseY < spawnYRange.x
                    ? (spawnYRange.x + spawnYRange.y - pattern.Bounds.height) * 0.5f
                    : Random.Range(spawnYRange.x, maxBaseY);
                var rect = new Rect(x - coinRadius, candidate - coinRadius,
                    pattern.Bounds.width + 2f * coinRadius, pattern.Bounds.height + 2f * coinRadius);
                if (!ConflictsWith(liveZappers, rect, speed)) baseY = candidate;
            }
            if (baseY == null) return 0;

            foreach (var offset in pattern.Offsets)
            {
                var pos = new Vector3(x + offset.x, baseY.Value + offset.y, 0f);
                var instance = pool.Spawn(CoinPoolId, pos, Quaternion.identity);
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, CoinPoolId, speed, despawnX);
                    liveCoins.Add(new Tracked { Mover = mover, HalfExtents = new Vector2(coinRadius, coinRadius), Speed = speed });
                }
            }
            return pattern.Offsets.Length;
        }

        private void PruneTracked()
        {
            static bool Gone(Tracked t) => t.Mover == null || t.Mover.HasDespawned || !t.Mover.gameObject.activeInHierarchy;
            liveZappers.RemoveAll(Gone);
            liveCoins.RemoveAll(Gone);
        }

        private bool ConflictsWith(List<Tracked> others, Rect rect, float speed)
        {
            foreach (var other in others)
            {
                if (SpawnSafety.WillOverlap(rect, speed, other.Bounds, other.Speed, CoinZapperClearance, despawnX)) return true;
            }
            return false;
        }

        // GDD §3 "Pattern mixing": past the threshold, one spawn window yields a pair of obstacles.
        public static int DetermineObstacleClusterSize(float rampProgress01)
        {
            const float clusterThreshold = 0.65f;
            return rampProgress01 > clusterThreshold ? 2 : 1;
        }

        internal void SpawnObstacleNow()
        {
            PruneTracked();
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            int clusterSize = DetermineObstacleClusterSize(snapshot.RampProgress01);
            float speed = snapshot.ScrollSpeed * config.ZapperSpeedMultiplier;

            // The layout keeps every beam inside the band and the cluster's combined vertical span
            // leaves at least MinZapperLane free, so there is always a lane through the whole cluster.
            var cluster = ZapperLayout.PlanCluster(clusterSize, snapshot.RampProgress01, spawnYRange,
                zapperThickness, zapperLengths, MinZapperLane, zapperClusterGap, () => Random.value);
            var extents = new Vector2[cluster.Length];
            for (int i = 0; i < cluster.Length; i++)
                extents[i] = ZapperLayout.WorldExtents(cluster[i].Orientation, cluster[i].Length, zapperThickness);

            // Coins must never touch zappers. First try shifting the whole cluster vertically (only
            // to heights that keep the lane); if nothing is clear, hazards win and the coins in the
            // way are removed, so difficulty never drops because of coins.
            float dy = 0f;
            bool clear = ClusterClearOfCoins(cluster, extents, 0f, speed);
            if (!clear)
            {
                float minLo = float.MaxValue, maxHi = float.MinValue;
                for (int i = 0; i < cluster.Length; i++)
                {
                    minLo = Mathf.Min(minLo, cluster[i].Center.y - extents[i].y);
                    maxHi = Mathf.Max(maxHi, cluster[i].Center.y + extents[i].y);
                }
                var spans = new List<Vector2>(cluster.Length);
                for (int attempt = 0; attempt < zapperShiftAttempts && !clear; attempt++)
                {
                    float candidate = Random.Range(spawnYRange.x - minLo, spawnYRange.y - maxHi);
                    spans.Clear();
                    for (int i = 0; i < cluster.Length; i++)
                        spans.Add(new Vector2(cluster[i].Center.y + candidate - extents[i].y, cluster[i].Center.y + candidate + extents[i].y));
                    if (ZapperLayout.LargestGap(spans, spawnYRange) < MinZapperLane) continue;
                    if (ClusterClearOfCoins(cluster, extents, candidate, speed)) { dy = candidate; clear = true; }
                }
            }

            for (int i = 0; i < cluster.Length; i++)
            {
                var zapper = cluster[i];
                var pos = new Vector3(spawnPoint.position.x + zapper.Center.x, zapper.Center.y + dy, 0f);
                var rotation = Quaternion.Euler(0f, 0f, ZapperLayout.AngleDegrees(zapper.Orientation));
                var instance = pool.Spawn(ObstaclePoolId, pos, rotation);
                if (instance.TryGetComponent<ZapperShape>(out var shape)) shape.Configure(zapper.Orientation, zapper.Length);
                else instance.transform.rotation = rotation;
                if (instance.TryGetComponent<HazardMover>(out var mover))
                {
                    mover.Configure(pool, ObstaclePoolId, speed, despawnX);
                    var tracked = new Tracked { Mover = mover, HalfExtents = extents[i], Speed = speed };
                    liveZappers.Add(tracked);
                    if (!clear) EvictCoinsNear(tracked);
                }
                ActiveObstacleCount++;
            }
        }

        private bool ClusterClearOfCoins(ZapperPlacement[] cluster, Vector2[] extents, float dy, float speed)
        {
            for (int i = 0; i < cluster.Length; i++)
            {
                var center = new Vector2(spawnPoint.position.x + cluster[i].Center.x, cluster[i].Center.y + dy);
                if (ConflictsWith(liveCoins, new Rect(center - extents[i], 2f * extents[i]), speed)) return false;
            }
            return true;
        }

        private void EvictCoinsNear(Tracked zapper)
        {
            var bounds = zapper.Bounds;
            for (int i = liveCoins.Count - 1; i >= 0; i--)
            {
                var coin = liveCoins[i];
                if (!SpawnSafety.WillOverlap(bounds, zapper.Speed, coin.Bounds, coin.Speed, CoinZapperClearance, despawnX)) continue;
                coin.Mover.Despawn();
                liveCoins.RemoveAt(i);
            }
        }

        // Rockets fly straight at spawnY; aiming happens beforehand via the tracking warning.
        internal void SpawnRocketNow(float? spawnY = null)
        {
            var snapshot = DifficultyEvaluator.Evaluate(gameManager.DistanceMeters, config);
            var pos = new Vector3(spawnPoint.position.x, spawnY ?? Random.Range(spawnYRange.x, spawnYRange.y), 0f);
            var instance = pool.Spawn(RocketPoolId, pos, Quaternion.identity);
            if (instance.TryGetComponent<HazardMover>(out var mover))
            {
                mover.Configure(pool, RocketPoolId, snapshot.ScrollSpeed * config.RocketSpeedMultiplier, despawnX);
            }
            RocketSpawned?.Invoke();
        }
    }
}
