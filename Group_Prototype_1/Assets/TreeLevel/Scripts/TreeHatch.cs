using UnityEngine;

/// <summary>
/// Sits under the hole in the top of the trunk. Once the player has
/// dropped in, it switches on the patches that close the hole, so the
/// hole cannot end up under the player when the tree turns.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TreeHatch : MonoBehaviour
{
    [Tooltip("Objects that are switched on to close the hole.")]
    public GameObject[] closeOnEnter;

    private bool closed;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (closed)
            return;

        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player == null || !player.CompareTag("Player"))
            return;

        closed = true;

        if (closeOnEnter != null)
        {
            foreach (GameObject piece in closeOnEnter)
            {
                if (piece != null)
                    piece.SetActive(true);
            }
        }

        Physics.SyncTransforms();
    }
}
