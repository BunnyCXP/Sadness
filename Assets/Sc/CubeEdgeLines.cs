using UnityEngine;

[ExecuteAlways]
public class CubeEdgeLines : MonoBehaviour
{
    [Header("边线设置")]
    public Material edgeMaterial;
    public float edgeThickness = 0.03f;

    [Header("尺寸")]
    public Vector3 boxSize = Vector3.one;

    [Header("自动刷新")]
    public bool rebuildNow = false;

    private Transform edgeRoot;

    void OnEnable()
    {
        BuildEdges();
    }

    void Update()
    {
        if (rebuildNow)
        {
            rebuildNow = false;
            BuildEdges();
        }
    }

    public void BuildEdges()
    {
        ClearEdges();

        GameObject rootObj = new GameObject("Generated_EdgeLines");
        rootObj.transform.SetParent(transform, false);
        edgeRoot = rootObj.transform;

        float x = boxSize.x * 0.5f;
        float y = boxSize.y * 0.5f;
        float z = boxSize.z * 0.5f;

        Vector3[] corners =
        {
            new Vector3(-x, -y, -z),
            new Vector3( x, -y, -z),
            new Vector3( x, -y,  z),
            new Vector3(-x, -y,  z),

            new Vector3(-x,  y, -z),
            new Vector3( x,  y, -z),
            new Vector3( x,  y,  z),
            new Vector3(-x,  y,  z),
        };

        CreateEdge(corners[0], corners[1]);
        CreateEdge(corners[1], corners[2]);
        CreateEdge(corners[2], corners[3]);
        CreateEdge(corners[3], corners[0]);

        CreateEdge(corners[4], corners[5]);
        CreateEdge(corners[5], corners[6]);
        CreateEdge(corners[6], corners[7]);
        CreateEdge(corners[7], corners[4]);

        CreateEdge(corners[0], corners[4]);
        CreateEdge(corners[1], corners[5]);
        CreateEdge(corners[2], corners[6]);
        CreateEdge(corners[3], corners[7]);
    }

    void CreateEdge(Vector3 start, Vector3 end)
    {
        GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edge.name = "EdgeLine";
        edge.transform.SetParent(edgeRoot, false);

        Vector3 center = (start + end) * 0.5f;
        Vector3 direction = end - start;
        float length = direction.magnitude;

        edge.transform.localPosition = center;
        edge.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        edge.transform.localScale = new Vector3(edgeThickness, edgeThickness, length);

        Collider col = edge.GetComponent<Collider>();
        if (col != null)
            DestroyImmediate(col);

        MeshRenderer renderer = edge.GetComponent<MeshRenderer>();
        if (renderer != null && edgeMaterial != null)
            renderer.sharedMaterial = edgeMaterial;
    }

    void ClearEdges()
    {
        Transform old = transform.Find("Generated_EdgeLines");

        if (old != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(old.gameObject);
#else
            Destroy(old.gameObject);
#endif
        }
    }
}