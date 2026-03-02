using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class KingGaugeUIController : MonoBehaviour
{
    private const string GaugeNamePrefix = "Gauge_";

    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private bool autoFindTurnManager = true;
    [SerializeField] private RectTransform gaugeBackground;
    [SerializeField] private RectTransform segmentContainer;
    [SerializeField] private HorizontalLayoutGroup segmentLayout;
    [SerializeField] private GameObject segmentPrefab;

    [Header("Gauge Layout")]
    [SerializeField] [Min(1)] private int segmentCount = 5;
    [SerializeField] private bool useSegmentNativeSize = true;
    [SerializeField] [Min(1f)] private float segmentWidth = 38f;
    [SerializeField] [Min(1f)] private float segmentHeight = 22f;
    [SerializeField] [Min(0f)] private float leftPadding = 24f;
    [SerializeField] [Min(0f)] private float rightPadding = 24f;
    [SerializeField] [Min(0f)] private float spacing = 8f;

    [Header("Gauge Colors")]
    [SerializeField] private Color filledColor = new(0.31f, 0.88f, 1f, 1f);
    [SerializeField] private Color emptyColor = new(0.22f, 0.35f, 0.48f, 0.25f);

    [Header("Events")]
    [SerializeField] private UnityEvent onGaugeFull;
    [SerializeField] private UnityEvent onKingButtonPressed;
    [Header("Full Gauge FX")]
    [SerializeField] private RectTransform kingIcon;
    [SerializeField] private float iconPulseDuration = 0.16f;
    [SerializeField] private float iconPauseDuration = 0.05f;
    [SerializeField] private Vector3 iconSquashScale = new(1.18f, 0.86f, 1f);
    [SerializeField] private Vector3 iconStretchScale = new(0.9f, 1.14f, 1f);

    [Header("Kill Popup")]
    [SerializeField] private RectTransform killBg;
    [SerializeField] private TMP_Text killTxt;
    [SerializeField] private float killBgHiddenWidth = 0f;
    [SerializeField] private float killBgTargetWidth = 600f;
    [SerializeField] private float killBgOvershootWidth = 36f;
    [SerializeField] private float killExpandDuration = 0.14f;
    [SerializeField] private float killSettleDuration = 0.1f;
    [SerializeField] private float killVisibleDuration = 2f;
    [SerializeField] private float killFadeDuration = 0.2f;
    [SerializeField] private bool killUiUseUnscaledTime = true;
    [SerializeField] private bool keepKillUiVisible = true;
    [Header("Turn Text")]
    [SerializeField] private TMP_Text turnPhaseTxt;
    [SerializeField] private string allyTurnLabel = "아군 턴";
    [SerializeField] private string enemyTurnLabel = "적군 턴";
    [SerializeField] [Min(0.05f)] private float turnDotInterval = 0.35f;
    [SerializeField] private bool turnTextUseUnscaledTime = true;
    [Header("Stage Group")]
    [SerializeField] private StageManager stageManager;
    [SerializeField] private bool autoFindStageManager = true;
    [SerializeField] private RectTransform stageGroup;
    [SerializeField] private TMP_Text stageTxt;
    [SerializeField] private TMP_Text waveTxt;
    [SerializeField] private TMP_Text nextWaveTxt;
    [SerializeField] private string nextWaveFormat = "다음 웨이브까지 {0}턴";
    [SerializeField] private string noNextWaveText = "마지막 웨이브";
    [SerializeField] [Min(0.05f)] private float stageUiRefreshInterval = 0.2f;
    [Header("Noti")]
    [SerializeField] private bool showFirstSkillReadyNoti = true;
    [SerializeField] private string firstSkillReadyNotiKey = "FirstKingSkillReady";

    private readonly List<Image> segments = new();
    private bool fullTriggered;
    private int cachedKillCount;
    private int cachedChargeCount;
    private bool turnManagerBound;
    private Coroutine iconPulseRoutine;
    private Vector3 kingIconBaseScale = Vector3.one;
    private bool kingIconScaleCached;
    private Button kingIconButton;
    private KingIconHoverOutline kingIconHoverOutline;
    private bool disableFullGaugePulseUntilGaugeDrops;
    private CanvasGroup killCanvasGroup;
    private Coroutine killPopupRoutine;
    private string killTextSuffix = " <size=40><color=#46d7ee>Kill</color></size>";
    private Coroutine turnPhaseRoutine;
    private bool firstSkillReadyNotiShown;
    private float stageUiRefreshTimer;

    private void Awake()
    {
        EnsureReferences();
        CacheKingIconBaseScale();
        CacheKillTextSuffix();
        HideNextWaveTextImmediate();
        UpdateStageTexts();
        UpdateKillText(cachedKillCount);
        HideKillPopupImmediate();
        int existingCount = CountExistingSegments();
        if (existingCount <= 0)
        {
            RebuildSegments();
        }
        else
        {
            TryBindExistingSegments();
        }

        RefreshGauge(cachedKillCount, invokeFullEvent: false);
    }

    private void OnEnable()
    {
        EnsureReferences();
        BindTurnManager();
        BindKingIconButton();
    }

    private void OnDisable()
    {
        UnbindTurnManager();
        UnbindKingIconButton();

        StopFullGaugeVisuals();
        StopKillPopupRoutine();
        StopTurnPhaseRoutine();
    }

    private void Update()
    {
        if (!turnManagerBound && autoFindTurnManager)
        {
            BindTurnManager();
        }

        if (stageManager == null && autoFindStageManager)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        stageUiRefreshTimer -= Time.unscaledDeltaTime;
        if (stageUiRefreshTimer <= 0f)
        {
            stageUiRefreshTimer = Mathf.Max(0.05f, stageUiRefreshInterval);
            UpdateStageTexts();
        }

        UpdateStageHoverState();
    }

    private void OnValidate()
    {
        if (segmentCount < 1)
        {
            segmentCount = 1;
        }

        if (!Application.isPlaying)
        {
            EnsureReferences();
            TryBindExistingSegments();
            ResizeBackgroundToSegments();
            HideNextWaveTextImmediate();
            UpdateStageTexts();
        }
    }

    private void OnDestroy()
    {
        CleanupGaugeChildren(immediate: true);
    }

    [ContextMenu("Rebuild King Gauge")]
    public void RebuildSegments()
    {
        if (segmentContainer == null || segmentPrefab == null)
        {
            return;
        }

        if (segmentLayout == null)
        {
            segmentLayout = segmentContainer.GetComponent<HorizontalLayoutGroup>();
        }

        if (segmentLayout != null)
        {
            segmentLayout.childAlignment = TextAnchor.MiddleLeft;
            segmentLayout.childControlWidth = false;
            segmentLayout.childControlHeight = false;
            segmentLayout.childForceExpandWidth = false;
            segmentLayout.childForceExpandHeight = false;
            segmentLayout.spacing = spacing;
            segmentLayout.padding = new RectOffset(
                Mathf.RoundToInt(leftPadding),
                Mathf.RoundToInt(rightPadding),
                0,
                0);
        }

        CleanupGaugeChildren(immediate: !Application.isPlaying);

        segments.Clear();
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject go = Instantiate(segmentPrefab, segmentContainer);
            go.name = $"{GaugeNamePrefix}{i:D2}";
            RectTransform rt = go.transform as RectTransform;

            Image image = go.GetComponent<Image>();
            if (image != null)
            {
                if (useSegmentNativeSize)
                {
                    image.SetNativeSize();
                }

                segments.Add(image);
            }

            LayoutElement le = go.GetComponent<LayoutElement>();
            if (useSegmentNativeSize)
            {
                if (le != null)
                {
                    le.preferredWidth = -1f;
                    le.preferredHeight = -1f;
                    le.flexibleWidth = 0f;
                    le.flexibleHeight = 0f;
                }
            }
            else
            {
                if (rt != null)
                {
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, segmentWidth);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, segmentHeight);
                }

                if (le == null)
                {
                    le = go.AddComponent<LayoutElement>();
                }

                le.preferredWidth = segmentWidth;
                le.preferredHeight = segmentHeight;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;
            }
        }

        ResizeBackgroundToSegments();
    }

    private void CleanupGaugeChildren(bool immediate)
    {
        if (segmentContainer == null)
        {
            return;
        }

        for (int i = segmentContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = segmentContainer.GetChild(i);
            if (!child.name.StartsWith(GaugeNamePrefix))
            {
                continue;
            }

            if (immediate)
            {
                DestroyImmediate(child.gameObject);
            }
            else
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void EnsureReferences()
    {
        if (segmentLayout == null && segmentContainer != null)
        {
            segmentLayout = segmentContainer.GetComponent<HorizontalLayoutGroup>();
        }

        if (kingIcon != null && kingIconButton == null)
        {
            kingIconButton = kingIcon.GetComponent<Button>();
            if (kingIconButton == null)
            {
                kingIconButton = kingIcon.gameObject.AddComponent<Button>();
            }

            Image iconImage = kingIcon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.raycastTarget = true;
            }

            if (kingIcon.GetComponent<KingIconHoverOutline>() == null)
            {
                kingIcon.gameObject.AddComponent<KingIconHoverOutline>();
            }
            kingIconHoverOutline = kingIcon.GetComponent<KingIconHoverOutline>();

            Graphic g = kingIcon.GetComponent<Graphic>();
            if (g != null)
            {
                g.raycastTarget = true;
            }
        }

        if (killBg == null)
        {
            Transform found = transform.Find("KillBG");
            if (found is RectTransform rt)
            {
                killBg = rt;
            }
        }

        if (killTxt == null)
        {
            Transform found = transform.Find("KillBG/KillTxt");
            if (found != null)
            {
                killTxt = found.GetComponent<TMP_Text>();
            }
        }

        if (killBg != null && killCanvasGroup == null)
        {
            killCanvasGroup = killBg.GetComponent<CanvasGroup>();
            if (killCanvasGroup == null)
            {
                killCanvasGroup = killBg.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (turnPhaseTxt == null)
        {
            Transform found = transform.Find("TurnGroup/TurnTxt");
            if (found != null)
            {
                turnPhaseTxt = found.GetComponent<TMP_Text>();
            }
        }

        if (stageGroup == null)
        {
            Transform found = transform.Find("StageGroup");
            if (found is RectTransform rt)
            {
                stageGroup = rt;
            }
        }

        if (stageTxt == null)
        {
            Transform found = transform.Find("StageGroup/StageTxt");
            if (found != null)
            {
                stageTxt = found.GetComponent<TMP_Text>();
            }
        }

        if (waveTxt == null)
        {
            Transform found = transform.Find("StageGroup/WaveTxt");
            if (found != null)
            {
                waveTxt = found.GetComponent<TMP_Text>();
            }
        }

        if (nextWaveTxt == null)
        {
            Transform found = transform.Find("StageGroup/NextWaveTxt");
            if (found != null)
            {
                nextWaveTxt = found.GetComponent<TMP_Text>();
            }
        }
    }

    private bool TryBindExistingSegments()
    {
        if (segmentContainer == null)
        {
            return false;
        }

        segments.Clear();
        for (int i = 0; i < segmentContainer.childCount; i++)
        {
            Transform child = segmentContainer.GetChild(i);
            if (!child.name.StartsWith(GaugeNamePrefix))
            {
                continue;
            }

            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                segments.Add(image);
            }
        }

        if (segments.Count == 0)
        {
            return false;
        }

        ResizeBackgroundToSegments();
        return true;
    }

    private int CountExistingSegments()
    {
        if (segmentContainer == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < segmentContainer.childCount; i++)
        {
            if (segmentContainer.GetChild(i).name.StartsWith(GaugeNamePrefix))
            {
                count++;
            }
        }

        return count;
    }

    private void BindTurnManager()
    {
        if (turnManager == null && autoFindTurnManager)
        {
            turnManager = FindFirstObjectByType<TurnManager>();
        }

        if (turnManager == null)
        {
            turnManagerBound = false;
            return;
        }

        turnManager.OnKingKillCountChanged -= HandleKingKillCountChanged;
        turnManager.OnKingKillCountChanged += HandleKingKillCountChanged;
        turnManager.OnKingSkillChargeChanged -= HandleKingSkillChargeChanged;
        turnManager.OnKingSkillChargeChanged += HandleKingSkillChargeChanged;
        turnManager.OnPhaseChanged -= HandleTurnPhaseChanged;
        turnManager.OnPhaseChanged += HandleTurnPhaseChanged;
        turnManagerBound = true;
        cachedKillCount = turnManager.KingKillCount;
        cachedChargeCount = turnManager.KingSkillChargeCount;
        UpdateKillText(cachedKillCount);
        RefreshGauge(cachedChargeCount, invokeFullEvent: false);
        HandleTurnPhaseChanged(turnManager.CurrentPhase);
    }

    private void UnbindTurnManager()
    {
        if (turnManager != null)
        {
            turnManager.OnKingKillCountChanged -= HandleKingKillCountChanged;
            turnManager.OnKingSkillChargeChanged -= HandleKingSkillChargeChanged;
            turnManager.OnPhaseChanged -= HandleTurnPhaseChanged;
        }

        turnManagerBound = false;
    }

    private void HandleKingKillCountChanged(int killCount)
    {
        int previous = cachedKillCount;
        cachedKillCount = killCount;
        UpdateKillText(cachedKillCount);
        if (killCount > previous)
        {
            ShowKillPopup();
        }
    }

    private void HandleKingSkillChargeChanged(int chargeCount)
    {
        cachedChargeCount = chargeCount;
        RefreshGauge(cachedChargeCount, invokeFullEvent: true);
    }

    private void HandleTurnPhaseChanged(TurnPhase _)
    {
        UpdateKingButtonStateByPhase();
        UpdateTurnPhaseText();
    }

    private void HandleKingButtonClick()
    {
        if (turnManager != null)
        {
            bool used = turnManager.TryUseKingSkill(segmentCount);
            if (!used)
            {
                return;
            }
        }

        disableFullGaugePulseUntilGaugeDrops = true;
        StopFullGaugeVisuals();
        onKingButtonPressed?.Invoke();
    }

    private void BindKingIconButton()
    {
        EnsureReferences();
        if (kingIconButton == null)
        {
            return;
        }

        kingIconButton.onClick.RemoveListener(HandleKingButtonClick);
        kingIconButton.onClick.AddListener(HandleKingButtonClick);
    }

    private void UnbindKingIconButton()
    {
        if (kingIconButton == null)
        {
            return;
        }

        kingIconButton.onClick.RemoveListener(HandleKingButtonClick);
    }

    private void RefreshGauge(int killCount, bool invokeFullEvent)
    {
        int fillCount = Mathf.Clamp(killCount, 0, segmentCount);
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].color = i < fillCount ? filledColor : emptyColor;
        }

        bool isFull = fillCount >= segmentCount;
        ApplyKingButtonInteractable(isFull && IsPlayerTurnPhase());

        if (isFull && !fullTriggered && invokeFullEvent)
        {
            if (!disableFullGaugePulseUntilGaugeDrops)
            {
                StartFullGaugeVisuals();
            }
            onGaugeFull?.Invoke();
            if (showFirstSkillReadyNoti && !firstSkillReadyNotiShown)
            {
                if (NotiManager.Instance != null && NotiManager.Instance.ShowByKey(firstSkillReadyNotiKey))
                {
                    firstSkillReadyNotiShown = true;
                }
            }
            fullTriggered = true;
        }
        else if (!isFull)
        {
            fullTriggered = false;
            disableFullGaugePulseUntilGaugeDrops = false;
            StopFullGaugeVisuals();
        }
    }

    private void UpdateKingButtonStateByPhase()
    {
        bool isFull = Mathf.Clamp(cachedChargeCount, 0, segmentCount) >= segmentCount;
        ApplyKingButtonInteractable(isFull && IsPlayerTurnPhase());
    }

    private void ApplyKingButtonInteractable(bool interactable)
    {
        if (kingIconButton != null)
        {
            kingIconButton.interactable = interactable;
        }

        if (kingIconHoverOutline != null)
        {
            kingIconHoverOutline.SetHoverEnabled(interactable);
        }
    }

    private bool IsPlayerTurnPhase()
    {
        return turnManager != null && turnManager.CurrentPhase == TurnPhase.PlayerTurn;
    }

    private void UpdateTurnPhaseText()
    {
        if (turnPhaseTxt == null)
        {
            return;
        }

        TurnPhase phase = turnManager != null ? turnManager.CurrentPhase : TurnPhase.PlayerTurn;
        switch (phase)
        {
            case TurnPhase.AllyAutoTurn:
                StartTurnPhaseRoutine(allyTurnLabel);
                break;
            case TurnPhase.EnemyAutoTurn:
                StartTurnPhaseRoutine(enemyTurnLabel);
                break;
            default:
                StopTurnPhaseRoutine();
                turnPhaseTxt.text = string.Empty;
                break;
        }
    }

    private void UpdateStageTexts()
    {
        if (stageManager == null && autoFindStageManager)
        {
            stageManager = FindFirstObjectByType<StageManager>();
        }

        if (stageManager == null)
        {
            return;
        }

        if (stageTxt != null)
        {
            stageTxt.text = stageManager.GetStageDisplayName();
        }

        if (waveTxt != null)
        {
            waveTxt.text = stageManager.GetCurrentWaveDisplayName();
        }

        if (nextWaveTxt != null)
        {
            if (stageManager.TryGetNextWaveInfo(out string _, out int turnsUntil))
            {
                nextWaveTxt.text = string.Format(nextWaveFormat, turnsUntil);
            }
            else
            {
                nextWaveTxt.text = noNextWaveText;
            }
        }
    }

    private void UpdateStageHoverState()
    {
        if (stageGroup == null || nextWaveTxt == null)
        {
            return;
        }

        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(stageGroup, GetPointerPosition(), null);
        if (nextWaveTxt.gameObject.activeSelf != hovered)
        {
            nextWaveTxt.gameObject.SetActive(hovered);
        }
    }

    private void HideNextWaveTextImmediate()
    {
        if (nextWaveTxt != null)
        {
            nextWaveTxt.gameObject.SetActive(false);
        }
    }

    private void StartTurnPhaseRoutine(string label)
    {
        if (turnPhaseTxt == null || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        StopTurnPhaseRoutine();
        turnPhaseRoutine = StartCoroutine(AnimateTurnPhaseDots(label));
    }

    private void StopTurnPhaseRoutine()
    {
        if (turnPhaseRoutine == null)
        {
            return;
        }

        StopCoroutine(turnPhaseRoutine);
        turnPhaseRoutine = null;
    }

    private IEnumerator AnimateTurnPhaseDots(string label)
    {
        int dots = 0;
        float interval = Mathf.Max(0.05f, turnDotInterval);
        while (true)
        {
            dots = (dots % 3) + 1;
            if (turnPhaseTxt != null)
            {
                turnPhaseTxt.text = label + new string('.', dots);
            }

            float elapsed = 0f;
            while (elapsed < interval)
            {
                elapsed += turnTextUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }
    }

    private void ResizeBackgroundToSegments()
    {
        if (gaugeBackground == null)
        {
            return;
        }

        float totalSegmentWidth = 0f;
        int validCount = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] == null)
            {
                continue;
            }

            RectTransform rt = segments[i].rectTransform;
            float width = LayoutUtility.GetPreferredWidth(rt);
            if (width <= 0f)
            {
                width = rt.rect.width;
            }

            if (width <= 0f)
            {
                width = segmentWidth;
            }

            totalSegmentWidth += width;
            validCount++;
        }

        if (validCount == 0)
        {
            validCount = segmentCount;
            totalSegmentWidth = segmentWidth * segmentCount;
        }

        int padLeft = segmentLayout != null ? segmentLayout.padding.left : Mathf.RoundToInt(leftPadding);
        int padRight = segmentLayout != null ? segmentLayout.padding.right : Mathf.RoundToInt(rightPadding);
        float gap = segmentLayout != null ? segmentLayout.spacing : spacing;
        float widthTotal = padLeft + padRight + totalSegmentWidth + (gap * Mathf.Max(0, validCount - 1));
        gaugeBackground.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, widthTotal);
        LayoutElement bgLayout = gaugeBackground.GetComponent<LayoutElement>();
        if (bgLayout != null)
        {
            bgLayout.preferredWidth = widthTotal;
        }
    }

    private void StartFullGaugeVisuals()
    {
        CacheKingIconBaseScale();

        if (kingIcon != null && iconPulseRoutine == null)
        {
            iconPulseRoutine = StartCoroutine(LoopIconPulse());
        }
    }

    private void StopFullGaugeVisuals()
    {
        if (iconPulseRoutine != null)
        {
            StopCoroutine(iconPulseRoutine);
            iconPulseRoutine = null;
        }

        if (kingIcon != null && kingIconScaleCached)
        {
            kingIcon.localScale = kingIconBaseScale;
        }
    }

    private IEnumerator LoopIconPulse()
    {
        if (kingIcon == null)
        {
            yield break;
        }

        while (true)
        {
            Vector3 squash = Vector3.Scale(kingIconBaseScale, iconSquashScale);
            Vector3 stretch = Vector3.Scale(kingIconBaseScale, iconStretchScale);

            yield return LerpIconScale(kingIconBaseScale, squash, iconPulseDuration);
            yield return LerpIconScale(squash, stretch, iconPulseDuration);
            yield return LerpIconScale(stretch, kingIconBaseScale, iconPulseDuration);
            if (iconPauseDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(iconPauseDuration);
            }
        }
    }

    private IEnumerator LerpIconScale(Vector3 from, Vector3 to, float duration)
    {
        if (kingIcon == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (kingIcon == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            kingIcon.localScale = Vector3.LerpUnclamped(from, to, n);
            yield return null;
        }

        if (kingIcon != null)
        {
            kingIcon.localScale = to;
        }
    }

    private void CacheKingIconBaseScale()
    {
        if (kingIcon == null)
        {
            return;
        }

        kingIconBaseScale = kingIcon.localScale;
        kingIconScaleCached = true;
    }

    private void CacheKillTextSuffix()
    {
        if (killTxt == null)
        {
            return;
        }

        string raw = killTxt.text ?? string.Empty;
        int firstSpace = raw.IndexOf(' ');
        if (firstSpace >= 0 && firstSpace < raw.Length - 1)
        {
            killTextSuffix = raw.Substring(firstSpace);
        }
    }

    private void UpdateKillText(int killCount)
    {
        if (killTxt == null)
        {
            return;
        }

        killTxt.text = $"{Mathf.Max(0, killCount)}{killTextSuffix}";
    }

    private void ShowKillPopup()
    {
        if (killBg == null)
        {
            return;
        }

        if (killPopupRoutine != null)
        {
            StopCoroutine(killPopupRoutine);
            killPopupRoutine = null;
        }

        killPopupRoutine = StartCoroutine(PlayKillPopupRoutine());
    }

    private void StopKillPopupRoutine()
    {
        if (killPopupRoutine == null)
        {
            return;
        }

        StopCoroutine(killPopupRoutine);
        killPopupRoutine = null;
    }

    private void HideKillPopupImmediate()
    {
        if (killBg == null)
        {
            return;
        }

        float width = keepKillUiVisible ? Mathf.Max(0f, killBgTargetWidth) : killBgHiddenWidth;
        SetKillBgWidth(width);
        if (killCanvasGroup != null)
        {
            killCanvasGroup.alpha = keepKillUiVisible ? 1f : 0f;
        }
    }

    private IEnumerator PlayKillPopupRoutine()
    {
        EnsureReferences();
        if (killBg == null)
        {
            yield break;
        }

        if (killCanvasGroup != null)
        {
            killCanvasGroup.alpha = 1f;
        }

        float target = Mathf.Max(0f, killBgTargetWidth);
        float overshoot = Mathf.Max(target, target + Mathf.Max(0f, killBgOvershootWidth));
        float from = keepKillUiVisible ? Mathf.Max(0f, killBg.rect.width) : killBgHiddenWidth;
        SetKillBgWidth(from);

        yield return LerpKillBgWidth(from, overshoot, killExpandDuration);
        yield return LerpKillBgWidth(overshoot, target, killSettleDuration);

        if (keepKillUiVisible)
        {
            if (killCanvasGroup != null)
            {
                killCanvasGroup.alpha = 1f;
            }

            killPopupRoutine = null;
            yield break;
        }

        yield return Wait(killVisibleDuration);

        if (killCanvasGroup != null)
        {
            float t = 0f;
            float duration = Mathf.Max(0.01f, killFadeDuration);
            while (t < duration)
            {
                t += DeltaTime();
                killCanvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / duration));
                yield return null;
            }

            killCanvasGroup.alpha = 0f;
        }

        SetKillBgWidth(killBgHiddenWidth);
        killPopupRoutine = null;
    }

    private IEnumerator LerpKillBgWidth(float from, float to, float duration)
    {
        float t = 0f;
        float d = Mathf.Max(0.0001f, duration);
        while (t < d)
        {
            t += DeltaTime();
            float n = Mathf.Clamp01(t / d);
            SetKillBgWidth(Mathf.LerpUnclamped(from, to, n));
            yield return null;
        }

        SetKillBgWidth(to);
    }

    private IEnumerator Wait(float duration)
    {
        float t = 0f;
        float d = Mathf.Max(0f, duration);
        while (t < d)
        {
            t += DeltaTime();
            yield return null;
        }
    }

    private float DeltaTime()
    {
        return killUiUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void SetKillBgWidth(float width)
    {
        if (killBg == null)
        {
            return;
        }

        killBg.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, width));
        LayoutElement layout = killBg.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = Mathf.Max(0f, width);
        }
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
