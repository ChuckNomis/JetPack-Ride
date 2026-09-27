using UnityEngine;

namespace JetpackRide.Hazards
{
    public class ObstacleBehaviour : MonoBehaviour
    {
        // Marker component for static hazards (zappers). Collision/despawn logic
        // lives in HazardMover; player death is triggered by the "Hazard" tag
        // on this object's Collider2D, read by PlayerController.OnTriggerEnter2D.
    }
}
