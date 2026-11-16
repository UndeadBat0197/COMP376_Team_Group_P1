using UnityEngine;

/// <summary>
/// A cover that slides around a cylindrical trunk while TreeRotator performs
/// a 180-degree turn. It is closed before/after each turn and open midway.
/// Attach to the cube covering the hole, under the rotating Tree hierarchy.
/// The tree's longitudinal axis must be its local Z axis.
/// </summary>
[DefaultExecutionOrder(-40)] // After TreeRotator (-50), before player movement (0)
public class SlidingHoleCover : MonoBehaviour
{
    [Header("Reference")]
    public TreeRotator treeRotator;

    [Header("Sliding motion")]
    [Tooltip("How far the cover moves around the trunk surface, in degrees.")]
    [Range(1f, 90f)] public float slideAngle = 30f;

    [Tooltip("Fraction of the tree turn by which the hole is fully open.")]
    [Range(0.05f, 0.45f)] public float fullyOpenAt = 0.22f;

    [Tooltip("Fraction of the tree turn when the cover starts closing.")]
    [Range(0.55f, 0.95f)] public float startClosingAt = 0.80f;

    private Vector3 closedPositionInTree;
    private Quaternion closedRotationInTree;
    private bool hasReference;

    private bool turning;
    private float turnStartAngle;
    private float turnAmount;

    void Start()
    {
        if (treeRotator == null)
            treeRotator = GetComponentInParent<TreeRotator>();

        if (treeRotator == null)
        {
            Debug.LogError("SlidingHoleCover: Assign the rotating Tree object's TreeRotator.", this);
            enabled = false;
            return;
        }

        // Store the original closed pose in the rotating tree's coordinates.
        // This allows the cover to follow the curved surface as the tree turns.
        Transform tree = treeRotator.transform;
        closedPositionInTree = tree.InverseTransformPoint(transform.position);
        closedRotationInTree = Quaternion.Inverse(tree.rotation) * transform.rotation;
        hasReference = true;
    }

    void Update()
    {
        if (!hasReference)
            return;

        // The existing TreeRotator knows whether the tree is performing a turn.
        // Restrict this cover to the requested 180-degree keyboard rotations;
        // ignore the existing 90-degree floor-button turns.
        if (!turning && treeRotator.IsTurning)
        {
            float pendingTurn = treeRotator.TargetAngle - treeRotator.CurrentAngle;
            if (Mathf.Abs(pendingTurn) >= 150f)
            {
                turning = true;
                turnStartAngle = treeRotator.CurrentAngle;
                turnAmount = pendingTurn;
            }
        }

        float openness = 0f;
        float direction = 1f;

        if (turning)
        {
            direction = Mathf.Sign(turnAmount);
            float progress = Mathf.Clamp01(
                (treeRotator.CurrentAngle - turnStartAngle) / turnAmount);

            float openTime = Mathf.Clamp(fullyOpenAt, 0.05f, 0.45f);
            float closeTime = Mathf.Clamp(startClosingAt, openTime + 0.05f, 0.95f);

            if (progress < openTime)
                openness = Mathf.SmoothStep(0f, 1f, progress / openTime);
            else if (progress < closeTime)
                openness = 1f;
            else
                openness = Mathf.SmoothStep(1f, 0f,
                    (progress - closeTime) / (1f - closeTime));

            if (!treeRotator.IsTurning)
            {
                turning = false;
                openness = 0f;
            }
        }

        // Rotate the cover's position around the tree's LOCAL Z axis.
        // This is motion relative to the hole, not just rotation inherited
        // from the parent. Q/R move the cover in opposite directions.
        Quaternion slide = Quaternion.AngleAxis(
            direction * slideAngle * openness, Vector3.forward);

        Transform treeTransform = treeRotator.transform;
        Vector3 targetPosition = treeTransform.TransformPoint(
            slide * closedPositionInTree);
        Quaternion targetRotation = treeTransform.rotation *
            slide * closedRotationInTree;

        transform.SetPositionAndRotation(targetPosition, targetRotation);

        // Make moving colliders available to CharacterController queries
        // in the same frame (also used by the existing TreeRotator).
        Physics.SyncTransforms();
    }
}
