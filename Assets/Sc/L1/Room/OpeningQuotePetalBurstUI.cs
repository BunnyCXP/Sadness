using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OpeningQuotePetalBurstUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("花瓣 UI 预制体。用一个 Image 就行，可以先用默认白色小图片。")]
    public Image petalPrefab;

    [Tooltip("花瓣生成在哪个 RectTransform 下。一般填 OpeningQuoteGroup。")]
    public RectTransform targetParent;

    [Tooltip("从哪里生成花瓣。一般填 OpeningQuoteText 的 RectTransform。")]
    public RectTransform spawnRect;

    [Header("整句爆发数量")]
    public int petalCount = 36;

    [Header("生成范围")]
    public float spawnWidthMultiplier = 0.85f;
    public float spawnHeightMultiplier = 0.35f;

    [Header("运动：右上随风飘")]
    [Tooltip("花瓣最短存在时间")]
    public float lifetimeMin = 3.0f;

    [Tooltip("花瓣最长存在时间")]
    public float lifetimeMax = 4.5f;

    [Tooltip("水平漂移最小值。正数代表往右")]
    public float horizontalDriftMin = 220f;

    [Tooltip("水平漂移最大值。正数代表往右")]
    public float horizontalDriftMax = 480f;

    [Tooltip("向上漂移最小值")]
    public float upwardDriftMin = 100f;

    [Tooltip("向上漂移最大值")]
    public float upwardDriftMax = 260f;

    [Tooltip("额外向右推一点，让风感更明显")]
    public float windBoostX = 80f;

    [Tooltip("额外向上推一点，让轨迹更柔和")]
    public float windBoostY = 35f;

    [Tooltip("横向轻微摆动强度，模拟风")]
    public float wiggleStrength = 14f;

    [Tooltip("摆动速度最小值")]
    public float wiggleSpeedMin = 0.5f;

    [Tooltip("摆动速度最大值")]
    public float wiggleSpeedMax = 1.2f;

    [Header("旋转")]
    [Tooltip("旋转速度最小值，调低后不会转太快")]
    public float rotateSpeedMin = -20f;

    [Tooltip("旋转速度最大值，调低后不会转太快")]
    public float rotateSpeedMax = 20f;

    [Header("大小")]
    public float startScaleMin = 0.28f;
    public float startScaleMax = 0.55f;

    public float endScaleMin = 0.12f;
    public float endScaleMax = 0.24f;

    [Header("伪失焦")]
    public bool useDefocusGhost = true;

    [Tooltip("从生命周期多少比例开始失焦。0.45 = 后 55% 开始。")]
    [Range(0f, 1f)]
    public float defocusStartNormalized = 0.45f;

    [Tooltip("失焦残影最大放大倍数")]
    public float defocusGhostScale = 2.6f;

    [Tooltip("失焦残影最大透明度")]
    [Range(0f, 1f)]
    public float defocusGhostMaxAlpha = 0.18f;

    [Tooltip("失焦后主花瓣额外变淡")]
    [Range(0f, 1f)]
    public float defocusMainFadeMultiplier = 0.55f;

    [Header("颜色")]
    public Color[] petalColors =
    {
        new Color(1f, 0.92f, 0.95f, 1f),
        new Color(1f, 0.78f, 0.86f, 1f),
        new Color(1f, 1f, 1f, 1f)
    };

    [Header("整句爆发错峰")]
    [Tooltip("PlayBurst 使用。每个字触发的 PlayBurstAt 不使用这个。")]
    public float burstSpreadTime = 0.18f;

    private readonly List<GameObject> spawnedPetals = new List<GameObject>();

    public void PlayBurst()
    {
        if (petalPrefab == null)
        {
            Debug.LogWarning("OpeningQuotePetalBurstUI: petalPrefab 没有设置。");
            return;
        }

        if (targetParent == null)
            targetParent = transform as RectTransform;

        if (targetParent == null)
        {
            Debug.LogWarning("OpeningQuotePetalBurstUI: targetParent 没有设置。");
            return;
        }

        ClearOldPetals();
        StartCoroutine(BurstRoutine());
    }

    IEnumerator BurstRoutine()
    {
        for (int i = 0; i < petalCount; i++)
        {
            SpawnOnePetal(GetSpawnPosition());

            if (burstSpreadTime > 0f && petalCount > 1)
            {
                float wait = burstSpreadTime / petalCount;
                yield return new WaitForSeconds(wait);
            }
            else
            {
                yield return null;
            }
        }
    }

    public void PlayBurstAt(Vector2 anchoredPosition, int count)
    {
        if (petalPrefab == null)
        {
            Debug.LogWarning("OpeningQuotePetalBurstUI: petalPrefab 没有设置。");
            return;
        }

        if (targetParent == null)
            targetParent = transform as RectTransform;

        if (targetParent == null)
        {
            Debug.LogWarning("OpeningQuotePetalBurstUI: targetParent 没有设置。");
            return;
        }

        count = Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            Vector2 jitter = new Vector2(
                Random.Range(-3f, 3f),
                Random.Range(-3f, 3f)
            );

            SpawnOnePetal(anchoredPosition + jitter);
        }
    }

    void SpawnOnePetal(Vector2 startPos)
    {
        Image ghost = null;

        if (useDefocusGhost)
        {
            ghost = Instantiate(petalPrefab, targetParent);
            ghost.gameObject.SetActive(true);
            ghost.raycastTarget = false;
            ghost.color = new Color(1f, 1f, 1f, 0f);
            spawnedPetals.Add(ghost.gameObject);
        }

        Image petal = Instantiate(petalPrefab, targetParent);
        petal.gameObject.SetActive(true);
        petal.raycastTarget = false;

        RectTransform rt = petal.rectTransform;
        RectTransform ghostRt = ghost != null ? ghost.rectTransform : null;

        float lifetime = Random.Range(lifetimeMin, lifetimeMax);

        Vector2 drift = new Vector2(
            Random.Range(horizontalDriftMin, horizontalDriftMax) + windBoostX,
            Random.Range(upwardDriftMin, upwardDriftMax) + windBoostY
        );

        float startScale = Random.Range(startScaleMin, startScaleMax);
        float endScale = Random.Range(endScaleMin, endScaleMax);

        float startRotation = Random.Range(-20f, 20f);
        float rotateSpeed = Random.Range(rotateSpeedMin, rotateSpeedMax);

        float wiggleSpeed = Random.Range(wiggleSpeedMin, wiggleSpeedMax);
        float wigglePhase = Random.Range(0f, Mathf.PI * 2f);

        Color baseColor = petalColors.Length > 0
            ? petalColors[Random.Range(0, petalColors.Length)]
            : petal.color;

        petal.color = baseColor;

        rt.anchoredPosition = startPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, startRotation);
        rt.localScale = Vector3.one * startScale;

        if (ghostRt != null)
        {
            ghostRt.anchoredPosition = startPos;
            ghostRt.localRotation = Quaternion.Euler(0f, 0f, startRotation);
            ghostRt.localScale = Vector3.one * startScale;
        }

        spawnedPetals.Add(petal.gameObject);

        StartCoroutine(AnimatePetal(
            petal,
            rt,
            ghost,
            ghostRt,
            startPos,
            drift,
            lifetime,
            startScale,
            endScale,
            startRotation,
            rotateSpeed,
            wiggleStrength,
            wiggleSpeed,
            wigglePhase,
            baseColor
        ));
    }

    Vector2 GetSpawnPosition()
    {
        if (spawnRect == null)
            return Vector2.zero;

        Rect rect = spawnRect.rect;

        float width = rect.width * spawnWidthMultiplier;
        float height = rect.height * spawnHeightMultiplier;

        Vector2 center = spawnRect.anchoredPosition;

        float x = Random.Range(-width * 0.5f, width * 0.5f);
        float y = Random.Range(-height * 0.5f, height * 0.5f);

        return center + new Vector2(x, y);
    }

    IEnumerator AnimatePetal(
        Image petal,
        RectTransform rt,
        Image ghost,
        RectTransform ghostRt,
        Vector2 startPos,
        Vector2 drift,
        float lifetime,
        float startScale,
        float endScale,
        float startRotation,
        float rotateSpeed,
        float wiggleStrengthValue,
        float wiggleSpeed,
        float wigglePhase,
        Color baseColor
    )
    {
        float elapsed = 0f;

        float sideWiggleSeed = Random.Range(0.8f, 1.2f);
        float riseSoftness = Random.Range(0.9f, 1.15f);

        while (elapsed < lifetime)
        {
            if (petal == null || rt == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / lifetime);

            float moveT = 1f - Mathf.Pow(1f - t, 1.8f);

            float sideWiggle =
                Mathf.Sin(t * Mathf.PI * 2f * wiggleSpeed + wigglePhase) *
                wiggleStrengthValue *
                sideWiggleSeed;

            float verticalWiggle =
                Mathf.Sin(t * Mathf.PI * 1.4f + wigglePhase * 0.7f) *
                (wiggleStrengthValue * 0.18f);

            Vector2 pos = startPos + new Vector2(
                drift.x * moveT + sideWiggle,
                drift.y * moveT * riseSoftness + verticalWiggle
            );

            rt.anchoredPosition = pos;

            float rotation = startRotation + rotateSpeed * elapsed;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);

            float scale = Mathf.Lerp(startScale, endScale, t);
            rt.localScale = Vector3.one * scale;

            float alpha;

            if (t < 0.10f)
            {
                alpha = Mathf.InverseLerp(0f, 0.10f, t);
            }
            else if (t < 0.65f)
            {
                alpha = 1f;
            }
            else
            {
                alpha = 1f - Mathf.InverseLerp(0.65f, 1f, t);
            }

            float defocusT = Mathf.InverseLerp(defocusStartNormalized, 1f, t);
            defocusT = Mathf.Clamp01(defocusT);
            float smoothDefocusT = Mathf.SmoothStep(0f, 1f, defocusT);

            Color c = baseColor;
            c.a = alpha * Mathf.Lerp(1f, defocusMainFadeMultiplier, smoothDefocusT);
            petal.color = c;

            if (ghost != null && ghostRt != null)
            {
                ghostRt.anchoredPosition = pos;
                ghostRt.localRotation = Quaternion.Euler(0f, 0f, rotation);
                ghostRt.localScale = Vector3.one * scale * Mathf.Lerp(1f, defocusGhostScale, smoothDefocusT);

                Color ghostColor = baseColor;
                ghostColor.a = alpha * defocusGhostMaxAlpha * smoothDefocusT;
                ghost.color = ghostColor;
            }

            yield return null;
        }

        if (petal != null)
        {
            spawnedPetals.Remove(petal.gameObject);
            Destroy(petal.gameObject);
        }

        if (ghost != null)
        {
            spawnedPetals.Remove(ghost.gameObject);
            Destroy(ghost.gameObject);
        }
    }

    public void ClearOldPetals()
    {
        for (int i = spawnedPetals.Count - 1; i >= 0; i--)
        {
            if (spawnedPetals[i] != null)
                Destroy(spawnedPetals[i]);
        }

        spawnedPetals.Clear();
    }
}