
using UnityEngine;

[DefaultExecutionOrder(0)]
public class ThirdPersonMovement : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Transform cam;
    public Animator foxAnimator;

    // =====================================
    // FOX SOUND EFFECTS
    // =====================================

    [Header("Fox Sound Effects")]
    public AudioClip footstepSound;
    public AudioClip jumpSound;

    [Header("Audio Sources")]
    public AudioSource footstepAudio;
    public AudioSource jumpAudio;

    [Header("Sound Settings")]
    [Range(0f, 1f)]
    public float footstepVolume = 0.5f;

    [Range(0f, 1f)]
    public float jumpVolume = 0.7f;

    public float walkFootstepPitch = 1f;
    public float runFootstepPitch = 1.25f;

    // =====================================
    // MOVEMENT
    // =====================================

    [Header("Movement")]
    public float speed = 6f;
    public float runSpeed = 9f;
    public float turnSmoothTime = 0.3f;

    private float turnSmoothVelocity;

    [Header("Smoothing")]
    public float acceleration = 45f;
    public float deceleration = 55f;

    [Range(0f, 1f)]
    public float airControl = 0.45f;

    [Header("Ground sticking")]
    public LayerMask groundMask = ~0;
    public float groundSnapDistance = 0.6f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Pushable Objects")]
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
    public Transform model;
    public bool alignModelToFeet = true;
    public float modelGroundOffset = 0f;

    // =====================================
    // RUNTIME STATE
    // =====================================

    private Vector3 planarVelocity;
    private Vector3 slideVelocity;
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

    // =====================================
    // START
    // =====================================

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
            foxAnimator.applyRootMotion = false;

            foreach (AnimatorControllerParameter p
                     in foxAnimator.parameters)
            {
                if (p.nameHash == SpeedParam)
                    hasSpeedParam = true;

                if (p.nameHash == GroundedParam)
                    hasGroundedParam = true;

                if (p.nameHash == JumpParam)
                    hasJumpParam = true;
            }
        }

        if (model == null && foxAnimator != null)
            model = foxAnimator.transform;

        AlignModelToFeet();

        SetupAudio();
    }

    // =====================================
    // AUDIO SETUP
    // =====================================

    void SetupAudio()
    {
        // Dedicated Audio Source for footsteps

        if (footstepAudio == null)
        {
            footstepAudio =
                gameObject.AddComponent<AudioSource>();
        }

        // Dedicated Audio Source for jumping

        if (jumpAudio == null || jumpAudio == footstepAudio)
        {
            jumpAudio =
                gameObject.AddComponent<AudioSource>();
        }

        // Footstep audio settings

        footstepAudio.enabled = true;
        footstepAudio.playOnAwake = false;
        footstepAudio.loop = true;
        footstepAudio.spatialBlend = 0f;
        footstepAudio.volume = footstepVolume;
        footstepAudio.pitch = walkFootstepPitch;
        footstepAudio.mute = false;

        footstepAudio.Stop();
        footstepAudio.clip = footstepSound;

        // Jump audio settings

        jumpAudio.enabled = true;
        jumpAudio.playOnAwake = false;
        jumpAudio.loop = false;
        jumpAudio.spatialBlend = 0f;
        jumpAudio.volume = 1f;
        jumpAudio.mute = false;

        if (footstepSound == null)
        {
            Debug.LogWarning(
                "Fox: Footstep Sound is missing!");
        }

        Debug.Log("Fox audio initialized.");
    }

    // =====================================
    // MODEL ALIGNMENT
    // =====================================

    public void AlignModelToFeet()
    {
        if (!alignModelToFeet ||
            model == null ||
            controller == null)
            return;

        if (model == transform ||
            model.parent != transform)
            return;

        float scaleY =
            Mathf.Abs(transform.lossyScale.y);

        if (scaleY < 0.0001f)
            scaleY = 1f;

        GetWorldCapsule(
            out _,
            out float worldHeight);

        float localBottom =
            controller.center.y -
            (worldHeight * 0.5f +
             controller.skinWidth) / scaleY;

        Vector3 local = model.localPosition;

        local.y =
            localBottom + modelGroundOffset / scaleY;

        model.localPosition = local;
    }

    // =====================================
    // UPDATE
    // =====================================

    void Update()
    {
        if (controller == null || cam == null)
        {
            StopFootsteps();
            return;
        }

        float dt = Time.deltaTime;

        if (dt <= 0f)
            return;

        // =====================================
        // 1. GROUND DETECTION
        // =====================================

        bool probeHit = ProbeGround(
            groundSnapDistance,
            out RaycastHit groundHit);

        float contactRange =
            controller.skinWidth + 0.06f;

        bool touchingGround =
            controller.isGrounded ||
            (probeHit &&
             groundHit.distance <= contactRange);

        if (probeHit)
            groundNormal = groundHit.normal;
        else if (!touchingGround)
            groundNormal = Vector3.up;

        float slope =
            Vector3.Angle(groundNormal, Vector3.up);

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

            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }

        // =====================================
        // 2. MOVEMENT INPUT
        // =====================================

        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");

        Vector3 direction =
            new Vector3(horizontal, 0f, vertical);

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        bool isMoving =
            direction.sqrMagnitude > 0.01f;

        bool isRunning =
            isMoving &&
            Input.GetKey(KeyCode.LeftShift);

        float currentSpeed =
            isRunning ? runSpeed : speed;

        Vector3 moveDir =
            Quaternion.Euler(
                0f,
                cam.eulerAngles.y,
                0f) * direction;

        Vector3 targetVelocity =
            moveDir * currentSpeed;

        float rate =
            isMoving ? acceleration : deceleration;

        if (!grounded)
            rate *= airControl;

        planarVelocity =
            Vector3.MoveTowards(
                planarVelocity,
                targetVelocity,
                rate * dt);

        // Rotate towards movement direction

        if (isMoving)
        {
            float targetAngle =
                Mathf.Atan2(
                    moveDir.x,
                    moveDir.z) * Mathf.Rad2Deg;

            float angle =
                Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    targetAngle,
                    ref turnSmoothVelocity,
                    turnSmoothTime);

            transform.rotation =
                Quaternion.Euler(0f, angle, 0f);
        }

        // =====================================
        // 3. JUMP
        // =====================================

        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        bool jumpedThisFrame = false;

        if (Time.time - lastJumpPressedTime
            <= jumpBufferTime)
        {
            bool canGroundJump =
                !jumpedSinceGrounded &&
                !onSteepSurface &&
                Time.time - lastGroundedTime
                    <= coyoteTime;

            bool canAirJump =
                !canGroundJump &&
                airJumpsUsed < additionalJumps;

            if (canGroundJump || canAirJump)
            {
                verticalVelocity =
                    Mathf.Sqrt(
                        jumpHeight * -2f * gravity);

                if (canGroundJump)
                    jumpedSinceGrounded = true;
                else
                    airJumpsUsed++;

                lastJumpPressedTime = -10f;

                jumpedThisFrame = true;
                grounded = false;

                // Stop walking sound before jumping

                StopFootsteps();

                // Play jump sound

                PlayJumpSound();
            }
        }

        // =====================================
        // 4. GRAVITY AND SLOPE SLIDING
        // =====================================

        verticalVelocity += gravity * dt;

        if (onSteepSurface && !jumpedThisFrame)
        {
            Vector3 v =
                slideVelocity +
                Vector3.up * verticalVelocity;

            float into =
                Vector3.Dot(v, groundNormal);

            if (into < 0f)
                v -= groundNormal * into;

            verticalVelocity = v.y;

            slideVelocity =
                new Vector3(v.x, 0f, v.z);
        }
        else if (grounded)
        {
            slideVelocity =
                Vector3.MoveTowards(
                    slideVelocity,
                    Vector3.zero,
                    deceleration * dt);
        }

        // =====================================
        // 5. MOVE
        // =====================================

        Vector3 walkVelocity = planarVelocity;

        if (grounded &&
            planarVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 alongGround =
                Vector3.ProjectOnPlane(
                    planarVelocity,
                    groundNormal);

            if (alongGround.sqrMagnitude > 0.0001f)
            {
                walkVelocity =
                    alongGround.normalized *
                    planarVelocity.magnitude;
            }
        }

        Vector3 velocity =
            walkVelocity +
            slideVelocity +
            Vector3.up * verticalVelocity;

        bool wasGrounded = grounded;

        controller.Move(velocity * dt);

        // Stop jump when hitting a ceiling

        if ((controller.collisionFlags &
             CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        // =====================================
        // 6. SNAP TO GROUND
        // =====================================

        if (wasGrounded &&
            !jumpedThisFrame &&
            !controller.isGrounded)
        {
            if (ProbeGround(
                    groundSnapDistance,
                    out RaycastHit snapHit) &&
                Vector3.Angle(
                    snapHit.normal,
                    Vector3.up) <= maxStandAngle &&
                snapHit.distance > 0.001f)
            {
                controller.Move(
                    Vector3.down * snapHit.distance);
            }
        }

        // =====================================
        // 7. ANIMATION
        // =====================================

        UpdateAnimator(dt, jumpedThisFrame);

        // =====================================
        // 8. FOOTSTEP AUDIO
        // =====================================

        UpdateFootstepSound(
            isMoving,
            isRunning,
            jumpedThisFrame);
    }

    // =====================================
    // FOOTSTEP SOUND
    // =====================================

    void UpdateFootstepSound(
        bool isMoving,
        bool isRunning,
        bool jumpedThisFrame)
    {
        if (footstepAudio == null ||
            footstepSound == null)
        {
            StopFootsteps();
            return;
        }

        // Use the existing custom ground detection.
        // Short grace period supports curved surfaces.

        bool recentlyGrounded =
            Time.time - lastGroundedTime <= 0.15f;

        bool isOnGround =
            (grounded || recentlyGrounded) &&
            !jumpedSinceGrounded &&
            verticalVelocity <= 0.01f;

        // IMPORTANT:
        // Footsteps require active movement input.
        // Releasing WASD stops the audio immediately.

        bool shouldPlay =
            isMoving &&
            isOnGround &&
            !jumpedThisFrame;

        if (!shouldPlay)
        {
            StopFootsteps();
            return;
        }

        // Update the clip if needed

        if (footstepAudio.clip != footstepSound)
        {
            footstepAudio.Stop();
            footstepAudio.clip = footstepSound;
        }

        // Normal walking vs running

        footstepAudio.pitch =
            isRunning
                ? runFootstepPitch
                : walkFootstepPitch;

        footstepAudio.volume = footstepVolume;

        // Start only once.
        // Looping is handled by Audio Source.

        if (!footstepAudio.isPlaying)
        {
            footstepAudio.Play();
        }
    }

    // =====================================
    // STOP FOOTSTEPS
    // =====================================

    void StopFootsteps()
    {
        if (footstepAudio == null)
            return;

        if (footstepAudio.isPlaying)
            footstepAudio.Stop();
    }

    // =====================================
    // JUMP SOUND
    // =====================================

    void PlayJumpSound()
    {
        if (jumpAudio == null ||
            jumpSound == null)
            return;

        jumpAudio.PlayOneShot(
            jumpSound,
            jumpVolume);
    }

    // =====================================
    // ANIMATION
    // =====================================

    void UpdateAnimator(
        float dt,
        bool jumpedThisFrame)
    {
        if (foxAnimator == null)
            return;

        float planarSpeed =
            planarVelocity.magnitude;

        float animationSpeed;

        if (planarSpeed <= speed ||
            runSpeed <= speed)
        {
            animationSpeed =
                speed > 0f
                    ? 0.5f * planarSpeed / speed
                    : 0f;
        }
        else
        {
            animationSpeed =
                0.5f +
                0.5f *
                (planarSpeed - speed) /
                (runSpeed - speed);
        }

        if (hasSpeedParam)
        {
            foxAnimator.SetFloat(
                SpeedParam,
                Mathf.Clamp01(animationSpeed),
                0.08f,
                dt);
        }

        if (hasGroundedParam)
        {
            bool animGrounded =
                !jumpedSinceGrounded &&
                Time.time - lastGroundedTime
                    <= 0.15f;

            foxAnimator.SetBool(
                GroundedParam,
                animGrounded);
        }

        if (jumpedThisFrame && hasJumpParam)
        {
            foxAnimator.SetTrigger(JumpParam);
        }
    }

    // =====================================
    // GROUND DETECTION
    // =====================================

    void GetWorldCapsule(
        out float worldRadius,
        out float worldHeight)
    {
        Vector3 s = transform.lossyScale;

        worldRadius =
            controller.radius *
            Mathf.Max(
                Mathf.Abs(s.x),
                Mathf.Abs(s.z));

        worldHeight =
            Mathf.Max(
                controller.height *
                Mathf.Abs(s.y),
                worldRadius * 2f);
    }

    bool ProbeGround(
        float maxDistance,
        out RaycastHit hit)
    {
        GetWorldCapsule(
            out float worldRadius,
            out float worldHeight);

        Vector3 centre =
            transform.TransformPoint(
                controller.center);

        float probeRadius =
            worldRadius * 0.95f;

        float lift = 0.1f;

        Vector3 origin =
            centre +
            Vector3.down *
            (worldHeight * 0.5f - worldRadius) +
            Vector3.up * lift;

        float extra =
            lift + (worldRadius - probeRadius);

        bool found =
            Physics.SphereCast(
                origin,
                probeRadius,
                Vector3.down,
                out hit,
                maxDistance +
                extra +
                controller.skinWidth,
                groundMask,
                QueryTriggerInteraction.Ignore);

        if (!found)
            return false;

        // Ignore the player's own colliders

        if (hit.collider != null &&
            hit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        hit.distance =
            Mathf.Max(
                0f,
                hit.distance - extra);

        return true;
    }

    // =====================================
    // PUSHABLE OBJECTS
    // =====================================

    void OnControllerColliderHit(
        ControllerColliderHit hit)
    {
        Rigidbody body =
            hit.collider.attachedRigidbody;

        if (body != null &&
            !body.isKinematic &&
            hit.moveDirection.y > -0.3f)
        {
            Vector3 pushDirection =
                new Vector3(
                    hit.moveDirection.x,
                    0f,
                    hit.moveDirection.z);

            if (pushDirection.sqrMagnitude > 0.01f)
            {
                body.AddForce(
                    pushDirection.normalized *
                    pushAcceleration,
                    ForceMode.Acceleration);
            }
        }
    }

    // =====================================
    // DISABLE
    // =====================================

    void OnDisable()
    {
        StopFootsteps();

        if (jumpAudio != null)
            jumpAudio.Stop();
    }
}
