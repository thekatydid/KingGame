using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Serializable]
    public struct RuntimeState
    {
        public int currentGlobalTurn;
        public int lastProcessedTurn;
    }

    [Serializable]
    private sealed class StageData
    {
        public string stageId;
        public WaveData[] waves;
    }

    [Serializable]
    private sealed class WaveData
    {
        public string waveName;
        public int waveTurn = 0;
        public SpawnEntry[] entries;
    }

    [Serializable]
    private sealed class SpawnEntry
    {
        public int turn = 1;
        public string piece = "Pawn";
        public int count = 1;
    }

    [Header("References")]
    [SerializeField] private ChessBoardManager board;

    [Header("Stage JSON")]
    [SerializeField] private TextAsset stageJson;
    [SerializeField] private bool isTest;
    [SerializeField, Min(1)] private int playerSafeBoxSize = 10;
    [Header("Stage Audio")]
    [SerializeField] private bool playStageBgmOnStart = true;
    [SerializeField] private string stageBgmKey = "MainBGM";
    [SerializeField] private bool restartStageBgmOnStart = true;
    [SerializeField] [Min(0f)] private float stageBgmRetrySeconds = 2f;

    [Header("Runtime (Debug)")]
    [SerializeField, Min(1)] private int currentGlobalTurn = 1;
    [SerializeField] private string loadedStageId;
    [SerializeField, Min(-1)] private int lastProcessedTurn = -1;

    private StageData stageData;
    private bool stageBgmRequested;
    private Coroutine stageBgmCoroutine;
    private readonly HashSet<int> startedWaveIndices = new();
    public event Action<int, string> OnWaveStarted;

    private void Start()
    {
        RequestStageBgmPlayback();
    }

    public void Initialize(ChessBoardManager boardManager)
    {
        board = boardManager;
        LoadStageData();
        ResetStageProgress();
        RequestStageBgmPlayback();
    }

    public void SetStageJson(TextAsset json)
    {
        stageJson = json;
        LoadStageData();
        ResetStageProgress();
    }

    public void ResetStageProgress()
    {
        currentGlobalTurn = 1;
        lastProcessedTurn = -1;
        startedWaveIndices.Clear();
    }

    public RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            currentGlobalTurn = currentGlobalTurn,
            lastProcessedTurn = lastProcessedTurn
        };
    }

    public void RestoreRuntimeState(RuntimeState state)
    {
        currentGlobalTurn = Mathf.Max(1, state.currentGlobalTurn);
        lastProcessedTurn = state.lastProcessedTurn;
        RebuildStartedWavesFromCurrentTurn();
    }

    public void SpawnEnemiesForTurn()
    {
        SpawnEnemiesForTurn(currentGlobalTurn);
        currentGlobalTurn++;
    }

    public void SpawnEnemiesForTurn(int globalTurn)
    {
        if (!EnsureReady())
        {
            return;
        }

        if (globalTurn < 1)
        {
            return;
        }

        currentGlobalTurn = globalTurn;
        if (lastProcessedTurn == currentGlobalTurn)
        {
            return;
        }

        lastProcessedTurn = currentGlobalTurn;
        NotifyWaveStartsForTurn(currentGlobalTurn);

        if (stageData == null || stageData.waves == null || stageData.waves.Length == 0)
        {
            return;
        }

        for (int waveIndex = 0; waveIndex < stageData.waves.Length; waveIndex++)
        {
            WaveData wave = stageData.waves[waveIndex];
            if (wave == null || wave.entries == null || wave.entries.Length == 0)
            {
                continue;
            }

            int waveStartTurn = Mathf.Max(1, wave.waveTurn);
            for (int i = 0; i < wave.entries.Length; i++)
            {
                SpawnEntry entry = wave.entries[i];
                if (entry == null)
                {
                    continue;
                }

                int entryOffsetTurn = Mathf.Max(0, entry.turn);
                int effectiveTurn = waveStartTurn + entryOffsetTurn;
                if (effectiveTurn != currentGlobalTurn)
                {
                    continue;
                }

                if (!TryParsePieceType(entry.piece, out PieceType pieceType))
                {
                    Debug.LogWarning($"[StageManager] Unknown piece type '{entry.piece}' at wave {waveIndex + 1}, entry turn {entry.turn}.");
                    continue;
                }

                int spawnCount = Mathf.Max(0, entry.count);
                SpawnEnemiesAtEdge(pieceType, spawnCount);
            }
        }
    }

    public string GetStageDisplayName()
    {
        if (stageData == null)
        {
            LoadStageData();
        }

        if (!string.IsNullOrWhiteSpace(stageData?.stageId))
        {
            return stageData.stageId;
        }

        return stageJson != null ? stageJson.name : "Stage";
    }

    public string GetCurrentWaveDisplayName()
    {
        if (stageData == null)
        {
            LoadStageData();
        }

        if (stageData?.waves == null || stageData.waves.Length == 0)
        {
            return "Wave";
        }

        int turn = Mathf.Max(1, currentGlobalTurn);
        int currentIndex = 0;
        int bestStart = int.MinValue;
        for (int i = 0; i < stageData.waves.Length; i++)
        {
            WaveData wave = stageData.waves[i];
            if (wave == null)
            {
                continue;
            }

            int start = Mathf.Max(1, wave.waveTurn);
            if (start <= turn && start >= bestStart)
            {
                bestStart = start;
                currentIndex = i;
            }
        }

        WaveData selected = stageData.waves[currentIndex];
        if (selected != null && !string.IsNullOrWhiteSpace(selected.waveName))
        {
            return selected.waveName;
        }

        return $"Wave {currentIndex + 1}";
    }

    public bool TryGetNextWaveInfo(out string nextWaveName, out int turnsUntilNextWave)
    {
        nextWaveName = string.Empty;
        turnsUntilNextWave = 0;

        if (stageData == null)
        {
            LoadStageData();
        }

        if (stageData?.waves == null || stageData.waves.Length == 0)
        {
            return false;
        }

        int turn = Mathf.Max(1, currentGlobalTurn);
        int bestIndex = -1;
        int bestStart = int.MaxValue;
        for (int i = 0; i < stageData.waves.Length; i++)
        {
            WaveData wave = stageData.waves[i];
            if (wave == null)
            {
                continue;
            }

            int start = Mathf.Max(1, wave.waveTurn);
            if (start > turn && start < bestStart)
            {
                bestStart = start;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        WaveData next = stageData.waves[bestIndex];
        nextWaveName = !string.IsNullOrWhiteSpace(next.waveName) ? next.waveName : $"Wave {bestIndex + 1}";
        turnsUntilNextWave = Mathf.Max(0, bestStart - turn);
        return true;
    }

    public bool HasPendingSpawnsFromCurrentTurn()
    {
        if (stageData == null)
        {
            LoadStageData();
        }

        if (stageData?.waves == null || stageData.waves.Length == 0)
        {
            return false;
        }

        int fromTurn = Mathf.Max(1, currentGlobalTurn);
        for (int waveIndex = 0; waveIndex < stageData.waves.Length; waveIndex++)
        {
            WaveData wave = stageData.waves[waveIndex];
            if (wave == null || wave.entries == null || wave.entries.Length == 0)
            {
                continue;
            }

            int waveStartTurn = Mathf.Max(1, wave.waveTurn);
            for (int i = 0; i < wave.entries.Length; i++)
            {
                SpawnEntry entry = wave.entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (Mathf.Max(0, entry.count) <= 0)
                {
                    continue;
                }

                if (!TryParsePieceType(entry.piece, out _))
                {
                    continue;
                }

                int effectiveTurn = waveStartTurn + Mathf.Max(0, entry.turn);
                if (effectiveTurn >= fromTurn)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsFinalWaveCleared()
    {
        if (!EnsureReady())
        {
            return false;
        }

        if (stageData?.waves == null || stageData.waves.Length == 0)
        {
            return false;
        }

        if (HasPendingSpawnsFromCurrentTurn())
        {
            return false;
        }

        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null && piece.Team == Team.Enemy)
            {
                return false;
            }
        }

        return true;
    }

    public void PlayStageBgmNow(bool restartIfSame = true)
    {
        if (SoundManager.Instance == null || string.IsNullOrWhiteSpace(stageBgmKey))
        {
            return;
        }

        SoundManager.Instance.PlayBgm(stageBgmKey, restartIfSame);
        stageBgmRequested = true;
    }

    private bool EnsureReady()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoardManager>();
        }

        if (board == null)
        {
            return false;
        }

        if (stageData == null)
        {
            LoadStageData();
        }

        return true;
    }

    private void TryPlayStageBgm()
    {
        if (stageBgmRequested || !playStageBgmOnStart)
        {
            return;
        }

        if (SoundManager.Instance == null || string.IsNullOrWhiteSpace(stageBgmKey))
        {
            return;
        }

        SoundManager.Instance.PlayBgm(stageBgmKey, restartStageBgmOnStart);
        stageBgmRequested = true;
    }

    private void RequestStageBgmPlayback()
    {
        TryPlayStageBgm();
        if (stageBgmRequested)
        {
            return;
        }

        if (stageBgmCoroutine != null)
        {
            StopCoroutine(stageBgmCoroutine);
        }

        stageBgmCoroutine = StartCoroutine(TryPlayStageBgmRoutine());
    }

    private System.Collections.IEnumerator TryPlayStageBgmRoutine()
    {
        float elapsed = 0f;
        float maxWait = Mathf.Max(0f, stageBgmRetrySeconds);
        while (!stageBgmRequested && elapsed <= maxWait)
        {
            TryPlayStageBgm();
            if (stageBgmRequested)
            {
                break;
            }

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        stageBgmCoroutine = null;
    }

    private void LoadStageData()
    {
        stageData = null;
        loadedStageId = string.Empty;
        startedWaveIndices.Clear();

        if (stageJson == null || string.IsNullOrWhiteSpace(stageJson.text))
        {
            return;
        }

        try
        {
            stageData = JsonUtility.FromJson<StageData>(stageJson.text);
            loadedStageId = stageData != null ? stageData.stageId : string.Empty;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StageManager] Failed to parse stage json '{stageJson.name}'. {ex.Message}");
            stageData = null;
            loadedStageId = string.Empty;
        }
    }

    private void RebuildStartedWavesFromCurrentTurn()
    {
        startedWaveIndices.Clear();
        if (stageData?.waves == null)
        {
            return;
        }

        int turn = Mathf.Max(1, currentGlobalTurn);
        for (int i = 0; i < stageData.waves.Length; i++)
        {
            WaveData wave = stageData.waves[i];
            if (wave == null)
            {
                continue;
            }

            int start = Mathf.Max(1, wave.waveTurn);
            if (start <= turn)
            {
                startedWaveIndices.Add(i);
            }
        }
    }

    private void NotifyWaveStartsForTurn(int globalTurn)
    {
        if (stageData?.waves == null)
        {
            return;
        }

        int turn = Mathf.Max(1, globalTurn);
        for (int i = 0; i < stageData.waves.Length; i++)
        {
            WaveData wave = stageData.waves[i];
            if (wave == null)
            {
                continue;
            }

            int start = Mathf.Max(1, wave.waveTurn);
            if (start != turn || startedWaveIndices.Contains(i))
            {
                continue;
            }

            startedWaveIndices.Add(i);
            string name = !string.IsNullOrWhiteSpace(wave.waveName) ? wave.waveName : $"Wave {i + 1}";
            OnWaveStarted?.Invoke(i + 1, name);
        }
    }

    private void SpawnEnemiesAtEdge(PieceType pieceType, int count)
    {
        if (count <= 0)
        {
            return;
        }

        List<BoardCoord> edgeCoords = BuildAvailableEdgeCoords();
        if (edgeCoords.Count == 0)
        {
            return;
        }

        int spawnCount = Mathf.Min(count, edgeCoords.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, edgeCoords.Count);
            BoardCoord coord = edgeCoords[randomIndex];
            edgeCoords.RemoveAt(randomIndex);

            board.SpawnPiece(pieceType, Team.Enemy, coord);
        }
    }

    private List<BoardCoord> BuildAvailableEdgeCoords()
    {
        List<BoardCoord> coords = new();
        if (board == null || board.Width <= 0 || board.Height <= 0)
        {
            return coords;
        }

        ChessPiece king = FindAllyKing();

        void TryAdd(BoardCoord c)
        {
            if (!board.IsInside(c) || board.IsOccupied(c))
            {
                return;
            }

            if (!isTest && king != null && IsInsidePlayerSafeBox(c, king.Coord, playerSafeBoxSize))
            {
                return;
            }

            if (!coords.Contains(c))
            {
                coords.Add(c);
            }
        }

        for (int x = 0; x < board.Width; x++)
        {
            TryAdd(new BoardCoord(x, 0));
            TryAdd(new BoardCoord(x, board.Height - 1));
        }

        for (int y = 1; y < board.Height - 1; y++)
        {
            TryAdd(new BoardCoord(0, y));
            TryAdd(new BoardCoord(board.Width - 1, y));
        }

        return coords;
    }

    private ChessPiece FindAllyKing()
    {
        if (board == null)
        {
            return null;
        }

        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null && piece.Team == Team.Ally && piece.PieceType == PieceType.King)
            {
                return piece;
            }
        }

        return null;
    }

    private static bool IsInsidePlayerSafeBox(BoardCoord target, BoardCoord center, int boxSize)
    {
        int size = Mathf.Max(1, boxSize);
        int minOffset = -((size - 1) / 2);
        int maxOffset = size / 2;

        int dx = target.x - center.x;
        int dy = target.y - center.y;
        return dx >= minOffset && dx <= maxOffset && dy >= minOffset && dy <= maxOffset;
    }

    private static bool TryParsePieceType(string raw, out PieceType result)
    {
        if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, true, out result))
        {
            return true;
        }

        string key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        switch (key)
        {
            case "폰":
                result = PieceType.Pawn;
                return true;
            case "룩":
                result = PieceType.Rook;
                return true;
            case "나이트":
                result = PieceType.Knight;
                return true;
            case "비숍":
                result = PieceType.Bishop;
                return true;
            case "퀸":
                result = PieceType.Queen;
                return true;
            default:
                result = PieceType.Pawn;
                return false;
        }
    }
}
