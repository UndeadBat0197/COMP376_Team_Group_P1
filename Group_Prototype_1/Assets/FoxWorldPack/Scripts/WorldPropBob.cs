using UnityEngine;

// Split out of FoxWorldEffects.cs: Unity needs one component class per file
// (named after the class), otherwise prefabs cannot keep the component.
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
}
