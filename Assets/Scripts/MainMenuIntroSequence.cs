using System.Collections;
using UnityEngine;

/// <summary>Coordinates the title screen's visual entrance in one sequence.</summary>
public class MainMenuIntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject particles;
    [SerializeField] private GameObject magicCircle;
    [SerializeField] private GameObject title;
    [SerializeField] private GameObject subtitle;
    [SerializeField] private DividerLineIntro divider;
    [SerializeField] private Transform buttonContainer;
    [SerializeField, Min(0.01f)] private float backgroundFadeDuration = 0.85f;
    [SerializeField, Min(0f)] private float titleFadeDelay = 0.2f;
    [SerializeField, Min(0.01f)] private float titleFadeDuration = 0.5f;

    private IEnumerator Start()
    {
        yield return null;

        CanvasGroup particlesGroup = PrepareHidden(particles);
        CanvasGroup circleGroup = PrepareHidden(magicCircle);
        CanvasGroup titleGroup = PrepareHidden(title);
        CanvasGroup subtitleGroup = PrepareHidden(subtitle);
        TitleScreenButtonEntrance[] buttonEntrances = buttonContainer != null
            ? buttonContainer.GetComponentsInChildren<TitleScreenButtonEntrance>(true)
            : new TitleScreenButtonEntrance[0];

        foreach (TitleScreenButtonEntrance entrance in buttonEntrances)
            entrance.GetComponent<CanvasGroup>().alpha = 0f;

        Coroutine backgroundFade = StartCoroutine(Fade(backgroundFadeDuration, particlesGroup, circleGroup));
        yield return new WaitForSecondsRealtime(titleFadeDelay);
        yield return Fade(titleFadeDuration, titleGroup, subtitleGroup);
        yield return backgroundFade;

        if (divider != null)
            divider.Play();

        foreach (TitleScreenButtonEntrance entrance in buttonEntrances)
            entrance.Play();
    }

    private static CanvasGroup PrepareHidden(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static IEnumerator Fade(float duration, params CanvasGroup[] groups)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            progress = progress * progress * (3f - 2f * progress);
            foreach (CanvasGroup group in groups)
                if (group != null) group.alpha = progress;
            yield return null;
        }

        foreach (CanvasGroup group in groups)
        {
            if (group == null)
                continue;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }
}
