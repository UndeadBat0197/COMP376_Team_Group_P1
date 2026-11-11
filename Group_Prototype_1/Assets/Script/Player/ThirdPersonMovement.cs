using UnityEngine;

/// <summary>
/// Third person movement for the fox.
///
/// What changed compared to the first version:
///  - Horizontal speed now accelerates / decelerates instead of snapping
///    between 0 and full speed.
///  - The ground is found with a sphere cast under the capsule, so the
///    character follows curved floors (the inside of the cylinder) and
///    small steps instead of leaving the ground and dropping back on it.
///  - Short "coyote time" and jump buffering so jumps feel responsive.
///  - The visual model is moved down to the bottom of the capsule at
///    start, so the fox stands on the floor instead of hovering.
/// </summary>
[DefaultExecutionOrder(0)]
public class ThirdPersonMovement : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Transform cam;
    public Animator foxAnimator;

    [Header("Movement")]
    public float speed = 6f;
    public float runSpeed = 9f;
    public float turnSmoothTime = 0.3f;
    float turnSmoothVelocity;

    [Header("Smoothing")]
    [Tooltip("How fast the character reaches its target speed (units/s²).")]
    public float acceleration = 45f;

    [Tooltip("How fast the character stops when there is no input (units/s²).")]
    public float deceleration = 55f;

    [Tooltip("How much steering you keep while in the air (0 = none, 1 = full).")]
    [Range(0f, 1f)]
    public float airControl = 0.45f;

    [Header("Ground sticking")]
    [Tooltip("Which layers count as ground.")]
    public LayerMask groundMask = ~0;

    [Tooltip("If the floor drops away by less than this while walking, " +
             "the character is pulled back onto it instead of falling.")]
    public float groundSnapDistance = 0.6f;

    [Tooltip("You can still jump this long after walking off an edge.")]
    public float coyoteTime = 0.12f;

    [Tooltip("A jump pressed this long before landing still counts.")]
    public float jumpBufferTime = 0.12f;

    [Header("Pushable Objects")]
    [Tooltip("How strongly the character pushes physics objects.")]
    public float pushAcceleration = 12f;

    [Header("Jump and Gravity")]
    public float jumpHeight = 4f;
    public float gravity = -19.62f;
    public int additionalJumps = 0;

    [Header("Falling off steep surfaces")]
    public bool slideOffSteepSurfaces = true;

    [Range(1f, 89f)]
    public float maxStandAngle = 45f;

    [Header("Model")]
    [Tooltip("The visual model (the fox). Found automatically if empty.")]
    public Transform model;

    [Tooltip("Move the model down to the bottom of the capsule at start, " +
             "so its feet touch the floor.")]
    public bool alignModelToFeet = true;

    [Tooltip("Extra height added to the model after aligning (world units). " +
             "Negative sinks the feet into the floor a little.")]
    public float modelGroundOffset = 0f;

    // ---- runtime state ----
    private Vector3 planarVelocity;      // smoothed horizontal velocity
    private Vector3 slideVelocity;       // horizontal part of slope sliding
    private float verticalVelocity;

    private Vector3 groundNormal = Vector3.up;
    private bool grounded;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private bool jumpedSinceGrounded;
    private int airJumpsUsed;

    // Animation parameters
    private static readonly int SpeedParam =
        Animator.StringToHash("speed");

    private static readonly int GroundedParam =
        Animator.StringToHash("isGrounded");

    private static readonly int JumpParam =
        Animator.StringToHash("jump");

    private bool hasSpeedParam;
    private bool hasGroundedParam;
    private bool hasJumpParam;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        if (foxAnimator == null)
            foxAnimator = GetComponentInChildren<Animator>();

        if (controller != null && slideOffSteepSurfaces)
            controller.slopeLimit = maxStandAngle;

        if (foxAnimator != null)
        {
            // Movement is controlled by CharacterController,
            // not by the animation.
            foxAnimator.applyRootMotion = false;

            // Only set parameters the Animator really has,
            // so a different controller does not spam warnings.
            foreach (AnimatorControllerParameter p in foxAnimator.parameters)
            {
                if (p.nameHash == SpeedParam) hasSpeedParam = true;
                if (p.nameHash == GroundedParam) hasGroundedParam = true;
                if (p.nameHash == JumpParam) hasJumpParam = true;
            }
        }

        if (model == null && foxAnimator != null)
            model = foxAnimator.transform;

        AlignModelToFeet();
    }

    /// <summary>
    /// Puts the model's origin (the fox's feet) at the bottom of the
    /// CharacterController capsule. Without this the fox sits at the
    /// capsule centre and floats half a capsule above the floor.
    /// </summary>
    public void AlignModelToFeet()
    {
        if (!alignModelToFeet || model == null || controller == null)
            return;

        if (model == transform || model.parent != transform)
            return;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        if (scaleY < 0.0001f)
            scaleY = 1f;

        GetWorldCapsule(out _, out float worldHeight);

        // Bottom of the capsule in this object's local space. The
        // controller also keeps "skin width" of air under it.
        float localBottom =
            controller.center.y -
            (worldHeight * 0.5f + controller.skinWidth) / scaleY;

        Vector3 local = model.localPosition;
        local.y = localBottom + modelGroundOffset / scaleY;
        model.localPosition = local;
    }

    void Update()
    {
        if (controller == null || cam == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        // ------------------------------------------------------
        // 1. Where is the ground?
        // ------------------------------------------------------
        bool probeHit = ProbeGround(
            groundSnapDistance, out RaycastHit groundHit);

        float contactRange = controller.skinWidth + 0.06f;

        bool touchingGround =
            controller.isGrounded ||
            (probeHit && groundHit.distance <= contactRange);

        if (probeHit)
            groundNormal = groundHit.normal;
        else if (!touchingGround)
            groundNormal = Vector3.up;

        float slope = Vector3.Angle(groundNormal, Vector3.up);

        bool onSteepSurface =
            slideOffSteepSurfaces &&
            touchingGround &&
            slope > maxStandAngle;

        grounded =
            touchingGround &&
            !onSteepSurface &&
            verticalVelocity <= 0.01f;

        if (grounded)
        {
            lastGroundedTime = Time.time;
            jumpedSinceGrounded = false;
            airJumpsUsed = 0;

            // Small constant push keeps the controller on the floor.
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }

        // ------------------------------------------------------
        // 2. Input -> smoothed horizontal velocity
        // ------------------------------------------------------
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(horizontal, 0f, vertical);
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        bool isMoving = direction.sqrMagnitude > 0.01f;
        bool isRunning = isMoving && Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? runSpeed : speed;

        Vector3 moveDir =
            Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * direction;

        Vector3 targetVelocity = moveDir * currentSpeed;

        float rate = isMoving ? acceleration : deceleration;
        if (!grounded)
            rate *= airControl;

        planarVelocity = Vector3.MoveTowards(
            planarVelocity, targetVelocity, rate * dt);

        // Rotate the character towards where it is going.
        if (isMoving)
        {
            float targetAngle =
                Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothVelocity,
                turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        // ------------------------------------------------------
        // 3. Jump (buffered, with coyote time)
        // ------------------------------------------------------
        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        bool jumpedThisFrame = false;

        if (Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            bool canGroundJump =
                !jumpedSinceGrounded &&
                !onSteepSurface &&
                Time.time - lastGroundedTime <= coyoteTime;

            bool canAirJump =
                !canGroundJump &&
                airJumpsUsed < additionalJumps;

            if (canGroundJump || canAirJump)
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);

                if (canGroundJump)
                    jumpedSinceGrounded = true;
                else
                    airJumpsUsed++;

                lastJumpPressedTime = -10f;
                jumpedThisFrame = true;
                grounded = false;
            }
        }

        // ------------------------------------------------------
        // 4. Gravity and sliding off steep surfaces
        // ------------------------------------------------------
        verticalVelocity += gravity * dt;

        if (onSteepSurface && !jumpedThisFrame)
        {
            Vector3 v =
                slideVelocity +
                Vector3.up * verticalVelocity;

            float into = Vector3.Dot(v, groundNormal);

            if (into < 0f)
                v -= groundNormal * into;

            verticalVelocity = v.y;
            slideVelocity = new Vector3(v.x, 0f, v.z);
        }
        else if (grounded)
        {
            // Ease out of a slide instead of stopping dead.
            slideVelocity = Vector3.MoveTowards(
                slideVelocity, Vector3.zero, deceleration * dt);
        }

        // ------------------------------------------------------
        // 5. Move
        // ------------------------------------------------------
        Vector3 walkVelocity = planarVelocity;

        if (grounded && planarVelocity.sqrMagnitude > 0.0001f)
        {
            // Walk along the surface, so going "downhill" on the
            // curved cylinder floor does not launch us off it.
            Vector3 alongGround =
                Vector3.ProjectOnPlane(planarVelocity, groundNormal);

            if (alongGround.sqrMagnitude > 0.0001f)
            {
                walkVelocity =
                    alongGround.normalized * planarVelocity.magnitude;
            }
        }

        Vector3 velocity =
            walkVelocity +
            slideVelocity +
            Vector3.up * verticalVelocity;

        bool wasGrounded = grounded;

        controller.Move(velocity * dt);

        // Hitting a ceiling stops the jump right away.
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        // ------------------------------------------------------
        // 6. Snap back to the ground if we just walked off it
        // ------------------------------------------------------
        if (wasGrounded && !jumpedThisFrame && !controller.isGrounded)
        {
            if (ProbeGround(groundSnapDistance, out RaycastHit snapHit) &&
                Vector3.Angle(snapHit.normal, Vector3.up) <= maxStandAngle &&
                snapHit.distance > 0.001f)
            {
                controller.Move(Vector3.down * snapHit.distance);
            }
        }

        UpdateAnimator(dt, jumpedThisFrame);
    }

    void UpdateAnimator(float dt, bool jumpedThisFrame)
    {
        if (foxAnimator == null)
            return;

        // 0 = Idle, 0.5 = Walk, 1 = Run. Uses the real (smoothed)
        // speed, so the animation eases in and out with the movement.
        float planarSpeed = planarVelocity.magnitude;
        float animationSpeed;

        if (planarSpeed <= speed || runSpeed <= speed)
        {
            animationSpeed =
                speed > 0f ? 0.5f * planarSpeed / speed : 0f;
        }
        else
        {
            animationSpeed =
                0.5f + 0.5f * (planarSpeed - speed) / (runSpeed - speed);
        }

        if (hasSpeedParam)
        {
            foxAnimator.SetFloat(
                SpeedParam,
                Mathf.Clamp01(animationSpeed),
                0.08f,
                dt);
        }

        // A short grace period stops the animation from flickering
        // to "in air" on tiny bumps.
        if (hasGroundedParam)
        {
            bool animGrounded =
                !jumpedSinceGrounded &&
                Time.time - lastGroundedTime <= 0.15f;

            foxAnimator.SetBool(GroundedParam, animGrounded);
        }

        if (jumpedThisFrame && hasJumpParam)
            foxAnimator.SetTrigger(JumpParam);
    }

    /// <summary>
    /// World-space size of the CharacterController capsule
    /// (the controller scales with the transform).
    /// </summary>
    void GetWorldCapsule(out float worldRadius, out float worldHeight)
    {
        Vector3 s = transform.lossyScale;

        worldRadius =
            controller.radius *
            Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));

        worldHeight = Mathf.Max(
            controller.height * Mathf.Abs(s.y),
            worldRadius * 2f);
    }

    /// <summary>
    /// Sphere cast straight down from the bottom of the capsule.
    /// hit.distance is the gap between the capsule and the ground.
    /// </summary>
    bool ProbeGround(float maxDistance, out RaycastHit hit)
    {
        GetWorldCapsule(out float worldRadius, out float worldHeight);

        Vector3 centre = transform.TransformPoint(controller.center);

        // Slightly smaller sphere, started a little higher, so the
        // cast never begins inside the floor.
        float probeRadius = worldRadius * 0.95f;
        float lift = 0.1f;

        Vector3 origin =
            centre +
            Vector3.down * (worldHeight * 0.5f - worldRadius) +
            Vector3.up * lift;

        float extra = lift + (worldRadius - probeRadius);

        bool found = Physics.SphereCast(
            origin,
            probeRadius,
            Vector3.down,
            out hit,
            maxDistance + extra + controller.skinWidth,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (!found)
            return false;

        // Never treat our own colliders as ground.
        if (hit.collider != null &&
            hit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        hit.distance = Mathf.Max(0f, hit.distance - extra);
        return true;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body != null &&
            !body.isKinematic &&
            hit.moveDirection.y > -0.3f)
        {
            Vector3 pushDirection = new Vector3(
                hit.moveDirection.x,
                0f,
                hit.moveDirection.z);

            if (pushDirection.sqrMagnitude > 0.01f)
            {
                body.AddForce(
                    pushDirection.normalized * pushAcceleration,
                    ForceMode.Acceleration);
            }
        }
    }
}
