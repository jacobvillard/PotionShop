using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger volume for a tripod. A cauldron boils while it remains in this box.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TripodCookBox : MonoBehaviour
{
    [Header("Boiling")]
    [SerializeField, Min(0f)] private float boilingTime = 8f;

    private readonly Dictionary<Cauldron, int> cauldronsInBox = new Dictionary<Cauldron, int>();
    private Cauldron activeCauldron;
    private Coroutine boilingRoutine;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Cauldron cauldron = other.GetComponentInParent<Cauldron>();
        if (cauldron == null)
            return;

        if (!cauldronsInBox.TryGetValue(cauldron, out int colliderCount))
        {
            cauldronsInBox.Add(cauldron, 1);
            TryStartBoiling(cauldron);
            return;
        }

        cauldronsInBox[cauldron] = colliderCount + 1;
    }

    private void OnTriggerExit(Collider other)
    {
        Cauldron cauldron = other.GetComponentInParent<Cauldron>();
        if (cauldron == null || !cauldronsInBox.TryGetValue(cauldron, out int colliderCount))
            return;

        if (colliderCount > 1)
        {
            cauldronsInBox[cauldron] = colliderCount - 1;
            return;
        }

        cauldronsInBox.Remove(cauldron);
        if (activeCauldron == cauldron)
            CancelBoiling();
    }

    private void TryStartBoiling(Cauldron cauldron)
    {
        if (activeCauldron != null || !cauldron.BeginBoiling())
            return;

        activeCauldron = cauldron;
        boilingRoutine = StartCoroutine(BoilCauldron(cauldron));
    }

    private IEnumerator BoilCauldron(Cauldron cauldron)
    {
        float elapsed = 0f;
        while (elapsed < boilingTime && cauldron != null && cauldron.IsBoiling)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cauldron != null && cauldron.IsBoiling)
            cauldron.FinishBoiling();

        activeCauldron = null;
        boilingRoutine = null;
    }

    private void CancelBoiling()
    {
        if (boilingRoutine != null)
            StopCoroutine(boilingRoutine);

        if (activeCauldron != null)
            activeCauldron.StopBoiling();

        boilingRoutine = null;
        activeCauldron = null;
    }

    private void OnDisable()
    {
        CancelBoiling();
        cauldronsInBox.Clear();
    }

    private void OnValidate()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
            boxCollider.isTrigger = true;
    }
}
