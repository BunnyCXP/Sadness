using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EndingSlideshow : MonoBehaviour
{
    [Serializable]
    public class EndingSlide
    {
        public Sprite image;
        public float duration = 2f;
        public bool useFade = true;
    }

    [Header("UI Images")]
    public Image imageA;
    public Image imageB;

    [Header("Fade Overlay")]
    public Image fadeOverlayImage;
    public CanvasGroup fadeOverlayGroup;

    [Header("Auto Fill From Assets Folder")]
    public bool autoFillFromAssetsFolderOnStart = true;

    [Tooltip("你的图片文件夹路径，例如 Assets/ENDING")]
    public string assetsSlidesFolder = "Assets/ENDING";

    public float defaultSlideDuration = 2f;
    public bool defaultUseFade = true;

    [Header("Slides")]
    public EndingSlide[] slides;

    [Header("Timing")]
    public float startFadeDuration = 1.2f;
    public float crossFadeDuration = 0.7f;
    public float endFadeDuration = 1.8f;
    public float finalBlackDuration = 2f;

    [Header("Music")]
    public AudioSource musicSource;
    public AudioClip endingMusic;
    [Range(0f, 1f)] public float musicVolume = 1f;
    public float musicFadeInDuration = 1.5f;
    public float musicFadeOutDuration = 2f;

    [Header("Input")]
    public bool allowSkip = true;
    public KeyCode skipKey = KeyCode.Space;

    [Header("After Ending")]
    public bool loadSceneAfterEnding = false;
    public string nextSceneName = "MainMenu";

    private bool showingImageA = true;
    private bool skipRequested;

    private void Start()
    {
#if UNITY_EDITOR
        if (autoFillFromAssetsFolderOnStart)
        {
            AutoFillSlidesFromAssetsFolder();
        }
#endif

        StartCoroutine(PlayEnding());
    }

    private void Update()
    {
        if (allowSkip && Input.GetKeyDown(skipKey))
        {
            skipRequested = true;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Fill Slides From Assets Folder")]
    private void AutoFillSlidesFromAssetsFolder()
    {
        if (!AssetDatabase.IsValidFolder(assetsSlidesFolder))
        {
            Debug.LogWarning("EndingSlideshow: Folder not found: " + assetsSlidesFolder);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { assetsSlidesFolder });
        List<Sprite> foundSprites = new List<Sprite>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite != null)
            {
                foundSprites.Add(sprite);
            }
        }

        foundSprites.Sort(CompareSpriteNamesByNumbers);

        List<EndingSlide> result = new List<EndingSlide>();

        foreach (Sprite sprite in foundSprites)
        {
            EndingSlide slide = new EndingSlide
            {
                image = sprite,
                duration = defaultSlideDuration,
                useFade = defaultUseFade
            };

            result.Add(slide);
        }

        slides = result.ToArray();

        EditorUtility.SetDirty(this);

        Debug.Log("EndingSlideshow: Auto-filled " + slides.Length + " slides from " + assetsSlidesFolder);
    }
#endif

    private int CompareSpriteNamesByNumbers(Sprite a, Sprite b)
    {
        List<int> aNumbers = ExtractNumbers(a.name);
        List<int> bNumbers = ExtractNumbers(b.name);

        int count = Mathf.Max(aNumbers.Count, bNumbers.Count);

        for (int i = 0; i < count; i++)
        {
            int aValue = i < aNumbers.Count ? aNumbers[i] : 0;
            int bValue = i < bNumbers.Count ? bNumbers[i] : 0;

            if (aValue != bValue)
            {
                return aValue.CompareTo(bValue);
            }
        }

        return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
    }

    private List<int> ExtractNumbers(string text)
    {
        List<int> numbers = new List<int>();

        MatchCollection matches = Regex.Matches(text, @"\d+");

        foreach (Match match in matches)
        {
            if (int.TryParse(match.Value, out int number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    private IEnumerator PlayEnding()
    {
        SetupImages();
        SetupFadeOverlay();

        if (fadeOverlayGroup != null)
        {
            fadeOverlayGroup.alpha = 1f;
        }

        PlayMusic();

        yield return FadeOverlay(1f, 0f, startFadeDuration);

        if (slides == null || slides.Length == 0)
        {
            Debug.LogWarning("EndingSlideshow: No slides assigned.");
        }
        else
        {
            for (int i = 0; i < slides.Length; i++)
            {
                if (skipRequested)
                {
                    break;
                }

                yield return ShowSlide(slides[i]);
            }
        }

        yield return FadeMusic(
            musicSource != null ? musicSource.volume : 0f,
            0f,
            musicFadeOutDuration
        );

        yield return FadeOverlay(0f, 1f, endFadeDuration);
        yield return Wait(finalBlackDuration);

        if (loadSceneAfterEnding && !string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private IEnumerator ShowSlide(EndingSlide slide)
    {
        if (slide == null || slide.image == null)
        {
            yield break;
        }

        Image currentImage = showingImageA ? imageA : imageB;
        Image previousImage = showingImageA ? imageB : imageA;

        showingImageA = !showingImageA;

        if (currentImage == null)
        {
            yield break;
        }

        currentImage.sprite = slide.image;
        ResetImage(currentImage);

        if (!slide.useFade || crossFadeDuration <= 0f)
        {
            SetImageAlpha(currentImage, 1f);
            SetImageAlpha(previousImage, 0f);

            yield return Wait(slide.duration);
            yield break;
        }

        SetImageAlpha(currentImage, 0f);

        float timer = 0f;

        while (timer < crossFadeDuration)
        {
            if (skipRequested)
            {
                yield break;
            }

            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / crossFadeDuration);
            float eased = EaseInOut(t);

            SetImageAlpha(currentImage, eased);
            SetImageAlpha(previousImage, 1f - eased);

            yield return null;
        }

        SetImageAlpha(currentImage, 1f);
        SetImageAlpha(previousImage, 0f);

        yield return Wait(slide.duration);
    }

    private void SetupImages()
    {
        SetupImage(imageA);
        SetupImage(imageB);
    }

    private void SetupImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.gameObject.SetActive(true);
        image.preserveAspect = true;
        image.raycastTarget = false;

        ResetImage(image);
        SetImageAlpha(image, 0f);
    }

    private void ResetImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void SetupFadeOverlay()
    {
        if (fadeOverlayImage == null && fadeOverlayGroup != null)
        {
            fadeOverlayImage = fadeOverlayGroup.GetComponent<Image>();
        }

        if (fadeOverlayGroup == null && fadeOverlayImage != null)
        {
            fadeOverlayGroup = fadeOverlayImage.GetComponent<CanvasGroup>();
        }

        if (fadeOverlayImage == null)
        {
            return;
        }

        fadeOverlayImage.raycastTarget = false;

        RectTransform rect = fadeOverlayImage.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void PlayMusic()
    {
        if (musicSource == null || endingMusic == null)
        {
            return;
        }

        musicSource.clip = endingMusic;
        musicSource.loop = false;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.mute = false;
        musicSource.volume = 0f;
        musicSource.Play();

        StartCoroutine(FadeMusic(0f, musicVolume, musicFadeInDuration));
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (fadeOverlayGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            fadeOverlayGroup.alpha = to;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            fadeOverlayGroup.alpha = Mathf.Lerp(from, to, EaseInOut(t));

            yield return null;
        }

        fadeOverlayGroup.alpha = to;
    }

    private IEnumerator FadeMusic(float from, float to, float duration)
    {
        if (musicSource == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            musicSource.volume = to;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            musicSource.volume = Mathf.Lerp(from, to, EaseInOut(t));

            yield return null;
        }

        musicSource.volume = to;
    }

    private IEnumerator Wait(float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            if (skipRequested)
            {
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetImageAlpha(Image targetImage, float alpha)
    {
        if (targetImage == null)
        {
            return;
        }

        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
    }

    private float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (6f * t - 15f) + 10f);
    }
}