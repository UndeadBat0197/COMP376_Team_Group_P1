using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor tools (menu: Tools > Foxy Adventure) that set up
///
///   1. the extra playable character on the Third Person Player prefab
///   2. the polished main menu in the Main_Menu scene
///
/// Both create normal objects, components and assets that you can see
/// and edit afterwards in the Hierarchy / Inspector. Nothing is built at
/// runtime. Running a tool again rebuilds its own objects only.
/// </summary>
public static class FoxyMenuBuilder
{
    // ---------------- project paths ----------------
    private const string MenuScenePath = "Assets/Scenes/Main_Menu.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/Third Person Player.prefab";
    private const string CameraPrefabPath = "Assets/Prefabs/Player/Third Person Camera.prefab";
    private const string FoxPrefabPath = "Assets/Fox/Prefabs/Fox.prefab";
    private const string FoxControllerPath = "Assets/Fox/foxanimator.controller";
    private const string GenFolder = "Assets/UI/PolishedMenu";

    // Camera orbit rings as  radius, height  for Top, Center, Bottom.
    // Was 12/14, 18/5, 12/-1: far enough back for walls to block the view.
    // Change these and run "3. Fix Third Person Camera" to tune it.
    private static readonly float[] CloseOrbit =
    {
        6.5f, 7.5f,
        9f, 3f,
        6.5f, 0f
    };

    private const string RootName = "Polished_Menu";
    private const string OldCanvasName = "Main_Menu_Canvas";

    // ---------------- palette ----------------
    private static readonly Color Ink = Hex("#0C1813");
    private static readonly Color CardBg = Hex("#14261F");
    private static readonly Color CardHover = Hex("#27503A");
    private static readonly Color StageBg = Hex("#1F3B2D");
    private static readonly Color Chip = Hex("#2A4C3A");
    private static readonly Color Cream = Hex("#FFF3DA");
    private static readonly Color Muted = Hex("#B4C4B2");
    private static readonly Color Orange = Hex("#FF8A2B");
    private static readonly Color OrangeHot = Hex("#FFA95C");
    private static readonly Color Leaf = Hex("#7BD05A");
    private static readonly Color Yellow = Hex("#FFD23F");

    /// <summary>
    /// Extra playable characters. To add another one: put its model in
    /// its own folder, add a line here, and run the tools again.
    /// </summary>
    private class ExtraCharacter
    {
        public string id;
        public string displayName;
        public string tagline;
        public string folder;
        public Color accent;

        // Pairs of  material name, colour.  Models that are coloured by
        // material instead of by a texture get these colours.
        public string[] materialColors;

        // Turn the model so it looks along the player's forward
        // direction (degrees around the up axis). If the character
        // walks backwards, add or subtract 180.
        public float modelYaw;

        // Playback speed of the leg animation at walking and at
        // running speed. Higher = faster legs.
        public float walkAnimSpeed = 1f;
        public float runAnimSpeed = 1.6f;

        // Movement speed for this character (the fox uses 6 and 9).
        // 0 = same as the values on ThirdPersonMovement.
        public float moveSpeed;
        public float runSpeed;
    }

    private static readonly ExtraCharacter[] Extras =
    {
        new ExtraCharacter
        {
            id = "Cat",
            displayName = "Cat",
            tagline = "Sleek, quick and always lands on its feet.",
            folder = "Assets/Characters/Cat",
            accent = Hex("#FF9ECF"),
            modelYaw = 90f,
            walkAnimSpeed = 1.5f,
            runAnimSpeed = 2.5f,
            moveSpeed = 7.5f,
            runSpeed = 11f,
            materialColors = new[]
            {
                "Grey", "#55525E",
                "Pink", "#FF9ECF",
                "White", "#FAF6EE"
            }
        }
    };

    private class CharacterDef
    {
        public string id;
        public string displayName;
        public string tagline;
        public Color accent;
        public GameObject asset;
        public RuntimeAnimatorController controller;

        // True when the controller was made by this tool from the
        // model's idle / walk / run clips (it has no jump animation).
        public bool autoController;

        // Cartoon materials (two-tone shading + outline), one for each
        // material name the model uses.
        public Dictionary<string, Material> toonMaterials;

        public float modelYaw;
        public float moveSpeed;
        public float runSpeed;
    }

    private class LevelDef
    {
        public string scene;
        public string title;
        public string description;
    }

    // generated sprites
    private static Sprite roundSprite;
    private static Sprite ringSprite;
    private static Sprite glowSprite;
    private static Sprite gradientSprite;
    private static Sprite vignetteSprite;

    // =========================================================
    // Menu items
    // =========================================================

    [MenuItem("Tools/Foxy Adventure/1. Add Characters To Player Prefab")]
    public static void AddCharactersToPlayerMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Foxy Adventure", "Stop Play mode first.", "OK");
            return;
        }

        string message;
        AddCharactersToPlayer(out message);
        EditorUtility.DisplayDialog("Foxy Adventure", message, "OK");
    }

    [MenuItem("Tools/Foxy Adventure/2. Build Polished Main Menu")]
    public static void BuildMenuMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Foxy Adventure", "Stop Play mode first.", "OK");
            return;
        }

        string message;
        BuildMenu(out message);
        EditorUtility.DisplayDialog("Foxy Adventure", message, "OK");
    }

    [MenuItem("Tools/Foxy Adventure/3. Fix Third Person Camera")]
    public static void FixCameraMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Foxy Adventure", "Stop Play mode first.", "OK");
            return;
        }

        string message;
        FixThirdPersonCamera(out message);
        EditorUtility.DisplayDialog("Foxy Adventure", message, "OK");
    }

    [MenuItem("Tools/Foxy Adventure/Set Up Everything")]
    public static void SetUpEverything()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Foxy Adventure", "Stop Play mode first.", "OK");
            return;
        }

        string playerMessage;
        string menuMessage;

        string cameraMessage;

        AddCharactersToPlayer(out playerMessage);
        FixThirdPersonCamera(out cameraMessage);
        BuildMenu(out menuMessage);

        EditorUtility.DisplayDialog(
            "Foxy Adventure",
            "PLAYER\n" + playerMessage +
            "\n\nCAMERA\n" + cameraMessage +
            "\n\nMENU\n" + menuMessage,
            "OK");
    }

    // =========================================================
    // 1. Player prefab
    // =========================================================

    private static bool AddCharactersToPlayer(out string message)
    {
        if (!File.Exists(PlayerPrefabPath))
        {
            message = "Player prefab not found at:\n" + PlayerPrefabPath;
            return false;
        }

        // Find the models (and build their animator controllers) first,
        // before the prefab is opened.
        List<CharacterDef> extras = LoadExtras();

        if (extras.Count == 0)
        {
            message =
                "No extra character models found. Expected a model in:\n" +
                ExtraFolders();
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            ThirdPersonMovement movement =
                root.GetComponent<ThirdPersonMovement>();

            CharacterController controller =
                root.GetComponent<CharacterController>();

            if (movement == null || controller == null)
            {
                message =
                    "The player prefab has no ThirdPersonMovement / " +
                    "CharacterController on its root.";
                return false;
            }

            Transform fox = FindDeep(root.transform, "Fox");

            if (fox == null)
            {
                Animator anyAnimator =
                    root.GetComponentInChildren<Animator>(true);

                if (anyAnimator != null)
                    fox = anyAnimator.transform;
            }

            // New characters go next to the fox, wherever it lives in this
            // version of the prefab. Never under a switched-off object
            // (an old disabled "GFX" would hide them completely).
            Transform holder =
                fox != null && fox.parent != null ? fox.parent : root.transform;

            for (Transform t = holder; t != null && t != root.transform; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    holder = root.transform;
                    break;
                }
            }

            // ---- where are the feet, and how tall should it be? ----
            Vector3 scale = root.transform.lossyScale;

            float capsuleRadius =
                controller.radius *
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            float capsuleHeight = Mathf.Max(
                controller.height * Mathf.Abs(scale.y),
                capsuleRadius * 2f);

            Vector3 capsuleCentre =
                root.transform.TransformPoint(controller.center);

            float feetY =
                capsuleCentre.y - capsuleHeight * 0.5f - controller.skinWidth;

            // Extra characters are sized by their longest side.
            float targetSize = capsuleHeight * 0.6f;

            Bounds foxBounds;
            if (fox != null && TryGetBounds(fox.gameObject, out foxBounds) &&
                foxBounds.size.y > 0.05f)
            {
                // (The feet go at the bottom of the capsule, which is
                // also where ThirdPersonMovement puts the fox at start.)
                float foxLongest = Mathf.Max(
                    foxBounds.size.x,
                    Mathf.Max(foxBounds.size.y, foxBounds.size.z));

                // A little smaller than the fox.
                targetSize = Mathf.Clamp(
                    foxLongest * 0.85f,
                    capsuleHeight * 0.3f,
                    capsuleHeight * 1.2f);
            }

            Vector3 feet = new Vector3(
                capsuleCentre.x, feetY, capsuleCentre.z);

            // ---- switcher ----
            PlayerCharacterSwitcher switcher =
                root.GetComponent<PlayerCharacterSwitcher>();

            if (switcher == null)
                switcher = root.AddComponent<PlayerCharacterSwitcher>();

            switcher.movement = movement;
            switcher.characters = new List<PlayerCharacterSwitcher.Entry>();

            if (fox != null)
            {
                switcher.characters.Add(new PlayerCharacterSwitcher.Entry
                {
                    id = "Fox",
                    root = fox.gameObject,
                    animator = fox.GetComponentInChildren<Animator>(true)
                });
            }

            string added = "";

            foreach (CharacterDef def in extras)
            {
                // Rebuild: remove this character from a previous run.
                Transform old = FindDeep(root.transform, def.id);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);

                Transform rig;
                GameObject model;

                GameObject wrapper = BuildFitted(
                    def.asset, def.id, holder,
                    feet, targetSize, true, def.modelYaw,
                    out rig, out model);

                if (wrapper == null)
                    continue;

                ApplyToonMaterial(model, def);

                Animator animator = PrepareAnimation(wrapper, rig, model, def);

                // Hidden by default so the prefab looks the same as before
                // in the editor; PlayerCharacterSwitcher turns it on in game.
                wrapper.SetActive(false);

                PlayerCharacterSwitcher.Entry entry =
                    new PlayerCharacterSwitcher.Entry
                    {
                        id = def.id,
                        root = wrapper,
                        animator = animator
                    };

                if (def.moveSpeed > 0f && def.runSpeed > 0f)
                {
                    entry.overrideMovement = true;
                    entry.speed = def.moveSpeed;
                    entry.runSpeed = def.runSpeed;
                    entry.jumpHeight = movement.jumpHeight;
                }

                switcher.characters.Add(entry);

                added += (added.Length > 0 ? ", " : "") + def.displayName;
            }

            if (added.Length == 0)
            {
                message = "The models found have no visible meshes.";
                return false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);

            message =
                "Added to the player prefab next to the fox: " + added + ".\n" +
                "PlayerCharacterSwitcher on the prefab root lists them.\n\n" +
                "If one faces the wrong way or is the wrong size, open the " +
                "prefab and rotate / scale  (name) > Rig > Fit.";
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Sets up how a character instance is animated and returns the
    /// Animator to hand to ThirdPersonMovement (or null).
    ///
    ///  - Controller with its own in-air state (the fox): Animator only.
    ///  - Controller built here from idle / walk / run clips: Animator
    ///    for the legs, plus the procedural stretch and squash for jumps.
    ///  - No clips at all: fully procedural hop, lean and squash.
    /// </summary>
    private static Animator PrepareAnimation(
        GameObject wrapper, Transform rig, GameObject model, CharacterDef def)
    {
        Animator animator = model.GetComponentInChildren<Animator>(true);

        if (def.controller != null)
        {
            if (animator == null)
                animator = model.AddComponent<Animator>();

            animator.runtimeAnimatorController = def.controller;
            animator.applyRootMotion = false;
            animator.enabled = true;

            if (def.autoController)
            {
                ProceduralCharacterAnimator jumpOnly =
                    wrapper.AddComponent<ProceduralCharacterAnimator>();

                jumpOnly.target = rig;
                jumpOnly.hopHeight = 0f;
                jumpOnly.leanAngle = 0f;
                jumpOnly.waddleAngle = 0f;
                jumpOnly.strideSquash = 0f;
                jumpOnly.breatheAmount = 0f;
            }

            return animator;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.applyRootMotion = false;
            return animator;
        }

        if (animator != null)
            animator.enabled = false;

        ProceduralCharacterAnimator procedural =
            wrapper.AddComponent<ProceduralCharacterAnimator>();

        procedural.target = rig;
        return null;
    }

    private static string ExtraFolders()
    {
        string text = "";

        foreach (ExtraCharacter extra in Extras)
            text += extra.folder + "\n";

        return text;
    }

    /// <summary>Every extra character whose model is in the project.</summary>
    private static List<CharacterDef> LoadExtras()
    {
        List<CharacterDef> found = new List<CharacterDef>();

        foreach (ExtraCharacter extra in Extras)
        {
            GameObject asset = FindModel(extra.folder);

            if (asset == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(asset);

            RuntimeAnimatorController controller =
                GetLocomotionController(assetPath, extra);

            // Building the controller re-imports the model.
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (asset == null)
                continue;

            found.Add(new CharacterDef
            {
                id = extra.id,
                displayName = extra.displayName,
                tagline = extra.tagline,
                accent = extra.accent,
                asset = asset,
                controller = controller,
                autoController = controller != null,
                toonMaterials = GetToonMaterials(asset, extra),
                modelYaw = extra.modelYaw,
                moveSpeed = extra.moveSpeed,
                runSpeed = extra.runSpeed
            });
        }

        return found;
    }

    /// <summary>
    /// If the model file has idle / walk / run clips, makes (once) an
    /// Animator Controller next to it: one blend tree driven by the same
    /// "speed" parameter ThirdPersonMovement already sets for the fox
    /// (0 = idle, 0.5 = walk, 1 = run). Returns null if there are no clips.
    /// </summary>
    private static RuntimeAnimatorController GetLocomotionController(
        string modelPath, ExtraCharacter extra)
    {
        string controllerPath =
            extra.folder + "/" + extra.id + "_Locomotion.controller";

        AnimatorController existing =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (existing != null)
        {
            // Still built from this model's clips? Then keep it. If the
            // model file was swapped for another one, build it again.
            AnimationClip[] used = existing.animationClips;
            bool valid = used != null && used.Length > 0;

            if (valid)
            {
                foreach (AnimationClip clip in used)
                {
                    if (clip == null ||
                        AssetDatabase.GetAssetPath(clip) != modelPath)
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (valid)
            {
                ApplyLegSpeeds(existing, extra);
                return existing;
            }

            AssetDatabase.DeleteAsset(controllerPath);
        }

        ModelImporter importer =
            AssetImporter.GetAtPath(modelPath) as ModelImporter;

        if (importer == null)
            return null;

        // Idle, walk and run must loop.
        ModelImporterClipAnimation[] clipSettings = importer.clipAnimations;

        if (clipSettings == null || clipSettings.Length == 0)
            clipSettings = importer.defaultClipAnimations;

        if (clipSettings != null && clipSettings.Length > 0)
        {
            bool changed = false;

            foreach (ModelImporterClipAnimation clip in clipSettings)
            {
                string lower = clip.name.ToLowerInvariant();

                bool shouldLoop =
                    lower.Contains("idle") || lower.Contains("walk") ||
                    lower.Contains("run");

                if (shouldLoop && !clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }
            }

            if (changed || importer.clipAnimations == null ||
                importer.clipAnimations.Length == 0)
            {
                importer.clipAnimations = clipSettings;
                importer.SaveAndReimport();
            }
        }

        AnimationClip idle = null;
        AnimationClip walk = null;
        AnimationClip run = null;

        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            AnimationClip clip = sub as AnimationClip;

            if (clip == null || clip.name.StartsWith("__preview__"))
                continue;

            string lower = clip.name.ToLowerInvariant();

            if (idle == null && lower.Contains("idle")) idle = clip;
            else if (walk == null && lower.Contains("walk")) walk = clip;
            else if (run == null && lower.Contains("run")) run = clip;
        }

        if (idle == null || (walk == null && run == null))
            return null;

        // A model with only a walk uses it, played faster, as its run.
        if (walk == null) walk = run;
        if (run == null) run = walk;

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("speed", AnimatorControllerParameterType.Float);

        BlendTree tree;
        controller.CreateBlendTreeInController("Locomotion", out tree);

        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.5f);
        tree.AddChild(run, 1f);

        ApplyLegSpeeds(controller, extra);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    /// <summary>
    /// Makes (once) cartoon materials for the character with the
    /// project's own "Toon/Lit Outline" shader, the same one the tree
    /// level uses: flat two-tone shading, a soft rim light and a dark
    /// outline. One material per material name on the model, keeping its
    /// texture if it has one. Tweak the look afterwards on the material
    /// assets (they are not overwritten when you run this again).
    /// </summary>
    private static Dictionary<string, Material> GetToonMaterials(
        GameObject asset, ExtraCharacter extra)
    {
        Dictionary<string, Material> result =
            new Dictionary<string, Material>();

        Shader shader = Shader.Find("Toon/Lit Outline");
        if (shader == null) shader = Shader.Find("Toon/Lit");
        if (shader == null) return result;

        foreach (Renderer r in asset.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material source in r.sharedMaterials)
            {
                if (source == null || result.ContainsKey(source.name))
                    continue;

                string safeName = source.name
                    .Replace("/", "_").Replace("\\", "_").Replace(":", "_");

                string path =
                    extra.folder + "/" + extra.id + "_Toon_" + safeName + ".mat";

                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(shader);
                    material.name = extra.id + "_Toon_" + safeName;

                    Color baseColor = Color.white;

                    if (source.mainTexture != null)
                        material.SetTexture("_BaseMap", source.mainTexture);
                    else if (source.HasProperty("_BaseColor"))
                        baseColor = source.GetColor("_BaseColor");
                    else if (source.HasProperty("_Color"))
                        baseColor = source.GetColor("_Color");

                    // Colours listed for this character win.
                    if (extra.materialColors != null)
                    {
                        string lower = source.name.ToLowerInvariant();

                        for (int i = 0; i + 1 < extra.materialColors.Length; i += 2)
                        {
                            if (lower.Contains(
                                    extra.materialColors[i].ToLowerInvariant()))
                            {
                                baseColor = Hex(extra.materialColors[i + 1]);
                                break;
                            }
                        }
                    }

                    baseColor.a = 1f;
                    material.SetColor("_BaseColor", baseColor);

                    // Cool, slightly purple shadows instead of grey keep
                    // the colours bright and cartoony.
                    material.SetColor(
                        "_ShadeColor", new Color(0.58f, 0.55f, 0.80f, 1f));
                    material.SetFloat("_RampThreshold", 0.45f);
                    material.SetFloat("_RampSmooth", 0.02f);
                    material.SetFloat("_AmbientStrength", 0.45f);

                    material.SetColor("_RimColor", Color.white);
                    material.SetFloat("_RimPower", 0.65f);
                    material.SetFloat("_RimStrength", 0.25f);

                    material.SetColor("_OutlineColor", Hex("#1B1430"));
                    material.SetFloat("_OutlineWidth", 2f);

                    AssetDatabase.CreateAsset(material, path);
                }

                result[source.name] = material;
            }
        }

        return result;
    }

    private static void ApplyToonMaterial(GameObject model, CharacterDef def)
    {
        if (def.toonMaterials == null || def.toonMaterials.Count == 0)
            return;

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer ||
                r is LineRenderer)
                continue;

            Material[] slots = r.sharedMaterials;

            for (int i = 0; i < slots.Length; i++)
            {
                Material toon;

                if (slots[i] != null &&
                    def.toonMaterials.TryGetValue(slots[i].name, out toon))
                {
                    slots[i] = toon;
                }
            }

            r.sharedMaterials = slots;
        }
    }

    /// <summary>
    /// Sets how fast the walk and run animations play in the character's
    /// blend tree (children are: 0 idle, 1 walk, 2 run). Applied every
    /// time the tools run, from the numbers in the Extras list.
    /// </summary>
    private static void ApplyLegSpeeds(
        AnimatorController controller, ExtraCharacter extra)
    {
        if (controller == null || controller.layers.Length == 0)
            return;

        AnimatorState state =
            controller.layers[0].stateMachine.defaultState;

        BlendTree tree = state != null ? state.motion as BlendTree : null;

        if (tree == null)
            return;

        ChildMotion[] children = tree.children;

        if (children.Length < 3)
            return;

        children[1].timeScale = extra.walkAnimSpeed;
        children[2].timeScale = extra.runAnimSpeed;
        tree.children = children;

        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
    }

    private static GameObject FindModel(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return null;

        // A prefab you made yourself wins over the raw model file.
        foreach (string filter in new[] { "t:Prefab", "t:Model" })
        {
            GameObject best = null;
            int bestCount = 0;

            foreach (string guid in
                     AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (asset == null)
                    continue;

                int count =
                    asset.GetComponentsInChildren<Renderer>(true).Length;

                if (count > bestCount)
                {
                    best = asset;
                    bestCount = count;
                }
            }

            if (best != null)
                return best;
        }

        return null;
    }

    /// <summary>
    /// Creates   name > Rig > Fit > Model   under parent.
    ///  - "name" sits at the feet point and cancels any stretched scale
    ///    on the parents, so the model is not deformed.
    ///  - "Rig" is what the procedural animation moves.
    ///  - "Fit" scales the model to the wanted size and puts its feet
    ///    on the pivot. "Model" is the untouched model instance.
    /// </summary>
    private static GameObject BuildFitted(
        GameObject asset, string name, Transform parent,
        Vector3 feetWorld, float targetSize, bool fitLargestSide,
        float modelYaw, out Transform rig, out GameObject model)
    {
        GameObject wrapper = new GameObject(name);
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.position = feetWorld;
        wrapper.transform.rotation = parent.rotation;

        Vector3 ps = parent.lossyScale;
        wrapper.transform.localScale = new Vector3(
            SafeInverse(ps.x), SafeInverse(ps.y), SafeInverse(ps.z));

        GameObject rigObject = new GameObject("Rig");
        rigObject.transform.SetParent(wrapper.transform, false);
        rig = rigObject.transform;

        // "Fit" carries the size and offset, so the model instance itself
        // stays exactly as imported (its own animation cannot undo the fit).
        GameObject fitObject = new GameObject("Fit");
        fitObject.transform.SetParent(rig, false);
        Transform fit = fitObject.transform;

        // Turn first, measure after, so the feet still end up centred.
        fit.localRotation = Quaternion.Euler(0f, modelYaw, 0f);

        model = (GameObject)PrefabUtility.InstantiatePrefab(asset, fit);
        model.name = "Model";

        // Downloaded models often bring their own camera and lights.
        foreach (Camera c in model.GetComponentsInChildren<Camera>(true))
            c.enabled = false;

        foreach (Light l in model.GetComponentsInChildren<Light>(true))
            l.enabled = false;

        foreach (AudioListener a in
                 model.GetComponentsInChildren<AudioListener>(true))
            a.enabled = false;

        Bounds bounds;
        if (!TryGetBounds(model, out bounds))
        {
            Object.DestroyImmediate(wrapper);
            rig = null;
            model = null;
            return null;
        }

        float size = fitLargestSide
            ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
            : bounds.size.y;

        if (size > 0.0001f)
            fit.localScale = Vector3.one * (targetSize / size);

        if (TryGetBounds(model, out bounds))
        {
            fit.position += new Vector3(
                feetWorld.x - bounds.center.x,
                feetWorld.y - bounds.min.y,
                feetWorld.z - bounds.center.z);
        }

        return wrapper;
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer ||
                r is LineRenderer)
                continue;

            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t != parent && t.name == name)
                return t;
        }

        return null;
    }

    // =========================================================
    // Camera
    // =========================================================

    /// <summary>
    /// The Cinemachine "Decollider" on the Third Person Camera prefab
    /// pushes the camera out of walls. With a wide camera radius and
    /// without "Use Follow Target" it can end up pulled right into the
    /// player inside the tree, which looks first-person. This sets the
    /// values the main branch uses. Edited through SerializedObject so
    /// the result is ordinary Inspector values on the prefab.
    /// </summary>
    private static bool FixThirdPersonCamera(out string message)
    {
        if (!File.Exists(CameraPrefabPath))
        {
            message = "Camera prefab not found at:\n" + CameraPrefabPath;
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CameraPrefabPath);

        try
        {
            MonoBehaviour decollider = null;

            foreach (MonoBehaviour behaviour in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null &&
                    behaviour.GetType().Name == "CinemachineDecollider")
                {
                    decollider = behaviour;
                    break;
                }
            }

            if (decollider == null)
            {
                message =
                    "No CinemachineDecollider on the camera prefab, " +
                    "nothing to change.";
                return false;
            }

            SerializedObject so = new SerializedObject(decollider);

            SerializedProperty radius = so.FindProperty("CameraRadius");
            SerializedProperty useFollow =
                so.FindProperty("Decollision.UseFollowTarget.Enabled");
            SerializedProperty damping =
                so.FindProperty("Decollision.Damping");

            if (radius != null) radius.floatValue = 0.15f;
            if (useFollow != null) useFollow.boolValue = true;
            if (damping != null) damping.floatValue = 0f;

            so.ApplyModifiedPropertiesWithoutUndo();

            // Bring the orbit closer to the player. From far back, walls
            // and the next section of the level get between the camera
            // and the character.
            string orbitNote = "";

            foreach (MonoBehaviour behaviour in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null ||
                    behaviour.GetType().Name != "CinemachineOrbitalFollow")
                    continue;

                SerializedObject orbit = new SerializedObject(behaviour);

                SetFloat(orbit, "Orbits.Top.Radius", CloseOrbit[0]);
                SetFloat(orbit, "Orbits.Top.Height", CloseOrbit[1]);
                SetFloat(orbit, "Orbits.Center.Radius", CloseOrbit[2]);
                SetFloat(orbit, "Orbits.Center.Height", CloseOrbit[3]);
                SetFloat(orbit, "Orbits.Bottom.Radius", CloseOrbit[4]);
                SetFloat(orbit, "Orbits.Bottom.Height", CloseOrbit[5]);

                orbit.ApplyModifiedPropertiesWithoutUndo();
                orbitNote = " Orbit moved closer to the player.";
                break;
            }

            PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);

            message =
                "Third Person Camera prefab: Decollider camera radius " +
                "0.15, Use Follow Target on, damping 0." + orbitNote;
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetFloat(
        SerializedObject target, string propertyPath, float value)
    {
        SerializedProperty property = target.FindProperty(propertyPath);

        if (property != null &&
            property.propertyType == SerializedPropertyType.Float)
        {
            property.floatValue = value;
        }
    }

    // =========================================================
    // 2. Main menu
    // =========================================================

    private static bool BuildMenu(out string message)
    {
        // ---- open the menu scene ----
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != MenuScenePath)
        {
            if (!File.Exists(MenuScenePath))
            {
                message = "Menu scene not found at:\n" + MenuScenePath;
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                message = "Cancelled.";
                return false;
            }

            scene = EditorSceneManager.OpenScene(
                MenuScenePath, OpenSceneMode.Single);
        }

        // ---- characters that can be shown ----
        List<CharacterDef> characters = new List<CharacterDef>();

        GameObject foxAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefabPath);

        if (foxAsset != null)
        {
            characters.Add(new CharacterDef
            {
                id = "Fox",
                displayName = "Fox",
                tagline = "Quick, clever and sure-footed.",
                accent = Orange,
                asset = foxAsset,
                controller =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        FoxControllerPath)
            });
        }

        List<CharacterDef> extras = LoadExtras();
        characters.AddRange(extras);

        if (characters.Count == 0)
        {
            message = "No character models found (expected " +
                      FoxPrefabPath + ").";
            return false;
        }

        // ---- levels = enabled scenes in Build Settings ----
        List<LevelDef> levels = new List<LevelDef>();

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || s.path == MenuScenePath)
                continue;

            levels.Add(DescribeLevel(
                Path.GetFileNameWithoutExtension(s.path)));
        }

        // Tree level first.
        levels.Sort((a, b) =>
            (a.scene == "Tree_Level" ? 0 : 1).CompareTo(
                b.scene == "Tree_Level" ? 0 : 1));

        EnsureMenuSceneInBuild();

        // ---- assets ----
        EnsureFolder();
        GenerateSprites();

        // ---- scene clean-up ----
        GameObject oldCanvas = FindInScene(scene, OldCanvasName);

        if (oldCanvas != null && oldCanvas.activeSelf)
        {
            Undo.RecordObject(oldCanvas, "Hide old menu canvas");
            oldCanvas.SetActive(false);
        }

        GameObject previous = FindInScene(scene, RootName);
        if (previous != null)
            Object.DestroyImmediate(previous);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build polished menu");

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.transform.SetParent(root.transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ---- controller ----
        GameObject controllerObject = new GameObject("MenuController");
        controllerObject.transform.SetParent(root.transform, false);

        PolishedMenuController menu =
            controllerObject.AddComponent<PolishedMenuController>();

        AudioSource audio = controllerObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        menu.audioSource = audio;

        menu.cylinder =
            Object.FindFirstObjectByType<CylinderMenuRotation>();

        // ---- 3D previews ----
        GameObject previews = new GameObject("Character_Previews");
        previews.transform.SetParent(root.transform, false);
        previews.transform.position = new Vector3(0f, -3000f, 0f);
        menu.previewsRoot = previews;

        Material pedestal = GetPedestalMaterial();

        List<CharacterPreview> stages = new List<CharacterPreview>();
        List<RenderTexture> textures = new List<RenderTexture>();

        for (int i = 0; i < characters.Count; i++)
        {
            RenderTexture rt;
            stages.Add(BuildStage(
                previews.transform, characters[i], i, pedestal, out rt));
            textures.Add(rt);
        }

        // ---- canvas ----
        GameObject canvasObject = new GameObject(
            "Menu_Canvas", typeof(RectTransform));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(root.transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Transform canvasT = canvasObject.transform;

        BuildBackdrop(canvasT);

        Button play;
        menu.mainPanel = BuildMainPanel(
            canvasT, menu, characters, textures, out play);
        menu.mainFirst = play;

        Button continueButton;
        menu.characterPanel = BuildCharacterPanel(
            canvasT, menu, characters, stages, textures,
            out continueButton);
        menu.characterFirst = continueButton;

        Button firstLevel;
        menu.levelPanel = BuildLevelPanel(
            canvasT, menu, levels, out firstLevel);
        menu.levelFirst = firstLevel;

        Button controlsBack;
        menu.controlsPanel = BuildControlsPanel(
            canvasT, menu, out controlsBack);
        menu.controlsFirst = controlsBack;

        // ---- fader (on top of everything) ----
        RectTransform faderRect = UI("Fader", canvasT);
        Fill(faderRect);
        Img(faderRect, null, Color.black, false, true);

        CanvasGroup fader = faderRect.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 0f;
        fader.blocksRaycasts = false;
        fader.interactable = false;
        menu.fader = fader;

        // Only the main panel is visible in the editor, like in game.
        menu.characterPanel.gameObject.SetActive(false);
        menu.levelPanel.gameObject.SetActive(false);
        menu.controlsPanel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        message =
            "Menu built in the Hierarchy under '" + RootName + "' with " +
            characters.Count + " character(s) and " + levels.Count +
            " level(s).\n" +
            (oldCanvas != null
                ? "The old '" + OldCanvasName +
                  "' was switched off, not deleted.\n"
                : "") +
            (extras.Count == 0
                ? "\nNo extra character model was found, so only the " +
                  "fox card was made.\n"
                : "") +
            "\nCheck it, then save the scene (Ctrl+S).";
        return true;
    }

    /// <summary>
    /// The menu has to be in the Build Settings scene list (and first),
    /// otherwise the game cannot start on it or come back to it from a
    /// level. Levels keep their order.
    /// </summary>
    private static void EnsureMenuSceneInBuild()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        if (scenes.Count > 0 && scenes[0].path == MenuScenePath &&
            scenes[0].enabled)
            return;

        scenes.RemoveAll(scene => scene.path == MenuScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static LevelDef DescribeLevel(string sceneName)
    {
        LevelDef level = new LevelDef { scene = sceneName };

        if (sceneName == "Tree_Level")
        {
            level.title = "The Trunk";
            level.description =
                "Rotate the tree, dodge the hazards and reach the acorn.";
        }
        else if (sceneName == "Rotation_test_scene")
        {
            level.title = "Rotation Lab";
            level.description =
                "Spin the cylinder with Q and R and grab every apple.";
        }
        else
        {
            level.title = ObjectNames.NicifyVariableName(
                sceneName.Replace("_", " "));
            level.description = "";
        }

        return level;
    }

    // ---------------------------------------------------------
    // Backdrop
    // ---------------------------------------------------------

    private static void BuildBackdrop(Transform canvas)
    {
        RectTransform backdrop = UI("Backdrop", canvas);
        Fill(backdrop);

        RectTransform vignette = UI("Vignette", backdrop);
        Fill(vignette);
        Img(vignette, vignetteSprite, Ink, false, false);

        // Dark fade on the left so the text is readable over the 3D scene.
        RectTransform shade = UI("Left_Shade", backdrop);
        shade.anchorMin = new Vector2(0f, 0f);
        shade.anchorMax = new Vector2(0.72f, 1f);
        shade.offsetMin = Vector2.zero;
        shade.offsetMax = Vector2.zero;
        Img(shade, gradientSprite, WithAlpha(Ink, 0.94f), false, false);
    }

    // ---------------------------------------------------------
    // Main panel
    // ---------------------------------------------------------

    private static CanvasGroup BuildMainPanel(
        Transform canvas, PolishedMenuController menu,
        List<CharacterDef> characters, List<RenderTexture> textures,
        out Button play)
    {
        RectTransform panel = UI("Main_Panel", canvas);
        Fill(panel);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

        // ---- title ----
        RectTransform title = UI("Title", panel);
        At(title, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(130f, -110f), new Vector2(1000f, 340f));
        title.gameObject.AddComponent<UIFloat>();

        TextMeshProUGUI foxy = Txt(title, "Title_Top", "FOXY", 176f,
            Orange, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        At(foxy.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1000f, 190f));
        foxy.characterSpacing = 4f;
        ApplyShadow(foxy);

        TextMeshProUGUI adventure = Txt(title, "Title_Bottom", "ADVENTURE",
            80f, Cream, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        At(adventure.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(6f, -168f), new Vector2(1000f, 100f));
        adventure.characterSpacing = 14f;
        ApplyShadow(adventure);

        RectTransform bar = UI("Accent_Bar", title);
        At(bar, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -268f), new Vector2(120f, 8f));
        Img(bar, roundSprite, Leaf, true, false).pixelsPerUnitMultiplier = 6f;

        TextMeshProUGUI tagline = Txt(title, "Tagline",
            "Twist the trunk. Dodge the hazards. Reach the acorn.",
            28f, Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        At(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -292f), new Vector2(1000f, 44f));

        // ---- buttons ----
        RectTransform column = UI("Buttons", panel);
        At(column, new Vector2(0f, 0.5f), new Vector2(0f, 1f),
            new Vector2(130f, -50f), new Vector2(460f, 300f));

        VerticalLayoutGroup layout =
            column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Vector2 size = new Vector2(440f, 84f);

        play = MakeButton(column, "Play_Button", "PLAY", size, true, true);
        UnityEventTools.AddPersistentListener(
            play.onClick, new UnityAction(menu.ShowCharacters));

        Button controls = MakeButton(
            column, "Controls_Button", "CONTROLS", size, false, true);
        UnityEventTools.AddPersistentListener(
            controls.onClick, new UnityAction(menu.ShowControls));

        Button quit = MakeButton(
            column, "Quit_Button", "QUIT", size, false, true);
        UnityEventTools.AddPersistentListener(
            quit.onClick, new UnityAction(menu.Quit));
        menu.quitButton = quit.gameObject;

        // ---- character portraits (live 3D, like tilted photos) ----
        RectTransform portraits = UI("Portraits", panel);
        At(portraits, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-90f, -10f), new Vector2(820f, 640f));

        int shown = Mathf.Min(characters.Count, 3);

        for (int i = 0; i < shown; i++)
        {
            // First character big and in front on the left, the others
            // smaller, overlapping, each tilted the other way.
            bool main = i == 0;
            float cardSize = main ? 400f : 320f;

            Vector2 position = main
                ? new Vector2(-470f, 60f)
                : new Vector2(-150f - (i - 1) * 60f, -110f + (i - 1) * 300f);

            float tilt = main ? -6f : (i % 2 == 1 ? 7f : -4f);

            RectTransform card = UI("Portrait_" + characters[i].id, portraits);
            At(card, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(cardSize, cardSize + 70f));
            card.localRotation = Quaternion.Euler(0f, 0f, tilt);

            UIFloat drift = card.gameObject.AddComponent<UIFloat>();
            drift.amplitude = 6f;
            drift.speed = 0.9f + i * 0.25f;
            drift.tiltDegrees = 1.2f;

            RectTransform shadow = UI("Shadow", card);
            Fill(shadow, -30f, -42f, -30f, -18f);
            Img(shadow, glowSprite, new Color(0f, 0f, 0f, 0.45f), true, false);

            RectTransform paper = UI("Paper", card);
            Fill(paper);
            Img(paper, roundSprite, Cream, true, false);

            RectTransform photo = UI("Photo", card);
            Fill(photo, 18f, 88f, 18f, 18f);
            Img(photo, roundSprite, StageBg, true, false)
                .pixelsPerUnitMultiplier = 1.6f;

            RectTransform view = UI("Preview", photo);
            Fill(view, 8f, 8f, 8f, 8f);
            RawImage raw = view.gameObject.AddComponent<RawImage>();
            raw.texture = textures[i];
            raw.raycastTarget = false;

            TextMeshProUGUI caption = Txt(card, "Caption",
                characters[i].displayName.ToUpperInvariant(),
                main ? 40f : 34f, Ink, FontStyles.Bold,
                TextAlignmentOptions.Center);
            At(caption.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(cardSize - 36f, 52f));
            caption.characterSpacing = 8f;

            RectTransform dot = UI("Accent", card);
            At(dot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(56f, 6f));
            Img(dot, roundSprite, characters[i].accent, true, false)
                .pixelsPerUnitMultiplier = 8f;
        }

        // The big portrait is drawn last so it sits on top.
        if (shown > 0)
            portraits.GetChild(0).SetAsLastSibling();

        // ---- footer ----
        TextMeshProUGUI playingAs = Txt(panel, "Playing_As",
            "Playing as  Fox", 26f, Cream, FontStyles.Normal,
            TextAlignmentOptions.BottomLeft);
        At(playingAs.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(134f, 64f), new Vector2(800f, 40f));
        menu.playingAsLabels.Add(playingAs);

        TextMeshProUGUI credit = Txt(panel, "Credit",
            "COMP 376  |  Team Prototype 1", 20f, Muted, FontStyles.Normal,
            TextAlignmentOptions.BottomRight);
        At(credit.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-60f, 50f), new Vector2(700f, 36f));

        return group;
    }

    // ---------------------------------------------------------
    // Character panel
    // ---------------------------------------------------------

    private static CanvasGroup BuildCharacterPanel(
        Transform canvas, PolishedMenuController menu,
        List<CharacterDef> characters, List<CharacterPreview> stages,
        List<RenderTexture> textures, out Button continueButton)
    {
        RectTransform panel = UI("Character_Panel", canvas);
        Fill(panel);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

        Dim(panel);
        Heading(panel, "CHOOSE YOUR CHARACTER",
            "Pick who you play as. You can change it here any time.");

        RectTransform row = UI("Cards", panel);
        At(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -10f), new Vector2(1600f, 600f));
        Row(row, 70f);

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterDef def = characters[i];

            RectTransform card = UI("Card_" + def.id, row);
            card.sizeDelta = new Vector2(420f, 540f);

            RectTransform glow = UI("Glow", card);
            Fill(glow, -34f, -34f, -34f, -34f);
            Image glowImage =
                Img(glow, glowSprite, WithAlpha(def.accent, 0f), true, false);

            RectTransform body = UI("Body", card);
            Fill(body);
            Image bodyImage = Img(body, roundSprite, CardBg, true, true);

            RectTransform stage = UI("Stage", card);
            At(stage, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(380f, 360f));
            Img(stage, roundSprite, StageBg, true, false);

            RectTransform view = UI("Preview", stage);
            Fill(view, 12f, 12f, 12f, 12f);
            RawImage raw = view.gameObject.AddComponent<RawImage>();
            raw.texture = textures[i];
            raw.raycastTarget = false;

            TextMeshProUGUI nameText = Txt(card, "Name", def.displayName,
                44f, Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            At(nameText.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 82f), new Vector2(380f, 58f));

            TextMeshProUGUI tag = Txt(card, "Tagline", def.tagline,
                22f, Muted, FontStyles.Normal, TextAlignmentOptions.Center);
            At(tag.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(380f, 40f));

            RectTransform frame = UI("Frame", card);
            Fill(frame);
            Image frameImage = Img(
                frame, ringSprite, new Color(1f, 1f, 1f, 0.12f), true, false);

            RectTransform badge = UI("Selected_Badge", card);
            At(badge, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-34f, -34f), new Vector2(150f, 40f));
            Img(badge, roundSprite, def.accent, true, false);

            TextMeshProUGUI badgeText = Txt(badge, "Label", "SELECTED",
                18f, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(badgeText.rectTransform);
            badgeText.characterSpacing = 6f;

            Button button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bodyImage;
            UnityEventTools.AddStringPersistentListener(
                button.onClick,
                new UnityAction<string>(menu.SelectCharacter), def.id);

            CharacterCard cardScript =
                card.gameObject.AddComponent<CharacterCard>();
            cardScript.characterId = def.id;
            cardScript.displayName = def.displayName;
            cardScript.frame = frameImage;
            cardScript.glow = glowImage;
            cardScript.selectedBadge = badge.gameObject;
            cardScript.preview = stages[i];
            cardScript.selectedFrame = def.accent;

            badge.gameObject.SetActive(i == 0);
            menu.characterCards.Add(cardScript);
        }

        RectTransform buttons = UI("Buttons", panel);
        At(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 64f), new Vector2(800f, 80f));
        Row(buttons, 24f);

        Button back = MakeButton(buttons, "Back_Button", "BACK",
            new Vector2(240f, 76f), false, false);
        UnityEventTools.AddPersistentListener(
            back.onClick, new UnityAction(menu.ShowMain));

        continueButton = MakeButton(buttons, "Continue_Button", "CONTINUE",
            new Vector2(340f, 76f), true, false);
        UnityEventTools.AddPersistentListener(
            continueButton.onClick, new UnityAction(menu.ShowLevels));

        return group;
    }

    // ---------------------------------------------------------
    // Level panel
    // ---------------------------------------------------------

    private static CanvasGroup BuildLevelPanel(
        Transform canvas, PolishedMenuController menu,
        List<LevelDef> levels, out Button first)
    {
        RectTransform panel = UI("Level_Panel", canvas);
        Fill(panel);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

        Dim(panel);
        TextMeshProUGUI sub = Heading(panel, "SELECT LEVEL", "Playing as  Fox");
        menu.playingAsLabels.Add(sub);

        RectTransform row = UI("Levels", panel);
        At(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(1700f, 340f));
        Row(row, 50f);

        first = null;

        for (int i = 0; i < levels.Count; i++)
        {
            LevelDef level = levels[i];

            RectTransform card = UI("Level_" + level.scene, row);
            card.sizeDelta = new Vector2(480f, 300f);
            Image body = Img(card, roundSprite, CardBg, true, true);

            TextMeshProUGUI number = Txt(card, "Number",
                (i + 1).ToString("00"), 64f, Orange, FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            At(number.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -24f), new Vector2(300f, 80f));

            TextMeshProUGUI title = Txt(card, "Title", level.title,
                38f, Cream, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            At(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -112f), new Vector2(416f, 52f));

            TextMeshProUGUI description = Txt(card, "Description",
                level.description, 22f, Muted, FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            At(description.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(32f, -168f), new Vector2(416f, 70f));

            TextMeshProUGUI hint = Txt(card, "Play_Hint", "PLAY  >",
                22f, Orange, FontStyles.Bold,
                TextAlignmentOptions.BottomRight);
            At(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 24f), new Vector2(200f, 32f));

            Button button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = body;
            UnityEventTools.AddStringPersistentListener(
                button.onClick,
                new UnityAction<string>(menu.LoadLevel), level.scene);

            MenuButtonFX fx = card.gameObject.AddComponent<MenuButtonFX>();
            fx.background = body;
            fx.label = title;
            fx.normalBackground = CardBg;
            fx.highlightBackground = CardHover;
            fx.normalText = Cream;
            fx.highlightText = Color.white;
            fx.labelShift = 0f;

            if (first == null)
                first = button;
        }

        RectTransform buttons = UI("Buttons", panel);
        At(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 64f), new Vector2(800f, 80f));
        Row(buttons, 24f);

        Button back = MakeButton(buttons, "Back_Button", "BACK",
            new Vector2(240f, 76f), false, false);
        UnityEventTools.AddPersistentListener(
            back.onClick, new UnityAction(menu.ShowCharacters));

        if (first == null)
            first = back;

        return group;
    }

    // ---------------------------------------------------------
    // Controls panel
    // ---------------------------------------------------------

    private static CanvasGroup BuildControlsPanel(
        Transform canvas, PolishedMenuController menu, out Button back)
    {
        RectTransform panel = UI("Controls_Panel", canvas);
        Fill(panel);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

        Dim(panel);
        Heading(panel, "CONTROLS", "Everything you need to get to the acorn.");

        RectTransform body = UI("Body", panel);
        At(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -10f), new Vector2(980f, 500f));
        Img(body, roundSprite, CardBg, true, false);

        string[] actions =
        {
            "Move", "Run", "Jump", "Rotate the world", "Pause"
        };

        string[][] keys =
        {
            new[] { "W", "A", "S", "D" },
            new[] { "Shift" },
            new[] { "Space" },
            new[] { "Q", "R" },
            new[] { "Esc" }
        };

        for (int i = 0; i < actions.Length; i++)
        {
            float y = -48f - i * 84f;

            RectTransform rowRect = UI("Row_" + actions[i], body);
            At(rowRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, y), new Vector2(980f, 64f));

            TextMeshProUGUI action = Txt(rowRect, "Action", actions[i],
                30f, Cream, FontStyles.Bold, TextAlignmentOptions.Left);
            At(action.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(64f, 0f), new Vector2(400f, 64f));

            float x = 500f;

            foreach (string key in keys[i])
            {
                float width = key.Length <= 1 ? 64f : 36f + key.Length * 22f;

                RectTransform chip = UI("Key_" + key, rowRect);
                At(chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(x, 0f), new Vector2(width, 60f));
                Img(chip, roundSprite, Chip, true, false)
                    .pixelsPerUnitMultiplier = 1.6f;

                TextMeshProUGUI keyText = Txt(chip, "Label", key, 26f,
                    Cream, FontStyles.Bold, TextAlignmentOptions.Center);
                Fill(keyText.rectTransform);

                x += width + 12f;
            }
        }

        RectTransform buttons = UI("Buttons", panel);
        At(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 64f), new Vector2(800f, 80f));
        Row(buttons, 24f);

        back = MakeButton(buttons, "Back_Button", "BACK",
            new Vector2(240f, 76f), false, false);
        UnityEventTools.AddPersistentListener(
            back.onClick, new UnityAction(menu.ShowMain));

        return group;
    }

    // ---------------------------------------------------------
    // 3D preview stage
    // ---------------------------------------------------------

    private static CharacterPreview BuildStage(
        Transform parent, CharacterDef def, int index,
        Material pedestalMaterial, out RenderTexture texture)
    {
        GameObject stage = new GameObject("Stage_" + def.id);
        stage.transform.SetParent(parent, false);
        stage.transform.localPosition = new Vector3(index * 40f, 0f, 0f);

        Vector3 origin = stage.transform.position;

        // ---- pedestal ----
        GameObject pedestal =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Pedestal";
        pedestal.transform.SetParent(stage.transform, false);
        pedestal.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        pedestal.transform.localScale = new Vector3(1.7f, 0.04f, 1.7f);

        Collider collider = pedestal.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        if (pedestalMaterial != null)
        {
            pedestal.GetComponent<MeshRenderer>().sharedMaterial =
                pedestalMaterial;
        }

        // ---- model on a turntable ----
        GameObject turntable = new GameObject("Turntable");
        turntable.transform.SetParent(stage.transform, false);

        Transform rig;
        GameObject model;

        GameObject wrapper = BuildFitted(
            def.asset, def.id, turntable.transform,
            origin, 1.15f, true, def.modelYaw, out rig, out model);

        CharacterPreview preview = stage.AddComponent<CharacterPreview>();
        preview.turntable = turntable.transform;

        if (wrapper != null)
        {
            ApplyToonMaterial(model, def);

            preview.animator = PrepareAnimation(wrapper, rig, model, def);
            preview.procedural =
                wrapper.GetComponent<ProceduralCharacterAnimator>();
        }

        turntable.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);

        // ---- camera into a RenderTexture ----
        texture = GetRenderTexture("Preview_" + def.id);

        GameObject cameraObject = new GameObject("Preview_Camera");
        cameraObject.transform.SetParent(stage.transform, false);
        cameraObject.transform.position = origin + new Vector3(0f, 0.7f, 3.4f);
        cameraObject.transform.LookAt(origin + new Vector3(0f, 0.5f, 0f));

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = StageBg;
        camera.fieldOfView = 28f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 12f;
        camera.targetTexture = texture;

        return preview;
    }

    // =========================================================
    // UI helpers
    // =========================================================

    private static RectTransform UI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Fill(
        RectTransform rt,
        float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void At(
        RectTransform rt, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static Image Img(
        RectTransform rt, Sprite sprite, Color color,
        bool sliced, bool raycastTarget)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;

        if (sliced && sprite != null)
            image.type = Image.Type.Sliced;

        return image;
    }

    private static TextMeshProUGUI Txt(
        Transform parent, string name, string text, float size,
        Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rt = UI(name, parent);

        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Uses TextMesh Pro's own drop-shadow preset if present.</summary>
    private static void ApplyShadow(TextMeshProUGUI text)
    {
        Material shadow = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/" +
            "LiberationSans SDF - Drop Shadow.mat");

        TMP_FontAsset font =
            text.font != null ? text.font : TMP_Settings.defaultFontAsset;

        if (shadow != null && font != null &&
            font.name == "LiberationSans SDF")
        {
            text.fontSharedMaterial = shadow;
        }
    }

    private static Button MakeButton(
        Transform parent, string name, string text, Vector2 size,
        bool primary, bool leftAligned)
    {
        RectTransform rt = UI(name, parent);
        rt.sizeDelta = size;

        Color normal = primary ? Orange : WithAlpha(CardBg, 0.9f);

        Image background = Img(rt, roundSprite, normal, true, true);

        Button button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        TextMeshProUGUI label = Txt(rt, "Label", text, 34f,
            primary ? Ink : Cream, FontStyles.Bold,
            leftAligned ? TextAlignmentOptions.Left
                        : TextAlignmentOptions.Center);
        Fill(label.rectTransform, 36f, 0f, 36f, 0f);
        label.characterSpacing = 6f;

        MenuButtonFX fx = rt.gameObject.AddComponent<MenuButtonFX>();
        fx.background = background;
        fx.label = label;
        fx.labelRect = label.rectTransform;
        fx.normalBackground = normal;
        fx.highlightBackground = primary ? OrangeHot : Orange;
        fx.normalText = primary ? Ink : Cream;
        fx.highlightText = Ink;
        fx.labelShift = leftAligned ? 12f : 0f;

        return button;
    }

    private static void Row(RectTransform rt, float spacing)
    {
        HorizontalLayoutGroup layout =
            rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void Dim(RectTransform panel)
    {
        RectTransform dim = UI("Dim", panel);
        Fill(dim);
        Img(dim, null, WithAlpha(Ink, 0.78f), false, true);
    }

    private static TextMeshProUGUI Heading(
        RectTransform panel, string title, string subtitle)
    {
        TextMeshProUGUI heading = Txt(panel, "Heading", title, 64f,
            Cream, FontStyles.Bold, TextAlignmentOptions.Center);
        At(heading.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -70f), new Vector2(1500f, 84f));
        heading.characterSpacing = 8f;

        TextMeshProUGUI sub = Txt(panel, "Subheading", subtitle, 26f,
            Muted, FontStyles.Normal, TextAlignmentOptions.Center);
        At(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -158f), new Vector2(1500f, 40f));

        return sub;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == name)
                return rootObject;

            Transform found = FindDeep(rootObject.transform, name);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Color Hex(string html)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(html, out color)
            ? color
            : Color.magenta;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // =========================================================
    // Generated assets (saved in Assets/UI/PolishedMenu)
    // =========================================================

    private static void EnsureFolder()
    {
        if (!Directory.Exists(GenFolder))
        {
            Directory.CreateDirectory(GenFolder);
            AssetDatabase.Refresh();
        }
    }

    private static RenderTexture GetRenderTexture(string name)
    {
        string path = GenFolder + "/" + name + ".renderTexture";

        RenderTexture texture =
            AssetDatabase.LoadAssetAtPath<RenderTexture>(path);

        if (texture != null)
            return texture;

        texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        texture.antiAliasing = 4;
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static Material GetPedestalMaterial()
    {
        string path = GenFolder + "/Preview_Pedestal.mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        material = new Material(shader);
        material.color = Hex("#2F5A43");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void GenerateSprites()
    {
        roundSprite = RoundedSprite("Rounded", 64, 20f, 0f, 0f, 24);
        ringSprite = RoundedSprite("Rounded_Ring", 64, 20f, 4f, 0f, 24);
        glowSprite = RoundedSprite("Rounded_Glow", 128, 22f, 0f, 14f, 60);
        gradientSprite = GradientSprite();
        vignetteSprite = VignetteSprite();
    }

    /// <summary>
    /// White rounded rectangle for 9-slicing.
    /// stroke > 0 : only the outline.   blur > 0 : soft glow around it.
    /// </summary>
    private static Sprite RoundedSprite(
        string name, int size, float radius, float stroke, float blur,
        int border)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        float margin = blur > 0f ? blur * 2.4f : 0f;
        float half = size * 0.5f - margin;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - size * 0.5f) - (half - radius);
                float py = Mathf.Abs(y + 0.5f - size * 0.5f) - (half - radius);

                float outside = new Vector2(
                    Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;

                float inside = Mathf.Min(Mathf.Max(px, py), 0f);

                // Signed distance to the edge: negative = inside.
                float d = outside + inside - radius;

                float alpha;

                if (blur > 0f)
                {
                    alpha = d <= 0f
                        ? 1f
                        : Mathf.Exp(-(d * d) / (2f * blur * blur));
                }
                else if (stroke > 0f)
                {
                    alpha =
                        Mathf.Clamp01(0.5f - d) *
                        Mathf.Clamp01(0.5f + d + stroke);
                }
                else
                {
                    alpha = Mathf.Clamp01(0.5f - d);
                }

                pixels[y * size + x] = new Color32(
                    255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return SaveSprite(
            name, tex, new Vector4(border, border, border, border));
    }

    /// <summary>Solid on the left, fading to clear on the right.</summary>
    private static Sprite GradientSprite()
    {
        const int width = 256;
        const int height = 4;

        Texture2D tex =
            new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);
                float alpha = Mathf.Pow(1f - t, 1.7f);

                pixels[y * width + x] = new Color32(
                    255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return SaveSprite("Gradient_Left", tex, Vector4.zero);
    }

    /// <summary>Clear in the middle, darker toward the corners.</summary>
    private static Sprite VignetteSprite()
    {
        const int size = 128;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.55f, 1.35f, d)) * 0.85f;

                pixels[y * size + x] = new Color32(
                    255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return SaveSprite("Vignette", tex, Vector4.zero);
    }

    private static Sprite SaveSprite(string name, Texture2D tex, Vector4 border)
    {
        string path = GenFolder + "/" + name + ".png";

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer =
            (TextureImporter)AssetImporter.GetAtPath(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
