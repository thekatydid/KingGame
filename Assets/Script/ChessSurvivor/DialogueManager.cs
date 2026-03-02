using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class DialogueManager : MonoBehaviour
{
    [Serializable]
    private class DialogueJsonWrapper
    {
        public DialogueData.DialogueLine[] lines;
    }

    [Header("Data")]
    [SerializeField] private DialogueData dialogueData;
    [SerializeField] private TextAsset dialogueJson;
    [SerializeField] private bool playOnStart = true;

    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private TurnUndoManager turnUndoManager;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform kingOverride;

    [Header("UI Toggle While Dialogue")]
    [SerializeField] private List<GameObject> gameplayUiRootsToHide = new();

    [Header("Dialogue UI")]
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private RectTransform dialoguePanelForScale;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private string speakerPrefix = "";
    [SerializeField] private float panelOpenDuration = 0.2f;
    [SerializeField] private float panelCloseDuration = 0.18f;
    [SerializeField] private float panelOvershootScaleX = 1.08f;
    [SerializeField] private float lineFadeDuration = 0.1f;

    [Header("Input")]
    [SerializeField] private KeyCode nextKey = KeyCode.Space;
    [SerializeField] private int nextMouseButton = 0;
    [SerializeField] private bool requireExtraConfirmAfterLastLine = true;

    [Header("Camera Closeup")]
    [SerializeField] private Vector3 closeupOffset = new(0f, 3.2f, -2.8f);
    [SerializeField] private float lookHeight = 0.9f;
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private AnimationCurve moveCurve = null;
    [SerializeField] private bool restoreCameraAfterDialogue = true;

    [Header("After Dialogue")]
    [SerializeField] private bool startGameplayAfterDialogue = true;
    [SerializeField] private NotiManager notiManager;
    [SerializeField] private string introNotiKey = "GameStart";

    private readonly List<(GameObject go, bool wasActive)> hiddenUiStates = new();
    private TopDownCameraController cachedFollow;
    private bool cachedFollowEnabled;
    private Vector3 cameraOriginPos;
    private Quaternion cameraOriginRot;
    private bool playing;
    private RectTransform dialoguePanelRect;

    public bool IsPlaying => playing;

    private void Awake()
    {
        EnsureReferences();
        EnsureDialogueUi();

        if (moveCurve == null || moveCurve.length == 0)
        {
            moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (playOnStart && HasAnyLine())
        {
            turnManager?.ForcePhase(TurnPhase.Busy);
        }
    }

    private void Start()
    {
        if (!playOnStart)
        {
            return;
        }

        PlayDialogue();
    }

    [ContextMenu("Play Dialogue")]
    public void PlayDialogue()
    {
        if (playing || !HasAnyLine())
        {
            if (startGameplayAfterDialogue)
            {
                turnManager?.ForcePhase(TurnPhase.PlayerTurn);
            }
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RunDialogue());
    }

    private IEnumerator RunDialogue()
    {
        playing = true;
        EnsureReferences();
        EnsureDialogueUi();
        turnManager?.ForcePhase(TurnPhase.Busy);

        CacheAndHideGameplayUi();
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.gameObject.SetActive(true);
            dialogueCanvasGroup.alpha = 0f;
            SetDialoguePanelScale(0f);
            SetDialogueTextAlpha(0f);
            yield return PlayDialogueWindowOpen();
        }

        yield return MoveCameraToKing();

        if (!TryGetDialogueLines(out DialogueData.DialogueLine[] lines) || lines == null || lines.Length == 0)
        {
            RestoreGameplayUi();
            yield return RestoreCameraState();
            if (startGameplayAfterDialogue)
            {
                turnManager?.ForcePhase(TurnPhase.PlayerTurn);
            }
            playing = false;
            yield break;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            yield return ShowLineWithFade(lines[i]);
            bool isLast = i == lines.Length - 1;
            // Avoid double-confirm bug on the final line when extra confirm is enabled.
            if (!isLast || !requireExtraConfirmAfterLastLine)
            {
                yield return WaitForNextInput();
            }
        }

        if (requireExtraConfirmAfterLastLine)
        {
            yield return WaitForNextInput();
        }

        if (dialogueCanvasGroup != null)
        {
            yield return FadeDialogueText(1f, 0f, lineFadeDuration);
            yield return PlayDialogueWindowClose();
            dialogueCanvasGroup.gameObject.SetActive(false);
        }

        RestoreGameplayUi();
        yield return RestoreCameraState();

        bool shouldStartGameplay = startGameplayAfterDialogue || playOnStart;
        if (shouldStartGameplay)
        {
            FindFirstObjectByType<StageManager>()?.PlayStageBgmNow(false);
            turnManager?.ForcePhase(TurnPhase.PlayerTurn);
            notiManager?.ShowByKey(introNotiKey);
        }
        else
        {
            turnManager?.ForcePhase(TurnPhase.Busy);
        }

        turnUndoManager?.MarkPostDialogueCheckpoint();

        playing = false;
    }

    private void ApplyLine(DialogueData.DialogueLine line)
    {
        if (speakerText != null)
        {
            if (string.IsNullOrWhiteSpace(line.speaker))
            {
                speakerText.text = string.Empty;
            }
            else
            {
                speakerText.text = $"{speakerPrefix}{line.speaker}";
            }
        }

        if (bodyText != null)
        {
            bodyText.text = line.text ?? string.Empty;
        }
    }

    private IEnumerator WaitForNextInput()
    {
        // Consume any input still held/latched from the previous line this frame.
        while (IsNextInputPressedThisFrame())
        {
            yield return null;
        }

        while (true)
        {
            if (IsNextInputPressedThisFrame())
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool IsNextInputPressedThisFrame()
    {
        bool pressed = Input.GetKeyDown(nextKey) ||
                       Input.GetMouseButtonDown(nextMouseButton) ||
                       Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetMouseButtonDown(0);

#if ENABLE_INPUT_SYSTEM
        if (!pressed)
        {
            if (Keyboard.current != null)
            {
                pressed = Keyboard.current.spaceKey.wasPressedThisFrame ||
                          Keyboard.current.enterKey.wasPressedThisFrame ||
                          Keyboard.current.numpadEnterKey.wasPressedThisFrame;
            }

            if (!pressed && Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
            }
        }
#endif

        return pressed;
    }

    private IEnumerator ShowLineWithFade(DialogueData.DialogueLine line)
    {
        ApplyLine(line);
        yield return FadeDialogueText(0f, 1f, lineFadeDuration);
    }

    private IEnumerator FadeDialogueText(float from, float to, float duration)
    {
        SetDialogueTextAlpha(from);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            SetDialogueTextAlpha(Mathf.LerpUnclamped(from, to, n));
            yield return null;
        }

        SetDialogueTextAlpha(to);
    }

    private IEnumerator PlayDialogueWindowOpen()
    {
        float t = 0f;
        while (t < panelOpenDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = panelOpenDuration <= 0f ? 1f : Mathf.Clamp01(t / panelOpenDuration);
            float eased = n < 0.8f
                ? Mathf.LerpUnclamped(0f, panelOvershootScaleX, n / 0.8f)
                : Mathf.LerpUnclamped(panelOvershootScaleX, 1f, (n - 0.8f) / 0.2f);
            SetDialoguePanelScale(eased);
            if (dialogueCanvasGroup != null)
            {
                dialogueCanvasGroup.alpha = n;
            }

            yield return null;
        }

        SetDialoguePanelScale(1f);
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = 1f;
        }
    }

    private IEnumerator PlayDialogueWindowClose()
    {
        float t = 0f;
        while (t < panelCloseDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = panelCloseDuration <= 0f ? 1f : Mathf.Clamp01(t / panelCloseDuration);
            SetDialoguePanelScale(Mathf.LerpUnclamped(1f, 0f, n));
            if (dialogueCanvasGroup != null)
            {
                dialogueCanvasGroup.alpha = 1f - n;
            }

            yield return null;
        }

        SetDialoguePanelScale(0f);
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = 0f;
        }
    }

    private void SetDialoguePanelScale(float x)
    {
        if (dialoguePanelRect == null && dialoguePanelForScale != null)
        {
            dialoguePanelRect = dialoguePanelForScale;
        }

        if (dialoguePanelRect == null && bodyText != null && bodyText.transform.parent is RectTransform parentRt)
        {
            dialoguePanelRect = parentRt;
        }

        if (dialoguePanelRect == null && dialogueCanvasGroup != null)
        {
            dialoguePanelRect = dialogueCanvasGroup.GetComponent<RectTransform>();
        }

        if (dialoguePanelRect == null)
        {
            return;
        }

        dialoguePanelRect.localScale = new Vector3(Mathf.Max(0f, x), 1f, 1f);
    }

    private void SetDialogueTextAlpha(float alpha)
    {
        float clamped = Mathf.Clamp01(alpha);
        if (speakerText != null)
        {
            speakerText.alpha = clamped;
        }

        if (bodyText != null)
        {
            bodyText.alpha = clamped;
        }
    }

    private IEnumerator MoveCameraToKing()
    {
        if (targetCamera == null)
        {
            yield break;
        }

        Transform king = ResolveKingTransform();
        if (king == null)
        {
            yield break;
        }

        Transform camTr = targetCamera.transform;
        cameraOriginPos = camTr.position;
        cameraOriginRot = camTr.rotation;

        cachedFollow = targetCamera.GetComponent<TopDownCameraController>();
        cachedFollowEnabled = cachedFollow != null && cachedFollow.enabled;
        if (cachedFollowEnabled)
        {
            cachedFollow.enabled = false;
        }

        Vector3 focus = king.position + Vector3.up * lookHeight;
        Vector3 targetPos = focus + closeupOffset;
        Quaternion targetRot = Quaternion.LookRotation((focus - targetPos).normalized, Vector3.up);

        yield return LerpCamera(camTr, camTr.position, camTr.rotation, targetPos, targetRot, moveDuration);
    }

    private IEnumerator RestoreCameraState()
    {
        if (targetCamera == null)
        {
            yield break;
        }

        Transform camTr = targetCamera.transform;
        if (restoreCameraAfterDialogue)
        {
            yield return LerpCamera(camTr, camTr.position, camTr.rotation, cameraOriginPos, cameraOriginRot, moveDuration);
        }

        if (cachedFollow != null && cachedFollowEnabled)
        {
            cachedFollow.enabled = true;
        }

        cachedFollow = null;
        cachedFollowEnabled = false;
    }

    private IEnumerator LerpCamera(Transform cam, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float eased = moveCurve != null ? moveCurve.Evaluate(n) : n;
            cam.position = Vector3.LerpUnclamped(fromPos, toPos, eased);
            cam.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, eased);
            yield return null;
        }

        cam.position = toPos;
        cam.rotation = toRot;
    }

    private void CacheAndHideGameplayUi()
    {
        hiddenUiStates.Clear();
        for (int i = 0; i < gameplayUiRootsToHide.Count; i++)
        {
            GameObject go = gameplayUiRootsToHide[i];
            if (go == null)
            {
                continue;
            }

            bool active = go.activeSelf;
            hiddenUiStates.Add((go, active));
            if (active)
            {
                go.SetActive(false);
            }
        }
    }

    private void RestoreGameplayUi()
    {
        for (int i = 0; i < hiddenUiStates.Count; i++)
        {
            (GameObject go, bool wasActive) = hiddenUiStates[i];
            if (go != null)
            {
                go.SetActive(wasActive);
            }
        }

        hiddenUiStates.Clear();
    }

    private bool HasAnyLine()
    {
        return TryGetDialogueLines(out DialogueData.DialogueLine[] lines) && lines != null && lines.Length > 0;
    }

    private bool TryGetDialogueLines(out DialogueData.DialogueLine[] lines)
    {
        lines = null;

        if (dialogueData != null && dialogueData.TryGetLines(out lines) && lines != null && lines.Length > 0)
        {
            return true;
        }

        if (dialogueJson == null || string.IsNullOrWhiteSpace(dialogueJson.text))
        {
            return false;
        }

        try
        {
            DialogueJsonWrapper wrapper = JsonUtility.FromJson<DialogueJsonWrapper>(dialogueJson.text);
            lines = wrapper != null ? wrapper.lines : null;
            return lines != null && lines.Length > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DialogueManager] JSON parse failed: {ex.Message}");
            lines = null;
            return false;
        }
    }

    private Transform ResolveKingTransform()
    {
        if (kingOverride != null)
        {
            return kingOverride;
        }

        if (turnManager != null && turnManager.KingPiece != null)
        {
            return turnManager.KingPiece.transform;
        }

        ChessPiece[] pieces = FindObjectsByType<ChessPiece>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            ChessPiece piece = pieces[i];
            if (piece != null && piece.Team == Team.Ally && piece.PieceType == PieceType.King)
            {
                return piece.transform;
            }
        }

        return null;
    }

    private void EnsureReferences()
    {
        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TurnManager>();
        }

        if (turnUndoManager == null)
        {
            turnUndoManager = FindFirstObjectByType<TurnUndoManager>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (notiManager == null)
        {
            notiManager = FindFirstObjectByType<NotiManager>();
        }
    }

    private void EnsureDialogueUi()
    {
        if (dialogueCanvasGroup != null && bodyText != null)
        {
            if (dialoguePanelForScale != null)
            {
                dialoguePanelRect = dialoguePanelForScale;
            }
            else if (bodyText.transform.parent is RectTransform parentRt)
            {
                dialoguePanelRect = parentRt;
            }
            else
            {
                dialoguePanelRect = dialogueCanvasGroup.GetComponent<RectTransform>();
            }
            return;
        }

        GameObject canvasGo = new("DialogueCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.sizeDelta = new Vector2(0f, 220f);
        panelRt.anchoredPosition = Vector2.zero;

        GameObject speakerGo = new("Speaker");
        speakerGo.transform.SetParent(panel.transform, false);
        speakerText = speakerGo.AddComponent<TextMeshProUGUI>();
        speakerText.fontSize = 28;
        speakerText.color = Color.white;
        speakerText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform speakerRt = speakerGo.GetComponent<RectTransform>();
        speakerRt.anchorMin = new Vector2(0f, 0f);
        speakerRt.anchorMax = new Vector2(1f, 1f);
        speakerRt.offsetMin = new Vector2(26f, 130f);
        speakerRt.offsetMax = new Vector2(-26f, -20f);

        GameObject bodyGo = new("Body");
        bodyGo.transform.SetParent(panel.transform, false);
        bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 30;
        bodyText.color = Color.white;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(26f, 20f);
        bodyRt.offsetMax = new Vector2(-26f, -62f);

        dialogueCanvasGroup = panel.AddComponent<CanvasGroup>();
        dialogueCanvasGroup.alpha = 0f;
        dialogueCanvasGroup.gameObject.SetActive(false);
        dialoguePanelForScale = panel.GetComponent<RectTransform>();
        dialoguePanelRect = dialoguePanelForScale;
    }
}
