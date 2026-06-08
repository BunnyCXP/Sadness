using System.Collections;
using TMPro;
using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public AudioClip clip;
    [TextArea(2, 4)] public string subtitle;
    public float fallbackDuration = 1.5f;
    public float minDuration = 0f;
}

/// <summary>
/// Reusable chapter dialogue player with lightweight audio/subtitle synchronization.
/// It deliberately has no dependency on RoomInteraction.
/// </summary>
[DisallowMultipleComponent]
public class ChapterDialogueSubtitlePlayer : MonoBehaviour
{
    public AudioSource audioSource;
    public CanvasGroup subtitleCanvasGroup;
    public TMP_Text subtitleText;
    public float fadeDuration = 0.12f;
    public float gapBetweenLines = 0.05f;
    public bool hideAfterLine = true;

    private Coroutine playRoutine;

    void Awake()
    {
        HideImmediate();
    }

    public void PlayLines(DialogueLine[] lines)
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayLinesRoutine(lines));
    }

    public IEnumerator PlayLinesRoutine(DialogueLine[] lines)
    {
        if (lines == null)
            yield break;

        for (int i = 0; i < lines.Length; i++)
        {
            yield return PlayLineRoutine(lines[i]);

            if (gapBetweenLines > 0f && i < lines.Length - 1)
                yield return new WaitForSeconds(gapBetweenLines);
        }

        playRoutine = null;
    }

    public IEnumerator PlayLineRoutine(DialogueLine line)
    {
        if (line == null)
            yield break;

        if (subtitleCanvasGroup != null)
            subtitleCanvasGroup.gameObject.SetActive(true);

        if (subtitleText != null)
        {
            subtitleText.text = string.IsNullOrEmpty(line.speaker)
                ? line.subtitle
                : line.speaker + ": " + line.subtitle;
        }

        yield return FadeSubtitle(GetAlpha(), 1f);

        float duration = Mathf.Max(line.minDuration, line.fallbackDuration);

        if (audioSource != null && line.clip != null)
        {
            audioSource.clip = line.clip;
            audioSource.Play();
            duration = Mathf.Max(line.minDuration, line.clip.length);
        }

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (hideAfterLine)
            yield return FadeSubtitle(GetAlpha(), 0f);
    }

    public void HideImmediate()
    {
        if (audioSource != null)
            audioSource.Stop();

        if (subtitleText != null)
            subtitleText.text = "";

        if (subtitleCanvasGroup != null)
        {
            subtitleCanvasGroup.alpha = 0f;
            subtitleCanvasGroup.blocksRaycasts = false;
            subtitleCanvasGroup.interactable = false;
        }
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        if (audioSource == null)
            Debug.LogWarning("ChapterDialogueSubtitlePlayer: audioSource is missing; subtitle-only playback will still work.", this);
        if (subtitleCanvasGroup == null || subtitleText == null)
            Debug.LogWarning("ChapterDialogueSubtitlePlayer: subtitle references are incomplete.", this);
    }

    private IEnumerator FadeSubtitle(float from, float to)
    {
        if (subtitleCanvasGroup == null)
            yield break;

        float duration = Mathf.Max(0f, fadeDuration);

        if (duration <= 0f)
        {
            subtitleCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            subtitleCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        subtitleCanvasGroup.alpha = to;
    }

    private float GetAlpha()
    {
        return subtitleCanvasGroup != null ? subtitleCanvasGroup.alpha : 0f;
    }
}
