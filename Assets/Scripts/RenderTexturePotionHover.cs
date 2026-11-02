using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Turns the mouse position over a RawImage-backed render texture into a ray from
/// its render camera, then gently enlarges the named potion while that ray hits it.
/// </summary>
public class RenderTexturePotionHover : MonoBehaviour
{
    [Header("Render Texture")]
    [SerializeField] private Camera renderTextureCamera;
    [SerializeField] private RawImage renderTextureImage;

    [Header("Potion")]
    [SerializeField] private string potionObjectName = "HealthPotion";
    [SerializeField, Min(1f)] private float hoverScale = 1.12f;
    [SerializeField, Min(0.01f)] private float scaleResponsiveness = 12f;
    [SerializeField, Min(0.01f)] private float rayDistance = 100f;

    private Transform potion;
    private Vector3 restingScale;
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];

    private void Awake()
    {
        GameObject potionObject = GameObject.Find(potionObjectName);
        if (potionObject == null)
        {
            Debug.LogWarning($"{nameof(RenderTexturePotionHover)} could not find '{potionObjectName}'.", this);
            enabled = false;
            return;
        }

        potion = potionObject.transform;
        restingScale = potion.localScale;
    }

    private void Update()
    {
        if (Mouse.current == null || renderTextureCamera == null || renderTextureImage == null)
            return;

        bool isHovered = TryGetTextureRay(Mouse.current.position.ReadValue(), out Ray ray) && RayHitsPotion(ray);
        Vector3 targetScale = restingScale * (isHovered ? hoverScale : 1f);
        float blend = 1f - Mathf.Exp(-scaleResponsiveness * Time.deltaTime);
        potion.localScale = Vector3.Lerp(potion.localScale, targetScale, blend);
    }

    private bool TryGetTextureRay(Vector2 screenPosition, out Ray ray)
    {
        Canvas canvas = renderTextureImage.canvas;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                renderTextureImage.rectTransform, screenPosition, uiCamera, out Vector2 localPoint))
        {
            ray = default;
            return false;
        }

        Rect rect = renderTextureImage.rectTransform.rect;
        Vector2 viewportPoint = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));

        if (viewportPoint.x <= 0f || viewportPoint.x >= 1f || viewportPoint.y <= 0f || viewportPoint.y >= 1f)
        {
            ray = default;
            return false;
        }

        ray = renderTextureCamera.ViewportPointToRay(viewportPoint);
        return true;
    }

    private bool RayHitsPotion(Ray ray)
    {
        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, rayDistance, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = raycastHits[i].collider;
            if (hitCollider != null &&
                (hitCollider.transform == potion || hitCollider.transform.IsChildOf(potion)))
                return true;
        }

        return false;
    }
}
