using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundSpikeBurst : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] spikePrefabs;

    [Header("Spawn Shape")]
    [SerializeField] private int count = 1;
    [SerializeField] private float minSpawnRadius = 0.2f;
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float buryDepth = 0.35f;
    [SerializeField] private float riseHeight = 0.45f;
    [SerializeField] private float outwardRiseDistance = 0.35f;

    [Header("Variation")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.7f, 1.4f);
    [SerializeField] private Vector2 riseTimeRange = new Vector2(0.08f, 0.16f);
    [SerializeField] private Vector2 spawnDelayRange = new Vector2(0.0f, 0.08f);

    [Header("Orientation")]
    [SerializeField] private Vector3 spikeAxisLocal = Vector3.up;
    [SerializeField] private bool invertSpikeAxis = false;
    [SerializeField] private bool randomInvertSpikeAxis = true;
    [SerializeField, Range(0f, 1f)] private float randomInvertChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float outwardTilt = 0.7f;

    [Header("Lifetime")]
    [SerializeField] private bool autoDestroySpike = true;
    [SerializeField] private float spikeLifetime = 2f;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("SFX")]
    [SerializeField] private bool playImpactSfx = true;
    [SerializeField] private string impactSfxKey = "Rock";

    [Header("VFX")]
    [SerializeField] private GameObject impactParticlePrefab;
    [SerializeField] private Vector3 impactParticleOffset = Vector3.zero;
    [SerializeField] private bool alignParticleToNormal = true;
    [SerializeField] private float impactParticleLifetime = 2f;
    
    public float EstimatedLifetime =>
        Mathf.Max(
            Mathf.Max(0f, spikeLifetime) + Mathf.Max(0f, fadeOutDuration) + Mathf.Max(0f, spawnDelayRange.y) + Mathf.Max(0f, riseTimeRange.y),
            Mathf.Max(0f, impactParticleLifetime)
        ) + 0.1f;

    public void Play(Vector3 hitPos, Vector3 normal)
    {
        if (spikePrefabs == null || spikePrefabs.Length == 0)
        {
            Debug.LogWarning("GroundSpikeBurst requires at least one spike prefab.");
            return;
        }

        TryPlayImpactSfx();

        Vector3 up = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        TrySpawnImpactParticle(hitPos, up);
        BuildBasis(up, out Vector3 tangent, out Vector3 bitangent);

        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float maxRadius = Mathf.Max(minSpawnRadius, radius);
            float distance = Mathf.Lerp(minSpawnRadius, maxRadius, Mathf.Sqrt(Random.value));
            Vector3 radial = (Mathf.Cos(angle) * tangent + Mathf.Sin(angle) * bitangent).normalized;

            Vector3 basePos = hitPos + radial * distance;
            Vector3 outward = Vector3.ProjectOnPlane(basePos - hitPos, up).normalized;
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = radial;
            }

            SpawnSpike(basePos, up, outward);
        }
    }

    public void PlayAtPositions(Vector3 centerPos, Vector3 normal, IReadOnlyList<Vector3> spawnPositions)
    {
        if (spikePrefabs == null || spikePrefabs.Length == 0 || spawnPositions == null || spawnPositions.Count == 0)
        {
            return;
        }

        TryPlayImpactSfx();

        Vector3 up = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        TrySpawnImpactParticle(centerPos, up);
        int spawnTotal = Mathf.Min(Mathf.Max(1, count), spawnPositions.Count);
        int[] sampledIndices = BuildRandomUniqueIndices(spawnPositions.Count, spawnTotal);
        for (int i = 0; i < sampledIndices.Length; i++)
        {
            Vector3 basePos = spawnPositions[sampledIndices[i]];
            Vector3 outward = Vector3.ProjectOnPlane(basePos - centerPos, up).normalized;
            if (outward.sqrMagnitude < 0.0001f)
            {
                BuildBasis(up, out Vector3 tangent, out _);
                outward = tangent;
            }

            SpawnSpike(basePos, up, outward);
        }
    }

    private void SpawnSpike(Vector3 basePos, Vector3 up, Vector3 outward)
    {
        Vector3 startPos = basePos - up * buryDepth;
        Vector3 endPos = basePos + up * riseHeight + outward * outwardRiseDistance;

        GameObject prefab = spikePrefabs[Random.Range(0, spikePrefabs.Length)];
        GameObject spikeGo = Instantiate(prefab, startPos, prefab.transform.rotation, transform);
        Transform spike = spikeGo.transform;
        Vector3 baseScale = spike.localScale;
        Vector3 desiredAxis = (up + outward * outwardTilt).normalized;
        bool shouldInvert = invertSpikeAxis;
        if (randomInvertSpikeAxis && Random.value < randomInvertChance)
        {
            shouldInvert = !shouldInvert;
        }

        if (shouldInvert)
        {
            desiredAxis = -desiredAxis;
        }

        AlignSpikeAxis(spike, spikeAxisLocal, desiredAxis);

        float targetScale = Random.Range(scaleRange.x, scaleRange.y);
        float riseDuration = Random.Range(riseTimeRange.x, riseTimeRange.y);
        float delay = Random.Range(spawnDelayRange.x, spawnDelayRange.y);

        StartCoroutine(Emerge(spike, startPos, endPos, baseScale, targetScale, riseDuration, delay, useUnscaledTime));

        if (autoDestroySpike)
        {
            StartCoroutine(FadeAndDestroy(spikeGo, up, spikeLifetime, fadeOutDuration, useUnscaledTime));
        }
    }

    private static int[] BuildRandomUniqueIndices(int sourceCount, int pickCount)
    {
        int[] indices = new int[sourceCount];
        for (int i = 0; i < sourceCount; i++)
        {
            indices[i] = i;
        }

        for (int i = sourceCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int[] result = new int[pickCount];
        for (int i = 0; i < pickCount; i++)
        {
            result[i] = indices[i];
        }

        return result;
    }

    private void TryPlayImpactSfx()
    {
        if (!playImpactSfx || string.IsNullOrWhiteSpace(impactSfxKey))
        {
            return;
        }

        SoundManager.Instance?.PlaySfx(impactSfxKey);
    }

    private void TrySpawnImpactParticle(Vector3 centerPos, Vector3 normal)
    {
        if (impactParticlePrefab == null)
        {
            return;
        }

        Vector3 up = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Quaternion rotation = alignParticleToNormal
            ? Quaternion.FromToRotation(Vector3.up, up)
            : impactParticlePrefab.transform.rotation;
        GameObject particleGo = Instantiate(
            impactParticlePrefab,
            centerPos + impactParticleOffset,
            rotation,
            transform
        );

        if (impactParticleLifetime > 0f)
        {
            Destroy(particleGo, impactParticleLifetime);
        }
    }

    private static void BuildBasis(Vector3 up, out Vector3 tangent, out Vector3 bitangent)
    {
        tangent = Vector3.Cross(up, Vector3.right);
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector3.Cross(up, Vector3.forward);
        }

        tangent.Normalize();
        bitangent = Vector3.Cross(up, tangent).normalized;
    }

    private static void AlignSpikeAxis(Transform spike, Vector3 localAxis, Vector3 desiredAxis)
    {
        Vector3 safeLocalAxis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        Vector3 currentAxisWorld = spike.TransformDirection(safeLocalAxis).normalized;
        if (currentAxisWorld.sqrMagnitude < 0.0001f || desiredAxis.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion alignDelta = Quaternion.FromToRotation(currentAxisWorld, desiredAxis.normalized);
        spike.rotation = alignDelta * spike.rotation;
    }

    private static IEnumerator Emerge(
        Transform spike,
        Vector3 startPos,
        Vector3 endPos,
        Vector3 baseScale,
        float targetScale,
        float duration,
        float delay,
        bool useUnscaled
    )
    {
        if (delay > 0f)
        {
            if (useUnscaled)
            {
                float wait = 0f;
                while (wait < delay)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(delay);
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic

            spike.position = Vector3.Lerp(startPos, endPos, eased);
            spike.localScale = baseScale * Mathf.Lerp(0.01f, targetScale, eased);

            yield return null;
        }

        spike.position = endPos;
        spike.localScale = baseScale * targetScale;
    }

    private static IEnumerator FadeAndDestroy(GameObject target, Vector3 up, float lifetime, float fadeDuration, bool useUnscaled)
    {
        if (target == null)
        {
            yield break;
        }

        float safeLifetime = Mathf.Max(0f, lifetime);
        float safeFade = Mathf.Clamp(fadeDuration, 0f, safeLifetime);
        float waitTime = Mathf.Max(0f, safeLifetime - safeFade);
        if (waitTime > 0f)
        {
            if (useUnscaled)
            {
                float wait = 0f;
                while (wait < waitTime)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(waitTime);
            }
        }

        Transform root = target.transform;
        Vector3 startScale = root.localScale;
        Vector3 startPos = root.position;
        Vector3 endPos = startPos - up.normalized * 0.35f;

        if (safeFade <= 0f)
        {
            Object.Destroy(target);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < safeFade)
        {
            if (target == null)
            {
                yield break;
            }

            elapsed += useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeFade);

            root.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            root.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        if (target != null)
        {
            Object.Destroy(target);
        }
    }
}
