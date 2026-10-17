
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

    void Update()
    {
        // Check if the player is touching the ground
        bool grounded = controller.isGrounded;

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

        // Apply gravity
        verticalVelocity += gravity * Time.deltaTime;

        // Combine horizontal and vertical movement
        Vector3 velocity =
            moveDir * speed + Vector3.up * verticalVelocity;

        // Move the CharacterController
        controller.Move(velocity * Time.deltaTime);
    }
}
