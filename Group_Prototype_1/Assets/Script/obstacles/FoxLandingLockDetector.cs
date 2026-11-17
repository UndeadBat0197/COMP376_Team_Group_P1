using UnityEngine;

/// <summary>
/// Attach to the SAME GameObject as the fox's CharacterController.
/// Detects landing on a LockingPlatform using real controller collisions.
/// No changes to the existing player movement script are required.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FoxLandingLockDetector : MonoBehaviour
{
    [Header("Landing detection")]
    [Tooltip("Minimum upward-facing contact normal. 0.65 rejects side contacts.")]
    [Range(0f, 1f)] public float minimumUpwardNormal = 0.65f;

    private LockingPlatform pendingLock;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null)
            return;

        // Only the top/upper surface counts as a landing; side hits don't.
        if (Vector3.Dot(hit.normal, Vector3.up) < minimumUpwardNormal)
            return;

        // Do not lock a platform when the fox hits it while moving upward.
        if (hit.moveDirection.y > 0.05f)
            return;

        LockingPlatform platform = hit.collider.GetComponentInParent<LockingPlatform>();
        if (platform == null || platform.IsLocked)
            return;

        pendingLock = platform;
    }

    private void LateUpdate()
    {
        if (pendingLock == null)
            return;

        LockingPlatform platform = pendingLock;
        pendingLock = null;

        // Defer reparenting until after CharacterController.Move completes.
        if (platform != null)
            platform.Lock();
    }
}
