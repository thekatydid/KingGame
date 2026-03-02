using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class TextAnimator : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Wiggle")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float characterPhaseOffset = 0.35f;
    [SerializeField] private float amplitude = 7f;
    [SerializeField] private bool bounceOnlyUp = true;

    [Header("Extra Motion")]
    [SerializeField] private float scaleAmount = 0.12f;
    [SerializeField] private float rotationAmount = 8f;

    private TMP_Text textComponent;
    private TMP_MeshInfo[] cachedMeshInfo;
    private bool playing;
    private float timeSeed;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        timeSeed = Random.Range(0f, 999f);
    }

    private void OnEnable()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TMP_Text>();
        }

        CacheMeshData();
        playing = playOnEnable;
    }

    private void OnDisable()
    {
        RestoreOriginalMesh();
    }

    public void Play()
    {
        CacheMeshData();
        playing = true;
    }

    public void Stop(bool restoreImmediately = true)
    {
        playing = false;
        if (restoreImmediately)
        {
            RestoreOriginalMesh();
        }
    }

    private void LateUpdate()
    {
        if (!playing || textComponent == null)
        {
            return;
        }

        if (textComponent.havePropertiesChanged || cachedMeshInfo == null || cachedMeshInfo.Length == 0)
        {
            CacheMeshData();
        }

        TMP_TextInfo textInfo = textComponent.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
        {
            return;
        }

        float dtTime = (useUnscaledTime ? Time.unscaledTime : Time.time) + timeSeed;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
            {
                continue;
            }

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Vector3[] srcVertices = cachedMeshInfo[materialIndex].vertices;
            Vector3[] dstVertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 center = (srcVertices[vertexIndex] + srcVertices[vertexIndex + 2]) * 0.5f;
            float phase = (dtTime * speed) + (i * characterPhaseOffset);
            float wave = Mathf.Sin(phase);
            float bounce = bounceOnlyUp ? Mathf.Abs(wave) : wave;

            float yOffset = bounce * amplitude;
            float scale = 1f + (bounce * scaleAmount);
            float rot = wave * rotationAmount;

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(0f, yOffset, 0f),
                Quaternion.Euler(0f, 0f, rot),
                new Vector3(scale, scale, 1f));

            dstVertices[vertexIndex] = matrix.MultiplyPoint3x4(srcVertices[vertexIndex] - center) + center;
            dstVertices[vertexIndex + 1] = matrix.MultiplyPoint3x4(srcVertices[vertexIndex + 1] - center) + center;
            dstVertices[vertexIndex + 2] = matrix.MultiplyPoint3x4(srcVertices[vertexIndex + 2] - center) + center;
            dstVertices[vertexIndex + 3] = matrix.MultiplyPoint3x4(srcVertices[vertexIndex + 3] - center) + center;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            textComponent.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    private void CacheMeshData()
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.ForceMeshUpdate();
        cachedMeshInfo = textComponent.textInfo.CopyMeshInfoVertexData();
    }

    private void RestoreOriginalMesh()
    {
        if (textComponent == null || cachedMeshInfo == null)
        {
            return;
        }

        TMP_TextInfo textInfo = textComponent.textInfo;
        int count = Mathf.Min(textInfo.meshInfo.Length, cachedMeshInfo.Length);
        for (int i = 0; i < count; i++)
        {
            Vector3[] src = cachedMeshInfo[i].vertices;
            Vector3[] dst = textInfo.meshInfo[i].vertices;
            int len = Mathf.Min(src.Length, dst.Length);
            for (int v = 0; v < len; v++)
            {
                dst[v] = src[v];
            }

            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}
