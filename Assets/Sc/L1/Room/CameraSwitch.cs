using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class CameraSwitch : MonoBehaviour
{
    [Header("相机设置")]
    public Camera roomCamera;
    public Camera movieCamera;

    [Header("荧幕参考点")]
    [Tooltip("把墙上的电视屏幕 / 荧幕物体拖进来。镜头会根据它的方向来正对推进。")]
    public Transform screenTransform;

    [Header("UI设置")]
    public RectTransform topBar;
    public RectTransform bottomBar;
    public CanvasGroup whiteFlashGroup;

    [Header("转场参数")]
    public float maxBarHeight = 80f;
    public float zoomInDuration = 2.0f;

    [Tooltip("推进后距离荧幕多远。数值越小，越贴近屏幕。")]
    public float finalDistanceFromScreen = 1.2f;

    [Tooltip("转场时相机是否逐渐摆正到和荧幕平行。")]
    public bool alignCameraToScreen = true;

    [Header("正交电影相机专属设置")]
    [Tooltip("刚进入电影时的特写大小，越小越放大")]
    public float movieStartSize = 3f;

    [Tooltip("最终全貌的大小，越大越看全")]
    public float movieFinalSize = 8f;

    [Tooltip("特写停留时间")]
    public float stayAtStartDuration = 1.0f;

    [Tooltip("从特写退回全貌的时间")]
    public float returnDuration = 2.0f;

    [Header("错觉方块控制")]
    public IllusionBlock[] illusionBlocks;

    [Header("交互管理器")]
    public InteractionManager interactionManager;

    [Header("电影内开场序章")]
    public OpeningTrainCinematicSequence openingTrainSequence;

    private bool isTransitioning = false;
    private Vector3 roomInitialPos;
    private Quaternion roomInitialRot;

    void Start()
    {
        if (roomCamera != null)
        {
            roomCamera.gameObject.SetActive(true);
            roomInitialPos = roomCamera.transform.position;
            roomInitialRot = roomCamera.transform.rotation;
        }

        if (movieCamera != null)
            movieCamera.gameObject.SetActive(false);

        SetIllusionModeForAll(false);

        if (whiteFlashGroup != null)
            whiteFlashGroup.alpha = 0f;

        if (topBar != null)
            topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, 0f);

        if (bottomBar != null)
            bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, 0f);
    }

    public void StartCinematicFromRemote()
    {
        if (!isTransitioning)
        {
            StartCoroutine(PlayCinematicTransition());
        }
    }

    public void ExitMovieMode()
    {
        if (movieCamera != null)
            movieCamera.gameObject.SetActive(false);

        if (roomCamera != null)
            roomCamera.gameObject.SetActive(true);

        if (interactionManager != null)
            interactionManager.canInteract = false;

        SetIllusionModeForAll(false);
        isTransitioning = false;
    }

    private void OnDisable()
    {
        SetIllusionModeForAll(false);
    }

    private void SetIllusionModeForAll(bool isActive)
    {
        if (illusionBlocks != null)
        {
            for (int i = 0; i < illusionBlocks.Length; i++)
            {
                if (illusionBlocks[i] != null)
                    illusionBlocks[i].SetIllusionMode(isActive);
            }
        }

        IllusionBlock[] sceneBlocks = FindObjectsByType<IllusionBlock>(FindObjectsSortMode.None);

        for (int i = 0; i < sceneBlocks.Length; i++)
        {
            if (sceneBlocks[i] != null)
                sceneBlocks[i].SetIllusionMode(isActive);
        }
    }

    IEnumerator PlayCinematicTransition()
    {
        isTransitioning = true;

        if (roomCamera == null || movieCamera == null)
        {
            Debug.LogError("CameraSwitch: roomCamera 或 movieCamera 没有拖拽。");
            isTransitioning = false;
            yield break;
        }

        if (screenTransform == null)
        {
            Debug.LogError("CameraSwitch: screenTransform 没有拖拽。请把墙上的电视屏幕 / 荧幕物体拖进去。");
            isTransitioning = false;
            yield break;
        }

        if (topBar == null || bottomBar == null)
        {
            Debug.LogError("CameraSwitch: topBar 或 bottomBar 没有拖拽。");
            isTransitioning = false;
            yield break;
        }

        if (whiteFlashGroup == null)
        {
            Debug.LogError("CameraSwitch: whiteFlashGroup 没有拖拽。");
            isTransitioning = false;
            yield break;
        }

        // 重新记录当前相机位置，避免 Start 时记录的位置和当前不一致
        roomInitialPos = roomCamera.transform.position;
        roomInitialRot = roomCamera.transform.rotation;

        // 取荧幕法线方向
        Vector3 screenNormal = screenTransform.forward;

        // 让 normal 永远指向“相机所在的一侧”
        Vector3 screenToCamera = roomCamera.transform.position - screenTransform.position;
        if (Vector3.Dot(screenNormal, screenToCamera) < 0f)
        {
            screenNormal = -screenNormal;
        }

        // 终点：位于荧幕正前方 finalDistanceFromScreen 的位置
        Vector3 roomEndPos = screenTransform.position + screenNormal * finalDistanceFromScreen;

        // 终点旋转：相机正对荧幕，并且相机画面和荧幕平行
        Quaternion roomEndRot = Quaternion.LookRotation(-screenNormal, screenTransform.up);

        // 1. 房间相机正对荧幕推进 + 黑框落下
        float elapsed = 0f;

        while (elapsed < zoomInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / zoomInDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            roomCamera.transform.position = Vector3.Lerp(roomInitialPos, roomEndPos, smoothT);

            if (alignCameraToScreen)
            {
                roomCamera.transform.rotation = Quaternion.Slerp(roomInitialRot, roomEndRot, smoothT);
            }

            topBar.sizeDelta = new Vector2(
                topBar.sizeDelta.x,
                Mathf.Lerp(0f, maxBarHeight, smoothT)
            );

            bottomBar.sizeDelta = new Vector2(
                bottomBar.sizeDelta.x,
                Mathf.Lerp(0f, maxBarHeight, smoothT)
            );

            yield return null;
        }

        roomCamera.transform.position = roomEndPos;

        if (alignCameraToScreen)
            roomCamera.transform.rotation = roomEndRot;

        topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, maxBarHeight);
        bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, maxBarHeight);

        // 2. 闪白进入
        Debug.Log("CameraSwitch: white flash started");
        elapsed = 0f;
        float flashInDuration = 0.1f;

        while (elapsed < flashInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flashInDuration);
            whiteFlashGroup.alpha = t;
            yield return null;
        }

        whiteFlashGroup.alpha = 1f;

        // 3. 切换相机 + 初始化电影特写
        roomCamera.gameObject.SetActive(false);
        movieCamera.gameObject.SetActive(true);

        SetIllusionModeForAll(true);

        if (openingTrainSequence != null)
        {
            Debug.Log("CameraSwitch: preparing opening train focus during white flash");
            openingTrainSequence.PrepareForWhiteReveal();
        }

        // 4. 闪白消退
        elapsed = 0f;
        float flashOutDuration = 0.4f;

        while (elapsed < flashOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flashOutDuration);
            whiteFlashGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        whiteFlashGroup.alpha = 0f;

        Debug.Log("CameraSwitch: white flash finished, playing opening train sequence");
        if (openingTrainSequence != null)
        {
            yield return StartCoroutine(openingTrainSequence.PlayAfterWhiteReveal());

            Debug.Log("CameraSwitch: opening sequence finished, starting camera restore and letterbox retract");

            bool cameraRestoreDone = false;
            bool letterboxDone = false;

            StartCoroutine(RunCameraRestore(() => cameraRestoreDone = true));
            StartCoroutine(RunLetterboxRetract(() => letterboxDone = true));

            while (!cameraRestoreDone || !letterboxDone)
                yield return null;

            openingTrainSequence.RestorePlayerControl();
            Debug.Log("CameraSwitch: outro finished, player control enabled");
        }
        else
        {
            Debug.Log("CameraSwitch: no opening sequence, continue default cinematic flow");
            yield return new WaitForSeconds(stayAtStartDuration);

            yield return StartCoroutine(RetractLetterbox());

            if (interactionManager != null)
                interactionManager.canInteract = true;
            else
                Debug.LogError("CameraSwitch 找不到 InteractionManager，请检查面板拖拽！");
        }

        isTransitioning = false;
    }

    IEnumerator RunCameraRestore(Action onComplete)
    {
        if (openingTrainSequence != null)
            yield return StartCoroutine(openingTrainSequence.RestoreCameraAfterIntroSmooth(openingTrainSequence.cameraRestoreDuration));

        onComplete?.Invoke();
    }

    IEnumerator RunLetterboxRetract(Action onComplete)
    {
        yield return StartCoroutine(RetractLetterbox());
        onComplete?.Invoke();
    }

    IEnumerator RetractLetterbox()
    {
        // 5. 电影镜头保持 / 序章结束后，黑框收回
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            topBar.sizeDelta = new Vector2(
                topBar.sizeDelta.x,
                Mathf.Lerp(maxBarHeight, 0f, smoothT)
            );

            bottomBar.sizeDelta = new Vector2(
                bottomBar.sizeDelta.x,
                Mathf.Lerp(maxBarHeight, 0f, smoothT)
            );

            yield return null;
        }

        topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, 0f);
        bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, 0f);

        Debug.Log("CameraSwitch: letterbox retract finished");
    }
}
