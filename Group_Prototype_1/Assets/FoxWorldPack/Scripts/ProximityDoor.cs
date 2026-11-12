using UnityEngine;

// Split out of FoxWorldEffects.cs: Unity needs one component class per file
// (named after the class), otherwise prefabs cannot keep the component.
namespace FoxWorldPack
{
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
