
using UnityEngine;

public class CylinderRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 25f;
    public float maxAngle = 90f;

    [Header("Controls")]
    public KeyCode counterClockwiseKey = KeyCode.Q;
    public KeyCode clockwiseKey = KeyCode.R;

    private float currentAngle = 0f;
    private float targetAngle = 0f;

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        // Q Counterclockwise rotation
        if (Input.GetKeyDown(counterClockwiseKey))
        {
            targetAngle = maxAngle;
        }

        // R Clockwise rotation
        if (Input.GetKeyDown(clockwiseKey))
        {
            targetAngle = -maxAngle;
        }

        // Automatically rotate toward the target
        currentAngle = Mathf.MoveTowards(
            currentAngle,
            targetAngle,
            rotationSpeed * Time.deltaTime
        );

        // Rotate around the cylinder's local Z axis
        transform.localRotation =
            initialRotation *
            Quaternion.Euler(0f, 0f, currentAngle);
    }
}
