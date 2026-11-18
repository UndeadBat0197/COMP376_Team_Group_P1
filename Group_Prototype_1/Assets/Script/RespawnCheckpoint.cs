using UnityEngine;

/// <summary>
/// Add to a trigger on (or just next to) a respawn point. Assign the
/// matching Transform that appears in NearestRespawn's Inspector arrays.
/// Do not use TreeCheckpoint on the same trigger.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class RespawnCheckpoint : MonoBehaviour
{
    [Tooltip("The safe respawn Transform listed in the fox's NearestRespawn array.")]
    public Transform respawnPoint;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (respawnPoint == null)
            respawnPoint = transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivate(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Handles the case where the fox starts or respawns inside the volume.
        TryActivate(other);
    }

    private void TryActivate(Collider other)
    {
        CharacterController fox = other.GetComponentInParent<CharacterController>();
        if (fox == null)
            return;

        NearestRespawn respawn = fox.GetComponent<NearestRespawn>();
        if (respawn == null)
            respawn = fox.GetComponentInParent<NearestRespawn>();

        if (respawn == null)
            return;

        respawn.ActivateCheckpoint(respawnPoint != null ? respawnPoint : transform);
    }
}
