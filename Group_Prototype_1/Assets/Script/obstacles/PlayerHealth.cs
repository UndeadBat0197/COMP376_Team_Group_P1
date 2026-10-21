using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Put this on the Player (the "Third Person Player" object, same one as the CharacterController).
public class PlayerHealth : MonoBehaviour
{
    public enum DeathAction { Respawn, ReloadScene, Nothing }

    [Header("Health")]
    public float maxHealth = 3f;

    [Tooltip("Seconds of invincibility after getting hit, so one touch = one zap.")]
    public float invincibilityTime = 1f;

    [Tooltip("Make the player blink while invincible.")]
    public bool flickerWhileInvincible = true;

    [Header("Death")]
    public DeathAction onDeath = DeathAction.Respawn;

    [Tooltip("Optional. Where to respawn. Leave empty to respawn where the player started.")]
    public Transform respawnPoint;

    [Header("Events (optional hooks for sounds, effects)")]
    public UnityEvent<float> onDamaged;   // passes current health
    public UnityEvent onDied;

    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;
    public bool IsInvincible => Time.time < invincibleUntil;
    public float HealthPercent => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

    private float invincibleUntil = 0f;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Renderer[] renderers;
    private CharacterController controller;

    void Awake()
    {
        CurrentHealth = maxHealth;
        controller = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        if (!flickerWhileInvincible) return;

        // Blink on/off quickly while invincible, solid otherwise.
        bool visible = !IsInvincible || Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;
        SetVisible(visible);
    }

    // Returns true if the damage was actually applied.
    public bool TakeDamage(float amount)
    {
        if (IsDead || IsInvincible || amount <= 0f)
            return false;

        CurrentHealth -= amount;

        // Avoid rounding leftovers like 0.0001 keeping you alive (3 x 0.3333 = 0.9999).
        if (CurrentHealth < 0.01f)
            CurrentHealth = 0f;

        invincibleUntil = Time.time + invincibilityTime;

        Debug.Log($"Player hit! Health: {CurrentHealth:0.##}/{maxHealth}");
        onDamaged?.Invoke(CurrentHealth);

        if (IsDead)
            Die();

        return true;
    }

    // Damage as a fraction of max health (0.3333 = one third).
    public bool TakeDamageFraction(float fraction)
    {
        return TakeDamage(maxHealth * fraction);
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    void Die()
    {
        Debug.Log("Player died!");
        onDied?.Invoke();

        switch (onDeath)
        {
            case DeathAction.Respawn:
                Respawn();
                break;

            case DeathAction.ReloadScene:
                ReloadScene();
                break;
        }
    }

    public void Respawn()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPosition;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRotation;

        // A CharacterController ignores position changes while enabled, so turn it off briefly.
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(pos, rot);
        if (controller != null) controller.enabled = true;

        CurrentHealth = maxHealth;
        invincibleUntil = Time.time + invincibilityTime;   // brief safety after respawning
    }

    void ReloadScene()
    {
        Scene scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        // In the editor this works even if the scene isn't in Build Settings.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = visible;
    }

    void OnDisable()
    {
        if (renderers != null) SetVisible(true);
    }
}
