using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueData", menuName = "ChessSurvivor/Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 5)] public string text;
    }

    [Serializable]
    private class DialogueJsonWrapper
    {
        public DialogueLine[] lines;
    }

    [Header("Source")]
    [SerializeField] private TextAsset jsonFile;

    [Header("Fallback (optional)")]
    [SerializeField] private DialogueLine[] fallbackLines;

    public bool TryGetLines(out DialogueLine[] lines)
    {
        lines = null;

        if (jsonFile != null && !string.IsNullOrWhiteSpace(jsonFile.text))
        {
            try
            {
                DialogueJsonWrapper wrapper = JsonUtility.FromJson<DialogueJsonWrapper>(jsonFile.text);
                if (wrapper != null && wrapper.lines != null && wrapper.lines.Length > 0)
                {
                    lines = wrapper.lines;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueData] JSON parse failed: {ex.Message}");
            }
        }

        if (fallbackLines != null && fallbackLines.Length > 0)
        {
            lines = fallbackLines;
            return true;
        }

        return false;
    }
}
