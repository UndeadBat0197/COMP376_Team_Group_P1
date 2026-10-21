
using UnityEngine;

public class ThirdPersonMovement : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Transform cam;

    [Header("Movement")]
    public float speed = 6f;
    public float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    [Header("Jump and Gravity")]
    public float jumpHeight = 4f;
    public float gravity = (-9.81f)*2f;

    // Number of extra jumps allowed in the air
    public int additionalJumps = 0;

    // Number of jumps already performed
    private int jumpsUsed = 0;

    float verticalVelocity;

    [Header("Falling off steep surfaces")]
    [Tooltip("If on, surfaces tilted more than Max Stand Angle can't be stood on — " +
             "normal gravity slides you down and off them.")]
    public bool slideOffSteepSurfaces = true;

    [Tooltip("Steepest surface (in degrees) you can stand on. Flatter = normal grip. " +
             "Steeper = you slide off. 0 = flat, 90 = wall.")]
    [Range(1f, 89f)] public float maxStandAngle = 45f;

    // Sideways speed picked up while sliding, so you fly off the edge naturally
    Vector3 slideVelocity;

    Vector3 groundNormal = Vector3.up;
    Vector3 lastFrameGroundNormal = Vector3.up;
    bool foundGround;

    void Start()
    {
        // Make "can't walk up it" and "slide off it" use the same angle.
        if (slideOffSteepSurfaces)
            controller.slopeLimit = maxStandAngle;
    }

    void Update()
    {
        // Touching something under our feet?
        bool touchingGround = controller.isGrounded;

        // Too steep to stand on?
        float slope = Vector3.Angle(lastFrameGroundNormal, Vector3.up);
        bool onSteepSurface = slideOffSteepSurfaces && touchingGround && slope > maxStandAngle;

        // Only count as "grounded" if it's a surface we can actually stand on
        bool grounded = touchingGround && !onSteepSurface;

        // Keep the player grounded
        if (grounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        // Get movement input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(
            horizontal, 0f, vertical
        ).normalized;

        // Rotate and move relative to the camera
        if (direction.magnitude >= 0.1f)
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

        // Calculate horizontal movement
        Vector3 moveDir =
            Quaternion.Euler(0f, cam.eulerAngles.y, 0f)
            * direction;

        // Reset jumps when touching the ground
        if (grounded)
        {
            jumpsUsed = 0;
        }

        // Jump if we have jumps remaining
        if (Input.GetButtonDown("Jump") &&
            jumpsUsed <= additionalJumps)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            jumpsUsed++;
        }

        // Apply gravity (same pull everywhere)
        verticalVelocity += gravity * Time.deltaTime;

        if (onSteepSurface)
        {
            // Gravity pulls straight down, but the surface is in the way,
            // so the part of the motion going INTO the surface is removed
            // and what's left slides you down along it.
            Vector3 v = slideVelocity + Vector3.up * verticalVelocity;
            float into = Vector3.Dot(v, lastFrameGroundNormal);
            if (into < 0f)
                v -= lastFrameGroundNormal * into;

            verticalVelocity = v.y;
            slideVelocity = new Vector3(v.x, 0f, v.z);
        }
        else if (grounded)
        {
            // Normal ground: full grip, no sliding.
            slideVelocity = Vector3.zero;
        }
        // In the air: keep whatever sideways speed we had, so you
        // carry on off the edge instead of stopping dead.

        // Combine horizontal, sliding and vertical movement
        Vector3 velocity =
            moveDir * speed
            + slideVelocity
            + Vector3.up * verticalVelocity;

        // Move the CharacterController
        // (groundNormal is refreshed by OnControllerColliderHit during Move)
        groundNormal = Vector3.up;
        foundGround = false;
        controller.Move(velocity * Time.deltaTime);
        lastFrameGroundNormal = groundNormal;
    }

    // Called by Unity for every surface the CharacterController touches during Move().
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Only count hits under the player's feet (not walls beside them)
        float feetY = transform.position.y + controller.center.y
                      - controller.height * 0.5f + controller.radius;

        if (hit.point.y <= feetY && hit.normal.y > 0.05f)
        {
            // If touching several surfaces, use the flattest one
            if (!foundGround || hit.normal.y > groundNormal.y)
                groundNormal = hit.normal;
            foundGround = true;
        }
    }
}
