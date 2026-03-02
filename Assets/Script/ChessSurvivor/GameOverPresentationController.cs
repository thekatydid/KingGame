using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameOverPresentationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform gameOverPanel;
    [SerializeField] private CanvasGroup statisticsGroup;
    [SerializeField] private CanvasGroup toTitleButtonGroup;
    [SerializeField] private TMP_Text totalKillNumText;
    [SerializeField] private TMP_Text turnNumText;
    [SerializeField] private TMP_Text coopPlanNumText;
    [SerializeField] private TMP_Text superSaveNumText;
    [SerializeField] private Button toTitleButton;

    [Header("Timing")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float flyInDuration = 0.45f;
    [SerializeField] private float panelExpandDelay = 1f;
    [SerializeField] private float panelExpandDuration = 0.25f;
    [SerializeField] private float statsFadeDelay = 0.2f;
    [SerializeField] private float statsFadeDuration = 0.2f;
    [SerializeField] private float buttonFadeDelayAfterStats = 1f;
    [SerializeField] private float buttonFadeDuration = 0.2f;

    [Header("Panel")]
    [SerializeField] private float flyInStartOffsetX = 1200f;
    [SerializeField] private float collapsedPanelHeight = 180f;
    [SerializeField] private float expandedPanelHeight = 600f;

    [Header("Scene")]
    [SerializeField] private string titleSceneName = "titleScene";

    private Coroutine routine;
    private Vector2 panelCenterPos;
    private bool initialized;
    private bool buttonBound;

    private void Awake()
    {
        EnsureBindings();
        InitializeVisualState();
    }

    public void HideImmediate()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        EnsureBindings();
        InitializeVisualState();
    }

    public void Show(int totalKills, int turnNum, int coopPlanNum, int superSaveNum)
    {
        EnsureBindings();
        InitializeVisualState();
        ApplyStats(totalKills, turnNum, coopPlanNum, superSaveNum);

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PlayRoutine());
    }

    private void EnsureBindings()
    {
        if (gameOverPanel == null)
        {
            gameOverPanel = FindRect("GameOverPannel");
        }

        if (statisticsGroup == null)
        {
            statisticsGroup = FindCanvasGroup("Statistics");
        }

        if (toTitleButtonGroup == null)
        {
            toTitleButtonGroup = FindCanvasGroup("ToTitleButton");
        }

        if (totalKillNumText == null)
        {
            totalKillNumText = FindTmp("TotalKillNum");
        }

        if (turnNumText == null)
        {
            turnNumText = FindTmp("TurnNum");
        }

        if (coopPlanNumText == null)
        {
            coopPlanNumText = FindTmp("CoopPlanNum");
        }

        if (superSaveNumText == null)
        {
            superSaveNumText = FindTmp("SuperSaveNum");
        }

        if (toTitleButton == null)
        {
            Transform t = FindByName("ToTitleButton");
            if (t != null)
            {
                toTitleButton = t.GetComponent<Button>();
            }
        }

        if (statisticsGroup != null && statisticsGroup.GetComponent<CanvasGroup>() == null)
        {
            statisticsGroup = statisticsGroup.gameObject.AddComponent<CanvasGroup>();
        }

        if (toTitleButtonGroup != null && toTitleButtonGroup.GetComponent<CanvasGroup>() == null)
        {
            toTitleButtonGroup = toTitleButtonGroup.gameObject.AddComponent<CanvasGroup>();
        }

        if (!buttonBound && toTitleButton != null)
        {
            toTitleButton.onClick.RemoveListener(OnClickToTitle);
            toTitleButton.onClick.AddListener(OnClickToTitle);
            buttonBound = true;
        }
    }

    private void InitializeVisualState()
    {
        if (gameOverPanel != null)
        {
            if (!initialized)
            {
                panelCenterPos = gameOverPanel.anchoredPosition;
                initialized = true;
            }

            gameOverPanel.gameObject.SetActive(false);
            Vector2 size = gameOverPanel.sizeDelta;
            size.y = collapsedPanelHeight;
            gameOverPanel.sizeDelta = size;
            gameOverPanel.anchoredPosition = panelCenterPos + Vector2.left * flyInStartOffsetX;
        }

        if (statisticsGroup != null)
        {
            statisticsGroup.alpha = 0f;
            statisticsGroup.interactable = false;
            statisticsGroup.blocksRaycasts = false;
        }

        if (toTitleButtonGroup != null)
        {
            toTitleButtonGroup.alpha = 0f;
            toTitleButtonGroup.interactable = false;
            toTitleButtonGroup.blocksRaycasts = false;
        }
    }

    private void ApplyStats(int totalKills, int turnNum, int coopPlanNum, int superSaveNum)
    {
        if (totalKillNumText != null)
        {
            totalKillNumText.text = totalKills.ToString();
        }

        if (turnNumText != null)
        {
            turnNumText.text = turnNum.ToString();
        }

        if (coopPlanNumText != null)
        {
            coopPlanNumText.text = coopPlanNum.ToString();
        }

        if (superSaveNumText != null)
        {
            superSaveNumText.text = superSaveNum.ToString();
        }
    }

    private IEnumerator PlayRoutine()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.anchoredPosition = panelCenterPos + Vector2.left * flyInStartOffsetX;
            gameOverPanel.gameObject.SetActive(true);
        }

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        if (gameOverPanel != null)
        {
            yield return LerpAnchoredPos(gameOverPanel, gameOverPanel.anchoredPosition, panelCenterPos, flyInDuration);
        }

        if (panelExpandDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(panelExpandDelay);
        }

        if (gameOverPanel != null)
        {
            Vector2 from = gameOverPanel.sizeDelta;
            Vector2 to = from;
            to.y = expandedPanelHeight;
            yield return LerpSize(gameOverPanel, from, to, panelExpandDuration);
        }

        if (statsFadeDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(statsFadeDelay);
        }

        if (statisticsGroup != null)
        {
            yield return LerpCanvasGroup(statisticsGroup, 0f, 1f, statsFadeDuration);
            statisticsGroup.interactable = true;
            statisticsGroup.blocksRaycasts = true;
        }

        if (buttonFadeDelayAfterStats > 0f)
        {
            yield return new WaitForSecondsRealtime(buttonFadeDelayAfterStats);
        }

        if (toTitleButtonGroup != null)
        {
            yield return LerpCanvasGroup(toTitleButtonGroup, 0f, 1f, buttonFadeDuration);
            toTitleButtonGroup.interactable = true;
            toTitleButtonGroup.blocksRaycasts = true;
        }

        routine = null;
    }

    private void OnClickToTitle()
    {
        if (!string.IsNullOrWhiteSpace(titleSceneName))
        {
            if (LoadingTransitionManager.Instance != null)
            {
                LoadingTransitionManager.Instance.LoadSceneWithTransition(titleSceneName);
            }
            else
            {
                SceneManager.LoadScene(titleSceneName);
            }
        }
    }

    private static IEnumerator LerpAnchoredPos(RectTransform rect, Vector2 from, Vector2 to, float duration)
    {
        if (rect == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (rect == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float eased = n * n * (3f - (2f * n));
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }

        if (rect != null)
        {
            rect.anchoredPosition = to;
        }
    }

    private static IEnumerator LerpSize(RectTransform rect, Vector2 from, Vector2 to, float duration)
    {
        if (rect == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (rect == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float eased = n * n * (3f - (2f * n));
            rect.sizeDelta = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }

        if (rect != null)
        {
            rect.sizeDelta = to;
        }
    }

    private static IEnumerator LerpCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        float t = 0f;
        group.alpha = from;
        while (t < duration)
        {
            if (group == null)
            {
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float n = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            group.alpha = Mathf.LerpUnclamped(from, to, n);
            yield return null;
        }

        if (group != null)
        {
            group.alpha = to;
        }
    }

    private RectTransform FindRect(string name)
    {
        Transform t = FindByName(name);
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    private CanvasGroup FindCanvasGroup(string name)
    {
        Transform t = FindByName(name);
        if (t == null)
        {
            return null;
        }

        CanvasGroup group = t.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = t.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private TMP_Text FindTmp(string name)
    {
        Transform t = FindByName(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private Transform FindByName(string objectName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == objectName)
            {
                return all[i];
            }
        }

        return null;
    }
}
