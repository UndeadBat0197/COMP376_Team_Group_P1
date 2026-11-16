using UnityEngine;

/// <summary>
/// A cover that slides around the tree while a keyboard 180-degree turn runs.
/// Supports both Automatic180 and Interruptible180; when the player reverses,
/// the opening animation restarts in the new direction.
/// In TargetAngle mode, holding Q or R opens the cover in that direction
/// and releasing the key closes it again.
/// </summary>
[DefaultExecutionOrder(-40)]
public class SlidingHoleCover : MonoBehaviour
{
    [Header("Reference")]
    public TreeRotator treeRotator;

    [Header("Sliding motion")]
    [Tooltip("How far the cover slides around the surface, in degrees.")]
    [Range(1f, 90f)] public float slideAngle = 30f;

    [Tooltip("Fraction of a half-turn at which the cover is fully open.")]
    [Range(0.05f, 0.45f)] public float fullyOpenAt = 0.22f;

    [Tooltip("Fraction of a half-turn at which the cover starts closing.")]
    [Range(0.55f, 0.95f)] public float startClosingAt = 0.80f;

    private Vector3 closedPositionInTree;
    private Quaternion closedRotationInTree;
    private bool hasReference;
    private int observedTurnSequence = -1;
    private float turnStartAngle;
    private float turnEndAngle;
    private float currentSignedOpenness;

    [Tooltip("How quickly the cover opens, closes, or reverses direction (openness units/second).")]
    [Min(0.1f)] public float slideResponse = 7f;

    void Start()
    {
        if (treeRotator == null)
            treeRotator = GetComponentInParent<TreeRotator>();

        if (treeRotator == null)
        {
            Debug.LogError("SlidingHoleCover: Assign the TreeRotator component.", this);
            enabled = false;
            return;
        }

        Transform tree = treeRotator.transform;
        if (!transform.IsChildOf(tree))
            Debug.LogWarning("SlidingHoleCover: For predictable collisions, place this cover under the rotating Tree in the Hierarchy.", this);

        closedPositionInTree = tree.InverseTransformPoint(transform.position);
        closedRotationInTree = Quaternion.Inverse(tree.rotation) * transform.rotation;
        hasReference = true;
    }

    void Update()
    {
        if (!hasReference)
            return;

        float openness = 0f;
        float direction = 1f;

        if (treeRotator.IsKeyboardHalfTurnActive)
        {
            if (treeRotator.KeyboardTurnSequence != observedTurnSequence)
            {
                observedTurnSequence = treeRotator.KeyboardTurnSequence;
                turnStartAngle = treeRotator.KeyboardTurnStartAngle;
                turnEndAngle = treeRotator.KeyboardTurnEndAngle;
            }

            float amount = turnEndAngle - turnStartAngle;
            direction = Mathf.Sign(amount);
            float progress = Mathf.Clamp01(
                (treeRotator.CurrentAngle - turnStartAngle) / amount);

            float openTime = Mathf.Clamp(fullyOpenAt, 0.05f, 0.45f);
            float closeTime = Mathf.Clamp(startClosingAt, openTime + 0.05f, 0.95f);

            if (progress < openTime)
                openness = Mathf.SmoothStep(0f, 1f, progress / openTime);
            else if (progress < closeTime)
                openness = 1f;
            else
                openness = Mathf.SmoothStep(1f, 0f,
                    (progress - closeTime) / (1f - closeTime));
        }
        else if (treeRotator.IsTargetKeyboardActive)
        {
            // TargetAngle uses held-key input instead of a timed 180-degree
            // animation. Open while Q/R is held, and close on release.
            // Keeping this separate also handles quick Q <-> R reversals.
            observedTurnSequence = -1;
            direction = treeRotator.TargetKeyboardDirection;
            openness = 1f;
        }
        else
        {
            observedTurnSequence = -1;
        }

        // Avoid visually snapping from one side of the hole to the other
        // when the player interrupts a 180-degree turn.
        currentSignedOpenness = Mathf.MoveTowards(
            currentSignedOpenness, direction * openness,
            slideResponse * Time.deltaTime);

        Quaternion slide = Quaternion.AngleAxis(
            slideAngle * currentSignedOpenness, Vector3.forward);

        Transform tree = treeRotator.transform;
        Vector3 targetPosition = tree.TransformPoint(slide * closedPositionInTree);
        Quaternion targetRotation = tree.rotation * slide * closedRotationInTree;

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();
    }
}