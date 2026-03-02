using System.Collections;
using UnityEngine;

public class KingKillCinematicController : MonoBehaviour
{
    [SerializeField] private bool enabledCinematic = true;
    [SerializeField] [Range(0.05f, 1f)] private float timeScale = 0.28f;
    [SerializeField] private float moveInDuration = 0.08f;
    [SerializeField] private float holdDuration = 0.16f;
    [SerializeField] private float postActionHoldDuration = 0.04f;
    [SerializeField] private float moveOutDuration = 0.12f;
    [SerializeField] private Vector3 cameraOffset = new(0f, 3.4f, -2.6f);
    [SerializeField] private float lookHeight = 0.55f;
    [Header("Dynamic Framing")]
    [SerializeField] private bool fitKingAndVictim = true;
    [SerializeField] private float zoomOutPerTile = 0.28f;
    [SerializeField] private float minZoomOutDistance = 1f;
    [SerializeField] private float maxZoomOutFactor = 2.2f;
    [SerializeField] private float skillExtraZoomOut = 0.35f;
    [SerializeField] private float sideOrbitAngle = 18f;
    [SerializeField] private AnimationCurve sideOrbitCurve = new(
        new Keyframe(0f, 0f, 0f, 1.8f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve moveInCurve = new(
        new Keyframe(0f, 0f, 0f, 4.5f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve moveOutCurve = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2.5f, 0f));

    private Coroutine routine;
    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;
    private TopDownCameraController cachedFollow;
    private bool cachedFollowWasEnabled;

    public void Play(Transform king, Vector3 victimPos, float actionDuration = 0f, bool isSkillDive = false)
    {
        if (!enabledCinematic || king == null || Camera.main == null)
        {
            return;
        }

        if (routine != null)
        {
            RestoreState();
            StopCoroutine(routine);
        }

        routine = StartCoroutine(Run(king, victimPos, actionDuration, isSkillDive));
    }

    private IEnumerator Run(Transform king, Vector3 victimPos, float actionDuration, bool isSkillDive)
    {
        Camera cam = Camera.main;
        if (cam == null || king == null)
        {
            yield break;
        }

        Transform camTr = cam.transform;
        Vector3 originPos = camTr.position;
        Quaternion originRot = camTr.rotation;

        cachedFollow = cam.GetComponent<TopDownCameraController>();
        cachedFollowWasEnabled = cachedFollow != null && cachedFollow.enabled;
        if (cachedFollowWasEnabled)
        {
            cachedFollow.enabled = false;
        }

        cachedTimeScale = Time.timeScale;
        cachedFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = 0.02f * timeScale;

        Vector3 focus = Vector3.Lerp(king.position, victimPos, 0.55f);
        focus.y = lookHeight;
        float sideSign = ResolveOrbitSign(king.position, victimPos, camTr.right);
        float signedOrbit = sideOrbitAngle * sideSign;
        float zoomFactor = ResolveZoomFactor(king.position, victimPos, isSkillDive);
        Vector3 targetOffset = Quaternion.AngleAxis(signedOrbit, Vector3.up) * (cameraOffset * zoomFactor);
        Vector3 targetPos = focus + targetOffset;
        Quaternion targetRot = Quaternion.LookRotation((focus - targetPos).normalized, Vector3.up);

        yield return LerpCameraWithOrbit(
            camTr,
            originPos,
            originRot,
            targetPos,
            targetRot,
            focus,
            signedOrbit,
            moveInDuration,
            moveInCurve,
            sideOrbitCurve);
        float timeScaleSafe = Mathf.Max(0.01f, timeScale);
        // Skill dive motion uses unscaled time, so its duration is already realtime.
        float actionDurationRealtime = isSkillDive ? actionDuration : (actionDuration / timeScaleSafe);
        float requiredActionHold = Mathf.Max(0f, actionDurationRealtime - moveInDuration) + postActionHoldDuration;
        float effectiveHold = Mathf.Max(holdDuration, requiredActionHold);
        yield return new WaitForSecondsRealtime(effectiveHold);
        yield return LerpCamera(camTr, targetPos, targetRot, originPos, originRot, moveOutDuration, moveOutCurve);

        RestoreState();
        routine = null;
    }

    private static IEnumerator LerpCamera(
        Transform camTr,
        Vector3 fromPos,
        Quaternion fromRot,
        Vector3 toPos,
        Quaternion toRot,
        float duration,
        AnimationCurve curve)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float eased = curve != null ? curve.Evaluate(normalized) : normalized;
            camTr.position = Vector3.LerpUnclamped(fromPos, toPos, eased);
            camTr.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, eased);
            yield return null;
        }
    }

    private static IEnumerator LerpCameraWithOrbit(
        Transform camTr,
        Vector3 fromPos,
        Quaternion fromRot,
        Vector3 toPos,
        Quaternion toRot,
        Vector3 pivot,
        float maxOrbitAngle,
        float duration,
        AnimationCurve moveCurve,
        AnimationCurve orbitCurve)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);

            float moveT = moveCurve != null ? moveCurve.Evaluate(normalized) : normalized;
            float orbitT = orbitCurve != null ? orbitCurve.Evaluate(normalized) : normalized;

            Vector3 basePos = Vector3.LerpUnclamped(fromPos, toPos, moveT);
            float orbitAngle = maxOrbitAngle * orbitT;
            Vector3 offset = basePos - pivot;
            Vector3 orbitPos = pivot + (Quaternion.AngleAxis(orbitAngle, Vector3.up) * offset);

            camTr.position = orbitPos;
            Vector3 lookDir = (pivot - orbitPos).normalized;
            Quaternion orbitRot = lookDir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDir, Vector3.up)
                : Quaternion.SlerpUnclamped(fromRot, toRot, moveT);
            camTr.rotation = orbitRot;
            yield return null;
        }
    }

    private static float ResolveOrbitSign(Vector3 kingPos, Vector3 victimPos, Vector3 cameraRight)
    {
        Vector3 toVictim = victimPos - kingPos;
        toVictim.y = 0f;
        if (toVictim.sqrMagnitude < 0.0001f)
        {
            return 1f;
        }

        float dot = Vector3.Dot(toVictim.normalized, cameraRight.normalized);
        return dot >= 0f ? 1f : -1f;
    }

    private float ResolveZoomFactor(Vector3 kingPos, Vector3 victimPos, bool isSkillDive)
    {
        if (!fitKingAndVictim)
        {
            return 1f;
        }

        Vector2 kingXZ = new(kingPos.x, kingPos.z);
        Vector2 victimXZ = new(victimPos.x, victimPos.z);
        float distance = Vector2.Distance(kingXZ, victimXZ);
        float extraDistance = Mathf.Max(0f, distance - minZoomOutDistance);
        float factor = 1f + extraDistance * zoomOutPerTile + (isSkillDive ? skillExtraZoomOut : 0f);
        return Mathf.Clamp(factor, 1f, maxZoomOutFactor);
    }

    private void OnDisable()
    {
        RestoreState();
    }

    private void RestoreState()
    {
        Time.timeScale = cachedTimeScale;
        Time.fixedDeltaTime = cachedFixedDeltaTime;

        if (cachedFollowWasEnabled && cachedFollow != null)
        {
            cachedFollow.enabled = true;
        }

        cachedFollow = null;
        cachedFollowWasEnabled = false;
        cachedTimeScale = 1f;
        cachedFixedDeltaTime = 0.02f;
    }
}
