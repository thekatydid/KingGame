using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class KingPlayerController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ChessBoardManager board;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private KingQueenSkillController kingQueenSkill;
    [Header("Pick Up Visual")]
    [SerializeField] private float pickedHeightOffset = 0.55f;
    [SerializeField] private float pickedLerpSpeed = 14f;
    [SerializeField] private Vector3 pickedEuler = new(-14f, 0f, 12f);
    [SerializeField] private string kingHandSfxKey = "Hand";
    [SerializeField] private string kingDropSfxKey = "Drop";
    [Header("Support Rules")]
    [SerializeField] private bool allowForcedSupportHold = false;
    [Header("King Hover Icon")]
    [SerializeField] private bool showKingIconOnHover = true;
    [SerializeField] private string kingIconRootName = "KingIcon";

    private ChessPiece king;
    private bool picked;
    private Quaternion groundedRotation = Quaternion.identity;
    private bool groundedRotationInitialized;
    private Transform kingIconRoot;

    public void Initialize(Camera cam, ChessBoardManager boardManager, TurnManager manager, ChessPiece kingPiece)
    {
        mainCamera = cam;
        board = boardManager;
        turnManager = manager;
        king = kingPiece;
        kingIconRoot = null;
        SetKingIconVisible(false);
        CaptureGroundedRotation(force: true);
    }

    public void RebindKing(ChessPiece kingPiece)
    {
        king = kingPiece;
        kingIconRoot = null;
        SetKingIconVisible(false);
        CaptureGroundedRotation(force: true);
    }

    private void Update()
    {
        if (turnManager == null || board == null || king == null)
        {
            return;
        }

        if (kingQueenSkill == null)
        {
            kingQueenSkill = FindFirstObjectByType<KingQueenSkillController>();
        }

        if (turnManager.CurrentPhase != TurnPhase.PlayerTurn)
        {
            ForcePutDownAndClear();
            SetKingIconVisible(false);
            return;
        }

        if (!picked)
        {
            CaptureGroundedRotation(force: false);
        }

        if (IsSpaceHeld())
        {
            if (picked)
            {
                PutDown(cancelOnly: true);
            }

            turnManager.EndPlayerTurn();
            return;
        }

        if (IsRightMousePressedThisFrame() && picked)
        {
            PutDown(cancelOnly: true, playDropSfx: true);
            return;
        }

        if (IsLeftMousePressedThisFrame() && !picked)
        {
            if (IsClickOnKing())
            {
                CaptureGroundedRotation(force: true);
                SoundManager.Instance?.PlaySfx(kingHandSfxKey);
                picked = true;
                return;
            }
        }

        if (picked && IsLeftMousePressedThisFrame())
        {
            TryPlaceOrCaptureFromPointer();
        }

        if (picked)
        {
            List<BoardCoord> enemyDanger = BuildBlockedDangerForKing();
            board.ShowMoveHighlights(GetKingMoveCandidates(), enemyDanger);
            board.ShowCaptureBorders(GetCapturableTargets());
            UpdateSupportPreviewUnderMouse();
            SetKingIconVisible(false);
        }
        else
        {
            UpdateKingHoverIcon();
            if (!turnManager.HasForcedSupportOrders)
            {
                turnManager.ClearForcedSupportPreview();
            }
        }

        UpdateKingPickedVisual();
    }

    private void UpdateKingHoverIcon()
    {
        if (!showKingIconOnHover || king == null || mainCamera == null)
        {
            SetKingIconVisible(false);
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetKingIconVisible(false);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(GetPointerPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit, 150f))
        {
            SetKingIconVisible(false);
            return;
        }

        ChessPiece hovered = hit.collider.GetComponentInParent<ChessPiece>();
        SetKingIconVisible(hovered != null && hovered == king);
    }

    private void EnsureKingIconRoot()
    {
        if (kingIconRoot != null || king == null || string.IsNullOrWhiteSpace(kingIconRootName))
        {
            return;
        }

        kingIconRoot = king.transform.Find(kingIconRootName);
    }

    private void SetKingIconVisible(bool visible)
    {
        EnsureKingIconRoot();
        if (kingIconRoot != null)
        {
            kingIconRoot.gameObject.SetActive(visible);
        }
    }

    private bool IsClickOnKing()
    {
        Ray ray = mainCamera.ScreenPointToRay(GetPointerPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return false;
        }

        ChessPiece clicked = hit.collider.GetComponentInParent<ChessPiece>();
        return clicked != null && clicked == king;
    }

    private void TryPlaceOrCaptureFromPointer()
    {
        Ray ray = mainCamera.ScreenPointToRay(GetPointerPosition());
        if (!board.TryGetCoordFromRay(ray, out BoardCoord target))
        {
            return;
        }

        if (target.Equals(king.Coord))
        {
            PutDown(cancelOnly: true, playDropSfx: true);
            return;
        }

        List<BoardCoord> legalMoves = GetKingMoveCandidates();
        List<BoardCoord> legalAttacks = GetKingAttackCandidates();

        bool isMove = legalMoves.Contains(target);
        bool isCapture = false;
        ChessPiece victim = board.GetPieceAt(target);
        if (victim != null && victim.Team == Team.Enemy && legalAttacks.Contains(target))
        {
            isCapture = true;
        }

        if (!isMove && !isCapture)
        {
            return;
        }

        KingMoveEvaluation evaluation = EvaluateKingMoveSafety(target, isCapture ? victim : null);
        if (!evaluation.canEnter)
        {
            return;
        }

        turnManager.ClearForcedSupportOrders();
        bool hasSuperSaveSupport = false;
        List<ChessPiece> supportAllies = null;
        List<ChessPiece> supportEnemies = null;
        List<BoardCoord> supportTargets = null;
        if (evaluation.requiresSupport)
        {
            supportAllies = new List<ChessPiece>();
            supportEnemies = new List<ChessPiece>();
            supportTargets = new List<BoardCoord>();
            for (int i = 0; i < evaluation.supportPlan.Count; i++)
            {
                SupportOrder order = evaluation.supportPlan[i];
                supportAllies.Add(order.ally);
                supportEnemies.Add(order.enemy);
                supportTargets.Add(order.destination);
                hasSuperSaveSupport |= order.isSuperSave;

                if (order.actionType == TurnManager.ForcedSupportActionType.Kill && order.enemy != null)
                {
                    turnManager.RegisterForcedSupportKill(order.ally, order.enemy, order.isSuperSave);
                }
                else if (order.actionType == TurnManager.ForcedSupportActionType.Hold)
                {
                    turnManager.RegisterForcedSupportHold(order.ally, order.isSuperSave);
                }
                else
                {
                    turnManager.RegisterForcedSupportMove(order.ally, order.destination, order.isSuperSave);
                }
            }

            // SuperSave callout is intentionally deferred until after king move confirm.
            // Before movement, only non-SuperSave support shows combo preview.
            if (!hasSuperSaveSupport)
            {
                turnManager.PreviewForcedSupportAllies(
                    supportAllies,
                    useSuperSaveLabel: false,
                    targetEnemies: supportEnemies,
                    targetCoords: supportTargets);
            }
            else
            {
                turnManager.ClearForcedSupportPreview();
            }
        }

        if (isCapture)
        {
            if (!turnManager.OnPlayerCaptureEnemy(king, victim, target))
            {
                return;
            }
        }
        else if (!board.MovePiece(king, target))
        {
            return;
        }
        else
        {
            SoundManager.Instance?.PlaySfx(kingDropSfxKey);
        }

        PutDown(cancelOnly: false);
        if (evaluation.requiresSupport)
        {
            turnManager.RegisterCoopPlan(hasSuperSaveSupport);
        }

        if (hasSuperSaveSupport && supportAllies != null && supportAllies.Count > 0)
        {
            turnManager.PreviewForcedSupportAllies(
                supportAllies,
                useSuperSaveLabel: true,
                targetEnemies: supportEnemies,
                targetCoords: supportTargets);
        }
        turnManager.EndPlayerTurn();
    }

    private List<BoardCoord> GetCapturableTargets()
    {
        List<BoardCoord> attacks = GetKingAttackCandidates();
        List<BoardCoord> result = new();

        for (int i = 0; i < attacks.Count; i++)
        {
            ChessPiece victim = board.GetPieceAt(attacks[i]);
            if (victim == null || victim.Team != Team.Enemy)
            {
                continue;
            }

            KingMoveEvaluation evaluation = EvaluateKingMoveSafety(attacks[i], victim);
            if (!evaluation.canEnter)
            {
                continue;
            }

            result.Add(attacks[i]);
        }

        return result;
    }

    public bool HasAnyLegalKingAction()
    {
        if (board == null || king == null)
        {
            return false;
        }

        List<BoardCoord> legalMoves = GetKingMoveCandidates();
        for (int i = 0; i < legalMoves.Count; i++)
        {
            KingMoveEvaluation evaluation = EvaluateKingMoveSafety(legalMoves[i], null);
            if (evaluation.canEnter)
            {
                return true;
            }
        }

        List<BoardCoord> legalAttacks = GetKingAttackCandidates();
        for (int i = 0; i < legalAttacks.Count; i++)
        {
            ChessPiece victim = board.GetPieceAt(legalAttacks[i]);
            if (victim == null || victim.Team != Team.Enemy)
            {
                continue;
            }

            KingMoveEvaluation evaluation = EvaluateKingMoveSafety(legalAttacks[i], victim);
            if (evaluation.canEnter)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct SupportOrder
    {
        public TurnManager.ForcedSupportActionType actionType { get; }
        public ChessPiece ally { get; }
        public ChessPiece enemy { get; }
        public BoardCoord destination { get; }
        public bool isSuperSave { get; }

        public SupportOrder(
            TurnManager.ForcedSupportActionType action,
            ChessPiece allyPiece,
            ChessPiece enemyPiece,
            BoardCoord targetCoord,
            bool superSave)
        {
            actionType = action;
            ally = allyPiece;
            enemy = enemyPiece;
            destination = targetCoord;
            isSuperSave = superSave;
        }
    }

    private readonly struct KingMoveEvaluation
    {
        public bool canEnter { get; }
        public bool requiresSupport { get; }
        public bool isSuperSave { get; }
        public List<SupportOrder> supportPlan { get; }

        public KingMoveEvaluation(bool canEnterTile, bool needsSupport, bool superSave, List<SupportOrder> plan)
        {
            canEnter = canEnterTile;
            requiresSupport = needsSupport;
            isSuperSave = superSave;
            supportPlan = plan;
        }
    }

    private List<BoardCoord> BuildBlockedDangerForKing()
    {
        List<BoardCoord> blocked = new();
        List<BoardCoord> legalMoves = GetKingMoveCandidates();

        for (int i = 0; i < legalMoves.Count; i++)
        {
            KingMoveEvaluation evaluation = EvaluateKingMoveSafety(legalMoves[i]);
            if (!evaluation.canEnter)
            {
                blocked.Add(legalMoves[i]);
            }
        }

        return blocked;
    }

    private KingMoveEvaluation EvaluateKingMoveSafety(BoardCoord kingTarget, ChessPiece capturedEnemy = null)
    {
        List<ChessPiece> immediateThreats = GetEnemiesThreateningKingInState(kingTarget, capturedEnemy, null, null, null);
        if (immediateThreats.Count == 0)
        {
            return new KingMoveEvaluation(canEnterTile: true, needsSupport: false, superSave: false, plan: new List<SupportOrder>());
        }

        if (!TryBuildSupportPlan(kingTarget, capturedEnemy, out List<SupportOrder> supportPlan))
        {
            return new KingMoveEvaluation(canEnterTile: false, needsSupport: false, superSave: false, plan: new List<SupportOrder>());
        }

        bool isSuperSave = capturedEnemy == null;
        return new KingMoveEvaluation(canEnterTile: true, needsSupport: true, superSave: isSuperSave, supportPlan);
    }

    private bool TryBuildSupportPlan(BoardCoord kingTarget, ChessPiece capturedEnemy, out List<SupportOrder> plan)
    {
        plan = new List<SupportOrder>();
        HashSet<ChessPiece> reservedAllies = new();
        HashSet<ChessPiece> removedEnemies = new();
        Dictionary<ChessPiece, BoardCoord> movedAllies = new();
        return TryBuildSupportPlanRecursive(
            kingTarget,
            capturedEnemy,
            reservedAllies,
            removedEnemies,
            movedAllies,
            plan,
            depth: 0);
    }

    private List<ChessPiece> GetEnemiesThreateningKingInState(
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies,
        HashSet<ChessPiece> reservedAllies)
    {
        List<ChessPiece> result = new();
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece == null || piece.Team != Team.Enemy || !IsPiecePresentInState(piece, capturedEnemy, removedEnemies))
            {
                continue;
            }

            if (CanPieceCaptureCoordInState(piece, kingTarget, kingTarget, capturedEnemy, removedEnemies, movedAllies))
            {
                result.Add(piece);
            }
        }

        AddPotentialDiscoveredLineThreats(kingTarget, capturedEnemy, removedEnemies, movedAllies, reservedAllies, result);

        return result;
    }

    private void AddPotentialDiscoveredLineThreats(
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies,
        HashSet<ChessPiece> reservedAllies,
        List<ChessPiece> threats)
    {
        foreach (ChessPiece attacker in board.AllPieces)
        {
            if (attacker == null || attacker.Team != Team.Enemy || !IsPiecePresentInState(attacker, capturedEnemy, removedEnemies))
            {
                continue;
            }

            if (attacker.PieceType != PieceType.Rook && attacker.PieceType != PieceType.Bishop && attacker.PieceType != PieceType.Queen)
            {
                continue;
            }

            if (threats.Contains(attacker))
            {
                continue;
            }

            BoardCoord from = GetPieceCoordInState(attacker, kingTarget, movedAllies);
            if (!IsLineAttackGeometry(attacker.PieceType, from, kingTarget))
            {
                continue;
            }

            List<ChessPiece> blockers = GetLineBlockersInState(from, kingTarget, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            if (blockers.Count != 1)
            {
                continue;
            }

            ChessPiece blocker = blockers[0];
            if (blocker == null || !IsPiecePresentInState(blocker, capturedEnemy, removedEnemies))
            {
                continue;
            }

            if (CanBlockerVacateLine(blocker, attacker, from, kingTarget, capturedEnemy, removedEnemies, movedAllies, reservedAllies))
            {
                threats.Add(attacker);
            }
        }
    }

    private bool CanBlockerVacateLine(
        ChessPiece blocker,
        ChessPiece attacker,
        BoardCoord attackerFrom,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies,
        HashSet<ChessPiece> reservedAllies)
    {
        if (reservedAllies != null && reservedAllies.Contains(blocker))
        {
            // Explicitly reserved blockers are forced to hold this turn.
            return false;
        }

        BoardCoord blockerFrom = GetPieceCoordInState(blocker, kingTarget, movedAllies);
        HashSet<BoardCoord> blockingLine = new(GetIntermediateLineCoords(attackerFrom, kingTarget));

        List<BoardCoord> moveCandidates = GetMoveCandidatesInState(blocker, blockerFrom, kingTarget, capturedEnemy, removedEnemies, movedAllies);
        for (int i = 0; i < moveCandidates.Count; i++)
        {
            BoardCoord dst = moveCandidates[i];
            if (dst.Equals(blockerFrom) || blockingLine.Contains(dst))
            {
                continue;
            }

            if (CanPieceTypeMoveCoordInState(blocker.PieceType, blockerFrom, dst, kingTarget, capturedEnemy, removedEnemies, movedAllies))
            {
                return true;
            }
        }

        List<BoardCoord> attackCandidates = GetAttackCandidatesInState(blocker, blockerFrom, kingTarget, capturedEnemy, removedEnemies, movedAllies);
        for (int i = 0; i < attackCandidates.Count; i++)
        {
            BoardCoord dst = attackCandidates[i];
            if (dst.Equals(blockerFrom) || blockingLine.Contains(dst))
            {
                continue;
            }

            ChessPiece victim = GetPieceAtCoordInState(dst, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            if (victim == null || victim.Team == blocker.Team)
            {
                continue;
            }

            // Ally blocker capturing the line attacker is a safe resolution, not a discovered threat.
            if (blocker.Team == Team.Ally && attacker != null && victim == attacker)
            {
                continue;
            }

            if (CanPieceTypeCaptureCoord(blocker.PieceType, blockerFrom, dst, kingTarget, capturedEnemy, removedEnemies, movedAllies))
            {
                return true;
            }
        }

        return false;
    }

    private List<BoardCoord> GetMoveCandidatesInState(
        ChessPiece piece,
        BoardCoord from,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (from.Equals(piece.Coord))
        {
            return ChessRules.GetMoveCandidates(board, piece);
        }

        List<BoardCoord> result = new();
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                BoardCoord dst = new(x, y);
                if (dst.Equals(from) || dst.Equals(kingTarget))
                {
                    continue;
                }

                if (IsOccupiedInState(dst, kingTarget, capturedEnemy, removedEnemies, movedAllies))
                {
                    continue;
                }

                if (CanPieceTypeMoveCoordInState(piece.PieceType, from, dst, kingTarget, capturedEnemy, removedEnemies, movedAllies))
                {
                    result.Add(dst);
                }
            }
        }

        return result;
    }

    private List<BoardCoord> GetAttackCandidatesInState(
        ChessPiece piece,
        BoardCoord from,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (from.Equals(piece.Coord))
        {
            return ChessRules.GetAttackCandidates(board, piece);
        }

        List<BoardCoord> result = new();
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                BoardCoord dst = new(x, y);
                if (dst.Equals(from))
                {
                    continue;
                }

                ChessPiece victim = GetPieceAtCoordInState(dst, kingTarget, capturedEnemy, removedEnemies, movedAllies);
                if (victim == null || victim.Team == piece.Team)
                {
                    continue;
                }

                if (CanPieceTypeCaptureCoord(piece.PieceType, from, dst, kingTarget, capturedEnemy, removedEnemies, movedAllies))
                {
                    result.Add(dst);
                }
            }
        }

        return result;
    }

    private ChessPiece GetPieceAtCoordInState(
        BoardCoord coord,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (coord.Equals(kingTarget))
        {
            return king;
        }

        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece == null || !IsPiecePresentInState(piece, capturedEnemy, removedEnemies))
            {
                continue;
            }

            if (GetPieceCoordInState(piece, kingTarget, movedAllies).Equals(coord))
            {
                return piece;
            }
        }

        return null;
    }

    private List<ChessPiece> GetLineBlockersInState(
        BoardCoord from,
        BoardCoord to,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        List<ChessPiece> blockers = new();
        List<BoardCoord> between = GetIntermediateLineCoords(from, to);
        for (int i = 0; i < between.Count; i++)
        {
            ChessPiece piece = GetPieceAtCoordInState(between[i], kingTarget, capturedEnemy, removedEnemies, movedAllies);
            if (piece != null)
            {
                blockers.Add(piece);
            }
        }

        return blockers;
    }

    private static bool IsLineAttackGeometry(PieceType pieceType, BoardCoord from, BoardCoord to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);
        bool orthogonal = dx == 0 || dy == 0;
        bool diagonal = absDx == absDy;

        return pieceType switch
        {
            PieceType.Rook => orthogonal,
            PieceType.Bishop => diagonal,
            PieceType.Queen => orthogonal || diagonal,
            _ => false
        };
    }

    private static List<BoardCoord> GetIntermediateLineCoords(BoardCoord from, BoardCoord to)
    {
        List<BoardCoord> result = new();
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);
        int steps = Mathf.Max(absDx, absDy);
        if (steps <= 1)
        {
            return result;
        }

        int stepX = dx == 0 ? 0 : dx / absDx;
        int stepY = dy == 0 ? 0 : dy / absDy;
        for (int i = 1; i < steps; i++)
        {
            result.Add(new BoardCoord(from.x + stepX * i, from.y + stepY * i));
        }

        return result;
    }

    private bool TryBuildSupportPlanRecursive(
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> reservedAllies,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies,
        List<SupportOrder> plan,
        int depth)
    {
        if (depth > 64)
        {
            return false;
        }

        List<ChessPiece> threateningEnemies = GetEnemiesThreateningKingInState(kingTarget, capturedEnemy, removedEnemies, movedAllies, reservedAllies);
        if (threateningEnemies.Count == 0)
        {
            return true;
        }

        ChessPiece selectedThreat = null;
        List<SupportOrder> selectedActions = null;
        bool hasAnyCandidateThreat = false;

        for (int i = 0; i < threateningEnemies.Count; i++)
        {
            ChessPiece enemy = threateningEnemies[i];
            List<SupportOrder> candidates = GetSupportActionsForThreat(enemy, reservedAllies, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            if (candidates.Count == 0)
            {
                // A threat can become neutralized after resolving another threat first
                // (e.g. line gets blocked by moved ally). Do not fail early here.
                continue;
            }

            hasAnyCandidateThreat = true;
            if (selectedThreat == null || candidates.Count < selectedActions.Count)
            {
                selectedThreat = enemy;
                selectedActions = candidates;
            }
        }

        if (!hasAnyCandidateThreat || selectedThreat == null || selectedActions == null)
        {
            return false;
        }

        selectedActions.Sort((a, b) => DistanceSq(a.ally.Coord, selectedThreat.Coord).CompareTo(DistanceSq(b.ally.Coord, selectedThreat.Coord)));
        for (int i = 0; i < selectedActions.Count; i++)
        {
            SupportOrder action = selectedActions[i];
            ChessPiece ally = action.ally;
            reservedAllies.Add(ally);
            bool hadPreviousMove = movedAllies.TryGetValue(ally, out BoardCoord previousAllyCoord);
            bool removedSelectedThreat = false;
            if (action.actionType == TurnManager.ForcedSupportActionType.Kill && selectedThreat != null)
            {
                removedEnemies.Add(selectedThreat);
                movedAllies[ally] = GetPieceCoordInState(selectedThreat, kingTarget, movedAllies);
                removedSelectedThreat = true;
            }
            else if (action.actionType == TurnManager.ForcedSupportActionType.Move)
            {
                movedAllies[ally] = action.destination;
            }

            plan.Add(action);

            if (TryBuildSupportPlanRecursive(kingTarget, capturedEnemy, reservedAllies, removedEnemies, movedAllies, plan, depth + 1))
            {
                return true;
            }

            plan.RemoveAt(plan.Count - 1);
            if (hadPreviousMove)
            {
                movedAllies[ally] = previousAllyCoord;
            }
            else
            {
                movedAllies.Remove(ally);
            }
            if (removedSelectedThreat)
            {
                removedEnemies.Remove(selectedThreat);
            }
            reservedAllies.Remove(ally);
        }

        return false;
    }

    private List<SupportOrder> GetSupportActionsForThreat(
        ChessPiece enemy,
        HashSet<ChessPiece> reservedAllies,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        List<SupportOrder> result = new();

        List<ChessPiece> killAllies = GetSupportingAlliesForEnemy(enemy, reservedAllies, kingTarget, capturedEnemy, removedEnemies, movedAllies);
        for (int i = 0; i < killAllies.Count; i++)
        {
            result.Add(new SupportOrder(
                TurnManager.ForcedSupportActionType.Kill,
                killAllies[i],
                enemy,
                enemy.Coord,
                superSave: false));
        }

        List<BoardCoord> blockCoords = GetThreatBlockCoords(enemy, kingTarget);
        for (int i = 0; i < blockCoords.Count; i++)
        {
            BoardCoord blockCoord = blockCoords[i];
            foreach (ChessPiece ally in board.AllPieces)
            {
                if (ally == null || ally.Team != Team.Ally || ally == king || ally.PieceType == PieceType.King)
                {
                    continue;
                }

                if (reservedAllies.Contains(ally) || !IsPiecePresentInState(ally, capturedEnemy, removedEnemies))
                {
                    continue;
                }

                if (!CanAllyMoveToCoordInState(ally, blockCoord, kingTarget, capturedEnemy, removedEnemies, movedAllies))
                {
                    continue;
                }

                bool isSuperSave = IsAllyExposedAtCoord(ally, blockCoord, kingTarget, capturedEnemy, removedEnemies, movedAllies);
                result.Add(new SupportOrder(
                    TurnManager.ForcedSupportActionType.Move,
                    ally,
                    null,
                    blockCoord,
                    isSuperSave));
            }
        }

        // HOLD is optional and disabled by default.
        if (allowForcedSupportHold)
        {
            ChessPiece holdBlocker = TryGetSingleAllyLineBlocker(enemy, kingTarget, capturedEnemy, removedEnemies, movedAllies, reservedAllies);
            if (holdBlocker != null)
            {
                result.Add(new SupportOrder(
                    TurnManager.ForcedSupportActionType.Hold,
                    holdBlocker,
                    null,
                    GetPieceCoordInState(holdBlocker, kingTarget, movedAllies),
                    superSave: false));
            }
        }

        return result;
    }

    private ChessPiece TryGetSingleAllyLineBlocker(
        ChessPiece enemy,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies,
        HashSet<ChessPiece> reservedAllies)
    {
        if (enemy == null || !IsLineAttackGeometry(enemy.PieceType, enemy.Coord, kingTarget))
        {
            return null;
        }

        List<ChessPiece> blockers = GetLineBlockersInState(enemy.Coord, kingTarget, kingTarget, capturedEnemy, removedEnemies, movedAllies);
        if (blockers.Count != 1)
        {
            return null;
        }

        ChessPiece blocker = blockers[0];
        if (blocker == null || blocker.Team != Team.Ally || blocker == king || blocker.PieceType == PieceType.King)
        {
            return null;
        }

        if (reservedAllies != null && reservedAllies.Contains(blocker))
        {
            return null;
        }

        if (!CanBlockerVacateLine(blocker, enemy, enemy.Coord, kingTarget, capturedEnemy, removedEnemies, movedAllies, reservedAllies))
        {
            return null;
        }

        return blocker;
    }

    private List<ChessPiece> GetSupportingAlliesForEnemy(
        ChessPiece enemy,
        HashSet<ChessPiece> reservedAllies,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        List<ChessPiece> result = new();

        foreach (ChessPiece ally in board.AllPieces)
        {
            if (ally == null || ally.Team != Team.Ally || ally == king || ally.PieceType == PieceType.King)
            {
                continue;
            }

            if (reservedAllies.Contains(ally) || !IsPiecePresentInState(ally, capturedEnemy, removedEnemies))
            {
                continue;
            }

            if (!CanPieceCapturePieceInState(ally, enemy, kingTarget, capturedEnemy, removedEnemies, movedAllies))
            {
                continue;
            }

            result.Add(ally);
        }

        return result;
    }

    private bool CanPieceCapturePieceInState(
        ChessPiece attacker,
        ChessPiece victim,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (!IsPiecePresentInState(attacker, capturedEnemy, removedEnemies) || !IsPiecePresentInState(victim, capturedEnemy, removedEnemies))
        {
            return false;
        }

        BoardCoord from = GetPieceCoordInState(attacker, kingTarget, movedAllies);
        BoardCoord to = GetPieceCoordInState(victim, kingTarget, movedAllies);
        return CanPieceTypeCaptureCoord(attacker.PieceType, from, to, kingTarget, capturedEnemy, removedEnemies, movedAllies);
    }

    private bool CanPieceCaptureCoordInState(
        ChessPiece attacker,
        BoardCoord target,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (!IsPiecePresentInState(attacker, capturedEnemy, removedEnemies))
        {
            return false;
        }

        BoardCoord from = GetPieceCoordInState(attacker, kingTarget, movedAllies);
        return CanPieceTypeCaptureCoord(attacker.PieceType, from, target, kingTarget, capturedEnemy, removedEnemies, movedAllies);
    }

    private bool CanPieceTypeCaptureCoord(
        PieceType pieceType,
        BoardCoord from,
        BoardCoord to,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        switch (pieceType)
        {
            case PieceType.King:
                return (absDx <= 1 && absDy <= 1) && (absDx != 0 || absDy != 0);
            case PieceType.Pawn:
                return absDx == 1 && absDy == 1;
            case PieceType.Knight:
                return (absDx == 1 && absDy == 2) || (absDx == 2 && absDy == 1);
            case PieceType.Rook:
                return CanLinePieceCapture(from, to, allowOrthogonal: true, allowDiagonal: false, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            case PieceType.Bishop:
                return CanLinePieceCapture(from, to, allowOrthogonal: false, allowDiagonal: true, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            case PieceType.Queen:
                return CanLinePieceCapture(from, to, allowOrthogonal: true, allowDiagonal: true, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            default:
                return false;
        }
    }

    private bool CanLinePieceCapture(
        BoardCoord from,
        BoardCoord to,
        bool allowOrthogonal,
        bool allowDiagonal,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        bool isOrthogonal = dx == 0 || dy == 0;
        bool isDiagonal = absDx == absDy;
        if (!((allowOrthogonal && isOrthogonal) || (allowDiagonal && isDiagonal)))
        {
            return false;
        }

        int stepX = dx == 0 ? 0 : dx / absDx;
        int stepY = dy == 0 ? 0 : dy / absDy;
        int steps = Mathf.Max(absDx, absDy);
        if (steps <= 0)
        {
            return false;
        }

        for (int i = 1; i < steps; i++)
        {
            BoardCoord cursor = new(from.x + stepX * i, from.y + stepY * i);
            if (IsOccupiedInState(cursor, kingTarget, capturedEnemy, removedEnemies, movedAllies))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanAllyMoveToCoordInState(
        ChessPiece ally,
        BoardCoord target,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (!board.IsInside(target) || target.Equals(kingTarget))
        {
            return false;
        }

        if (IsOccupiedInState(target, kingTarget, capturedEnemy, removedEnemies, movedAllies))
        {
            return false;
        }

        BoardCoord from = GetPieceCoordInState(ally, kingTarget, movedAllies);
        if (from.Equals(ally.Coord))
        {
            List<BoardCoord> moves = ChessRules.GetMoveCandidates(board, ally);
            return moves.Contains(target);
        }

        return CanPieceTypeMoveCoordInState(ally.PieceType, from, target, kingTarget, capturedEnemy, removedEnemies, movedAllies);
    }

    private bool CanPieceTypeMoveCoordInState(
        PieceType pieceType,
        BoardCoord from,
        BoardCoord to,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        switch (pieceType)
        {
            case PieceType.Pawn:
                return (absDx + absDy) == 1;
            case PieceType.Knight:
                return (absDx == 1 && absDy == 2) || (absDx == 2 && absDy == 1);
            case PieceType.Rook:
                return CanLinePieceCapture(from, to, true, false, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            case PieceType.Bishop:
                return CanLinePieceCapture(from, to, false, true, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            case PieceType.Queen:
                return CanLinePieceCapture(from, to, true, true, kingTarget, capturedEnemy, removedEnemies, movedAllies);
            case PieceType.King:
                return (absDx <= 1 && absDy <= 1) && (absDx != 0 || absDy != 0);
            default:
                return false;
        }
    }

    private bool IsAllyExposedAtCoord(
        ChessPiece ally,
        BoardCoord destination,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        Dictionary<ChessPiece, BoardCoord> simulated = new(movedAllies);
        simulated[ally] = destination;

        foreach (ChessPiece enemy in board.AllPieces)
        {
            if (enemy == null || enemy.Team != Team.Enemy || !IsPiecePresentInState(enemy, capturedEnemy, removedEnemies))
            {
                continue;
            }

            if (CanPieceCaptureCoordInState(enemy, destination, kingTarget, capturedEnemy, removedEnemies, simulated))
            {
                return true;
            }
        }

        return false;
    }

    private List<BoardCoord> GetThreatBlockCoords(ChessPiece enemy, BoardCoord kingTarget)
    {
        List<BoardCoord> result = new();
        if (enemy == null)
        {
            return result;
        }

        BoardCoord from = enemy.Coord;
        int dx = kingTarget.x - from.x;
        int dy = kingTarget.y - from.y;
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);

        bool orthogonal = dx == 0 || dy == 0;
        bool diagonal = absDx == absDy;

        bool validLine = enemy.PieceType switch
        {
            PieceType.Rook => orthogonal,
            PieceType.Bishop => diagonal,
            PieceType.Queen => orthogonal || diagonal,
            _ => false
        };

        if (!validLine)
        {
            return result;
        }

        int stepX = dx == 0 ? 0 : dx / absDx;
        int stepY = dy == 0 ? 0 : dy / absDy;
        int steps = Mathf.Max(absDx, absDy);
        for (int i = 1; i < steps; i++)
        {
            BoardCoord c = new(from.x + stepX * i, from.y + stepY * i);
            if (!c.Equals(kingTarget))
            {
                result.Add(c);
            }
        }

        return result;
    }

    private bool IsOccupiedInState(
        BoardCoord coord,
        BoardCoord kingTarget,
        ChessPiece capturedEnemy,
        HashSet<ChessPiece> removedEnemies,
        Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (coord.Equals(kingTarget))
        {
            return true;
        }

        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece == null || piece == king || !IsPiecePresentInState(piece, capturedEnemy, removedEnemies))
            {
                continue;
            }

            BoardCoord pieceCoord = GetPieceCoordInState(piece, kingTarget, movedAllies);
            if (pieceCoord.Equals(coord))
            {
                return true;
            }
        }

        return false;
    }

    private BoardCoord GetPieceCoordInState(ChessPiece piece, BoardCoord kingTarget, Dictionary<ChessPiece, BoardCoord> movedAllies)
    {
        if (piece == king)
        {
            return kingTarget;
        }

        if (movedAllies != null && movedAllies.TryGetValue(piece, out BoardCoord movedCoord))
        {
            return movedCoord;
        }

        return piece.Coord;
    }

    private bool IsPiecePresentInState(ChessPiece piece, ChessPiece capturedEnemy, HashSet<ChessPiece> removedEnemies)
    {
        if (piece == null)
        {
            return false;
        }

        if (piece == capturedEnemy)
        {
            return false;
        }

        if (removedEnemies != null && removedEnemies.Contains(piece))
        {
            return false;
        }

        return true;
    }

    private static float DistanceSq(BoardCoord a, BoardCoord b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    private void UpdateKingPickedVisual()
    {
        if (!picked && board != null && board.IsPieceMoving(king))
        {
            // Keep authored motion while board movement coroutine is active.
            return;
        }

        Vector3 basePos = board.CoordToPieceWorld(king.Coord);
        Vector3 targetPos = picked ? basePos + Vector3.up * pickedHeightOffset : basePos;
        if (!groundedRotationInitialized)
        {
            groundedRotation = king.transform.rotation;
            groundedRotationInitialized = true;
        }

        Quaternion targetRot = picked
            ? groundedRotation * Quaternion.Euler(pickedEuler)
            : groundedRotation;

        king.transform.position = Vector3.Lerp(king.transform.position, targetPos, 1f - Mathf.Exp(-pickedLerpSpeed * Time.deltaTime));
        king.transform.rotation = Quaternion.Slerp(king.transform.rotation, targetRot, 1f - Mathf.Exp(-pickedLerpSpeed * Time.deltaTime));
    }

    private void PutDown(bool cancelOnly, bool playDropSfx = false)
    {
        picked = false;
        board.ClearHighlights();
        board.ClearCaptureBorders();
        if (cancelOnly || !turnManager.HasForcedSupportOrders)
        {
            turnManager.ClearForcedSupportPreview();
        }

        if (playDropSfx)
        {
            SoundManager.Instance?.PlaySfx(kingDropSfxKey);
        }

        if (cancelOnly)
        {
            king.transform.position = board.CoordToPieceWorld(king.Coord);
            king.transform.rotation = groundedRotationInitialized ? groundedRotation : king.transform.rotation;
        }
    }

    private void ForcePutDownAndClear()
    {
        picked = false;
        board.ClearHighlights();
        board.ClearCaptureBorders();
        if (!turnManager.HasForcedSupportOrders)
        {
            turnManager.ClearForcedSupportPreview();
        }
        if (board == null || king == null || !board.IsPieceMoving(king))
        {
            king.transform.position = board.CoordToPieceWorld(king.Coord);
            king.transform.rotation = groundedRotationInitialized ? groundedRotation : king.transform.rotation;
        }
    }

    private void CaptureGroundedRotation(bool force)
    {
        if (king == null)
        {
            return;
        }

        if (force || !groundedRotationInitialized)
        {
            groundedRotation = king.transform.rotation;
            groundedRotationInitialized = true;
        }
    }

    private void UpdateSupportPreviewUnderMouse()
    {
        if (mainCamera == null || board == null || turnManager == null || !picked)
        {
            turnManager.ClearForcedSupportPreview();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(GetPointerPosition());
        if (!board.TryGetCoordFromRay(ray, out BoardCoord target))
        {
            turnManager.ClearForcedSupportPreview();
            return;
        }

        if (target.Equals(king.Coord))
        {
            turnManager.ClearForcedSupportPreview();
            return;
        }

        List<BoardCoord> legalMoves = GetKingMoveCandidates();
        List<BoardCoord> legalAttacks = GetKingAttackCandidates();

        bool isMove = legalMoves.Contains(target);
        ChessPiece victim = board.GetPieceAt(target);
        bool isCapture = victim != null && victim.Team == Team.Enemy && legalAttacks.Contains(target);
        if (!isMove && !isCapture)
        {
            turnManager.ClearForcedSupportPreview();
            return;
        }

        KingMoveEvaluation evaluation = EvaluateKingMoveSafety(target, isCapture ? victim : null);
        if (!evaluation.requiresSupport)
        {
            turnManager.ClearForcedSupportPreview();
            return;
        }

        List<ChessPiece> allies = new();
        List<ChessPiece> enemies = new();
        List<BoardCoord> targets = new();
        bool hasSuperSave = false;
        for (int i = 0; i < evaluation.supportPlan.Count; i++)
        {
            SupportOrder order = evaluation.supportPlan[i];
            allies.Add(order.ally);
            enemies.Add(order.enemy);
            targets.Add(order.destination);
            hasSuperSave |= order.isSuperSave;
        }

        // Hover/preview phase hides label for SuperSave cases.
        if (hasSuperSave)
        {
            turnManager.ClearForcedSupportPreview();
            return;
        }

        turnManager.PreviewForcedSupportAllies(allies, useSuperSaveLabel: false, targetEnemies: enemies, targetCoords: targets);
    }

    private List<BoardCoord> GetKingMoveCandidates()
    {
        if (kingQueenSkill != null && kingQueenSkill.IsActive)
        {
            HashSet<BoardCoord> merged = new();
            List<BoardCoord> queenMoves = ChessRules.GetMoveCandidates(board, king, PieceType.Queen);
            for (int i = 0; i < queenMoves.Count; i++)
            {
                merged.Add(queenMoves[i]);
            }

            List<BoardCoord> knightMoves = ChessRules.GetMoveCandidates(board, king, PieceType.Knight);
            for (int i = 0; i < knightMoves.Count; i++)
            {
                merged.Add(knightMoves[i]);
            }

            return new List<BoardCoord>(merged);
        }

        return ChessRules.GetMoveCandidates(board, king, PieceType.King);
    }

    private List<BoardCoord> GetKingAttackCandidates()
    {
        if (kingQueenSkill != null && kingQueenSkill.IsActive)
        {
            HashSet<BoardCoord> merged = new();
            List<BoardCoord> queenAttacks = ChessRules.GetAttackCandidates(board, king, PieceType.Queen);
            for (int i = 0; i < queenAttacks.Count; i++)
            {
                merged.Add(queenAttacks[i]);
            }

            List<BoardCoord> knightAttacks = ChessRules.GetAttackCandidates(board, king, PieceType.Knight);
            for (int i = 0; i < knightAttacks.Count; i++)
            {
                merged.Add(knightAttacks[i]);
            }

            return new List<BoardCoord>(merged);
        }

        return ChessRules.GetAttackCandidates(board, king, PieceType.King);
    }

    private static bool IsSpaceHeld()
    {
        bool held = Input.GetKey(KeyCode.Space);
#if ENABLE_INPUT_SYSTEM
        if (!held && Keyboard.current != null)
        {
            held = Keyboard.current.spaceKey.isPressed;
        }
#endif
        return held;
    }

    private static bool IsLeftMousePressedThisFrame()
    {
        bool pressed = Input.GetMouseButtonDown(0);
#if ENABLE_INPUT_SYSTEM
        if (!pressed && Mouse.current != null)
        {
            pressed = Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return pressed;
    }

    private static bool IsRightMousePressedThisFrame()
    {
        bool pressed = Input.GetMouseButtonDown(1);
#if ENABLE_INPUT_SYSTEM
        if (!pressed && Mouse.current != null)
        {
            pressed = Mouse.current.rightButton.wasPressedThisFrame;
        }
#endif
        return pressed;
    }

    private static Vector2 GetPointerPosition()
    {
        Vector2 pos = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            pos = Mouse.current.position.ReadValue();
        }
#endif
        return pos;
    }
}
