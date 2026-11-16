using UnityEngine;

/// <summary>
/// A small 3D stage for one character in the menu. A camera films it
/// into a RenderTexture that the character card shows. The model turns
/// slowly, and runs on the spot while it is the selected one.
/// </summary>
public class CharacterPreview : MonoBehaviour
{
    public Transform turntable;

    [Tooltip("For rigged characters (the fox).")]
    public Animator animator;

    [Tooltip("For models without animation clips.")]
    public ProceduralCharacterAnimator procedural;

    public float idleSpin = 18f;
    public float selectedSpin = 40f;

    private static readonly int SpeedParam = Animator.StringToHash("speed");
    private static readonly int GroundedParam = Animator.StringToHash("isGrounded");

    private bool hasSpeed;
    private bool hasGrounded;
    private bool selected;
    private float spin;

    void Awake()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == SpeedParam) hasSpeed = true;
            if (p.nameHash == GroundedParam) hasGrounded = true;
        }
    }

    void OnEnable()
    {
        spin = idleSpin;

        // Parameters reset when the object is switched off and on again.
        if (animator != null && hasGrounded)
            animator.SetBool(GroundedParam, true);
    }

    public void SetSelected(bool value)
    {
        selected = value;
    }

    public void Celebrate()
    {
        if (procedural != null)
            procedural.PlayHop();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        spin = Mathf.Lerp(
            spin, selected ? selectedSpin : idleSpin, dt * 4f);

        if (turntable != null)
            turntable.Rotate(0f, spin * dt, 0f, Space.Self);

        if (animator != null && hasSpeed)
        {
            animator.SetFloat(
                SpeedParam, selected ? 1f : 0f, 0.2f, dt);
        }

        if (procedural != null)
        {
            procedural.manualSpeed01 = Mathf.MoveTowards(
                procedural.manualSpeed01, selected ? 0.8f : 0f, dt * 3f);
        }
    }
}
