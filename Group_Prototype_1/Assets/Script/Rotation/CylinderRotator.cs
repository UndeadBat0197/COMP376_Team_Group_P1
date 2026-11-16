using UnityEngine;

// One key press starts one complete, non-interruptible rotation.
// Runs before the player's movement so collision checks use the new orientation.
[DefaultExecutionOrder(-50)]
public class CylinderRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Min(1f)] public float degreesPerTurn = 180f;
    [Min(0.05f)] public float turnDuration = 3f;

    [Header("Controls")]
    public KeyCode counterClockwiseKey = KeyCode.Q;
    public KeyCode clockwiseKey = KeyCode.R;

    private Quaternion initialRotation;
    private float currentAngle;
    private float startAngle;
    private float targetAngle;
    private float elapsed;
    private bool isRotating;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        // Do not accept rotation commands while the game is paused.
        if (Time.timeScale <= 0f)
            return;

        if (!isRotating)
        {
            // GetKeyDown fires once, regardless of how long a key is held.
            // Commands during an ongoing rotation are ignored.
            bool turnLeft = Input.GetKeyDown(counterClockwiseKey);
            bool turnRight = Input.GetKeyDown(clockwiseKey);

            if (turnLeft != turnRight)
            {
                startAngle = currentAngle;
                targetAngle = currentAngle + (turnLeft ? degreesPerTurn : -degreesPerTurn);
                elapsed = 0f;
                isRotating = true;
            }
        }

        if (!isRotating)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, turnDuration));

        // Interpolate unwrapped angle values: never use LerpAngle for exact 180-degree turns.
        currentAngle = Mathf.Lerp(startAngle, targetAngle, t);
        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, currentAngle);

        // Keep collider transforms in sync with CharacterController movement.
        Physics.SyncTransforms();

        if (t >= 1f)
        {
            currentAngle = targetAngle;
            isRotating = false;
        }
    }
}
