using UnityEngine;
using UnityEngine.SceneManagement;

// Add this component to the same GameObject as ThirdPersonMovement.
[RequireComponent(typeof(CharacterController))]
public class PlayerRetry : MonoBehaviour
{
    [Header("Fall Detection")]
    [Tooltip("Reload the level if the player drops below this WORLD Y position.")]
    public float fallY = -12f;

    private bool restarting;

    private void Update()
    {
        if (!restarting && transform.position.y < fallY)
        {
            RestartLevel();
        }
    }

    public void RestartLevel()
    {
        if (restarting) return;
        restarting = true;

        // Reloading the level restores the original player position and fruits.
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }
}
