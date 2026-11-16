using UnityEngine;

/// <summary>
/// Remembers which character the player picked in the main menu.
/// Saved in PlayerPrefs, so it survives scene loads and restarts.
/// </summary>
public static class CharacterSelection
{
    public const string DefaultId = "Fox";

    private const string PrefsKey = "SelectedCharacter";

    public static string SelectedId
    {
        get { return PlayerPrefs.GetString(PrefsKey, DefaultId); }
        set
        {
            PlayerPrefs.SetString(PrefsKey, value);
            PlayerPrefs.Save();
        }
    }
}
