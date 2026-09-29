using UnityEngine;
using JetpackRide.Pooling;

namespace JetpackRide.Hazards
{
    public class RocketWarningIndicator : MonoBehaviour, IPoolable
    {
        private ObjectPoolManager pool;
        private string poolId;
        private int spawnGeneration;

        public void Configure(ObjectPoolManager pool, string poolId)
        {
            this.pool = pool;
            this.poolId = poolId;
        }

        public async Awaitable PlayAndDespawnAsync(float lifetimeSeconds)
        {
            // If a run reset despawns this warning and the pool hands it out again before the
            // delay ends, the generation changes and this stale call must leave the new use alone.
            int generation = spawnGeneration;
            await Awaitable.WaitForSecondsAsync(lifetimeSeconds);
            if (this == null || pool == null || generation != spawnGeneration) return;
            pool.Despawn(poolId, gameObject);
        }

        public void OnSpawned() => spawnGeneration++;
        public void OnDespawned() { }
    }
}
