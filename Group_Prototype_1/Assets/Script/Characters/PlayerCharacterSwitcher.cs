using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the "Third Person Player" prefab. Every character model is a
/// child of the player; when the level starts this turns on the one that
/// was picked in the main menu and turns the others off.
///
/// Runs before ThirdPersonMovement and PlayerHealth (execution order -100),
/// so they only ever see the active character.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerCharacterSwitcher : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Must match the id on the character card in the menu.")]
        public string id = "Fox";

        [Tooltip("The model object under the player for this character.")]
        public GameObject root;

        [Tooltip("Animator with speed / isGrounded / jump parameters. " +
                 "Leave empty for models animated by ProceduralCharacterAnimator.")]
        public Animator animator;

        [Header("Optional: different feel per character")]
        [Tooltip("Off = use the values already on ThirdPersonMovement.")]
        public bool overrideMovement = false;
        public float speed = 6f;
        public float runSpeed = 9f;
        public float jumpHeight = 4f;
    }

    public ThirdPersonMovement movement;

    public List<Entry> characters = new List<Entry>();

    [Tooltip("For testing a level directly in the editor without going " +
             "through the menu. Leave empty to use the menu choice.")]
    public string forceCharacterId = "";

    public string ActiveId { get; private set; }

    void Awake()
    {
        if (movement == null)
            movement = GetComponent<ThirdPersonMovement>();

        string wanted = string.IsNullOrEmpty(forceCharacterId)
            ? CharacterSelection.SelectedId
            : forceCharacterId;

        Apply(wanted);
    }

    public void Apply(string id)
    {
        Entry chosen = null;

        foreach (Entry entry in characters)
        {
            if (entry != null && entry.root != null && entry.id == id)
            {
                chosen = entry;
                break;
            }
        }

        // Unknown id (or that model was never added): use the first one.
        if (chosen == null)
        {
            foreach (Entry entry in characters)
            {
                if (entry != null && entry.root != null)
                {
                    chosen = entry;
                    break;
                }
            }
        }

        if (chosen == null)
            return;

        foreach (Entry entry in characters)
        {
            if (entry != null && entry.root != null)
                entry.root.SetActive(entry == chosen);
        }

        ActiveId = chosen.id;

        if (movement == null)
            return;

        movement.foxAnimator = chosen.animator;

        if (chosen.overrideMovement)
        {
            movement.speed = chosen.speed;
            movement.runSpeed = chosen.runSpeed;
            movement.jumpHeight = chosen.jumpHeight;
        }
    }
}
