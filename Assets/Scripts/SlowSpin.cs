using UnityEngine;

/// <summary>Rotates this object at a constant speed around its local Z axis.</summary>
public class SlowSpin : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 8f;

    private Quaternion startingRotation;
    private float startTime;

    private void OnEnable()
    {
        startingRotation = transform.localRotation;
        startTime = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        float angle = (Time.unscaledTime - startTime) * degreesPerSecond;
        transform.localRotation = startingRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
