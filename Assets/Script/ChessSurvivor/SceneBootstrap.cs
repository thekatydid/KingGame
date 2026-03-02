using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SceneBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootstrapCurrentScene(scene);
    }

    private static void BootstrapCurrentScene(Scene scene)
    {
        if (scene.name == "mainGameScene")
        {
            BuildMainGame();
        }
    }

    private static void BuildMainGame()
    {
        EnsureEventSystem();

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        ChessBoardManager board = Object.FindFirstObjectByType<ChessBoardManager>();
        if (board == null)
        {
            GameObject boardGo = new("ChessBoard");
            board = boardGo.AddComponent<ChessBoardManager>();
        }

        GameManager flow = Object.FindFirstObjectByType<GameManager>();
        if (flow == null)
        {
            GameObject flowGo = new("GameManager");
            flow = flowGo.AddComponent<GameManager>();
        }

        ExperienceSystem exp = Object.FindFirstObjectByType<ExperienceSystem>();
        if (exp == null)
        {
            GameObject expGo = new("ExperienceSystem");
            exp = expGo.AddComponent<ExperienceSystem>();
        }

        StageManager stage = Object.FindFirstObjectByType<StageManager>();
        if (stage == null)
        {
            GameObject stageGo = new("StageManager");
            stage = stageGo.AddComponent<StageManager>();
        }
        stage.Initialize(board);

        TurnManager turn = Object.FindFirstObjectByType<TurnManager>();
        if (turn == null)
        {
            GameObject turnGo = new("TurnManager");
            turn = turnGo.AddComponent<TurnManager>();
        }

        turn.Initialize(board, exp, stage);

        bool hadAnyPieces = HasAnyPieces(board);
        ChessPiece king = FindExistingKing(board);
        if (king == null)
        {
            BoardCoord kingSpawn = FindNearestEmptyToCenter(board);
            king = board.SpawnPiece(PieceType.King, Team.Ally, kingSpawn);
        }

        if (!hadAnyPieces)
        {
            int centerX = board.Width / 2;
            int centerY = board.Height / 2;

            board.SpawnPiece(PieceType.Pawn, Team.Ally, new BoardCoord(centerX - 1, centerY));
            board.SpawnPiece(PieceType.Pawn, Team.Ally, new BoardCoord(centerX + 1, centerY));
        }

        turn.SetKing(king);

        KingPlayerController input = Object.FindFirstObjectByType<KingPlayerController>();
        if (input != null)
        {
            input.Initialize(cam, board, turn, king);
        }

        EnemyHoverThreatPreview hoverPreview = Object.FindFirstObjectByType<EnemyHoverThreatPreview>();
        if (hoverPreview == null)
        {
            GameObject hoverPreviewGo = new("EnemyHoverThreatPreview");
            hoverPreview = hoverPreviewGo.AddComponent<EnemyHoverThreatPreview>();
        }
        hoverPreview.Initialize(cam, board);

        if (Object.FindFirstObjectByType<AllyHoverPlannedPathPreview>() == null)
        {
            GameObject allyPlanPreviewGo = new("AllyHoverPlannedPathPreview");
            AllyHoverPlannedPathPreview allyPlanPreview = allyPlanPreviewGo.AddComponent<AllyHoverPlannedPathPreview>();
            allyPlanPreview.Initialize(cam, board, turn);
        }

        if (Object.FindFirstObjectByType<SummonUIController>() == null)
        {
            BuildMainUI(turn, board, exp, flow);
        }
    }

    private static bool HasAnyPieces(ChessBoardManager board)
    {
        foreach (ChessPiece _ in board.AllPieces)
        {
            return true;
        }

        return false;
    }

    private static ChessPiece FindExistingKing(ChessBoardManager board)
    {
        foreach (ChessPiece piece in board.AllPieces)
        {
            if (piece != null && piece.Team == Team.Ally && piece.PieceType == PieceType.King)
            {
                return piece;
            }
        }

        return null;
    }

    private static BoardCoord FindNearestEmptyToCenter(ChessBoardManager board)
    {
        int centerX = board.Width / 2;
        int centerY = board.Height / 2;
        BoardCoord center = new(centerX, centerY);
        if (!board.IsOccupied(center))
        {
            return center;
        }

        int maxR = Mathf.Max(board.Width, board.Height);
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
                    if (!board.IsInside(c) || board.IsOccupied(c))
                    {
                        continue;
                    }

                    return c;
                }
            }
        }

        return center;
    }

    private static void BuildMainUI(TurnManager turn, ChessBoardManager board, ExperienceSystem exp, GameManager flow)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameObject canvasGo;
        if (canvas == null)
        {
            canvasGo = new("Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvasGo = canvas.gameObject;
            if (canvasGo.GetComponent<CanvasScaler>() == null)
            {
                canvasGo.AddComponent<CanvasScaler>();
            }

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }
        }

        GameObject panel = CreateUIObject("RightPanel", canvasGo.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.7f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.sizeDelta = new Vector2(280f, 0f);
        panelRt.anchoredPosition = Vector2.zero;

        GameObject summonListObj = CreateUIObject("SummonList", panel.transform);
        RectTransform listRt = summonListObj.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0.08f, 0.42f);
        listRt.anchorMax = new Vector2(0.92f, 0.94f);
        listRt.offsetMin = Vector2.zero;
        listRt.offsetMax = Vector2.zero;
        VerticalLayoutGroup vlg = summonListObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        GameObject chargeObj = CreateUIObject("ChargeSummaryText", panel.transform);
        TMP_Text chargeText = chargeObj.AddComponent<TextMeshProUGUI>();
        chargeText.color = new Color(0.85f, 0.95f, 0.95f, 1f);
        chargeText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform chargeRt = chargeObj.GetComponent<RectTransform>();
        chargeRt.anchorMin = new Vector2(0.08f, 0.17f);
        chargeRt.anchorMax = new Vector2(0.92f, 0.40f);
        chargeRt.offsetMin = Vector2.zero;
        chargeRt.offsetMax = Vector2.zero;

        GameObject expObj = CreateUIObject("ExpText", panel.transform);
        Text expText = expObj.AddComponent<Text>();
        expText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        expText.color = new Color(0.9f, 0.9f, 0.3f, 1f);
        expText.alignment = TextAnchor.UpperLeft;
        RectTransform expRt = expObj.GetComponent<RectTransform>();
        expRt.anchorMin = new Vector2(0.1f, 0.2f);
        expRt.anchorMax = new Vector2(0.9f, 0.32f);
        expRt.offsetMin = Vector2.zero;
        expRt.offsetMax = Vector2.zero;

        SummonUIController summonUi = canvasGo.AddComponent<SummonUIController>();
        summonUi.Initialize(turn, board, listRt, chargeText);

        exp.OnExpChanged += (level, current, required) =>
        {
            expText.text = $"LV {level}\nEXP {current}/{required}";
        };
        exp.OnLevelUp += level => { Debug.Log($"Level Up! {level}"); };
        expText.text = $"LV {exp.Level}\nEXP {exp.CurrentExp}/{exp.RequiredExp}";
    }

    private static void EnsureEventSystem()
    {
        UnityEngine.EventSystems.EventSystem[] allEventSystems =
            Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (allEventSystems.Length > 1)
        {
            for (int i = 1; i < allEventSystems.Length; i++)
            {
                if (allEventSystems[i] != null)
                {
                    Object.Destroy(allEventSystems[i].gameObject);
                }
            }
        }

        UnityEngine.EventSystems.EventSystem esFirst =
            Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        if (esFirst != null)
        {
            bool hasAnyInputModule = esFirst.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() != null;
#if ENABLE_INPUT_SYSTEM
            hasAnyInputModule |= esFirst.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null;
#endif
            if (!hasAnyInputModule)
            {
                esFirst.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            return;
        }

        GameObject es = new("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

}
