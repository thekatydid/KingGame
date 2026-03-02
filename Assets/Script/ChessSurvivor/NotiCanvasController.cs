using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NotiCanvasController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform notiBackground;
    [SerializeField] private TMP_Text notiTxt;
    [SerializeField] private CanvasGroup notiCanvasGroup;
    [SerializeField] private Image notiBackgroundImage;

    [Header("Layout")]
    [SerializeField] [Min(0f)] private float horizontalPadding = 12f;
    [SerializeField] [Min(0f)] private float verticalPadding = 10f;
    [SerializeField] [Min(0f)] private float minWidth = 0f;
    [SerializeField] [Min(0f)] private float minHeight = 0f;
    [SerializeField] [Min(0f)] private float maxContentWidth = 0f;
    [SerializeField] [Range(0.3f, 1f)] private float autoMaxContentWidthRatio = 0.72f;

    [Header("Animation")]
    [SerializeField] [Min(0.01f)] private float expandDuration = 0.2f;
    [SerializeField] [Min(0.01f)] private float collapseDuration = 0.14f;
    [SerializeField] [Min(0f)] private float fadeInDuration = 0.12f;
    [SerializeField] [Min(0f)] private float fadeOutDuration = 0.12f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private AnimationCurve expandCurve = null;
    [SerializeField] private AnimationCurve collapseCurve = null;

    [Header("SFX")]
    [SerializeField] private bool playOpenSfx = true;
    [SerializeField] private string openSfxKey = "Noti";

    private Coroutine playRoutine;
    private Color txtBaseColor = Color.white;
    private bool txtBaseColorCached;
    private Color bgBaseColor = Color.white;
    private bool bgBaseColorCached;

    private void Awake()
    {
        AutoBind();
        EnsureCurves();
        CacheTextBaseColor();
        HideImmediate();
    }

    private void OnValidate()
    {
        AutoBind();
        EnsureCurves();
    }

    public void ShowNotification(string message, float visibleSeconds)
    {
        ShowNotification(message, visibleSeconds, null, null);
    }

    public void ShowNotification(string message, float visibleSeconds, Action onCompleted)
    {
        ShowNotification(message, visibleSeconds, null, onCompleted);
    }

    public void ShowNotification(string message, float visibleSeconds, Color? backgroundColor, Action onCompleted = null)
    {
        if (notiBackground == null || notiTxt == null)
        {
            onCompleted?.Invoke();
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine(message ?? string.Empty, Mathf.Max(0f, visibleSeconds), backgroundColor, onCompleted));
    }

    [ContextMenu("Hide Immediate")]
    public void HideImmediate()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (notiBackground != null)
        {
            notiBackground.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            notiBackground.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
        }

        if (notiTxt != null)
        {
            SetTextVisible(false);
        }

        if (notiCanvasGroup != null)
        {
            notiCanvasGroup.alpha = 0f;
        }
    }

    private IEnumerator PlayRoutine(string message, float visibleSeconds, Color? backgroundColor, Action onCompleted)
    {
        notiTxt.text = message;
        SetTextVisible(false); // hidden while measuring/expanding
        SetBackgroundColor(backgroundColor);

        // Width: use the longest explicit line, not full raw string length.
        // Height: use whole text preferred height (includes line breaks).
        notiTxt.ForceMeshUpdate();
        Vector2 preferred = notiTxt.GetPreferredValues(message);
        float longestLineWidth = 0f;
        string[] lines = (message ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            float lineWidth = notiTxt.GetPreferredValues(lines[i]).x;
            if (lineWidth > longestLineWidth)
            {
                longestLineWidth = lineWidth;
            }
        }

        float contentWidthLimit = ResolveContentWidthLimit();
        float unclampedWidth = Mathf.Max(minWidth, longestLineWidth + (horizontalPadding * 2f));
        float targetWidth = contentWidthLimit > 0f
            ? Mathf.Min(unclampedWidth, contentWidthLimit + (horizontalPadding * 2f))
            : unclampedWidth;

        float constrainedContentWidth = Mathf.Max(0f, targetWidth - (horizontalPadding * 2f));
        float heightBase = contentWidthLimit > 0f
            ? notiTxt.GetPreferredValues(message, constrainedContentWidth, 0f).y
            : preferred.y;
        float targetHeight = Mathf.Max(minHeight, heightBase + (verticalPadding * 2f));

        if (playOpenSfx && !string.IsNullOrWhiteSpace(openSfxKey))
        {
            SoundManager.Instance?.PlaySfx(openSfxKey);
        }

        yield return AnimateOpen(targetWidth, targetHeight);
        SetTextVisible(true);

        float elapsed = 0f;
        while (elapsed < visibleSeconds)
        {
            elapsed += DeltaTime();
            yield return null;
        }

        SetTextVisible(false);
        yield return AnimateClose(targetWidth, targetHeight);
        playRoutine = null;
        onCompleted?.Invoke();
    }

    private IEnumerator AnimateOpen(float targetWidth, float targetHeight)
    {
        float widthTime = Mathf.Max(0.0001f, expandDuration);
        float fadeTime = Mathf.Max(0.0001f, fadeInDuration);
        float total = Mathf.Max(widthTime, fadeTime);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += DeltaTime();

            float wn = Mathf.Clamp01(elapsed / widthTime);
            float eased = expandCurve != null ? expandCurve.Evaluate(wn) : wn;
            float width = Mathf.LerpUnclamped(0f, targetWidth, eased);
            float height = Mathf.LerpUnclamped(0f, targetHeight, eased);
            notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (notiCanvasGroup != null)
            {
                float fn = Mathf.Clamp01(elapsed / fadeTime);
                notiCanvasGroup.alpha = fn;
            }

            yield return null;
        }

        notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        if (notiCanvasGroup != null)
        {
            notiCanvasGroup.alpha = 1f;
        }
    }

    private IEnumerator AnimateClose(float startWidth, float startHeight)
    {
        float widthTime = Mathf.Max(0.0001f, collapseDuration);
        float fadeTime = Mathf.Max(0.0001f, fadeOutDuration);
        float total = Mathf.Max(widthTime, fadeTime);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += DeltaTime();

            float wn = Mathf.Clamp01(elapsed / widthTime);
            float eased = collapseCurve != null ? collapseCurve.Evaluate(wn) : wn;
            float width = Mathf.LerpUnclamped(startWidth, 0f, eased);
            float height = Mathf.LerpUnclamped(startHeight, 0f, eased);
            notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (notiCanvasGroup != null)
            {
                float fn = Mathf.Clamp01(elapsed / fadeTime);
                notiCanvasGroup.alpha = 1f - fn;
            }

            yield return null;
        }

        notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
        notiBackground?.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
        if (notiCanvasGroup != null)
        {
            notiCanvasGroup.alpha = 0f;
        }
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void SetTextVisible(bool visible)
    {
        if (notiTxt == null)
        {
            return;
        }

        CacheTextBaseColor();
        Color c = txtBaseColor;
        c.a = visible ? txtBaseColor.a : 0f;
        notiTxt.color = c;
    }

    private void CacheTextBaseColor()
    {
        if (txtBaseColorCached || notiTxt == null)
        {
            return;
        }

        txtBaseColor = notiTxt.color;
        txtBaseColorCached = true;
    }

    private void CacheBackgroundBaseColor()
    {
        if (bgBaseColorCached)
        {
            return;
        }

        if (notiBackgroundImage == null && notiBackground != null)
        {
            notiBackgroundImage = notiBackground.GetComponent<Image>();
        }

        if (notiBackgroundImage == null)
        {
            return;
        }

        bgBaseColor = notiBackgroundImage.color;
        bgBaseColorCached = true;
    }

    private void SetBackgroundColor(Color? overrideColor)
    {
        if (notiBackgroundImage == null && notiBackground != null)
        {
            notiBackgroundImage = notiBackground.GetComponent<Image>();
        }

        if (notiBackgroundImage == null)
        {
            return;
        }

        CacheBackgroundBaseColor();
        notiBackgroundImage.color = overrideColor ?? bgBaseColor;
    }

    private void AutoBind()
    {
        if (notiCanvasGroup == null)
        {
            notiCanvasGroup = GetComponent<CanvasGroup>();
            if (notiCanvasGroup == null)
            {
                notiCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (notiBackground == null)
        {
            Transform t = transform.Find("Noti");
            if (t is RectTransform rt)
            {
                notiBackground = rt;
            }
        }

        if (notiBackgroundImage == null && notiBackground != null)
        {
            notiBackgroundImage = notiBackground.GetComponent<Image>();
        }

        if (notiTxt == null && notiBackground != null)
        {
            Transform txt = notiBackground.Find("NotiTxt");
            if (txt != null)
            {
                notiTxt = txt.GetComponent<TMP_Text>();
            }
        }
    }

    private void EnsureCurves()
    {
        if (expandCurve == null || expandCurve.length == 0)
        {
            // Slight overshoot to get a sticky expansion feel.
            expandCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3.8f),
                new Keyframe(0.72f, 1.06f, 0f, 0f),
                new Keyframe(1f, 1f, -2f, 0f));
        }

        if (collapseCurve == null || collapseCurve.length == 0)
        {
            collapseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private float ResolveContentWidthLimit()
    {
        if (maxContentWidth > 0f)
        {
            return maxContentWidth;
        }

        RectTransform scope = null;
        if (notiBackground != null)
        {
            scope = notiBackground.parent as RectTransform;
        }

        if (scope == null)
        {
            scope = transform as RectTransform;
        }

        if (scope == null)
        {
            return 0f;
        }

        float width = scope.rect.width;
        if (width <= 0f)
        {
            return 0f;
        }

        return width * Mathf.Clamp(autoMaxContentWidthRatio, 0.3f, 1f);
    }
}
