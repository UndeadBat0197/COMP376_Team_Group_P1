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
}
