using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the missing ways out of a level, without changing GameManager:
///
///  - Pause menu (Esc):  "Main Menu" and "Quit" under Resume / Restart
///  - Win screen:        "Main Menu" and "Quit" under Restart
///  - Death screen:      "Try Again", "Main Menu", "Quit" when the
///                       player's health runs out or it falls off
///                       the level
///
/// It adds itself to every level automatically (any scene with a
/// GameManager), so nothing has to be placed in the scenes.
/// </summary>
[DefaultExecutionOrder(-50)]
public class InGameMenuExtras : MonoBehaviour
{
    private const string MenuSceneName = "Main_Menu";
    private const string CanvasName = "Game UI Canvas";

    private GameObject pauseMenu;
    private GameObject winRestartButton;
    private GameObject winMainMenuButton;
    private GameObject winQuitButton;
    private GameObject deathScreen;
    private PlayerHealth health;
    private Transform player;
    private bool deathShown;
    private bool restartOnRetry;
    private bool ready;

    // GameManager restarts the level by itself below y = -45. Catching
    // the fall a little earlier lets the death screen show first.
    private const float FallDeathY = -40f;

    // =====================================================
    // Automatic set-up
    // =====================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterFirstScene()
    {
        EnsureInScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInScene(scene);
    }

    static void EnsureInScene(Scene scene)
    {
        if (scene.name == MenuSceneName)
            return;

        if (FindFirstObjectByType<GameManager>() == null)
            return;

        if (FindFirstObjectByType<InGameMenuExtras>() != null)
            return;

        new GameObject("InGameMenuExtras").AddComponent<InGameMenuExtras>();
    }

    IEnumerator Start()
    {
        // GameManager builds its canvas in Start; wait until it exists.
        yield return null;
        yield return null;

        GameObject canvas = GameObject.Find(CanvasName);

        if (canvas == null)
            yield break;

        Transform canvasT = canvas.transform;

        // ---- pause menu ----
        Transform pause = canvasT.Find("Pause Menu");

        if (pause != null)
        {
            pauseMenu = pause.gameObject;

            if (pause.Find("Main Menu Button") == null)
            {
                CreateButton(pause, "Main Menu",
                    new Vector2(0f, -240f), GoToMainMenu);
            }

            if (CanQuit() && pause.Find("Quit Button") == null)
            {
                CreateButton(pause, "Quit",
                    new Vector2(0f, -320f), QuitGame);
            }
        }

        // ---- win screen ----
        Transform winRestart = canvasT.Find("Restart Button");

        if (winRestart != null)
        {
            winRestartButton = winRestart.gameObject;

            Transform existing = canvasT.Find("Main Menu Button");

            winMainMenuButton = existing != null
                ? existing.gameObject
                : CreateButton(canvasT, "Main Menu",
                    new Vector2(0f, -150f), GoToMainMenu);

            if (CanQuit())
            {
                winQuitButton = CreateButton(canvasT, "Quit",
                    new Vector2(0f, -230f), QuitGame);
            }

            SyncWinButtons();
        }

        // ---- death screen ----
        BuildDeathScreen(canvasT);

        health = FindFirstObjectByType<PlayerHealth>();

        if (health != null)
        {
            if (health.onDied == null)
                health.onDied = new UnityEvent();

            health.onDied.AddListener(OnPlayerDied);
        }

        GameObject playerObject = GameObject.Find("Third Person Player");

        if (playerObject != null)
            player = playerObject.transform;

        ready = true;
    }

    void Update()
    {
        // Fell off the level (only while the game is actually running).
        if (ready && !deathShown && player != null &&
            Time.timeScale > 0f && player.position.y < FallDeathY)
        {
            ShowDeathScreen(true);
        }
    }

    void OnDestroy()
    {
        if (health != null && health.onDied != null)
            health.onDied.RemoveListener(OnPlayerDied);
    }

    void LateUpdate()
    {
        SyncWinButtons();

        if (!deathShown)
            return;

        // Esc should not open the pause menu on top of the death screen.
        if (pauseMenu != null && pauseMenu.activeSelf &&
            GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SyncWinButtons()
    {
        if (winRestartButton == null)
            return;

        bool show = winRestartButton.activeSelf;

        if (winMainMenuButton != null && winMainMenuButton.activeSelf != show)
            winMainMenuButton.SetActive(show);

        if (winQuitButton != null && winQuitButton.activeSelf != show)
            winQuitButton.SetActive(show);
    }

    // =====================================================
    // Death
    // =====================================================

    void OnPlayerDied()
    {
        // That mode reloads the level by itself straight away.
        if (health != null &&
            health.onDeath == PlayerHealth.DeathAction.ReloadScene)
            return;

        ShowDeathScreen(false);
    }

    void ShowDeathScreen(bool restartLevelOnRetry)
    {
        if (deathScreen == null || deathShown)
            return;

        deathShown = true;
        restartOnRetry = restartLevelOnRetry;
        deathScreen.SetActive(true);
        deathScreen.transform.SetAsLastSibling();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void TryAgain()
    {
        deathShown = false;

        if (deathScreen != null)
            deathScreen.SetActive(false);

        // After losing all health, PlayerHealth has normally respawned
        // the player already. After a fall, or if it is set to do nothing
        // on death, restart the level instead.
        bool stillDead = health != null && health.IsDead;

        if ((restartOnRetry || stillDead) && GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
            return;
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void BuildDeathScreen(Transform canvas)
    {
        deathScreen = new GameObject("Death Screen");
        deathScreen.transform.SetParent(canvas, false);

        Image dim = deathScreen.AddComponent<Image>();
        dim.color = new Color(0.05f, 0.02f, 0.04f, 0.75f);

        RectTransform rect = deathScreen.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(deathScreen.transform, false);

        Text title = titleObject.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 90;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(1f, 0.35f, 0.3f, 1f);
        title.text = "YOU DIED";

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 130f);
        titleRect.sizeDelta = new Vector2(900f, 140f);

        CreateButton(deathScreen.transform, "Try Again",
            new Vector2(0f, 0f), TryAgain);

        CreateButton(deathScreen.transform, "Main Menu",
            new Vector2(0f, -80f), GoToMainMenu);

        if (CanQuit())
        {
            CreateButton(deathScreen.transform, "Quit",
                new Vector2(0f, -160f), QuitGame);
        }

        deathScreen.SetActive(false);
    }

    // =====================================================
    // Actions
    // =====================================================

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!Application.CanStreamedLevelBeLoaded(MenuSceneName))
        {
            Debug.LogError(
                "[Menu] '" + MenuSceneName + "' is not in the Build " +
                "Settings scene list. Run Tools > Foxy Adventure > " +
                "Build Polished Main Menu, or add it by hand.");
            return;
        }

        SceneManager.LoadScene(MenuSceneName);
    }

    void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Quitting does nothing in a browser build, so no button there.
    static bool CanQuit()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return false;
#else
        return true;
#endif
    }

    // Same look as the buttons GameManager makes.
    static GameObject CreateButton(
        Transform parent, string label, Vector2 position, UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(250f, 60f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.text = label;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return buttonObject;
    }
}
