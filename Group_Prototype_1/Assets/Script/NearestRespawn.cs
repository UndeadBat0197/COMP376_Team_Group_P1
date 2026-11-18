using UnityEngine;

/// <summary>
/// Activated checkpoints by area. Checkpoints are ordered from the start
/// to the end of each area in the Inspector arrays. Passing an earlier
/// checkpoint again never loses the most advanced unlocked checkpoint.
///
/// This deliberately keeps the class name and existing serialized fields
/// from the previous NearestRespawn script, so existing references and
/// RespawnAreaTrigger.SetInside() remain compatible.
/// </summary>
[DefaultExecutionOrder(-100)]
public class NearestRespawn : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;
    public TreeRotator treeRotator;

    [Header("Respawn Points - ORDER FROM START TO FINISH")]
    public Transform[] outsideRespawnPoints;
    public Transform[] insideRespawnPoints;

    [Header("Fallbacks (used until a checkpoint is activated in this area)")]
    public Transform outsideFallback;
    public Transform insideFallback;

    [Header("Current Area")]
    [Tooltip("Enable if the fox begins the scene inside the trunk.")]
    public bool startsInside = false;

    [Header("Optional tree orientation on respawn")]
    [Tooltip("Remember the tree's angle when reaching a checkpoint, then restore it on death.")]
    public bool restoreCheckpointTreeAngle = true;
    public float outsideFallbackTreeAngle = 0f;
    public float insideFallbackTreeAngle = 0f;

    public bool IsInside { get; private set; }

    private int highestInside = -1;
    private int highestOutside = -1;
    private float[] insideRecordedAngles;
    private float[] outsideRecordedAngles;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (treeRotator == null)
            treeRotator = FindFirstObjectByType<TreeRotator>();

        IsInside = startsInside;
        EnsureAngleArrays();
    }

    private void Start()
    {
        ApplyRespawnForCurrentArea();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.onDied.AddListener(ApplyRespawnForCurrentArea);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.onDied.RemoveListener(ApplyRespawnForCurrentArea);
    }

    /// <summary>Called by your existing inside/outside markers.</summary>
    public void SetInside(bool inside)
    {
        if (IsInside == inside)
            return;

        IsInside = inside;
        Debug.Log("[Checkpoint] Area: " + (inside ? "INSIDE" : "OUTSIDE"), this);
        ApplyRespawnForCurrentArea();
    }

    /// <summary>Called by RespawnCheckpoint when the fox reaches a point.</summary>
    public void ActivateCheckpoint(Transform point)
    {
        if (point == null)
            return;

        EnsureAngleArrays();

        int insideIndex = FindIndex(insideRespawnPoints, point);
        int outsideIndex = FindIndex(outsideRespawnPoints, point);

        if (insideIndex < 0 && outsideIndex < 0)
        {
            Debug.LogWarning(
                "[Checkpoint] '" + point.name +
                "' is missing from BOTH NearestRespawn arrays. Add it to the appropriate array.",
                point);
            return;
        }

        // Each checkpoint belongs to one area only.
        if (insideIndex >= 0 && outsideIndex >= 0)
        {
            Debug.LogWarning("[Checkpoint] Point is listed in both areas: " + point.name, point);
            return;
        }

        bool inside = insideIndex >= 0;
        int index = inside ? insideIndex : outsideIndex;
        int previousHighest = inside ? highestInside : highestOutside;

        // The area marker normally changes this first, but a checkpoint
        // can also identify its own area reliably.
        bool areaChanged = IsInside != inside;
        if (index <= previousHighest && !areaChanged)
            return; // Avoid repeated work and Console spam from OnTriggerStay.

        if (areaChanged)
        {
            IsInside = inside;
            Debug.Log("[Checkpoint] Area: " + (inside ? "INSIDE" : "OUTSIDE"), this);
        }

        if (index > previousHighest)
        {
            if (inside)
            {
                highestInside = index;
                insideRecordedAngles[index] = treeRotator != null ? treeRotator.CurrentAngle : 0f;
            }
            else
            {
                highestOutside = index;
                outsideRecordedAngles[index] = treeRotator != null ? treeRotator.CurrentAngle : 0f;
            }

            Debug.Log("[Checkpoint] ACTIVATED " + (inside ? "INSIDE" : "OUTSIDE") +
                      " #" + index + ": " + point.name, this);
        }

        ApplyRespawnForCurrentArea();
    }

    /// <summary>
    /// Re-select the best previously activated point in the current area.
    /// Called before PlayerHealth actually teleports the player on death.
    /// </summary>
    private void ApplyRespawnForCurrentArea()
    {
        if (playerHealth == null)
            return;

        Transform[] points = IsInside ? insideRespawnPoints : outsideRespawnPoints;
        int highest = IsInside ? highestInside : highestOutside;
        float[] recordedAngles = IsInside ? insideRecordedAngles : outsideRecordedAngles;

        Transform selected = null;
        float selectedAngle = IsInside ? insideFallbackTreeAngle : outsideFallbackTreeAngle;

        if (points != null)
        {
            for (int i = Mathf.Min(highest, points.Length - 1); i >= 0; i--)
            {
                if (points[i] == null || !points[i].gameObject.activeInHierarchy)
                    continue;

                selected = points[i];
                if (recordedAngles != null && i < recordedAngles.Length)
                    selectedAngle = recordedAngles[i];
                break;
            }
        }

        if (selected == null)
        {
            Transform fallback = IsInside ? insideFallback : outsideFallback;
            if (fallback != null && fallback.gameObject.activeInHierarchy)
                selected = fallback;
        }

        // null makes PlayerHealth use its original scene starting point.
        playerHealth.respawnPoint = selected;

        if (treeRotator != null && restoreCheckpointTreeAngle)
            treeRotator.checkpointAngle = selectedAngle;

        if (selected == null)
            Debug.LogWarning("[Checkpoint] No active respawn point for " +
                             (IsInside ? "INSIDE" : "OUTSIDE") +
                             ". PlayerHealth will use its original start position.", this);
        else
            Debug.Log("[Checkpoint] Selected " + (IsInside ? "INSIDE" : "OUTSIDE") +
                      ": " + selected.name, this);
    }

    private void EnsureAngleArrays()
    {
        int insideCount = insideRespawnPoints != null ? insideRespawnPoints.Length : 0;
        int outsideCount = outsideRespawnPoints != null ? outsideRespawnPoints.Length : 0;

        if (insideRecordedAngles == null || insideRecordedAngles.Length != insideCount)
            System.Array.Resize(ref insideRecordedAngles, insideCount);
        if (outsideRecordedAngles == null || outsideRecordedAngles.Length != outsideCount)
            System.Array.Resize(ref outsideRecordedAngles, outsideCount);
    }

    private static int FindIndex(Transform[] points, Transform point)
    {
        if (points == null)
            return -1;

        for (int i = 0; i < points.Length; i++)
            if (points[i] == point)
                return i;

        return -1;
    }
}
