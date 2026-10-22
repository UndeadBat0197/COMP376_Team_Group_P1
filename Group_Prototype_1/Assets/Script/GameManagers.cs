using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private int applesCollected = 0;
    private int totalApples;

    private Text appleText;
    private Text winText;

    private GameObject startMenu;
    private GameObject pauseMenu;

    private bool gameStarted = false;
    private bool gameWon = false;
    private bool gamePaused = false;

    private Transform player;

    private float fallDeathY = -45f;
    private bool restarting = false;

    void Awake()
    {
        Instance = this;

        // Always restore normal time when the scene loads.
        Time.timeScale = 1f;
    }

    void Start()
    {
        // Automatically count all fruits in the current scene.
        totalApples =
            FindObjectsByType<Fruit>(FindObjectsSortMode.None).Length;

        Debug.Log("Total apples in scene: " + totalApples);

        // Find the player.
        GameObject playerObject =
            GameObject.Find("Third Person Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogError("Third Person Player not found!");
        }

        // Create all UI.
        CreateUI();

        // Set initial apple counter.
        UpdateAppleText();

        // Check whether this scene load came from our RestartGame().
        if (PlayerPrefs.GetInt("SkipStartMenu", 0) == 1)
        {
            // Clear the flag immediately so it only works once.
            PlayerPrefs.SetInt("SkipStartMenu", 0);
            PlayerPrefs.Save();

            // Restart directly into gameplay.
            StartGameplay();
        }
        else
        {
            // Normal first launch.
            ShowStartMenu();
        }
    }

    void Update()
    {
        // Do not run gameplay logic before Start Game is pressed.
        if (!gameStarted)
            return;

        // Restart if the player falls below the map.
        if (player != null &&
            player.position.y < fallDeathY &&
            !restarting &&
            !gameWon)
        {
            restarting = true;
            RestartGame();
            return;
        }

        // ESC opens/closes the pause menu.
        if (Input.GetKeyDown(KeyCode.Escape) && !gameWon)
        {
            if (gamePaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // =========================================
    // Apple Collection
    // =========================================

    public void CollectApple()
    {
        // Do not collect apples before starting,
        // while paused, or after winning.
        if (!gameStarted || gamePaused || gameWon)
            return;

        applesCollected++;

        UpdateAppleText();

        if (applesCollected >= totalApples)
        {
            WinGame();
        }
    }

    void UpdateAppleText()
    {
        appleText.text =
            "Apples: " + applesCollected + "/" + totalApples;
    }

    // =========================================
    // Start Menu
    // =========================================

    void ShowStartMenu()
    {
        gameStarted = false;
        gamePaused = false;

        startMenu.SetActive(true);
        pauseMenu.SetActive(false);

        // Freeze the game while the start menu is open.
        Time.timeScale = 0f;

        // Release mouse for the Start button.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartGame()
    {
        StartGameplay();
    }

    void StartGameplay()
    {
        gameStarted = true;
        gamePaused = false;

        startMenu.SetActive(false);
        pauseMenu.SetActive(false);

        // Start/resume gameplay.
        Time.timeScale = 1f;

        // Lock mouse for gameplay.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // =========================================
    // Win
    // =========================================

    void WinGame()
    {
        gameWon = true;

        winText.text = "YOU WIN!";
        winText.gameObject.SetActive(true);

        // Freeze the game.
        Time.timeScale = 0f;

        // Release the mouse.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // =========================================
    // Pause
    // =========================================

    void PauseGame()
    {
        gamePaused = true;

        pauseMenu.SetActive(true);

        // Freeze the game.
        Time.timeScale = 0f;

        // Release mouse for buttons.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        gamePaused = false;

        pauseMenu.SetActive(false);

        // Resume the game.
        Time.timeScale = 1f;

        // Lock mouse again.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // =========================================
    // Restart
    // =========================================

    public void RestartGame()
    {
        // Tell the next scene load to skip the start menu.
        PlayerPrefs.SetInt("SkipStartMenu", 1);
        PlayerPrefs.Save();

        // Restore time before reloading.
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    // =========================================
    // Create UI
    // =========================================

    void CreateUI()
    {
        // =====================================
        // Canvas
        // =====================================

        GameObject canvasObject =
            new GameObject("Game UI Canvas");

        Canvas canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();


        // =====================================
        // Apple Counter
        // =====================================

        GameObject appleObject =
            new GameObject("Apple Counter");

        appleObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        appleText =
            appleObject.AddComponent<Text>();

        appleText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        appleText.fontSize = 32;
        appleText.color = Color.white;

        RectTransform appleRect =
            appleText.rectTransform;

        appleRect.anchorMin =
            new Vector2(1, 1);

        appleRect.anchorMax =
            new Vector2(1, 1);

        appleRect.pivot =
            new Vector2(1, 1);

        appleRect.anchoredPosition =
            new Vector2(-30, -30);

        appleRect.sizeDelta =
            new Vector2(300, 60);


        // =====================================
        // Win Text
        // =====================================

        GameObject winObject =
            new GameObject("Win Text");

        winObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        winText =
            winObject.AddComponent<Text>();

        winText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        winText.fontSize = 70;

        winText.alignment =
            TextAnchor.MiddleCenter;

        winText.color = Color.white;

        RectTransform winRect =
            winText.rectTransform;

        winRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        winRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        winRect.pivot =
            new Vector2(0.5f, 0.5f);

        winRect.anchoredPosition =
            Vector2.zero;

        winRect.sizeDelta =
            new Vector2(600, 150);

        winObject.SetActive(false);


        // =====================================
        // START MENU
        // =====================================

        startMenu =
            new GameObject("Start Menu");

        startMenu.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform startMenuRect =
            startMenu.AddComponent<RectTransform>();

        startMenuRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        startMenuRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        startMenuRect.pivot =
            new Vector2(0.5f, 0.5f);

        startMenuRect.anchoredPosition =
            Vector2.zero;

        startMenuRect.sizeDelta =
            new Vector2(700, 600);


        // =====================================
        // Start Menu Title
        // =====================================

        GameObject titleObject =
            new GameObject("Title");

        titleObject.transform.SetParent(
            startMenu.transform,
            false
        );

        Text titleText =
            titleObject.AddComponent<Text>();

        titleText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        titleText.fontSize = 60;

        titleText.alignment =
            TextAnchor.MiddleCenter;

        titleText.color = Color.white;

        titleText.text =
            "COLLECT ALL APPLES!";

        RectTransform titleRect =
            titleText.rectTransform;

        titleRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        titleRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        titleRect.pivot =
            new Vector2(0.5f, 0.5f);

        titleRect.anchoredPosition =
            new Vector2(0, 180);

        titleRect.sizeDelta =
            new Vector2(700, 100);


        // =====================================
        // Start Menu Instructions
        // =====================================

        GameObject instructionsObject =
            new GameObject("Instructions");

        instructionsObject.transform.SetParent(
            startMenu.transform,
            false
        );

        Text instructionsText =
            instructionsObject.AddComponent<Text>();

        instructionsText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        instructionsText.fontSize = 30;

        instructionsText.alignment =
            TextAnchor.MiddleCenter;

        instructionsText.color =
            Color.white;

        instructionsText.text =
            "WASD - Move\n" +
            "Q / R - Rotate\n" +
            "ESC - Pause";

        RectTransform instructionsRect =
            instructionsText.rectTransform;

        instructionsRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        instructionsRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        instructionsRect.pivot =
            new Vector2(0.5f, 0.5f);

        instructionsRect.anchoredPosition =
            new Vector2(0, 20);

        instructionsRect.sizeDelta =
            new Vector2(500, 180);


        // =====================================
        // Start Button
        // =====================================

        CreateButton(
            startMenu.transform,
            "Start Game",
            new Vector2(0, -140),
            StartGame
        );


        // =====================================
        // PAUSE MENU
        // =====================================

        pauseMenu =
            new GameObject("Pause Menu");

        pauseMenu.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform menuRect =
            pauseMenu.AddComponent<RectTransform>();

        menuRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        menuRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        menuRect.pivot =
            new Vector2(0.5f, 0.5f);

        menuRect.anchoredPosition =
            Vector2.zero;

        menuRect.sizeDelta =
            new Vector2(500, 500);


        // =====================================
        // PAUSED Text
        // =====================================

        GameObject pausedTextObject =
            new GameObject("Paused Text");

        pausedTextObject.transform.SetParent(
            pauseMenu.transform,
            false
        );

        Text pausedText =
            pausedTextObject.AddComponent<Text>();

        pausedText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        pausedText.fontSize = 60;

        pausedText.alignment =
            TextAnchor.MiddleCenter;

        pausedText.color =
            Color.white;

        pausedText.text =
            "PAUSED";

        RectTransform pausedRect =
            pausedText.rectTransform;

        pausedRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        pausedRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        pausedRect.pivot =
            new Vector2(0.5f, 0.5f);

        pausedRect.anchoredPosition =
            new Vector2(0, 160);

        pausedRect.sizeDelta =
            new Vector2(400, 80);


        // =====================================
        // Pause Menu Instructions
        // =====================================

        GameObject pauseInstructionsObject =
            new GameObject("Pause Instructions");

        pauseInstructionsObject.transform.SetParent(
            pauseMenu.transform,
            false
        );

        Text pauseInstructionsText =
            pauseInstructionsObject.AddComponent<Text>();

        pauseInstructionsText.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        pauseInstructionsText.fontSize = 26;

        pauseInstructionsText.alignment =
            TextAnchor.MiddleCenter;

        pauseInstructionsText.color =
            Color.white;

        pauseInstructionsText.text =
            "WASD - Move\n" +
            "Q / R - Rotate\n" +
            "ESC - Pause";

        RectTransform pauseInstructionsRect =
            pauseInstructionsText.rectTransform;

        pauseInstructionsRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        pauseInstructionsRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        pauseInstructionsRect.pivot =
            new Vector2(0.5f, 0.5f);

        pauseInstructionsRect.anchoredPosition =
            new Vector2(0, 30);

        pauseInstructionsRect.sizeDelta =
            new Vector2(400, 130);


        // =====================================
        // Resume Button
        // =====================================

        CreateButton(
            pauseMenu.transform,
            "Resume",
            new Vector2(0, -80),
            ResumeGame
        );


        // =====================================
        // Restart Button
        // =====================================

        CreateButton(
            pauseMenu.transform,
            "Restart",
            new Vector2(0, -160),
            RestartGame
        );


        // Pause menu starts hidden.
        pauseMenu.SetActive(false);
    }


    // =========================================
    // Button Creator
    // =========================================

    void CreateButton(
        Transform parent,
        string buttonText,
        Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject =
            new GameObject(
                buttonText + " Button"
            );

        buttonObject.transform.SetParent(
            parent,
            false
        );

        Image image =
            buttonObject.AddComponent<Image>();

        image.color =
            Color.white;

        Button button =
            buttonObject.AddComponent<Button>();

        button.onClick.AddListener(action);


        // Button position and size.
        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.anchoredPosition =
            position;

        rect.sizeDelta =
            new Vector2(250, 60);


        // =====================================
        // Button Text
        // =====================================

        GameObject textObject =
            new GameObject("Text");

        textObject.transform.SetParent(
            buttonObject.transform,
            false
        );

        Text text =
            textObject.AddComponent<Text>();

        text.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );

        text.fontSize = 30;

        text.alignment =
            TextAnchor.MiddleCenter;

        text.color =
            Color.black;

        text.text =
            buttonText;


        // Make text fill the button.
        RectTransform textRect =
            text.rectTransform;

        textRect.anchorMin =
            Vector2.zero;

        textRect.anchorMax =
            Vector2.one;

        textRect.offsetMin =
            Vector2.zero;

        textRect.offsetMax =
            Vector2.zero;
    }
}