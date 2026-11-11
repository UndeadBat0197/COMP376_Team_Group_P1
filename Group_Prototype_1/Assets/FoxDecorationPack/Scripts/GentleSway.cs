using UnityEngine;

namespace FoxDecorationPack
{
    public class GentleSway : MonoBehaviour
    {
        [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 0f, 1f);
        [SerializeField] private float angle = 4f;
        [SerializeField] private float speed = 1.2f;
        [SerializeField] private float phaseOffset = 0f;

        private Quaternion baseLocalRotation;

        void Awake()
        {
            baseLocalRotation = transform.localRotation;
            if (phaseOffset == 0f)
                phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            float s = Mathf.Sin(Time.time * speed + phaseOffset) * angle;
            transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(s, rotationAxis.normalized);
        }
    }
}
