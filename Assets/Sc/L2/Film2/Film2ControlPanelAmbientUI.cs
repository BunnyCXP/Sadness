using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds subtle ambient motion to Film 2's foreground control panel.
/// It does not process input or control lever gameplay.
/// </summary>
[DisallowMultipleComponent]
public class Film2ControlPanelAmbientUI : MonoBehaviour
{
    public RectTransform[] swayingPlants;
    public Image[] warningLights;
    public RectTransform scanLine;

    public float plantSwayAngle = 3f;
    public float plantSwaySpeed = 1.2f;
    public float warningBlinkSpeed = 1.8f;
    public float scanLineDistance = 300f;
    public float scanLineSpeed = 40f;

    private Quaternion[] plantBaseRotations;
    private Color[] warningBaseColors;
    private float scanLineStartX;

    private void Awake()
    {
        CacheInitialState();
    }

    private void OnEnable()
    {
        CacheInitialState();
    }

    private void Update()
    {
        AnimatePlants();
        AnimateWarningLights();
        AnimateScanLine();
    }

    private void CacheInitialState()
    {
        int plantCount = swayingPlants == null ? 0 : swayingPlants.Length;
        plantBaseRotations = new Quaternion[plantCount];

        for (int i = 0; i < plantCount; i++)
        {
            if (swayingPlants[i] != null)
                plantBaseRotations[i] = swayingPlants[i].localRotation;
        }

        int lightCount = warningLights == null ? 0 : warningLights.Length;
        warningBaseColors = new Color[lightCount];

        for (int i = 0; i < lightCount; i++)
        {
            if (warningLights[i] != null)
                warningBaseColors[i] = warningLights[i].color;
        }

        if (scanLine != null)
            scanLineStartX = scanLine.anchoredPosition.x;
    }

    private void AnimatePlants()
    {
        if (swayingPlants == null || plantBaseRotations == null)
            return;

        for (int i = 0; i < swayingPlants.Length; i++)
        {
            RectTransform plant = swayingPlants[i];

            if (plant == null)
                continue;

            float phase = i * 0.83f;
            float angle = Mathf.Sin(Time.time * plantSwaySpeed + phase) * plantSwayAngle;
            plant.localRotation = plantBaseRotations[i] * Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void AnimateWarningLights()
    {
        if (warningLights == null || warningBaseColors == null)
            return;

        for (int i = 0; i < warningLights.Length; i++)
        {
            Image light = warningLights[i];

            if (light == null)
                continue;

            float phase = i * 1.31f;
            float pulse = 0.62f + Mathf.Sin(Time.time * warningBlinkSpeed + phase) * 0.18f;
            Color color = warningBaseColors[i];
            color.a *= pulse;
            light.color = color;
        }
    }

    private void AnimateScanLine()
    {
        if (scanLine == null)
            return;

        float distance = Mathf.Max(0f, scanLineDistance);
        float offset = distance <= 0f
            ? 0f
            : Mathf.Repeat(Time.time * scanLineSpeed, distance) - distance * 0.5f;

        Vector2 position = scanLine.anchoredPosition;
        position.x = scanLineStartX + offset;
        scanLine.anchoredPosition = position;
    }
}
