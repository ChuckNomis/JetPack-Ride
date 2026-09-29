using System;
using UnityEngine;
using JetpackRide.Hazards;
using JetpackRide.Pooling;

namespace JetpackRide.Pickups
{
    public class CoinBehaviour : MonoBehaviour, IPoolable
    {
        public bool Collected { get; private set; }
        public event Action OnCollected;

        [SerializeField] private GameObject sparklePrefab;

        public void Collect()
        {
            if (Collected) return;
            Collected = true;
            if (sparklePrefab != null) Instantiate(sparklePrefab, transform.position, Quaternion.identity);
            OnCollected?.Invoke();

            // Return to the pool rather than just deactivating, or collected coins would stay
            // checked out of the pool for the rest of the run.
            if (TryGetComponent<HazardMover>(out var mover)) mover.Despawn();
            else gameObject.SetActive(false);
        }

        public void OnSpawned()
        {
            Collected = false;
        }

        public void OnDespawned()
        {
        }
    }
}
