using UnityEngine;

namespace FoxSawPack
{
    // Lives on the Blade child of both saw prefabs.
    // Unity can only store a component in a prefab when its class has a
    // file of the same name; while this class was inside SawTrap.cs the
    // prefabs lost it ("missing script") and the saws did no damage.
    public class SawDamageZone : MonoBehaviour
    {
        [SerializeField] private SawTrap saw;
        [Tooltip("Effective radius around the blade center. Normal blade radius is 0.96.")]
        [Min(0.1f)] public float hitRadius = 1.08f;

        private PlayerHealth player;
        private Collider playerCollider;
        private float nextHitTime;
        private Vector3 lastPosition;
        private bool hasLastPosition;

        private void Awake()
        {
            if (saw == null) saw = GetComponentInParent<SawTrap>();
        }

        private void FixedUpdate()
        {
            if (saw == null || !saw.isActiveAndEnabled || saw.damage <= 0f) return;

            if (player == null)
            {
                player = FindObjectOfType<PlayerHealth>();
                if (player == null) return;
                playerCollider = player.GetComponent<CharacterController>();
                if (playerCollider == null) playerCollider = player.GetComponentInChildren<Collider>();
                if (playerCollider == null) playerCollider = player.GetComponentInParent<Collider>();
            }
            if (player.IsDead || Time.time < nextHitTime) return;

            // The moving blade can travel across the player between physics updates;
            // check the complete swept segment, not only the current center.
            Vector3 current = transform.position;
            Vector3 previous = hasLastPosition ? lastPosition : current;
            lastPosition = current;
            hasLastPosition = true;

            Vector3 target = playerCollider != null && playerCollider.enabled
                ? playerCollider.bounds.center : player.transform.position;
            Vector3 seg = current - previous;
            float t = seg.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(target - previous, seg) / seg.sqrMagnitude)
                : 1f;
            Vector3 closestSaw = previous + seg * t;

            float gap;
            if (playerCollider != null && playerCollider.enabled)
            {
                // Collider.ClosestPoint does not support a CharacterController,
                // so use its bounding box instead.
                Vector3 onPlayer = playerCollider.bounds.ClosestPoint(closestSaw);
                gap = Vector3.Distance(closestSaw, onPlayer);
            }
            else
            {
                gap = Vector3.Distance(closestSaw, target);
            }

            // hitRadius is in the prefab's own units; follow the saw's scale.
            float worldRadius = hitRadius * Mathf.Abs(saw.transform.lossyScale.x);

            if (gap <= worldRadius)
            {
                // PlayerHealth owns invulnerability frames and UI updates.
                player.TakeDamage(saw.damage);
                nextHitTime = Time.time + Mathf.Max(0.05f, saw.hitCooldown);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitRadius * Mathf.Abs(transform.lossyScale.x));
        }
    }
}
