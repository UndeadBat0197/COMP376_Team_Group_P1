
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class Fruit : MonoBehaviour
{
    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        // Check whether the object touching the fruit
        // belongs to the player.
        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player == null || !player.CompareTag("Player"))
            return;

        collected = true;

        Debug.Log("Fruit collected!");

        // Remove the fruit from the scene.
        Destroy(gameObject);
    }
}
