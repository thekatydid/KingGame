using UnityEngine;

public class TopDownCameraController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindKingTarget = true;
    [SerializeField] private float targetFindInterval = 0.25f;
    [SerializeField] private Vector3 followOffset = new(0f, 20f, -8f);
    [SerializeField] private float followSmooth = 10f;
    [SerializeField] private float panSpeed = 12f;
    [SerializeField] private bool lockY = true;
    [Header("Mouse Drag Pan")]
    [SerializeField] private bool enableMiddleMouseDragPan = true;
    [SerializeField] private float middleMouseDragSensitivity = 0.02f;
    [SerializeField] private bool blockDragWhileCinematic = true;
    [Header("Scroll Zoom")]
    [SerializeField] private bool enableScrollZoom = true;
    [SerializeField] private float scrollZoomSensitivity = 12f;
    [SerializeField] private float minZoomHeight = 8f;
    [SerializeField] private float maxZoomHeight = 30f;
    [SerializeField] private bool keepViewAngleOnZoom = true;
    [SerializeField] private bool blockZoomWhileCinematic = true;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private CinematicDirector cinematicDirector;

    private Vector3 manualOffset;
    private float nextFindTime;
    private float zoomZRatio;
    private bool zoomRatioInitialized;
    private bool draggingWithMiddleMouse;
    private Vector3 lastMousePosition;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
    }

    private void LateUpdate()
    {
        if (target == null && autoFindKingTarget && Time.time >= nextFindTime)
        {
            nextFindTime = Time.time + targetFindInterval;
            TryFindKingTarget();
        }

        if (target == null)
        {
            return;
        }

        HandleScrollZoom();

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 pan = (right * h + forward * v) * (panSpeed * Time.deltaTime);
        pan += GetMiddleMouseDragPan(right, forward);

        manualOffset += pan;
        if (lockY)
        {
            manualOffset.y = 0f;
        }

        Vector3 desired = target.position + followOffset + manualOffset;
        if (lockY)
        {
            desired.y = followOffset.y;
        }

        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
    }

    private Vector3 GetMiddleMouseDragPan(Vector3 right, Vector3 forward)
    {
        if (!enableMiddleMouseDragPan)
        {
            return Vector3.zero;
        }

        if (blockDragWhileCinematic && IsCinematicPlaying())
        {
            draggingWithMiddleMouse = false;
            return Vector3.zero;
        }

        if (Input.GetMouseButtonDown(2))
        {
            draggingWithMiddleMouse = true;
            lastMousePosition = Input.mousePosition;
            return Vector3.zero;
        }

        if (!Input.GetMouseButton(2))
        {
            draggingWithMiddleMouse = false;
            return Vector3.zero;
        }

        Vector3 currentMouse = Input.mousePosition;
        Vector3 delta = currentMouse - lastMousePosition;
        lastMousePosition = currentMouse;

        if (!draggingWithMiddleMouse || delta.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        // Drag right -> camera pans right, drag up -> camera pans forward.
        float scale = middleMouseDragSensitivity;
        Vector3 pan = (right * delta.x + forward * delta.y) * scale;
        return pan;
    }

    private void HandleScrollZoom()
    {
        if (!enableScrollZoom)
        {
            return;
        }

        if (blockZoomWhileCinematic && IsCinematicPlaying())
        {
            return;
        }

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) < 0.0001f)
        {
            return;
        }

        float nextY = Mathf.Clamp(followOffset.y + (-wheel * scrollZoomSensitivity), minZoomHeight, maxZoomHeight);
        followOffset.y = nextY;

        if (keepViewAngleOnZoom)
        {
            EnsureZoomRatio();
            followOffset.z = followOffset.y * zoomZRatio;
        }
    }

    private void TryFindKingTarget()
    {
        ChessPiece[] pieces = FindObjectsByType<ChessPiece>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null && pieces[i].Team == Team.Ally && pieces[i].PieceType == PieceType.King)
            {
                target = pieces[i].transform;
                return;
            }
        }
    }

    private bool IsCinematicPlaying()
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>();
        }

        if (cinematicDirector == null)
        {
            cinematicDirector = FindFirstObjectByType<CinematicDirector>();
        }

        return (dialogueManager != null && dialogueManager.IsPlaying)
               || (cinematicDirector != null && cinematicDirector.IsPlaying);
    }

    private void EnsureZoomRatio()
    {
        if (zoomRatioInitialized)
        {
            return;
        }

        float safeY = Mathf.Abs(followOffset.y) < 0.0001f ? 1f : followOffset.y;
        zoomZRatio = followOffset.z / safeY;
        zoomRatioInitialized = true;
    }
}
