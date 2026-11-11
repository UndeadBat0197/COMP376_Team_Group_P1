using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small procedural meshes used by the tree level: the hollow trunk,
/// the ring walls with a doorway, thorn cones and the acorn.
/// </summary>
public static class TreeMeshes
{
    class Builder
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();

        int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            vertices.Add(p);
            normals.Add(n);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        // Adds a triangle that faces "facing", whatever order the
        // corners were given in.
        public void Triangle(
            Vector3 a, Vector3 b, Vector3 c,
            Vector3 na, Vector3 nb, Vector3 nc,
            Vector2 ua, Vector2 ub, Vector2 uc,
            Vector3 facing)
        {
            int ia = Add(a, na, ua);
            int ib = Add(b, nb, ub);
            int ic = Add(c, nc, uc);

            Vector3 winding = Vector3.Cross(b - a, c - a);

            if (Vector3.Dot(winding, facing) >= 0f)
            {
                triangles.Add(ia); triangles.Add(ib); triangles.Add(ic);
            }
            else
            {
                triangles.Add(ia); triangles.Add(ic); triangles.Add(ib);
            }
        }

        // Flat-shaded triangle.
        public void FlatTriangle(
            Vector3 a, Vector3 b, Vector3 c,
            Vector2 ua, Vector2 ub, Vector2 uc,
            Vector3 facing)
        {
            Vector3 n = facing.normalized;
            Triangle(a, b, c, n, n, n, ua, ub, uc, facing);
        }

        // Quad a-b-c-d (corners in order around the quad).
        public void Quad(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
            Vector3 facing)
        {
            Triangle(a, b, c, na, nb, nc, ua, ub, uc, facing);
            Triangle(a, c, d, na, nc, nd, ua, uc, ud, facing);
        }

        public void FlatQuad(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
            Vector3 facing)
        {
            Vector3 n = facing.normalized;
            Quad(a, b, c, d, n, n, n, n, ua, ub, uc, ud, facing);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            if (vertices.Count > 65000)
            {
                mesh.indexFormat =
                    UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // Direction from the trunk axis to the surface at "degrees"
    // (0 = straight down, 90 = +X side). The axis is local Z.
    public static Vector3 Radial(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), -Mathf.Cos(r), 0f);
    }

    /// <summary>
    /// One wall of a tube around the Z axis. inward = true makes the
    /// surface face the axis (the inside of the trunk).
    ///
    /// A rectangular hole can be left out of it: holeHalfDegrees > 0
    /// cuts the area around holeDegrees between holeZStart and holeZEnd.
    /// With onlyHole = true the opposite happens and only that patch is
    /// built (used for the hatch that closes the hole).
    /// </summary>
    public static Mesh TubeSurface(
        string name, float radius, float zStart, float zEnd,
        int sides, bool inward, float tileSize,
        float holeDegrees = 0f, float holeHalfDegrees = 0f,
        float holeZStart = 0f, float holeZEnd = 0f,
        bool onlyHole = false)
    {
        Builder b = new Builder();

        bool hasHole = holeHalfDegrees > 0f && holeZEnd > holeZStart;

        // Cut the tube into rings, with cuts exactly at the hole's ends.
        List<float> cuts = new List<float>();
        cuts.Add(zStart);

        int rings = Mathf.Max(1, Mathf.RoundToInt((zEnd - zStart) / 4f));
        for (int r = 1; r < rings; r++)
            cuts.Add(Mathf.Lerp(zStart, zEnd, (float)r / rings));

        if (hasHole)
        {
            cuts.Add(holeZStart);
            cuts.Add(holeZEnd);
        }

        cuts.Add(zEnd);
        cuts.Sort();

        float circumference = 2f * Mathf.PI * radius;

        // Whole number of tiles around, so the texture has no seam.
        float tilesAround =
            Mathf.Max(1f, Mathf.Round(circumference / tileSize));

        for (int s = 0; s < sides; s++)
        {
            float a0 = 360f * s / sides;
            float a1 = 360f * (s + 1) / sides;

            bool angleInHole = hasHole &&
                IsInDoor((a0 + a1) * 0.5f, holeDegrees, holeHalfDegrees);

            Vector3 d0 = Radial(a0);
            Vector3 d1 = Radial(a1);

            Vector3 n0 = inward ? -d0 : d0;
            Vector3 n1 = inward ? -d1 : d1;

            float u0 = tilesAround * s / sides;
            float u1 = tilesAround * (s + 1) / sides;

            for (int r = 0; r < cuts.Count - 1; r++)
            {
                float z0 = cuts[r];
                float z1 = cuts[r + 1];

                if (z1 - z0 < 0.0001f)
                    continue;

                float zMid = (z0 + z1) * 0.5f;

                bool inHole =
                    angleInHole && zMid > holeZStart && zMid < holeZEnd;

                if (inHole != onlyHole)
                    continue;

                Vector3 p00 = d0 * radius + Vector3.forward * z0;
                Vector3 p10 = d1 * radius + Vector3.forward * z0;
                Vector3 p11 = d1 * radius + Vector3.forward * z1;
                Vector3 p01 = d0 * radius + Vector3.forward * z1;

                b.Quad(
                    p00, p10, p11, p01,
                    n0, n1, n1, n0,
                    new Vector2(u0, z0 / tileSize),
                    new Vector2(u1, z0 / tileSize),
                    new Vector2(u1, z1 / tileSize),
                    new Vector2(u0, z1 / tileSize),
                    (n0 + n1) * 0.5f);
            }
        }

        return b.ToMesh(name);
    }

    /// <summary>
    /// The four cut faces around a hole in the trunk wall, joining the
    /// inside surface to the bark.
    /// </summary>
    public static Mesh HoleRim(
        string name, float innerRadius, float outerRadius, int sides,
        float holeDegrees, float holeHalfDegrees,
        float holeZStart, float holeZEnd)
    {
        Builder b = new Builder();

        Vector2 uv0 = new Vector2(0f, 0f);
        Vector2 uv1 = new Vector2(1f, 0f);
        Vector2 uv2 = new Vector2(1f, 0.2f);
        Vector2 uv3 = new Vector2(0f, 0.2f);

        for (int s = 0; s < sides; s++)
        {
            float a0 = 360f * s / sides;
            float a1 = 360f * (s + 1) / sides;
            float step = 360f / sides;
            float mid = (a0 + a1) * 0.5f;

            bool inHole = IsInDoor(mid, holeDegrees, holeHalfDegrees);
            bool nextInHole =
                IsInDoor(mid + step, holeDegrees, holeHalfDegrees);

            Vector3 d0 = Radial(a0);
            Vector3 d1 = Radial(a1);

            if (inHole)
            {
                // Faces at the two ends of the hole (along the trunk).
                b.FlatQuad(
                    d0 * innerRadius + Vector3.forward * holeZStart,
                    d1 * innerRadius + Vector3.forward * holeZStart,
                    d1 * outerRadius + Vector3.forward * holeZStart,
                    d0 * outerRadius + Vector3.forward * holeZStart,
                    uv0, uv1, uv2, uv3,
                    Vector3.forward);

                b.FlatQuad(
                    d0 * innerRadius + Vector3.forward * holeZEnd,
                    d1 * innerRadius + Vector3.forward * holeZEnd,
                    d1 * outerRadius + Vector3.forward * holeZEnd,
                    d0 * outerRadius + Vector3.forward * holeZEnd,
                    uv0, uv1, uv2, uv3,
                    Vector3.back);
            }

            if (inHole != nextInHole)
            {
                // Faces along the sides of the hole.
                Vector3 tangent = new Vector3(-d1.y, d1.x, 0f);
                Vector3 facing = inHole ? -tangent : tangent;

                b.FlatQuad(
                    d1 * innerRadius + Vector3.forward * holeZStart,
                    d1 * outerRadius + Vector3.forward * holeZStart,
                    d1 * outerRadius + Vector3.forward * holeZEnd,
                    d1 * innerRadius + Vector3.forward * holeZEnd,
                    uv0, uv1, uv2, uv3,
                    facing);
            }
        }

        return b.ToMesh(name);
    }

    /// <summary>
    /// A flat, door-shaped panel on a ring wall (facing -Z). Used to
    /// paint a fake doorway.
    /// </summary>
    public static Mesh DoorPanel(
        string name, float radius, int sides,
        float doorDegrees, float doorHalfDegrees, float doorHeight)
    {
        Builder b = new Builder();
        float doorRadius = Mathf.Max(0.5f, radius - doorHeight);

        for (int s = 0; s < sides; s++)
        {
            float a0 = 360f * s / sides;
            float a1 = 360f * (s + 1) / sides;

            if (!IsInDoor((a0 + a1) * 0.5f, doorDegrees, doorHalfDegrees))
                continue;

            Vector3 d0 = Radial(a0);
            Vector3 d1 = Radial(a1);

            b.FlatQuad(
                d0 * doorRadius, d1 * doorRadius,
                d1 * radius, d0 * radius,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
                Vector3.back);
        }

        return b.ToMesh(name);
    }

    /// <summary>
    /// A chunky low-poly boulder: a ball with flat faces and a slightly
    /// lumpy outline.
    /// </summary>
    public static Mesh Rock(string name, float radius, float seed)
    {
        float g = (1f + Mathf.Sqrt(5f)) * 0.5f;

        Vector3[] corners =
        {
            new Vector3(-1f, g, 0f), new Vector3(1f, g, 0f),
            new Vector3(-1f, -g, 0f), new Vector3(1f, -g, 0f),
            new Vector3(0f, -1f, g), new Vector3(0f, 1f, g),
            new Vector3(0f, -1f, -g), new Vector3(0f, 1f, -g),
            new Vector3(g, 0f, -1f), new Vector3(g, 0f, 1f),
            new Vector3(-g, 0f, -1f), new Vector3(-g, 0f, 1f),
        };

        int[] faces =
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1,
        };

        Builder b = new Builder();

        for (int f = 0; f < faces.Length; f += 3)
        {
            Vector3 a = corners[faces[f]].normalized;
            Vector3 c = corners[faces[f + 1]].normalized;
            Vector3 d = corners[faces[f + 2]].normalized;

            // Split each face into four for a rounder shape.
            Vector3 ac = (a + c).normalized;
            Vector3 cd = (c + d).normalized;
            Vector3 da = (d + a).normalized;

            RockFace(b, a, ac, da, radius, seed);
            RockFace(b, c, cd, ac, radius, seed);
            RockFace(b, d, da, cd, radius, seed);
            RockFace(b, ac, cd, da, radius, seed);
        }

        return b.ToMesh(name);
    }

    static void RockFace(
        Builder b, Vector3 a, Vector3 c, Vector3 d, float radius, float seed)
    {
        Vector3 pa = RockPoint(a, radius, seed);
        Vector3 pc = RockPoint(c, radius, seed);
        Vector3 pd = RockPoint(d, radius, seed);

        b.FlatTriangle(
            pa, pc, pd,
            new Vector2(a.x, a.y) * 0.5f, new Vector2(c.x, c.y) * 0.5f,
            new Vector2(d.x, d.y) * 0.5f,
            pa + pc + pd);
    }

    // The same direction always gives the same point, so neighbouring
    // faces meet without gaps.
    static Vector3 RockPoint(Vector3 direction, float radius, float seed)
    {
        float noise = Mathf.PerlinNoise(
            direction.x * 1.9f + seed + direction.z * 0.7f,
            direction.y * 1.9f + seed * 0.37f - direction.z * 1.3f);

        return direction * radius * (0.88f + 0.2f * noise);
    }

    /// <summary>
    /// Flat ring (the cut end of the trunk) at z, facing "facingZ".
    /// The texture is mapped once across the whole disc.
    /// </summary>
    public static Mesh EndRing(
        string name, float innerRadius, float outerRadius,
        float z, float facingZ, int sides)
    {
        Builder b = new Builder();
        Vector3 facing = new Vector3(0f, 0f, facingZ);

        for (int s = 0; s < sides; s++)
        {
            Vector3 d0 = Radial(360f * s / sides);
            Vector3 d1 = Radial(360f * (s + 1) / sides);

            Vector3 a = d0 * innerRadius + Vector3.forward * z;
            Vector3 c = d1 * outerRadius + Vector3.forward * z;
            Vector3 bb = d0 * outerRadius + Vector3.forward * z;
            Vector3 d = d1 * innerRadius + Vector3.forward * z;

            b.FlatQuad(
                a, bb, c, d,
                DiscUV(a, outerRadius), DiscUV(bb, outerRadius),
                DiscUV(c, outerRadius), DiscUV(d, outerRadius),
                facing);
        }

        return b.ToMesh(name);
    }

    static Vector2 DiscUV(Vector3 p, float radius)
    {
        return new Vector2(
            p.x / (2f * radius) + 0.5f,
            p.y / (2f * radius) + 0.5f);
    }

    /// <summary>
    /// A disc that blocks the trunk, with a doorway cut into its rim.
    /// The doorway is centred on doorDegrees and is doorHeight tall,
    /// measured from the trunk floor towards the axis.
    /// </summary>
    public static Mesh RingWall(
        string name, float radius, float thickness, int sides,
        float doorDegrees, float doorHalfDegrees, float doorHeight)
    {
        Builder b = new Builder();

        float half = thickness * 0.5f;
        float doorRadius = Mathf.Max(0.5f, radius - doorHeight);

        Vector3 front = Vector3.back * half;     // faces -Z
        Vector3 back = Vector3.forward * half;   // faces +Z

        for (int s = 0; s < sides; s++)
        {
            float a0 = 360f * s / sides;
            float a1 = 360f * (s + 1) / sides;
            float mid = (a0 + a1) * 0.5f;

            bool inDoor = IsInDoor(mid, doorDegrees, doorHalfDegrees);
            float r = inDoor ? doorRadius : radius;

            Vector3 d0 = Radial(a0);
            Vector3 d1 = Radial(a1);

            Vector3 p0 = d0 * r;
            Vector3 p1 = d1 * r;

            // Front and back faces of this wedge.
            b.FlatTriangle(
                front, p0 + front, p1 + front,
                DiscUV(Vector3.zero, radius),
                DiscUV(p0, radius), DiscUV(p1, radius),
                Vector3.back);

            b.FlatTriangle(
                back, p0 + back, p1 + back,
                DiscUV(Vector3.zero, radius),
                DiscUV(p0, radius), DiscUV(p1, radius),
                Vector3.forward);

            if (inDoor)
            {
                // Top of the doorway (faces the floor).
                Vector3 n = (d0 + d1).normalized;

                b.FlatQuad(
                    p0 + front, p1 + front, p1 + back, p0 + back,
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 0.1f), new Vector2(0f, 0.1f),
                    n);
            }

            // Sides of the doorway, where a door wedge meets a
            // full wedge.
            float nextMid = mid + 360f / sides;
            bool nextInDoor =
                IsInDoor(nextMid, doorDegrees, doorHalfDegrees);

            if (inDoor != nextInDoor)
            {
                Vector3 inner = d1 * doorRadius;
                Vector3 outer = d1 * radius;

                // Tangent pointing into the doorway.
                Vector3 tangent = new Vector3(-d1.y, d1.x, 0f);
                Vector3 facing = inDoor ? -tangent : tangent;

                b.FlatQuad(
                    inner + front, outer + front,
                    outer + back, inner + back,
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 0.1f), new Vector2(0f, 0.1f),
                    facing);
            }
        }

        return b.ToMesh(name);
    }

    static bool IsInDoor(float degrees, float doorDegrees, float halfDegrees)
    {
        return Mathf.Abs(Mathf.DeltaAngle(degrees, doorDegrees))
            <= halfDegrees;
    }

    /// <summary>Cone standing on y = 0 with its tip at y = height.</summary>
    public static Mesh Cone(string name, float radius, float height, int sides)
    {
        Builder b = new Builder();
        Vector3 tip = new Vector3(0f, height, 0f);

        for (int s = 0; s < sides; s++)
        {
            float a0 = 2f * Mathf.PI * s / sides;
            float a1 = 2f * Mathf.PI * (s + 1) / sides;

            Vector3 p0 = new Vector3(
                Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(
                Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

            Vector3 outward = (p0 + p1) * 0.5f;
            outward.y = radius * radius / Mathf.Max(0.01f, height);

            b.FlatTriangle(
                p0, p1, tip,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 1f),
                outward);

            b.FlatTriangle(
                p0, p1, Vector3.zero,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector3.down);
        }

        return b.ToMesh(name);
    }

    /// <summary>
    /// Spins a 2D outline (x = distance from the Y axis, y = height)
    /// around the Y axis. Used for the acorn.
    /// </summary>
    public static Mesh Lathe(string name, Vector2[] outline, int sides)
    {
        Builder b = new Builder();

        for (int i = 0; i < outline.Length - 1; i++)
        {
            Vector2 lower = outline[i];
            Vector2 upper = outline[i + 1];

            // Normal of this band of the outline, in the (radius, y) plane.
            Vector2 edge = upper - lower;
            Vector2 bandNormal = new Vector2(edge.y, -edge.x);

            if (bandNormal.sqrMagnitude < 0.000001f)
                bandNormal = new Vector2(1f, 0f);

            bandNormal.Normalize();

            for (int s = 0; s < sides; s++)
            {
                float a0 = 2f * Mathf.PI * s / sides;
                float a1 = 2f * Mathf.PI * (s + 1) / sides;

                Vector3 dir0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 dir1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                Vector3 n0 = dir0 * bandNormal.x + Vector3.up * bandNormal.y;
                Vector3 n1 = dir1 * bandNormal.x + Vector3.up * bandNormal.y;

                Vector3 p00 = dir0 * lower.x + Vector3.up * lower.y;
                Vector3 p10 = dir1 * lower.x + Vector3.up * lower.y;
                Vector3 p11 = dir1 * upper.x + Vector3.up * upper.y;
                Vector3 p01 = dir0 * upper.x + Vector3.up * upper.y;

                float u0 = (float)s / sides;
                float u1 = (float)(s + 1) / sides;
                float v0 = (float)i / (outline.Length - 1);
                float v1 = (float)(i + 1) / (outline.Length - 1);

                b.Quad(
                    p00, p10, p11, p01,
                    n0, n1, n1, n0,
                    new Vector2(u0, v0), new Vector2(u1, v0),
                    new Vector2(u1, v1), new Vector2(u0, v1),
                    (n0 + n1) * 0.5f);
            }
        }

        return b.ToMesh(name);
    }
}
