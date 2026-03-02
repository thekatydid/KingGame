using UnityEngine;
using UnityEngine.EventSystems;

public class AllyHoverPlannedPathPreview : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ChessBoardManager board;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private float lineWidth = 0.12f;
    [SerializeField] private float lineY = 0.08f;
    [SerializeField] private Color moveColor = new(0.2f, 1f, 0.45f, 0.95f);
    [SerializeField] private Color attackColor = new(1f, 0.35f, 0.35f, 0.95f);

    private ChessPiece hoveredAlly;
    private LineRenderer line;

    public void Initialize(Camera cam, ChessBoardManager boardManager, TurnManager manager)
    {
        mainCamera = cam;
        board = boardManager;
        turnManager = manager;
    }

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoardManager>();
        }

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TurnManager>();
        }

        EnsureLine();
        Clear();
    }

    private void Update()
    {
        if (mainCamera == null || board == null || turnManager == null)
        {
            Clear();
            return;
        }

        if (turnManager.CurrentPhase != TurnPhase.PlayerTurn)
        {
            Clear();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Clear();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 300f))
        {
            Clear();
            return;
        }

        ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
        if (piece == null || piece.Team != Team.Ally || piece.PieceType == PieceType.King)
        {
            Clear();
            return;
        }

        if (!turnManager.TryGetPredictedAllyAction(piece, out BoardCoord from, out BoardCoord to, out bool isAttack))
        {
            Clear();
            return;
        }

        hoveredAlly = piece;
        ShowLine(from, to, isAttack);
    }

    private void EnsureLine()
    {
        if (line != null)
        {
            return;
        }

        GameObject go = new("AllyActionPreviewLine");
        go.transform.SetParent(transform, false);

        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        line.sharedMaterial = new Material(shader);

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.enabled = false;
    }

    private void ShowLine(BoardCoord from, BoardCoord to, bool isAttack)
    {
        EnsureLine();

        Vector3 up = board.transform.up * lineY;
        Vector3 p0 = board.CoordToWorld(from) + up;
        Vector3 p1 = board.CoordToWorld(to) + up;

        line.startColor = isAttack ? attackColor : moveColor;
        line.endColor = isAttack ? attackColor : moveColor;
        line.SetPosition(0, p0);
        line.SetPosition(1, p1);
        line.enabled = true;
    }

    private void Clear()
    {
        hoveredAlly = null;
        if (line != null)
        {
            line.enabled = false;
        }
    }

    private void OnDisable()
    {
        Clear();
    }
}
