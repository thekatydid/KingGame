using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HoverColorChanger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Input Mode")]
    [SerializeField] private bool useInternalPointerInput = true;

    [Header("References")]
    [SerializeField] private RectTransform squashTarget;
    [SerializeField] private List<Graphic> targetA = new();
    [SerializeField] private List<Graphic> targetB = new();

    [Header("Colors")]
    [SerializeField] private bool applyColorChanges = true;
    [SerializeField] private Color hoverAColor = Color.white;
    [SerializeField] private Color hoverBColor = Color.white;

    [Header("Hover Squash")]
    [SerializeField] private Vector3 hoverScaleMultiplier = new(1.08f, 1.08f, 1f);
    [SerializeField] private float hoverLerpSpeed = 16f;

    [Header("Click Press")]
    [SerializeField] private Vector3 clickScaleMultiplier = new(0.9f, 0.9f, 1f);
    [SerializeField] private float clickPressDuration = 0.06f;
    [SerializeField] private float clickReleaseDuration = 0.09f;
    [SerializeField] private bool useUnscaledTime = true;
    [Header("Click SFX")]
    [SerializeField] private bool playClickSfx = false;
    [SerializeField] private string clickSfxKey = "";
    [Header("Hover SFX")]
    [SerializeField] private bool playHoverSfx = false;
    [SerializeField] private string hoverSfxKey = "";

    private readonly List<GraphicColorCache> targetACache = new();
    private readonly List<GraphicColorCache> targetBCache = new();
    private Vector3 baseScale = Vector3.one;
    private bool hovered;
    private bool pressed;
    private bool clickedRecovering;
    private float clickTimer;
    private Selectable boundSelectable;

    public bool UseInternalPointerInput
    {
        get => useInternalPointerInput;
        set => useInternalPointerInput = value;
    }

    public bool ApplyColorChanges
    {
        get => applyColorChanges;
        set => applyColorChanges = value;
    }

    private struct GraphicColorCache
    {
        public Graphic graphic;
        public Color defaultColor;
    }

    private void Awake()
    {
        EnsureRefs();
        CacheDefaults();
    }

    private void OnEnable()
    {
        EnsureRefs();
        CacheDefaults();
        hovered = false;
        pressed = false;
        clickedRecovering = false;
        clickTimer = 0f;
        ApplyDefaultVisualImmediate();
    }

    private void Update()
    {
        if (squashTarget == null)
        {
            return;
        }

        if (!IsInteractionAllowed())
        {
            hovered = false;
            pressed = false;
            clickedRecovering = false;
            clickTimer = 0f;
            if (useInternalPointerInput)
            {
                ApplyDefaultVisualImmediate();
            }
            else
            {
                squashTarget.localScale = Vector3.Lerp(
                    squashTarget.localScale,
                    baseScale,
                    1f - Mathf.Exp(-hoverLerpSpeed * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime)));
            }
            return;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector3 hoverTargetScale = hovered ? Multiply(baseScale, hoverScaleMultiplier) : baseScale;
        Vector3 targetScale = hoverTargetScale;

        if (pressed)
        {
            clickedRecovering = false;
            clickTimer = 0f;
            targetScale = Multiply(hoverTargetScale, clickScaleMultiplier);
            float speed = clickPressDuration > 0f ? (1f / clickPressDuration) : 999f;
            squashTarget.localScale = Vector3.Lerp(squashTarget.localScale, targetScale, 1f - Mathf.Exp(-speed * dt));
            return;
        }

        if (clickedRecovering)
        {
            clickTimer += dt;
            float n = clickReleaseDuration <= 0f ? 1f : Mathf.Clamp01(clickTimer / clickReleaseDuration);
            float eased = n * n * (3f - 2f * n);
            Vector3 fromPressed = Multiply(hoverTargetScale, clickScaleMultiplier);
            squashTarget.localScale = Vector3.LerpUnclamped(fromPressed, hoverTargetScale, eased);
            if (n >= 1f)
            {
                clickedRecovering = false;
            }
            return;
        }

        squashTarget.localScale = Vector3.Lerp(
            squashTarget.localScale,
            targetScale,
            1f - Mathf.Exp(-hoverLerpSpeed * dt));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!useInternalPointerInput)
        {
            return;
        }

        ExecuteHoverEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!useInternalPointerInput)
        {
            return;
        }

        ExecuteHoverExit();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!useInternalPointerInput)
        {
            return;
        }

        ExecutePointerDown();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!useInternalPointerInput)
        {
            return;
        }

        ExecutePointerUp();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!useInternalPointerInput)
        {
            return;
        }

        ExecutePointerClick();
    }

    public void ExecuteHoverEnter()
    {
        if (!IsInteractionAllowed())
        {
            return;
        }

        hovered = true;
        ApplyHoverColors();
        TryPlayHoverSfx();
    }

    public void ExecuteHoverExit()
    {
        hovered = false;
        ApplyDefaultColors();
    }

    public void SetExternalHover(bool isHover)
    {
        if (isHover)
        {
            ExecuteHoverEnter();
        }
        else
        {
            ExecuteHoverExit();
        }
    }

    public void ExecutePointerDown()
    {
        if (!IsInteractionAllowed())
        {
            return;
        }

        pressed = true;
    }

    public void ExecutePointerUp()
    {
        if (!pressed)
        {
            return;
        }

        pressed = false;
        clickedRecovering = true;
        clickTimer = 0f;
    }

    public void ExecutePointerClick()
    {
        if (!IsInteractionAllowed())
        {
            return;
        }

        // Click complete: restore color to default regardless of hover state.
        hovered = false;
        ApplyDefaultColors();
        TryPlayClickSfx();
    }

    private void EnsureRefs()
    {
        if (squashTarget == null)
        {
            squashTarget = transform as RectTransform;
        }

        if (boundSelectable == null)
        {
            boundSelectable = GetComponent<Selectable>();
        }
    }

    private void CacheDefaults()
    {
        RebuildGraphicCache(targetA, targetACache);
        RebuildGraphicCache(targetB, targetBCache);

        if (squashTarget != null)
        {
            baseScale = squashTarget.localScale;
        }
    }

    private void ApplyHoverColors()
    {
        if (!applyColorChanges)
        {
            return;
        }

        ApplyCacheColor(targetACache, hoverAColor);
        ApplyCacheColor(targetBCache, hoverBColor);
    }

    private void ApplyDefaultColors()
    {
        if (!applyColorChanges)
        {
            return;
        }

        RestoreCacheColor(targetACache);
        RestoreCacheColor(targetBCache);
    }

    private void ApplyDefaultVisualImmediate()
    {
        ApplyDefaultColors();
        if (squashTarget != null)
        {
            squashTarget.localScale = baseScale;
        }
    }

    private static Vector3 Multiply(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    private void TryPlayClickSfx()
    {
        if (!playClickSfx || string.IsNullOrWhiteSpace(clickSfxKey))
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(clickSfxKey);
    }

    private void TryPlayHoverSfx()
    {
        if (!playHoverSfx || string.IsNullOrWhiteSpace(hoverSfxKey))
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(hoverSfxKey);
    }

    private bool IsInteractionAllowed()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (boundSelectable != null && !boundSelectable.interactable)
        {
            return false;
        }

        return true;
    }

    private static void RebuildGraphicCache(List<Graphic> source, List<GraphicColorCache> cache)
    {
        cache.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Graphic g = source[i];
            if (g == null)
            {
                continue;
            }

            cache.Add(new GraphicColorCache
            {
                graphic = g,
                defaultColor = g.color
            });
        }
    }

    private static void ApplyCacheColor(List<GraphicColorCache> cache, Color color)
    {
        for (int i = 0; i < cache.Count; i++)
        {
            Graphic g = cache[i].graphic;
            if (g == null)
            {
                continue;
            }

            g.color = color;
        }
    }

    private static void RestoreCacheColor(List<GraphicColorCache> cache)
    {
        for (int i = 0; i < cache.Count; i++)
        {
            Graphic g = cache[i].graphic;
            if (g == null)
            {
                continue;
            }

            g.color = cache[i].defaultColor;
        }
    }
}
