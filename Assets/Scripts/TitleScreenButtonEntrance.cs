using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Fades title-screen buttons in sequentially, ordered by their UI sibling position.</summary>
[DisallowMultipleComponent]
public class TitleScreenButtonEntrance : MonoBehaviour
{
    [SerializeField] private bool playOnEnable;
    [SerializeField, Min(0f)] private float initialDelay = 0.35f;
    [SerializeField, Min(0f)] private float staggerDelay = 0.12f;
    [SerializeField, Min(0.01f)] private float duration = 0.26f;

    private CanvasGroup canvasGroup;
    private Coroutine entranceRoutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    public void Play()
    {
        if (entranceRoutine != null)
            StopCoroutine(entranceRoutine);
        entranceRoutine = StartCoroutine(PlayEntrance());
    }

    private IEnumerator PlayEntrance()
    {
        // Let all menu buttons register before determining their order.
        yield return null;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        yield return new WaitForSecondsRealtime(initialDelay + GetSequenceIndex() * staggerDelay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress);
            canvasGroup.alpha = progress;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    private int GetSequenceIndex()
    {
        Transform parent = transform.parent;
        if (parent == null)
            return 0;

        TitleScreenButtonEntrance[] allEntrances = parent.GetComponentsInChildren<TitleScreenButtonEntrance>(true);
        List<TitleScreenButtonEntrance> siblings = new List<TitleScreenButtonEntrance>();
        foreach (TitleScreenButtonEntrance entrance in allEntrances)
            if (entrance.transform.parent == parent) siblings.Add(entrance);

        siblings.Sort((first, second) => first.transform.GetSiblingIndex().CompareTo(second.transform.GetSiblingIndex()));
        return siblings.IndexOf(this);
    }
}
