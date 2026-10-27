using UnityEngine;

/// <summary>
/// Temporarily rotates this object around its local Z axis.
/// </summary>
public class TempOven : MonoBehaviour
{
    [Header("Spin")]
    [SerializeField] private bool isSpinning = true;
    [SerializeField] private float spinSpeed = 90f;

    private void Update()
    {
        if (!isSpinning)
            return;

        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }
}
