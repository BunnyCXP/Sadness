using UnityEngine;

public class FilmExitTrigger : MonoBehaviour
{
    public int filmIndex = 1;
    public FilmTransitionController.SceneTransitionMode transitionMode =
        FilmTransitionController.SceneTransitionMode.FromExistingWhiteCover;

    [Header("Existing Finale Cover")]
    public bool finaleAlreadyHasWhiteCover = true;
    public bool inheritLetterboxHeightFromFinale = true;
    public RectTransform existingTopBar;
    public RectTransform existingBottomBar;
    public float fallbackInheritedLetterboxHeight = 200f;

    public bool triggerOnce = true;
    public bool verboseDebug = true;

    [SerializeField] private bool hasTriggered;

    public void FinishFilm()
    {
        if (triggerOnce && hasTriggered)
            return;

        hasTriggered = true;

        if (verboseDebug)
            Debug.Log("FilmExitTrigger: finishing film " + filmIndex);

        if (GameFlowManager.Instance == null)
        {
            Debug.LogWarning("FilmExitTrigger: GameFlowManager.Instance is missing.");
            return;
        }

        if (finaleAlreadyHasWhiteCover)
        {
            GameFlowManager.Instance.FinishFilmFromExistingWhiteCover(
                filmIndex,
                GetInheritedLetterboxHeight());
        }
        else
        {
            GameFlowManager.Instance.FinishFilm(
                filmIndex,
                FilmTransitionController.SceneTransitionMode.FullExitAndEnter);
        }
    }

    public float GetInheritedLetterboxHeight()
    {
        if (inheritLetterboxHeightFromFinale)
        {
            float height = ReadBarHeight(existingTopBar);

            if (height <= 0f)
                height = ReadBarHeight(existingBottomBar);

            VoidTrainFinaleSequence finale = GetComponent<VoidTrainFinaleSequence>();

            if (height <= 0f && finale != null)
            {
                height = ReadBarHeight(finale.topBar);

                if (height <= 0f)
                    height = ReadBarHeight(finale.bottomBar);

                if (height <= 0f)
                    height = finale.letterboxHeight;
            }

            if (height > 0f)
                return height;
        }

        return Mathf.Max(0f, fallbackInheritedLetterboxHeight);
    }

    private float ReadBarHeight(RectTransform bar)
    {
        return bar != null ? Mathf.Abs(bar.sizeDelta.y) : 0f;
    }

    public void FinishFilmOne()
    {
        filmIndex = 1;
        FinishFilm();
    }

    public void FinishFilmTwo()
    {
        filmIndex = 2;
        FinishFilm();
    }

    public void FinishFilmThree()
    {
        filmIndex = 3;
        FinishFilm();
    }
}
