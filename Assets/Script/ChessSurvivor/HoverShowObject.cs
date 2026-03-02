using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class HoverShowObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject[] targets;
    [SerializeField] private bool hideTargetsOnStart = true;
    [SerializeField] private bool showOnHover = true;
    [SerializeField] private bool hideOnExit = true;

    private void Awake()
    {
        if (hideTargetsOnStart)
        {
            SetTargets(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (showOnHover)
        {
            SetTargets(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hideOnExit)
        {
            SetTargets(false);
        }
    }

    private void OnMouseEnter()
    {
        if (showOnHover)
        {
            SetTargets(true);
        }
    }

    private void OnMouseExit()
    {
        if (hideOnExit)
        {
            SetTargets(false);
        }
    }

    [ContextMenu("Show Targets")]
    public void ShowTargets()
    {
        SetTargets(true);
    }

    [ContextMenu("Hide Targets")]
    public void HideTargets()
    {
        SetTargets(false);
    }

    private void SetTargets(bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }
}

