using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace JetpackRide.Pooling
{
    public class ObjectPoolManager : MonoBehaviour
    {
        [Serializable]
        public class PoolEntry
        {
            public string id;
            public GameObject prefab;
            public int defaultCapacity = 10;
            public int maxSize = 50;
        }

        [SerializeField] private List<PoolEntry> poolEntries = new();

        private readonly Dictionary<string, ObjectPool<GameObject>> pools = new();
        private readonly Dictionary<string, HashSet<GameObject>> activeByPool = new();
        private readonly List<IPoolable> poolableBuffer = new();

        public GameObject Spawn(string id, Vector3 position, Quaternion rotation)
        {
            var instance = GetPool(id).Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            activeByPool[id].Add(instance);
            NotifyPoolables(instance, spawned: true);
            return instance;
        }

        public void Despawn(string id, GameObject instance)
        {
            // Idempotent: a second Despawn (e.g. self-despawn racing DespawnAll) must not push the
            // instance onto the pool twice, or two later Spawn calls would hand out the same object.
            if (!activeByPool.TryGetValue(id, out var active) || !active.Remove(instance)) return;
            pools[id].Release(instance);
        }

        public void DespawnAll()
        {
            foreach (var kvp in activeByPool)
            {
                var instances = new List<GameObject>(kvp.Value);
                foreach (var instance in instances)
                {
                    if (instance != null) Despawn(kvp.Key, instance);
                }
                kvp.Value.Clear();
            }
        }

        // Pools are built on first use rather than in Awake, so entries assigned after
        // AddComponent (tests, runtime setup) are honoured.
        private ObjectPool<GameObject> GetPool(string id)
        {
            if (pools.TryGetValue(id, out var existing)) return existing;

            var entry = poolEntries.Find(e => e.id == id)
                ?? throw new ArgumentException($"No pool entry with id '{id}'", nameof(id));

            activeByPool[id] = new HashSet<GameObject>();
            var pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(entry.prefab, transform),
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go =>
                {
                    NotifyPoolables(go, spawned: false);
                    go.SetActive(false);
                },
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: entry.defaultCapacity,
                maxSize: entry.maxSize);
            pools[id] = pool;
            return pool;
        }

        // Every IPoolable on the object is notified: pooled prefabs pair HazardMover with
        // RocketBehaviour/CoinBehaviour, and each needs its per-spawn reset.
        private void NotifyPoolables(GameObject go, bool spawned)
        {
            go.GetComponents(poolableBuffer);
            foreach (var poolable in poolableBuffer)
            {
                if (spawned) poolable.OnSpawned();
                else poolable.OnDespawned();
            }
            poolableBuffer.Clear();
        }
    }
}
