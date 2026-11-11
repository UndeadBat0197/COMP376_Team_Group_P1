using UnityEngine;

/// <summary>
/// Turns the tree trunk (and everything parented to it) around its
/// local Z axis. Buttons call RotateTo() or TurnAndReturn(); the trunk
/// then eases to that angle. Runs before the player script so the player always collides
/// with the trunk's current pose.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TreeRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Roughly how long a turn takes, in seconds.")]
    public float smoothTime = 0.55f;

    [Tooltip("Top turning speed in degrees per second.")]
    public float maxSpeed = 140f;

    [Header("Optional keyboard control (for testing)")]
    public bool allowKeyboard = false;
    public float keyboardSpeed = 40f;
    public KeyCode counterClockwiseKey = KeyCode.Q;
    public KeyCode clockwiseKey = KeyCode.E;

    [Header("Respawn")]
    [Tooltip("Angle the trunk returns to when the player dies. " +
             "Checkpoints update this.")]
    public float checkpointAngle = 0f;

    public float CurrentAngle { get { return currentAngle; } }
    public float TargetAngle { get { return targetAngle; } }

    public bool IsTurning
    {
        get { return Mathf.Abs(targetAngle - currentAngle) > 0.5f; }
    }

    /// <summary>True while turning or while a trap turn is playing.</summary>
    public bool Busy
    {
        get { return IsTurning || trapActive; }
    }

    private float currentAngle;
    private float targetAngle;
    private float angularVelocity;

    private bool trapActive;
    private float trapHoldLeft;
    private float trapReturnAngle;
    private Quaternion initialRotation;

    private PlayerHealth playerHealth;
    private CharacterController playerController;
    private readonly Collider[] overlapBuffer = new Collider[32];

    void Awake()
    {
        initialRotation = transform.localRotation;
    }

    void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerController =
                playerHealth.GetComponent<CharacterController>();

            playerHealth.onDied.AddListener(OnPlayerDied);
        }

        if (playerController == null)
        {
            playerController =
                FindFirstObjectByType<CharacterController>();
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.onDied.RemoveListener(OnPlayerDied);
    }

    void OnPlayerDied()
    {
        SnapTo(checkpointAngle);
    }

    /// <summary>Ease to this angle (degrees around local Z).</summary>
    public void RotateTo(float angle)
    {
        targetAngle = angle;
    }

    /// <summary>
    /// Turn to "angle", stay there for holdSeconds, then turn back to
    /// where the trunk was. Used for trap buttons and timed doors.
    /// </summary>
    public void TurnAndReturn(float trapAngle, float holdSeconds)
    {
        if (trapActive)
            return;

        trapReturnAngle = targetAngle;
        targetAngle = trapAngle;
        trapHoldLeft = holdSeconds;
        trapActive = true;
    }

    /// <summary>
    /// Settle on this angle and cancel any pending "turn back". Called
    /// by checkpoints: once the player is through a timed door the tree
    /// must not turn back. Takes the shortest way round.
    /// </summary>
    public void HoldAt(float angle)
    {
        trapActive = false;
        targetAngle = currentAngle + Mathf.DeltaAngle(currentAngle, angle);
    }

    /// <summary>Jump straight to this angle, no animation.</summary>
    public void SnapTo(float angle)
    {
        trapActive = false;
        targetAngle = angle;
        currentAngle = angle;
        angularVelocity = 0f;
        Apply();
    }

    void Update()
    {
        if (allowKeyboard)
        {
            float input = 0f;
            if (Input.GetKey(counterClockwiseKey)) input += 1f;
            if (Input.GetKey(clockwiseKey)) input -= 1f;

            targetAngle += input * keyboardSpeed * Time.deltaTime;
        }

        if (trapActive && !IsTurning)
        {
            // Sitting in the trap position: wait, then turn back.
            trapHoldLeft -= Time.deltaTime;

            if (trapHoldLeft <= 0f)
            {
                trapActive = false;
                targetAngle = trapReturnAngle;
            }
        }

        if (Mathf.Approximately(currentAngle, targetAngle) &&
            Mathf.Abs(angularVelocity) < 0.01f)
        {
            return;
        }

        currentAngle = Mathf.SmoothDamp(
            currentAngle,
            targetAngle,
            ref angularVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime);

        if (Mathf.Abs(targetAngle - currentAngle) < 0.02f)
        {
            currentAngle = targetAngle;
            angularVelocity = 0f;
        }

        Apply();
        PushPlayerOutOfObstacles();
    }

    void Apply()
    {
        transform.localRotation =
            initialRotation * Quaternion.Euler(0f, 0f, currentAngle);

        // Update the colliders now instead of on the next physics
        // step, so the player never collides with the old pose.
        Physics.SyncTransforms();
    }

    /// <summary>
    /// While the trunk turns, solid props attached to it sweep
    /// sideways. If one ends up inside the player, move the player
    /// out of it instead of letting them clip through.
    /// </summary>
    void PushPlayerOutOfObstacles()
    {
        if (playerController == null || !playerController.enabled)
            return;

        Bounds bounds = playerController.bounds;

        int count = Physics.OverlapSphereNonAlloc(
            bounds.center,
            bounds.extents.magnitude,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapBuffer[i];

            if (other == null || other == playerController)
                continue;

            if (!other.transform.IsChildOf(transform))
                continue;

            // ComputePenetration does not support non-convex meshes
            // (the trunk itself and the ring walls).
            MeshCollider meshCollider = other as MeshCollider;
            if (meshCollider != null && !meshCollider.convex)
                continue;

            bool overlapping = Physics.ComputePenetration(
                playerController,
                playerController.transform.position,
                playerController.transform.rotation,
                other,
                other.transform.position,
                other.transform.rotation,
                out Vector3 direction,
                out float distance);

            if (overlapping && distance > 0f)
                playerController.Move(direction * (distance + 0.02f));
        }
    }
}
