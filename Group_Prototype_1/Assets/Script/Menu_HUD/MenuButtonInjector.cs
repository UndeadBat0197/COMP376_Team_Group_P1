using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuButtonInjector : MonoBehaviour
{
    // Automatically runs in EVERY scene without needing to attach it manually
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInject()
    {
        if (SceneManager.GetActiveScene().name == "Main_Menu")
            return;

        if (FindFirstObjectByType<MenuButtonInjector>() == null)
        {
            GameObject injectorObj = new GameObject("MenuButtonInjector_Auto");
            injectorObj.AddComponent<MenuButtonInjector>();
        }
    }

    IEnumerator Start()
    {
        // Wait one frame so GameManager.Start() finishes building Game UI Canvas
        yield return null;

        GameObject canvasObj = GameObject.Find("Game UI Canvas");
        if (canvasObj == null) yield break;

        // 1. Add Main Menu button to Pause Menu (if not already added)
        Transform pauseMenu = canvasObj.transform.Find("Pause Menu");
        if (pauseMenu != null && pauseMenu.Find("Main Menu Button") == null)
        {
            CreateMenuButton(pauseMenu, "Main Menu", new Vector2(0, -240));
        }

        // 2. Add Main Menu button to Win Screen (if not already added)
        Transform winRestart = canvasObj.transform.Find("Restart Button");
        if (winRestart != null && canvasObj.transform.Find("Main Menu Button") == null)
        {
            GameObject winMenuBtn = CreateMenuButton(canvasObj.transform, "Main Menu", new Vector2(0, -150));
            winMenuBtn.SetActive(false);

            // Attach watcher to the active canvasObj so Update() keeps running
            ButtonVisibilitySync sync = canvasObj.AddComponent<ButtonVisibilitySync>();
            sync.targetToWatch = winRestart.gameObject;
            sync.buttonToControl = winMenuBtn;
        }
    }

    GameObject CreateMenuButton(Transform parent, string label, Vector2 position)
    {
        GameObject btnObj = new GameObject(label + " Button");
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(GoToMainMenu);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(250, 60);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);

        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 30;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        txt.text = label;

        RectTransform txtRect = txt.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        return btnObj;
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;

        SceneLoader loader = FindFirstObjectByType<SceneLoader>();
        if (loader != null)
        {
            loader.ReturnToMainMenu();
        }
        else
        {
            SceneManager.LoadScene("Main_Menu");
        }
    }
}

public class ButtonVisibilitySync : MonoBehaviour
{
    public GameObject targetToWatch;
    public GameObject buttonToControl;

    void Update()
    {
        if (targetToWatch != null && buttonToControl != null &&
            buttonToControl.activeSelf != targetToWatch.activeSelf)
        {
            buttonToControl.SetActive(targetToWatch.activeSelf);
        }
    }
}
