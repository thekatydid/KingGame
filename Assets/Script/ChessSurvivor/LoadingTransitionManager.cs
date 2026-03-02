using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LoadingTransitionManager : MonoBehaviour
{
    public static LoadingTransitionManager Instance { get; private set; }

    [Header("Overlay")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private RectTransform kingIconRect;
    [SerializeField] private Image kingIconImage;
    [SerializeField] private Sprite kingIconSprite;
    [SerializeField] private Color iconColor = Color.black;
    [SerializeField] private int sortingOrder = 5000;

    [Header("Timing")]
    [SerializeField] private float expandDuration = 0.45f;
    [SerializeField] private float holdAfterExpand = 0.05f;
    [SerializeField] private float shrinkDuration = 0.45f;
    [SerializeField] private float startScale = 0.05f;
    [SerializeField] private float firstPeakScale = 1.2f;
    [SerializeField] private float dipScale = 0.95f;
    [SerializeField] private float coverScale = 44f;
    [SerializeField] private float coverPadding = 1.2f;
    [SerializeField] private float coverVerticalCompensation = 1.35f;
    [SerializeField] private float endScale = 0.05f;
    [SerializeField] private float idleScale = 1f;
    [Header("Post Load Stabilize")]
    [SerializeField, Min(0)] private int settleFramesBeforeReveal = 2;
    [SerializeField, Min(0f)] private float settleDelayBeforeReveal = 0.05f;

    private bool isLoading;
    private bool revealStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        SetOverlayVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void LoadSceneWithTransition(string sceneName, Sprite iconOverride = null)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        StartCoroutine(PlayTransition(sceneName, iconOverride));
    }

    private IEnumerator PlayTransition(string sceneName, Sprite iconOverride)
    {
        isLoading = true;
        revealStarted = false;
        EnsureOverlay();

        if (kingIconImage != null)
        {
            if (iconOverride != null)
            {
                kingIconImage.sprite = iconOverride;
            }
            else if (kingIconSprite != null)
            {
                kingIconImage.sprite = kingIconSprite;
            }

            kingIconImage.color = iconColor;
        }

        if (kingIconRect != null)
        {
            kingIconRect.localScale = Vector3.one * startScale;
        }

        SetOverlayVisible(true);
        yield return PlayExpandSequence();

        if (holdAfterExpand > 0f)
        {
            yield return new WaitForSecondsRealtime(holdAfterExpand);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        yield return RevealOnlyRoutine();
    }

    private IEnumerator LerpIconScale(float from, float to, float duration, bool easeIn)
    {
        if (kingIconRect == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float eased = easeIn ? (n * n * n) : (1f - Mathf.Pow(1f - n, 3f));
            float scale = Mathf.LerpUnclamped(from, to, eased);
            kingIconRect.localScale = Vector3.one * scale;
            yield return null;
        }

        kingIconRect.localScale = Vector3.one * to;
    }

    private IEnumerator PlayExpandSequence()
    {
        float targetCover = Mathf.Max(coverScale, ComputeAutoCoverScale());
        float phase1 = expandDuration * 0.35f;
        float phase2 = expandDuration * 0.15f;
        float phase3 = Mathf.Max(0.01f, expandDuration - phase1 - phase2);

        yield return LerpIconScale(startScale, firstPeakScale, phase1, easeIn: true);
        yield return LerpIconScale(firstPeakScale, dipScale, phase2, easeIn: false);
        yield return LerpIconScale(dipScale, targetCover, phase3, easeIn: true);
    }

    private float ComputeAutoCoverScale()
    {
        if (kingIconRect == null)
        {
            return coverScale;
        }

        float iconW = Mathf.Max(1f, kingIconRect.rect.width);
        float iconH = Mathf.Max(1f, kingIconRect.rect.height);
        float screenW = Mathf.Max(1f, Screen.width);
        float screenH = Mathf.Max(1f, Screen.height);

        float needByWidth = screenW / iconW;
        float needByHeight = (screenH / iconH) * Mathf.Max(1f, coverVerticalCompensation);
        float needByDiagonal = Mathf.Sqrt(screenW * screenW + screenH * screenH) / Mathf.Min(iconW, iconH);

        float required = Mathf.Max(needByWidth, needByHeight, needByDiagonal);
        return required * Mathf.Max(1f, coverPadding);
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null && overlayCanvasGroup != null && kingIconRect != null && kingIconImage != null)
        {
            NormalizeOverlayTransforms();
            if (overlayCanvas != null)
            {
                overlayCanvas.sortingOrder = sortingOrder;
            }
            return;
        }

        if (overlayCanvas == null)
        {
            GameObject canvasGo = new("LoadingTransitionCanvas");
            canvasGo.transform.SetParent(transform, false);
            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            overlayCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
        }
        else if (overlayCanvasGroup == null)
        {
            overlayCanvasGroup = overlayCanvas.GetComponent<CanvasGroup>();
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlayCanvas.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (kingIconRect == null || kingIconImage == null)
        {
            GameObject iconGo = new("KingTransitionIcon");
            iconGo.transform.SetParent(overlayCanvas.transform, false);
            kingIconRect = iconGo.AddComponent<RectTransform>();
            kingIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            kingIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            kingIconRect.pivot = new Vector2(0.5f, 0.5f);
            kingIconRect.anchoredPosition = Vector2.zero;
            kingIconRect.sizeDelta = new Vector2(260f, 260f);
            kingIconImage = iconGo.AddComponent<Image>();
            kingIconImage.raycastTarget = false;
            kingIconImage.color = iconColor;
            if (kingIconSprite != null)
            {
                kingIconImage.sprite = kingIconSprite;
            }
        }

        NormalizeOverlayTransforms();
        if (overlayCanvas != null)
        {
            overlayCanvas.sortingOrder = sortingOrder;
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = visible;
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = visible ? 1f : 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void ResetIconToIdleScale()
    {
        if (kingIconRect == null)
        {
            return;
        }

        kingIconRect.localScale = Vector3.one * Mathf.Max(0.001f, idleScale);
    }

    private IEnumerator RevealOnlyRoutine()
    {
        if (revealStarted)
        {
            yield break;
        }

        revealStarted = true;
        EnsureOverlay();
        SetOverlayVisible(true);
        float cover = Mathf.Max(coverScale, ComputeAutoCoverScale());
        if (kingIconRect != null)
        {
            // Prevent 1-frame partial coverage when reverse starts after scene load.
            kingIconRect.localScale = Vector3.one * Mathf.Max(0.001f, cover);
        }

        // Let the loaded scene finish first-frame spikes (Awake/Start/UI rebuild/shader warmup).
        int waitFrames = Mathf.Max(0, settleFramesBeforeReveal);
        for (int i = 0; i < waitFrames; i++)
        {
            yield return null;
        }

        if (settleDelayBeforeReveal > 0f)
        {
            yield return new WaitForSecondsRealtime(settleDelayBeforeReveal);
        }

        float from = Mathf.Max(0.001f, cover);
        float to = Mathf.Max(0.001f, endScale);
        yield return LerpIconScale(from, to, shrinkDuration, easeIn: false);

        SetOverlayVisible(false);
        ResetIconToIdleScale();
        isLoading = false;
        revealStarted = false;
    }

    private void NormalizeOverlayTransforms()
    {
        if (overlayCanvas != null && overlayCanvas.transform.localScale.sqrMagnitude < 0.0001f)
        {
            overlayCanvas.transform.localScale = Vector3.one;
        }

        if (kingIconRect != null && kingIconRect.localScale.sqrMagnitude < 0.0001f)
        {
            kingIconRect.localScale = Vector3.one * Mathf.Max(0.001f, idleScale);
        }
    }
}
