using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class CinematicDirector : MonoBehaviour
{
    public const int FirstKingSkillCinematicId = 1;

    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private ChessBoardManager board;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform kingOverride;

    [Header("UI")]
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private RectTransform dialoguePanelForScale;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private string speakerPrefix = "";
    [SerializeField] private List<GameObject> gameplayUiRootsToHide = new();

    [Header("Dialogue Animation")]
    [SerializeField] private float panelOpenDuration = 0.2f;
    [SerializeField] private float panelCloseDuration = 0.18f;
    [SerializeField] private float panelOvershootScaleX = 1.08f;
    [SerializeField] private float lineFadeDuration = 0.1f;
    [SerializeField] private bool waitInputOnLastLine = true;

    [Header("First King Skill")]
    [SerializeField] private int firstKingSkillId = FirstKingSkillCinematicId;
    [SerializeField] private bool firstKingSkillPlayOncePerRun = true;
    [SerializeField] private bool firstKingSkillRequireTitleEntry = true;
    [SerializeField] private DialogueData firstKingSkillDialogue;
    [SerializeField] private Vector3 firstZoomOffset = new(0f, 3.0f, -2.8f);
    [SerializeField] private Vector3 secondZoomOffset = new(0f, 2.1f, -2.0f);
    [SerializeField] private float lookHeight = 0.9f;
    [SerializeField] private float moveDuration = 0.38f;
    [SerializeField] private AnimationCurve moveCurve = null;

    [Header("Soul Effect")]
    [SerializeField] private bool enableSoulEffect = true;
    [SerializeField] private float soulDuration = 0.9f;
    [SerializeField] private float soulTargetHeight = 1.2f;
    [SerializeField] private float soulArcHeight = 0.45f;
    [SerializeField] private Material soulMaterial;
    [SerializeField] private string soulGatherSfxKey = "Gauge";
    [SerializeField] private PieceType[] soulPieceFilter =
    {
        PieceType.Pawn,
        PieceType.Knight,
        PieceType.Rook,
        PieceType.Bishop,
        PieceType.Queen
    };

    [Header("After Cinematic Noti")]
    [SerializeField] private bool showAfterFirstKingSkillNoti = true;
    [SerializeField] private string afterFirstKingSkillNotiKey = "AfterFirstKingSkill";

    private readonly List<(GameObject go, bool wasActive)> hiddenUiStates = new();
    private bool playing;
    private Vector3 cameraOriginPos;
    private Quaternion cameraOriginRot;
    private TopDownCameraController cachedFollow;
    private bool cachedFollowEnabled;
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
    }

    public bool TryPlayCinematic(int cinematicId, Action onCompleted = null)
    {
        if (playing)
        {
            Debug.Log("[CinematicDirector] Skip play: another cinematic is already running.");
            return false;
        }

        if (!CanPlay(cinematicId, out string reason))
        {
            Debug.Log($"[CinematicDirector] Skip play({cinematicId}): {reason}");
            return false;
        }

        StartCoroutine(RunCinematic(cinematicId, onCompleted));
        return true;
    }

    private bool CanPlay(int cinematicId, out string reason)
    {
        if (cinematicId != firstKingSkillId)
        {
            reason = "unknown cinematic id.";
            return false;
        }

        if (firstKingSkillRequireTitleEntry && !GameSessionState.EnteredMainFromTitle)
        {
            reason = "firstKingSkillRequireTitleEntry is true and run did not enter from title.";
            return false;
        }

        if (firstKingSkillPlayOncePerRun && GameSessionState.HasPlayedCinematic(cinematicId))
        {
            reason = "already played once in current run.";
            return false;
        }

        if (ResolveKingTransform() == null)
        {
            reason = "king transform not found.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private IEnumerator RunCinematic(int cinematicId, Action onCompleted)
    {
        playing = true;
        EnsureReferences();
        EnsureDialogueUi();
        CacheAndHideGameplayUi();
        turnManager?.ForcePhase(TurnPhase.Busy);

        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.gameObject.SetActive(true);
            dialogueCanvasGroup.alpha = 0f;
            SetDialoguePanelScale(0f);
            SetDialogueTextAlpha(0f);
            yield return PlayDialogueWindowOpen();
        }

        Transform king = ResolveKingTransform();
        if (king != null)
        {
            yield return MoveCameraToOffset(king, firstZoomOffset);
        }

        DialogueData.DialogueLine[] lines = GetDialogueLines(firstKingSkillDialogue);
        if (lines.Length > 0)
        {
            yield return ShowLineWithFade(lines[0]);
            yield return WaitForNextInput();
        }

        if (king != null)
        {
            yield return MoveCameraToOffset(king, secondZoomOffset);
        }

        if (enableSoulEffect && king != null)
        {
            yield return PlaySoulAbsorbEffect(king);
        }

        if (lines.Length > 1)
        {
            yield return ShowLineWithFade(lines[1]);
            if (waitInputOnLastLine)
            {
                yield return WaitForNextInput();
            }
            else
            {
                yield return null;
            }
        }

        if (dialogueCanvasGroup != null)
        {
            yield return FadeDialogueText(1f, 0f, lineFadeDuration);
            yield return PlayDialogueWindowClose();
            dialogueCanvasGroup.gameObject.SetActive(false);
        }

        GameSessionState.MarkCinematicPlayed(cinematicId);
        yield return RestoreCameraState();
        RestoreGameplayUi();
        turnManager?.ForcePhase(TurnPhase.PlayerTurn);
        if (showAfterFirstKingSkillNoti && cinematicId == firstKingSkillId)
        {
            NotiManager.Instance?.ShowByKey(afterFirstKingSkillNotiKey);
        }
        playing = false;
        onCompleted?.Invoke();
    }

    private DialogueData.DialogueLine[] GetDialogueLines(DialogueData data)
    {
        if (data != null && data.TryGetLines(out DialogueData.DialogueLine[] lines) && lines != null && lines.Length > 0)
        {
            return lines;
        }

        return Array.Empty<DialogueData.DialogueLine>();
    }

    private void ApplyLine(DialogueData.DialogueLine line)
    {
        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrWhiteSpace(line.speaker) ? string.Empty : $"{speakerPrefix}{line.speaker}";
        }

        if (bodyText != null)
        {
            bodyText.text = line.text ?? string.Empty;
        }
    }

    private IEnumerator WaitForNextInput()
    {
        while (IsNextInputPressedThisFrame())
        {
            yield return null;
        }

        while (!IsNextInputPressedThisFrame())
        {
            yield return null;
        }
    }

    private bool IsNextInputPressedThisFrame()
    {
        bool pressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);

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

        if (dialoguePanelRect == null && dialogueCanvasGroup != null)
        {
            dialoguePanelRect = dialogueCanvasGroup.GetComponent<RectTransform>();
        }

        // Prefer scaling the actual dialogue panel (typically parent of texts) over canvas root.
        if (dialoguePanelRect == null && bodyText != null && bodyText.transform.parent is RectTransform parentRt)
        {
            dialoguePanelRect = parentRt;
        }

        if (dialoguePanelRect == null)
        {
            return;
        }

        dialoguePanelRect.localScale = new Vector3(Mathf.Max(0f, x), 1f, 1f);
    }

    private void SetDialogueTextAlpha(float alpha)
    {
        if (speakerText != null)
        {
            speakerText.alpha = Mathf.Clamp01(alpha);
        }

        if (bodyText != null)
        {
            bodyText.alpha = Mathf.Clamp01(alpha);
        }
    }

    private IEnumerator MoveCameraToOffset(Transform king, Vector3 offset)
    {
        if (targetCamera == null || king == null)
        {
            yield break;
        }

        Transform camTr = targetCamera.transform;
        if (cachedFollow == null)
        {
            cameraOriginPos = camTr.position;
            cameraOriginRot = camTr.rotation;
            cachedFollow = targetCamera.GetComponent<TopDownCameraController>();
            cachedFollowEnabled = cachedFollow != null && cachedFollow.enabled;
            if (cachedFollowEnabled)
            {
                cachedFollow.enabled = false;
            }
        }

        Vector3 focus = king.position + Vector3.up * lookHeight;
        Vector3 targetPos = focus + offset;
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
        yield return LerpCamera(camTr, camTr.position, camTr.rotation, cameraOriginPos, cameraOriginRot, moveDuration);

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

    private IEnumerator PlaySoulAbsorbEffect(Transform king)
    {
        if (board == null || king == null)
        {
            yield break;
        }

        SoundManager.Instance?.PlaySfx(soulGatherSfxKey);

        List<SoulGhost> ghosts = new();
        HashSet<PieceType> filters = new(soulPieceFilter ?? Array.Empty<PieceType>());

        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece == null || piece == turnManager?.KingPiece || !filters.Contains(piece.PieceType))
            {
                continue;
            }

            GameObject clone = Instantiate(piece.gameObject, piece.transform.position, piece.transform.rotation);
            clone.name = $"{piece.name}_SoulGhost";
            PrepareGhostObject(clone);

            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            if (soulMaterial != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    Material[] mats = r.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        mats[m] = soulMaterial;
                    }

                    r.sharedMaterials = mats;
                }
            }

            ghosts.Add(new SoulGhost
            {
                root = clone.transform,
                startPos = clone.transform.position,
                startScale = clone.transform.localScale
            });
        }

        float elapsed = 0f;
        while (elapsed < soulDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = soulDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / soulDuration);
            Vector3 end = king.position + Vector3.up * soulTargetHeight;
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i].root == null)
                {
                    continue;
                }

                Vector3 p = Vector3.LerpUnclamped(ghosts[i].startPos, end, t);
                p.y += Mathf.Sin(t * Mathf.PI) * soulArcHeight;
                ghosts[i].root.position = p;
                ghosts[i].root.localScale = Vector3.LerpUnclamped(ghosts[i].startScale, Vector3.zero, t);
            }

            yield return null;
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            if (ghosts[i].root != null)
            {
                Destroy(ghosts[i].root.gameObject);
            }
        }
    }

    private void PrepareGhostObject(GameObject go)
    {
        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
            rigidbodies[i].detectCollisions = false;
        }

        MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
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

        if (board != null)
        {
            foreach (ChessPiece piece in board.AllPieces)
            {
                if (piece != null && piece.Team == Team.Ally && piece.PieceType == PieceType.King)
                {
                    return piece.transform;
                }
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

        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoardManager>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
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
            else if (bodyText != null && bodyText.transform.parent is RectTransform parentRt)
            {
                dialoguePanelRect = parentRt;
            }
            else
            {
                dialoguePanelRect = dialogueCanvasGroup.GetComponent<RectTransform>();
            }
            return;
        }

        GameObject canvasGo = new("CinematicDialogueCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.52f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.sizeDelta = new Vector2(0f, 220f);
        panelRt.anchoredPosition = Vector2.zero;

        GameObject speakerGo = new("Speaker");
        speakerGo.transform.SetParent(panel.transform, false);
        speakerText = speakerGo.AddComponent<TextMeshProUGUI>();
        speakerText.fontSize = 34f;
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
        bodyText.fontSize = 38f;
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

    private struct SoulGhost
    {
        public Transform root;
        public Vector3 startPos;
        public Vector3 startScale;
    }
}
