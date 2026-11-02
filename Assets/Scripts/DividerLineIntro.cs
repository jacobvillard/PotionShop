using System.Collections;
using UnityEngine;

/// <summary>
/// Grows a UI divider horizontally when its scene begins.
/// Attach this to the divider's RectTransform.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DividerLineIntro : MonoBehaviour
{
    [SerializeField] private bool playOnStart;
    [SerializeField, Min(0f)] private float targetWidth = 687.4f;
    [SerializeField, Min(0.01f)] private float duration = 1f;

    private RectTransform divider;
    private Coroutine animationRoutine;

    private void Awake()
    {
        divider = GetComponent<RectTransform>();
        SetWidth(0f);
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateWidth());
    }

    private IEnumerator AnimateWidth()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetWidth(Mathf.Lerp(0f, targetWidth, elapsed / duration));
            yield return null;
        }

        SetWidth(targetWidth);
    }

    private void SetWidth(float width)
    {
        divider.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }
}
