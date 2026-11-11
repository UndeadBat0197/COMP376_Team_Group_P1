using UnityEngine;

/// <summary>
/// Trigger volume that hurts the player (thorns and similar).
/// Uses the project's existing PlayerHealth, which already handles
/// the short invincibility after a hit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ToonHazard : MonoBehaviour
{
    public float damage = 1f;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Hurt(other);
    }

    void OnTriggerStay(Collider other)
    {
        Hurt(other);
    }

    void Hurt(Collider other)
    {
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health != null)
            health.TakeDamage(damage);
    }
}
