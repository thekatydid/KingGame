using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class SummonUIController : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private ChessBoardManager board;
    [SerializeField] private RectTransform verticalRoot;
    [SerializeField] private TMP_Text chargeSummaryText;
    [SerializeField] private TMP_Text turnPointNumberText;
    [SerializeField] private bool autoCreateMissingRows = false;
    [Header("SFX")]
    [SerializeField] private string handSfxKey = "Hand";
    [SerializeField] private string dropSfxKey = "Drop";
    [Header("Noti")]
    [SerializeField] private bool showFirstSummonNotiOnButtonClick = true;
    [SerializeField] private string firstSummonNotiKey = "FirstSummon";
    [Header("Row Visual")]
    [SerializeField] private Color iconAndTextDisabledColor = new(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color iconTextDisabledColor = new(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color rowBackgroundDisabledColor = new(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private float interactableOffsetX = -25f;
    [SerializeField] private float hoverExtraOffsetX = 5f;
    [SerializeField] private float rowMoveDuration = 0.12f;
    [SerializeField] private Ease rowMoveEase = Ease.OutQuad;

    private readonly List<PieceType> summonOrder = new() { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen };
    private readonly Dictionary<PieceType, RowRefs> rows = new();
    private Camera mainCamera;
    private PieceType? pendingSummonType;
    private GameObject pendingPreviewObject;
    private bool rowsBuilt;
    private bool hierarchyEnsured;
    private bool firstSummonNotiShown;

    private void Awake()
    {
        EnsureAttachedToCanvas();
    }

    private sealed class RowRefs
    {
        public Button button;
        public TMP_Text nameText;
        public TMP_Text requiredText;
        public Image icon;
        public TMP_Text iconText;
        public Image background;
        public HoverColorChanger hoverColorChanger;
        public RectTransform rowRect;
        public float basePosX;
        public bool basePosCached;
        public bool lastInteractable;
        public bool lastHover;
        public bool visualStateInitialized;
        public bool baseColorCached;
        public Color baseIconColor;
        public Color baseIconTextColor;
        public Color baseNameColor;
        public Color baseRequiredColor;
        public Color baseBackgroundColor;
        public Tweener moveTween;
    }

    public void Initialize(TurnManager manager, ChessBoardManager boardManager, RectTransform root, TMP_Text bottomSummary)
    {
        turnManager = manager;
        board = boardManager;
        verticalRoot = root;
        chargeSummaryText = bottomSummary;
        mainCamera = Camera.main;

        BuildRowsIfNeeded();
    }

    private void BuildRowsIfNeeded()
    {
        if (rowsBuilt || verticalRoot == null)
        {
            return;
        }

        TryBindExistingRows();

        foreach (PieceType piece in summonOrder)
        {
            if (rows.ContainsKey(piece))
            {
                continue;
            }

            if (!autoCreateMissingRows)
            {
                continue;
            }

            GameObject row = CreateUIObject($"Summon_{piece}", verticalRoot);
            Image rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(0.13f, 0.13f, 0.14f, 0.95f);
            Button button = row.AddComponent<Button>();

            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 54f);

            HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.spacing = 8f;
            h.padding = new RectOffset(10, 10, 6, 6);

            GameObject iconObj = CreateUIObject("Icon", row.transform);
            Image icon = iconObj.AddComponent<Image>();
            icon.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 28f;

            GameObject nameObj = CreateUIObject("Name", row.transform);
            TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.text = PieceToDisplayName(piece);
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 95f;

            GameObject requiredObj = CreateUIObject("RequiredTxt", row.transform);
            TMP_Text requiredText = requiredObj.AddComponent<TextMeshProUGUI>();
            requiredText.color = new Color(0.95f, 0.85f, 0.4f, 1f);
            requiredText.alignment = TextAlignmentOptions.MidlineLeft;
            LayoutElement reqLayout = requiredObj.AddComponent<LayoutElement>();
            reqLayout.flexibleWidth = 1f;

            PieceType captured = piece;
            button.onClick.AddListener(() => OnClickSummon(captured));

            rows[piece] = new RowRefs
            {
                button = button,
                nameText = nameText,
                requiredText = requiredText,
                icon = icon,
                iconText = FindNamedText(row.transform, "IconTxt"),
                rowRect = rowRt
            };
        }

        rowsBuilt = true;
    }

    private void TryBindExistingRows()
    {
        List<RowRefs> fallbackRows = new();
        for (int i = 0; i < verticalRoot.childCount; i++)
        {
            Transform child = verticalRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (!TryGetPieceFromName(child.name, out PieceType piece))
            {
                RowRefs unnamed = EnsureRowRefs(child.gameObject);
                if (unnamed != null && unnamed.button != null)
                {
                    fallbackRows.Add(unnamed);
                }
                continue;
            }

            if (rows.ContainsKey(piece))
            {
                continue;
            }

            RowRefs refs = EnsureRowRefs(child.gameObject);
            if (refs == null || refs.button == null)
            {
                continue;
            }

            if (refs.nameText != null)
            {
                refs.nameText.text = PieceToDisplayName(piece);
            }
            BindRowButton(refs.button, piece);
            rows[piece] = refs;
        }

        // Secondary pass: support nested row hierarchies under SummonList.
        // (Some prefab setups place row buttons one level deeper.)
        if (rows.Count < summonOrder.Count)
        {
            Button[] nestedButtons = verticalRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < nestedButtons.Length; i++)
            {
                Button button = nestedButtons[i];
                if (button == null)
                {
                    continue;
                }

                Transform rowTransform = button.transform;
                if (rowTransform == null || rowTransform == verticalRoot)
                {
                    continue;
                }

                if (!TryGetPieceFromName(rowTransform.name, out PieceType piece))
                {
                    continue;
                }

                if (rows.ContainsKey(piece))
                {
                    continue;
                }

                RowRefs refs = EnsureRowRefs(rowTransform.gameObject);
                if (refs == null || refs.button == null)
                {
                    continue;
                }

                if (refs.nameText != null)
                {
                    refs.nameText.text = PieceToDisplayName(piece);
                }

                BindRowButton(refs.button, piece);
                rows[piece] = refs;
            }
        }

        // Fallback: if some rows were not parsed by name, bind them in summon order.
        if (fallbackRows.Count > 0)
        {
            int fallbackIndex = 0;
            for (int i = 0; i < summonOrder.Count && fallbackIndex < fallbackRows.Count; i++)
            {
                PieceType piece = summonOrder[i];
                if (rows.ContainsKey(piece))
                {
                    continue;
                }

                RowRefs refs = fallbackRows[fallbackIndex++];
                if (refs == null || refs.button == null)
                {
                    continue;
                }

                if (refs.nameText != null)
                {
                    refs.nameText.text = PieceToDisplayName(piece);
                }

                BindRowButton(refs.button, piece);
                rows[piece] = refs;
            }
        }

        for (int i = 0; i < summonOrder.Count; i++)
        {
            PieceType piece = summonOrder[i];
            if (!rows.ContainsKey(piece))
            {
                Debug.LogWarning($"[SummonUI] Row binding missing for piece: {piece}");
            }
        }
    }

    private RowRefs EnsureRowRefs(GameObject rowObj)
    {
        if (rowObj == null)
        {
            return null;
        }

        Button button = rowObj.GetComponent<Button>();
        if (button == null)
        {
            // Some row prefabs keep the clickable Button on a child object.
            button = rowObj.GetComponentInChildren<Button>(true);
        }

        if (button == null)
        {
            button = rowObj.AddComponent<Button>();
        }

        TMP_Text nameText = FindNamedText(rowObj.transform, "Name");
        TMP_Text requiredText = FindNamedText(rowObj.transform, "RequiredTxt");
        if (requiredText == null)
        {
            requiredText = FindNamedText(rowObj.transform, "Required");
        }
        Image icon = FindNamedImage(rowObj.transform, "Icon");

        if (nameText == null || requiredText == null)
        {
            TMP_Text[] texts = rowObj.GetComponentsInChildren<TMP_Text>(true);
            if (nameText == null && texts.Length > 0)
            {
                nameText = texts[0];
            }

            if (requiredText == null && texts.Length > 1)
            {
                requiredText = texts[1];
            }
        }

        if (nameText == null)
        {
            GameObject nameObj = CreateUIObject("Name", rowObj.transform);
            nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        if (requiredText == null)
        {
            GameObject reqObj = CreateUIObject("RequiredTxt", rowObj.transform);
            requiredText = reqObj.AddComponent<TextMeshProUGUI>();
            requiredText.color = new Color(0.95f, 0.85f, 0.4f, 1f);
            requiredText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        return new RowRefs
        {
            button = button,
            nameText = nameText,
            requiredText = requiredText,
            icon = icon,
            iconText = FindIconText(rowObj.transform),
            background = rowObj.GetComponent<Image>(),
            hoverColorChanger = rowObj.GetComponent<HoverColorChanger>(),
            rowRect = button != null
                ? button.GetComponent<RectTransform>()
                : rowObj.GetComponent<RectTransform>()
        };
    }

    private static bool TryGetPieceFromName(string rowName, out PieceType piece)
    {
        string name = rowName ?? string.Empty;
        if (name.IndexOf("pawn", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("폰", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            piece = PieceType.Pawn;
            return true;
        }

        if (name.IndexOf("knight", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("나이트", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            piece = PieceType.Knight;
            return true;
        }

        if (name.IndexOf("bishop", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("비숍", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            piece = PieceType.Bishop;
            return true;
        }

        if (name.IndexOf("rook", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("룩", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            piece = PieceType.Rook;
            return true;
        }

        if (name.IndexOf("queen", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("퀸", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            piece = PieceType.Queen;
            return true;
        }

        piece = PieceType.Pawn;
        return false;
    }

    private static TMP_Text FindNamedText(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform found = root.Find(childName);
        if (found != null)
        {
            TMP_Text direct = found.GetComponent<TMP_Text>();
            if (direct != null)
            {
                return direct;
            }
        }

        TMP_Text[] all = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text txt = all[i];
            if (txt == null)
            {
                continue;
            }

            if (txt.name.IndexOf(childName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return txt;
            }
        }

        return null;
    }

    private static Image FindNamedImage(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform found = root.Find(childName);
        if (found != null)
        {
            Image direct = found.GetComponent<Image>();
            if (direct != null)
            {
                return direct;
            }
        }

        Image[] all = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Image image = all[i];
            if (image == null)
            {
                continue;
            }

            if (image.name.IndexOf(childName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return null;
    }

    private static TMP_Text FindIconText(Transform root)
    {
        TMP_Text txt = FindNamedText(root, "IconTxt");
        if (txt != null)
        {
            return txt;
        }

        txt = FindNamedText(root, "IconText");
        if (txt != null)
        {
            return txt;
        }

        return null;
    }

    private void BindRowButton(Button button, PieceType piece)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnClickSummon(piece));
    }

    private void Update()
    {
        EnsureAutoBindings();
        if (turnManager == null || board == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        for (int i = 0; i < summonOrder.Count; i++)
        {
            PieceType piece = summonOrder[i];
            if (!rows.TryGetValue(piece, out RowRefs row) || row == null)
            {
                continue;
            }

            int required = turnManager.GetRequiredCharge(piece);
            bool ready = turnManager.IsSummonReady(piece);
            bool interactable = turnManager.CurrentPhase == TurnPhase.PlayerTurn && ready;

            row.requiredText.text = required.ToString();
            row.button.interactable = interactable;
            ApplyRowVisuals(row, interactable);
        }

        int sharedCharge = turnManager.GetSharedSummonCharge();
        if (turnPointNumberText != null)
        {
            turnPointNumberText.text = sharedCharge.ToString();
        }

        if (chargeSummaryText != null)
        {
            chargeSummaryText.text =
                $"턴 {turnManager.TurnCount} ({turnManager.CurrentPhase})\n" +
                $"공용 턴 게이지: {sharedCharge}\n" +
                $"비용 - 폰 {turnManager.GetRequiredCharge(PieceType.Pawn)} / " +
                $"나이트 {turnManager.GetRequiredCharge(PieceType.Knight)} / " +
                $"비숍 {turnManager.GetRequiredCharge(PieceType.Bishop)} / " +
                $"룩 {turnManager.GetRequiredCharge(PieceType.Rook)} / " +
                $"퀸 {turnManager.GetRequiredCharge(PieceType.Queen)}";
        }

        if (turnManager.CurrentPhase != TurnPhase.PlayerTurn)
        {
            CancelPendingSummon();
            return;
        }

        if (pendingSummonType.HasValue)
        {
            UpdatePendingPreviewFollowMouse();

            if (Input.GetMouseButtonDown(1))
            {
                CancelPendingSummon();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryPlacePendingSummon();
            }
        }
    }

    private void EnsureAutoBindings()
    {
        EnsureAttachedToCanvas();
        EnsureUIHierarchy();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TurnManager>();
        }

        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoardManager>();
        }

        if (verticalRoot == null)
        {
            Transform found = transform.Find("SummonList");
            if (found == null)
            {
                found = transform.Find("RightPanel/SummonList");
            }

            if (found is RectTransform rt)
            {
                verticalRoot = rt;
            }
        }

        if (chargeSummaryText == null)
        {
            Transform found = transform.Find("ChargeSummaryText");
            if (found == null)
            {
                found = transform.Find("RightPanel/ChargeSummaryText");
            }

            if (found != null)
            {
                chargeSummaryText = found.GetComponent<TMP_Text>();
            }
        }

        if (turnPointNumberText == null)
        {
            Transform found = transform.Find("TurnPointUI-Number");
            if (found == null)
            {
                found = transform.Find("RightPanel/TurnPointUI-Number");
            }

            if (found != null)
            {
                turnPointNumberText = found.GetComponent<TMP_Text>();
            }
        }

        BuildRowsIfNeeded();
    }

    private void EnsureUIHierarchy()
    {
        if (hierarchyEnsured)
        {
            return;
        }

        if (verticalRoot == null)
        {
            Transform found = transform.Find("SummonList");
            if (found == null)
            {
                GameObject listObj = CreateUIObject("SummonList", transform);
                RectTransform listRt = listObj.GetComponent<RectTransform>();
                listRt.anchorMin = new Vector2(0.08f, 0.42f);
                listRt.anchorMax = new Vector2(0.92f, 0.94f);
                listRt.offsetMin = Vector2.zero;
                listRt.offsetMax = Vector2.zero;
                verticalRoot = listRt;
            }
            else if (found is RectTransform listRt)
            {
                verticalRoot = listRt;
            }
        }

        if (verticalRoot != null && verticalRoot.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup vlg = verticalRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
        }

        if (chargeSummaryText == null)
        {
            Transform found = transform.Find("ChargeSummaryText");
            if (found == null)
            {
                GameObject textObj = CreateUIObject("ChargeSummaryText", transform);
                RectTransform rt = textObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.08f, 0.17f);
                rt.anchorMax = new Vector2(0.92f, 0.40f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
                text.color = new Color(0.85f, 0.95f, 0.95f, 1f);
                text.alignment = TextAlignmentOptions.TopLeft;
                chargeSummaryText = text;
            }
            else
            {
                chargeSummaryText = found.GetComponent<TMP_Text>();
                if (chargeSummaryText == null)
                {
                    TextMeshProUGUI tmp = found.gameObject != null
                        ? found.gameObject.GetComponent<TextMeshProUGUI>()
                        : null;

                    if (tmp == null && found.gameObject != null)
                    {
                        tmp = found.gameObject.AddComponent<TextMeshProUGUI>();
                    }

                    if (tmp != null)
                    {
                        tmp.color = new Color(0.85f, 0.95f, 0.95f, 1f);
                        tmp.alignment = TextAlignmentOptions.TopLeft;
                        chargeSummaryText = tmp;
                    }
                }
            }
        }

        hierarchyEnsured = true;
    }

    private void EnsureAttachedToCanvas()
    {
        if (GetComponentInParent<Canvas>() != null)
        {
            return;
        }

        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        if (existingCanvas == null)
        {
            GameObject canvasGo = new("Canvas");
            existingCanvas = canvasGo.AddComponent<Canvas>();
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        transform.SetParent(existingCanvas.transform, false);
    }

    private void OnClickSummon(PieceType selected)
    {
        if (turnManager.CurrentPhase != TurnPhase.PlayerTurn)
        {
            return;
        }

        if (!turnManager.IsSummonReady(selected))
        {
            Debug.Log($"[SummonUI] Not ready: {selected}, shared={turnManager.GetSharedSummonCharge()}, required={turnManager.GetRequiredCharge(selected)}");
            return;
        }

        if (showFirstSummonNotiOnButtonClick && !firstSummonNotiShown)
        {
            if (NotiManager.Instance != null && NotiManager.Instance.ShowByKey(firstSummonNotiKey))
            {
                firstSummonNotiShown = true;
            }
        }

        if (pendingSummonType.HasValue)
        {
            CancelPendingSummon();
        }

        if (rows.TryGetValue(selected, out RowRefs row) && row != null && row.hoverColorChanger != null)
        {
            row.hoverColorChanger.ExecutePointerClick();
            row.lastHover = false;
        }

        pendingSummonType = selected;
        pendingPreviewObject = board.SpawnPreviewPiece(selected, Team.Ally);
        SoundManager.Instance?.PlaySfx(handSfxKey);
    }

    private void TryPlacePendingSummon()
    {
        if (!pendingSummonType.HasValue || mainCamera == null || board == null || turnManager == null)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!board.TryGetCoordFromRay(ray, out BoardCoord target))
        {
            return;
        }

        if (!IsValidSummonTile(target))
        {
            return;
        }

        PieceType type = pendingSummonType.Value;
        ChessPiece spawned = board.SpawnPiece(type, Team.Ally, target);
        if (spawned == null)
        {
            Debug.LogWarning($"[SummonUI] Spawn failed: {type} at {target} (inside={board.IsInside(target)}, occupied={board.IsOccupied(target)})");
            return;
        }

        turnManager.ConsumeCharge(type);
        turnManager.RegisterPlayerSummon();
        turnManager.ReevaluatePlayerTurnCheckState();
        SoundManager.Instance?.PlaySfx(dropSfxKey);
        CancelPendingSummon();
    }

    private bool IsValidSummonTile(BoardCoord target)
    {
        return board.IsInside(target) && !board.IsOccupied(target);
    }

    private void UpdatePendingPreviewFollowMouse()
    {
        if (pendingPreviewObject == null || mainCamera == null || board == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (board.TryGetCoordFromRay(ray, out BoardCoord coord))
        {
            Vector3 pos = board.CoordToPieceWorld(coord) + board.transform.up * 0.06f;
            pendingPreviewObject.transform.position = pos;
        }
        else
        {
            pendingPreviewObject.transform.position = ray.origin + ray.direction * 7f;
        }
    }

    private void CancelPendingSummon()
    {
        pendingSummonType = null;
        if (pendingPreviewObject != null)
        {
            Destroy(pendingPreviewObject);
            pendingPreviewObject = null;
        }
    }

    private void OnDisable()
    {
        CancelPendingSummon();
        foreach (RowRefs row in rows.Values)
        {
            row.moveTween?.Kill();
            row.moveTween = null;
        }
    }

    private static string PieceToDisplayName(PieceType type)
    {
        return type.ToString();
    }

    private void ApplyRowVisuals(RowRefs row, bool interactable)
    {
        if (row == null)
        {
            return;
        }

        if (!row.baseColorCached)
        {
            if (row.icon != null)
            {
                row.baseIconColor = row.icon.color;
            }

            if (row.iconText != null)
            {
                row.baseIconTextColor = row.iconText.color;
            }

            if (row.nameText != null)
            {
                row.baseNameColor = row.nameText.color;
            }

            if (row.requiredText != null)
            {
                row.baseRequiredColor = row.requiredText.color;
            }

            if (row.background != null)
            {
                row.baseBackgroundColor = row.background.color;
            }

            row.baseColorCached = true;
        }

        if (row.rowRect == null)
        {
            return;
        }

        if (!row.basePosCached)
        {
            row.basePosX = row.rowRect.anchoredPosition.x;
            row.basePosCached = true;
        }

        bool isHover = false;
        if (interactable)
        {
            isHover = RectTransformUtility.RectangleContainsScreenPoint(row.rowRect, Input.mousePosition, null);
        }

        if (row.hoverColorChanger != null)
        {
            row.hoverColorChanger.UseInternalPointerInput = false;
        }

        bool stateChanged = !row.visualStateInitialized
            || row.lastHover != isHover
            || row.lastInteractable != interactable;
        if (!stateChanged)
        {
            return;
        }

        if (row.hoverColorChanger != null)
        {
            // Enabled state: delegate hover/default color transitions to HoverColorChanger.
            row.hoverColorChanger.ApplyColorChanges = interactable;
            row.hoverColorChanger.SetExternalHover(interactable && isHover);
        }

        if (!interactable)
        {
            // Disabled state: force explicit gray palette from SummonUI.
            if (row.icon != null)
            {
                row.icon.color = iconAndTextDisabledColor;
            }

            if (row.iconText != null)
            {
                row.iconText.color = iconAndTextDisabledColor;
            }

            if (row.nameText != null)
            {
                row.nameText.color = iconTextDisabledColor;
            }

            if (row.requiredText != null)
            {
                row.requiredText.color = iconTextDisabledColor;
            }

            if (row.background != null)
            {
                row.background.color = rowBackgroundDisabledColor;
            }
        }
        else
        {
            // Enabled state: when not hovering, restore base palette.
            if (!isHover && row.icon != null)
            {
                row.icon.color = row.baseIconColor;
            }

            if (!isHover && row.iconText != null)
            {
                row.iconText.color = row.baseIconTextColor;
            }

            if (!isHover && row.background != null)
            {
                row.background.color = row.baseBackgroundColor;
            }

            // Keep text colors in base color too.
            if (row.nameText != null)
            {
                row.nameText.color = row.baseNameColor;
            }

            if (row.requiredText != null)
            {
                row.requiredText.color = row.baseRequiredColor;
            }
        }

        row.visualStateInitialized = true;
        row.lastInteractable = interactable;
        row.lastHover = isHover;
        row.moveTween?.Kill();
        float hoverOffset = 0f;
        if (isHover)
        {
            float sign = Mathf.Approximately(interactableOffsetX, 0f) ? 1f : Mathf.Sign(interactableOffsetX);
            hoverOffset = -sign * hoverExtraOffsetX;
        }

        float targetX = row.basePosX + (interactable ? interactableOffsetX + hoverOffset : 0f);
        row.moveTween = row.rowRect.DOAnchorPosX(targetX, rowMoveDuration).SetEase(rowMoveEase);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
