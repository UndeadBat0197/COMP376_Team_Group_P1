using UnityEngine;

/// <summary>
/// The goal of the level. Spins and bobs; touching it wins the game.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalAcorn : MonoBehaviour
{
    [Tooltip("The part that spins and bobs. Uses this object if empty.")]
    public Transform visual;

    public float spinDegreesPerSecond = 70f;
    public float bobHeight = 0.25f;
    public float bobSpeed = 1.6f;

    private Vector3 restPosition;
    private bool reached;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (visual == null)
            visual = transform;

        restPosition = visual.localPosition;
    }

    void Update()
    {
        if (visual == null)
            return;

        // Unscaled time keeps the acorn moving on the win screen.
        float t = Time.unscaledTime;

        visual.localPosition =
            restPosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);

        visual.Rotate(
            Vector3.up,
            spinDegreesPerSecond * Time.unscaledDeltaTime,
            Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (reached)
            return;

        CharacterController player =
            other.GetComponentInParent<CharacterController>();

        if (player == null || !player.CompareTag("Player"))
            return;

        reached = true;

        if (GameManager.Instance != null)
            GameManager.Instance.ReachGoal();
        else
            Debug.Log("Acorn reached! (no GameManager in the scene)");
    }
}
