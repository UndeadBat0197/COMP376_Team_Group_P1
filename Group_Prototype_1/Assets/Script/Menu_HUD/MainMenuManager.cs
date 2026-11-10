using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    public List<RectTransform> menuPanels = new List<RectTransform>();

    [Header("Starting Panel")]
    public RectTransform startingPanel;

    [Header("Cylinder Settings")]
    public CylinderMenuRotation cylinderScript;
    public float transitionSpeedMultiplier = 12f;
    public float transitionDuration = 0.4f;
    public float offScreenWidth = 1920f;

    private RectTransform activePanel;
    private Coroutine activeTransition;

    void Start()
    {
        // Turn off all panels except the starting panel initially
        foreach (RectTransform panel in menuPanels)
        {
            if (panel != null)
                panel.gameObject.SetActive(panel == startingPanel);
        }

        if (startingPanel != null)
        {
            activePanel = startingPanel;
            activePanel.anchoredPosition = Vector2.zero;
        }
    }

    // Call this from your UI Button OnClick() events
    public void ShowPanel(RectTransform targetPanel)
    {
        // Don't interrupt a transition or transition to the same panel
        if (targetPanel == null || targetPanel == activePanel || activeTransition != null) return;

        activeTransition = StartCoroutine(RotateAndSlideRoutine(activePanel, targetPanel));
    }

    private IEnumerator RotateAndSlideRoutine(RectTransform oldPanel, RectTransform newPanel)
    {
        float elapsedTime = 0f;

        Vector2 centerPosition = Vector2.zero;
        Vector2 exitPosition = new Vector2(-offScreenWidth, 0f);
        Vector2 entryPosition = new Vector2(offScreenWidth, 0f);

        Vector2 startOldPos = oldPanel.anchoredPosition;

        // Prepare the new panel before animating it
        newPanel.gameObject.SetActive(true);
        newPanel.anchoredPosition = entryPosition;

        float baseSpeed = cylinderScript.normalSpeed;
        float topSpeed = baseSpeed * transitionSpeedMultiplier;

        //while (elapsedTime < transitionDuration)
        //{
        //    elapsedTime += Time.deltaTime;
        //    float t = elapsedTime / transitionDuration;
        //    float tSmooth = Mathf.SmoothStep(0f, 1f, t);

        //    // Animate cylinder speed spike
        //    float speedFactor = Mathf.Sin(t * Mathf.PI);
        //    cylinderScript.currentSpeed = Mathf.Lerp(baseSpeed, topSpeed, speedFactor);

        //    // Animate sliding positions
        //    oldPanel.anchoredPosition = Vector2.Lerp(startOldPos, exitPosition, tSmooth);
        //    newPanel.anchoredPosition = Vector2.Lerp(entryPosition, centerPosition, tSmooth);

        //    yield return null;
        //}

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / transitionDuration;
            float tSmooth = Mathf.SmoothStep(0f, 1f, t);

            // Squaring the sine wave makes the acceleration burst sharper and more noticeable!
            float speedFactor = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2f);
            cylinderScript.currentSpeed = Mathf.Lerp(baseSpeed, topSpeed, speedFactor);

            // Animate sliding positions
            oldPanel.anchoredPosition = Vector2.Lerp(startOldPos, exitPosition, tSmooth);
            newPanel.anchoredPosition = Vector2.Lerp(entryPosition, centerPosition, tSmooth);

            yield return null;
        }


        // Lock positions exactly into place
        oldPanel.anchoredPosition = exitPosition;
        newPanel.anchoredPosition = centerPosition;

        // Safe to disable the old panel now that it's off-screen
        oldPanel.gameObject.SetActive(false);
        cylinderScript.currentSpeed = baseSpeed;

        activePanel = newPanel;
        activeTransition = null;
    }
}
