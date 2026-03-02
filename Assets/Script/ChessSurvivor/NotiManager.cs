using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NotiManager : MonoBehaviour
{
    [Serializable]
    private sealed class NotiJsonRoot
    {
        public NotiEntry[] entries;
        public NotiSequence[] sequences;
    }

    [Serializable]
    private sealed class NotiEntry
    {
        public string key;
        public string text;
        public float duration = -1f;
        public string colorKey;
    }

    [Serializable]
    private sealed class NotiSequence
    {
        public string key;
        public float gap = -1f;
        public NotiSequenceItem[] items;
    }

    [Serializable]
    private sealed class NotiSequenceItem
    {
        public string key;
        public float duration = -1f;
        public float gap = -1f;
    }

    private readonly struct PendingNoti
    {
        public readonly string text;
        public readonly float duration;
        public readonly float gapAfter;
        public readonly bool hasColor;
        public readonly Color color;
        public readonly Action onCompleted;

        public PendingNoti(string text, float duration, float gapAfter, bool hasColor, Color color, Action onCompleted)
        {
            this.text = text;
            this.duration = duration;
            this.gapAfter = gapAfter;
            this.hasColor = hasColor;
            this.color = color;
            this.onCompleted = onCompleted;
        }
    }

    [Serializable]
    private struct NotiColorMapping
    {
        public string key;
        public Color color;
    }

    public static NotiManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private NotiCanvasController notiCanvas;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private StageManager stageManager;

    [Header("Data")]
    [SerializeField] private TextAsset notiJson;
    [SerializeField] [Min(0f)] private float defaultDuration = 2f;
    [SerializeField] [Min(0f)] private float defaultSequenceGap = 0.15f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private string defaultColorKey = "Default";
    [SerializeField] private List<NotiColorMapping> colorMappings = new();

    [Header("Auto Noti")]
    [SerializeField] private bool autoShowFirstKill = true;
    [SerializeField] private bool firstKillUsesSequence = true;
    [SerializeField] private string firstKillKey = "FirstKill";
    [SerializeField] private string firstKillSequenceKey = "FirstKillSequence";
    [SerializeField] private bool autoShowFirstSummon = false;
    [SerializeField] private string firstSummonKey = "FirstSummon";
    [SerializeField] private bool autoShowSecondTurn = true;
    [SerializeField] private string secondTurnKey = "SecondTurn";
    [SerializeField] private bool autoShowWave2Info = true;
    [SerializeField] private int wave2Number = 2;
    [SerializeField] private string wave2SequenceKey = "Wave2Sequence";

    private readonly Dictionary<string, NotiEntry> entryTable = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NotiSequence> sequenceTable = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Color> colorTable = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<PendingNoti> queue = new();
    private Coroutine queueRoutine;
    private bool firstKillShown;
    private bool firstSummonShown;
    private bool secondTurnShown;
    private bool wave2InfoShown;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
        EnsureReferences();
        RebuildTable();
        BindTurnManager();
        BindStageManager();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnbindTurnManager();
        UnbindStageManager();
    }

    public bool ShowByKey(string key)
    {
        return ShowByKey(key, -1f, null);
    }

    public bool ShowByKey(string key, float durationOverride)
    {
        return ShowByKey(key, durationOverride, null);
    }

    public bool ShowByKey(string key, float durationOverride, Action onCompleted)
    {
        if (!TryGetEntry(key, out NotiEntry entry))
        {
            return false;
        }

        ResolveColor(entry, out bool hasColor, out Color color);
        Enqueue(entry.text, ResolveDuration(entry, durationOverride), 0f, hasColor, color, onCompleted);
        return true;
    }

    public bool ShowSequenceByKey(string sequenceKey)
    {
        return ShowSequenceByKey(sequenceKey, -1f, null);
    }

    public bool HasSequenceKey(string sequenceKey)
    {
        return TryGetSequence(sequenceKey, out NotiSequence sequence)
            && sequence != null
            && sequence.items != null
            && sequence.items.Length > 0;
    }

    public bool ShowSequenceByKey(string sequenceKey, float gapOverride)
    {
        return ShowSequenceByKey(sequenceKey, gapOverride, null);
    }

    public bool ShowSequenceByKey(string sequenceKey, float gapOverride, Action onCompleted)
    {
        if (!TryGetSequence(sequenceKey, out NotiSequence sequence))
        {
            return false;
        }

        if (sequence.items == null || sequence.items.Length == 0)
        {
            return false;
        }

        float seqGap = gapOverride >= 0f
            ? gapOverride
            : (sequence.gap >= 0f ? sequence.gap : defaultSequenceGap);

        for (int i = 0; i < sequence.items.Length; i++)
        {
            NotiSequenceItem item = sequence.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.key))
            {
                continue;
            }

            if (!TryGetEntry(item.key, out NotiEntry entry))
            {
                continue;
            }

            float duration = ResolveDuration(entry, item.duration);
            bool isLast = i == sequence.items.Length - 1;
            float itemGap = item.gap >= 0f ? item.gap : seqGap;
            ResolveColor(entry, out bool hasColor, out Color color);
            Enqueue(entry.text, duration, isLast ? 0f : itemGap, hasColor, color, isLast ? onCompleted : null);
        }

        return true;
    }

    [ContextMenu("Reload Noti JSON")]
    public void RebuildTable()
    {
        entryTable.Clear();
        sequenceTable.Clear();
        colorTable.Clear();
        RebuildColorTable();
        if (notiJson == null || string.IsNullOrWhiteSpace(notiJson.text))
        {
            return;
        }

        try
        {
            NotiJsonRoot root = JsonUtility.FromJson<NotiJsonRoot>(notiJson.text);
            if (root != null)
            {
                if (root.entries != null)
                {
                    for (int i = 0; i < root.entries.Length; i++)
                    {
                        NotiEntry e = root.entries[i];
                        if (e == null || string.IsNullOrWhiteSpace(e.key))
                        {
                            continue;
                        }

                        entryTable[e.key] = e;
                    }
                }

                if (root.sequences != null)
                {
                    for (int i = 0; i < root.sequences.Length; i++)
                    {
                        NotiSequence s = root.sequences[i];
                        if (s == null || string.IsNullOrWhiteSpace(s.key))
                        {
                            continue;
                        }

                        sequenceTable[s.key] = s;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotiManager] Failed to parse noti JSON: {ex.Message}");
        }
    }

    private bool TryGetEntry(string key, out NotiEntry entry)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            entry = null;
            return false;
        }

        if (entryTable.Count == 0)
        {
            RebuildTable();
        }

        return entryTable.TryGetValue(key, out entry);
    }

    private bool TryGetSequence(string key, out NotiSequence sequence)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            sequence = null;
            return false;
        }

        if (entryTable.Count == 0 && sequenceTable.Count == 0)
        {
            RebuildTable();
        }

        return sequenceTable.TryGetValue(key, out sequence);
    }

    private float ResolveDuration(NotiEntry entry, float durationOverride)
    {
        if (durationOverride > 0f)
        {
            return durationOverride;
        }

        if (entry != null && entry.duration > 0f)
        {
            return entry.duration;
        }

        return defaultDuration;
    }

    private void RebuildColorTable()
    {
        colorTable.Clear();
        for (int i = 0; i < colorMappings.Count; i++)
        {
            NotiColorMapping mapping = colorMappings[i];
            if (string.IsNullOrWhiteSpace(mapping.key))
            {
                continue;
            }

            colorTable[mapping.key] = mapping.color;
        }
    }

    private void ResolveColor(NotiEntry entry, out bool hasColor, out Color color)
    {
        hasColor = false;
        color = Color.white;

        if (colorTable.Count == 0)
        {
            RebuildColorTable();
        }

        string key = entry != null && !string.IsNullOrWhiteSpace(entry.colorKey)
            ? entry.colorKey
            : defaultColorKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!colorTable.TryGetValue(key, out color))
        {
            return;
        }

        hasColor = true;
    }

    private void Enqueue(string text, float duration, float gapAfter, bool hasColor, Color color, Action onCompleted = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        queue.Enqueue(new PendingNoti(text, Mathf.Max(0f, duration), Mathf.Max(0f, gapAfter), hasColor, color, onCompleted));
        TryRunQueue();
    }

    private void TryRunQueue()
    {
        EnsureReferences();
        if (queueRoutine != null || notiCanvas == null || queue.Count == 0)
        {
            return;
        }

        queueRoutine = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        while (queue.Count > 0)
        {
            if (notiCanvas == null)
            {
                EnsureReferences();
                if (notiCanvas == null)
                {
                    break;
                }
            }

            PendingNoti item = queue.Dequeue();
            bool completed = false;
            if (item.hasColor)
            {
                notiCanvas.ShowNotification(item.text, item.duration, item.color, () => completed = true);
            }
            else
            {
                notiCanvas.ShowNotification(item.text, item.duration, null, () => completed = true);
            }
            while (!completed)
            {
                yield return null;
            }

            if (item.gapAfter > 0f)
            {
                float t = 0f;
                while (t < item.gapAfter)
                {
                    t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }

            item.onCompleted?.Invoke();
        }

        queueRoutine = null;
        if (queue.Count > 0)
        {
            TryRunQueue();
        }
    }

    private void EnsureReferences()
    {
        if (notiCanvas == null)
        {
            notiCanvas = FindFirstObjectByType<NotiCanvasController>();
        }

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TurnManager>();
        }

        if (stageManager == null)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }
    }

    private void BindTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.OnKingKillCountChanged -= HandleKingKillChanged;
        turnManager.OnKingKillCountChanged += HandleKingKillChanged;
        HandleKingKillChanged(turnManager.KingKillCount);

        turnManager.OnSummonCountChanged -= HandleSummonCountChanged;
        turnManager.OnSummonCountChanged += HandleSummonCountChanged;
        HandleSummonCountChanged(turnManager.SummonCount);

        turnManager.OnPhaseChanged -= HandlePhaseChanged;
        turnManager.OnPhaseChanged += HandlePhaseChanged;
        HandlePhaseChanged(turnManager.CurrentPhase);
    }

    private void BindStageManager()
    {
        if (stageManager == null)
        {
            return;
        }

        stageManager.OnWaveStarted -= HandleWaveStarted;
        stageManager.OnWaveStarted += HandleWaveStarted;
    }

    private void UnbindTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.OnKingKillCountChanged -= HandleKingKillChanged;
        turnManager.OnSummonCountChanged -= HandleSummonCountChanged;
        turnManager.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void UnbindStageManager()
    {
        if (stageManager == null)
        {
            return;
        }

        stageManager.OnWaveStarted -= HandleWaveStarted;
    }

    private void HandleKingKillChanged(int kingKill)
    {
        if (!autoShowFirstKill)
        {
            return;
        }

        if (kingKill <= 0)
        {
            firstKillShown = false;
            return;
        }

        if (firstKillShown)
        {
            return;
        }

        firstKillShown = true;
        if (firstKillUsesSequence && ShowSequenceByKey(firstKillSequenceKey))
        {
            return;
        }

        ShowByKey(firstKillKey);
    }

    private void HandleSummonCountChanged(int summonCount)
    {
        if (!autoShowFirstSummon)
        {
            return;
        }

        if (summonCount <= 0)
        {
            firstSummonShown = false;
            return;
        }

        if (firstSummonShown)
        {
            return;
        }

        firstSummonShown = true;
        ShowByKey(firstSummonKey);
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        if (turnManager == null || !autoShowSecondTurn)
        {
            return;
        }

        if (turnManager.TurnCount <= 1)
        {
            secondTurnShown = false;
            return;
        }

        if (phase != TurnPhase.PlayerTurn || turnManager.TurnCount != 2)
        {
            return;
        }

        if (secondTurnShown)
        {
            return;
        }

        secondTurnShown = true;
        ShowByKey(secondTurnKey);
    }

    private void HandleWaveStarted(int waveNumber, string _waveName)
    {
        if (!autoShowWave2Info)
        {
            return;
        }

        if (waveNumber < wave2Number)
        {
            wave2InfoShown = false;
            return;
        }

        if (waveNumber != wave2Number || wave2InfoShown)
        {
            return;
        }

        wave2InfoShown = true;
        ShowSequenceByKey(wave2SequenceKey);
    }
}
