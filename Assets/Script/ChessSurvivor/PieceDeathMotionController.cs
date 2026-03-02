using System.Collections;
using System;
using UnityEngine;

public class PieceDeathMotionController : MonoBehaviour
{
    [Header("Pre Launch Bounce")]
    [SerializeField] private int preLaunchBounceCount = 3;
    [SerializeField] private float preLaunchSquashXZ = 0.84f;
    [SerializeField] private float preLaunchSquashY = 1.18f;
    [SerializeField] private float preLaunchStretchXZ = 1.1f;
    [SerializeField] private float preLaunchStretchY = 0.9f;
    [SerializeField] private float preLaunchBounceHalfDuration = 0.05f;
    [SerializeField] private float preLaunchForwardOffset = 0.28f;
    [SerializeField] private float preLaunchForwardDuration = 0.06f;

    [Header("Knockback")]
    [SerializeField] private float knockbackHorizontalForce = 9.5f;
    [SerializeField] private float knockbackUpwardForce = 6.5f;
    [SerializeField] private float knockbackTorque = 14f;
    [SerializeField] private float dummyLinearDamping = 0.55f;
    [SerializeField] private float dummyAngularDamping = 0.35f;

    public void Play(ChessPiece victim, Vector3 attackerPos, float lifeTime, Action onBeforeLaunch = null)
    {
        if (victim == null)
        {
            return;
        }

        StartCoroutine(BounceThenLaunch(victim, attackerPos, lifeTime, onBeforeLaunch));
    }

    private IEnumerator BounceThenLaunch(ChessPiece victim, Vector3 attackerPos, float lifeTime, Action onBeforeLaunch)
    {
        if (victim == null)
        {
            yield break;
        }

        Rigidbody rb = victim.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = victim.gameObject.AddComponent<Rigidbody>();
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        Vector3 basePos = victim.transform.position;
        Vector3 baseScale = victim.transform.localScale;
        Vector3 toVictim = basePos - attackerPos;
        toVictim.y = 0f;
        if (toVictim.sqrMagnitude < 0.0001f)
        {
            toVictim = Vector3.right;
        }
        Vector3 launchDir = toVictim.normalized;
        Vector3 preImpactPos = basePos + launchDir * preLaunchForwardOffset;
        yield return LerpVictimPosition(victim.transform, basePos, preImpactPos, preLaunchForwardDuration);
        if (victim == null)
        {
            yield break;
        }

        Vector3 pulseSquash = new(
            baseScale.x * preLaunchSquashXZ,
            baseScale.y * preLaunchSquashY,
            baseScale.z * preLaunchSquashXZ);
        Vector3 pulseStretch = new(
            baseScale.x * preLaunchStretchXZ,
            baseScale.y * preLaunchStretchY,
            baseScale.z * preLaunchStretchXZ);

        int bounceCount = Mathf.Max(0, preLaunchBounceCount);
        for (int i = 0; i < bounceCount; i++)
        {
            Vector3 pulse = (i & 1) == 0 ? pulseSquash : pulseStretch;
            yield return LerpVictimScale(victim.transform, baseScale, pulse, preLaunchBounceHalfDuration);
            yield return LerpVictimScale(victim.transform, pulse, baseScale, preLaunchBounceHalfDuration);
        }

        if (victim == null)
        {
            yield break;
        }

        victim.transform.localScale = baseScale;
        onBeforeLaunch?.Invoke();
        Launch(victim, attackerPos, lifeTime);
    }

    private void Launch(ChessPiece victim, Vector3 attackerPos, float lifeTime)
    {
        if (victim == null)
        {
            return;
        }

        Collider[] colliders = victim.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        ChessPiece pieceComp = victim.GetComponent<ChessPiece>();
        if (pieceComp != null)
        {
            pieceComp.enabled = false;
        }

        Rigidbody rb = victim.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = victim.gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.mass = 0.65f;
        rb.linearDamping = dummyLinearDamping;
        rb.angularDamping = dummyAngularDamping;

        Vector3 dir = (victim.transform.position - attackerPos);
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.right;
        }

        dir = dir.normalized;
        Vector3 impulse = (dir * knockbackHorizontalForce) + (Vector3.up * knockbackUpwardForce);
        rb.AddForce(impulse, ForceMode.Impulse);
        Vector3 torqueAxis = new(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1.2f, 1.2f),
            UnityEngine.Random.Range(-1f, 1f));
        rb.AddTorque(torqueAxis.normalized * knockbackTorque, ForceMode.Impulse);

        PlayAttachedDeathEffects(victim);
        Destroy(victim.gameObject, lifeTime);
    }

    private static IEnumerator LerpVictimPosition(Transform victim, Vector3 from, Vector3 to, float duration)
    {
        if (victim == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (victim == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            victim.position = Vector3.LerpUnclamped(from, to, n);
            yield return null;
        }

        if (victim != null)
        {
            victim.position = to;
        }
    }

    private static IEnumerator LerpVictimScale(Transform victim, Vector3 from, Vector3 to, float duration)
    {
        if (victim == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (victim == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            victim.localScale = Vector3.LerpUnclamped(from, to, n);
            yield return null;
        }

        if (victim != null)
        {
            victim.localScale = to;
        }
    }

    private static void PlayAttachedDeathEffects(ChessPiece victim)
    {
        ParticleSystem[] particles = victim.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ActivateEffectHierarchy(victim.transform, particles[i].transform);
        }
    }

    private static void ActivateEffectHierarchy(Transform root, Transform effectNode)
    {
        Transform current = effectNode;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            if (current == root)
            {
                break;
            }

            current = current.parent;
        }
    }
}
