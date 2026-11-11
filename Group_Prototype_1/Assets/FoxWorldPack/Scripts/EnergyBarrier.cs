using UnityEngine;

// Split out of FoxWorldEffects.cs: Unity needs one component class per file
// (named after the class), otherwise prefabs cannot keep the component.
namespace FoxWorldPack
{
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
}
