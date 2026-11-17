using UnityEngine;

/// <summary>
/// Put this on each of the three platforms that initially rotate with the tree.
/// When FoxLandingLockDetector reports a landing, the platform moves to the
/// non-rotating hierarchy while keeping its current world transform.
/// </summary>
public class LockingPlatform : MonoBehaviour
{
    [Header("Hierarchy")]
    [Tooltip("Drag 'World (does not turn)' here. It must NOT be a child of the rotating tree.")]
    public Transform stationaryParent;

    [Header("Optional feedback")]
    [Tooltip("Optional child GameObject to show once this platform is locked.")]
    public GameObject lockedIndicator;

    [SerializeField] private bool isLocked;
    public bool IsLocked => isLocked;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    private void Awake()
    {
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;

        isLocked = false;
        if (lockedIndicator != null)
            lockedIndicator.SetActive(false);
    }

    public void Lock()
    {
        if (isLocked)
            return;

        if (stationaryParent == null)
        {
            Debug.LogWarning($"{name}: Set 'Stationary Parent' on LockingPlatform first.", this);
            return;
        }

        if (stationaryParent == transform || stationaryParent.IsChildOf(transform))
        {
            Debug.LogError($"{name}: Stationary Parent cannot be this platform or its child.", this);
            return;
        }

        // true preserves world position, rotation, and (where possible) world scale.
        transform.SetParent(stationaryParent, true);
        isLocked = true;

        if (lockedIndicator != null)
            lockedIndicator.SetActive(true);

        Physics.SyncTransforms();
        Debug.Log($"Locked platform: {name}", this);
    }

    /// <summary>
    /// Optional reset for death/retry. Invoke from your checkpoint reset logic.
    /// </summary>
    public void ResetToRotating()
    {
        if (originalParent == null)
        {
            Debug.LogWarning($"{name}: Original parent is missing; cannot reset.", this);
            return;
        }

        transform.SetParent(originalParent, false);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
        isLocked = false;

        if (lockedIndicator != null)
            lockedIndicator.SetActive(false);

        Physics.SyncTransforms();
    }
}
