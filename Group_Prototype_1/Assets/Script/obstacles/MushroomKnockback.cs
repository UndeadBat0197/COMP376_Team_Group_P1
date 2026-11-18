using System.Collections;
using UnityEngine;
/// <summary>
/// makes the player bounce off a mushroom when they touch it. The player is pushed away from the mushroom and pops up into the air.
/// can be used for other objects as well, like a spring or a trampoline. The player is pushed away from the object and pops up into the air.
/// </summary>
// Attach to your mushroom with a Collider (set "Is Trigger" to true).
// Works directly with CharacterController

public class MushroomKnockback : MonoBehaviour
{
    [Header("Knockback Settings")]
    [Tooltip("Horizontal push speed in units/sec.")]
    public float knockbackForce = 16f;

    [Tooltip("Upward bounce speed in units/sec.")]
    public float upwardForce = 8f;

    [Tooltip("How long the push lasts in seconds.")]
    public float pushDuration = 0.25f;

    [Tooltip("Prevents re-triggering on the same player immediately.")]
    public float cooldown = 0.3f;

    private float lastTriggerTime = -10f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time - lastTriggerTime < cooldown) return;

        // Find the CharacterController on the player or its parent
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (controller == null) return;

        lastTriggerTime = Time.time;

        // Calculate horizontal direction away from the mushroom
        Vector3 direction = controller.transform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = -controller.transform.forward;
        else
            direction.Normalize();

        StartCoroutine(PushController(controller, direction));
    }

    private IEnumerator PushController(CharacterController controller, Vector3 horizontalDir)
    {
        float elapsed = 0f;
        float currentUpward = upwardForce;

        while (elapsed < pushDuration)
        {
            if (controller == null) yield break;

            float dt = Time.deltaTime;
            elapsed += dt;

            // Smoothly fade out the horizontal push over the duration
            float progress = elapsed / pushDuration;
            float currentHorizontal = Mathf.Lerp(knockbackForce, 0f, progress);

            // Apply gravity to the upward pop so it arcs naturally
            currentUpward += Physics.gravity.y * dt;

            Vector3 velocity = (horizontalDir * currentHorizontal) + (Vector3.up * currentUpward);
            controller.Move(velocity * dt);

            yield return null;
        }
    }
}
