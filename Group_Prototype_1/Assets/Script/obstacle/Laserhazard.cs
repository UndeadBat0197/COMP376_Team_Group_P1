using UnityEngine;

// Attach to each laser beam; its BoxCollider must be Is Trigger.
[RequireComponent(typeof(BoxCollider))]
public class LaserHazard : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Same CharacterController + Player tag check used by Fruit.cs.
        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player == null || !player.CompareTag("Player"))
            return;

        PlayerRetry retry = player.GetComponent<PlayerRetry>();
        if (retry == null)
            retry = player.GetComponentInParent<PlayerRetry>();

        if (retry != null)
        {
            Debug.Log("Laser hit! Restarting level.");
            retry.RestartLevel();
        }
        else
        {
            Debug.LogWarning("Add PlayerRetry to the player to restart on laser hits.");
        }
    }
}
