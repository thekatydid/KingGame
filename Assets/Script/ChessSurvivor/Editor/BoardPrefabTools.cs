using System.IO;
using UnityEditor;
using UnityEngine;

public static class BoardPrefabTools
{
    private const string FolderPath = "Assets/Prefabs/ChessBoard";
    private const string CellPrefabPath = FolderPath + "/BoardCell.prefab";
    private const string BoardPrefabPath = FolderPath + "/ChessBoard_40x40.prefab";
    private const string MaterialFolderPath = FolderPath + "/Materials";
    private const string TileLightMatPath = MaterialFolderPath + "/M_BoardTile_Light.mat";
    private const string TileDarkMatPath = MaterialFolderPath + "/M_BoardTile_Dark.mat";
    private const string HighlightMatPath = MaterialFolderPath + "/M_BoardHighlight.mat";
    private const string ThreatMatPath = MaterialFolderPath + "/M_BoardThreat.mat";
    private const string BorderMatPath = MaterialFolderPath + "/M_BoardBorder.mat";

    private const int DefaultWidth = 40;
    private const int DefaultHeight = 40;
    private const float DefaultCellSize = 1.5f;
    private const float DefaultTileHeight = 0.06f;

    [MenuItem("Tools/ChessSurvivor/Board/Create 1-Cell Prefab")]
    public static void CreateOneCellPrefab()
    {
        Directory.CreateDirectory(FolderPath);
        Directory.CreateDirectory(MaterialFolderPath);

        Material tileLightMat = GetOrCreateMaterial(TileLightMatPath, "Universal Render Pipeline/Lit", new Color(0.86f, 0.86f, 0.9f, 1f));
        Material tileDarkMat = GetOrCreateMaterial(TileDarkMatPath, "Universal Render Pipeline/Lit", new Color(0.18f, 0.18f, 0.2f, 1f));
        Material highlightMat = GetOrCreateMaterial(HighlightMatPath, "Universal Render Pipeline/Lit", new Color(0f, 1f, 0f, 1f));
        Material threatMat = GetOrCreateMaterial(ThreatMatPath, "Universal Render Pipeline/Lit", new Color(1f, 0.7f, 0f, 0.95f));
        Material borderMat = GetOrCreateMaterial(BorderMatPath, "Sprites/Default", new Color(1f, 0.15f, 0.15f, 1f));

        GameObject root = new("BoardCell");

        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = "Tile";
        tile.transform.SetParent(root.transform, false);
        tile.transform.localPosition = new Vector3(0f, -DefaultTileHeight * 0.5f, 0f);
        tile.transform.localScale = new Vector3(DefaultCellSize * 0.98f, DefaultTileHeight, DefaultCellSize * 0.98f);
        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            tileRenderer.sharedMaterial = tileLightMat;
        }

        GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlight.name = "Highlight";
        highlight.transform.SetParent(root.transform, false);
        highlight.transform.localPosition = new Vector3(0f, DefaultTileHeight + 0.01f, 0f);
        highlight.transform.localScale = new Vector3(DefaultCellSize * 0.85f, 0.02f, DefaultCellSize * 0.85f);
        Renderer highlightRenderer = highlight.GetComponent<Renderer>();
        if (highlightRenderer != null)
        {
            highlightRenderer.sharedMaterial = highlightMat;
        }
        DisableCollider(highlight);
        highlight.SetActive(false);

        GameObject threat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        threat.name = "Threat";
        threat.transform.SetParent(root.transform, false);
        threat.transform.localPosition = new Vector3(0f, DefaultTileHeight + 0.035f, 0f);
        threat.transform.localScale = new Vector3(DefaultCellSize * 0.45f, 0.015f, DefaultCellSize * 0.45f);
        Renderer threatRenderer = threat.GetComponent<Renderer>();
        if (threatRenderer != null)
        {
            threatRenderer.sharedMaterial = threatMat;
        }
        DisableCollider(threat);
        threat.SetActive(false);

        GameObject borderGo = new("CaptureBorder");
        borderGo.transform.SetParent(root.transform, false);
        borderGo.transform.localPosition = new Vector3(0f, DefaultTileHeight + 0.035f, 0f);
        LineRenderer border = borderGo.AddComponent<LineRenderer>();
        border.sharedMaterial = borderMat;
        border.useWorldSpace = false;
        border.loop = false;
        border.positionCount = 5;
        border.startWidth = 0.06f;
        border.endWidth = 0.06f;
        border.startColor = new Color(1f, 0.15f, 0.15f, 1f);
        border.endColor = border.startColor;
        float h = DefaultCellSize * 0.47f;
        border.SetPosition(0, new Vector3(-h, 0f, -h));
        border.SetPosition(1, new Vector3(h, 0f, -h));
        border.SetPosition(2, new Vector3(h, 0f, h));
        border.SetPosition(3, new Vector3(-h, 0f, h));
        border.SetPosition(4, new Vector3(-h, 0f, -h));
        borderGo.SetActive(false);

        BoardCellInstance cellInstance = root.AddComponent<BoardCellInstance>();
        SerializedObject cellSo = new(cellInstance);
        cellSo.FindProperty("tileRenderer").objectReferenceValue = tileRenderer;
        cellSo.FindProperty("lightTileMaterial").objectReferenceValue = tileLightMat;
        cellSo.FindProperty("darkTileMaterial").objectReferenceValue = tileDarkMat;
        cellSo.FindProperty("useDarkTile").boolValue = false;
        cellSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created 1-cell prefab: {CellPrefabPath}");
    }

    [MenuItem("Tools/ChessSurvivor/Board/Create 40x40 Board Prefab (from 1-Cell)")]
    public static void CreateBoardPrefabFromCell()
    {
        GameObject cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
        if (cellPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Cell Prefab",
                $"Create the 1-cell prefab first:\n{CellPrefabPath}",
                "OK");
            return;
        }

        Directory.CreateDirectory(FolderPath);

        GameObject boardRoot = new("ChessBoard");
        ChessBoardManager manager = boardRoot.AddComponent<ChessBoardManager>();
        ApplyBoardSerializedDefaults(manager);

        GameObject tilesRoot = new("BoardTiles");
        tilesRoot.transform.SetParent(boardRoot.transform, false);
        GameObject highlightsRoot = new("BoardHighlights");
        highlightsRoot.transform.SetParent(boardRoot.transform, false);

        Vector3 origin = new(
            -((DefaultWidth - 1) * DefaultCellSize) * 0.5f,
            0f,
            -((DefaultHeight - 1) * DefaultCellSize) * 0.5f);

        for (int x = 0; x < DefaultWidth; x++)
        {
            for (int y = 0; y < DefaultHeight; y++)
            {
                GameObject cellInstance = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, boardRoot.transform);
                cellInstance.name = $"Cell_{x}_{y}";
                Vector3 basePos = origin + new Vector3(x * DefaultCellSize, 0f, y * DefaultCellSize);
                cellInstance.transform.SetParent(tilesRoot.transform, false);
                cellInstance.transform.localPosition = basePos;

                BoardCellInstance cellComp = cellInstance.GetComponent<BoardCellInstance>();
                if (cellComp != null)
                {
                    cellComp.SetUseDarkTile(((x + y) & 1) == 1);
                }
            }
        }

        PrefabUtility.SaveAsPrefabAsset(boardRoot, BoardPrefabPath);
        Object.DestroyImmediate(boardRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created board prefab: {BoardPrefabPath}");
    }

    private static void ApplyBoardSerializedDefaults(ChessBoardManager manager)
    {
        SerializedObject so = new(manager);
        so.FindProperty("width").intValue = DefaultWidth;
        so.FindProperty("height").intValue = DefaultHeight;
        so.FindProperty("cellSize").floatValue = DefaultCellSize;
        so.FindProperty("autoCenterOrigin").boolValue = true;
        so.FindProperty("buildTileGridVisual").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DisableCollider(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null)
        {
            c.enabled = false;
        }
    }

    private static Material GetOrCreateMaterial(string path, string shaderName, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
