
using UnityEngine;

public class TreeRotationSound : MonoBehaviour
{
    [Header("Rotation Reference")]
    public TreeRotator treeRotator;

    [Header("Rotation Sound")]
    public AudioClip rotationSound;

    [Header("Sound Settings")]
    [Range(0f, 1f)]
    public float volume = 0.45f;

    public float minRotationSpeed = 0.1f;
    public float maxRotationSpeed = 100f;

    [Range(0.1f, 3f)]
    public float normalPitch = 1f;

    public bool changePitchWithSpeed = false;

    private AudioSource audioSource;
    private float previousAngle;
    private bool initialized = false;

    void Start()
    {
        // Automatically find TreeRotator
        if (treeRotator == null)
        {
            treeRotator =
                FindFirstObjectByType<TreeRotator>();
        }

        // Create dedicated Audio Source
        audioSource =
            gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
        audioSource.pitch = normalPitch;

        audioSource.clip = rotationSound;

        if (treeRotator != null)
        {
            previousAngle = treeRotator.CurrentAngle;
            initialized = true;
        }
        else
        {
            Debug.LogWarning(
                "TreeRotationSound: TreeRotator not found!"
            );
        }
    }

    void LateUpdate()
    {
        if (treeRotator == null)
        {
            treeRotator =
                FindFirstObjectByType<TreeRotator>();

            if (treeRotator == null)
            {
                StopSound();
                return;
            }

            previousAngle = treeRotator.CurrentAngle;
            initialized = true;
        }

        if (!initialized)
        {
            previousAngle = treeRotator.CurrentAngle;
            initialized = true;
            return;
        }

        float currentAngle = treeRotator.CurrentAngle;

        float angleDifference = Mathf.Abs(
            Mathf.DeltaAngle(
                previousAngle,
                currentAngle
            )
        );

        previousAngle = currentAngle;

        if (Time.timeScale <= 0f ||
            Time.deltaTime <= 0f)
        {
            StopSound();
            return;
        }

        // Ignore large instant rotations,
        // such as respawning or teleporting.
        if (angleDifference > 45f)
        {
            StopSound();
            return;
        }

        float rotationSpeed =
            angleDifference / Time.deltaTime;

        bool isRotating =
            rotationSpeed > minRotationSpeed;

        if (isRotating && rotationSound != null)
        {
            if (audioSource.clip != rotationSound)
            {
                audioSource.Stop();
                audioSource.clip = rotationSound;
            }

            float speedFactor = Mathf.InverseLerp(
                minRotationSpeed,
                Mathf.Max(
                    minRotationSpeed + 0.01f,
                    maxRotationSpeed
                ),
                rotationSpeed
            );

            audioSource.volume = volume;

            if (changePitchWithSpeed)
            {
                audioSource.pitch = Mathf.Lerp(
                    normalPitch * 0.85f,
                    normalPitch * 1.15f,
                    speedFactor
                );
            }
            else
            {
                audioSource.pitch = normalPitch;
            }

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            StopSound();
        }
    }

    void StopSound()
    {
        if (audioSource != null &&
            audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    void OnDisable()
    {
        StopSound();
    }
}
