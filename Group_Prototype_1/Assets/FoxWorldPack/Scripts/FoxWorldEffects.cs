using UnityEngine;

namespace FoxWorldPack
{
    public class WorldPropBob : MonoBehaviour
    {
        public float speed = 1.5f;
        public float amplitude = 0.1f;
        private Vector3 initial;
        private void Awake() { initial = transform.localPosition; }
        private void Update() { transform.localPosition = initial + Vector3.up * (Mathf.Sin(Time.time * speed) * amplitude); }
    }

    // Barrier damage and periodic on/off. Collider on the root is a trigger.
    // Does not reference or overwrite any project gameplay classes at compile time.
    [RequireComponent(typeof(BoxCollider))]
    public class EnergyBarrier : MonoBehaviour
    {
        public float activeSeconds = 2.5f;
        public float inactiveSeconds = 1.5f;
        public float damage = 1f;
        public float hitCooldown = 1f;
        public GameObject fieldVisual;
        private bool active;
        private float nextHit;
        private Collider[] cached = new Collider[16];
        private BoxCollider trigger;
        private void Awake() { trigger = GetComponent<BoxCollider>(); trigger.isTrigger = true; }
        private void Update()
        {
            float cycle = Mathf.Max(.05f, activeSeconds) + Mathf.Max(.05f, inactiveSeconds);
            active = (Time.time % cycle) < activeSeconds;
            if (fieldVisual && fieldVisual.activeSelf != active) fieldVisual.SetActive(active);
            if (!active || Time.time < nextHit) return;
            // OverlapBox works even if a CharacterController stays inside while the field reactivates.
            var center = transform.TransformPoint(trigger.center);
            var half = Vector3.Scale(trigger.size * .5f, transform.lossyScale);
            int n = Physics.OverlapBoxNonAlloc(center, new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)), cached, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = cached[i];
                if (!c || c.transform.IsChildOf(transform)) continue;
                var health = c.GetComponentInParent<PlayerHealth>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                    nextHit = Time.time + Mathf.Max(.1f, hitCooldown);
                    break;
                }
            }
        }
    }

    public class ProximityDoor : MonoBehaviour
    {
        public Transform doorLeaf;
        public float detectionDistance = 4f;
        public float liftHeight = 3f;
        public float openSpeed = 2f;
        private Vector3 closedPosition;
        private Transform player;
        private void Start()
        {
            if (!doorLeaf) return;
            closedPosition = doorLeaf.localPosition;
            var hp = FindObjectOfType<PlayerHealth>();
            if (hp) player = hp.transform;
        }
        private void Update()
        {
            if (!doorLeaf || !player) return;
            bool near = Vector3.Distance(player.position, transform.position) < detectionDistance;
            var target = closedPosition + Vector3.up * (near ? liftHeight : 0f);
            doorLeaf.localPosition = Vector3.MoveTowards(doorLeaf.localPosition, target, openSpeed * Time.deltaTime);
        }
    }
}
