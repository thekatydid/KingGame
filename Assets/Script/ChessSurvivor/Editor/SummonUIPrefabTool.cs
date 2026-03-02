using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SummonUIPrefabTool
{
    private const string OutputFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = OutputFolder + "/SummonUI.prefab";

    [MenuItem("Tools/ChessSurvivor/UI/Create SummonUI Prefab")]
    public static void CreateSummonUIPrefab()
    {
        Directory.CreateDirectory(OutputFolder);

        GameObject root = new("SummonUI");
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 0f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 0.5f);
        rootRt.sizeDelta = new Vector2(280f, 0f);
        rootRt.anchoredPosition = Vector2.zero;

        Image panelImage = root.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.7f);

        GameObject summonListObj = CreateUIObject("SummonList", root.transform);
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

        GameObject chargeObj = CreateUIObject("ChargeSummaryText", root.transform);
        Text chargeText = chargeObj.AddComponent<Text>();
        chargeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        chargeText.color = new Color(0.85f, 0.95f, 0.95f, 1f);
        chargeText.alignment = TextAnchor.UpperLeft;
        RectTransform chargeRt = chargeObj.GetComponent<RectTransform>();
        chargeRt.anchorMin = new Vector2(0.08f, 0.17f);
        chargeRt.anchorMax = new Vector2(0.92f, 0.40f);
        chargeRt.offsetMin = Vector2.zero;
        chargeRt.offsetMax = Vector2.zero;

        SummonUIController controller = root.AddComponent<SummonUIController>();
        SerializedObject so = new(controller);
        so.FindProperty("verticalRoot").objectReferenceValue = listRt;
        so.FindProperty("chargeSummaryText").objectReferenceValue = chargeText;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created SummonUI prefab: {PrefabPath}");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
