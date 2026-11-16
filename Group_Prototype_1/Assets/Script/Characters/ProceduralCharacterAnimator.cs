using UnityEngine;

/// <summary>
/// Makes a model with no animation clips look alive: it hops and leans
/// while running, stretches on the way up, squashes when it lands and
/// breathes when idle. All of it is driven by the CharacterController
/// on the player, so it always matches the real movement.
///
/// Put it on the character's wrapper object and point "target" at the
/// child that should move (its origin should be at the feet).
/// </summary>
public class ProceduralCharacterAnimator : MonoBehaviour
{
    [Tooltip("The child that gets moved / rotated / scaled. Pivot at the feet.")]
    public Transform target;

    [Tooltip("Found on a parent automatically. With no controller " +
             "(menu preview) the manual speed below is used.")]
    public CharacterController controller;

    [Tooltip("Planar speed that counts as a full run.")]
    public float runSpeedReference = 9f;

    [Header("Running")]
    [Tooltip("Height of each running hop, in world units.")]
    public float hopHeight = 0.16f;
    public float walkStepsPerSecond = 2.4f;
    public float runStepsPerSecond = 3.8f;
    public float leanAngle = 13f;
    public float waddleAngle = 6f;
    [Tooltip("How much it squashes and stretches with each step.")]
    public float strideSquash = 0.10f;

    [Header("Jumping")]
    public float airStretch = 0.16f;
    public float landSquash = 0.28f;
    public float airTilt = 10f;

    [Header("Idle")]
    public float breatheAmount = 0.02f;
    public float breatheSpeed = 2.2f;

    [Header("Menu preview")]
    [Range(0f, 1f)]
    public float manualSpeed01 = 0f;
    public float previewHopHeight = 0.35f;
    public float previewHopTime = 0.45f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private bool hasBase;

    private float phase;
    private float moveBlend;
    private float airBlend;
    private float springPos;
    private float springVel;
    private float lastGroundedTime = -10f;
    private float lastAirVertical;
    private bool wasGrounded = true;
    private float previewHop = -1f;

    void Awake()
    {
        if (target == null && transform.childCount > 0)
            target = transform.GetChild(0);

        if (controller == null)
            controller = GetComponentInParent<CharacterController>();

        CaptureBase();
    }

    void CaptureBase()
    {
        if (target == null || hasBase)
            return;

        basePosition = target.localPosition;
        baseRotation = target.localRotation;
        baseScale = target.localScale;
        hasBase = true;
    }

    void OnDisable()
    {
        if (target != null && hasBase)
        {
            target.localPosition = basePosition;
            target.localRotation = baseRotation;
            target.localScale = baseScale;
        }
    }

    /// <summary>A single hop on the spot (used by the menu when picked).</summary>
    public void PlayHop()
    {
        previewHop = 0f;
    }

    void Update()
    {
        if (target == null)
            return;

        CaptureBase();

        bool usingController = controller != null && controller.enabled;

        // Unscaled time in the menu so it still moves if time is frozen.
        float dt = usingController ? Time.deltaTime : Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;

        dt = Mathf.Min(dt, 1f / 30f);

        // ---- what is the character doing? ----
        float speed01;
        float vertical;
        bool grounded;
        float extraHeight = 0f;

        if (usingController)
        {
            Vector3 v = controller.velocity;
            float planar = new Vector2(v.x, v.z).magnitude;

            speed01 = runSpeedReference > 0f
                ? Mathf.Clamp01(planar / runSpeedReference)
                : 0f;

            vertical = v.y;

            if (controller.isGrounded)
                lastGroundedTime = Time.time;

            // Short grace so tiny bumps do not count as being in the air.
            grounded =
                Time.time - lastGroundedTime < 0.1f && vertical < 1.5f;
        }
        else
        {
            speed01 = manualSpeed01;
            vertical = 0f;
            grounded = true;
        }

        if (previewHop >= 0f)
        {
            previewHop += dt / Mathf.Max(0.05f, previewHopTime);

            if (previewHop >= 1f)
            {
                previewHop = -1f;
            }
            else
            {
                grounded = false;
                extraHeight =
                    Mathf.Sin(previewHop * Mathf.PI) * previewHopHeight;
                vertical = Mathf.Cos(previewHop * Mathf.PI) * 7f;
            }
        }

        // ---- take-off and landing kicks ----
        if (!grounded)
            lastAirVertical = vertical;

        if (grounded && !wasGrounded)
        {
            float impact =
                Mathf.Clamp01(Mathf.Abs(lastAirVertical) / 12f + 0.3f);

            springVel -= landSquash * 14f * impact;
        }
        else if (!grounded && wasGrounded && vertical > 1f)
        {
            springVel += 2.5f;
        }

        wasGrounded = grounded;

        // Damped spring that settles the squash back to normal.
        const float stiffness = 240f;
        const float damping = 17f;

        springVel += (-stiffness * springPos - damping * springVel) * dt;
        springPos += springVel * dt;

        // ---- blends ----
        moveBlend = Mathf.MoveTowards(
            moveBlend, grounded ? speed01 : 0f, dt * 6f);

        airBlend = Mathf.MoveTowards(
            airBlend, grounded ? 0f : 1f, dt * 10f);

        if (moveBlend > 0.02f)
        {
            float steps = Mathf.Lerp(
                walkStepsPerSecond, runStepsPerSecond, speed01);

            phase += dt * Mathf.PI * steps;
        }

        float stride = Mathf.Abs(Mathf.Sin(phase));

        // ---- pose ----
        float hop = stride * hopHeight * moveBlend + extraHeight;

        float scaleY = 1f;

        scaleY +=
            Mathf.Sin(Time.unscaledTime * breatheSpeed) *
            breatheAmount * (1f - moveBlend) * (1f - airBlend);

        scaleY += (stride - 0.5f) * strideSquash * moveBlend;

        scaleY +=
            Mathf.Clamp(vertical * 0.025f, -0.10f, airStretch) * airBlend;

        scaleY += springPos;
        scaleY = Mathf.Clamp(scaleY, 0.6f, 1.4f);

        // Keep the volume: thinner when taller, wider when squashed.
        float scaleXZ = 1f / Mathf.Sqrt(scaleY);

        float pitch =
            leanAngle * moveBlend +
            airTilt * Mathf.Clamp(-vertical / 10f, -1f, 1f) * airBlend;

        float roll = Mathf.Sin(phase) * waddleAngle * moveBlend;

        // hopHeight is in world units; convert to the target's parent space.
        float parentScale = target.parent != null
            ? Mathf.Abs(target.parent.lossyScale.y)
            : 1f;

        if (parentScale < 0.0001f)
            parentScale = 1f;

        target.localPosition =
            basePosition + Vector3.up * (hop / parentScale);

        target.localRotation =
            baseRotation * Quaternion.Euler(pitch, 0f, roll);

        target.localScale = new Vector3(
            baseScale.x * scaleXZ,
            baseScale.y * scaleY,
            baseScale.z * scaleXZ);
    }
}
