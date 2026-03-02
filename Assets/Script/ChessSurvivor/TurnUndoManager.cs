using System.Collections.Generic;
using UnityEngine;

public class TurnUndoManager : MonoBehaviour
{
    private enum GameOverUndoMode
    {
        ResumePlayable,
        ReviewOnly
    }

    [System.Serializable]
    private struct PieceState
    {
        public Team team;
        public PieceType pieceType;
        public BoardCoord coord;
    }

    private sealed class Snapshot
    {
        public List<PieceState> pieces = new();
        public TurnManager.RuntimeState turnState;
        public StageManager.RuntimeState stageState;
    }

    [SerializeField] private ChessBoardManager board;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private StageManager stageManager;
    [SerializeField] private KingPlayerController kingInput;
    [SerializeField] private KeyCode undoKey = KeyCode.Z;
    [SerializeField] private bool allowUndoInAnyPhase = true;
    [Header("Game Over Undo")]
    [SerializeField] private bool allowUndoAfterGameOver = true;
    [SerializeField] private GameOverUndoMode gameOverUndoMode = GameOverUndoMode.ResumePlayable;

    private readonly List<Snapshot> turnHistory = new();
    private int currentHistoryIndex = -1;
    private Snapshot postDialogueCheckpoint;
    private TurnPhase lastObservedPhase;
    private bool hasCapturedInitialSnapshot;

    public void Initialize(ChessBoardManager boardManager, TurnManager turn, StageManager stage)
    {
        board = boardManager;
        turnManager = turn;
        stageManager = stage;
        EnsureReferences();
        TryCaptureInitialSnapshot();
    }

    private void Awake()
    {
        EnsureReferences();
        lastObservedPhase = turnManager != null ? turnManager.CurrentPhase : TurnPhase.PlayerTurn;
    }

    private void Start()
    {
        TryCaptureInitialSnapshot();
    }

    public void MarkPostDialogueCheckpoint()
    {
        EnsureReferences();
        if (turnManager == null || board == null)
        {
            return;
        }

        postDialogueCheckpoint = BuildSnapshot();
    }

    public bool RestorePostDialogueCheckpoint(bool resumePlayable = true)
    {
        EnsureReferences();
        if (postDialogueCheckpoint == null)
        {
            return false;
        }

        RestoreSnapshot(postDialogueCheckpoint);
        GameManager.Instance?.ClearGameOverState();
        turnManager?.ForcePhase(resumePlayable ? TurnPhase.PlayerTurn : TurnPhase.Busy);
        lastObservedPhase = turnManager != null ? turnManager.CurrentPhase : TurnPhase.PlayerTurn;
        turnHistory.Clear();
        turnHistory.Add(postDialogueCheckpoint);
        currentHistoryIndex = 0;
        return true;
    }

    private void Update()
    {
        EnsureReferences();
        if (turnManager == null || board == null)
        {
            return;
        }

        bool isGameOver = GameManager.Instance != null && GameManager.Instance.IsGameOver;
        bool isKingMissing = !HasAliveAllyKingOnBoard();
        bool isTerminalState = isGameOver || isKingMissing;
        bool canUndoInCurrentState = allowUndoInAnyPhase
            || turnManager.CurrentPhase == TurnPhase.PlayerTurn
            || (allowUndoAfterGameOver && isTerminalState);

        if (Input.GetKeyDown(undoKey) && canUndoInCurrentState)
        {
            UndoOneTurn(isTerminalState);
            return;
        }

        TurnPhase phase = turnManager.CurrentPhase;
        if (phase == TurnPhase.PlayerTurn && lastObservedPhase != TurnPhase.PlayerTurn)
        {
            CapturePlayerTurnSnapshot();
        }

        lastObservedPhase = phase;
    }

    private void EnsureReferences()
    {
        if (turnManager == null)
        {
            turnManager = GetComponent<TurnManager>();
            if (turnManager == null)
            {
                turnManager = GetComponentInParent<TurnManager>();
            }

            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<TurnManager>();
            }
        }

        if (board == null)
        {
            if (board == null)
            {
                board = GetComponentInParent<ChessBoardManager>();
            }

            if (board == null)
            {
                board = FindFirstObjectByType<ChessBoardManager>();
            }
        }

        if (stageManager == null)
        {
            stageManager = GetComponentInParent<StageManager>();
            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }
        }

        if (kingInput == null)
        {
            kingInput = FindFirstObjectByType<KingPlayerController>();
        }
    }

    private void TryCaptureInitialSnapshot()
    {
        if (hasCapturedInitialSnapshot || turnManager == null || board == null)
        {
            return;
        }

        CapturePlayerTurnSnapshot();
        hasCapturedInitialSnapshot = true;
    }

    private void CapturePlayerTurnSnapshot()
    {
        Snapshot next = BuildSnapshot();
        if (currentHistoryIndex < turnHistory.Count - 1)
        {
            turnHistory.RemoveRange(currentHistoryIndex + 1, turnHistory.Count - currentHistoryIndex - 1);
        }

        turnHistory.Add(next);
        currentHistoryIndex = turnHistory.Count - 1;
        if (postDialogueCheckpoint == null)
        {
            postDialogueCheckpoint = next;
        }
    }

    private Snapshot BuildSnapshot()
    {
        Snapshot s = new Snapshot();
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece == null)
            {
                continue;
            }

            s.pieces.Add(new PieceState
            {
                team = piece.Team,
                pieceType = piece.PieceType,
                coord = piece.Coord
            });
        }

        s.turnState = turnManager.CaptureRuntimeState();
        s.stageState = stageManager != null ? stageManager.CaptureRuntimeState() : default;
        return s;
    }

    private void UndoOneTurn(bool fromGameOver)
    {
        if (turnHistory.Count == 0 || currentHistoryIndex <= 0)
        {
            return;
        }

        currentHistoryIndex--;
        RestoreSnapshot(turnHistory[currentHistoryIndex]);

        if (fromGameOver)
        {
            if (gameOverUndoMode == GameOverUndoMode.ResumePlayable)
            {
                GameManager.Instance?.ClearGameOverState();
            }
            else
            {
                turnManager.ForcePhase(TurnPhase.Busy);
            }
        }

        lastObservedPhase = turnManager != null ? turnManager.CurrentPhase : TurnPhase.PlayerTurn;
    }

    private void RestoreSnapshot(Snapshot snapshot)
    {
        if (snapshot == null || board == null || turnManager == null)
        {
            return;
        }

        List<ChessPiece> existing = new();
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null)
            {
                existing.Add(piece);
            }
        }

        for (int i = 0; i < existing.Count; i++)
        {
            board.RemovePiece(existing[i]);
        }

        ChessPiece restoredKing = null;
        for (int i = 0; i < snapshot.pieces.Count; i++)
        {
            PieceState p = snapshot.pieces[i];
            ChessPiece spawned = board.SpawnPiece(p.pieceType, p.team, p.coord);
            if (spawned != null && p.team == Team.Ally && p.pieceType == PieceType.King)
            {
                restoredKing = spawned;
            }
        }

        turnManager.RestoreRuntimeState(snapshot.turnState, restoredKing);
        if (stageManager != null)
        {
            stageManager.RestoreRuntimeState(snapshot.stageState);
        }

        if (kingInput == null)
        {
            kingInput = FindFirstObjectByType<KingPlayerController>();
        }

        if (kingInput != null && restoredKing != null)
        {
            kingInput.RebindKing(restoredKing);
        }
    }

    private bool HasAliveAllyKingOnBoard()
    {
        if (board == null)
        {
            return false;
        }

        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null && piece.Team == Team.Ally && piece.PieceType == PieceType.King)
            {
                return true;
            }
        }

        return false;
    }
}
