using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class HoverSquashLoop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Mode")]
    [SerializeField] private bool alwaysAnimate = false;
    [SerializeField] private bool resetScaleWhenStopped = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Motion")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float horizontalScaleMultiplier = 1.08f;
    [SerializeField] private float verticalScaleMultiplier = 0.92f;
    [SerializeField] private bool includeZScale = false;
    [Header("Click Press")]
    [SerializeField] private bool enableClickSquash = true;
    [SerializeField] private Vector3 clickScaleMultiplier = new(0.9f, 0.9f, 1f);
    [SerializeField] private float clickPressLerpSpeed = 22f;
    [SerializeField] private float clickReleaseLerpSpeed = 16f;
    [Header("Click SFX (Optional)")]
    [SerializeField] private string clickSfxKey = "";
    [Header("Hover SFX (Optional)")]
    [SerializeField] private bool playHoverSfx = false;
    [SerializeField] private string hoverSfxKey = "";

    [Header("Optional Target")]
    [SerializeField] private Transform target;

    private bool hovered;
    private bool pressed;
    private Vector3 baseScale = Vector3.one;
    private float phase;
    private float pressBlend;

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }

        baseScale = target.localScale;
    }

    private void OnEnable()
    {
        if (target == null)
        {
            target = transform;
        }

        baseScale = target.localScale;
        phase = 0f;
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        bool active = alwaysAnimate || hovered;
        if (!active)
        {
            if (resetScaleWhenStopped)
            {
                target.localScale = baseScale;
            }

            return;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        phase += dt * speed;
        float wave = Mathf.Sin(phase);
        float t = (wave + 1f) * 0.5f;

        float sx = Mathf.Lerp(1f, horizontalScaleMultiplier, t);
        float sy = Mathf.Lerp(1f, verticalScaleMultiplier, t);
        float sz = includeZScale ? Mathf.Lerp(1f, horizontalScaleMultiplier, t) : 1f;

        float targetPress = (enableClickSquash && pressed) ? 1f : 0f;
        float pressLerp = targetPress > pressBlend ? clickPressLerpSpeed : clickReleaseLerpSpeed;
        pressBlend = Mathf.MoveTowards(pressBlend, targetPress, dt * Mathf.Max(0f, pressLerp));

        sx *= Mathf.Lerp(1f, clickScaleMultiplier.x, pressBlend);
        sy *= Mathf.Lerp(1f, clickScaleMultiplier.y, pressBlend);
        if (includeZScale)
        {
            sz *= Mathf.Lerp(1f, clickScaleMultiplier.z, pressBlend);
        }

        target.localScale = new Vector3(
            baseScale.x * sx,
            baseScale.y * sy,
            baseScale.z * sz);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        PlayHoverSfxIfNeeded();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        PlayClickSfxIfNeeded();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    private void OnDisable()
    {
        pressed = false;
        pressBlend = 0f;
        if (target != null && resetScaleWhenStopped)
        {
            target.localScale = baseScale;
        }
    }

    private void PlayClickSfxIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(clickSfxKey))
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(clickSfxKey);
    }

    private void PlayHoverSfxIfNeeded()
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
}
