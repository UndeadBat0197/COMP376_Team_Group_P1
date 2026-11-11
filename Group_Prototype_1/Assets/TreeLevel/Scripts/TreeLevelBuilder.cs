using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the whole "inside the hollow tree" level as ordinary GameObjects:
/// the trunk, the ring walls with doorways, the buttons that turn the tree,
/// the obstacles and the acorn at the end.
///
/// In the editor the level is built into the scene the first time the scene
/// is opened, so it shows up in the Hierarchy and can be moved, edited and
/// saved like anything placed by hand. To rebuild it after changing the
/// numbers below, right-click this component and choose
/// "Build Level In Editor" ("Clear Built Level" removes it).
///
/// How the level works:
///  - The player starts on a deck on top of the trunk and has to push a
///    boulder along a channel into a hole, then drop in after it.
///  - Inside, the trunk is split into sections by ring walls. Every button
///    turns the tree a quarter turn left or right. Only one lane of each
///    wall has a real doorway; the others are painted fakes.
///  - Section 1: one turn. The wrong button swings thorns under you.
///  - Section 2: the doorway is on the ceiling. Two turns are needed, and
///    the second pair of buttons is back at the start of the section.
///  - Section 3: a timed door. The tree turns back after a few seconds,
///    so you have to run the obstacles to the doorway in time.
/// </summary>
[ExecuteAlways]
public class TreeLevelBuilder : MonoBehaviour
{
    [Header("Trunk")]
    [Tooltip("Inside radius of the hollow trunk.")]
    public float radius = 10f;
    public float barkThickness = 1.4f;
    [Range(16, 96)] public int sides = 48;

    [Header("Sections")]
    [Tooltip("Length of each of the four sections.")]
    public float sectionLength = 26f;
    public float doorHeight = 6f;
    [Tooltip("Half the doorway's width, in degrees around the trunk.")]
    public float doorHalfAngle = 20f;

    [Header("Start on top")]
    [Tooltip("How long the hole in the top of the trunk is.")]
    public float holeLength = 7f;
    [Tooltip("Radius of the boulder the player pushes into the hole.")]
    public float rockRadius = 1.8f;

    [Header("Obstacles from the other packs (optional)")]
    public GameObject fixedSawPrefab;
    public GameObject movingSawPrefab;
    public GameObject healthPotionPrefab;
    [Tooltip("Flowers, rocks... scattered along the sides of each lane.")]
    public GameObject[] decorationPrefabs;

    [Header("Puzzle timing")]
    [Tooltip("How long a wrong button keeps the thorns under the player.")]
    public float trapSeconds = 1.3f;
    [Tooltip("How long the timed door in section 3 stays open.")]
    public float timedDoorSeconds = 5f;

    [Header("Build")]
    [Tooltip("Build the level when the game starts, if it is not " +
             "already in the scene.")]
    public bool buildOnAwake = true;
    [Tooltip("Build the level into the scene when the scene is opened " +
             "in the editor, if it is not already there.")]
    public bool autoBuildInEditor = true;

    const string PivotName = "Tree (turns with the buttons)";
    const string FixedName = "World (does not turn)";
    const int SectionCount = 4;

    Transform pivot;
    Transform fixedRoot;

    // New objects on the trunk go under this folder.
    Transform group;
    TreeRotator rotator;

    Material barkMat, woodMat, ringsMat, leafMat, thornMat, thornBaseMat;
    Material buttonMat, buttonPressedMat, buttonBaseMat;
    Material acornNutMat, acornCapMat, stumpMat, mushroomCapMat, mushroomStemMat;
    Material deckMat, rockMat, fakeDoorMat, trapButtonMat, timedButtonMat;

    // Where each section's entry lane sits around the trunk, in degrees
    // (0 = at the bottom when the level starts). Each section has four
    // lanes, a quarter turn apart: lane 0 is the entry lane, lane 1 is on
    // the player's right, lane 2 is overhead and lane 3 is on the left.
    static readonly float[] SectionBase = { 0f, 90f, 270f, 0f };

    // The hole in the top of the trunk (above the first section).
    const float HoleAngle = 180f;
    const float HoleHalfAngle = 22.5f;
    const float HoleStart = 3f;

    Mesh coneMesh;

    void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (buildOnAwake && transform.Find(PivotName) == null)
            Build();
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        // In the editor (not playing): put the level into the scene so it
        // is visible in the Hierarchy. Done a moment later, because
        // objects should not be created while a scene is still loading.
        if (Application.isPlaying ||
            UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (autoBuildInEditor && transform.Find(PivotName) == null)
            UnityEditor.EditorApplication.delayCall += EditorAutoBuild;
    }

    void EditorAutoBuild()
    {
        if (this == null || Application.isPlaying)
            return;

        if (transform.Find(PivotName) != null)
            return;

        Build();
        MarkSceneChanged();
    }

    void MarkSceneChanged()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                gameObject.scene);
        }
    }
#endif

    // ------------------------------------------------------------
    // Menu commands (right-click the component header)
    // ------------------------------------------------------------

    [ContextMenu("Build Level In Editor")]
    public void BuildFromMenu()
    {
        Clear();
        Build();

#if UNITY_EDITOR
        MarkSceneChanged();
#endif
    }

    [ContextMenu("Clear Built Level")]
    public void Clear()
    {
        Remove(transform.Find(PivotName));
        Remove(transform.Find(FixedName));

        // Names used by earlier versions of this script.
        Remove(transform.Find("TreePivot"));
        Remove(transform.Find("TreeFixed"));
    }

    void Remove(Transform t)
    {
        if (t == null)
            return;

        if (Application.isPlaying)
        {
            t.gameObject.SetActive(false);
            Destroy(t.gameObject);
        }
        else
        {
            DestroyImmediate(t.gameObject);
        }
    }

    // ------------------------------------------------------------
    // Layout helpers
    // ------------------------------------------------------------

    float TrunkStart { get { return -16f; } }
    float HoleEnd { get { return HoleStart + holeLength; } }
    float OuterRadius { get { return radius + barkThickness; } }
    float DeckTop { get { return OuterRadius + 0.6f; } }
    float TrunkEnd { get { return SectionCount * sectionLength + 3f; } }

    // Angle around the trunk of one of a section's four lanes.
    float Lane(int section, int lane)
    {
        int s = Mathf.Clamp(section, 0, SectionBase.Length - 1);
        return SectionBase[s] + 90f * lane;
    }

    // Trunk rotation that brings a section's entry lane to the bottom.
    float TrunkAngleFor(int section)
    {
        return -Lane(section, 0);
    }

    float SectionStart(int section)
    {
        return section * sectionLength;
    }

    // Converts a sideways distance on the floor into degrees.
    float SideDegrees(float distance)
    {
        return distance / radius * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Places a transform on the inside of the trunk. Its local "up"
    /// points at the trunk's axis and its local Z runs along the trunk.
    /// </summary>
    void PlaceOnTrunk(
        Transform t, float degrees, float z, float heightAboveFloor)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);

        // Folders under the pivot sit at the pivot's origin, so the
        // same local position works inside any of them.
        t.SetParent(group != null ? group : pivot, false);
        t.localRotation = rotation;
        t.localPosition =
            rotation * new Vector3(0f, -radius + heightAboveFloor, z);
    }

    /// <summary>Makes an empty "folder" object in the Hierarchy.</summary>
    static Transform Folder(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    Transform NewGroup(string name, float degrees, float z, float height)
    {
        GameObject go = new GameObject(name);
        PlaceOnTrunk(go.transform, degrees, z, height);
        return go.transform;
    }

    // ------------------------------------------------------------
    // Build
    // ------------------------------------------------------------

    public void Build()
    {
        LoadMaterials();
        coneMesh = TreeMeshes.Cone("ThornCone", 0.5f, 1f, 6);

        GameObject pivotObject = new GameObject(PivotName);
        pivot = pivotObject.transform;
        pivot.SetParent(transform, false);

        // Kinematic body: tells physics this whole group moves.
        Rigidbody body = pivotObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        rotator = pivotObject.AddComponent<TreeRotator>();

        GameObject fixedObject = new GameObject(FixedName);
        fixedRoot = fixedObject.transform;
        fixedRoot.SetParent(transform, false);

        group = Folder(pivot, "Trunk");
        BuildTrunk();

        group = Folder(pivot, "Branches And Leaves");
        BuildOutsideTree();

        BuildTopDeck();

        BuildSection0();
        BuildSection1();
        BuildSection2();
        BuildSection3();

        group = null;
    }

    void LoadMaterials()
    {
        barkMat = LoadMaterial("Tree_Bark", new Color(0.45f, 0.28f, 0.16f));
        woodMat = LoadMaterial("Tree_Wood", new Color(0.86f, 0.66f, 0.42f));
        ringsMat = LoadMaterial("Tree_Rings", new Color(0.9f, 0.72f, 0.48f));
        leafMat = LoadMaterial("Tree_Leaves", new Color(0.35f, 0.75f, 0.3f));
        thornMat = LoadMaterial("Tree_Thorn", new Color(0.75f, 0.2f, 0.3f));
        thornBaseMat = LoadMaterial("Tree_ThornBase", new Color(0.3f, 0.45f, 0.2f));
        buttonMat = LoadMaterial("Tree_Button", new Color(0.95f, 0.25f, 0.2f));
        buttonPressedMat = LoadMaterial("Tree_ButtonPressed", new Color(0.3f, 0.85f, 0.35f));
        buttonBaseMat = LoadMaterial("Tree_ButtonBase", new Color(0.35f, 0.3f, 0.3f));
        acornNutMat = LoadMaterial("Tree_AcornNut", new Color(0.87f, 0.6f, 0.25f));
        acornCapMat = LoadMaterial("Tree_AcornCap", new Color(0.45f, 0.28f, 0.14f));
        stumpMat = LoadMaterial("Tree_Stump", new Color(0.7f, 0.5f, 0.3f));
        mushroomCapMat = LoadMaterial("Tree_MushroomCap", new Color(0.95f, 0.35f, 0.3f));
        mushroomStemMat = LoadMaterial("Tree_MushroomStem", new Color(0.97f, 0.93f, 0.85f));
        deckMat = LoadMaterial("Tree_Deck", new Color(0.72f, 0.5f, 0.3f));
        rockMat = LoadMaterial("Tree_Rock", new Color(0.6f, 0.62f, 0.68f));
        fakeDoorMat = LoadMaterial("Tree_FakeDoor", new Color(0.2f, 0.13f, 0.1f));
        trapButtonMat = LoadMaterial("Tree_ButtonTrap", new Color(0.55f, 0.2f, 0.6f));
        timedButtonMat = LoadMaterial("Tree_ButtonTimed", new Color(0.2f, 0.55f, 0.95f));
    }

    static Material LoadMaterial(string name, Color fallbackColor)
    {
        Material material = Resources.Load<Material>("TreeLevel/" + name);

        if (material != null)
            return material;

        // The material asset is missing: make a plain coloured one so
        // the level is still playable.
        Shader shader = Shader.Find("Toon/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        material = new Material(shader);
        material.name = name + " (fallback)";
        material.color = fallbackColor;
        return material;
    }

    // ------------------------------------------------------------
    // Trunk
    // ------------------------------------------------------------

    void BuildTrunk()
    {
        float outer = OuterRadius;

        // Inside: the surface the player walks on, with a hole in the
        // top that the player drops through at the start.
        GameObject inside = MeshObject(
            "Trunk Inside",
            TreeMeshes.TubeSurface(
                "TrunkInside", radius, TrunkStart, TrunkEnd, sides, true, 8f,
                HoleAngle, HoleHalfAngle, HoleStart, HoleEnd),
            woodMat,
            group);

        // The sun shines "through" the trunk, otherwise the whole
        // inside would be in shadow.
        NoShadow(inside);

        MeshCollider floor = inside.AddComponent<MeshCollider>();
        floor.sharedMesh = inside.GetComponent<MeshFilter>().sharedMesh;

        // Outside: bark.
        GameObject bark = MeshObject(
            "Trunk Bark",
            TreeMeshes.TubeSurface(
                "TrunkBark", outer, TrunkStart, TrunkEnd, sides, false, 8f,
                HoleAngle, HoleHalfAngle, HoleStart, HoleEnd),
            barkMat,
            group);

        NoShadow(bark);

        // Cut wood around the edge of the hole.
        NoShadow(MeshObject(
            "Hole Rim",
            TreeMeshes.HoleRim(
                "HoleRim", radius, outer, sides,
                HoleAngle, HoleHalfAngle, HoleStart, HoleEnd),
            ringsMat,
            group));

        // The two cut ends, showing the growth rings.
        NoShadow(MeshObject(
            "Trunk End (start)",
            TreeMeshes.EndRing(
                "TrunkEndStart", radius, outer, TrunkStart, -1f, sides),
            ringsMat,
            group));

        NoShadow(MeshObject(
            "Trunk End (goal)",
            TreeMeshes.EndRing(
                "TrunkEndGoal", radius, outer, TrunkEnd, 1f, sides),
            ringsMat,
            group));

        // Hatch: the missing piece of trunk. It is switched on once the
        // player is inside, so the hole can never rotate under them.
        GameObject hatchInside = MeshObject(
            "Hatch (inside)",
            TreeMeshes.TubeSurface(
                "HatchInside", radius, TrunkStart, TrunkEnd, sides, true, 8f,
                HoleAngle, HoleHalfAngle, HoleStart, HoleEnd, true),
            woodMat,
            group);

        NoShadow(hatchInside);

        MeshCollider hatchCollider = hatchInside.AddComponent<MeshCollider>();
        hatchCollider.sharedMesh =
            hatchInside.GetComponent<MeshFilter>().sharedMesh;

        GameObject hatchBark = MeshObject(
            "Hatch (bark)",
            TreeMeshes.TubeSurface(
                "HatchBark", outer, TrunkStart, TrunkEnd, sides, false, 8f,
                HoleAngle, HoleHalfAngle, HoleStart, HoleEnd, true),
            barkMat,
            group);

        NoShadow(hatchBark);

        hatchInside.SetActive(false);
        hatchBark.SetActive(false);

        // Trigger under the hole: closes the hatch, and is the first
        // checkpoint (so dying inside does not put you back on top).
        float holeMiddle = (HoleStart + HoleEnd) * 0.5f;

        Transform checkpoints = Folder(fixedRoot, "Checkpoints");
        Transform invisibleWalls = Folder(fixedRoot, "Invisible Walls");

        GameObject entry = new GameObject("Entry (hatch + checkpoint 0)");
        entry.transform.SetParent(checkpoints, false);
        entry.transform.localPosition = new Vector3(0f, -1f, holeMiddle);

        BoxCollider entryBox = entry.AddComponent<BoxCollider>();
        entryBox.size = new Vector3(radius * 1.6f, 5f, holeLength + 6f);
        entryBox.isTrigger = true;

        TreeHatch hatch = entry.AddComponent<TreeHatch>();
        hatch.closeOnEnter = new GameObject[] { hatchInside, hatchBark };

        Transform respawn = new GameObject("Respawn Point").transform;
        respawn.SetParent(checkpoints, false);
        respawn.localPosition = new Vector3(0f, -radius + 3f, holeMiddle);

        TreeCheckpoint firstCheckpoint = entry.AddComponent<TreeCheckpoint>();
        firstCheckpoint.trunkAngle = TrunkAngleFor(0);
        firstCheckpoint.respawnPoint = respawn;

        // Invisible wall so the player cannot walk out of the back.
        GameObject backStop = new GameObject("Back Stop");
        backStop.transform.SetParent(invisibleWalls, false);
        backStop.transform.localPosition =
            new Vector3(0f, 0f, TrunkStart + 1f);

        BoxCollider stop = backStop.AddComponent<BoxCollider>();
        stop.size = new Vector3(radius * 2f, radius * 2f, 1f);

        // ...and one behind the acorn.
        GameObject frontStop = new GameObject("Front Stop");
        frontStop.transform.SetParent(invisibleWalls, false);
        frontStop.transform.localPosition =
            new Vector3(0f, 0f, TrunkEnd - 1f);

        BoxCollider stop2 = frontStop.AddComponent<BoxCollider>();
        stop2.size = new Vector3(radius * 2f, radius * 2f, 1f);
    }

    static void NoShadow(GameObject go)
    {
        go.GetComponent<MeshRenderer>().shadowCastingMode =
            ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------
    // The start: a deck on top of the trunk
    // ------------------------------------------------------------

    /// <summary>
    /// A wooden deck on top of the trunk. A channel between thorn beds
    /// leads to the hole; a boulder sits in the channel and has to be
    /// pushed into the hole before the player can follow it down.
    /// The deck does not turn with the tree.
    /// </summary>
    void BuildTopDeck()
    {
        Transform start = Folder(fixedRoot, "Start (top of the tree)");
        Transform deck = Folder(start, "Deck");

        float top = DeckTop;
        float slabY = top - 0.3f;

        float back = -14f;
        float front = HoleEnd + 1.5f;
        float halfWidth = 6f;
        float holeHalfWidth = 3.4f;

        // Floor, in four pieces around the hole.
        DeckBox(deck, "Deck", 0f, slabY, (back + HoleStart) * 0.5f,
            halfWidth * 2f, 0.6f, HoleStart - back, deckMat);

        float sideWidth = halfWidth - holeHalfWidth;
        float sideX = holeHalfWidth + sideWidth * 0.5f;
        float holeMiddle = (HoleStart + HoleEnd) * 0.5f;

        DeckBox(deck, "Deck (left of hole)", -sideX, slabY, holeMiddle,
            sideWidth, 0.6f, holeLength, deckMat);
        DeckBox(deck, "Deck (right of hole)", sideX, slabY, holeMiddle,
            sideWidth, 0.6f, holeLength, deckMat);
        DeckBox(deck, "Deck (past hole)", 0f, slabY, (HoleEnd + front) * 0.5f,
            halfWidth * 2f, 0.6f, front - HoleEnd, deckMat);

        // Log rails round the outside, so nothing rolls off.
        float railY = top + 0.6f;
        float length = front - back;
        float middle = (front + back) * 0.5f;

        DeckBox(deck, "Rail", -halfWidth, railY, middle, 0.8f, 1.2f, length, barkMat);
        DeckBox(deck, "Rail", halfWidth, railY, middle, 0.8f, 1.2f, length, barkMat);
        DeckBox(deck, "Rail", 0f, railY, back, halfWidth * 2f, 1.2f, 0.8f, barkMat);
        DeckBox(deck, "Rail", 0f, railY, front, halfWidth * 2f, 1.2f, 0.8f, barkMat);

        // The channel the boulder rolls along.
        float channelStart = -7f;
        float channelLength = HoleStart - channelStart;
        float channelMiddle = (HoleStart + channelStart) * 0.5f;
        float channelHalfWidth = rockRadius + 0.75f;

        DeckBox(deck, "Channel Log", -channelHalfWidth, top + 0.45f,
            channelMiddle, 0.5f, 0.9f, channelLength, barkMat);
        DeckBox(deck, "Channel Log", channelHalfWidth, top + 0.45f,
            channelMiddle, 0.5f, 0.9f, channelLength, barkMat);

        // Thorns on both sides of the channel: no walking round the rock.
        float bedWidth = halfWidth - 0.4f - (channelHalfWidth + 0.25f);
        float bedX = channelHalfWidth + 0.25f + bedWidth * 0.5f;

        for (int sideSign = -1; sideSign <= 1; sideSign += 2)
        {
            Transform bed = new GameObject("Deck Thorns").transform;
            bed.SetParent(start, false);
            bed.localPosition =
                new Vector3(sideSign * bedX, top, channelMiddle);

            FillThorns(bed, bedWidth, channelLength);
        }

        // The boulder.
        GameObject rock = MeshObject(
            "Boulder",
            TreeMeshes.Rock("Boulder", rockRadius, 3.1f),
            rockMat,
            start);

        rock.transform.localPosition =
            new Vector3(0f, top + rockRadius + 0.05f, channelStart + 3.5f);

        SphereCollider rockCollider = rock.AddComponent<SphereCollider>();
        rockCollider.radius = rockRadius * 0.95f;

        Rigidbody rockBody = rock.AddComponent<Rigidbody>();
        rockBody.mass = 3f;
    }

    GameObject DeckBox(
        Transform parent, string name,
        float x, float y, float z,
        float sizeX, float sizeY, float sizeZ, Material material)
    {
        return Primitive(
            PrimitiveType.Cube, name, parent,
            new Vector3(x, y, z), new Vector3(sizeX, sizeY, sizeZ),
            material, true);
    }

    /// <summary>
    /// Branches and leaf clumps on the outside, so from the ends the
    /// trunk reads as a tree and not a pipe.
    /// </summary>
    void BuildOutsideTree()
    {
        float outer = radius + barkThickness;
        Random.State previous = Random.state;
        Random.InitState(7);

        int branchCount = 9;

        for (int i = 0; i < branchCount; i++)
        {
            float z = Mathf.Lerp(
                TrunkStart + 4f, TrunkEnd - 4f,
                (i + 0.5f) / branchCount);

            float degrees = 110f + i * 97f;
            float length = Random.Range(7f, 12f);

            // Nothing may grow through the deck on top.
            if (z < HoleEnd + 8f &&
                Mathf.Abs(Mathf.DeltaAngle(degrees, 180f)) < 75f)
            {
                degrees += 150f;
            }

            Transform branch = new GameObject("Branch " + i).transform;
            branch.SetParent(group, false);
            branch.localRotation = Quaternion.Euler(
                Random.Range(-25f, 25f), 0f, degrees + 180f);
            branch.localPosition =
                TreeMeshes.Radial(degrees) * (outer - 0.5f) +
                Vector3.forward * z;

            // Local "up" of the branch points away from the trunk.
            Primitive(
                PrimitiveType.Cylinder, "Wood", branch,
                new Vector3(0f, length * 0.5f, 0f),
                new Vector3(1.6f, length * 0.5f, 1.6f),
                barkMat, false);

            for (int k = 0; k < 3; k++)
            {
                float size = Random.Range(5f, 8f);

                Primitive(
                    PrimitiveType.Sphere, "Leaves", branch,
                    new Vector3(
                        Random.Range(-2.5f, 2.5f),
                        length + Random.Range(-1.5f, 2f),
                        Random.Range(-2.5f, 2.5f)),
                    new Vector3(size, size * 0.8f, size),
                    leafMat, false);
            }
        }

        // A leafy wreath around both openings.
        for (int end = 0; end < 2; end++)
        {
            float z = end == 0 ? TrunkStart : TrunkEnd;

            for (int i = 0; i < 10; i++)
            {
                float degrees = i * 36f + Random.Range(-8f, 8f);
                float size = Random.Range(3.5f, 5.5f);

                // Leave the top of the start end clear for the deck.
                if (end == 0 &&
                    Mathf.Abs(Mathf.DeltaAngle(degrees, 180f)) < 50f)
                {
                    continue;
                }

                Primitive(
                    PrimitiveType.Sphere, "Leaves", group,
                    TreeMeshes.Radial(degrees) * (outer + 0.8f) +
                    Vector3.forward * (z + Random.Range(-0.8f, 0.8f)),
                    new Vector3(size, size, size),
                    leafMat, false);
            }
        }

        Random.state = previous;
    }

    // ------------------------------------------------------------
    // Walls, buttons, checkpoints
    // ------------------------------------------------------------

    /// <summary>
    /// The ring wall at the end of a section. The real doorway is on
    /// doorLane; fakeLanes get a painted doorway. Also adds the
    /// checkpoint just past the real doorway.
    /// </summary>
    void BuildWall(int section, int doorLane, int[] fakeLanes)
    {
        float wallZ = SectionStart(section + 1);
        float doorDegrees = Lane(section, doorLane);

        GameObject wall = MeshObject(
            "Ring Wall",
            TreeMeshes.RingWall(
                "RingWall", radius + 0.3f, 0.8f, sides,
                doorDegrees, doorHalfAngle, doorHeight),
            ringsMat,
            group);

        wall.transform.localPosition = new Vector3(0f, 0f, wallZ);
        NoShadow(wall);

        MeshCollider wallCollider = wall.AddComponent<MeshCollider>();
        wallCollider.sharedMesh =
            wall.GetComponent<MeshFilter>().sharedMesh;

        // The same leafy arch round real and fake doorways, so they
        // look alike at a glance.
        DoorArch(doorDegrees, wallZ);

        foreach (int fakeLane in fakeLanes)
        {
            float fakeDegrees = Lane(section, fakeLane);

            GameObject fake = MeshObject(
                "Fake Door",
                TreeMeshes.DoorPanel(
                    "FakeDoor", radius + 0.3f, sides,
                    fakeDegrees, doorHalfAngle, doorHeight),
                fakeDoorMat,
                group);

            fake.transform.localPosition =
                new Vector3(0f, 0f, wallZ - 0.45f);
            NoShadow(fake);

            DoorArch(fakeDegrees, wallZ);
        }

        // Checkpoint just past the doorway. It does not turn with the
        // tree: once the door lane is at the bottom, so is this.
        Transform checkpoints = fixedRoot.Find("Checkpoints");

        GameObject checkpoint =
            new GameObject("Checkpoint " + (section + 1));
        checkpoint.transform.SetParent(
            checkpoints != null ? checkpoints : fixedRoot, false);
        checkpoint.transform.localPosition =
            new Vector3(0f, -radius + 3f, wallZ + 2.5f);

        BoxCollider box = checkpoint.AddComponent<BoxCollider>();
        box.size = new Vector3(8f, 6f, 1.5f);
        box.isTrigger = true;

        TreeCheckpoint cp = checkpoint.AddComponent<TreeCheckpoint>();
        cp.trunkAngle = TrunkAngleFor(section + 1);
    }

    enum ButtonKind
    {
        Stay,       // the tree stays turned
        Trap,       // wrong way: thorns swing under the player, then back
        TimedDoor,  // right way, but the tree turns back after a while
    }

    /// <summary>
    /// Two buttons side by side on a lane: the left one turns the tree
    /// to bring the lane on the left down, the right one the lane on
    /// the right.
    /// </summary>
    void ButtonPair(
        int section, int lane, float zInSection,
        ButtonKind left, ButtonKind right)
    {
        float degrees = Lane(section, lane);
        float z = SectionStart(section) + zInSection;

        BuildButton(degrees + SideDegrees(-3.4f), z, -90f, left);
        BuildButton(degrees + SideDegrees(3.4f), z, 90f, right);
    }

    /// <summary>
    /// Thorns right across a lane. Used under the buttons on the lane a
    /// wrong button turns to.
    /// </summary>
    void ThornBed(int section, int lane, float zInSection, float length)
    {
        ThornPatch(
            Lane(section, lane), SectionStart(section) + zInSection,
            0f, 15.4f, length);
    }

    void DoorArch(float doorDegrees, float wallZ)
    {
        for (int i = -2; i <= 2; i++)
        {
            float degrees = doorDegrees + i * doorHalfAngle * 0.55f;
            float height = doorHeight + 0.6f - Mathf.Abs(i) * 0.25f;

            Transform leaf = NewGroup(
                "Door Leaves", degrees, wallZ - 0.6f, height);

            Primitive(
                PrimitiveType.Sphere, "Leaves", leaf, Vector3.zero,
                new Vector3(2.4f, 1.6f, 1.2f), leafMat, false);
        }
    }

    void BuildButton(
        float laneDegrees, float z, float turnDegrees, ButtonKind kind)
    {
        Material idle =
            kind == ButtonKind.TimedDoor ? timedButtonMat : buttonMat;

        Transform root = NewGroup("Tree Button", laneDegrees, z, 0f);

        Primitive(
            PrimitiveType.Cylinder, "Base", root,
            new Vector3(0f, 0.1f, 0f), new Vector3(3.4f, 0.12f, 3.4f),
            buttonBaseMat, false);

        GameObject cap = Primitive(
            PrimitiveType.Cylinder, "Cap", root,
            new Vector3(0f, 0.36f, 0f), new Vector3(2.7f, 0.16f, 2.7f),
            idle, false);

        // A bobbing marker above the button draws the eye to it.
        GameObject marker = Primitive(
            PrimitiveType.Sphere, "Marker", root,
            new Vector3(0f, 4.2f, 0f), new Vector3(0.7f, 0.7f, 0.7f),
            idle, false);

        marker.GetComponent<MeshRenderer>().shadowCastingMode =
            ShadowCastingMode.Off;

        BoxCollider trigger = root.gameObject.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 1.2f, 0f);
        trigger.size = new Vector3(2.6f, 2.4f, 2.6f);
        trigger.isTrigger = true;

        TreeButton button = root.gameObject.AddComponent<TreeButton>();
        button.turnDegrees = turnDegrees;
        button.rotator = rotator;
        button.cap = cap.transform;
        button.capRenderer = cap.GetComponent<MeshRenderer>();
        button.idleMaterial = idle;

        if (kind == ButtonKind.Trap)
        {
            button.returnAfterSeconds = trapSeconds;
            button.pressedMaterial = trapButtonMat;
        }
        else if (kind == ButtonKind.TimedDoor)
        {
            button.returnAfterSeconds = timedDoorSeconds;
            button.pressedMaterial = buttonPressedMat;
        }
        else
        {
            button.returnAfterSeconds = 0f;
            button.pressedMaterial = buttonPressedMat;
        }
    }

    // ------------------------------------------------------------
    // Obstacles
    // ------------------------------------------------------------

    /// <summary>
    /// A patch of thorns that hurts on touch. Wide patches are built
    /// from narrow strips so they follow the curve of the floor.
    /// </summary>
    void ThornPatch(
        float laneDegrees, float z, float side,
        float width, float length)
    {
        int strips = Mathf.Max(1, Mathf.CeilToInt(width / 2.4f));
        float stripWidth = width / strips;

        for (int i = 0; i < strips; i++)
        {
            float offset = (i - (strips - 1) * 0.5f) * stripWidth;
            ThornStrip(laneDegrees, z, side + offset, stripWidth, length);
        }
    }

    void ThornStrip(
        float laneDegrees, float z, float side,
        float width, float length)
    {
        Transform root = NewGroup(
            "Thorn Patch", laneDegrees + SideDegrees(side), z, 0f);

        FillThorns(root, width, length);
    }

    /// <summary>
    /// Fills a flat rectangle (centred on root, in its local X/Z) with
    /// thorns and makes it hurt on touch.
    /// </summary>
    void FillThorns(Transform root, float width, float length)
    {
        Primitive(
            PrimitiveType.Cube, "Base", root,
            new Vector3(0f, 0.08f, 0f),
            new Vector3(width, 0.2f, length),
            thornBaseMat, false);

        int across = Mathf.Max(1, Mathf.RoundToInt(width / 0.9f));
        int along = Mathf.Max(1, Mathf.RoundToInt(length / 0.9f));

        for (int x = 0; x < across; x++)
        {
            for (int y = 0; y < along; y++)
            {
                GameObject spike = new GameObject("Thorn");
                spike.transform.SetParent(root, false);

                float px = (x + 0.5f) / across - 0.5f;
                float pz = (y + 0.5f) / along - 0.5f;

                // Offset every other row for a less regular look.
                float stagger = (y % 2 == 0) ? 0.12f : -0.12f;

                spike.transform.localPosition = new Vector3(
                    px * width + stagger, 0.15f, pz * length);

                float tall = 1.1f + 0.35f * (((x * 7 + y * 13) % 5) / 4f);
                spike.transform.localScale =
                    new Vector3(0.7f, tall, 0.7f);

                spike.AddComponent<MeshFilter>().sharedMesh = coneMesh;
                spike.AddComponent<MeshRenderer>().sharedMaterial = thornMat;
            }
        }

        BoxCollider trigger = root.gameObject.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 0.6f, 0f);
        trigger.size = new Vector3(
            width, 1.1f, Mathf.Max(0.2f, length - 0.3f));
        trigger.isTrigger = true;

        root.gameObject.AddComponent<ToonHazard>();
    }

    /// <summary>A fallen log across the lane: jump over it.</summary>
    void Hurdle(float laneDegrees, float z, float width, float height)
    {
        // Three pieces, so the log follows the curve of the floor
        // and cannot be walked around.
        const int pieces = 3;
        float pieceWidth = width / pieces;

        for (int i = 0; i < pieces; i++)
        {
            float side = (i - (pieces - 1) * 0.5f) * pieceWidth;

            Transform root = NewGroup(
                "Log Hurdle", laneDegrees + SideDegrees(side), z, 0f);

            Primitive(
                PrimitiveType.Cube, "Log", root,
                new Vector3(0f, height * 0.5f - 0.2f, 0f),
                new Vector3(pieceWidth + 0.3f, height + 0.4f, 1.3f),
                barkMat, true);
        }
    }

    /// <summary>A tree stump to stand on.</summary>
    void Stump(float laneDegrees, float z, float side, float height, float width)
    {
        Transform root = NewGroup(
            "Stump", laneDegrees + SideDegrees(side), z, 0f);

        GameObject stump = Primitive(
            PrimitiveType.Cylinder, "Wood", root,
            new Vector3(0f, height * 0.5f - 0.15f, 0f),
            new Vector3(width, height * 0.5f + 0.15f, width),
            stumpMat, false);

        // The primitive's capsule collider is rounded on top; a
        // convex mesh collider gives a flat top to land on.
        MeshCollider collider = stump.AddComponent<MeshCollider>();
        collider.sharedMesh = stump.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = true;
    }

    void Mushroom(float laneDegrees, float z, float side, float size)
    {
        Transform root = NewGroup(
            "Mushroom", laneDegrees + SideDegrees(side), z, 0f);

        Primitive(
            PrimitiveType.Cylinder, "Stem", root,
            new Vector3(0f, 0.5f * size, 0f),
            new Vector3(0.35f * size, 0.5f * size, 0.35f * size),
            mushroomStemMat, false);

        Primitive(
            PrimitiveType.Sphere, "Cap", root,
            new Vector3(0f, 1.0f * size, 0f),
            new Vector3(1.3f * size, 0.7f * size, 1.3f * size),
            mushroomCapMat, false);
    }

    GameObject Spawn(
        GameObject prefab, string name,
        float laneDegrees, float z, float side, float height, float scale)
    {
        if (prefab == null)
            return null;

        GameObject instance = null;

#if UNITY_EDITOR
        // Built in the editor: keep the link to the prefab, so the
        // object shows as a prefab instance in the Hierarchy.
        if (!Application.isPlaying)
        {
            instance =
                UnityEditor.PrefabUtility.InstantiatePrefab(prefab)
                as GameObject;
        }
#endif

        if (instance == null)
            instance = Instantiate(prefab);

        instance.name = name;

        PlaceOnTrunk(
            instance.transform,
            laneDegrees + SideDegrees(side), z, height);

        instance.transform.localScale = Vector3.one * scale;
        return instance;
    }

    /// <summary>
    /// Makes sure a saw hurts: its blade already has a trigger collider,
    /// so give that a ToonHazard as well as the pack's own damage script.
    /// </summary>
    static void MakeBladeHurt(GameObject saw)
    {
        if (saw == null)
            return;

        foreach (Collider c in saw.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger && c.GetComponent<ToonHazard>() == null)
                c.gameObject.AddComponent<ToonHazard>();
        }
    }

    /// <summary>Scatters flowers/rocks along both sides of a lane.</summary>
    void Decorate(int section, int laneIndex, int seed)
    {
        Random.State previous = Random.state;
        Random.InitState(seed);

        float lane = Lane(section, laneIndex);
        float start = SectionStart(section);

        for (int i = 0; i < 10; i++)
        {
            float z = start + Random.Range(2f, sectionLength - 9f);

            // Keep the middle of the lane clear.
            float side = Random.Range(5.5f, 8.5f);
            if (i % 2 == 0)
                side = -side;

            bool placedPrefab = false;

            if (decorationPrefabs != null && decorationPrefabs.Length > 0)
            {
                GameObject prefab =
                    decorationPrefabs[i % decorationPrefabs.Length];

                GameObject decoration = Spawn(
                    prefab, "Decoration", lane, z, side, 0f,
                    Random.Range(1.2f, 1.8f));

                if (decoration != null)
                {
                    // Decorations should never block the player.
                    foreach (Collider c in
                        decoration.GetComponentsInChildren<Collider>())
                    {
                        c.enabled = false;
                    }

                    placedPrefab = true;
                }
            }

            if (!placedPrefab || i % 3 == 0)
                Mushroom(lane, z + 0.8f, side * 1.05f, Random.Range(0.7f, 1.4f));
        }

        Random.state = previous;
    }

    // ------------------------------------------------------------
    // Sections. z positions are measured from the section's start.
    // ------------------------------------------------------------

    // Section 1: land, learn to jump, meet the two buttons.
    // One turn to the right opens the way; left is the trap.
    void BuildSection0()
    {
        Transform section = Folder(pivot, "Section 1 - Landing");
        float z = SectionStart(0);
        float lane = Lane(0, 0);

        group = Folder(section, "Obstacles");

        // The player (and the boulder) land between z = 3 and 10.
        Hurdle(lane, z + 13.5f, 14f, 1.3f);

        // Two patches with a gap in the middle to walk through.
        ThornPatch(lane, z + 17.5f, -4.6f, 6f, 2f);
        ThornPatch(lane, z + 17.5f, 4.6f, 6f, 2f);

        group = Folder(section, "Buttons");
        ButtonPair(0, 0, 21.5f, ButtonKind.Trap, ButtonKind.Stay);

        group = Folder(section, "Trap Thorns (left lane)");
        ThornBed(0, 3, 21.4f, 7.6f);

        group = Folder(section, "Wall And Doors");
        BuildWall(0, 1, new int[] { 3 });

        group = Folder(section, "Decorations");
        Decorate(0, 0, 11);
    }

    // Section 2: the doorway is on the ceiling, so it takes two turns.
    //  - Entry lane: weave through thorns to the first buttons.
    //    Left is correct; right swings thorns under you.
    //  - Left lane: walk BACK past a moving saw to the second buttons
    //    near the start. Left again brings the door lane down;
    //    right undoes your first turn.
    //  - Door lane: cross the thorn field on stumps to the doorway.
    void BuildSection1()
    {
        Transform section = Folder(pivot, "Section 2 - Door On The Ceiling");
        float z = SectionStart(1);

        // --- entry lane
        float entry = Lane(1, 0);

        group = Folder(section, "Entry Lane - Thorn Slalom");

        // Each patch blocks one side of the lane: go left, right, left.
        ThornPatch(entry, z + 5.5f, 3.2f, 7f, 2f);
        ThornPatch(entry, z + 9.5f, -3.2f, 7f, 2f);
        ThornPatch(entry, z + 13.5f, 3.2f, 7f, 2f);

        group = Folder(section, "Entry Lane - Buttons");
        ButtonPair(1, 0, 21.5f, ButtonKind.Stay, ButtonKind.Trap);

        group = Folder(section, "Trap Thorns (right lane)");
        ThornBed(1, 1, 21.4f, 7.6f);

        // --- left lane: back towards the start
        float left = Lane(1, 3);

        group = Folder(section, "Left Lane - Saw And Buttons");

        // The saw slides sideways across the lane (its local X).
        GameObject saw = Spawn(
            movingSawPrefab, "Moving Saw", left, z + 13f, 0f, 0.4f, 1.5f);

        MakeBladeHurt(saw);

        if (saw != null)
        {
            // Its rail is straight, so centre it on the lane: the saw
            // starts 4.5 units to one side and travels 9 units.
            saw.transform.localPosition +=
                saw.transform.localRotation * new Vector3(-4.5f, 0f, 0f);
        }
        else
        {
            ThornPatch(left, z + 13f, 0f, 3f, 1.6f);
        }

        Spawn(healthPotionPrefab, "Health Potion",
            left, z + 9f, 3.5f, 1.4f, 1.3f);

        ButtonPair(1, 3, 5.5f, ButtonKind.Stay, ButtonKind.Stay);

        // --- door lane (overhead at first)
        float door = Lane(1, 2);

        group = Folder(section, "Door Lane - Stump Crossing");

        // Thorns across the whole lane...
        ThornPatch(door, z + 15f, 0f, 15.4f, 10f);

        // ...with a path of stumps to hop across.
        Stump(door, z + 8.6f, 0f, 1.0f, 2.8f);
        Stump(door, z + 11.6f, -1.5f, 1.6f, 2.4f);
        Stump(door, z + 15.2f, 1.5f, 2.0f, 2.4f);
        Stump(door, z + 18.8f, -1.2f, 1.6f, 2.4f);

        group = Folder(section, "Wall And Doors");
        BuildWall(1, 2, new int[] { 1, 3 });

        group = Folder(section, "Decorations");
        Decorate(1, 0, 23);
    }

    // Section 3: a timed door. The blue button (right) brings the door
    // lane down, but only for a few seconds, and that lane is an
    // obstacle run. When the tree turns back, the entry lane under you
    // is covered in thorns. The left button is a trap.
    void BuildSection2()
    {
        Transform section = Folder(pivot, "Section 3 - Timed Door");
        float z = SectionStart(2);

        group = Folder(section, "Entry Lane - Buttons");
        ButtonPair(2, 0, 5f, ButtonKind.Trap, ButtonKind.TimedDoor);

        group = Folder(section, "Entry Lane - Thorns");
        ThornBed(2, 0, 17.3f, 16f);

        group = Folder(section, "Trap Thorns (left lane)");
        ThornBed(2, 3, 5f, 8f);

        // --- door lane: run it before the tree turns back
        float run = Lane(2, 1);

        group = Folder(section, "Door Lane - Obstacle Run");

        Hurdle(run, z + 10.5f, 14f, 1.3f);

        ThornPatch(run, z + 15f, -4.6f, 6f, 2f);
        ThornPatch(run, z + 15f, 4.6f, 6f, 2f);

        GameObject saw = Spawn(
            fixedSawPrefab, "Fixed Saw", run, z + 19.5f, -3.2f, 0f, 1.5f);

        MakeBladeHurt(saw);

        if (saw == null)
            ThornPatch(run, z + 19.5f, -3.2f, 2.4f, 1.6f);

        Spawn(healthPotionPrefab, "Health Potion",
            run, z + 22.5f, 3f, 1.4f, 1.3f);

        group = Folder(section, "Wall And Doors");
        BuildWall(2, 1, new int[] { 3 });

        group = Folder(section, "Decorations");
        Decorate(2, 1, 37);
    }

    // Section 4: the last climb, and the acorn.
    void BuildSection3()
    {
        Transform section = Folder(pivot, "Section 4 - Acorn");
        float lane = Lane(3, 0);
        float z = SectionStart(3);

        group = Folder(section, "Obstacles");

        Hurdle(lane, z + 5f, 14f, 1.3f);
        ThornPatch(lane, z + 8.2f, -4.6f, 6f, 2f);
        ThornPatch(lane, z + 8.2f, 4.6f, 6f, 2f);

        Spawn(healthPotionPrefab, "Health Potion",
            lane, z + 11f, 0f, 1.4f, 1.3f);

        // Steps up to the acorn's stump.
        Stump(lane, z + 14.5f, 0f, 1.0f, 3.2f);
        Stump(lane, z + 17.5f, 0f, 2.0f, 3.2f);
        Stump(lane, z + 21f, 0f, 3.0f, 4.2f);

        group = Folder(section, "Goal");
        BuildAcorn(lane, z + 21f, 3.0f + 2.2f);

        group = Folder(section, "Decorations");
        Decorate(3, 0, 51);
    }

    void BuildAcorn(float laneDegrees, float z, float height)
    {
        Transform root = NewGroup("Goal Acorn", laneDegrees, z, height);

        SphereCollider trigger =
            root.gameObject.AddComponent<SphereCollider>();
        trigger.radius = 1.6f;
        trigger.isTrigger = true;

        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root, false);

        // Outlines are (distance from the centre line, height).
        Vector2[] nut =
        {
            new Vector2(0.00f, -1.25f),
            new Vector2(0.22f, -1.10f),
            new Vector2(0.55f, -0.75f),
            new Vector2(0.80f, -0.25f),
            new Vector2(0.86f,  0.20f),
            new Vector2(0.80f,  0.45f),
        };

        Vector2[] cap =
        {
            new Vector2(0.98f, 0.30f),
            new Vector2(1.02f, 0.55f),
            new Vector2(0.88f, 0.85f),
            new Vector2(0.55f, 1.08f),
            new Vector2(0.00f, 1.18f),
        };

        MeshObject("Nut", TreeMeshes.Lathe("AcornNut", nut, 20),
            acornNutMat, visual);

        MeshObject("Cap", TreeMeshes.Lathe("AcornCap", cap, 20),
            acornCapMat, visual);

        // Underside of the cap.
        Vector2[] capBase =
        {
            new Vector2(0.70f, 0.30f),
            new Vector2(0.98f, 0.30f),
        };

        MeshObject("Cap Rim", TreeMeshes.Lathe("AcornCapRim", capBase, 20),
            acornCapMat, visual);

        Primitive(
            PrimitiveType.Cylinder, "Stem", visual,
            new Vector3(0.05f, 1.35f, 0f),
            new Vector3(0.14f, 0.25f, 0.14f),
            acornCapMat, false);

        GoalAcorn goal = root.gameObject.AddComponent<GoalAcorn>();
        goal.visual = visual;

        // Leaves around the acorn's stump.
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;

            Transform leaf = NewGroup(
                "Goal Leaves",
                laneDegrees + SideDegrees(Mathf.Cos(a) * 3.4f),
                z + Mathf.Sin(a) * 3.4f,
                0.6f);

            Primitive(
                PrimitiveType.Sphere, "Leaves", leaf, Vector3.zero,
                new Vector3(2.6f, 1.6f, 2.6f), leafMat, false);
        }
    }

    // ------------------------------------------------------------
    // Object helpers
    // ------------------------------------------------------------

    static GameObject MeshObject(
        string name, Mesh mesh, Material material, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    /// <summary>
    /// Creates a Unity primitive. solid = false removes its collider.
    /// </summary>
    static GameObject Primitive(
        PrimitiveType type, string name, Transform parent,
        Vector3 localPosition, Vector3 localScale,
        Material material, bool solid)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        if (!solid)
        {
            Collider collider = go.GetComponent<Collider>();

            if (collider != null)
            {
                collider.enabled = false;

                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }
        }

        return go;
    }
}
