
using UnityEngine;

// Attach to the fox, on the same object as CharacterController.
public class WallDamageOnHit : MonoBehaviour
{
    [Header("Walls that hurt the fox")]
    public Collider[] damagingWalls;

    [Header("Damage")]
    [Min(0.01f)]
    public float damagePerHit = 1f;

    public bool logSuccessfulHits = true;

    private PlayerHealth playerHealth;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (GetComponent<CharacterController>() == null)
            Debug.LogError(
                "Attach WallDamageOnHit to the object with CharacterController.",
                this);

        if (playerHealth == null)
            Debug.LogError(
                "PlayerHealth component not found.",
                this);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (playerHealth == null ||
            hit.collider == null ||
            damagingWalls == null)
            return;

        foreach (Collider wall in damagingWalls)
        {
            if (wall == null || hit.collider != wall)
                continue;

            // Uses existing health, invincibility and death system.
            bool damaged = playerHealth.TakeDamage(damagePerHit);

            if (damaged && logSuccessfulHits)
                Debug.Log("Fox hit dangerous wall: " + wall.name);

            return;
        }
    }
}
