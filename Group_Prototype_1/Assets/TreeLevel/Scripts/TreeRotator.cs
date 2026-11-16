using UnityEngine;

/// <summary>
/// Rotates the tree's local Z axis. Choose between:
/// TargetAngle: hold Q toward +90 or R toward -90; release to stop instantly.
/// Automatic180: each tap starts an uninterruptible 180-degree turn.
/// Interruptible180: each tap starts a 180-degree turn; the opposite key
///     cancels the remainder and starts a new 180-degree turn in reverse.
/// Floor buttons, timed returns and checkpoints are still supported.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TreeRotator : MonoBehaviour
{
    [Header("Floor Button Rotation")]
    [Tooltip("Smoothing time for floor-button turns only.")]
    public float smoothTime = 0.55f;

    [Tooltip("Maximum speed of floor-button turns, in degrees/second.")]
    public float maxSpeed = 140f;

    // Explicit enum values mean existing Automatic180=0 scenes retain their mode.
    // In an older version, value 1 meant HoldToRotate; it now means TargetAngle.
    public enum RotationMode
    {
        Automatic180 = 0,
        TargetAngle = 1,
        Interruptible180 = 2
    }

    [Header("Keyboard Rotation")]
    public RotationMode rotationMode = RotationMode.Automatic180;
    public KeyCode turnLeftKey = KeyCode.Q;
    public KeyCode turnRightKey = KeyCode.R;

    [Tooltip("Duration of each 180-degree turn, including turns started by reversing.")]
    [Min(0.1f)] public float halfTurnDuration = 3f;

    [Tooltip("TargetAngle mode: hold Q toward +this angle, R toward -this angle. Release to stop.")]
    [Min(0f)] public float targetLimitAngle = 90f;

    [Tooltip("TargetAngle mode: constant speed in degrees/second, with no easing delay.")]
    [Min(0f)] public float targetRotationSpeed = 25f;

    [Header("Respawn")]
    [Tooltip("Checkpoints update this angle; the tree returns here upon death.")]
    public float checkpointAngle = 0f;

    public float CurrentAngle { get { return currentAngle; } }
    public float TargetAngle { get { return targetAngle; } }

    // Exposed for SlidingHoleCover, so it can recognize interrupted turns.
    public bool IsKeyboardHalfTurnActive { get { return keyboardTurnActive; } }
    // Used by SlidingHoleCover for hold-to-rotate TargetAngle mode.
    public bool IsTargetKeyboardActive { get { return targetKeyboardActive; } }
    public int TargetKeyboardDirection { get { return targetKeyboardDirection; } }
    public int KeyboardTurnSequence { get { return keyboardTurnSequence; } }
    public float KeyboardTurnStartAngle { get { return keyboardTurnStart; } }
    public float KeyboardTurnEndAngle { get { return keyboardTurnEnd; } }

    public bool IsTurning
    {
        get
        {
            return keyboardTurnActive || targetKeyboardActive ||
                   Mathf.Abs(targetAngle - currentAngle) > 0.5f;
        }
    }

    public bool Busy
    {
        get
        {
            return keyboardTurnActive || targetKeyboardActive ||
                   scriptedTurnActive || trapActive ||
                   Mathf.Abs(targetAngle - currentAngle) > 0.02f ||
                   Mathf.Abs(angularVelocity) > 0.01f;
        }
    }

    private float currentAngle;
    private float targetAngle;
    private float angularVelocity;
    private Quaternion initialRotation;

    private bool keyboardTurnActive;
    private bool targetKeyboardActive;
    private int targetKeyboardDirection; // +1 for Q, -1 for R, 0 when released
    private bool scriptedTurnActive;
    private float keyboardTurnElapsed;
    private float keyboardTurnStart;
    private float keyboardTurnEnd;
    private int keyboardDirection; // +1 for Q, -1 for R
    private int keyboardTurnSequence;
    private RotationMode lastRotationMode;

    private bool trapActive;
    private float trapHoldLeft;
    private float trapReturnAngle;

    private PlayerHealth playerHealth;
    private CharacterController playerController;
    private readonly Collider[] overlapBuffer = new Collider[32];

    void Awake()
    {
        initialRotation = transform.localRotation;
        lastRotationMode = rotationMode;
    }

    void Start()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerController = playerHealth.GetComponent<CharacterController>();
            playerHealth.onDied.AddListener(OnPlayerDied);
        }

        if (playerController == null)
            playerController = FindFirstObjectByType<CharacterController>();
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

    // Existing API used by TreeButton.cs.
    public void RotateTo(float angle)
    {
        if (keyboardTurnActive || targetKeyboardActive)
            return;

        targetAngle = angle;
        scriptedTurnActive = true;
    }

    // Existing API for trap buttons / timed doors.
    public void TurnAndReturn(float trapAngle, float holdSeconds)
    {
        if (Busy)
            return;

        trapReturnAngle = targetAngle;
        targetAngle = trapAngle;
        trapHoldLeft = Mathf.Max(0f, holdSeconds);
        trapActive = true;
        scriptedTurnActive = true;
    }

    // Existing checkpoint API: cancel any current or pending keyboard turn.
    public void HoldAt(float angle)
    {
        CancelAllTurns();
        targetAngle = currentAngle + Mathf.DeltaAngle(currentAngle, angle);
        scriptedTurnActive = true;
    }

    public void SnapTo(float angle)
    {
        CancelAllTurns();
        targetAngle = angle;
        currentAngle = angle;
        Apply();
    }

    void CancelAllTurns()
    {
        keyboardTurnActive = false;
        targetKeyboardActive = false;
        targetKeyboardDirection = 0;
        scriptedTurnActive = false;
        trapActive = false;
        angularVelocity = 0f;
        targetAngle = currentAngle;
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // Switching inspector modes during Play cancels keyboard motion cleanly.
        if (lastRotationMode != rotationMode)
        {
            if (keyboardTurnActive || targetKeyboardActive)
            {
                keyboardTurnActive = false;
                targetKeyboardActive = false;
                targetKeyboardDirection = 0;
                targetAngle = currentAngle;
                angularVelocity = 0f;
            }
            lastRotationMode = rotationMode;
        }

        bool leftDown = Input.GetKeyDown(turnLeftKey);
        bool rightDown = Input.GetKeyDown(turnRightKey);

        if (rotationMode == RotationMode.TargetAngle)
        {
            // TargetAngle is hold-to-rotate, limited to +/- targetLimitAngle.
            // Unlike a key-down command, this stops on the release frame.
            bool leftHeld = Input.GetKey(turnLeftKey);
            bool rightHeld = Input.GetKey(turnRightKey);

            if (leftHeld != rightHeld)
            {
                int direction = leftHeld ? +1 : -1;

                // Keyboard can change direction instantly. A floor-button or
                // timed-door rotation is not interrupted by keyboard input.
                if (targetKeyboardActive || !Busy)
                {
                    targetKeyboardActive = true;
                    targetKeyboardDirection = direction;
                    targetAngle = direction * targetLimitAngle;
                    angularVelocity = 0f;
                }
            }
            else if (targetKeyboardActive)
            {
                // Released both keys (or both held): freeze at the exact angle.
                targetKeyboardActive = false;
                targetKeyboardDirection = 0;
                targetAngle = currentAngle;
                angularVelocity = 0f;
            }
        }
        else if (leftDown != rightDown)
        {
            // Both 180-degree modes start on a press and continue on release.
            int direction = leftDown ? +1 : -1;

            if (rotationMode == RotationMode.Automatic180)
            {
                if (!Busy)
                    BeginHalfTurn(direction);
            }
            else // Interruptible180
            {
                if (keyboardTurnActive)
                {
                    // Only the opposite key can interrupt a running turn.
                    if (direction != keyboardDirection)
                        BeginHalfTurn(direction);
                }
                else if (!Busy)
                {
                    BeginHalfTurn(direction);
                }
            }
        }

        if (keyboardTurnActive)
        {
            keyboardTurnElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(keyboardTurnElapsed /
                                    Mathf.Max(0.1f, halfTurnDuration));

            // Lerp the unwrapped angle, so an exact 180-degree turn has
            // no short-path ambiguity and no slow-down near the end.
            currentAngle = Mathf.Lerp(keyboardTurnStart, keyboardTurnEnd, t);

            if (t >= 1f)
            {
                currentAngle = keyboardTurnEnd;
                targetAngle = currentAngle;
                keyboardTurnActive = false;
                angularVelocity = 0f;
            }

            Apply();
            PushPlayerOutOfObstacles();
            return;
        }

        if (targetKeyboardActive)
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle,
                                             targetRotationSpeed * Time.deltaTime);

            if (Mathf.Abs(targetAngle - currentAngle) < 0.0001f)
            {
                // The tree has reached +/- the limit. Keep the held-key
                // state until release, so the cover does not flicker closed.
                currentAngle = targetAngle;
                angularVelocity = 0f;
            }

            Apply();
            PushPlayerOutOfObstacles();
            return;
        }

        // Floor-button/timed-return logic from the existing TreeRotator.
        if (trapActive && !IsTurning)
        {
            trapHoldLeft -= Time.deltaTime;
            if (trapHoldLeft <= 0f)
            {
                trapActive = false;
                targetAngle = trapReturnAngle;
                scriptedTurnActive = true;
            }
        }

        if (Mathf.Approximately(currentAngle, targetAngle) &&
            Mathf.Abs(angularVelocity) < 0.01f)
        {
            if (!trapActive)
                scriptedTurnActive = false;
            return;
        }

        currentAngle = Mathf.SmoothDamp(currentAngle, targetAngle,
                                       ref angularVelocity, smoothTime,
                                       maxSpeed, Time.deltaTime);

        if (Mathf.Abs(targetAngle - currentAngle) < 0.02f)
        {
            currentAngle = targetAngle;
            angularVelocity = 0f;
            if (!trapActive)
                scriptedTurnActive = false;
        }

        Apply();
        PushPlayerOutOfObstacles();
    }

    void BeginHalfTurn(int direction)
    {
        keyboardTurnActive = true;
        targetKeyboardActive = false;
        targetKeyboardDirection = 0;
        keyboardDirection = direction;
        keyboardTurnElapsed = 0f;
        keyboardTurnStart = currentAngle;
        keyboardTurnEnd = currentAngle + direction * 180f;
        targetAngle = keyboardTurnEnd;
        angularVelocity = 0f;
        keyboardTurnSequence++;
    }

    void Apply()
    {
        transform.localRotation =
            initialRotation * Quaternion.Euler(0f, 0f, currentAngle);
        Physics.SyncTransforms();
    }

    // Preserve overlap correction used by the original TreeRotator.
    void PushPlayerOutOfObstacles()
    {
        if (playerController == null || !playerController.enabled)
            return;

        Bounds bounds = playerController.bounds;
        int count = Physics.OverlapSphereNonAlloc(
            bounds.center, bounds.extents.magnitude,
            overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapBuffer[i];
            if (other == null || other == playerController)
                continue;
            if (!other.transform.IsChildOf(transform))
                continue;

            MeshCollider meshCollider = other as MeshCollider;
            if (meshCollider != null && !meshCollider.convex)
                continue;

            bool overlapping = Physics.ComputePenetration(
                playerController, playerController.transform.position,
                playerController.transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 direction, out float distance);

            if (overlapping && distance > 0f)
                playerController.Move(direction * (distance + 0.02f));
        }
    }
}
