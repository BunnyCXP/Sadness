using UnityEngine;

/// <summary>
/// Lightweight hover feedback for room props. Uses MaterialPropertyBlock so shared materials stay untouched.
/// </summary>
[DisallowMultipleComponent]
public class ObjectHoverFeedback : MonoBehaviour
{
    public Renderer[] glowRenderers;
    public GameObject outlineObject;
    public Color glowColor = new Color(1f, 0.85f, 0.35f, 1f);
    public float glowIntensity = 1.5f;
    public float pulseSpeed = 3f;
    public bool useEmissionGlow = true;
    public bool useOutlineObject = true;
    public bool useColorTintFallback = true;
    [Range(0f, 1f)] public float colorTintStrength = 0.35f;

    [SerializeField] private bool isHovering;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock propertyBlock;
    private Color[][] originalEmissionColors;
    private Color[][] originalBaseColors;

    void Awake()
    {
        ResolveReferences();
        SetHovering(false);
    }

    void Update()
    {
        if (!isHovering || !useEmissionGlow)
            return;

        float pulse = 0.82f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.18f;
        ApplyEmission(glowColor * (glowIntensity * pulse));
    }

    public void SetHovering(bool hovering)
    {
        isHovering = hovering;

        if (outlineObject != null && useOutlineObject)
            outlineObject.SetActive(hovering);

        if (useEmissionGlow || useColorTintFallback)
            ApplyEmission(hovering ? glowColor * glowIntensity : Color.black);
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ResolveReferences();

        if ((glowRenderers == null || glowRenderers.Length == 0) && outlineObject == null)
            Debug.LogWarning("ObjectHoverFeedback: no glow renderers or outline object assigned.", this);
    }

    private void ResolveReferences()
    {
        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<Renderer>(true);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (originalEmissionColors == null || originalEmissionColors.Length != glowRenderers.Length)
            CaptureOriginalColors();
    }

    private void ApplyEmission(Color color)
    {
        ResolveReferences();

        if (glowRenderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < glowRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = glowRenderers[rendererIndex];

            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material == null)
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

                if (useEmissionGlow && material.HasProperty(EmissionColorId))
                {
                    Color targetEmission = isHovering
                        ? color
                        : originalEmissionColors[rendererIndex][materialIndex];
                    propertyBlock.SetColor(EmissionColorId, targetEmission);
                }

                int baseColorProperty = material.HasProperty(BaseColorId)
                    ? BaseColorId
                    : material.HasProperty(ColorId)
                        ? ColorId
                        : -1;

                if (useColorTintFallback && baseColorProperty >= 0)
                {
                    Color original = originalBaseColors[rendererIndex][materialIndex];
                    Color targetBase = isHovering
                        ? Color.Lerp(original, glowColor, colorTintStrength)
                        : original;
                    targetBase.a = original.a;
                    propertyBlock.SetColor(baseColorProperty, targetBase);
                }

                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    private void CaptureOriginalColors()
    {
        originalEmissionColors = new Color[glowRenderers.Length][];
        originalBaseColors = new Color[glowRenderers.Length][];

        for (int rendererIndex = 0; rendererIndex < glowRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = glowRenderers[rendererIndex];
            Material[] materials = targetRenderer != null
                ? targetRenderer.sharedMaterials
                : new Material[0];
            originalEmissionColors[rendererIndex] = new Color[materials.Length];
            originalBaseColors[rendererIndex] = new Color[materials.Length];

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                originalEmissionColors[rendererIndex][materialIndex] =
                    material != null && material.HasProperty(EmissionColorId)
                        ? material.GetColor(EmissionColorId)
                        : Color.black;
                originalBaseColors[rendererIndex][materialIndex] =
                    material != null && material.HasProperty(BaseColorId)
                        ? material.GetColor(BaseColorId)
                        : material != null && material.HasProperty(ColorId)
                            ? material.GetColor(ColorId)
                            : Color.white;
            }
        }
    }
}
