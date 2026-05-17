using UnityEngine;

public class IllusionBlock : MonoBehaviour
{
    [Tooltip("普通的材质（有正常遮挡关系的Standard材质）")]
    public Material normalMaterial;

    [Tooltip("我们刚刚用Shader Graph做的透视魔法材质")]
    public Material illusionMaterial;

    private MeshRenderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        // 游戏一开始（在房间里时），强制使用正常材质
        meshRenderer.material = normalMaterial;
    }

    // 暴露给外部调用的方法：开启或关闭错觉
    public void SetIllusionMode(bool isActive)
    {
        if (isActive)
            meshRenderer.material = illusionMaterial;
        else
            meshRenderer.material = normalMaterial;
    }
}