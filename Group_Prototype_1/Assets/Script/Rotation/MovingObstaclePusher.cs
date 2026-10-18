
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class MovingObstaclePusher : MonoBehaviour
{
    [Header("References")]
    public CharacterController player;

    [Header("Push Settings")]
    public float separationPadding = 0.02f;

    private BoxCollider obstacleCollider;

    void Awake()
    {
        obstacleCollider = GetComponent<BoxCollider>();
    }

    void LateUpdate()
    {
        if (player == null || !player.enabled)
            return;

        // Check if the rotating obstacle overlaps the player.
        bool overlapping = Physics.ComputePenetration(
            player,
            player.transform.position,
            player.transform.rotation,

            obstacleCollider,
            obstacleCollider.transform.position,
            obstacleCollider.transform.rotation,

            out Vector3 pushDirection,
            out float penetrationDistance
        );

        if (overlapping && penetrationDistance > 0f)
        {
            // Move the character out of the obstacle.
            Vector3 push =
                pushDirection *
                (penetrationDistance + separationPadding);

            player.Move(push);
        }
    }
}
