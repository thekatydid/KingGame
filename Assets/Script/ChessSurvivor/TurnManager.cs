using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;

public enum AIDifficultyPreset
{
    Easy,
    Normal,
    Hard
}

public enum AIDecisionKey
{
    KingSurvival,
    KillThreatNearKing,
    KillNearestEnemy,
    KillHighValue,
    AvoidDeathZone,
    DontBlockKing,
    SetupNextTurnKill,
    PreferInDomain
}

[System.Serializable]
public class AIWeightSet
{
    public float kingSurvival = 2.0f;
    public float killThreatNearKing = 1.6f;
    public float killNearestEnemy = 1.0f;
    public float killHighValue = 1.2f;
    public float avoidDeathZone = 1.5f;
    public float dontBlockKing = 1.3f;
    public float setupNextTurnKill = 1.1f;
    public float preferInDomain = 0.9f;

    public Dictionary<AIDecisionKey, float> ToDictionary()
    {
        return new Dictionary<AIDecisionKey, float>
        {
            { AIDecisionKey.KingSurvival, kingSurvival },
            { AIDecisionKey.KillThreatNearKing, killThreatNearKing },
            { AIDecisionKey.KillNearestEnemy, killNearestEnemy },
            { AIDecisionKey.KillHighValue, killHighValue },
            { AIDecisionKey.AvoidDeathZone, avoidDeathZone },
            { AIDecisionKey.DontBlockKing, dontBlockKing },
            { AIDecisionKey.SetupNextTurnKill, setupNextTurnKill },
            { AIDecisionKey.PreferInDomain, preferInDomain }
        };
    }
}

public class TurnManager : MonoBehaviour
{
    public enum ForcedSupportActionType
    {
        Kill,
        Move,
        Hold
    }
    [System.Serializable]
    public struct RuntimeState
    {
        public TurnPhase phase;
        public int turnCount;
        public int sharedCharge;
        public int kingKillCount;
        public int kingSkillChargeCount;
        public int kingSkillRemainingTurns;
        public int totalKillCount;
        public int summonCount;
        public int coopPlanCount;
        public int superSaveCount;
        public bool stageClearTriggered;
    }

    [Header("References")]
    [SerializeField] private ChessBoardManager board;
    [SerializeField] private ExperienceSystem experience;
    [SerializeField] private StageManager stageManager;
    [SerializeField] private KingPlayerController kingPlayerController;

    [Header("Summon Charges (turns)")]
    [SerializeField] private int pawnChargeTurns = 2;
    [SerializeField] private int rookChargeTurns = 5;
    [SerializeField] private int bishopChargeTurns = 5;
    [SerializeField] private int knightChargeTurns = 4;
    [SerializeField] private int queenChargeTurns = 10;
    [Header("Initial Spawn Safety")]
    [SerializeField] private bool ensureInitialPiecesOnSceneLoad = true;

    [Header("Kill FX")]
    [SerializeField] private bool enableKillFx = true;
    [SerializeField] private float killFxLifeTime = 1.35f;
    [SerializeField] private float attackSlideDuration = 0.14f;
    [SerializeField] private float playerCaptureSlideDuration = 0.16f;
    [SerializeField] private float kingSkillCaptureDuration = 0.75f;
    [SerializeField] private float kingSkillCaptureDurationPerTile = 0.12f;
    [SerializeField] private float kingSkillCaptureMaxDuration = 1.8f;
    [SerializeField] private float playerTurnEndDelay = 1f;
    [SerializeField] private float allyTurnEndDelay = 1f;
    [SerializeField] private PieceDeathMotionController deathMotionController;
    [Header("SFX")]
    [SerializeField] private string skillUseSfxKey = "SkillUse";
    [SerializeField] private string killImpactSfxKey = "Kill";
    [SerializeField] private string kingCaptureFireSfxKey = "Fire";
    [SerializeField] private float kingCaptureFireDelay = 0.5f;
    [Header("King Kill Cinematic")]
    [SerializeField] private KingKillCinematicController kingKillCinematic;
    [SerializeField] private KingQueenSkillController kingQueenSkillController;
    [SerializeField] private KingSkillRangeAttack kingSkillRangeAttack;
    [SerializeField] private GroundSpikeBurst kingSkillGroundSpikeBurst;
    [SerializeField] private bool autoFindKingSkillGroundSpikeBurst = true;
    [SerializeField] private Vector3 kingSkillGroundSpikeOffset = new(0f, 0.02f, 0f);
    [SerializeField, Min(0)] private int kingSkillGroundSpikeTileRadius = 1;
    [SerializeField] private CinematicDirector cinematicDirector;
    [Header("Turn Start Alerts")]
    [SerializeField] private bool enableCheckAlerts = true;
    [SerializeField] private string checkNotiKey = "Check";
    [SerializeField] private string firstCheckSequenceKey = "CheckSequence";
    [SerializeField] private string firstCheckRescueSequenceKey = "CheckRescueSequence";
    [SerializeField] private string checkMateNotiKey = "CheckMate";
    [SerializeField] private string firstCheckMateInfoSequenceKey = "CheckMateInfoSequence";
    [SerializeField] private float checkMateGameOverDelay = 2f;
    [SerializeField] private string checkMateGameOverReason = "CheckMate";
    [SerializeField] private string stageClearNotiKey = "GameClear";
    [SerializeField] private float stageClearGameOverDelay = 2f;
    [SerializeField] private string stageClearGameOverReason = "StageClear";
    [Header("Weighted Ally AI")]
    [SerializeField] private AIDifficultyPreset difficultyPreset = AIDifficultyPreset.Normal;
    [SerializeField] private int kingDomainRadius = 6;
    [SerializeField] private AIWeightSet easyWeights = new()
    {
        kingSurvival = 3.2f,
        killThreatNearKing = 2.2f,
        killNearestEnemy = 1.1f,
        killHighValue = 1.0f,
        avoidDeathZone = 2.0f,
        dontBlockKing = 1.8f,
        setupNextTurnKill = 1.8f,
        preferInDomain = 1.6f
    };
    [SerializeField] private AIWeightSet normalWeights = new()
    {
        kingSurvival = 2.0f,
        killThreatNearKing = 1.6f,
        killNearestEnemy = 1.0f,
        killHighValue = 1.2f,
        avoidDeathZone = 1.5f,
        dontBlockKing = 1.3f,
        setupNextTurnKill = 1.1f,
        preferInDomain = 0.9f
    };
    [SerializeField] private AIWeightSet hardWeights = new()
    {
        kingSurvival = 1.0f,
        killThreatNearKing = 0.9f,
        killNearestEnemy = 0.9f,
        killHighValue = 1.0f,
        avoidDeathZone = 0.8f,
        dontBlockKing = 0.7f,
        setupNextTurnKill = 0.5f,
        preferInDomain = 0.4f
    };

    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.PlayerTurn;
    public int TurnCount { get; private set; } = 1;
    public ChessPiece KingPiece { get; private set; }
    public int KingKillCount { get; private set; }
    public int KingSkillChargeCount { get; private set; }
    public int TotalKillCount { get; private set; }
    public int SummonCount { get; private set; }
    public int CoopPlanCount { get; private set; }
    public int SuperSaveCount { get; private set; }
    public event Action<int> OnKingKillCountChanged;
    public event Action<int> OnKingSkillChargeChanged;
    public event Action<int> OnTotalKillCountChanged;
    public event Action<int> OnSummonCountChanged;
    public event Action<TurnPhase> OnPhaseChanged;
    public event Action OnKingSkillActivated;
    [SerializeField, Min(0)] private int sharedSummonCharge;
    [Header("Forced Support Label")]
    [SerializeField] private TMP_Text forcedSupportLabelPrefab;
    [SerializeField] private string forcedSupportLabelText = "군신협력";
    [SerializeField] private string superSaveLabelText = "Super Save!";
    [SerializeField] private Vector3 forcedSupportLabelLocalOffset = new(0f, 0f, 1f);
    [SerializeField] private float forcedSupportPathWidth = 0.12f;
    [SerializeField] private float forcedSupportPathY = 0.08f;
    [SerializeField] private Color forcedSupportPathColor = new(1f, 0.35f, 0.35f, 0.95f);

    private Dictionary<AIDecisionKey, float> activeWeights = new();
    private readonly List<ForcedSupportOrder> forcedSupportOrders = new();
    private readonly Dictionary<ChessPiece, TMP_Text> forcedSupportLabels = new();
    private readonly Dictionary<ChessPiece, LineRenderer> forcedSupportLines = new();
    private Coroutine turnStartAlertRoutine;
    private bool stageClearTriggered;
    private bool firstCheckInfoShown;
    private bool firstCheckMateInfoShown;
    private bool firstCheckRescueShown;
    private bool awaitingCheckRescueBySummon;
    public bool HasForcedSupportOrders => forcedSupportOrders.Count > 0;
    public ChessBoardManager Board => board;

    private readonly struct ForcedSupportOrder
    {
        public ChessPiece Ally { get; }
        public ChessPiece Enemy { get; }
        public BoardCoord Destination { get; }
        public ForcedSupportActionType ActionType { get; }
        public bool IsSuperSave { get; }

        public ForcedSupportOrder(ChessPiece ally, ChessPiece enemy, BoardCoord destination, ForcedSupportActionType actionType, bool isSuperSave)
        {
            Ally = ally;
            Enemy = enemy;
            Destination = destination;
            ActionType = actionType;
            IsSuperSave = isSuperSave;
        }
    }

    private void Start()
    {
        if (!ensureInitialPiecesOnSceneLoad)
        {
            return;
        }

        EnsureInitialPiecesIfNeeded();
    }

    public void Initialize(ChessBoardManager boardManager, ExperienceSystem exp, StageManager stage = null)
    {
        board = boardManager;
        experience = exp;
        stageManager = stage != null ? stage : FindFirstObjectByType<StageManager>();

        // Always start a fresh runtime state when the scene/bootstrap initializes.
        SetPhase(TurnPhase.PlayerTurn);
        TurnCount = 1;
        sharedSummonCharge = 0;
        KingKillCount = 0;
        KingSkillChargeCount = 0;
        TotalKillCount = 0;
        SummonCount = 0;
        CoopPlanCount = 0;
        SuperSaveCount = 0;
        stageClearTriggered = false;
        firstCheckInfoShown = false;
        firstCheckMateInfoShown = false;
        firstCheckRescueShown = false;
        awaitingCheckRescueBySummon = false;
        forcedSupportOrders.Clear();
        ClearForcedSupportLabels();

        if (kingQueenSkillController == null)
        {
            kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
        }
        kingQueenSkillController?.RestoreRuntimeState(0, TurnCount);

        OnKingKillCountChanged?.Invoke(KingKillCount);
        OnKingSkillChargeChanged?.Invoke(KingSkillChargeCount);
        OnTotalKillCountChanged?.Invoke(TotalKillCount);
        OnSummonCountChanged?.Invoke(SummonCount);

        ApplyDifficultyPreset(difficultyPreset);
    }

    private void EnsureInitialPiecesIfNeeded()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoardManager>();
        }

        if (board == null)
        {
            return;
        }

        bool hasAnyPiece = false;
        ChessPiece existingKing = null;
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece == null)
            {
                continue;
            }

            hasAnyPiece = true;
            if (piece.Team == Team.Ally && piece.PieceType == PieceType.King)
            {
                existingKing = piece;
                break;
            }
        }

        if (existingKing != null)
        {
            SetKing(existingKing);
            return;
        }

        BoardCoord kingSpawn = FindNearestEmptyToCenter(board);
        ChessPiece king = board.SpawnPiece(PieceType.King, Team.Ally, kingSpawn);
        if (king != null)
        {
            SetKing(king);
        }

        if (hasAnyPiece)
        {
            return;
        }

        int centerX = board.Width / 2;
        int centerY = board.Height / 2;
        board.SpawnPiece(PieceType.Pawn, Team.Ally, new BoardCoord(centerX - 1, centerY));
        board.SpawnPiece(PieceType.Pawn, Team.Ally, new BoardCoord(centerX + 1, centerY));
    }

    private static BoardCoord FindNearestEmptyToCenter(ChessBoardManager boardManager)
    {
        int centerX = boardManager.Width / 2;
        int centerY = boardManager.Height / 2;
        BoardCoord center = new(centerX, centerY);
        if (!boardManager.IsOccupied(center))
        {
            return center;
        }

        int maxR = Mathf.Max(boardManager.Width, boardManager.Height);
        for (int r = 1; r <= maxR; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r)
                    {
                        continue;
                    }

                    BoardCoord c = new(centerX + dx, centerY + dy);
                    if (!boardManager.IsInside(c) || boardManager.IsOccupied(c))
                    {
                        continue;
                    }

                    return c;
                }
            }
        }

        return center;
    }

    public void SetKing(ChessPiece king)
    {
        KingPiece = king;
        board?.ApplyDefaultFacing(KingPiece);
    }

    public HashSet<ChessPiece> CollectKingSkillRangeVictimsForState(BoardCoord center, ChessPiece excludeEnemy = null)
    {
        HashSet<ChessPiece> result = new();
        KingSkillRangeAttack rangeAttack = ResolveKingSkillRangeAttack();
        if (rangeAttack == null || !rangeAttack.IsRangeAttackActive || board == null)
        {
            return result;
        }

        rangeAttack.CollectVictims(board, center, result, excludeEnemy);
        return result;
    }

    public void OnPlayerKingMoveResolved(ChessPiece attacker, BoardCoord targetCoord)
    {
        if (attacker == null || attacker != KingPiece)
        {
            return;
        }

        if (kingQueenSkillController == null)
        {
            kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
        }

        if (kingQueenSkillController == null || !kingQueenSkillController.IsActive)
        {
            return;
        }

        PlayKingSkillGroundSpikeBurst(targetCoord);
        ApplyKingSkillRangeAttack(targetCoord, null);
    }

    public RuntimeState CaptureRuntimeState()
    {
        if (kingQueenSkillController == null)
        {
            kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
        }

        return new RuntimeState
        {
            phase = CurrentPhase,
            turnCount = TurnCount,
            sharedCharge = sharedSummonCharge,
            kingKillCount = KingKillCount,
            kingSkillChargeCount = KingSkillChargeCount,
            kingSkillRemainingTurns = kingQueenSkillController != null
                ? kingQueenSkillController.GetRemainingTurns(TurnCount)
                : 0,
            totalKillCount = TotalKillCount,
            summonCount = SummonCount,
            coopPlanCount = CoopPlanCount,
            superSaveCount = SuperSaveCount,
            stageClearTriggered = this.stageClearTriggered
        };
    }

    public void RestoreRuntimeState(RuntimeState state, ChessPiece king)
    {
        StopAllCoroutines();
        SetPhase(state.phase);
        TurnCount = Mathf.Max(1, state.turnCount);
        sharedSummonCharge = Mathf.Max(0, state.sharedCharge);
        KingKillCount = Mathf.Max(0, state.kingKillCount);
        KingSkillChargeCount = Mathf.Max(0, state.kingSkillChargeCount);
        TotalKillCount = Mathf.Max(0, state.totalKillCount);
        SummonCount = Mathf.Max(0, state.summonCount);
        CoopPlanCount = Mathf.Max(0, state.coopPlanCount);
        SuperSaveCount = Mathf.Max(0, state.superSaveCount);
        stageClearTriggered = state.stageClearTriggered;
        KingPiece = king;
        forcedSupportOrders.Clear();
        ClearForcedSupportLabels();
        if (kingQueenSkillController == null)
        {
            kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
        }

        kingQueenSkillController?.RestoreRuntimeState(state.kingSkillRemainingTurns, TurnCount);
        OnKingKillCountChanged?.Invoke(KingKillCount);
        OnKingSkillChargeChanged?.Invoke(KingSkillChargeCount);
        OnTotalKillCountChanged?.Invoke(TotalKillCount);
        OnSummonCountChanged?.Invoke(SummonCount);
    }

    public void ForcePhase(TurnPhase phase)
    {
        StopAllCoroutines();
        SetPhase(phase);
    }

    public bool IsSummonReady(PieceType type)
    {
        return sharedSummonCharge >= GetBaseCharge(type);
    }

    public int GetStackedCharge(PieceType type)
    {
        return Mathf.Max(sharedSummonCharge, 0);
    }

    public int GetSharedSummonCharge()
    {
        return Mathf.Max(sharedSummonCharge, 0);
    }

    public int GetRequiredCharge(PieceType type)
    {
        return GetBaseCharge(type);
    }

    public void ConsumeCharge(PieceType type)
    {
        int cost = GetBaseCharge(type);
        if (sharedSummonCharge < cost)
        {
            return;
        }

        sharedSummonCharge = Mathf.Max(0, sharedSummonCharge - cost);
    }

    public void RegisterPlayerSummon()
    {
        SummonCount = Mathf.Max(0, SummonCount + 1);
        OnSummonCountChanged?.Invoke(SummonCount);
    }

    public void RegisterCoopPlan(bool isSuperSave)
    {
        CoopPlanCount = Mathf.Max(0, CoopPlanCount + 1);
        if (isSuperSave)
        {
            SuperSaveCount = Mathf.Max(0, SuperSaveCount + 1);
        }
    }

    public void EndPlayerTurn()
    {
        if (CurrentPhase != TurnPhase.PlayerTurn)
        {
            return;
        }

        StartCoroutine(RunAutoTurns());
    }

    public void ClearForcedSupportOrders()
    {
        forcedSupportOrders.Clear();
    }

    public void RegisterForcedSupportKill(ChessPiece ally, ChessPiece enemy, bool isSuperSave = false)
    {
        if (ally == null || enemy == null || ally.Team != Team.Ally || enemy.Team != Team.Enemy)
        {
            return;
        }

        for (int i = 0; i < forcedSupportOrders.Count; i++)
        {
            if (forcedSupportOrders[i].Ally == ally
                && forcedSupportOrders[i].ActionType == ForcedSupportActionType.Kill
                && forcedSupportOrders[i].Enemy == enemy)
            {
                if (isSuperSave && !forcedSupportOrders[i].IsSuperSave)
                {
                    forcedSupportOrders[i] = new ForcedSupportOrder(ally, enemy, enemy.Coord, ForcedSupportActionType.Kill, true);
                }
                return;
            }
        }

        forcedSupportOrders.Add(new ForcedSupportOrder(ally, enemy, enemy.Coord, ForcedSupportActionType.Kill, isSuperSave));
    }

    public void RegisterForcedSupportMove(ChessPiece ally, BoardCoord destination, bool isSuperSave = false)
    {
        if (ally == null || ally.Team != Team.Ally)
        {
            return;
        }

        for (int i = 0; i < forcedSupportOrders.Count; i++)
        {
            if (forcedSupportOrders[i].Ally == ally
                && forcedSupportOrders[i].ActionType == ForcedSupportActionType.Move
                && forcedSupportOrders[i].Destination.Equals(destination))
            {
                if (isSuperSave && !forcedSupportOrders[i].IsSuperSave)
                {
                    forcedSupportOrders[i] = new ForcedSupportOrder(ally, null, destination, ForcedSupportActionType.Move, true);
                }
                return;
            }
        }

        forcedSupportOrders.Add(new ForcedSupportOrder(ally, null, destination, ForcedSupportActionType.Move, isSuperSave));
    }

    public void RegisterForcedSupportHold(ChessPiece ally, bool isSuperSave = false)
    {
        if (ally == null || ally.Team != Team.Ally)
        {
            return;
        }

        for (int i = 0; i < forcedSupportOrders.Count; i++)
        {
            if (forcedSupportOrders[i].Ally == ally
                && forcedSupportOrders[i].ActionType == ForcedSupportActionType.Hold)
            {
                if (isSuperSave && !forcedSupportOrders[i].IsSuperSave)
                {
                    forcedSupportOrders[i] = new ForcedSupportOrder(ally, null, ally.Coord, ForcedSupportActionType.Hold, true);
                }
                return;
            }
        }

        forcedSupportOrders.Add(new ForcedSupportOrder(ally, null, ally.Coord, ForcedSupportActionType.Hold, isSuperSave));
    }

    public void OnPlayerCaptureEnemy(ChessPiece victim)
    {
        if (victim == null || victim.Team != Team.Enemy)
        {
            return;
        }

        Vector3 victimPos = victim.transform.position;
        bool shouldSpawnImpactFx = KingPiece != null;

        board.DetachPiece(victim);
        SpawnKillEffects(
            victim,
            KingPiece != null ? KingPiece.transform.position : victimPos,
            shouldSpawnImpactFx ? Vector3.zero : (Vector3?)null,
            playDelayedFire: KingPiece != null);
        if (KingPiece != null)
        {
            TriggerKingKillCinematic(victimPos);
        }
        experience?.AddExp(2);
        AddTotalKillCount(1);
        AddKingKillCount(1);
        TryHandleFinalWaveClear();
    }

    public bool OnPlayerCaptureEnemy(ChessPiece attacker, ChessPiece victim, BoardCoord victimCoord)
    {
        if (attacker == null || victim == null || victim.Team != Team.Enemy || board == null)
        {
            return false;
        }

        Vector3 victimPos = victim.transform.position;
        board.DetachPiece(victim);
        if (kingQueenSkillController == null)
        {
            kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
        }
        bool isKingAttacker = attacker == KingPiece;
        bool isKingSkillActive = isKingAttacker && kingQueenSkillController != null && kingQueenSkillController.IsActive;
        PieceMoveStyle captureMoveStyle = isKingSkillActive
            ? PieceMoveStyle.SkillDive
            : (isKingAttacker ? PieceMoveStyle.Jump : PieceMoveStyle.Slide);
        float captureDuration = playerCaptureSlideDuration;
        if (isKingSkillActive)
        {
            int tileDistance = Mathf.Max(
                Mathf.Abs(victimCoord.x - attacker.Coord.x),
                Mathf.Abs(victimCoord.y - attacker.Coord.y));
            captureDuration = kingSkillCaptureDuration + tileDistance * kingSkillCaptureDurationPerTile;
            captureDuration = Mathf.Min(captureDuration, kingSkillCaptureMaxDuration);
        }

        bool moved = board.MovePiece(
            attacker,
            victimCoord,
            captureDuration,
            () =>
            {
                SpawnKillEffects(victim, attacker.transform.position, Vector3.zero, isKingAttacker);
                if (isKingSkillActive)
                {
                    PlayKingSkillGroundSpikeBurst(victimCoord);
                    ApplyKingSkillRangeAttack(victimCoord, victim);
                }
            },
            captureMoveStyle);

        if (!moved)
        {
            SpawnKillEffects(victim, attacker.transform.position, Vector3.zero, isKingAttacker);
            if (isKingSkillActive)
            {
                PlayKingSkillGroundSpikeBurst(victimCoord);
                ApplyKingSkillRangeAttack(victimCoord, victim);
            }
            return false;
        }

        TriggerKingKillCinematic(victimPos, captureDuration, isKingSkillActive);
        experience?.AddExp(2);
        AddTotalKillCount(1);
        AddKingKillCount(1);
        TryHandleFinalWaveClear();
        return true;
    }

    public void ResetKingKillCount()
    {
        KingKillCount = 0;
        KingSkillChargeCount = 0;
        OnKingKillCountChanged?.Invoke(KingKillCount);
        OnKingSkillChargeChanged?.Invoke(KingSkillChargeCount);
    }

    public void KingSkill()
    {
        TryUseKingSkill(1);
    }

    public bool TryUseKingSkill(int requiredCharge)
    {
        int required = Mathf.Max(1, requiredCharge);
        if (kingQueenSkillController == null)
        {
            kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
        }

        if (kingQueenSkillController != null && kingQueenSkillController.IsActive)
        {
            return false;
        }

        if (KingSkillChargeCount < required)
        {
            return false;
        }

        KingSkillChargeCount = Mathf.Max(0, KingSkillChargeCount - required);
        OnKingSkillChargeChanged?.Invoke(KingSkillChargeCount);
        SoundManager.Instance?.PlaySfx(skillUseSfxKey);

        if (cinematicDirector == null)
        {
            cinematicDirector = FindFirstObjectByType<CinematicDirector>();
        }

        if (cinematicDirector != null
            && cinematicDirector.TryPlayCinematic(
                CinematicDirector.FirstKingSkillCinematicId,
                onCompleted: ActivateKingSkill))
        {
            return true;
        }

        ActivateKingSkill();
        return true;
    }

    public void ActivateKingSkill()
    {
        OnKingSkillActivated?.Invoke();
    }

    private void AddKingKillCount(int delta)
    {
        if (delta > 0)
        {
            if (kingQueenSkillController == null)
            {
                kingQueenSkillController = FindFirstObjectByType<KingQueenSkillController>();
            }

            if (kingQueenSkillController != null && kingQueenSkillController.IsActive)
            {
                return;
            }
        }

        KingKillCount = Mathf.Max(0, KingKillCount + delta);
        KingSkillChargeCount = Mathf.Max(0, KingSkillChargeCount + delta);
        OnKingKillCountChanged?.Invoke(KingKillCount);
        OnKingSkillChargeChanged?.Invoke(KingSkillChargeCount);
    }

    private void AddTotalKillCount(int delta)
    {
        TotalKillCount = Mathf.Max(0, TotalKillCount + delta);
        OnTotalKillCountChanged?.Invoke(TotalKillCount);
    }

    public void ApplyDifficultyPreset(AIDifficultyPreset preset)
    {
        difficultyPreset = preset;
        activeWeights = preset switch
        {
            AIDifficultyPreset.Easy => easyWeights.ToDictionary(),
            AIDifficultyPreset.Hard => hardWeights.ToDictionary(),
            _ => normalWeights.ToDictionary()
        };
    }

    public void SetWeight(AIDecisionKey key, float value)
    {
        if (activeWeights == null)
        {
            activeWeights = new Dictionary<AIDecisionKey, float>();
        }

        activeWeights[key] = value;
    }

    public bool IsCheck()
    {
        if (KingPiece == null)
        {
            return false;
        }

        List<BoardCoord> danger = board.BuildDangerMap(Team.Enemy);
        return danger.Contains(KingPiece.Coord);
    }

    public bool IsCheckmateLikeBlocked()
    {
        if (KingPiece == null)
        {
            return true;
        }

        List<BoardCoord> moves = ChessRules.GetMoveCandidates(board, KingPiece);
        List<BoardCoord> danger = board.BuildDangerMap(Team.Enemy);
        for (int i = 0; i < moves.Count; i++)
        {
            if (!danger.Contains(moves[i]))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator RunAutoTurns()
    {
        SetPhase(TurnPhase.Busy);

        if (playerTurnEndDelay > 0f)
        {
            yield return new WaitForSeconds(playerTurnEndDelay);
        }

        SetPhase(TurnPhase.AllyAutoTurn);
        yield return RunSingleAutoTurn(Team.Ally);
        if (allyTurnEndDelay > 0f)
        {
            yield return new WaitForSeconds(allyTurnEndDelay);
        }

        SetPhase(TurnPhase.EnemyAutoTurn);
        if (stageManager == null)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        yield return RunSingleAutoTurn(Team.Enemy);
        stageManager?.SpawnEnemiesForTurn(TurnCount);
        TryHandleFinalWaveClear();

        TickSummonCharges();
        TurnCount++;
        forcedSupportOrders.Clear();
        ClearForcedSupportLabels();

        SetPhase(TurnPhase.PlayerTurn);
    }

    private void SetPhase(TurnPhase nextPhase)
    {
        if (CurrentPhase == nextPhase)
        {
            return;
        }

        if (turnStartAlertRoutine != null)
        {
            StopCoroutine(turnStartAlertRoutine);
            turnStartAlertRoutine = null;
        }

        CurrentPhase = nextPhase;
        OnPhaseChanged?.Invoke(CurrentPhase);

        if (enableCheckAlerts && CurrentPhase == TurnPhase.PlayerTurn)
        {
            StartCheckAlertRoutine(showCheckNoti: true, delayOneFrame: true);
        }
    }

    public void ReevaluatePlayerTurnCheckState()
    {
        if (!enableCheckAlerts || CurrentPhase != TurnPhase.PlayerTurn)
        {
            return;
        }

        TryShowFirstCheckRescueNoti();

        // Mid-turn recheck (e.g., after summon placement consumed the last charge).
        StartCheckAlertRoutine(showCheckNoti: false, delayOneFrame: false);
    }

    private void StartCheckAlertRoutine(bool showCheckNoti, bool delayOneFrame)
    {
        if (turnStartAlertRoutine != null)
        {
            StopCoroutine(turnStartAlertRoutine);
            turnStartAlertRoutine = null;
        }

        turnStartAlertRoutine = StartCoroutine(HandlePlayerTurnStartAlerts(showCheckNoti, delayOneFrame));
    }

    private IEnumerator HandlePlayerTurnStartAlerts(bool showCheckNoti, bool delayOneFrame)
    {
        if (delayOneFrame)
        {
            // Wait one frame so board state/UI updates are settled after phase switch.
            yield return null;
        }

        if (!enableCheckAlerts || CurrentPhase != TurnPhase.PlayerTurn || board == null || KingPiece == null)
        {
            turnStartAlertRoutine = null;
            yield break;
        }

        bool noKingMoves = IsKingMovementBlockedByDanger();
        if (!noKingMoves)
        {
            awaitingCheckRescueBySummon = false;
            turnStartAlertRoutine = null;
            yield break;
        }

        NotiManager noti = NotiManager.Instance;
        if (showCheckNoti)
        {
            if (!firstCheckInfoShown && noti != null && !string.IsNullOrWhiteSpace(firstCheckSequenceKey) && noti.ShowSequenceByKey(firstCheckSequenceKey))
            {
                firstCheckInfoShown = true;
            }
            else if (noti != null)
            {
                noti.ShowByKey(checkNotiKey);
            }
        }

        bool canSummonAny = CanSummonAnyPieceNow();
        if (canSummonAny)
        {
            if (showCheckNoti && !firstCheckRescueShown)
            {
                awaitingCheckRescueBySummon = true;
            }
            turnStartAlertRoutine = null;
            yield break;
        }

        awaitingCheckRescueBySummon = false;

        bool terminalNotiCompleted = false;
        bool queuedTerminalCompletion = false;
        if (noti != null)
        {
            bool shouldShowFirstCheckMateInfo = !firstCheckMateInfoShown && !string.IsNullOrWhiteSpace(firstCheckMateInfoSequenceKey);
            bool canShowInfoSequence = shouldShowFirstCheckMateInfo && noti.HasSequenceKey(firstCheckMateInfoSequenceKey);
            if (canShowInfoSequence)
            {
                noti.ShowByKey(checkMateNotiKey, checkMateGameOverDelay);
                noti.ShowSequenceByKey(firstCheckMateInfoSequenceKey, -1f, () => terminalNotiCompleted = true);
                firstCheckMateInfoShown = true;
                queuedTerminalCompletion = true;
            }
            else if (noti.ShowByKey(checkMateNotiKey, checkMateGameOverDelay, () => terminalNotiCompleted = true))
            {
                queuedTerminalCompletion = true;
            }
        }

        if (!queuedTerminalCompletion)
        {
            terminalNotiCompleted = true;
        }

        turnStartAlertRoutine = null;
        SetPhase(TurnPhase.Busy);
        while (!terminalNotiCompleted)
        {
            yield return null;
        }

        GameManager.Instance?.LoseGame(checkMateGameOverReason);
    }

    private void TryShowFirstCheckRescueNoti()
    {
        if (firstCheckRescueShown || !awaitingCheckRescueBySummon)
        {
            return;
        }

        bool noKingMoves = IsKingMovementBlockedByDanger();
        if (noKingMoves)
        {
            return;
        }

        awaitingCheckRescueBySummon = false;
        firstCheckRescueShown = true;

        NotiManager noti = NotiManager.Instance;
        if (noti == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(firstCheckRescueSequenceKey) && noti.ShowSequenceByKey(firstCheckRescueSequenceKey))
        {
            return;
        }
    }

    private bool CanSummonAnyPieceNow()
    {
        // If no summon is affordable this turn, checkmate condition is satisfied.
        PieceType[] summonables =
        {
            PieceType.Pawn,
            PieceType.Knight,
            PieceType.Bishop,
            PieceType.Rook,
            PieceType.Queen
        };

        for (int i = 0; i < summonables.Length; i++)
        {
            if (IsSummonReady(summonables[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsKingMovementBlockedByDanger()
    {
        if (kingPlayerController == null)
        {
            kingPlayerController = FindFirstObjectByType<KingPlayerController>();
        }

        if (kingPlayerController != null)
        {
            return !kingPlayerController.HasAnyLegalKingAction();
        }

        if (board == null || KingPiece == null)
        {
            return false;
        }

        List<BoardCoord> danger = board.BuildDangerMap(Team.Enemy);
        HashSet<BoardCoord> candidates = new();

        List<BoardCoord> moves = ChessRules.GetMoveCandidates(board, KingPiece, PieceType.King);
        for (int i = 0; i < moves.Count; i++)
        {
            candidates.Add(moves[i]);
        }

        List<BoardCoord> attacks = ChessRules.GetAttackCandidates(board, KingPiece, PieceType.King);
        for (int i = 0; i < attacks.Count; i++)
        {
            BoardCoord c = attacks[i];
            ChessPiece victim = board.GetPieceAt(c);
            if (victim != null && victim.Team == Team.Enemy)
            {
                candidates.Add(c);
            }
        }

        if (candidates.Count == 0)
        {
            return true;
        }

        foreach (BoardCoord c in candidates)
        {
            if (!danger.Contains(c))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator RunSingleAutoTurn(Team team)
    {
        List<ChessPiece> actors = new();
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null && piece.Team == team && piece.PieceType != PieceType.King)
            {
                actors.Add(piece);
            }
        }

        for (int i = 0; i < actors.Count; i++)
        {
            ChessPiece actor = actors[i];
            if (actor == null)
            {
                continue;
            }

            ExecutePieceAI(actor);
            yield return new WaitForSeconds(0.08f);
        }
    }

    private void ExecutePieceAI(ChessPiece actor)
    {
        if (actor.Team == Team.Ally)
        {
            ExecuteWeightedAllyAI(actor);
            return;
        }

        ExecuteLegacyEnemyAI(actor);
    }

    private void ExecuteWeightedAllyAI(ChessPiece actor)
    {
        if (TryExecuteForcedSupportOrder(actor))
        {
            return;
        }

        AIActionCandidate best = SelectWeightedAllyAction(actor);
        ExecuteAction(best, actor);
    }

    private bool TryExecuteForcedSupportOrder(ChessPiece actor)
    {
        if (actor == null || actor.Team != Team.Ally)
        {
            return false;
        }

        for (int i = forcedSupportOrders.Count - 1; i >= 0; i--)
        {
            ForcedSupportOrder order = forcedSupportOrders[i];
            if (order.Ally == null || order.Enemy == null)
            {
                if (order.ActionType != ForcedSupportActionType.Move)
                {
                    forcedSupportOrders.RemoveAt(i);
                    continue;
                }
            }

            if (order.Ally == null)
            {
                forcedSupportOrders.RemoveAt(i);
                continue;
            }

            if (order.Ally != actor)
            {
                continue;
            }

            if (order.ActionType == ForcedSupportActionType.Move)
            {
                List<BoardCoord> moves = ChessRules.GetMoveCandidates(board, actor);
                if (!moves.Contains(order.Destination))
                {
                    forcedSupportOrders.RemoveAt(i);
                    return false;
                }

                board.MovePiece(actor, order.Destination, attackSlideDuration);
                RemoveForcedOrdersByAlly(actor);
                return true;
            }

            if (order.ActionType == ForcedSupportActionType.Hold)
            {
                RemoveForcedOrdersByAlly(actor);
                return true;
            }

            if (order.Enemy == null || order.Enemy.Team != Team.Enemy)
            {
                forcedSupportOrders.RemoveAt(i);
                continue;
            }

            if (board.GetPieceAt(order.Enemy.Coord) != order.Enemy)
            {
                forcedSupportOrders.RemoveAt(i);
                continue;
            }

            List<BoardCoord> attacks = ChessRules.GetAttackCandidates(board, actor);
            if (!attacks.Contains(order.Enemy.Coord))
            {
                forcedSupportOrders.RemoveAt(i);
                return false;
            }

            ResolveAttack(actor, order.Enemy);
            RemoveForcedOrdersByEnemy(order.Enemy);
            RemoveForcedOrdersByAlly(actor);
            return true;
        }

        return false;
    }

    private void RemoveForcedOrdersByEnemy(ChessPiece enemy)
    {
        for (int i = forcedSupportOrders.Count - 1; i >= 0; i--)
        {
            if (forcedSupportOrders[i].Enemy == enemy)
            {
                forcedSupportOrders.RemoveAt(i);
            }
        }
    }

    private void RemoveForcedOrdersByAlly(ChessPiece ally)
    {
        for (int i = forcedSupportOrders.Count - 1; i >= 0; i--)
        {
            if (forcedSupportOrders[i].Ally == ally)
            {
                forcedSupportOrders.RemoveAt(i);
            }
        }
    }

    private void OnDisable()
    {
        ClearForcedSupportLabels();
    }

    public void PreviewForcedSupportAllies(
        List<ChessPiece> allies,
        bool useSuperSaveLabel = false,
        List<ChessPiece> targetEnemies = null,
        List<BoardCoord> targetCoords = null)
    {
        ClearForcedSupportLabels();
        if (allies == null || allies.Count == 0)
        {
            return;
        }

        string labelText = useSuperSaveLabel ? superSaveLabelText : forcedSupportLabelText;

        HashSet<ChessPiece> unique = new();
        for (int i = 0; i < allies.Count; i++)
        {
            ChessPiece ally = allies[i];
            if (ally == null || !unique.Add(ally))
            {
                continue;
            }

            TMP_Text label = CreateForcedSupportLabel(ally.transform, labelText);
            if (label == null)
            {
                continue;
            }

            forcedSupportLabels[ally] = label;

            ChessPiece enemy = null;
            if (targetEnemies != null && i < targetEnemies.Count)
            {
                enemy = targetEnemies[i];
            }

            BoardCoord? targetCoord = null;
            if (enemy != null)
            {
                targetCoord = enemy.Coord;
            }
            else if (targetCoords != null && i < targetCoords.Count)
            {
                targetCoord = targetCoords[i];
            }

            if (targetCoord.HasValue)
            {
                LineRenderer path = CreateForcedSupportPathLine(ally, targetCoord.Value);
                if (path != null)
                {
                    forcedSupportLines[ally] = path;
                }
            }
        }
    }

    public void ClearForcedSupportPreview()
    {
        ClearForcedSupportLabels();
    }

    private TMP_Text CreateForcedSupportLabel(Transform parent, string labelText)
    {
        TMP_Text label;
        if (forcedSupportLabelPrefab != null)
        {
            label = Instantiate(forcedSupportLabelPrefab, parent);
        }
        else
        {
            GameObject go = new("ForcedSupportLabel");
            go.transform.SetParent(parent, false);
            label = go.AddComponent<TextMeshPro>();
            label.fontSize = 4.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.cyan;
        }

        label.text = labelText;
        label.transform.localPosition = forcedSupportLabelLocalOffset;
        label.transform.localRotation = Quaternion.identity;
        BillboardToCamera billboard = label.GetComponent<BillboardToCamera>();
        if (billboard == null)
        {
            billboard = label.gameObject.AddComponent<BillboardToCamera>();
        }

        billboard.Configure(parent, forcedSupportLabelLocalOffset);
        return label;
    }

    private void ClearForcedSupportLabels()
    {
        foreach (KeyValuePair<ChessPiece, TMP_Text> kv in forcedSupportLabels)
        {
            if (kv.Value != null)
            {
                Destroy(kv.Value.gameObject);
            }
        }

        forcedSupportLabels.Clear();

        foreach (KeyValuePair<ChessPiece, LineRenderer> kv in forcedSupportLines)
        {
            if (kv.Value != null)
            {
                Destroy(kv.Value.gameObject);
            }
        }

        forcedSupportLines.Clear();
    }

    private LineRenderer CreateForcedSupportPathLine(ChessPiece ally, BoardCoord targetCoord)
    {
        if (board == null || ally == null)
        {
            return null;
        }

        GameObject go = new($"ForcedSupportPath_{ally.name}");
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            line.sharedMaterial = new Material(shader);
        }

        line.startWidth = forcedSupportPathWidth;
        line.endWidth = forcedSupportPathWidth;
        line.startColor = forcedSupportPathColor;
        line.endColor = forcedSupportPathColor;

        Vector3 up = board.transform.up * forcedSupportPathY;
        line.SetPosition(0, board.CoordToWorld(ally.Coord) + up);
        line.SetPosition(1, board.CoordToWorld(targetCoord) + up);
        line.enabled = true;
        return line;
    }

    private void ExecuteLegacyEnemyAI(ChessPiece actor)
    {
        AIActionCandidate selected = SelectLegacyAction(actor);
        ExecuteAction(selected, actor);
    }

    public bool TryGetPredictedAllyAction(ChessPiece allyPiece, out BoardCoord start, out BoardCoord destination, out bool isAttack)
    {
        start = default;
        destination = default;
        isAttack = false;

        if (board == null || allyPiece == null || allyPiece.Team != Team.Ally || allyPiece.PieceType == PieceType.King)
        {
            return false;
        }

        start = allyPiece.Coord;
        AIActionCandidate selected = SelectWeightedAllyAction(allyPiece);
        switch (selected.Type)
        {
            case AIActionType.Attack:
                if (selected.Target == null)
                {
                    return false;
                }

                destination = selected.Target.Coord;
                isAttack = true;
                return true;
            case AIActionType.Move:
                destination = selected.Destination;
                isAttack = false;
                return true;
            default:
                return false;
        }
    }

    private void ResolveAttack(ChessPiece attacker, ChessPiece victim)
    {
        if (victim.Team == Team.Ally)
        {
            BoardCoord victimCoord = victim.Coord;
            board.RemovePiece(victim);
            if (victim.PieceType == PieceType.King)
            {
                GameManager.Instance?.LoseGame("킹이 파괴되었습니다.");
            }

            if (attacker != null && !board.IsOccupied(victimCoord))
            {
                board.MovePiece(attacker, victimCoord, attackSlideDuration);
            }

            return;
        }

        if (victim.Team == Team.Enemy)
        {
            BoardCoord victimCoord = victim.Coord;
            board.DetachPiece(victim);
            if (attacker != null && attacker.Team == Team.Ally && !board.IsOccupied(victimCoord))
            {
                board.MovePiece(
                    attacker,
                    victimCoord,
                    attackSlideDuration,
                    () => SpawnKillEffects(victim, attacker.transform.position, Vector3.zero));
            }
            else
            {
                SpawnKillEffects(victim, attacker != null ? attacker.transform.position : victim.transform.position, null);
            }

            experience?.AddExp(2);
            AddTotalKillCount(1);
            TryHandleFinalWaveClear();
        }
    }

    private void TryHandleFinalWaveClear()
    {
        if (stageClearTriggered)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            return;
        }

        if (stageManager == null)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        if (stageManager == null || !stageManager.IsFinalWaveCleared())
        {
            return;
        }

        stageClearTriggered = true;
        StartCoroutine(HandleStageClearRoutine());
    }

    private IEnumerator HandleStageClearRoutine()
    {
        SetPhase(TurnPhase.Busy);

        float delay = Mathf.Max(0f, stageClearGameOverDelay);
        if (!string.IsNullOrWhiteSpace(stageClearNotiKey))
        {
            NotiManager.Instance?.ShowByKey(stageClearNotiKey, delay);
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        GameManager.Instance?.LoseGame(stageClearGameOverReason);
    }

    private ChessPiece FindNearestTarget(List<BoardCoord> attackCoords, BoardCoord from, Team targetTeam)
    {
        ChessPiece best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < attackCoords.Count; i++)
        {
            ChessPiece target = board.GetPieceAt(attackCoords[i]);
            if (target == null || target.Team != targetTeam)
            {
                continue;
            }

            float d = DistanceSq(from, target.Coord);
            if (d < bestDistance)
            {
                best = target;
                bestDistance = d;
            }
        }

        return best;
    }

    private AIActionCandidate SelectWeightedAllyAction(ChessPiece actor)
    {
        if (actor == null)
        {
            return AIActionCandidate.MakeWait();
        }

        if (KingPiece == null || activeWeights == null || activeWeights.Count == 0)
        {
            return SelectLegacyAction(actor);
        }

        Team targetTeam = Team.Enemy;
        HashSet<BoardCoord> enemyAttackMap = new(board.BuildDangerMap(targetTeam));
        int kingEscapeCount = ComputeKingEscapeCount(enemyAttackMap, actor, actor.Coord);

        List<AIActionCandidate> candidates = BuildActionCandidates(actor, targetTeam);
        if (candidates.Count == 0)
        {
            return AIActionCandidate.MakeWait();
        }

        AIActionCandidate best = candidates[0];
        float bestScore = ScoreAction(best, actor, enemyAttackMap, kingEscapeCount, targetTeam);
        for (int i = 1; i < candidates.Count; i++)
        {
            float score = ScoreAction(candidates[i], actor, enemyAttackMap, kingEscapeCount, targetTeam);
            if (score > bestScore)
            {
                best = candidates[i];
                bestScore = score;
            }
        }

        return best;
    }

    private AIActionCandidate SelectLegacyAction(ChessPiece actor)
    {
        if (actor == null)
        {
            return AIActionCandidate.MakeWait();
        }

        Team enemyTeam = actor.Team == Team.Ally ? Team.Enemy : Team.Ally;

        List<BoardCoord> attacks = ChessRules.GetAttackCandidates(board, actor);
        ChessPiece kingThreat = null;
        for (int i = 0; i < attacks.Count; i++)
        {
            ChessPiece target = board.GetPieceAt(attacks[i]);
            if (target != null && target.PieceType == PieceType.King && target.Team == enemyTeam)
            {
                kingThreat = target;
                break;
            }
        }

        if (kingThreat != null)
        {
            return AIActionCandidate.MakeAttack(kingThreat);
        }

        ChessPiece nearestKillable = FindNearestTarget(attacks, actor.Coord, enemyTeam);
        if (nearestKillable != null)
        {
            return AIActionCandidate.MakeAttack(nearestKillable);
        }

        List<BoardCoord> moves = ChessRules.GetMoveCandidates(board, actor);
        if (moves.Count == 0 || KingPiece == null)
        {
            return AIActionCandidate.MakeWait();
        }

        BoardCoord best = moves[0];
        float bestDistance = DistanceSq(best, KingPiece.Coord);
        for (int i = 1; i < moves.Count; i++)
        {
            float d = DistanceSq(moves[i], KingPiece.Coord);
            if (d < bestDistance)
            {
                best = moves[i];
                bestDistance = d;
            }
        }

        return AIActionCandidate.MakeMove(best);
    }

    private void TickSummonCharges()
    {
        sharedSummonCharge = Mathf.Max(0, sharedSummonCharge + 1);
    }

    private int GetBaseCharge(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => pawnChargeTurns,
            PieceType.Rook => rookChargeTurns,
            PieceType.Bishop => bishopChargeTurns,
            PieceType.Knight => knightChargeTurns,
            PieceType.Queen => queenChargeTurns,
            _ => 1
        };
    }

    private static float DistanceSq(BoardCoord a, BoardCoord b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    private static int DistanceManhattan(BoardCoord a, BoardCoord b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private int ComputeKingEscapeCount(HashSet<BoardCoord> enemyAttackMap, ChessPiece movingActor, BoardCoord actorTarget)
    {
        List<BoardCoord> kingMoves = ChessRules.GetMoveCandidates(board, KingPiece);
        int count = 0;
        for (int i = 0; i < kingMoves.Count; i++)
        {
            BoardCoord move = kingMoves[i];

            bool occupiedByOther = board.IsOccupied(move) && !move.Equals(movingActor.Coord);
            bool occupiedByMovedActor = move.Equals(actorTarget);
            if (occupiedByOther || occupiedByMovedActor)
            {
                continue;
            }

            if (enemyAttackMap.Contains(move))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private float ScoreAction(AIActionCandidate action, ChessPiece actor, HashSet<BoardCoord> enemyAttackMap, int kingEscapeCount, Team targetTeam)
    {
        float score = 0f;
        float riskLevel = kingEscapeCount <= 1 ? 2f : (kingEscapeCount <= 2 ? 1f : 0f);
        int maxBoardDist = board.Width + board.Height;

        if (action.Type == AIActionType.Attack && action.Target != null)
        {
            int distTargetToKing = DistanceManhattan(action.Target.Coord, KingPiece.Coord);
            int distActorToTarget = DistanceManhattan(actor.Coord, action.Target.Coord);

            bool targetThreatensKing = ChessRules.GetThreatenedCoords(board, action.Target).Contains(KingPiece.Coord);

            score += Weight(AIDecisionKey.KingSurvival) * riskLevel * (targetThreatensKing ? 1.6f : 0.35f);
            score += Weight(AIDecisionKey.KillThreatNearKing) * (maxBoardDist - distTargetToKing);
            score += Weight(AIDecisionKey.KillNearestEnemy) * (maxBoardDist - distActorToTarget);
            score += Weight(AIDecisionKey.KillHighValue) * GetPieceValue(action.Target.PieceType);

            if (IsInsideKingDomain(action.Target.Coord))
            {
                score += Weight(AIDecisionKey.PreferInDomain) * 20f;
            }
        }
        else if (action.Type == AIActionType.Move)
        {
            bool intoDanger = enemyAttackMap.Contains(action.Destination);
            if (intoDanger)
            {
                score -= Weight(AIDecisionKey.AvoidDeathZone) * 120f;
            }
            else
            {
                score += Weight(AIDecisionKey.AvoidDeathZone) * 12f;
            }

            int nextKingEscape = ComputeKingEscapeCount(enemyAttackMap, actor, action.Destination);
            int escapeDelta = nextKingEscape - kingEscapeCount;
            score += Weight(AIDecisionKey.DontBlockKing) * (escapeDelta * 30f);

            if (CanSetUpNextTurnKill(actor, action.Destination, targetTeam))
            {
                score += Weight(AIDecisionKey.SetupNextTurnKill) * 28f;
            }

            if (IsInsideKingDomain(action.Destination))
            {
                score += Weight(AIDecisionKey.PreferInDomain) * 12f;
            }

            int beforeDist = DistanceManhattan(actor.Coord, KingPiece.Coord);
            int afterDist = DistanceManhattan(action.Destination, KingPiece.Coord);
            score += Weight(AIDecisionKey.KingSurvival) * riskLevel * (beforeDist - afterDist) * 4f;
        }

        return score;
    }

    private bool CanSetUpNextTurnKill(ChessPiece actor, BoardCoord movedCoord, Team targetTeam)
    {
        List<ChessPiece> enemies = new();
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null && piece.Team == targetTeam)
            {
                enemies.Add(piece);
            }
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            if (CanAttackFrom(actor.PieceType, movedCoord, enemies[i].Coord, actor.Coord))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanAttackFrom(PieceType type, BoardCoord from, BoardCoord target, BoardCoord actorOriginalCoord)
    {
        int dx = target.x - from.x;
        int dy = target.y - from.y;

        switch (type)
        {
            case PieceType.Pawn:
                return Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 1;
            case PieceType.Knight:
                return (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 2) || (Mathf.Abs(dx) == 2 && Mathf.Abs(dy) == 1);
            case PieceType.King:
                return Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == 1;
            case PieceType.Rook:
                if (dx != 0 && dy != 0) return false;
                return IsPathClearForLine(from, target, actorOriginalCoord);
            case PieceType.Bishop:
                if (Mathf.Abs(dx) != Mathf.Abs(dy)) return false;
                return IsPathClearForLine(from, target, actorOriginalCoord);
            case PieceType.Queen:
                if (dx == 0 || dy == 0 || Mathf.Abs(dx) == Mathf.Abs(dy))
                {
                    return IsPathClearForLine(from, target, actorOriginalCoord);
                }

                return false;
            default:
                return false;
        }
    }

    private bool IsPathClearForLine(BoardCoord from, BoardCoord target, BoardCoord actorOriginalCoord)
    {
        int stepX = target.x == from.x ? 0 : (target.x > from.x ? 1 : -1);
        int stepY = target.y == from.y ? 0 : (target.y > from.y ? 1 : -1);
        BoardCoord cursor = new(from.x + stepX, from.y + stepY);

        while (!cursor.Equals(target))
        {
            if (!cursor.Equals(actorOriginalCoord) && board.IsOccupied(cursor))
            {
                return false;
            }

            cursor = new BoardCoord(cursor.x + stepX, cursor.y + stepY);
        }

        return true;
    }

    private List<AIActionCandidate> BuildActionCandidates(ChessPiece actor, Team targetTeam)
    {
        List<AIActionCandidate> result = new();
        List<BoardCoord> attacks = ChessRules.GetAttackCandidates(board, actor);
        for (int i = 0; i < attacks.Count; i++)
        {
            ChessPiece target = board.GetPieceAt(attacks[i]);
            if (target != null && target.Team == targetTeam)
            {
                result.Add(AIActionCandidate.MakeAttack(target));
            }
        }

        List<BoardCoord> moves = ChessRules.GetMoveCandidates(board, actor);
        for (int i = 0; i < moves.Count; i++)
        {
            result.Add(AIActionCandidate.MakeMove(moves[i]));
        }

        if (result.Count == 0)
        {
            result.Add(AIActionCandidate.MakeWait());
        }

        return result;
    }

    private void ExecuteAction(AIActionCandidate action, ChessPiece actor)
    {
        switch (action.Type)
        {
            case AIActionType.Attack:
                if (action.Target != null)
                {
                    ResolveAttack(actor, action.Target);
                }
                break;
            case AIActionType.Move:
                board.MovePiece(actor, action.Destination);
                break;
            case AIActionType.Wait:
            default:
                break;
        }
    }

    private float Weight(AIDecisionKey key)
    {
        return activeWeights.TryGetValue(key, out float value) ? value : 0f;
    }

    private bool IsInsideKingDomain(BoardCoord coord)
    {
        if (KingPiece == null)
        {
            return false;
        }

        return DistanceManhattan(coord, KingPiece.Coord) <= kingDomainRadius;
    }

    private static float GetPieceValue(PieceType type)
    {
        return type switch
        {
            PieceType.Queen => 80f,
            PieceType.Rook => 60f,
            PieceType.Bishop => 50f,
            PieceType.Knight => 50f,
            PieceType.Pawn => 20f,
            PieceType.King => 120f,
            _ => 10f
        };
    }

    private void SpawnKillEffects(
        ChessPiece victim,
        Vector3 attackerPos,
        Vector3? delayedImpactPos,
        bool playDelayedFire = false,
        float preLaunchTimingMultiplier = 1f)
    {
        if (victim == null)
        {
            return;
        }

        if (!enableKillFx)
        {
            Destroy(victim.gameObject);
            return;
        }

        if (deathMotionController == null)
        {
            deathMotionController = FindFirstObjectByType<PieceDeathMotionController>();
            if (deathMotionController == null)
            {
                deathMotionController = gameObject.AddComponent<PieceDeathMotionController>();
            }
        }

        deathMotionController.Play(
            victim,
            attackerPos,
            killFxLifeTime,
            () =>
            {
                SoundManager.Instance?.PlaySfx(killImpactSfxKey);

                if (delayedImpactPos.HasValue)
                {
                    Vector3 impactPos = victim != null
                        ? victim.transform.position + board.transform.up * 0.08f
                        : delayedImpactPos.Value;
                    board.SpawnKingKillImpactFx(impactPos);
                }

                if (playDelayedFire && !string.IsNullOrWhiteSpace(kingCaptureFireSfxKey))
                {
                    StartCoroutine(PlaySfxDelayedRealtime(kingCaptureFireSfxKey, kingCaptureFireDelay));
                }

            },
            preLaunchTimingMultiplier);
    }

    private KingSkillRangeAttack ResolveKingSkillRangeAttack()
    {
        if (kingSkillRangeAttack == null)
        {
            kingSkillRangeAttack = FindFirstObjectByType<KingSkillRangeAttack>();
        }

        return kingSkillRangeAttack;
    }

    private GroundSpikeBurst ResolveKingSkillGroundSpikeBurst()
    {
        if (kingSkillGroundSpikeBurst == null && autoFindKingSkillGroundSpikeBurst)
        {
            kingSkillGroundSpikeBurst = FindFirstObjectByType<GroundSpikeBurst>();
        }

        return kingSkillGroundSpikeBurst;
    }

    private void PlayKingSkillGroundSpikeBurst(BoardCoord targetCoord)
    {
        GroundSpikeBurst burst = ResolveKingSkillGroundSpikeBurst();
        if (burst == null || board == null)
        {
            return;
        }

        int tileRadius = Mathf.Max(0, kingSkillGroundSpikeTileRadius);
        List<Vector3> tilePositions = new();
        for (int y = targetCoord.y - tileRadius; y <= targetCoord.y + tileRadius; y++)
        {
            for (int x = targetCoord.x - tileRadius; x <= targetCoord.x + tileRadius; x++)
            {
                BoardCoord c = new(x, y);
                if (!board.IsInside(c))
                {
                    continue;
                }

                tilePositions.Add(board.CoordToWorld(c) + kingSkillGroundSpikeOffset);
            }
        }

        Vector3 centerPos = board.CoordToWorld(targetCoord) + kingSkillGroundSpikeOffset;
        if (tilePositions.Count > 0)
        {
            burst.PlayAtPositions(centerPos, Vector3.up, tilePositions);
        }
        else
        {
            burst.Play(centerPos, Vector3.up);
        }
    }

    private void ApplyKingSkillRangeAttack(BoardCoord center, ChessPiece excludeEnemy)
    {
        KingSkillRangeAttack rangeAttack = ResolveKingSkillRangeAttack();
        if (rangeAttack == null || !rangeAttack.IsRangeAttackActive || board == null)
        {
            return;
        }

        HashSet<ChessPiece> splashVictims = new();
        rangeAttack.CollectVictims(board, center, splashVictims, excludeEnemy);
        if (splashVictims.Count == 0)
        {
            return;
        }

        float splashTimingMultiplier = rangeAttack.SplashPreLaunchTimingMultiplier;
        foreach (ChessPiece victim in splashVictims)
        {
            if (victim == null || victim.Team != Team.Enemy)
            {
                continue;
            }

            Vector3 attackerPos = KingPiece != null
                ? KingPiece.transform.position
                : victim.transform.position;

            board.DetachPiece(victim);
            SpawnKillEffects(
                victim,
                attackerPos,
                Vector3.zero,
                playDelayedFire: false,
                preLaunchTimingMultiplier: splashTimingMultiplier);
            experience?.AddExp(2);
            AddTotalKillCount(1);
            AddKingKillCount(1);
        }

        TryHandleFinalWaveClear();
    }

    private IEnumerator PlaySfxDelayedRealtime(string sfxKey, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        SoundManager.Instance?.PlaySfx(sfxKey);
    }

    private void TriggerKingKillCinematic(Vector3 victimPos, float actionDuration = 0f, bool isSkillDive = false)
    {
        if (kingKillCinematic == null)
        {
            kingKillCinematic = FindFirstObjectByType<KingKillCinematicController>();
        }

        kingKillCinematic?.Play(KingPiece.transform, victimPos, actionDuration, isSkillDive);
    }

    private enum AIActionType
    {
        Attack,
        Move,
        Wait
    }

    private readonly struct AIActionCandidate
    {
        public AIActionType Type { get; }
        public ChessPiece Target { get; }
        public BoardCoord Destination { get; }

        private AIActionCandidate(AIActionType type, ChessPiece target, BoardCoord destination)
        {
            Type = type;
            Target = target;
            Destination = destination;
        }

        public static AIActionCandidate MakeAttack(ChessPiece target)
        {
            return new AIActionCandidate(AIActionType.Attack, target, default);
        }

        public static AIActionCandidate MakeMove(BoardCoord destination)
        {
            return new AIActionCandidate(AIActionType.Move, null, destination);
        }

        public static AIActionCandidate MakeWait()
        {
            return new AIActionCandidate(AIActionType.Wait, null, default);
        }
    }
}
