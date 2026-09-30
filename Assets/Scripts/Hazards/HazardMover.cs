using UnityEngine;
using JetpackRide.Pooling;

namespace JetpackRide.Hazards
{
    public class HazardMover : MonoBehaviour, IPoolable
    {
        private ObjectPoolManager pool;
        private string poolId;
        private float speed;
        private float despawnX;

        public bool HasDespawned { get; private set; }
        // Stops scrolling (e.g. while the world is frozen on game over); cleared on every spawn.
        public bool Frozen { get; set; }

        public void Configure(ObjectPoolManager pool, string poolId, float speed, float despawnX)
        {
            this.pool = pool;
            this.poolId = poolId;
            this.speed = speed;
            this.despawnX = despawnX;
        }

        private void FixedUpdate()
        {
            if (Frozen) return;
            transform.position += Vector3.left * speed * Time.fixedDeltaTime;

            if (transform.position.x <= despawnX) Despawn();
        }

        // Returns this object to its pool (idempotent). Also used by pickups when collected.
        public void Despawn()
        {
            if (HasDespawned) return;
            HasDespawned = true;

            if (pool != null) pool.Despawn(poolId, gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned()
        {
            HasDespawned = false;
            Frozen = false;
        }

        public void OnDespawned()
        {
            HasDespawned = true;
        }
    }
}
