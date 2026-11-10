using UnityEngine;

public class CylinderMenuRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationAxis = Vector3.up;
    public float normalSpeed = 2f;

    [HideInInspector]
    public float currentSpeed;

    void Start()
    {
        currentSpeed = normalSpeed;
    }

    void Update()
    {
        // Smoothly rotate the cylinder every frame
        transform.Rotate(rotationAxis * currentSpeed * Time.deltaTime);
    }
}
