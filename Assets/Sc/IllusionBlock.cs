using UnityEngine;

public class IllusionBlock : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("要切换材质的 Renderer。不填就自动使用当前物体上的 MeshRenderer。")]
    public MeshRenderer targetRenderer;

    [Header("Old Single Material Fallback")]
    [Tooltip("普通材质。可以不填；不填时会自动记录当前 Renderer 上的所有材质。")]
    public Material normalMaterial;

    [Tooltip("幻觉材质。如果只填这个，所有材质槽都会切成这个材质。")]
    public Material illusionMaterial;

    [Header("Two Material Mode")]
    [Tooltip("普通状态材质数组。可以不填；不填时自动记录当前材质槽。")]
    public Material[] normalMaterials;

    [Tooltip("幻觉状态材质数组。两个材质槽时，Element 0 = 顶面，Element 1 = 侧面/底面。")]
    public Material[] illusionMaterials;

    private Material[] cachedNormalMaterials;
    private bool isIllusionMode;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (targetRenderer != null &&
            (normalMaterials == null || normalMaterials.Length == 0) &&
            normalMaterial == null)
        {
            cachedNormalMaterials = CopyMaterials(targetRenderer.sharedMaterials);
        }

        SetIllusionMode(false);
    }

    private void Initialize()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();

        if (targetRenderer == null)
        {
            Debug.LogWarning($"{name}: IllusionBlock 找不到 MeshRenderer。");
            return;
        }

        if (normalMaterials != null && normalMaterials.Length > 0)
        {
            cachedNormalMaterials = CopyMaterials(normalMaterials);
        }
        else if (normalMaterial != null)
        {
            cachedNormalMaterials = BuildSameMaterialArray(targetRenderer.sharedMaterials.Length, normalMaterial);
        }
        else
        {
            cachedNormalMaterials = CopyMaterials(targetRenderer.sharedMaterials);
        }
    }

    public void SetIllusionMode(bool isActive)
    {
        if (targetRenderer == null)
            Initialize();

        if (targetRenderer == null)
            return;

        isIllusionMode = isActive;

        int slotCount = targetRenderer.sharedMaterials.Length;

        if (isIllusionMode)
        {
            Material[] targetMaterials;

            if (illusionMaterials != null && illusionMaterials.Length > 0)
            {
                targetMaterials = BuildMaterialArray(slotCount, illusionMaterials);
            }
            else if (illusionMaterial != null)
            {
                targetMaterials = BuildSameMaterialArray(slotCount, illusionMaterial);
            }
            else
            {
                Debug.LogWarning($"{name}: 没有设置幻觉材质。");
                return;
            }

            targetRenderer.sharedMaterials = targetMaterials;
        }
        else
        {
            if (cachedNormalMaterials == null || cachedNormalMaterials.Length == 0)
                Initialize();

            targetRenderer.sharedMaterials = BuildMaterialArray(slotCount, cachedNormalMaterials);
        }
    }

    public void ToggleIllusionMode()
    {
        SetIllusionMode(!isIllusionMode);
    }

    public bool IsIllusionMode()
    {
        return isIllusionMode;
    }

    private static Material[] BuildMaterialArray(int slotCount, Material[] source)
    {
        if (slotCount <= 0)
            slotCount = 1;

        Material[] result = new Material[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            if (source != null && source.Length > 0)
            {
                if (i < source.Length)
                    result[i] = source[i];
                else
                    result[i] = source[source.Length - 1];
            }
        }

        return result;
    }

    private static Material[] BuildSameMaterialArray(int slotCount, Material material)
    {
        if (slotCount <= 0)
            slotCount = 1;

        Material[] result = new Material[slotCount];

        for (int i = 0; i < slotCount; i++)
            result[i] = material;

        return result;
    }

    private static Material[] CopyMaterials(Material[] source)
    {
        if (source == null)
            return new Material[0];

        Material[] copy = new Material[source.Length];

        for (int i = 0; i < source.Length; i++)
            copy[i] = source[i];

        return copy;
    }
}