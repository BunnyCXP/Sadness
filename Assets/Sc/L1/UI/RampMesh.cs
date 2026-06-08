using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class RampMesh : MonoBehaviour
{
    [Min(0.01f)] public float width = 2f;
    [Min(0.01f)] public float depth = 4f;
    [Min(0.01f)] public float height = 1.5f;

    private void OnEnable()
    {
        GenerateRamp();
    }

    private void OnValidate()
    {
        GenerateRamp();
    }

    private void GenerateRamp()
    {
        float w = width / 2f;
        float d = depth / 2f;

        Vector3 A = new Vector3(-w, 0, -d);       // 左前底
        Vector3 B = new Vector3(w, 0, -d);       // 右前底
        Vector3 C = new Vector3(-w, 0, d);       // 左后底
        Vector3 D = new Vector3(w, 0, d);       // 右后底
        Vector3 E = new Vector3(-w, height, d);   // 左后顶
        Vector3 F = new Vector3(w, height, d);   // 右后顶

        Mesh mesh = new Mesh();
        mesh.name = "Hard Edge Ramp Mesh";

        Vector3[] vertices =
        {
            // 底面
            A, B, D, C,

            // 后面竖直面
            C, D, F, E,

            // 上方斜面
            A, E, F, B,

            // 左侧三角面
            A, C, E,

            // 右侧三角面
            B, F, D
        };

        int[] triangles =
        {
            // 底面
            0, 1, 2,
            0, 2, 3,

            // 后面竖直面
            4, 5, 6,
            4, 6, 7,

            // 上方斜面
            8, 9, 10,
            8, 10, 11,

            // 左侧三角面
            12, 13, 14,

            // 右侧三角面
            15, 16, 17
        };

        Vector2[] uvs =
        {
            // 底面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),

            // 后面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),

            // 斜面
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),

            // 左三角面
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1),

            // 右三角面
            new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
    }
}