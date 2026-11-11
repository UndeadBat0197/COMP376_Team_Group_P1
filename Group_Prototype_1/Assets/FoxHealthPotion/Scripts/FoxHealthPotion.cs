using UnityEngine;

/// <summary>
/// Standalone one-heart pickup for this project's PlayerHealth / HealthUI system.
/// Put the prefab in a scene. Does not change apples or scoring.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class FoxHealthPotion : MonoBehaviour
{
    [Header("Healing")]
    [Min(0.01f)] public float healAmount = 1f;
    public bool consumeOnlyIfInjured = true;

    [Header("Appearance / Motion")]
    public Transform visualRoot;
    public float spinDegreesPerSecond = 65f;
    public float bobHeight = 0.15f;
    public float bobFrequency = 1.4f;
    public float pulseAmount = 0.045f;

    [Header("Optional Feedback")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float soundVolume = 0.7f;

    private bool collected;
    private Vector3 visualBasePosition;
    private Vector3 visualBaseScale;
    private float phase;

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        if (trigger.radius < 0.1f) trigger.radius = 0.85f;
        if (visualRoot == null)
        {
            Transform candidate = transform.Find("Visual");
            if (candidate != null) visualRoot = candidate;
        }
        if (visualRoot != null)
        {
            visualBasePosition = visualRoot.localPosition;
            visualBaseScale = visualRoot.localScale;
        }
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (collected || visualRoot == null) return;
        float time = Time.time * bobFrequency * Mathf.PI * 2f + phase;
        visualRoot.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(time) * bobHeight);
        visualRoot.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
        visualRoot.localScale = visualBaseScale * (1f + Mathf.Sin(time) * pulseAmount);
    }

    private void OnTriggerEnter(Collider other) { TryHeal(other); }
    // Also allows the player to collect it after taking damage while already in range.
    private void OnTriggerStay(Collider other) { TryHeal(other); }

    private void TryHeal(Collider other)
    {
        if (collected || other == null) return;
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead) return;
        if (consumeOnlyIfInjured && health.CurrentHealth >= health.maxHealth - 0.001f) return;

        float before = health.CurrentHealth;
        health.Heal(healAmount);
        if (health.CurrentHealth <= before + 0.001f) return;

        collected = true;
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        Debug.Log($"Health Potion: +{health.CurrentHealth - before:0.##} HP ({health.CurrentHealth:0.##}/{health.maxHealth:0.##})");
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        healAmount = Mathf.Max(0.01f, healAmount);
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger != null) trigger.isTrigger = true;
    }
}
