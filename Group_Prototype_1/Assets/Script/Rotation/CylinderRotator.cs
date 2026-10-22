
using UnityEngine;

public class CylinderRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 25f;   // degrees per second while a key is held
    public float maxAngle = 90f;        // limit in each direction from the start rotation

    [Header("Controls")]
    public KeyCode counterClockwiseKey = KeyCode.Q;
    public KeyCode clockwiseKey = KeyCode.E;

    private float currentAngle = 0f;
    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        // -1, 0 or +1 depending on which key is held this frame
        float input = 0f;
        if (Input.GetKey(counterClockwiseKey)) input += 1f;
        if (Input.GetKey(clockwiseKey))        input -= 1f;

        // Only rotates while a key is held; stops the moment you let go
        currentAngle += input * rotationSpeed * Time.deltaTime;
        currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);

        // Rotate around the cylinder's local Z axis
        transform.localRotation =
            initialRotation *
            Quaternion.Euler(0f, 0f, currentAngle);
    }
}
