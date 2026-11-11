
using UnityEngine;

// Runs before the player script, so the player always moves against
// the cylinder's new rotation instead of last frame's.
[DefaultExecutionOrder(-50)]
public class CylinderRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 25f;   // degrees per second while a key is held
    public float maxAngle = 90f;        // limit in each direction from the start rotation

    [Tooltip("How quickly the rotation speeds up and slows down (degrees/s²). " +
             "Lower = softer start and stop.")]
    public float rotationAcceleration = 120f;

    [Header("Controls")]
    public KeyCode counterClockwiseKey = KeyCode.Q;
    public KeyCode clockwiseKey = KeyCode.E;

    private float currentAngle = 0f;
    private float currentSpeed = 0f;
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

        // Ease in and out instead of starting/stopping instantly.
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            input * rotationSpeed,
            rotationAcceleration * Time.deltaTime);

        float previousAngle = currentAngle;

        currentAngle += currentSpeed * Time.deltaTime;
        currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);

        // Stop pushing against the limit.
        if (Mathf.Approximately(Mathf.Abs(currentAngle), maxAngle) &&
            Mathf.Sign(currentSpeed) == Mathf.Sign(currentAngle))
        {
            currentSpeed = 0f;
        }

        if (Mathf.Approximately(previousAngle, currentAngle))
            return;

        // Rotate around the cylinder's local Z axis
        transform.localRotation =
            initialRotation *
            Quaternion.Euler(0f, 0f, currentAngle);

        // Colliders moved through the Transform are normally only
        // updated on the next physics step. Updating them now stops the
        // CharacterController from colliding with where the cylinder
        // and platforms used to be, which made the player jitter.
        Physics.SyncTransforms();
    }
}
