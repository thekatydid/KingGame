using UnityEngine;

[DisallowMultipleComponent]
public class BillboardToCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 baseLocalOffset = new(0f, 0f, 1f);
    [SerializeField] private float distanceForMaxCompensation = 15f;
    [SerializeField] private float horizontalCompensationAtMax = 0.5f;
    [SerializeField] private float verticalCompensationAtMax = 0.2f;

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        if (followTarget == null)
        {
            followTarget = transform.parent;
        }

        ApplyOffsetCompensation();

        Vector3 toCamera = transform.position - targetCamera.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(toCamera.normalized, targetCamera.transform.up);
    }

    private void ApplyOffsetCompensation()
    {
        if (followTarget == null || transform.parent == null)
        {
            return;
        }

        Vector3 worldPivot = followTarget.position;
        Vector3 view = targetCamera.WorldToViewportPoint(worldPivot);
        float horizontal = Mathf.Clamp((0.5f - view.x) * 2f, -1f, 1f);

        float distance = Vector3.Distance(targetCamera.transform.position, worldPivot);
        float t = distanceForMaxCompensation <= 0.0001f ? 1f : Mathf.Clamp01(distance / distanceForMaxCompensation);

        Vector3 offset = baseLocalOffset;
        offset.x += horizontal * horizontalCompensationAtMax * t;
        offset.y += verticalCompensationAtMax * t;
        offset.z = baseLocalOffset.z;
        transform.localPosition = offset;
    }

    public void Configure(Transform follow, Vector3 localOffset)
    {
        followTarget = follow;
        baseLocalOffset = localOffset;
    }
}
