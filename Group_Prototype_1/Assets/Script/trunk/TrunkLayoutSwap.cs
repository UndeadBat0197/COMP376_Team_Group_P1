
using UnityEngine;

public class TrunkLayoutSwap : MonoBehaviour
{
    [Header("Start position")]
    public GameObject startHoleAndStairs;
    public GameObject startPlain;

    [Header("End position")]
    public GameObject endPlain;
    public GameObject endHoleAndStairs;

    [Header("Optional rotation reset")]
    public TreeRotator treeRotator;
    public bool resetRotation = true;

    private bool hasSwapped = false;

    public void SwapToExitLayout()
    {
        if (hasSwapped) return;

        if (startHoleAndStairs == null ||
            startPlain == null ||
            endPlain == null ||
            endHoleAndStairs == null)
        {
            Debug.LogError("Trunk swap: Missing a layout reference.");
            return;
        }

        // Restore the original Z-axis orientation while
        // the camera is looking away.
        if (resetRotation && treeRotator != null)
            treeRotator.SnapTo(0f);

        // Hide old geometry and its colliders.
        startHoleAndStairs.SetActive(false);
        endPlain.SetActive(false);

        // Show the new geometry and its colliders.
        startPlain.SetActive(true);
        endHoleAndStairs.SetActive(true);

        Physics.SyncTransforms();

        hasSwapped = true;
        Debug.Log("Trunk exit layout activated.");
    }
}
