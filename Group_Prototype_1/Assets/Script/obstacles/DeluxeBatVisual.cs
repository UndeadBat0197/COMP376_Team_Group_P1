using System.Collections.Generic;
using UnityEngine;

// Procedural 3D bat appearance. Works without Blender, FBX, textures or external packages.
// This component does NOT move or damage the player: BatEnemy handles all gameplay.
public class DeluxeBatVisual : MonoBehaviour
{
    [Header("Visual animation")]
    public float wingFlapSpeed = 10f;
    public float wingFlapAngle = 27f;
    public float tailSway = 8f;

    Transform leftWing, rightWing, tail;
    readonly List<Renderer> bodyRenderers = new List<Renderer>();
    Material fur, belly, membrane, wingBones, eyeGlow, innerEar, white, noseMat, trim;
    float phase;
    bool built;
    bool alert;
    Color baseEye = new Color(0.85f, 0.25f, 1f);

    Shader LitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Sprites/Default");
        return s;
    }
    Material Mat(string name, Color color, bool emission = false)
    {
        Material m = new Material(LitShader());
        m.name = name;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 2f);
        }
        return m;
    }
    Transform Node(string name, Transform parent, Vector3 localPosition)
    {
        GameObject o = new GameObject(name);
        o.transform.SetParent(parent, false);
        o.transform.localPosition = localPosition;
        return o.transform;
    }
    GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        Renderer r = obj.GetComponent<Renderer>();
        if (r) { r.sharedMaterial = material; bodyRenderers.Add(r); }
        Collider c = obj.GetComponent<Collider>();
        if (c) { c.enabled = false; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
        return obj;
    }
    void Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        => Primitive(name, PrimitiveType.Sphere, parent, position, scale, mat);

    void Rod(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
    {
        Vector3 delta = b - a;
        if (delta.sqrMagnitude < .00001f) return;
        GameObject rod = Primitive(name, PrimitiveType.Cylinder, parent, (a + b) * .5f,
            new Vector3(thickness, delta.magnitude * .5f, thickness), material);
        rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
    }
    // A sculpted double-sided, smooth-shaded bat-wing membrane.
    void WingMembrane(string name, Transform pivot, float side)
    {
        // In local wing space, the wing attaches at the origin and opens out along x.
        Vector3[] outline = {
            new Vector3(0, 0, 0),
            new Vector3(side*.46f, .12f, .05f),
            new Vector3(side*1.05f, .30f, -.02f),
            new Vector3(side*1.55f, .38f, -.07f),
            new Vector3(side*1.82f, .17f, -.04f),
            new Vector3(side*1.57f, -.12f, -.08f),
            new Vector3(side*1.31f, -.21f, -.04f),
            new Vector3(side*1.10f, -.35f, -.02f),
            new Vector3(side*.86f, -.28f, .04f),
            new Vector3(side*.60f, -.42f, .02f),
            new Vector3(side*.35f, -.26f, .07f)
        };
        // Triangle fan centered in the membrane, with geometry visible from both sides.
        Vector3 center = new Vector3(side*.91f, -.02f, .08f);
        Vector3[] verts = new Vector3[outline.Length+1];
        verts[0] = center;
        for (int i=0;i<outline.Length;i++) verts[i+1]=outline[i];
        List<int> tris = new List<int>();
        for (int i=0;i<outline.Length;i++)
        {
            int j=(i+1)%outline.Length;
            tris.Add(0);tris.Add(i+1);tris.Add(j+1);
            tris.Add(0);tris.Add(j+1);tris.Add(i+1);
        }
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        GameObject obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(pivot,false);
        obj.GetComponent<MeshFilter>().sharedMesh=mesh;
        var rend=obj.GetComponent<MeshRenderer>(); rend.sharedMaterial=membrane; bodyRenderers.Add(rend);
        // Two long reinforced fingers and shorter segments make the wings legible from afar.
        Rod("Upper wing finger",pivot,outline[0],outline[3],.055f,wingBones);
        Rod("Outer wing finger",pivot,outline[0],outline[5],.033f,wingBones);
        Rod("Lower wing finger",pivot,outline[0],outline[7],.029f,wingBones);
        Rod("Lower edge",pivot,outline[0],outline[9],.025f,wingBones);
        Sphere("Claw tip",pivot,outline[3],new Vector3(.12f,.08f,.10f),trim);
        Sphere("Wing knuckle",pivot,outline[2],new Vector3(.13f,.1f,.13f),wingBones);
    }
    void Ear(string label, float side)
    {
        // Three overlapping tapered spheres form a pointy silhouette without sharp polygon artifacts.
        GameObject outer=Primitive(label+" outer",PrimitiveType.Sphere,transform,
            new Vector3(side*.27f,.70f,.12f),new Vector3(.23f,.41f,.19f),fur);
        outer.transform.localRotation=Quaternion.Euler(0,0,-side*20f);
        GameObject tip=Primitive(label+" tip",PrimitiveType.Sphere,transform,
            new Vector3(side*.34f,.94f,.12f),new Vector3(.12f,.31f,.12f),fur);
        tip.transform.localRotation=Quaternion.Euler(0,0,-side*23f);
        GameObject inner=Primitive(label+" inner",PrimitiveType.Sphere,transform,
            new Vector3(side*.29f,.72f,.29f),new Vector3(.10f,.30f,.035f),innerEar);
        inner.transform.localRotation=Quaternion.Euler(0,0,-side*20f);
    }
    public void Build()
    {
        if (built) return;
        built=true;
        fur=Mat("Midnight plum fur",new Color(.19f,.13f,.29f));
        belly=Mat("Violet belly",new Color(.42f,.31f,.55f));
        membrane=Mat("Silky indigo wing membranes",new Color(.37f,.24f,.62f));
        wingBones=Mat("Dark purple wing ribs",new Color(.15f,.09f,.29f));
        innerEar=Mat("Rose violet ears",new Color(.78f,.38f,.65f));
        eyeGlow=Mat("Magical eyes",baseEye,true);
        white=Mat("Small ivory fangs",new Color(.98f,.93f,.85f));
        noseMat=Mat("Pink nose",new Color(.91f,.48f,.73f));
        trim=Mat("Lavender details",new Color(.65f,.45f,.89f));

        // Local forward is +Z; flight controller rotates this root towards travel direction.
        Sphere("Pear shaped torso",transform,new Vector3(0,-.10f,-.06f),new Vector3(.65f,.81f,.55f),fur);
        Sphere("Soft violet chest",transform,new Vector3(0,-.14f,.25f),new Vector3(.42f,.57f,.13f),belly);
        Sphere("Round head",transform,new Vector3(0,.40f,.22f),new Vector3(.85f,.69f,.68f),fur);
        Sphere("Muzzle",transform,new Vector3(0,.19f,.55f),new Vector3(.36f,.20f,.22f),belly);
        Ear("Left ear",-1f); Ear("Right ear",1f);
        foreach(float side in new float[]{-1f,1f})
        {
            Sphere(side<0?"Left eye socket":"Right eye socket",transform,
                new Vector3(side*.23f,.44f,.515f),new Vector3(.27f,.29f,.12f),wingBones);
            Sphere(side<0?"Left luminous eye":"Right luminous eye",transform,
                new Vector3(side*.23f,.44f,.607f),new Vector3(.17f,.20f,.073f),eyeGlow);
            Sphere(side<0?"Left eye shine":"Right eye shine",transform,
                new Vector3(side*.195f,.49f,.675f),new Vector3(.052f,.062f,.02f),white);
            GameObject fang=Primitive(side<0?"Left fang":"Right fang",PrimitiveType.Capsule,transform,
                new Vector3(side*.14f,.055f,.69f),new Vector3(.055f,.12f,.055f),white);
            fang.transform.localRotation=Quaternion.Euler(0,0,side*12f);
            Sphere(side<0?"Left foot":"Right foot",transform,
                new Vector3(side*.19f,-.51f,.11f),new Vector3(.23f,.15f,.28f),wingBones);
        }
        Sphere("Heart shaped nose",transform,new Vector3(0,.235f,.695f),new Vector3(.14f,.085f,.07f),noseMat);
        tail=Node("Tail sway",transform,new Vector3(0,-.48f,-.18f));
        GameObject tailPiece=Primitive("Tail",PrimitiveType.Capsule,tail,new Vector3(0,-.19f,-.14f),
            new Vector3(.12f,.28f,.14f),fur);
        tailPiece.transform.localRotation=Quaternion.Euler(-28,0,0);
        // Symmetrical independently animated wing hinges.
        leftWing=Node("Left wing articulated pivot",transform,new Vector3(-.29f,.12f,-.06f));
        rightWing=Node("Right wing articulated pivot",transform,new Vector3(.29f,.12f,-.06f));
        WingMembrane("Left sculpted membrane",leftWing,-1f);
        WingMembrane("Right sculpted membrane",rightWing,1f);
        SetAlert(false);
    }
    void Awake() { Build(); }
    public void SetAlert(bool value)
    {
        alert=value;
        if (!eyeGlow) return;
        Color c=alert ? new Color(1f,.08f,.06f) : baseEye;
        if (eyeGlow.HasProperty("_BaseColor")) eyeGlow.SetColor("_BaseColor",c);
        if (eyeGlow.HasProperty("_Color")) eyeGlow.SetColor("_Color",c);
        if (eyeGlow.HasProperty("_EmissionColor")) eyeGlow.SetColor("_EmissionColor",c*2.5f);
        if (membrane && membrane.HasProperty("_BaseColor"))
            membrane.SetColor("_BaseColor", alert ? new Color(.76f,.23f,.34f) : new Color(.37f,.24f,.62f));
        if (membrane && membrane.HasProperty("_Color"))
            membrane.SetColor("_Color", alert ? new Color(.76f,.23f,.34f) : new Color(.37f,.24f,.62f));
    }
    void Update()
    {
        if (!built) return;
        phase += Time.deltaTime*wingFlapSpeed;
        float a=Mathf.Sin(phase)*wingFlapAngle;
        if (leftWing) leftWing.localRotation=Quaternion.Euler(0,0,a);
        if (rightWing) rightWing.localRotation=Quaternion.Euler(0,0,-a);
        if (tail) tail.localRotation=Quaternion.Euler(0,Mathf.Sin(phase*.35f)*tailSway,0);
    }
}
