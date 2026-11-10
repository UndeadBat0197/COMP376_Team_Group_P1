
using UnityEngine;

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

    [Header("Pushable Objects")]
    [Tooltip("How strongly the character pushes physics objects.")]
    public float pushAcceleration = 12f;

    [Header("Jump and Gravity")]
    public float jumpHeight = 4f;
    public float gravity = -19.62f;
    public int additionalJumps = 0;

    private int jumpsUsed = 0;
    private float verticalVelocity;

    [Header("Falling off steep surfaces")]
    public bool slideOffSteepSurfaces = true;

    [Range(1f, 89f)]
    public float maxStandAngle = 45f;

    private Vector3 slideVelocity;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 lastFrameGroundNormal = Vector3.up;
    private bool foundGround;

    // Animation parameters
    private static readonly int SpeedParam =
        Animator.StringToHash("speed");

    private static readonly int GroundedParam =
        Animator.StringToHash("isGrounded");

    private static readonly int JumpParam =
        Animator.StringToHash("jump");

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

        // Movement is controlled by CharacterController,
        // not by the animation.
        if (foxAnimator != null)
            foxAnimator.applyRootMotion = false;
    }

    void Update()
    {
        if (controller == null || cam == null)
            return;

        // Check if grounded
        bool touchingGround = controller.isGrounded;

        float slope = Vector3.Angle(
            lastFrameGroundNormal, Vector3.up
        );

        bool onSteepSurface =
            slideOffSteepSurfaces &&
            touchingGround &&
            slope > maxStandAngle;

        bool grounded =
            touchingGround && !onSteepSurface;

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        // Movement input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(
            horizontal, 0f, vertical
        ).normalized;

        bool isMoving = direction.sqrMagnitude > 0.01f;
        bool isRunning = isMoving &&
                         Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = isRunning ? runSpeed : speed;

        // Rotate character relative to camera
        if (isMoving)
        {
            float targetAngle =
                Mathf.Atan2(direction.x, direction.z)
                * Mathf.Rad2Deg + cam.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothVelocity,
                turnSmoothTime
            );

            transform.rotation =
                Quaternion.Euler(0f, angle, 0f);
        }

        Vector3 moveDir =
            Quaternion.Euler(0f, cam.eulerAngles.y, 0f)
            * direction;

        // Reset jumps
        if (grounded)
            jumpsUsed = 0;

        // Jump
        bool jumpedThisFrame = false;

        if (Input.GetButtonDown("Jump") &&
            jumpsUsed <= additionalJumps)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            jumpsUsed++;
            jumpedThisFrame = true;
        }

        // Gravity
        verticalVelocity += gravity * Time.deltaTime;

        // Slope sliding
        if (onSteepSurface)
        {
            Vector3 v =
                slideVelocity +
                Vector3.up * verticalVelocity;

            float into =
                Vector3.Dot(v, lastFrameGroundNormal);

            if (into < 0f)
                v -= lastFrameGroundNormal * into;

            verticalVelocity = v.y;

            slideVelocity = new Vector3(
                v.x, 0f, v.z
            );
        }
        else if (grounded)
        {
            slideVelocity = Vector3.zero;
        }

        Vector3 velocity =
            moveDir * currentSpeed +
            slideVelocity +
            Vector3.up * verticalVelocity;

        groundNormal = Vector3.up;
        foundGround = false;

        controller.Move(velocity * Time.deltaTime);

        lastFrameGroundNormal = groundNormal;

        // FOX ANIMATIONS
        if (foxAnimator != null)
        {
            // 0 = Idle
            // 0.5 = Walk
            // 1 = Run
            float animationSpeed = 0f;

            if (isMoving)
                animationSpeed = isRunning ? 1f : 0.5f;

            foxAnimator.SetFloat(
                SpeedParam,
                animationSpeed,
                0.08f,
                Time.deltaTime
            );

            // Optional jump parameters.
            // Use these if they exist in your Animator.
            foxAnimator.SetBool(
                GroundedParam,
                controller.isGrounded
            );

            if (jumpedThisFrame)
                foxAnimator.SetTrigger(JumpParam);
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body =
            hit.collider.attachedRigidbody;

        if (body != null &&
            !body.isKinematic &&
            hit.moveDirection.y > -0.3f)
        {
            Vector3 pushDirection = new Vector3(
                hit.moveDirection.x,
                0f,
                hit.moveDirection.z
            );

            if (pushDirection.sqrMagnitude > 0.01f)
            {
                body.AddForce(
                    pushDirection.normalized *
                    pushAcceleration,
                    ForceMode.Acceleration
                );
            }
        }

        float feetY =
            transform.position.y +
            controller.center.y -
            controller.height * 0.5f +
            controller.radius;

        if (hit.point.y <= feetY &&
            hit.normal.y > 0.05f)
        {
            if (!foundGround ||
                hit.normal.y > groundNormal.y)
            {
                groundNormal = hit.normal;
            }

            foundGround = true;
        }
    }
}
