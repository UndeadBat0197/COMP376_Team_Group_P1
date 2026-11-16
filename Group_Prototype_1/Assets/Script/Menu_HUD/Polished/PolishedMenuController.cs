using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the polished main menu.
///
/// Flow:  Main  ->  Choose character  ->  Select level  ->  (fade) level
///
/// Every button in the scene calls one of the public methods below from
/// its OnClick() list, so the wiring is visible in the Inspector.
/// </summary>
public class PolishedMenuController : MonoBehaviour
{
    public static PolishedMenuController Instance { get; private set; }

    [Header("Panels")]
    public CanvasGroup mainPanel;
    public CanvasGroup characterPanel;
    public CanvasGroup levelPanel;
    public CanvasGroup controlsPanel;

    [Tooltip("Full screen black image used to fade in and out.")]
    public CanvasGroup fader;

    [Header("Button focused when a panel opens (keyboard / gamepad)")]
    public Selectable mainFirst;
    public Selectable characterFirst;
    public Selectable levelFirst;
    public Selectable controlsFirst;

    [Header("Characters")]
    public List<CharacterCard> characterCards = new List<CharacterCard>();

    [Tooltip("Text that shows who you are playing as.")]
    public List<TMP_Text> playingAsLabels = new List<TMP_Text>();

    [Tooltip("The 3D preview stages. Switched on while the main or " +
             "character panel (which show them) is open.")]
    public GameObject previewsRoot;

    [Header("Transitions")]
    public float panelFadeTime = 0.26f;
    public float slideDistance = 90f;
    public float sceneFadeTime = 0.45f;

    [Header("Background cylinder (optional)")]
    public CylinderMenuRotation cylinder;
    public float spinBurstMultiplier = 10f;

    [Header("Levels")]
    [Tooltip("Skip the extra 'Start Game' screen inside the level, " +
             "since the player already pressed Play here.")]
    public bool skipInLevelStartScreen = true;

    [Header("Other")]
    [Tooltip("Hidden in WebGL builds, where quitting does nothing.")]
    public GameObject quitButton;

    public AudioSource audioSource;

    [Range(0f, 1f)]
    public float sfxVolume = 0.3f;

    private CanvasGroup current;
    private bool busy;
    private AudioClip hoverClip;
    private AudioClip clickClip;

    void Awake()
    {
        Instance = this;

        // We may arrive here from a paused level.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        hoverClip = MakeBlip("MenuHover", 880f, 990f, 0.045f);
        clickClip = MakeBlip("MenuClick", 520f, 880f, 0.10f);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null)
            quitButton.SetActive(false);
#endif

        SetPanel(mainPanel, true);
        SetPanel(characterPanel, false);
        SetPanel(levelPanel, false);
        SetPanel(controlsPanel, false);
        current = mainPanel;

        if (previewsRoot != null)
            previewsRoot.SetActive(true);

        // Make sure the saved choice is one that exists in this menu.
        string id = CharacterSelection.SelectedId;

        if (FindCard(id) == null && characterCards.Count > 0 &&
            characterCards[0] != null)
        {
            id = characterCards[0].characterId;
            CharacterSelection.SelectedId = id;
        }

        RefreshCharacterUI(id, false);

        Focus(mainFirst);
        StartCoroutine(IntroRoutine());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Back();
    }

    // =====================================================
    // Called by buttons
    // =====================================================

    public void ShowMain()
    {
        Go(mainPanel, mainFirst, -1);
    }

    public void ShowCharacters()
    {
        Go(characterPanel, characterFirst, current == levelPanel ? -1 : 1);
    }

    public void ShowLevels()
    {
        Go(levelPanel, levelFirst, 1);
    }

    public void ShowControls()
    {
        Go(controlsPanel, controlsFirst, 1);
    }

    public void Back()
    {
        if (current == levelPanel)
            ShowCharacters();
        else if (current == characterPanel || current == controlsPanel)
            ShowMain();
    }

    /// <summary>Clicking a character card calls this with its id.</summary>
    public void SelectCharacter(string id)
    {
        if (busy || FindCard(id) == null)
            return;

        bool changed = CharacterSelection.SelectedId != id;

        CharacterSelection.SelectedId = id;
        RefreshCharacterUI(id, true);

        if (changed)
            PlayClick();
    }

    public void LoadLevel(string sceneName)
    {
        if (busy)
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "[Menu] Scene '" + sceneName + "' is not in the Build " +
                "Settings scene list, so it cannot be loaded.");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName));
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =====================================================
    // Sounds (tiny generated blips, no audio files needed)
    // =====================================================

    public void PlayHover()
    {
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip, sfxVolume * 0.6f);
    }

    public void PlayClick()
    {
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip, sfxVolume);
    }

    static AudioClip MakeBlip(
        string clipName, float fromHz, float toHz, float seconds)
    {
        const int rate = 44100;
        int count = Mathf.Max(1, Mathf.RoundToInt(rate * seconds));
        float[] data = new float[count];
        float angle = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float hz = Mathf.Lerp(fromHz, toHz, t);

            angle += 2f * Mathf.PI * hz / rate;

            // Quick attack, smooth fade out, so there is no click.
            float envelope =
                Mathf.Clamp01(t * 30f) * (1f - t) * (1f - t);

            data[i] = Mathf.Sin(angle) * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // =====================================================
    // Internals
    // =====================================================

    CharacterCard FindCard(string id)
    {
        foreach (CharacterCard card in characterCards)
        {
            if (card != null && card.characterId == id)
                return card;
        }

        return null;
    }

    void RefreshCharacterUI(string id, bool celebrate)
    {
        string shownName = id;

        foreach (CharacterCard card in characterCards)
        {
            if (card == null)
                continue;

            bool selected = card.characterId == id;
            card.SetSelected(selected, selected && celebrate);

            if (selected && !string.IsNullOrEmpty(card.displayName))
                shownName = card.displayName;
        }

        foreach (TMP_Text label in playingAsLabels)
        {
            if (label != null)
            {
                label.text =
                    "Playing as  <color=#FF8A2B><b>" + shownName + "</b></color>";
            }
        }
    }

    void Go(CanvasGroup next, Selectable focus, int direction)
    {
        if (busy || next == null || next == current)
            return;

        StartCoroutine(SwitchRoutine(current, next, focus, direction));
    }

    IEnumerator SwitchRoutine(
        CanvasGroup from, CanvasGroup to, Selectable focus, int direction)
    {
        busy = true;

        float baseSpin = cylinder != null ? cylinder.normalSpeed : 0f;
        float half = Mathf.Max(0.01f, panelFadeTime * 0.5f);

        RectTransform fromRect = from != null
            ? from.transform as RectTransform
            : null;

        RectTransform toRect = to.transform as RectTransform;

        if (from != null)
        {
            from.interactable = false;
            from.blocksRaycasts = false;
        }

        // Old panel slides away and fades out.
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            float k = Smooth(t / half);

            if (from != null)
                from.alpha = 1f - k;

            if (fromRect != null)
            {
                fromRect.anchoredPosition =
                    new Vector2(-direction * slideDistance * k, 0f);
            }

            SpinBurst(baseSpin, 0.5f * t / half);
            yield return null;
        }

        if (from != null)
            SetPanel(from, false);

        if (previewsRoot != null)
            previewsRoot.SetActive(to == characterPanel || to == mainPanel);

        // New panel slides in from the other side.
        to.gameObject.SetActive(true);
        to.alpha = 0f;

        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            float k = Smooth(t / half);

            to.alpha = k;

            if (toRect != null)
            {
                toRect.anchoredPosition =
                    new Vector2(direction * slideDistance * (1f - k), 0f);
            }

            SpinBurst(baseSpin, 0.5f + 0.5f * t / half);
            yield return null;
        }

        SetPanel(to, true);

        if (cylinder != null)
            cylinder.currentSpeed = baseSpin;

        current = to;
        busy = false;

        Focus(focus);
    }

    IEnumerator IntroRoutine()
    {
        if (fader == null)
            yield break;

        fader.gameObject.SetActive(true);
        fader.blocksRaycasts = false;
        fader.alpha = 1f;

        float duration = Mathf.Max(0.01f, sceneFadeTime);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            fader.alpha = 1f - Smooth(t / duration);
            yield return null;
        }

        fader.alpha = 0f;
    }

    IEnumerator LoadRoutine(string sceneName)
    {
        busy = true;

        if (fader != null)
        {
            fader.gameObject.SetActive(true);
            fader.blocksRaycasts = true;

            float duration = Mathf.Max(0.01f, sceneFadeTime);

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                fader.alpha = Smooth(t / duration);
                yield return null;
            }

            fader.alpha = 1f;
        }

        if (skipInLevelStartScreen)
        {
            // GameManager reads this flag and starts the level right away.
            PlayerPrefs.SetInt("SkipStartMenu", 1);
            PlayerPrefs.Save();
        }

        SceneManager.LoadScene(sceneName);
    }

    void SpinBurst(float baseSpin, float progress01)
    {
        if (cylinder == null)
            return;

        float burst = Mathf.Pow(Mathf.Sin(progress01 * Mathf.PI), 2f);

        cylinder.currentSpeed = Mathf.Lerp(
            baseSpin, baseSpin * spinBurstMultiplier, burst);
    }

    static void SetPanel(CanvasGroup panel, bool visible)
    {
        if (panel == null)
            return;

        panel.gameObject.SetActive(visible);
        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;

        RectTransform rect = panel.transform as RectTransform;

        if (rect != null)
            rect.anchoredPosition = Vector2.zero;
    }

    static void Focus(Selectable selectable)
    {
        if (selectable != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                selectable.gameObject);
        }
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
