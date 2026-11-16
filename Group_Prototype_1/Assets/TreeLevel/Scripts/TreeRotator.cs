using UnityEngine;

/// <summary>
/// Turns the tree trunk (and everything parented to it) around its local Z axis.
/// Q / R start a complete, uninterruptible 180-degree turn. Floor buttons still
/// use RotateTo / TurnAndReturn; checkpoints still use HoldAt / SnapTo.
/// Runs before the player movement script for up-to-date collider positions.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TreeRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Roughly how long a turn takes, in seconds.")]
    public float smoothTime = 0.55f;

    [Tooltip("Top turning speed in degrees per second.")]
    public float maxSpeed = 140f;

    [Header("Keyboard: one press = 180-degree turn")]
    [Tooltip("Seconds required to complete the automatic 180-degree turn.")]
    [Min(0.1f)] public float halfTurnDuration = 3f;

    // Renamed fields intentionally: old serialized E-key bindings from the
    // previous version will not override these Q / R defaults.
    public KeyCode turnLeftKey = KeyCode.Q;
    public KeyCode turnRightKey = KeyCode.R;

    [Header("Respawn")]
    [Tooltip("Angle the trunk returns to when the player dies. " +
             "Checkpoints update this.")]
    public float checkpointAngle = 0f;

    public float CurrentAngle { get { return currentAngle; } }
    public float TargetAngle { get { return targetAngle; } }

    public bool IsTurning
    {
        get { return keyboardTurnActive || Mathf.Abs(targetAngle - currentAngle) > 0.5f; }
    }

    /// <summary>Stops buttons or a second key press from starting another turn.</summary>
    public bool Busy
    {
        get
        {
            return keyboardTurnActive || trapActive ||
                   Mathf.Abs(targetAngle - currentAngle) > 0.02f ||
                   Mathf.Abs(angularVelocity) > 0.01f;
        }
    }

    private float currentAngle;
    private float targetAngle;
    private float angularVelocity;

    private bool keyboardTurnActive;
    private float keyboardTurnElapsed;
    private float keyboardTurnStart;
    private float keyboardTurnEnd;

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
        if (keyboardTurnActive) return;
        targetAngle = angle;
    }

    /// <summary>
    /// Turn to "angle", stay there for holdSeconds, then turn back to
    /// where the trunk was. Used for trap buttons and timed doors.
    /// </summary>
    public void TurnAndReturn(float trapAngle, float holdSeconds)
    {
        if (trapActive || keyboardTurnActive)
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
        // Checkpoints take priority over any automatic key turn.
        keyboardTurnActive = false;
        trapActive = false;
        angularVelocity = 0f;
        targetAngle = currentAngle + Mathf.DeltaAngle(currentAngle, angle);
    }

    /// <summary>Jump straight to this angle, no animation.</summary>
    public void SnapTo(float angle)
    {
        keyboardTurnActive = false;
        trapActive = false;
        targetAngle = angle;
        currentAngle = angle;
        angularVelocity = 0f;
        Apply();
    }

    void Update()
    {
        // One key-down event starts one complete turn. Holding a key does
        // nothing extra, and key presses during a turn are discarded.
        // Paused menus (timeScale = 0) cannot queue a new rotation.
        if (Time.timeScale > 0f && !Busy)
        {
            if (Input.GetKeyDown(turnLeftKey))
                BeginHalfTurn(+1f);
            else if (Input.GetKeyDown(turnRightKey))
                BeginHalfTurn(-1f);
        }

        if (keyboardTurnActive)
        {
            keyboardTurnElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(keyboardTurnElapsed / Mathf.Max(0.1f, halfTurnDuration));
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            currentAngle = Mathf.Lerp(keyboardTurnStart, keyboardTurnEnd, easedT);

            if (t >= 1f)
            {
                currentAngle = keyboardTurnEnd;
                targetAngle = keyboardTurnEnd;
                keyboardTurnActive = false;
                angularVelocity = 0f;
            }

            Apply();
            PushPlayerOutOfObstacles();
            return;
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

    void BeginHalfTurn(float direction)
    {
        keyboardTurnActive = true;
        keyboardTurnElapsed = 0f;
        keyboardTurnStart = currentAngle;
        keyboardTurnEnd = currentAngle + direction * 180f;
        targetAngle = keyboardTurnEnd;
        angularVelocity = 0f;
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
