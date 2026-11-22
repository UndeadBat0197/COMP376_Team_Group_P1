
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoulderSound : MonoBehaviour
{
    [Header("Sound Effects")]
    public AudioClip rollingSound;
    public AudioClip impactSound;

    [Header("Rolling Settings")]
    public float minRollingSpeed = 0.3f;
    public float maxRollingSpeed = 5f;

    [Range(0f, 1f)]
    public float rollingVolume = 0.4f;

    [Header("Impact Settings")]
    public float minImpactSpeed = 1.5f;

    [Range(0f, 1f)]
    public float impactVolume = 0.7f;

    public float impactCooldown = 0.2f;

    [Header("3D Audio Settings")]
    public float minDistance = 2f;
    public float maxDistance = 25f;

    private Rigidbody rb;

    private AudioSource rollingAudio;
    private AudioSource impactAudio;

    private float lastImpactTime = -10f;
    private float lastGroundContactTime = -10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Create rolling audio source
        rollingAudio =
            gameObject.AddComponent<AudioSource>();

        rollingAudio.playOnAwake = false;
        rollingAudio.loop = true;
        rollingAudio.spatialBlend = 1f;
        rollingAudio.volume = rollingVolume;
        rollingAudio.minDistance = minDistance;
        rollingAudio.maxDistance = maxDistance;
        rollingAudio.rolloffMode =
            AudioRolloffMode.Linear;

        // Create impact audio source
        impactAudio =
            gameObject.AddComponent<AudioSource>();

        impactAudio.playOnAwake = false;
        impactAudio.loop = false;
        impactAudio.spatialBlend = 1f;
        impactAudio.volume = 1f;
        impactAudio.minDistance = minDistance;
        impactAudio.maxDistance = maxDistance;
        impactAudio.rolloffMode =
            AudioRolloffMode.Linear;
    }

    void Update()
    {
        UpdateRollingSound();
    }

    void UpdateRollingSound()
    {
        if (rb == null ||
            rollingAudio == null ||
            rollingSound == null)
        {
            StopRolling();
            return;
        }

        // Supports Unity 2022 / 2023.
        // For Unity 6, velocity can be changed
        // to linearVelocity.
        Vector3 velocity = rb.linearVelocity;

        // Calculate horizontal movement
        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        float movementSpeed =
            horizontalVelocity.magnitude;

        // Check if the boulder is touching
        // a surface.
        bool touchingSurface =
            Time.time - lastGroundContactTime
            <= 0.2f;

        bool shouldRoll =
            touchingSurface &&
            movementSpeed >= minRollingSpeed;

        if (!shouldRoll)
        {
            StopRolling();
            return;
        }

        // Assign rolling clip
        if (rollingAudio.clip != rollingSound)
        {
            rollingAudio.Stop();
            rollingAudio.clip = rollingSound;
        }

        // Calculate volume based on speed
        float speedFactor = Mathf.InverseLerp(
            minRollingSpeed,
            Mathf.Max(
                maxRollingSpeed,
                minRollingSpeed + 0.01f
            ),
            movementSpeed
        );

        rollingAudio.volume = Mathf.Lerp(
            0.1f,
            rollingVolume,
            speedFactor
        );

        // Faster movement = slightly faster sound
        rollingAudio.pitch = Mathf.Lerp(
            0.85f,
            1.2f,
            speedFactor
        );

        // Start looping only once
        if (!rollingAudio.isPlaying)
        {
            rollingAudio.Play();
        }
    }

    void StopRolling()
    {
        if (rollingAudio != null &&
            rollingAudio.isPlaying)
        {
            rollingAudio.Stop();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        RegisterSurfaceContact(collision);

        if (impactSound == null ||
            impactAudio == null)
            return;

        // Prevent repeated impact sounds
        if (Time.time - lastImpactTime <
            impactCooldown)
            return;

        float collisionSpeed =
            collision.relativeVelocity.magnitude;

        if (collisionSpeed < minImpactSpeed)
            return;

        // Louder impact for stronger collision
        float strength = Mathf.InverseLerp(
            minImpactSpeed,
            minImpactSpeed + 8f,
            collisionSpeed
        );

        float volume = Mathf.Lerp(
            0.2f,
            impactVolume,
            strength
        );

        impactAudio.PlayOneShot(
            impactSound,
            volume
        );

        lastImpactTime = Time.time;
    }

    void OnCollisionStay(Collision collision)
    {
        RegisterSurfaceContact(collision);
    }

    void RegisterSurfaceContact(
        Collision collision)
    {
        // Detect contact with supporting surfaces.
        // Only surfaces with an upward normal
        // count as ground.

        for (int i = 0;
             i < collision.contactCount;
             i++)
        {
            ContactPoint contact =
                collision.GetContact(i);

            if (contact.normal.y > 0.3f)
            {
                lastGroundContactTime =
                    Time.time;
                return;
            }
        }
    }

    void OnDisable()
    {
        StopRolling();

        if (impactAudio != null)
            impactAudio.Stop();
    }

    void OnDestroy()
    {
        if (rollingAudio != null)
            rollingAudio.Stop();

        if (impactAudio != null)
            impactAudio.Stop();
    }
}
