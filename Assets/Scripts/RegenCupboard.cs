using System.Collections;
using System.Linq;
using UnityEngine;

public class RegenCupboard : MonoBehaviour, IStaffable
{
    [SerializeField] private GameObject cupboardPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Check")]
    [SerializeField] private Vector3 checkSize = new Vector3(0.2f, 0.2f, 0.2f);
    [SerializeField] private LayerMask blockingLayers;

    [Header("Spawn Animation")]
    [SerializeField] private float delayBetweenSpawns = 0.12f;
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private float startScale = 0.7f;
    [SerializeField] private float overshootScale = 1.08f;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSfx;
    [Range(0f, 1f)]
    [SerializeField] private float spawnSfxVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool triggerRegeneration;

    private SpawnData[] cachedSpawns;
    private bool regenerating;

    private struct SpawnData
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private void Awake()
    {
        // Cache spawn locations once.
        cachedSpawns = spawnPoints
            .Where(point => point != null)
            .Select(point => new SpawnData
            {
                position = point.position,
                rotation = point.rotation
            })

            // Sort left -> right relative to this object's orientation.
            .OrderByDescending(spawn =>
                Vector3.Dot(spawn.position - transform.position, transform.up))
            .ThenBy(spawn =>
                Vector3.Dot(spawn.position - transform.position, transform.right))
            .ToArray();
    }

    private void Update()
    {
        if (triggerRegeneration)
        {
            triggerRegeneration = false;
            RegenerateCupboard();
        }
    }

    public void RegenerateCupboard()
    {
        if (regenerating)
            return;

        StartCoroutine(RegenerateRoutine());
    }

    private IEnumerator RegenerateRoutine()
    {
        regenerating = true;

        foreach (SpawnData spawn in cachedSpawns)
        {
            if (IsSpawnPointOccupied(spawn))
                continue;

            GameObject cupboard = Instantiate(
                cupboardPrefab,
                spawn.position,
                spawn.rotation
            );

            // Remember its intended prefab scale.
            Vector3 targetScale = cupboard.transform.localScale;

            // Start smaller.
            cupboard.transform.localScale = targetScale * startScale;

            // Play spawn sound.
            if (AudioManager.Instance != null && spawnSfx != null)
            {
                AudioManager.Instance.PlayOneShot(
                    spawnSfx,
                    spawnSfxVolume
                );
            }

            // Animate this cupboard appearing.
            yield return AnimateSpawn(cupboard.transform, targetScale);

            // Small delay before moving onto the next one.
            if (delayBetweenSpawns > 0f)
                yield return new WaitForSeconds(delayBetweenSpawns);
        }

        regenerating = false;
    }

    private IEnumerator AnimateSpawn(
        Transform target,
        Vector3 targetScale)
    {
        if (target == null)
            yield break;

        Vector3 smallScale = targetScale * startScale;
        Vector3 bigScale = targetScale * overshootScale;

        float firstHalf = animationDuration * 0.65f;
        float timer = 0f;

        // Small -> slightly oversized.
        while (timer < firstHalf)
        {
            if (target == null)
                yield break;

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / firstHalf);
            t = EaseOutBack(t);

            target.localScale = Vector3.LerpUnclamped(
                smallScale,
                bigScale,
                t
            );

            yield return null;
        }

        float secondHalf = animationDuration - firstHalf;
        timer = 0f;

        // Oversized -> normal.
        while (timer < secondHalf)
        {
            if (target == null)
                yield break;

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / secondHalf);

            target.localScale = Vector3.Lerp(
                bigScale,
                targetScale,
                t
            );

            yield return null;
        }

        target.localScale = targetScale;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f +
               c3 * Mathf.Pow(t - 1f, 3f) +
               c1 * Mathf.Pow(t - 1f, 2f);
    }

    private bool IsSpawnPointOccupied(SpawnData spawn)
    {
        return Physics.CheckBox(
            spawn.position,
            checkSize * 0.5f,
            spawn.rotation,
            blockingLayers
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
            return;

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null)
                continue;

            Gizmos.matrix = Matrix4x4.TRS(
                spawnPoint.position,
                spawnPoint.rotation,
                Vector3.one
            );

            Gizmos.DrawWireCube(
                Vector3.zero,
                checkSize
            );
        }
    }

    public void GetStaffedBiatch()
    {
        RegenerateCupboard();
    }
}