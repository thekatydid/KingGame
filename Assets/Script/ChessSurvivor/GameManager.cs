using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool IsGameOver => ended;
    public const string BestKillPrefKey = "BEST_KILL";

    [SerializeField] private GameOverPresentationController gameOverPresentation;
    [SerializeField] private bool enableResetHotkey = true;
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    [SerializeField] private bool resetToPlayableState = true;
    [Header("UI Visibility")]
    [SerializeField] private GameObject kingUiRoot;
    [SerializeField] private GameObject summonUiRoot;
    [Header("Audio")]
    [SerializeField] private string gameOverBgmKey = "GameOver";
    [Header("GameOver Vignette")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private Color gameOverVignetteColor = new(0.4f, 0.015686275f, 0.015686275f, 1f); // #660404

    private Vignette cachedVignette;
    private Color cachedVignetteColor;
    private bool cachedVignetteColorValid;
    private TurnManager cachedTurnManager;
    private bool kingUiWasActive;
    private bool summonUiWasActive;

    private bool ended;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheVignetteReference();
        CacheGameOverPresentation();
        CacheTurnManager();
        CacheUiRoots();
    }

    public void LoseGame(string reason)
    {
        if (ended)
        {
            return;
        }

        ended = true;
        SetGameplayUiVisible(false);
        ApplyGameOverVignette();
        if (SoundManager.Instance != null && !string.IsNullOrWhiteSpace(gameOverBgmKey))
        {
            SoundManager.Instance.PlayBgm(gameOverBgmKey, true);
        }

        CacheTurnManager();
        if (cachedTurnManager != null)
        {
            SaveBestKillIfNeeded(cachedTurnManager.TotalKillCount);
        }

        if (gameOverPresentation != null && cachedTurnManager != null)
        {
            gameOverPresentation.Show(
                cachedTurnManager.KingKillCount,
                cachedTurnManager.TurnCount,
                cachedTurnManager.CoopPlanCount,
                cachedTurnManager.SuperSaveCount);
        }

    }

    private static void SaveBestKillIfNeeded(int totalKillCount)
    {
        int currentBest = PlayerPrefs.GetInt(BestKillPrefKey, 0);
        if (totalKillCount <= currentBest)
        {
            return;
        }

        PlayerPrefs.SetInt(BestKillPrefKey, totalKillCount);
        PlayerPrefs.Save();
    }

    private void Update()
    {
        if (enableResetHotkey && Input.GetKeyDown(resetKey))
        {
            ResetToDialogueEndCheckpoint();
        }
    }

    public void ClearGameOverState()
    {
        ended = false;
        SetGameplayUiVisible(true);
        RestoreVignetteColor();
    }

    public bool ResetToDialogueEndCheckpoint()
    {
        TurnUndoManager undoManager = FindFirstObjectByType<TurnUndoManager>();
        if (undoManager == null)
        {
            return false;
        }

        return undoManager.RestorePostDialogueCheckpoint(resetToPlayableState);
    }

    private void CacheGameOverPresentation()
    {
        if (gameOverPresentation != null)
        {
            return;
        }

        gameOverPresentation = FindFirstObjectByType<GameOverPresentationController>(FindObjectsInactive.Include);
        if (gameOverPresentation != null)
        {
            return;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != "GameOverCanvas")
            {
                continue;
            }

            if (t.hideFlags != HideFlags.None)
            {
                continue;
            }

            gameOverPresentation = t.GetComponent<GameOverPresentationController>();
            if (gameOverPresentation == null)
            {
                gameOverPresentation = t.gameObject.AddComponent<GameOverPresentationController>();
            }

            break;
        }
    }

    private void CacheTurnManager()
    {
        if (cachedTurnManager == null)
        {
            cachedTurnManager = FindFirstObjectByType<TurnManager>(FindObjectsInactive.Include);
        }
    }

    private void CacheUiRoots()
    {
        if (kingUiRoot == null)
        {
            KingGaugeUIController kingUi = FindFirstObjectByType<KingGaugeUIController>(FindObjectsInactive.Include);
            if (kingUi != null)
            {
                kingUiRoot = kingUi.gameObject;
            }
        }

        if (summonUiRoot == null)
        {
            SummonUIController summonUi = FindFirstObjectByType<SummonUIController>(FindObjectsInactive.Include);
            if (summonUi != null)
            {
                summonUiRoot = summonUi.gameObject;
            }
        }
    }

    private void SetGameplayUiVisible(bool visible)
    {
        CacheUiRoots();

        if (!visible)
        {
            if (kingUiRoot != null)
            {
                kingUiWasActive = kingUiRoot.activeSelf;
                kingUiRoot.SetActive(false);
            }

            if (summonUiRoot != null)
            {
                summonUiWasActive = summonUiRoot.activeSelf;
                summonUiRoot.SetActive(false);
            }

            return;
        }

        if (kingUiRoot != null && kingUiWasActive)
        {
            kingUiRoot.SetActive(true);
        }

        if (summonUiRoot != null && summonUiWasActive)
        {
            summonUiRoot.SetActive(true);
        }
    }

    private void CacheVignetteReference()
    {
        if (postProcessVolume == null)
        {
            postProcessVolume = FindFirstObjectByType<Volume>();
        }

        if (postProcessVolume == null || postProcessVolume.profile == null)
        {
            return;
        }

        postProcessVolume.profile.TryGet(out cachedVignette);
        if (cachedVignette != null)
        {
            cachedVignetteColor = cachedVignette.color.value;
            cachedVignetteColorValid = true;
        }
    }

    private void ApplyGameOverVignette()
    {
        if (cachedVignette == null)
        {
            CacheVignetteReference();
        }

        if (cachedVignette == null)
        {
            return;
        }

        if (!cachedVignetteColorValid)
        {
            cachedVignetteColor = cachedVignette.color.value;
            cachedVignetteColorValid = true;
        }

        cachedVignette.color.Override(gameOverVignetteColor);
    }

    private void RestoreVignetteColor()
    {
        if (cachedVignette == null || !cachedVignetteColorValid)
        {
            return;
        }

        cachedVignette.color.Override(cachedVignetteColor);
    }
}
