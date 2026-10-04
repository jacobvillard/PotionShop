using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonAudio : MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler
{
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 1f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayOneShot(
            hoverSound,
            hoverVolume
        );
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayOneShot(
            clickSound,
            clickVolume
        );
    }
}