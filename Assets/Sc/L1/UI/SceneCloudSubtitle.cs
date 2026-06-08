using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SceneCloudSubtitle : MonoBehaviour
{
    [Header("Trigger Target")]
    [Tooltip("火车根物体。用于 Trigger 判断、距离兜底触发、撞散检测。")]
    public Transform target;

    [Tooltip("留空则任何 Collider 都能触发；填 Train 之类的 Tag 则只允许对应 Tag 触发。")]
    public string requiredTag = "";

    public bool triggerOnce = true;
    public bool useTriggerStay = true;
    public bool verboseDebug = true;

    [Header("Distance Fallback Trigger")]
    [Tooltip("如果 Unity Trigger 没触发，就用 target 距离兜底触发字幕。")]
    public bool useDistanceFallback = true;

    [Tooltip("兜底触发中心。不拖则使用挂脚本物体的位置，也就是 SubtitleTrigger_01 的位置。")]
    public Transform fallbackTriggerCenter;

    [Tooltip("target 距离触发点多近时触发字幕。")]
    public float fallbackTriggerDistance = 1.2f;

    public bool drawFallbackGizmo = true;
    public bool logFallbackDistance = false;
    public float fallbackLogInterval = 0.5f;

    [Header("Text")]
    public TextMeshPro text;

    [Tooltip("真正被移动、缩放、面向相机的对象。不拖则默认使用 Text 自己。")]
    public Transform animatedRoot;

    [Tooltip("没有 Display Anchor 时，是否直接使用 Text 当前摆放的位置。建议勾选。")]
    public bool usePlacedTextPositionWhenNoAnchor = true;

    [TextArea(1, 3)]
    public string subtitleText;

    [Tooltip("字幕出现的位置。如果不拖，则使用 Text 当前摆放的位置。")]
    public Transform displayAnchor;

    public Vector3 worldOffset = Vector3.zero;

    [Header("Facing")]
    public bool faceCamera = true;
    public Camera targetCamera;

    [Header("Cloud Material")]
    public bool applyCloudMaterial = true;
    public Color faceColor = new Color(1f, 1f, 1f, 0.82f);
    public Color outlineColor = new Color(1f, 1f, 1f, 0.28f);
    public Color underlayColor = new Color(0.8f, 0.86f, 1f, 0.18f);
    [Range(0f, 1f)] public float outlineWidth = 0.18f;
    [Range(-1f, 1f)] public float faceDilate = 0.12f;
    [Range(0f, 1f)] public float underlaySoftness = 0.85f;

    [Header("Timing")]
    [Tooltip("字幕总时长。包括淡入、正常显示、撞散/自然散开。只要这个时间没结束，散开的字就会继续飘。")]
    public float subtitleTotalDuration = 3.0f;

    [Tooltip("淡入时长，也算在 Subtitle Total Duration 里面。")]
    public float fadeInDuration = 0.8f;

    [Tooltip("没有被车撞到时，结尾提前多久开始自然飘散。这个时间也包含在 Subtitle Total Duration 里面。")]
    public float naturalScatterLeadTime = 1.25f;

    [Tooltip("字幕出现后多久才允许被撞散，避免刚触发就立刻散。")]
    public float impactArmDelay = 0.15f;

    [Header("Appear Animation")]
    public float scaleFrom = 0.92f;
    public float scaleTo = 1f;
    public Vector3 appearLocalOffset = new Vector3(0f, -0.15f, 0f);
    public AnimationCurve appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Impact Detection")]
    [Tooltip("字幕被车撞散的判定区域。建议单独建 BoxCollider，勾 Is Trigger。")]
    public Collider impactCheckCollider;

    [Tooltip("如果没拖 Impact Check Collider，则用 target 到字幕中心的距离兜底。")]
    public float impactFallbackDistance = 0.9f;

    public LayerMask impactLayerMask = ~0;
    public QueryTriggerInteraction impactTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Hit Scatter")]
    public float hitScatterSpeed = 2.2f;
    public float hitUpwardBias = 0.35f;
    public float hitRandomness = 0.8f;
    public float hitSpinSpeed = 240f;

    [Header("Natural Scatter")]
    public float naturalScatterSpeed = 0.45f;
    public float naturalUpwardBias = 0.18f;
    public float naturalRandomness = 0.55f;
    public float naturalSpinSpeed = 55f;

    [Header("Letter Pieces")]
    public Transform letterPiecesParent;
    public float letterScaleMultiplier = 1f;

    [Header("Particles Optional")]
    public ParticleSystem appearParticles;
    public ParticleSystem hitScatterParticles;
    public ParticleSystem naturalScatterParticles;

    private bool hasTriggered;
    private bool isPlaying;
    private bool impactArmed;
    private bool scatterStarted;
    private bool scatterWasHit;

    private Coroutine currentRoutine;

    private Vector3 placedTextWorldPosition;
    private Vector3 originalPosition;
    private Vector3 originalLocalScale;

    private Material runtimeMaterial;
    private Renderer textRenderer;
    private Transform activeTarget;

    private float nextFallbackLogTime;
    private float scatterStartTime;
    private float scatterEndTime;

    private readonly List<LetterPiece> activePieces = new List<LetterPiece>();

    private struct LetterPiece
    {
        public TextMeshPro text;
        public Transform transform;
        public Vector3 startPosition;
        public Vector3 velocity;
        public Vector3 spinAxis;
        public float spinSpeed;
        public Color startColor;
    }

    void Start()
    {
        ResolveReferences();

        if (text == null)
        {
            Debug.LogWarning("SceneCloudSubtitle: TextMeshPro 3D reference is missing.");
            enabled = false;
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        textRenderer = text.GetComponent<Renderer>();

        CapturePlacedTextTransform();

        text.text = subtitleText;
        ApplyCloudMaterial();
        text.ForceMeshUpdate();

        SetAlpha(0f);

        if (animatedRoot != null)
        {
            animatedRoot.position = originalPosition + appearLocalOffset;
            animatedRoot.localScale = originalLocalScale * scaleFrom;
        }

        if (textRenderer != null)
            textRenderer.enabled = false;

        text.gameObject.SetActive(false);

        ResolveLetterPiecesParent();
    }

    void Update()
    {
        TryDistanceFallbackTrigger();

        if (!isPlaying)
            return;

        if (faceCamera)
            FaceCameraIfNeeded();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!useTriggerStay)
            return;

        TryTrigger(other);
    }

    private void TryTrigger(Collider other)
    {
        if (verboseDebug && other != null)
            Debug.Log("SceneCloudSubtitle: OnTrigger touched by " + other.name);

        if (triggerOnce && hasTriggered)
            return;

        if (isPlaying)
            return;

        if (!IsValidTriggerCollider(other))
            return;

        activeTarget = target != null ? target : other.transform.root;
        Show();
    }

    private void TryDistanceFallbackTrigger()
    {
        if (!useDistanceFallback)
            return;

        if (triggerOnce && hasTriggered)
            return;

        if (isPlaying)
            return;

        if (target == null)
            return;

        Vector3 triggerCenter = GetFallbackTriggerPosition();
        float distance = Vector3.Distance(target.position, triggerCenter);

        if (logFallbackDistance && Time.time >= nextFallbackLogTime)
        {
            nextFallbackLogTime = Time.time + fallbackLogInterval;

            Debug.Log(
                "SceneCloudSubtitle: fallback distance = " +
                distance.ToString("0.00") +
                ", target = " +
                target.name +
                ", center = " +
                triggerCenter
            );
        }

        if (distance <= fallbackTriggerDistance)
        {
            if (verboseDebug)
                Debug.Log("SceneCloudSubtitle: triggered by distance fallback. Distance = " + distance);

            activeTarget = target;
            Show();
        }
    }

    private bool IsValidTriggerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrEmpty(requiredTag))
        {
            bool selfMatch = other.CompareTag(requiredTag);
            bool rootMatch = other.transform.root != null && other.transform.root.CompareTag(requiredTag);

            if (!selfMatch && !rootMatch)
                return false;
        }

        if (target == null)
            return true;

        return other.transform == target ||
               other.transform.IsChildOf(target) ||
               target.IsChildOf(other.transform.root);
    }

    private void ResolveReferences()
    {
        if (text == null)
            text = GetComponent<TextMeshPro>();

        if (text == null)
            text = GetComponentInChildren<TextMeshPro>(true);

        if (animatedRoot == null && text != null)
            animatedRoot = text.transform;
    }

    private void ResolveLetterPiecesParent()
    {
        if (letterPiecesParent != null && text != null && letterPiecesParent == text.transform)
        {
            letterPiecesParent = text.transform.parent != null ? text.transform.parent : transform;
            return;
        }

        if (letterPiecesParent == null)
        {
            if (text != null && text.transform.parent != null)
                letterPiecesParent = text.transform.parent;
            else
                letterPiecesParent = transform;
        }
    }

    private void CapturePlacedTextTransform()
    {
        if (animatedRoot == null)
            ResolveReferences();

        if (animatedRoot != null)
        {
            placedTextWorldPosition = animatedRoot.position;
            originalPosition = GetSubtitleDisplayPosition();
            originalLocalScale = animatedRoot.localScale;
        }
        else
        {
            placedTextWorldPosition = transform.position;
            originalPosition = transform.position + worldOffset;
            originalLocalScale = transform.localScale;
        }
    }

    private Vector3 GetSubtitleDisplayPosition()
    {
        if (displayAnchor != null)
            return displayAnchor.position + worldOffset;

        if (usePlacedTextPositionWhenNoAnchor && animatedRoot != null)
            return placedTextWorldPosition + worldOffset;

        return transform.position + worldOffset;
    }

    private Vector3 GetFallbackTriggerPosition()
    {
        if (fallbackTriggerCenter != null)
            return fallbackTriggerCenter.position;

        return transform.position;
    }

    private Vector3 GetSubtitleCurrentCenter()
    {
        if (animatedRoot != null)
            return animatedRoot.position;

        return transform.position;
    }

    [ContextMenu("Test Show")]
    public void Show()
    {
        if (!Application.isPlaying)
        {
            PreviewShowInEditor();
            return;
        }

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowRoutine());
    }

    [ContextMenu("Preview Show In Editor")]
    public void PreviewShowInEditor()
    {
        ResolveReferences();

        if (text == null)
        {
            Debug.LogWarning("SceneCloudSubtitle: TextMeshPro 3D reference is missing.");
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        text.gameObject.SetActive(true);

        Renderer renderer = text.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = true;

        text.text = subtitleText;
        ApplyCloudMaterial();

        if (animatedRoot != null)
        {
            if (displayAnchor != null)
                animatedRoot.position = displayAnchor.position + worldOffset;

            animatedRoot.localScale = originalLocalScale == Vector3.zero ? Vector3.one : originalLocalScale * scaleTo;
        }

        SetAlpha(1f);
        FaceCameraIfNeeded();
    }

    [ContextMenu("Hide Preview In Editor")]
    public void HidePreviewInEditor()
    {
        ResolveReferences();

        SetAlpha(0f);

        if (text != null)
            text.gameObject.SetActive(false);

        Renderer renderer = text != null ? text.GetComponent<Renderer>() : null;
        if (renderer != null)
            renderer.enabled = false;

        ClearLetterPieces();
    }

    [ContextMenu("Reset Trigger State")]
    public void ResetTriggerState()
    {
        hasTriggered = false;
        isPlaying = false;
        impactArmed = false;
        scatterStarted = false;
        scatterWasHit = false;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = null;

        ClearLetterPieces();

        if (text != null)
        {
            SetAlpha(0f);
            text.gameObject.SetActive(false);
        }

        if (textRenderer != null)
            textRenderer.enabled = false;
    }

    IEnumerator ShowRoutine()
    {
        hasTriggered = true;
        isPlaying = true;
        impactArmed = false;
        scatterStarted = false;
        scatterWasHit = false;

        ClearLetterPieces();
        ResolveReferences();
        ResolveLetterPiecesParent();

        if (text == null || animatedRoot == null)
            yield break;

        if (targetCamera == null)
            targetCamera = Camera.main;

        originalPosition = GetSubtitleDisplayPosition();

        text.gameObject.SetActive(true);
        text.text = subtitleText;
        text.ForceMeshUpdate(true, true);

        if (textRenderer == null)
            textRenderer = text.GetComponent<Renderer>();

        if (textRenderer != null)
            textRenderer.enabled = true;

        ApplyCloudMaterial();

        animatedRoot.position = originalPosition + appearLocalOffset;
        animatedRoot.localScale = originalLocalScale * scaleFrom;
        SetAlpha(0f);

        if (appearParticles != null)
            appearParticles.Play(true);

        if (verboseDebug)
            Debug.Log("SceneCloudSubtitle: triggered - " + subtitleText);

        float totalDuration = Mathf.Max(0.05f, subtitleTotalDuration);
        float safeFadeDuration = Mathf.Clamp(fadeInDuration, 0.001f, totalDuration);
        float safeNaturalLead = Mathf.Clamp(naturalScatterLeadTime, 0.05f, totalDuration);

        float naturalScatterStartTime = Mathf.Max(safeFadeDuration, totalDuration - safeNaturalLead);
        naturalScatterStartTime = Mathf.Min(naturalScatterStartTime, totalDuration - 0.01f);

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            elapsed = Mathf.Min(elapsed, totalDuration);

            if (!scatterStarted)
            {
                UpdateOriginalTextBeforeScatter(elapsed, safeFadeDuration);

                if (!impactArmed && elapsed >= impactArmDelay)
                    impactArmed = true;

                if (impactArmed && IsTrainImpactingSubtitle(out Vector3 impactPoint))
                {
                    if (verboseDebug)
                        Debug.Log("SceneCloudSubtitle: hit scatter at " + elapsed.ToString("0.00") + "s.");

                    BeginScatter(true, impactPoint, elapsed, totalDuration);
                }
                else if (elapsed >= naturalScatterStartTime)
                {
                    if (verboseDebug)
                        Debug.Log("SceneCloudSubtitle: natural scatter at " + elapsed.ToString("0.00") + "s.");

                    BeginScatter(false, GetSubtitleCurrentCenter(), elapsed, totalDuration);
                }
            }

            if (scatterStarted)
            {
                UpdateScatterPieces(elapsed);
            }

            FaceCameraIfNeeded();

            yield return null;
        }

        Finish();
    }

    private void UpdateOriginalTextBeforeScatter(float elapsed, float safeFadeDuration)
    {
        float t = Mathf.Clamp01(elapsed / safeFadeDuration);
        float curvedT = EvaluateCurve(appearCurve, t);

        animatedRoot.position = Vector3.Lerp(originalPosition + appearLocalOffset, originalPosition, curvedT);
        animatedRoot.localScale = Vector3.Lerp(originalLocalScale * scaleFrom, originalLocalScale * scaleTo, curvedT);

        SetAlpha(curvedT);
    }

    private void BeginScatter(bool hitScatter, Vector3 impactPoint, float currentTime, float totalDuration)
    {
        if (scatterStarted)
            return;

        scatterStarted = true;
        scatterWasHit = hitScatter;
        scatterStartTime = currentTime;
        scatterEndTime = totalDuration;

        if (hitScatter && hitScatterParticles != null)
            hitScatterParticles.Play(true);

        if (!hitScatter && naturalScatterParticles != null)
            naturalScatterParticles.Play(true);

        BuildLetterPieces(hitScatter, impactPoint);

        SetAlpha(0f);

        if (textRenderer != null)
            textRenderer.enabled = false;

        if (text != null)
            text.gameObject.SetActive(false);
    }

    private void UpdateScatterPieces(float currentTime)
    {
        float duration = Mathf.Max(0.001f, scatterEndTime - scatterStartTime);
        float scatterElapsed = Mathf.Max(0f, currentTime - scatterStartTime);
        float t = Mathf.Clamp01(scatterElapsed / duration);
        float fade = 1f - t;

        for (int i = 0; i < activePieces.Count; i++)
        {
            LetterPiece piece = activePieces[i];

            if (piece.transform == null || piece.text == null)
                continue;

            Vector3 gravityCurve = Vector3.down * (0.25f * t * t);
            piece.transform.position = piece.startPosition + piece.velocity * scatterElapsed + gravityCurve;
            piece.transform.Rotate(piece.spinAxis, piece.spinSpeed * Time.deltaTime, Space.World);

            Color c = piece.startColor;
            c.a = fade;
            piece.text.color = c;

            if (piece.text.fontMaterial != null && piece.text.fontMaterial.HasProperty("_FaceColor"))
            {
                Color face = faceColor;
                face.a *= fade;
                piece.text.fontMaterial.SetColor("_FaceColor", face);
            }

            if (piece.text.fontMaterial != null && piece.text.fontMaterial.HasProperty("_OutlineColor"))
            {
                Color outline = outlineColor;
                outline.a *= fade;
                piece.text.fontMaterial.SetColor("_OutlineColor", outline);
            }

            if (piece.text.fontMaterial != null && piece.text.fontMaterial.HasProperty("_UnderlayColor"))
            {
                Color underlay = underlayColor;
                underlay.a *= fade;
                piece.text.fontMaterial.SetColor("_UnderlayColor", underlay);
            }
        }
    }

    private void Finish()
    {
        SetAlpha(0f);

        if (text != null)
            text.gameObject.SetActive(false);

        if (textRenderer != null)
            textRenderer.enabled = false;

        ClearLetterPieces();

        isPlaying = false;
        impactArmed = false;
        scatterStarted = false;
        currentRoutine = null;
    }

    private bool IsTrainImpactingSubtitle(out Vector3 impactPoint)
    {
        Transform checkTarget = activeTarget != null ? activeTarget : target;
        Vector3 subtitleCenter = GetSubtitleCurrentCenter();

        impactPoint = checkTarget != null ? checkTarget.position : subtitleCenter;

        if (checkTarget == null)
            return false;

        if (impactCheckCollider == null)
        {
            float distance = Vector3.Distance(checkTarget.position, subtitleCenter);
            return distance <= impactFallbackDistance;
        }

        Collider[] hits = GetImpactOverlaps();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];

            if (hit == null)
                continue;

            if (hit.transform == checkTarget || hit.transform.IsChildOf(checkTarget))
            {
                impactPoint = hit.ClosestPoint(subtitleCenter);
                return true;
            }
        }

        return false;
    }

    private Collider[] GetImpactOverlaps()
    {
        if (impactCheckCollider is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;

            return Physics.OverlapBox(
                center,
                halfExtents,
                box.transform.rotation,
                impactLayerMask,
                impactTriggerInteraction
            );
        }

        if (impactCheckCollider is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);

            float maxScale = Mathf.Max(
                Mathf.Abs(sphere.transform.lossyScale.x),
                Mathf.Abs(sphere.transform.lossyScale.y),
                Mathf.Abs(sphere.transform.lossyScale.z)
            );

            return Physics.OverlapSphere(
                center,
                sphere.radius * maxScale,
                impactLayerMask,
                impactTriggerInteraction
            );
        }

        Bounds bounds = impactCheckCollider.bounds;

        return Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            impactLayerMask,
            impactTriggerInteraction
        );
    }

    private void BuildLetterPieces(bool hitScatter, Vector3 impactPoint)
    {
        ClearLetterPieces();

        if (text == null)
            return;

        text.gameObject.SetActive(true);
        text.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = text.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            char character = charInfo.character;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 localCenter =
                (vertices[vertexIndex + 0] +
                 vertices[vertexIndex + 1] +
                 vertices[vertexIndex + 2] +
                 vertices[vertexIndex + 3]) * 0.25f;

            Vector3 worldCenter = text.transform.TransformPoint(localCenter);

            GameObject go = new GameObject("CloudLetter_" + character);

            if (letterPiecesParent != null)
                go.transform.SetParent(letterPiecesParent, true);

            go.transform.position = worldCenter;
            go.transform.rotation = text.transform.rotation;
            go.transform.localScale = text.transform.lossyScale * letterScaleMultiplier;

            TextMeshPro letterText = go.AddComponent<TextMeshPro>();
            CopyTextSettings(letterText, character);

            Vector3 direction;

            if (hitScatter)
            {
                direction = worldCenter - impactPoint;

                if (direction.sqrMagnitude < 0.001f)
                    direction = Random.onUnitSphere;

                direction.Normalize();
                direction += Random.insideUnitSphere * hitRandomness;
                direction += Vector3.up * hitUpwardBias;
                direction.Normalize();
            }
            else
            {
                direction = Random.insideUnitSphere * naturalRandomness;
                direction += Vector3.up * naturalUpwardBias;

                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector3.up;

                direction.Normalize();
            }

            float speed = hitScatter
                ? Random.Range(hitScatterSpeed * 0.65f, hitScatterSpeed * 1.25f)
                : Random.Range(naturalScatterSpeed * 0.6f, naturalScatterSpeed * 1.4f);

            float spinSpeed = hitScatter
                ? Random.Range(hitSpinSpeed * 0.65f, hitSpinSpeed * 1.25f)
                : Random.Range(naturalSpinSpeed * 0.65f, naturalSpinSpeed * 1.25f);

            activePieces.Add(new LetterPiece
            {
                text = letterText,
                transform = go.transform,
                startPosition = worldCenter,
                velocity = direction * speed,
                spinAxis = Random.onUnitSphere,
                spinSpeed = spinSpeed,
                startColor = letterText.color
            });
        }
    }

    private void CopyTextSettings(TextMeshPro letterText, char character)
    {
        letterText.text = character.ToString();
        letterText.font = text.font;
        letterText.fontSize = text.fontSize;
        letterText.alignment = TextAlignmentOptions.Center;
        letterText.textWrappingMode = TextWrappingModes.NoWrap;
        letterText.richText = false;
        letterText.color = text.color;

        if (runtimeMaterial != null)
            letterText.fontMaterial = new Material(runtimeMaterial);
        else if (text.fontMaterial != null)
            letterText.fontMaterial = new Material(text.fontMaterial);

        letterText.ForceMeshUpdate(true, true);
    }

    private void ClearLetterPieces()
    {
        for (int i = 0; i < activePieces.Count; i++)
        {
            if (activePieces[i].transform != null)
            {
                if (Application.isPlaying)
                    Destroy(activePieces[i].transform.gameObject);
                else
                    DestroyImmediate(activePieces[i].transform.gameObject);
            }
        }

        activePieces.Clear();
    }

    public void SetAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (text != null)
        {
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        SetMaterialColorAlpha("_FaceColor", faceColor, alpha);
        SetMaterialColorAlpha("_OutlineColor", outlineColor, alpha);
        SetMaterialColorAlpha("_UnderlayColor", underlayColor, alpha);
    }

    void FaceCameraIfNeeded()
    {
        if (!faceCamera)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Transform root = animatedRoot != null ? animatedRoot : transform;

        Vector3 direction = root.position - targetCamera.transform.position;

        if (direction.sqrMagnitude > 0.0001f)
            root.rotation = Quaternion.LookRotation(direction.normalized, targetCamera.transform.up);
    }

    void ApplyCloudMaterial()
    {
        if (!applyCloudMaterial || text == null)
            return;

        if (runtimeMaterial == null)
        {
            Material sourceMaterial = text.fontSharedMaterial != null ? text.fontSharedMaterial : text.fontMaterial;

            if (sourceMaterial == null)
                return;

            runtimeMaterial = new Material(sourceMaterial);
        }

        text.fontMaterial = runtimeMaterial;

        SetMaterialColor("_FaceColor", faceColor);
        SetMaterialColor("_OutlineColor", outlineColor);
        SetMaterialColor("_UnderlayColor", underlayColor);
        SetMaterialFloat("_OutlineWidth", outlineWidth);
        SetMaterialFloat("_FaceDilate", faceDilate);
        SetMaterialFloat("_UnderlaySoftness", underlaySoftness);
    }

    void SetMaterialColorAlpha(string propertyName, Color baseColor, float alpha)
    {
        Color color = baseColor;
        color.a *= alpha;
        SetMaterialColor(propertyName, color);
    }

    void SetMaterialColor(string propertyName, Color value)
    {
        if (runtimeMaterial != null && runtimeMaterial.HasProperty(propertyName))
            runtimeMaterial.SetColor(propertyName, value);
    }

    void SetMaterialFloat(string propertyName, float value)
    {
        if (runtimeMaterial != null && runtimeMaterial.HasProperty(propertyName))
            runtimeMaterial.SetFloat(propertyName, value);
    }

    private float EvaluateCurve(AnimationCurve curve, float t)
    {
        if (curve == null || curve.length == 0)
            return t;

        return Mathf.Clamp01(curve.Evaluate(t));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 fallbackCenter = fallbackTriggerCenter != null
            ? fallbackTriggerCenter.position
            : transform.position;

        if (drawFallbackGizmo)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.25f);
            Gizmos.DrawWireSphere(fallbackCenter, fallbackTriggerDistance);
        }

        if (impactCheckCollider == null)
            return;

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);

        if (impactCheckCollider is BoxCollider box)
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        else
        {
            Gizmos.DrawWireCube(impactCheckCollider.bounds.center, impactCheckCollider.bounds.size);
        }
    }
}