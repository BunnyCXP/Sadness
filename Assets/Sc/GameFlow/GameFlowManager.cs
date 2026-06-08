using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Chapter State")]
    public int currentChapterIndex = 1;
    public int lastFinishedFilmIndex = 0;

    [Header("Scene Names")]
    public string chapterOneSceneName = "L1";
    public string chapterTwoSceneName = "L2";
    public string chapterThreeSceneName = "L3";
    public string endingSceneName = "ED";

    [Header("References")]
    public FilmTransitionController transitionController;

    [Header("Debug")]
    public bool verboseDebug = true;

    private bool revealRoomAfterDirectLoad;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (verboseDebug)
                Debug.Log("GameFlow: destroying duplicate GameFlowManager from " + gameObject.scene.name);

            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        FilmTransitionController localTransitionController =
            GetComponent<FilmTransitionController>();

        if (localTransitionController != null)
            transitionController = localTransitionController;

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (verboseDebug)
            Debug.Log("GameFlow: persistent manager ready.");
    }

    private void Start()
    {
        RefreshSceneReferences();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    public void RegisterTransitionController(FilmTransitionController controller)
    {
        if (controller == null)
            return;

        if (transitionController != null &&
            transitionController != controller &&
            transitionController.gameObject == gameObject)
        {
            if (verboseDebug)
            {
                Debug.Log(
                    "GameFlow: ignored scene transition controller because the persistent controller is active.");
            }

            return;
        }

        transitionController = controller;

        if (verboseDebug)
            Debug.Log("GameFlow: registered FilmTransitionController.");
    }

    public void FinishFilm(int filmIndex)
    {
        FinishFilm(filmIndex, FilmTransitionController.SceneTransitionMode.FullExitAndEnter);
    }

    public void FinishFilmFromExistingWhiteCover(int filmIndex)
    {
        float inheritedHeight = transitionController != null
            ? transitionController.existingWhiteCoverLetterboxHeight
            : 0f;

        FinishFilmFromExistingWhiteCover(filmIndex, inheritedHeight);
    }

    public void FinishFilmFromExistingWhiteCover(int filmIndex, float inheritedLetterboxHeight)
    {
        FinishFilmInternal(
            filmIndex,
            FilmTransitionController.SceneTransitionMode.FromExistingWhiteCover,
            inheritedLetterboxHeight);
    }

    public void FinishFilm(int filmIndex, FilmTransitionController.SceneTransitionMode transitionMode)
    {
        float inheritedHeight = transitionMode == FilmTransitionController.SceneTransitionMode.FromExistingWhiteCover &&
                                transitionController != null
            ? transitionController.existingWhiteCoverLetterboxHeight
            : 0f;

        FinishFilmInternal(filmIndex, transitionMode, inheritedHeight);
    }

    private void FinishFilmInternal(
        int filmIndex,
        FilmTransitionController.SceneTransitionMode transitionMode,
        float inheritedLetterboxHeight)
    {
        lastFinishedFilmIndex = filmIndex;

        if (verboseDebug)
            Debug.Log("GameFlow: finished film " + filmIndex + " using " + transitionMode);

        switch (filmIndex)
        {
            case 1:
                currentChapterIndex = 2;
                LoadChapter(2, transitionMode, inheritedLetterboxHeight);
                break;

            case 2:
                currentChapterIndex = 3;
                LoadChapter(3, transitionMode, inheritedLetterboxHeight);
                break;

            case 3:
                currentChapterIndex = 3;
                LoadChapter(3, transitionMode, inheritedLetterboxHeight);
                break;

            default:
                Debug.LogWarning("GameFlow: unsupported film index " + filmIndex);
                break;
        }
    }

    public void FinishFilmOne()
    {
        FinishFilm(1);
    }

    public void FinishFilmTwo()
    {
        FinishFilm(2);
    }

    public void FinishFilmThree()
    {
        FinishFilm(3);
    }

    public void LoadChapter(int chapterIndex)
    {
        LoadChapter(chapterIndex, FilmTransitionController.SceneTransitionMode.FullExitAndEnter);
    }

    public void LoadChapter(
        int chapterIndex,
        FilmTransitionController.SceneTransitionMode transitionMode)
    {
        LoadChapter(chapterIndex, transitionMode, 0f);
    }

    private void LoadChapter(
        int chapterIndex,
        FilmTransitionController.SceneTransitionMode transitionMode,
        float inheritedLetterboxHeight)
    {
        currentChapterIndex = Mathf.Clamp(chapterIndex, 1, 3);
        string sceneName = GetChapterSceneName(currentChapterIndex);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("GameFlow: chapter scene name is empty for chapter " + currentChapterIndex);
            return;
        }

        LoadSceneWithTransition(sceneName, transitionMode, inheritedLetterboxHeight);
    }

    public void GoToEnding()
    {
        LoadSceneWithTransition(
            endingSceneName,
            FilmTransitionController.SceneTransitionMode.FullExitAndEnter,
            0f);
    }

    public void RefreshSceneReferences()
    {
        if (transitionController == null)
        {
            FilmTransitionController sceneTransition =
                FindFirstObjectByType<FilmTransitionController>(FindObjectsInactive.Include);

            if (sceneTransition != null)
                RegisterTransitionController(sceneTransition);
        }

        ApplyCurrentChapterRoomState();
    }

    private void LoadSceneWithTransition(
        string sceneName,
        FilmTransitionController.SceneTransitionMode transitionMode,
        float inheritedLetterboxHeight)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("GameFlow: cannot load an empty scene name.");
            return;
        }

        if (transitionController == null)
        {
            transitionController =
                FindFirstObjectByType<FilmTransitionController>(FindObjectsInactive.Include);
        }

        if (transitionController != null)
        {
            if (transitionMode == FilmTransitionController.SceneTransitionMode.FromExistingWhiteCover)
            {
                transitionController.PlayExitToSceneFromExistingWhiteCover(
                    sceneName,
                    inheritedLetterboxHeight);
            }
            else
            {
                transitionController.PlayExitToScene(sceneName, transitionMode);
            }

            return;
        }

        Debug.LogWarning("GameFlow: FilmTransitionController missing. Loading scene directly: " + sceneName);
        revealRoomAfterDirectLoad = true;
        SceneManager.LoadScene(sceneName);
    }

    private string GetChapterSceneName(int chapterIndex)
    {
        switch (chapterIndex)
        {
            case 1:
                return chapterOneSceneName;
            case 2:
                return chapterTwoSceneName;
            case 3:
                return chapterThreeSceneName;
            default:
                return "";
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (verboseDebug)
            Debug.Log("GameFlow: scene loaded " + scene.name + "; refreshing scene references.");

        StartCoroutine(RefreshSceneReferencesNextFrame());
    }

    private IEnumerator RefreshSceneReferencesNextFrame()
    {
        yield return null;
        RefreshSceneReferences();

        if (revealRoomAfterDirectLoad)
        {
            revealRoomAfterDirectLoad = false;

            ChapterRoomManager roomManager =
                FindFirstObjectByType<ChapterRoomManager>(FindObjectsInactive.Include);

            if (roomManager != null)
                roomManager.OnRoomRevealComplete();
        }
    }

    private void ApplyCurrentChapterRoomState()
    {
        ChapterRoomManager roomManager =
            FindFirstObjectByType<ChapterRoomManager>(FindObjectsInactive.Include);

        if (roomManager != null)
            roomManager.ApplyChapterStateFromGameFlow();
    }
}
