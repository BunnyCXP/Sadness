using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Text;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RoomInteraction : MonoBehaviour
{
    private enum IntroState
    {
        BootDelay,
        WakeUp,
        LookAtRemote,
        PickupAndLookAtTV,
        WaitForPressR2ToPlay,
        Countdown,
        Finished
    }

    [Header("Opening Timing")]
    public float introDelay = 0.15f;
    public float wakeUpDuration = 2.80f;
    public float lookAtRemoteDuration = 1.55f;
    public float pickupDuration = 1.00f;
    public float lookAtTVDuration = 1.65f;

    [Tooltip("How long the view stays clear on the remote before pickup starts.")]
    public float remoteClearHoldDuration = 0.30f;

    [Header("Opening Quote")]
    public bool useOpeningQuote = true;

    [TextArea(2, 4)]
    public string openingQuote = "I once believed love was enough to bridge time, distance, and everything between.";

    [Tooltip("开场黑幕文字的 CanvasGroup")]
    public CanvasGroup openingQuoteCanvasGroup;

    [Tooltip("开场黑幕文字 TMP_Text")]
    public TMP_Text openingQuoteText;

    [Tooltip("文字消失时播放的普通 ParticleSystem，可不填")]
    public ParticleSystem openingQuotePetalFX;

    [Tooltip("UI 花瓣飘散效果，可不填")]
    public OpeningQuotePetalBurstUI openingQuotePetalBurstUI;

    [Tooltip("每个字消失时默认生成几片花瓣")]
    public int petalsPerDisappearingChar = 1;

    [Header("花瓣数量随单词长度")]
    public bool useWordLengthPetalCount = true;

    public int shortWordPetals = 1;
    public int mediumWordPetals = 2;
    public int longWordPetals = 3;

    public int mediumWordMinLength = 5;
    public int longWordMinLength = 8;

    [Tooltip("是否随机顺序消失。打开后随机一个一个字溶解。")]
    public bool openingQuoteDisappearRandomOrder = true;

    [Tooltip("花瓣飞完前，文字隐藏后额外停留多久")]
    public float openingQuotePetalAfterHoldDuration = 4.5f;

    [Tooltip("黑幕后多久开始显示文字")]
    public float openingQuoteStartDelay = 0.40f;

    [Tooltip("每个字符原地淡入的持续时间")]
    public float openingQuoteCharFadeDuration = 0.16f;

    [Tooltip("字符出现的错峰间隔")]
    public float openingQuoteCharGap = 0.015f;

    [Tooltip("整句完全出现后停留时间")]
    public float openingQuoteHoldDuration = 1.00f;

    [Header("文字溶解为花瓣")]
    [Tooltip("每个字符溶解持续时间。越大越柔和")]
    public float openingQuoteCharDisappearDuration = 0.42f;

    [Tooltip("每隔多久启动下一个随机字的溶解。越小越像整句慢慢化开，越大越像一个一个字消失")]
    public float openingQuoteDisappearStep = 0.075f;

    [Tooltip("每个字开始溶解前，保持完整的比例。例如 0.15 = 前 15% 时间保持清晰")]
    [Range(0f, 0.5f)]
    public float openingQuoteDissolveSoftStart = 0.15f;

    [Tooltip("每个字溶解过程中最多喷几次花瓣")]
    [Range(1, 5)]
    public int openingQuotePetalPulsesPerChar = 3;

    [Tooltip("第一次喷花瓣发生在字溶解进度多少时")]
    [Range(0f, 1f)]
    public float openingQuoteFirstPetalPulseT = 0.08f;

    [Tooltip("最后一次喷花瓣发生在字溶解进度多少时")]
    [Range(0f, 1f)]
    public float openingQuoteLastPetalPulseT = 0.72f;

    [Header("Initial View")]
    public float startPitchDown = 18f;

    [Header("Look Targets")]
    public Transform wakeUpLookTarget;
    public Transform remoteLookTarget;
    public Transform tvLookTarget;

    [Header("Remote Setup")]
    public Transform remoteCube;
    public Transform holdPoint;

    [Header("Eyelids (two black rectangle Images on a normal Canvas)")]
    public RectTransform topEyelid;
    public RectTransform bottomEyelid;

    [Header("Automatic Eyelid Layout")]
    public RectTransform eyelidCanvasRect;
    public float eyelidOverlap = 20f;
    public float eyelidOpenMargin = 10f;

    [Header("Blink Settings")]
    public float closedHoldDuration = 0.70f;
    public float firstOpenDuration = 1.45f;
    public float blinkDuration = 0.16f;
    public float blinkGap = 0.14f;

    [Header("Final Blink")]
    [Range(0.3f, 0.9f)]
    public float finalBlinkStartNormalized = 0.60f;

    public float finalBlinkCloseDuration = 0.22f;
    public float finalBlinkClosedHold = 0.03f;
    public float finalBlinkOpenDuration = 1.45f;

    [Header("Uniform Blur Timeline")]
    [Range(0f, 1f)] public float blurAtBoot = 1.00f;
    [Range(0f, 1f)] public float blurAtRemoteEnd = 0.00f;
    [Range(0f, 1f)] public float blinkExtraBlur = 0.16f;

    [Header("Blur (URP Global Volume + Depth Of Field)")]
    public Volume blurVolume;
    public bool useBlur = true;

    [Header("Bokeh DOF Tuning")]
    public float bokehNearFocusDistance = 0.25f;
    public float bokehClearFocusDistance = 8.0f;
    public float bokehFocalLength = 90f;
    public float bokehStrongAperture = 1.8f;
    public float bokehClearAperture = 2.8f;

    [Header("Optional Opening Dialogue + Subtitles")]
    public bool useOpeningDialogue = true;

    public AudioSource dialogueAudioSource;

    public CanvasGroup subtitleCanvasGroup;
    public TMP_Text subtitleText;
    public float subtitleFadeDuration = 0.12f;

    public AudioClip wakeCallClip;
    [TextArea(2, 3)] public string wakeCallSubtitle = "...Wake up.";
    public float wakeCallFallbackDuration = 0.85f;

    public AudioClip hurryUpClip;
    [TextArea(2, 3)] public string hurryUpSubtitle = "Come on. The movie is about to start. Grab the remote.";
    public float hurryUpFallbackDuration = 1.60f;

    public AudioClip replyClip;
    [TextArea(2, 3)] public string replySubtitle = "...Okay. Hold on.";
    public float replyFallbackDuration = 1.05f;

    public float dialogueGap = 0.05f;

    [Header("Projection Tutorial UI")]
    public CanvasGroup tutorialCanvasGroup;
    public TMP_Text tutorialText;
    public float promptFadeDuration = 0.28f;
    public float tutorialReadTime = 1.35f;
    public float countdownInterval = 0.70f;

    [Header("Debug Input")]
    public bool allowKeyboardDebug = true;
    public bool keyboardSkipsTutorial = true;

    [Header("System Reference")]
    public CameraSwitch cameraSwitch;

    private IntroState currentState = IntroState.BootDelay;
    private bool isSequenceBusy = false;
    private bool hasWarnedMissingRefs = false;

    private DepthOfField depthOfField;
    private bool blurReady = false;

    private float computedTopOpenY;
    private float computedTopClosedY;
    private float computedBottomOpenY;
    private float computedBottomClosedY;
    private float computedEyelidHeight;

    private bool clarityTimelineActive = false;
    private float clarityElapsed = 0f;
    private float clarityDuration = 1f;
    private float currentBaseBlur = 1f;
    private float currentBlinkExtraBlur = 0f;

    void Start()
    {
        HidePromptImmediate();
        HideSubtitleImmediate();
        HideOpeningQuoteImmediate();

        RebuildEyelidLayout();
        SetupBlur();

        SetLidState(1f);
        currentBaseBlur = blurAtBoot;
        currentBlinkExtraBlur = 0f;
        ApplyCompositeBlur();

        Vector3 startEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(
            startEuler.x + startPitchDown,
            startEuler.y,
            startEuler.z
        );

        StartCoroutine(IntroSequence());
    }

    void OnDisable()
    {
        currentBlinkExtraBlur = 0f;
        currentBaseBlur = 0f;
        ApplyBlur(0f);

        HideSubtitleImmediate();
        HideOpeningQuoteImmediate();

        if (openingQuotePetalBurstUI != null)
            openingQuotePetalBurstUI.ClearOldPetals();
    }

    void Update()
    {
        AdvanceClarityTimeline(Time.deltaTime);
        ApplyCompositeBlur();

        if (!ValidateReferences())
            return;

        if (currentState == IntroState.WaitForPressR2ToPlay && !isSequenceBusy)
        {
            if (GameInputHub.R2PressedThisFrame)
            {
                StartCoroutine(BeginTVSequence(false));
            }
        }
    }

    bool ValidateReferences()
    {
        bool valid =
            wakeUpLookTarget != null &&
            remoteLookTarget != null &&
            tvLookTarget != null &&
            remoteCube != null &&
            holdPoint != null &&
            topEyelid != null &&
            bottomEyelid != null &&
            tutorialCanvasGroup != null &&
            tutorialText != null &&
            cameraSwitch != null;

        if (!valid && !hasWarnedMissingRefs)
        {
            hasWarnedMissingRefs = true;
            Debug.LogError("RoomInteraction is missing references. Check all required fields.");
        }

        return valid;
    }

    void RebuildEyelidLayout()
    {
        RectTransform canvasRect = eyelidCanvasRect;

        if (canvasRect == null && topEyelid != null)
            canvasRect = topEyelid.parent as RectTransform;

        if (canvasRect == null)
            return;

        float canvasHeight = canvasRect.rect.height;
        computedEyelidHeight = canvasHeight * 0.5f + eyelidOverlap;

        if (topEyelid != null)
        {
            Vector2 s = topEyelid.sizeDelta;
            s.y = computedEyelidHeight;
            topEyelid.sizeDelta = s;
        }

        if (bottomEyelid != null)
        {
            Vector2 s = bottomEyelid.sizeDelta;
            s.y = computedEyelidHeight;
            bottomEyelid.sizeDelta = s;
        }

        computedTopClosedY = 0f;
        computedBottomClosedY = 0f;

        computedTopOpenY = computedEyelidHeight + eyelidOpenMargin;
        computedBottomOpenY = -(computedEyelidHeight + eyelidOpenMargin);
    }

    void SetupBlur()
    {
        blurReady = false;

        if (!useBlur)
            return;

        if (blurVolume == null || blurVolume.profile == null)
        {
            Debug.LogWarning("RoomInteraction: blurVolume or Volume Profile is missing. Blur will be skipped.");
            return;
        }

        if (!blurVolume.profile.TryGet<DepthOfField>(out depthOfField))
        {
            Debug.LogWarning("RoomInteraction: Depth Of Field override is missing from the Volume Profile. Blur will be skipped.");
            return;
        }

        depthOfField.active = true;
        depthOfField.mode.overrideState = true;
        depthOfField.focusDistance.overrideState = true;
        depthOfField.focalLength.overrideState = true;
        depthOfField.aperture.overrideState = true;

        depthOfField.mode.value = DepthOfFieldMode.Bokeh;
        blurReady = true;
    }

    void ApplyBlur(float blurT)
    {
        if (!blurReady || depthOfField == null)
            return;

        blurT = Mathf.Clamp01(blurT);

        if (blurT <= 0.001f)
        {
            depthOfField.active = false;
            return;
        }

        depthOfField.active = true;
        depthOfField.mode.value = DepthOfFieldMode.Bokeh;

        depthOfField.focusDistance.value =
            Mathf.Lerp(bokehClearFocusDistance, bokehNearFocusDistance, blurT);

        depthOfField.focalLength.value = bokehFocalLength;

        depthOfField.aperture.value =
            Mathf.Lerp(bokehClearAperture, bokehStrongAperture, blurT);
    }

    void ApplyCompositeBlur()
    {
        float combined = Mathf.Clamp01(currentBaseBlur + currentBlinkExtraBlur);
        ApplyBlur(combined);
    }

    void BeginClarityTimeline()
    {
        clarityElapsed = 0f;

        clarityDuration =
            firstOpenDuration +
            (blinkDuration * 2f) +
            blinkGap +
            wakeUpDuration +
            lookAtRemoteDuration;

        clarityDuration = Mathf.Max(0.001f, clarityDuration);
        clarityTimelineActive = true;
        currentBaseBlur = blurAtBoot;
    }

    void AdvanceClarityTimeline(float deltaTime)
    {
        if (!clarityTimelineActive)
            return;

        clarityElapsed += deltaTime;

        if (clarityElapsed >= clarityDuration)
        {
            clarityElapsed = clarityDuration;
            clarityTimelineActive = false;
        }

        float t = Mathf.Clamp01(clarityElapsed / clarityDuration);
        currentBaseBlur = Mathf.Lerp(blurAtBoot, blurAtRemoteEnd, t);
    }

    bool HasDialogueContent(AudioClip clip, string subtitle)
    {
        return clip != null || !string.IsNullOrWhiteSpace(subtitle);
    }

    IEnumerator RunTogether(IEnumerator a, IEnumerator b)
    {
        bool aDone = false;
        bool bDone = false;

        StartCoroutine(RunAndFlag(a, () => aDone = true));
        StartCoroutine(RunAndFlag(b, () => bDone = true));

        while (!aDone || !bDone)
            yield return null;
    }

    IEnumerator RunAndFlag(IEnumerator routine, Action onDone)
    {
        yield return StartCoroutine(routine);
        onDone?.Invoke();
    }

    IEnumerator IntroSequence()
    {
        isSequenceBusy = true;

        currentState = IntroState.BootDelay;
        yield return new WaitForSeconds(introDelay);

        yield return StartCoroutine(PlayOpeningQuote());

        currentState = IntroState.WakeUp;
        yield return StartCoroutine(PlayWakeUpSequence());

        currentState = IntroState.LookAtRemote;

        if (useOpeningDialogue && HasDialogueContent(replyClip, replySubtitle))
        {
            yield return StartCoroutine(RunTogether(
                SmoothLookAtPointOnly(remoteLookTarget.position, lookAtRemoteDuration, true),
                PlayDialogueLine(replyClip, replySubtitle, replyFallbackDuration)
            ));
        }
        else
        {
            yield return StartCoroutine(
                SmoothLookAtPointOnly(remoteLookTarget.position, lookAtRemoteDuration, true)
            );
        }

        yield return new WaitForSeconds(remoteClearHoldDuration);

        currentState = IntroState.PickupAndLookAtTV;
        yield return StartCoroutine(PickupRemoteAndLookAtTV());

        currentState = IntroState.WaitForPressR2ToPlay;
        yield return StartCoroutine(ShowPrompt("Press R2 to turn on the TV"));

        isSequenceBusy = false;
    }

    IEnumerator PlayOpeningQuote()
    {
        if (!useOpeningQuote)
            yield break;

        if (openingQuoteCanvasGroup == null || openingQuoteText == null)
            yield break;

        if (string.IsNullOrWhiteSpace(openingQuote))
            yield break;

        HideOpeningQuoteImmediate();

        if (openingQuotePetalBurstUI != null)
            openingQuotePetalBurstUI.ClearOldPetals();

        openingQuoteText.richText = true;

        openingQuoteCanvasGroup.gameObject.SetActive(true);
        openingQuoteCanvasGroup.alpha = 1f;
        openingQuoteCanvasGroup.transform.SetAsLastSibling();

        if (openingQuoteStartDelay > 0f)
            yield return new WaitForSeconds(openingQuoteStartDelay);

        string fullText = openingQuote;
        int visibleCount = CountVisibleQuoteChars(fullText);

        float revealTotalDuration =
            openingQuoteCharFadeDuration +
            Mathf.Max(0, visibleCount - 1) * openingQuoteCharGap;

        float revealElapsed = 0f;

        while (revealElapsed < revealTotalDuration)
        {
            revealElapsed += Time.deltaTime;
            openingQuoteText.text = BuildRevealStyledText(fullText, revealElapsed);
            yield return null;
        }

        openingQuoteText.text = BuildAllCharsStyled(fullText, 1f);

        if (openingQuoteHoldDuration > 0f)
            yield return new WaitForSeconds(openingQuoteHoldDuration);

        if (openingQuotePetalFX != null)
            openingQuotePetalFX.Play();

        int[] scatterRanks = BuildScatterRanks(fullText);
        Vector2[] charPositions = CacheOpeningQuoteCharacterPositions(fullText);
        int[] petalPulseProgress = new int[fullText.Length];

        float disappearStep = Mathf.Max(0.001f, openingQuoteDisappearStep);

        float scatterTotalDuration =
            openingQuoteCharDisappearDuration +
            Mathf.Max(0, visibleCount - 1) * disappearStep;

        float scatterElapsed = 0f;

        while (scatterElapsed < scatterTotalDuration)
        {
            scatterElapsed += Time.deltaTime;

            SpawnPetalsForDisappearingCharacters(
                fullText,
                scatterElapsed,
                scatterRanks,
                charPositions,
                petalPulseProgress
            );

            openingQuoteText.text =
                BuildScatterStyledText(fullText, scatterElapsed, scatterRanks);

            yield return null;
        }

        openingQuoteText.text = BuildAllCharsStyled(fullText, 0f);

        if (openingQuotePetalAfterHoldDuration > 0f)
            yield return new WaitForSeconds(openingQuotePetalAfterHoldDuration);

        HideOpeningQuoteImmediate();
    }

    string BuildRevealStyledText(string fullText, float elapsed)
    {
        StringBuilder sb = new StringBuilder();

        int visibleIndex = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            char c = fullText[i];

            if (char.IsWhiteSpace(c))
            {
                sb.Append(c);
                continue;
            }

            float charStart = visibleIndex * openingQuoteCharGap;

            float t = Mathf.Clamp01(
                (elapsed - charStart) /
                Mathf.Max(0.0001f, openingQuoteCharFadeDuration)
            );

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            sb.Append(WrapStyledChar(c, smoothT));

            visibleIndex++;
        }

        return sb.ToString();
    }

    string BuildScatterStyledText(string fullText, float elapsed, int[] scatterRanks)
    {
        StringBuilder sb = new StringBuilder();

        float disappearStep = Mathf.Max(0.001f, openingQuoteDisappearStep);

        for (int i = 0; i < fullText.Length; i++)
        {
            char c = fullText[i];

            if (char.IsWhiteSpace(c))
            {
                sb.Append(c);
                continue;
            }

            int rank = scatterRanks[i];

            if (rank < 0)
            {
                sb.Append(WrapStyledChar(c, 1f));
                continue;
            }

            float charStart = rank * disappearStep;
            float charEnd = charStart + openingQuoteCharDisappearDuration;

            // 还没轮到这个字消失：完整显示
            if (elapsed < charStart)
            {
                sb.Append(WrapStyledChar(c, 1f));
                continue;
            }

            // 正在消失：像出现时一样，只做单纯淡出
            if (elapsed < charEnd)
            {
                float t = Mathf.InverseLerp(charStart, charEnd, elapsed);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float alpha = 1f - smoothT;

                sb.Append(WrapStyledChar(c, alpha));
                continue;
            }

            // 消失完：保持透明字符，防止整行排版跳动
            sb.Append(WrapStyledChar(c, 0f));
        }

        return sb.ToString();
    }

    string BuildAllCharsStyled(string fullText, float alpha)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < fullText.Length; i++)
        {
            char c = fullText[i];

            if (char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(WrapStyledChar(c, alpha));
            }
        }

        return sb.ToString();
    }

    Vector2[] CacheOpeningQuoteCharacterPositions(string fullText)
    {
        Vector2[] positions = new Vector2[fullText.Length];

        if (openingQuoteText == null)
            return positions;

        if (openingQuotePetalBurstUI == null || openingQuotePetalBurstUI.targetParent == null)
            return positions;

        RectTransform textRect = openingQuoteText.rectTransform;
        RectTransform targetParent = openingQuotePetalBurstUI.targetParent;

        openingQuoteText.text = fullText;

        Canvas.ForceUpdateCanvases();
        openingQuoteText.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = openingQuoteText.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            int sourceIndex = charInfo.index;

            if (sourceIndex < 0 || sourceIndex >= fullText.Length)
                continue;

            Vector3 localCenterInText =
                (charInfo.bottomLeft + charInfo.topRight) * 0.5f;

            Vector3 worldCenter =
                textRect.TransformPoint(localCenterInText);

            Vector3 localCenterInParent =
                targetParent.InverseTransformPoint(worldCenter);

            positions[sourceIndex] =
                new Vector2(localCenterInParent.x, localCenterInParent.y);
        }

        return positions;
    }

    void SpawnPetalsForDisappearingCharacters(
        string fullText,
        float scatterElapsed,
        int[] scatterRanks,
        Vector2[] charPositions,
        int[] petalPulseProgress
    )
    {
        if (openingQuotePetalBurstUI == null)
            return;

        if (scatterRanks == null || charPositions == null || petalPulseProgress == null)
            return;

        float disappearStep = Mathf.Max(0.001f, openingQuoteDisappearStep);
        int pulseCount = Mathf.Max(1, openingQuotePetalPulsesPerChar);

        float firstPulseT = Mathf.Min(openingQuoteFirstPetalPulseT, openingQuoteLastPetalPulseT);
        float lastPulseT = Mathf.Max(openingQuoteFirstPetalPulseT, openingQuoteLastPetalPulseT);

        for (int i = 0; i < fullText.Length; i++)
        {
            if (i >= scatterRanks.Length || i >= charPositions.Length || i >= petalPulseProgress.Length)
                continue;

            if (char.IsWhiteSpace(fullText[i]))
                continue;

            int rank = scatterRanks[i];

            if (rank < 0)
                continue;

            float charStart = rank * disappearStep;
            float charEnd = charStart + openingQuoteCharDisappearDuration;

            if (scatterElapsed < charStart || scatterElapsed > charEnd)
                continue;

            float charT = Mathf.InverseLerp(charStart, charEnd, scatterElapsed);

            int totalPetalsForThisChar = GetPetalCountForCharacter(fullText, i);

            while (petalPulseProgress[i] < pulseCount)
            {
                int pulseIndex = petalPulseProgress[i];

                float pulseT;

                if (pulseCount <= 1)
                {
                    pulseT = firstPulseT;
                }
                else
                {
                    pulseT = Mathf.Lerp(
                        firstPulseT,
                        lastPulseT,
                        pulseIndex / (float)(pulseCount - 1)
                    );
                }

                if (charT < pulseT)
                    break;

                if (pulseIndex < totalPetalsForThisChar)
                {
                    openingQuotePetalBurstUI.PlayBurstAt(charPositions[i], 1);
                }

                petalPulseProgress[i]++;
            }
        }
    }

    int GetPetalCountForCharacter(string text, int charIndex)
    {
        if (!useWordLengthPetalCount)
            return Mathf.Max(1, petalsPerDisappearingChar);

        if (string.IsNullOrEmpty(text))
            return Mathf.Max(1, petalsPerDisappearingChar);

        if (charIndex < 0 || charIndex >= text.Length)
            return Mathf.Max(1, petalsPerDisappearingChar);

        char current = text[charIndex];

        if (!IsQuoteWordCharacter(current))
            return Mathf.Max(1, petalsPerDisappearingChar);

        int start = charIndex;
        while (start > 0 && IsQuoteWordCharacter(text[start - 1]))
        {
            start--;
        }

        int end = charIndex;
        while (end < text.Length - 1 && IsQuoteWordCharacter(text[end + 1]))
        {
            end++;
        }

        int wordLength = end - start + 1;

        if (wordLength >= longWordMinLength)
            return Mathf.Max(1, longWordPetals);

        if (wordLength >= mediumWordMinLength)
            return Mathf.Max(1, mediumWordPetals);

        return Mathf.Max(1, shortWordPetals);
    }

    bool IsQuoteWordCharacter(char c)
    {
        return char.IsLetterOrDigit(c) || c == '\'';
    }

    string WrapStyledChar(char c, float alpha01)
    {
        string colorHex = GetQuoteColorHex(alpha01);
        return "<color=#" + colorHex + ">" + c + "</color>";
    }

    string GetQuoteColorHex(float alpha01)
    {
        Color32 c = openingQuoteText != null ? openingQuoteText.color : Color.white;
        c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha01) * 255f);

        return
            c.r.ToString("X2") +
            c.g.ToString("X2") +
            c.b.ToString("X2") +
            c.a.ToString("X2");
    }

    int CountVisibleQuoteChars(string text)
    {
        int count = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
                count++;
        }

        return count;
    }

    int[] BuildScatterRanks(string fullText)
    {
        int[] ranks = new int[fullText.Length];

        for (int i = 0; i < ranks.Length; i++)
            ranks[i] = -1;

        int visibleCount = CountVisibleQuoteChars(fullText);

        int order = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (!char.IsWhiteSpace(fullText[i]))
            {
                ranks[i] = order;
                order++;
            }
        }

        if (!openingQuoteDisappearRandomOrder)
            return ranks;

        int[] visibleIndices = new int[visibleCount];

        int index = 0;
        for (int i = 0; i < fullText.Length; i++)
        {
            if (!char.IsWhiteSpace(fullText[i]))
            {
                visibleIndices[index] = i;
                index++;
            }
        }

        for (int i = visibleIndices.Length - 1; i > 0; i--)
        {
            int j = Mathf.FloorToInt(Hash01(i * 31 + fullText.Length * 7) * (i + 1));

            int temp = visibleIndices[i];
            visibleIndices[i] = visibleIndices[j];
            visibleIndices[j] = temp;
        }

        for (int newOrder = 0; newOrder < visibleIndices.Length; newOrder++)
        {
            ranks[visibleIndices[newOrder]] = newOrder;
        }

        return ranks;
    }

    float Hash01(int seed)
    {
        return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
    }

    void HideOpeningQuoteImmediate()
    {
        if (openingQuoteCanvasGroup != null)
        {
            openingQuoteCanvasGroup.alpha = 0f;
            openingQuoteCanvasGroup.gameObject.SetActive(false);
        }

        if (openingQuoteText != null)
        {
            openingQuoteText.richText = true;
            openingQuoteText.text = "";
        }
    }

    IEnumerator BeginTVSequence(bool debugSkipTutorial)
    {
        isSequenceBusy = true;

        yield return StartCoroutine(HidePrompt());

        if (!debugSkipTutorial)
        {
            yield return StartCoroutine(ShowPrompt("Left Stick: Move Train\nRight Stick: Move Cursor\nR2: Grab / Drag"));
            yield return new WaitForSeconds(tutorialReadTime);
        }

        currentState = IntroState.Countdown;
        yield return StartCoroutine(PlayCountdown());

        yield return StartCoroutine(HidePrompt());

        currentBaseBlur = 0f;
        currentBlinkExtraBlur = 0f;
        ApplyBlur(0f);

        currentState = IntroState.Finished;

        cameraSwitch.StartCinematicFromRemote();

        enabled = false;
    }

    IEnumerator PlayWakeUpSequence()
    {
        SetLidState(1f);
        currentBaseBlur = blurAtBoot;
        currentBlinkExtraBlur = 0f;
        ApplyCompositeBlur();

        if (useOpeningDialogue && HasDialogueContent(wakeCallClip, wakeCallSubtitle))
        {
            yield return StartCoroutine(PlayDialogueLine(
                wakeCallClip,
                wakeCallSubtitle,
                wakeCallFallbackDuration,
                closedHoldDuration
            ));
        }
        else
        {
            yield return new WaitForSeconds(closedHoldDuration);
        }

        BeginClarityTimeline();

        yield return StartCoroutine(OpenEyesOnly(firstOpenDuration));
        yield return new WaitForSeconds(0.06f);

        yield return StartCoroutine(BlinkOnly(blinkDuration));
        yield return new WaitForSeconds(blinkGap);

        if (useOpeningDialogue && HasDialogueContent(hurryUpClip, hurryUpSubtitle))
        {
            yield return StartCoroutine(RunTogether(
                WakeUpLookWithBlinksOnly(),
                PlayDialogueLine(hurryUpClip, hurryUpSubtitle, hurryUpFallbackDuration)
            ));
        }
        else
        {
            yield return StartCoroutine(WakeUpLookWithBlinksOnly());
        }
    }

    IEnumerator WakeUpLookWithBlinksOnly()
    {
        Quaternion startRot = transform.rotation;
        Quaternion endRot = GetLookRotation(wakeUpLookTarget.position, startRot, false);

        float blink1Start = wakeUpDuration * 0.28f;

        float finalBlinkStart = wakeUpDuration * finalBlinkStartNormalized;
        float finalBlinkCloseEnd = finalBlinkStart + finalBlinkCloseDuration;
        float finalBlinkHoldEnd = finalBlinkCloseEnd + finalBlinkClosedHold;
        float finalBlinkOpenEnd = finalBlinkHoldEnd + finalBlinkOpenDuration;

        float totalDuration = Mathf.Max(wakeUpDuration, finalBlinkOpenEnd + 0.01f);

        float elapsed = 0f;
        bool blurKilled = false;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / totalDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.rotation = Quaternion.Slerp(startRot, endRot, smoothT);

            float lidT = 0f;

            if (!blurKilled)
                lidT = Mathf.Max(lidT, EvaluateBlinkAtTime(elapsed, blink1Start, blinkDuration, 1f));

            lidT = Mathf.Max(lidT, EvaluateCustomBlink(
                elapsed,
                finalBlinkStart,
                finalBlinkCloseDuration,
                finalBlinkClosedHold,
                finalBlinkOpenDuration,
                1f
            ));

            SetLidState(lidT);

            if (!blurKilled && elapsed >= finalBlinkCloseEnd)
            {
                blurKilled = true;
                clarityTimelineActive = false;
                currentBaseBlur = 0f;
                currentBlinkExtraBlur = 0f;
                ApplyBlur(0f);
            }

            if (!blurKilled)
            {
                float lidBlurT = lidT * lidT * (3f - 2f * lidT);
                currentBlinkExtraBlur = lidBlurT * blinkExtraBlur;
                ApplyCompositeBlur();
            }

            yield return null;
        }

        transform.rotation = endRot;
        SetLidState(0f);

        clarityTimelineActive = false;
        currentBaseBlur = 0f;
        currentBlinkExtraBlur = 0f;
        ApplyBlur(0f);
    }

    float EvaluateBlinkAtTime(float time, float blinkStart, float singlePhaseDuration, float closeAmount)
    {
        float closeStart = blinkStart;
        float closeEnd = blinkStart + singlePhaseDuration;
        float openEnd = closeEnd + singlePhaseDuration;

        if (time < closeStart || time > openEnd)
            return 0f;

        if (time <= closeEnd)
        {
            float t = Mathf.InverseLerp(closeStart, closeEnd, time);
            return Mathf.SmoothStep(0f, closeAmount, t);
        }

        float openT = Mathf.InverseLerp(closeEnd, openEnd, time);
        return Mathf.SmoothStep(closeAmount, 0f, openT);
    }

    float EvaluateCustomBlink(
        float time,
        float blinkStart,
        float closeDuration,
        float closedHold,
        float openDuration,
        float closeAmount
    )
    {
        float closeStart = blinkStart;
        float closeEnd = closeStart + closeDuration;
        float holdEnd = closeEnd + closedHold;
        float openEnd = holdEnd + openDuration;

        if (time < closeStart || time > openEnd)
            return 0f;

        if (time <= closeEnd)
        {
            float t = Mathf.InverseLerp(closeStart, closeEnd, time);
            return Mathf.SmoothStep(0f, closeAmount, t);
        }

        if (time <= holdEnd)
            return closeAmount;

        float openT = Mathf.InverseLerp(holdEnd, openEnd, time);
        return Mathf.SmoothStep(closeAmount, 0f, openT);
    }

    IEnumerator PickupRemoteAndLookAtTV()
    {
        float safePickupDuration = Mathf.Max(0.0001f, pickupDuration);
        float safeLookDuration = Mathf.Max(0.0001f, lookAtTVDuration);
        float totalDuration = Mathf.Max(safePickupDuration, safeLookDuration);

        Vector3 remoteStartPos = remoteCube.position;
        Quaternion remoteStartRot = remoteCube.rotation;

        Quaternion cameraStartRot = transform.rotation;
        Quaternion cameraEndRot = GetLookRotation(tvLookTarget.position, cameraStartRot, true);

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            float pickupT = Mathf.Clamp01(elapsed / safePickupDuration);
            float pickupSmooth = Mathf.SmoothStep(0f, 1f, pickupT);

            remoteCube.position = Vector3.Lerp(remoteStartPos, holdPoint.position, pickupSmooth);
            remoteCube.rotation = Quaternion.Slerp(remoteStartRot, holdPoint.rotation, pickupSmooth);

            float lookT = Mathf.Clamp01(elapsed / safeLookDuration);
            float lookSmooth = Mathf.SmoothStep(0f, 1f, lookT);

            transform.rotation = Quaternion.Slerp(cameraStartRot, cameraEndRot, lookSmooth);

            yield return null;
        }

        remoteCube.SetParent(holdPoint, false);
        remoteCube.localPosition = Vector3.zero;
        remoteCube.localRotation = Quaternion.identity;

        transform.rotation = cameraEndRot;
    }

    IEnumerator SmoothLookAtPointOnly(Vector3 targetPos, float duration, bool horizontalOnly)
    {
        Quaternion startRot = transform.rotation;
        Quaternion endRot = GetLookRotation(targetPos, startRot, horizontalOnly);

        if (duration <= 0f)
        {
            transform.rotation = endRot;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.rotation = Quaternion.Slerp(startRot, endRot, smoothT);

            yield return null;
        }

        transform.rotation = endRot;
    }

    Quaternion GetLookRotation(Vector3 targetPos, Quaternion fallback, bool horizontalOnly)
    {
        Vector3 dir = targetPos - transform.position;

        if (horizontalOnly)
            dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return fallback;

        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    void SetLidState(float t)
    {
        t = Mathf.Clamp01(t);

        if (topEyelid != null)
        {
            Vector2 p = topEyelid.anchoredPosition;
            p.y = Mathf.Lerp(computedTopOpenY, computedTopClosedY, t);
            topEyelid.anchoredPosition = p;
        }

        if (bottomEyelid != null)
        {
            Vector2 p = bottomEyelid.anchoredPosition;
            p.y = Mathf.Lerp(computedBottomOpenY, computedBottomClosedY, t);
            bottomEyelid.anchoredPosition = p;
        }
    }

    IEnumerator OpenEyesOnly(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            SetLidState(1f - smoothT);

            yield return null;
        }

        SetLidState(0f);
    }

    IEnumerator BlinkOnly(float singleBlinkDuration)
    {
        float elapsed = 0f;

        while (elapsed < singleBlinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / singleBlinkDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            SetLidState(smoothT);
            currentBlinkExtraBlur = smoothT * blinkExtraBlur;

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < singleBlinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / singleBlinkDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            SetLidState(1f - smoothT);
            currentBlinkExtraBlur = (1f - smoothT) * blinkExtraBlur;

            yield return null;
        }

        SetLidState(0f);
        currentBlinkExtraBlur = 0f;
    }

    IEnumerator PlayDialogueLine(AudioClip clip, string subtitle, float fallbackDuration, float minDuration = 0f)
    {
        bool hasAudio = dialogueAudioSource != null && clip != null;

        bool hasSubtitle =
            subtitleCanvasGroup != null &&
            subtitleText != null &&
            !string.IsNullOrWhiteSpace(subtitle);

        if (hasSubtitle)
            yield return StartCoroutine(ShowSubtitle(subtitle));

        if (hasAudio)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = clip;
            dialogueAudioSource.Play();
        }

        float waitTime = hasAudio ? clip.length : fallbackDuration;
        waitTime = Mathf.Max(waitTime, minDuration);

        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        if (hasSubtitle)
            yield return StartCoroutine(HideSubtitle());

        if (dialogueGap > 0f)
            yield return new WaitForSeconds(dialogueGap);
    }

    IEnumerator ShowSubtitle(string message)
    {
        if (subtitleCanvasGroup == null || subtitleText == null)
            yield break;

        subtitleText.text = message;
        subtitleCanvasGroup.gameObject.SetActive(true);

        float startAlpha = subtitleCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < subtitleFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / subtitleFadeDuration);
            subtitleCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);

            yield return null;
        }

        subtitleCanvasGroup.alpha = 1f;
    }

    IEnumerator HideSubtitle()
    {
        if (subtitleCanvasGroup == null)
            yield break;

        float startAlpha = subtitleCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < subtitleFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / subtitleFadeDuration);
            subtitleCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        subtitleCanvasGroup.alpha = 0f;
        subtitleCanvasGroup.gameObject.SetActive(false);

        if (subtitleText != null)
            subtitleText.text = "";
    }

    void HideSubtitleImmediate()
    {
        if (subtitleCanvasGroup != null)
        {
            subtitleCanvasGroup.alpha = 0f;
            subtitleCanvasGroup.gameObject.SetActive(false);
        }

        if (subtitleText != null)
            subtitleText.text = "";
    }

    IEnumerator PlayCountdown()
    {
        yield return StartCoroutine(SetPromptText("<size=250%>3</size>"));
        yield return new WaitForSeconds(countdownInterval);

        yield return StartCoroutine(SetPromptText("<size=250%>2</size>"));
        yield return new WaitForSeconds(countdownInterval);

        yield return StartCoroutine(SetPromptText("<size=250%>1</size>"));
        yield return new WaitForSeconds(countdownInterval);
    }

    IEnumerator ShowPrompt(string message)
    {
        if (tutorialCanvasGroup == null || tutorialText == null)
            yield break;

        tutorialText.richText = true;
        tutorialText.text = message;
        tutorialCanvasGroup.gameObject.SetActive(true);

        float startAlpha = tutorialCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < promptFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / promptFadeDuration);
            tutorialCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);

            yield return null;
        }

        tutorialCanvasGroup.alpha = 1f;
    }

    IEnumerator HidePrompt()
    {
        if (tutorialCanvasGroup == null)
            yield break;

        float startAlpha = tutorialCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < promptFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / promptFadeDuration);
            tutorialCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        tutorialCanvasGroup.alpha = 0f;
        tutorialCanvasGroup.gameObject.SetActive(false);

        if (tutorialText != null)
            tutorialText.text = "";
    }

    IEnumerator SetPromptText(string message)
    {
        if (tutorialCanvasGroup == null || tutorialText == null)
            yield break;

        tutorialText.richText = true;
        tutorialText.text = message;

        if (!tutorialCanvasGroup.gameObject.activeSelf)
            tutorialCanvasGroup.gameObject.SetActive(true);

        tutorialCanvasGroup.alpha = 1f;

        yield return null;
    }

    void HidePromptImmediate()
    {
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.gameObject.SetActive(false);
        }

        if (tutorialText != null)
        {
            tutorialText.richText = true;
            tutorialText.text = "";
        }
    }
}