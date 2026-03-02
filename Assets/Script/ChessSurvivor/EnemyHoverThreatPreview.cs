using UnityEngine;
using UnityEngine.EventSystems;

public class EnemyHoverThreatPreview : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ChessBoardManager board;

    private ChessPiece lastHoveredEnemy;

    public void Initialize(Camera cam, ChessBoardManager boardManager)
    {
        mainCamera = cam;
        board = boardManager;
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
    }

    private void Update()
    {
        if (mainCamera == null || board == null)
        {
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
        if (piece == null || piece.Team != Team.Enemy)
        {
            Clear();
            return;
        }

        if (piece == lastHoveredEnemy)
        {
            return;
        }

        lastHoveredEnemy = piece;
        board.ShowEnemyThreatPreview(ChessRules.GetThreatenedCoords(board, piece));
    }

    private void OnDisable()
    {
        Clear();
    }

    private void Clear()
    {
        lastHoveredEnemy = null;
        if (board != null)
        {
            board.ClearEnemyThreatPreview();
        }
    }
}
