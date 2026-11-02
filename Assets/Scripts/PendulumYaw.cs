using UnityEngine;

/// <summary>Gently swings this object left and right around its starting local Y rotation.</summary>
public class PendulumYaw : MonoBehaviour
{
    [SerializeField, Range(0f, 45f)] private float angle = 2f;
    [SerializeField, Min(0.01f)] private float cyclesPerSecond = 0.35f;

    private Quaternion startingRotation;

    private void Awake()
    {
        startingRotation = transform.localRotation;
    }

    private void Update()
    {
        float yaw = Mathf.Sin(Time.time * cyclesPerSecond * Mathf.PI * 2f) * angle;
        transform.localRotation = startingRotation * Quaternion.Euler(0f, yaw, 0f);
    }
}
