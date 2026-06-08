using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider))]
public class TwoMaterialRoadCube : MonoBehaviour
{
    [Header("Cube Size")]
    public Vector3 size = Vector3.one;

    [Header("Materials")]
    public Material topRoadMaterial;
    public Material sideDirtMaterial;

    private Mesh generatedMesh;

    private void OnEnable()
    {
        Generate();
    }

    private void OnValidate()
    {
        Generate();
    }

    private void Generate()
    {
        if (size.x <= 0f) size.x = 1f;
        if (size.y <= 0f) size.y = 1f;
        if (size.z <= 0f) size.z = 1f;

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "Two Material Road Cube";
        }
        else
        {
            generatedMesh.Clear();
        }

        float x = size.x * 0.5f;
        float y = size.y * 0.5f;
        float z = size.z * 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        List<int> topTriangles = new List<int>();
        List<int> sideTriangles = new List<int>();

        // SubMesh 0: Top road face
        AddQuad(
            vertices,
            uvs,
            topTriangles,
            new Vector3(-x, y, -z),
            new Vector3(-x, y, z),
            new Vector3(x, y, z),
            new Vector3(x, y, -z)
        );

        // SubMesh 1: Bottom + four sides
        // Bottom
        AddQuad(
            vertices,
            uvs,
            sideTriangles,
            new Vector3(-x, -y, -z),
            new Vector3(x, -y, -z),
            new Vector3(x, -y, z),
            new Vector3(-x, -y, z)
        );

        // Front
        AddQuad(
            vertices,
            uvs,
            sideTriangles,
            new Vector3(-x, -y, -z),
            new Vector3(-x, y, -z),
            new Vector3(x, y, -z),
            new Vector3(x, -y, -z)
        );

        // Back
        AddQuad(
            vertices,
            uvs,
            sideTriangles,
            new Vector3(-x, -y, z),
            new Vector3(x, -y, z),
            new Vector3(x, y, z),
            new Vector3(-x, y, z)
        );

        // Left
        AddQuad(
            vertices,
            uvs,
            sideTriangles,
            new Vector3(-x, -y, -z),
            new Vector3(-x, -y, z),
            new Vector3(-x, y, z),
            new Vector3(-x, y, -z)
        );

        // Right
        AddQuad(
            vertices,
            uvs,
            sideTriangles,
            new Vector3(x, -y, -z),
            new Vector3(x, y, -z),
            new Vector3(x, y, z),
            new Vector3(x, -y, z)
        );

        generatedMesh.SetVertices(vertices);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.subMeshCount = 2;

        generatedMesh.SetTriangles(topTriangles, 0);
        generatedMesh.SetTriangles(sideTriangles, 1);

        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = generatedMesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (topRoadMaterial != null && sideDirtMaterial != null)
        {
            meshRenderer.sharedMaterials = new Material[]
            {
                topRoadMaterial,
                sideDirtMaterial
            };
        }
        else if (meshRenderer.sharedMaterials.Length < 2)
        {
            meshRenderer.sharedMaterials = new Material[2];
        }

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.center = Vector3.zero;
        boxCollider.size = size;
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3)
    {
        int startIndex = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(1f, 0f));

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }
}