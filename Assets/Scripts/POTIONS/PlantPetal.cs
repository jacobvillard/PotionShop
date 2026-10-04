using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Harvests one configured petal, then regrows the plant before it can be harvested again.
/// Attach this to the flower subobject that has the interaction collider.
/// </summary>
public class PlantPetal : MonoBehaviour, IInteractable
{
    [Header("Petal")]
    [SerializeField] private GameObject petalPrefab;
    [Tooltip("A petal is spawned at every assigned point. Leave empty to spawn one at this flower's transform.")]
    [SerializeField] private List<Transform> petalSpawnPoints = new List<Transform>();

    [Header("Regrowth")]
    [Tooltip("The visible flower transform to shrink and regrow. Defaults to this object.")]
    [SerializeField] private Transform plantVisual;
    [Min(0f)]
    [SerializeField] private float regrowDuration = 4f;

    private Vector3 fullyGrownScale;
    private bool isHarvestable = true;
    private Coroutine regrowRoutine;

    private void Awake()
    {
        if (plantVisual == null)
        {
            plantVisual = transform;
        }

        fullyGrownScale = plantVisual.localScale;
    }

    public void Interact()
    {
        if (!isHarvestable)
        {
            return;
        }

        if (petalPrefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot harvest a petal because no Petal Prefab is assigned.", this);
            return;
        }

        if (petalSpawnPoints.Count == 0)
        {
            SpawnPetal(transform);
        }
        else
        {
            foreach (Transform spawnPoint in petalSpawnPoints)
            {
                if (spawnPoint != null)
                {
                    SpawnPetal(spawnPoint);
                }
            }
        }

        isHarvestable = false;
        plantVisual.localScale = Vector3.zero;

        if (regrowRoutine != null)
        {
            StopCoroutine(regrowRoutine);
        }

        regrowRoutine = StartCoroutine(Regrow());
    }

    private void SpawnPetal(Transform spawnPoint)
    {
        Instantiate(petalPrefab, spawnPoint.position, spawnPoint.rotation);
    }

    private IEnumerator Regrow()
    {
        if (regrowDuration <= 0f)
        {
            plantVisual.localScale = fullyGrownScale;
            isHarvestable = true;
            regrowRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < regrowDuration)
        {
            elapsed += Time.deltaTime;
            plantVisual.localScale = Vector3.Lerp(Vector3.zero, fullyGrownScale, elapsed / regrowDuration);
            yield return null;
        }

        plantVisual.localScale = fullyGrownScale;
        isHarvestable = true;
        regrowRoutine = null;
    }

    private void OnDisable()
    {
        if (regrowRoutine != null)
        {
            StopCoroutine(regrowRoutine);
            regrowRoutine = null;
        }
    }
}
