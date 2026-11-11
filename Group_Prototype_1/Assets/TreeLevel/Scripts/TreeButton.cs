using UnityEngine;

/// <summary>
/// A big floor button. Stepping on it turns the tree a quarter turn,
/// bringing the lane on the right (positive turnDegrees) or on the left
/// (negative) down to the floor.
///
/// If returnAfterSeconds is above zero the tree turns back by itself
/// after that long. That is used two ways in the level:
///  - a short time for wrong buttons, which swing thorns under the player;
///  - a longer time for "timed doors", where the player has to run to the
///    doorway before the tree turns back.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TreeButton : MonoBehaviour
{
    [Tooltip("How far this button turns the tree. Positive brings the " +
             "lane on the player's right down to the floor.")]
    public float turnDegrees = 90f;

    [Tooltip("0 = the tree stays turned. Above 0 = it turns back by " +
             "itself after this many seconds.")]
    public float returnAfterSeconds = 0f;

    [Tooltip("Found automatically in the parents if left empty.")]
    public TreeRotator rotator;

    [Header("Visuals (optional)")]
    public Transform cap;
    public Renderer capRenderer;
    public Material idleMaterial;
    public Material pressedMaterial;
    public float pressDepth = 0.22f;

    private Vector3 capRestPosition;
    private bool showingPressed;

    private bool hasBeenPressed;
    private float pressedTargetAngle;

    void Awake()
    {
        if (rotator == null)
            rotator = GetComponentInParent<TreeRotator>();

        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        if (cap != null)
            capRestPosition = cap.localPosition;
    }

    // Down while the tree is at (or heading for) the angle this
    // button sent it to.
    bool IsPressed
    {
        get
        {
            return hasBeenPressed &&
                   rotator != null &&
                   Mathf.Abs(rotator.TargetAngle - pressedTargetAngle) < 0.5f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryPress(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryPress(other);
    }

    void TryPress(Collider other)
    {
        // Busy: buttons on other lanes sweep past the player while the
        // tree turns, and must not go off.
        if (rotator == null || rotator.Busy || IsPressed)
            return;

        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player == null || !player.CompareTag("Player"))
            return;

        // Bringing the lane on the right down means turning the trunk
        // the other way.
        float target = rotator.TargetAngle - turnDegrees;

        if (returnAfterSeconds > 0f)
            rotator.TurnAndReturn(target, returnAfterSeconds);
        else
            rotator.RotateTo(target);

        pressedTargetAngle = target;
        hasBeenPressed = true;
    }

    void Update()
    {
        bool pressed = IsPressed;

        if (cap != null)
        {
            Vector3 goal = capRestPosition;
            if (pressed)
                goal += Vector3.down * pressDepth;

            cap.localPosition = Vector3.MoveTowards(
                cap.localPosition, goal, Time.deltaTime * 1.5f);
        }

        if (pressed != showingPressed && capRenderer != null)
        {
            Material next = pressed ? pressedMaterial : idleMaterial;
            if (next != null)
                capRenderer.sharedMaterial = next;
        }

        showingPressed = pressed;
    }
}
