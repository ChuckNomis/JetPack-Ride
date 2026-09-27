using UnityEngine;
using JetpackRide.Pooling;

// Lives in its own file (matching class name) so Instantiate can clone it like any other script.
public class ProbePoolable : MonoBehaviour, IPoolable
{
    public int SpawnedCount;
    public int DespawnedCount;
    public void OnSpawned() => SpawnedCount++;
    public void OnDespawned() => DespawnedCount++;
}
