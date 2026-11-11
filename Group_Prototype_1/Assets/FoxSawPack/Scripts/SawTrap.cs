using UnityEngine;

namespace FoxSawPack
{
    public class SawTrap : MonoBehaviour
    {
        [Header("Motion")]
        public bool moving = false;
        [Min(0f)] public float travelDistance = 6f;
        [Min(0.1f)] public float travelSpeed = 2.5f;
        public Vector3 travelDirection = Vector3.right;
        [Min(0f)] public float startDelay = 0f;

        [Header("Blade")]
        public Transform spinningBlade;
        public float spinDegreesPerSecond = 420f;

        [Header("Damage")]
        [Min(0f)] public float damage = 1f;
        [Min(0f)] public float hitCooldown = 1f;

        [Header("Visual")]
        public Transform movingCarriage;

        private Vector3 home;
        private float startedAt;

        private void Awake()
        {
            if (movingCarriage == null) movingCarriage = transform.Find("Carriage");
            if (spinningBlade == null && movingCarriage != null)
                spinningBlade = movingCarriage.Find("Blade/SpinVisual");
            if (movingCarriage != null) home = movingCarriage.localPosition;
            startedAt = Time.time;
        }

        private void Update()
        {
            float elapsed = Time.time - startedAt - startDelay;
            if (elapsed < 0f) return;

            if (spinningBlade != null)
                spinningBlade.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime, Space.Self);

            if (moving && movingCarriage != null)
            {
                Vector3 dir = travelDirection.sqrMagnitude < 0.0001f
                    ? Vector3.right : travelDirection.normalized;
                float d = Mathf.PingPong(elapsed * travelSpeed, Mathf.Max(0.001f, travelDistance));
                movingCarriage.localPosition = home + dir * d;
            }
        }
    }

    // Remains a separate component on the Blade child of both prefabs.
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
                // Collider.ClosestPoint correctly accounts for the fox's capsule/character controller.
                Vector3 onPlayer = playerCollider.ClosestPoint(closestSaw);
                gap = Vector3.Distance(closestSaw, onPlayer);
            }
            else
            {
                gap = Vector3.Distance(closestSaw, target);
            }

            if (gap <= hitRadius)
            {
                // PlayerHealth owns invulnerability frames and UI updates.
                player.TakeDamage(saw.damage);
                nextHitTime = Time.time + Mathf.Max(0.05f, saw.hitCooldown);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}
