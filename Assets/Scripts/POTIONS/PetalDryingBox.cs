using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger volume that dries Fresh petals while they remain inside it.
/// Requires a BoxCollider with Is Trigger enabled.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class PetalDryingBox : MonoBehaviour
{
    [SerializeField, Min(0f)] private float dryingTime = 10f;

    private readonly Dictionary<Petal, int> petalsInBox = new Dictionary<Petal, int>();

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Petal petal = other.GetComponentInParent<Petal>();
        if (petal == null)
        {
            return;
        }

        if (!petalsInBox.TryGetValue(petal, out int colliderCount))
        {
            petalsInBox.Add(petal, 1);
            petal.BeginDrying(this, dryingTime);
            return;
        }

        petalsInBox[petal] = colliderCount + 1;
    }

    private void OnTriggerExit(Collider other)
    {
        Petal petal = other.GetComponentInParent<Petal>();
        if (petal == null || !petalsInBox.TryGetValue(petal, out int colliderCount))
        {
            return;
        }

        if (colliderCount <= 1)
        {
            petalsInBox.Remove(petal);
            petal.StopDrying(this);
            return;
        }

        petalsInBox[petal] = colliderCount - 1;
    }

    private void OnDisable()
    {
        foreach (Petal petal in petalsInBox.Keys)
        {
            if (petal != null)
            {
                petal.StopDrying(this);
            }
        }

        petalsInBox.Clear();
    }

    private void OnValidate()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }
}
