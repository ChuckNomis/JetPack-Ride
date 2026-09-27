using UnityEngine;
using JetpackRide.Pooling;

namespace JetpackRide.Hazards
{
    public class RocketBehaviour : MonoBehaviour, IPoolable
    {
        [SerializeField] private Transform target;
        [SerializeField] private float homingVerticalSpeed = 3f;

        private bool homing;

        public void Init(bool homing)
        {
            this.homing = homing;
        }

        public void SetTarget(Transform target)
        {
            this.target = target;
        }

        // HazardMover handles the leftward scroll; homing only steers the rocket's Y toward
        // the target's current Y at a capped rate, so the player can still out-fly it.
        private void FixedUpdate()
        {
            if (!homing || target == null) return;

            var pos = transform.position;
            pos.y = Mathf.MoveTowards(pos.y, target.position.y, homingVerticalSpeed * Time.fixedDeltaTime);
            transform.position = pos;
        }

        public void OnSpawned()
        {
            homing = false;
        }

        public void OnDespawned()
        {
        }
    }
}
