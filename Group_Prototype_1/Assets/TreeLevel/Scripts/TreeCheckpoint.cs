using UnityEngine;

/// <summary>
/// Invisible trigger just past each doorway. Walking through it makes
/// this the respawn point, and remembers which way the trunk has to
/// be turned for this part of the level.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TreeCheckpoint : MonoBehaviour
{
    [Tooltip("Trunk angle that puts this section's floor at the bottom.")]
    public float trunkAngle = 0f;

    [Tooltip("Where the player reappears. Uses this object if empty.")]
    public Transform respawnPoint;

    private TreeRotator rotator;
    private bool reached;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        rotator = FindFirstObjectByType<TreeRotator>();
    }

    void OnTriggerEnter(Collider other)
    {
        // Only the first time through: later the player may walk back
        // past this spot with the tree turned some other way.
        if (reached)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health == null)
            return;

        reached = true;

        health.respawnPoint =
            respawnPoint != null ? respawnPoint : transform;

        if (rotator != null)
        {
            rotator.checkpointAngle = trunkAngle;

            // Through a timed door: stop the tree from turning back.
            rotator.HoldAt(trunkAngle);
        }
    }
}
