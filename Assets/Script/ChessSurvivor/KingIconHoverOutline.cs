using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class KingIconHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineScale = 1.08f;
    [SerializeField] private bool hideOnClick = true;

    private Image sourceImage;
    private Image outlineImage;
    private bool hoverEnabled = true;

    private void Awake()
    {
        EnsureOutlineImage();
        SetOutlineVisible(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverEnabled)
        {
            SetOutlineVisible(false);
            return;
        }

        SetOutlineVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetOutlineVisible(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hideOnClick)
        {
            SetOutlineVisible(false);
        }
    }

    public void SetHoverEnabled(bool enabled)
    {
        hoverEnabled = enabled;
        if (!hoverEnabled)
        {
            SetOutlineVisible(false);
        }
    }

    private void EnsureOutlineImage()
    {
        if (sourceImage == null)
        {
            sourceImage = GetComponent<Image>();
        }

        if (sourceImage != null)
        {
            sourceImage.raycastTarget = true;
        }

        if (outlineImage == null)
        {
            Transform child = transform.Find("HoverOutline");
            if (child != null)
            {
                outlineImage = child.GetComponent<Image>();
            }
        }

        if (outlineImage == null)
        {
            GameObject go = new("HoverOutline");
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            outlineImage = go.AddComponent<Image>();
        }

        RectTransform srcRt = sourceImage != null ? sourceImage.rectTransform : GetComponent<RectTransform>();
        RectTransform outlineRt = outlineImage.rectTransform;
        outlineRt.anchorMin = Vector2.zero;
        outlineRt.anchorMax = Vector2.one;
        outlineRt.offsetMin = Vector2.zero;
        outlineRt.offsetMax = Vector2.zero;
        outlineRt.localScale = new Vector3(outlineScale, outlineScale, 1f);

        if (sourceImage != null)
        {
            outlineImage.sprite = sourceImage.sprite;
            outlineImage.type = sourceImage.type;
            outlineImage.preserveAspect = sourceImage.preserveAspect;
            outlineImage.material = sourceImage.material;
        }

        outlineImage.color = Color.white;
        outlineImage.raycastTarget = false;
    }

    private void SetOutlineVisible(bool visible)
    {
        EnsureOutlineImage();
        if (outlineImage != null)
        {
            outlineImage.enabled = visible;
            outlineImage.color = outlineColor;
        }
    }
}
