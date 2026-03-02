using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TitleStartManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Transform kingTransform;
    [SerializeField] private GameObject trailEffectRoot;
    [SerializeField] private LoadingTransitionManager loadingTransition;
    [SerializeField] private Sprite loadingKingIconSprite;

    [Header("Scene")]
    [SerializeField] private string targetSceneName = "mainGameScene";
    [SerializeField] private string titleBgmKey = "title";
    [SerializeField] private bool playTitleBgmOnEnable = true;
    [SerializeField] [Min(0f)] private float titleBgmStartTimeSeconds = 78f;
    [SerializeField] [Min(0f)] private float titleBgmRetrySeconds = 2f;

    [Header("King Launch")]
    [SerializeField] private float launchDelay = 1f;
    [SerializeField] private float kingFlyDuration = 0.6f;
    [SerializeField] private float kingTargetZ = 8f;
    [SerializeField] private float kingFlySpinTurns = 4f;
    [SerializeField] private bool useLocalPositionZ = false;
    [SerializeField] private AnimationCurve kingFlyCurve = null;
    [Header("King Hover BestScore")]
    [SerializeField] private bool enableKingHoverBestScore = true;
    [SerializeField] private GameObject bestScoreBox;
    [SerializeField] private string bestScoreBoxName = "BestScoreBox";
    [SerializeField] private TMP_Text bestScoreTxt;
    [SerializeField] private string bestScoreTxtName = "BestScoreTxt";
    [SerializeField] private bool hideBestScoreBoxOnStart = true;

    private bool starting;
    private Coroutine titleBgmRoutine;

    private void Awake()
    {
        if (kingFlyCurve == null || kingFlyCurve.length == 0)
        {
            kingFlyCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0.6f),
                new Keyframe(1f, 1f, 2.4f, 0f));
        }

        AutoBind();
        TryAutoBindBestScoreBox();
        TryAutoBindBestScoreText();
        RefreshBestScoreText();
        if (hideBestScoreBoxOnStart && bestScoreBox != null)
        {
            bestScoreBox.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGameFromButton);
            startButton.onClick.AddListener(StartGameFromButton);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnClickExit);
            exitButton.onClick.AddListener(OnClickExit);
        }

        RequestTitleBgmPlayback();
        RefreshBestScoreText();
    }

    private void OnDisable()
    {
        if (titleBgmRoutine != null)
        {
            StopCoroutine(titleBgmRoutine);
            titleBgmRoutine = null;
        }
    }

    private void Update()
    {
        if (!starting && Input.GetKeyDown(KeyCode.Escape))
        {
            ResetBestKillData();
        }

        if (starting || !enableKingHoverBestScore || kingTransform == null || bestScoreBox == null)
        {
            return;
        }

        bool hoveringKing = IsHoveringKing();
        if (bestScoreBox.activeSelf != hoveringKing)
        {
            bestScoreBox.SetActive(hoveringKing);
        }
    }

    public void StartGameFromButton()
    {
        if (starting)
        {
            return;
        }

        StartCoroutine(RunStartSequence());
    }

    private IEnumerator RunStartSequence()
    {
        starting = true;
        if (startButton != null)
        {
            startButton.interactable = false;
        }

        GameSessionState.BeginMainRunFromTitle();
        EnableTrailEffects();

        if (launchDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(launchDelay);
        }

        PlayLaunchSfx();
        yield return FlyKingOut();

        if (loadingTransition == null)
        {
            loadingTransition = LoadingTransitionManager.Instance;
            if (loadingTransition == null)
            {
                GameObject go = new("LoadingTransitionManager");
                loadingTransition = go.AddComponent<LoadingTransitionManager>();
            }
        }

        if (loadingTransition != null)
        {
            loadingTransition.LoadSceneWithTransition(targetSceneName, loadingKingIconSprite);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    private static void PlayLaunchSfx()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlaySfx("Fire");
    }

    private void TryPlayTitleBgm()
    {
        if (!playTitleBgmOnEnable || string.IsNullOrWhiteSpace(titleBgmKey))
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            return;
        }

        if (titleBgmStartTimeSeconds > 0f)
        {
            SoundManager.Instance.PlayBgmAtTime(titleBgmKey, titleBgmStartTimeSeconds, true);
        }
        else
        {
            SoundManager.Instance.PlayBgm(titleBgmKey, true);
        }
    }

    private void RequestTitleBgmPlayback()
    {
        TryPlayTitleBgm();

        if (titleBgmRoutine != null)
        {
            StopCoroutine(titleBgmRoutine);
        }

        titleBgmRoutine = StartCoroutine(TryPlayTitleBgmRoutine());
    }

    private IEnumerator TryPlayTitleBgmRoutine()
    {
        float elapsed = 0f;
        float maxWait = Mathf.Max(0f, titleBgmRetrySeconds);
        while (elapsed <= maxWait)
        {
            TryPlayTitleBgm();
            if (IsTitleBgmPlaying())
            {
                break;
            }

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        if (!IsTitleBgmPlaying() && SoundManager.Instance != null && !string.IsNullOrWhiteSpace(titleBgmKey))
        {
            // Final fallback still respects configured start time.
            if (titleBgmStartTimeSeconds > 0f)
            {
                SoundManager.Instance.PlayBgmAtTime(titleBgmKey, titleBgmStartTimeSeconds, true);
            }
            else
            {
                SoundManager.Instance.PlayBgm(titleBgmKey, true);
            }
        }

        titleBgmRoutine = null;
    }

    private bool IsTitleBgmPlaying()
    {
        if (SoundManager.Instance == null)
        {
            return false;
        }

        if (!SoundManager.Instance.TryGetCurrentBgmState(out string key, out _))
        {
            return false;
        }

        return string.Equals(key, titleBgmKey, System.StringComparison.Ordinal);
    }

    private IEnumerator FlyKingOut()
    {
        if (kingTransform == null)
        {
            yield break;
        }

        float fromZ = useLocalPositionZ ? kingTransform.localPosition.z : kingTransform.position.z;
        float toZ = kingTargetZ;
        Quaternion baseRotation = kingTransform.rotation;
        float totalSpin = Mathf.Max(0f, kingFlySpinTurns) * 360f;
        float t = 0f;
        while (t < kingFlyDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = kingFlyDuration <= 0f ? 1f : Mathf.Clamp01(t / kingFlyDuration);
            float eased = kingFlyCurve != null ? kingFlyCurve.Evaluate(n) : (n * n);
            float z = Mathf.LerpUnclamped(fromZ, toZ, eased);
            float spinZ = totalSpin * eased;
            kingTransform.rotation = baseRotation * Quaternion.Euler(0f, 0f, spinZ);

            if (useLocalPositionZ)
            {
                Vector3 p = kingTransform.localPosition;
                p.z = z;
                kingTransform.localPosition = p;
            }
            else
            {
                Vector3 p = kingTransform.position;
                p.z = z;
                kingTransform.position = p;
            }

            yield return null;
        }

        if (useLocalPositionZ)
        {
            Vector3 p = kingTransform.localPosition;
            p.z = toZ;
            kingTransform.localPosition = p;
        }
        else
        {
            Vector3 p = kingTransform.position;
            p.z = toZ;
            kingTransform.position = p;
        }

        kingTransform.rotation = baseRotation * Quaternion.Euler(0f, 0f, totalSpin);
    }

    private void EnableTrailEffects()
    {
        if (trailEffectRoot != null)
        {
            trailEffectRoot.SetActive(true);
        }

        if (kingTransform == null)
        {
            return;
        }

        TrailRenderer[] trails = kingTransform.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].gameObject.SetActive(true);
            trails[i].Clear();
            trails[i].emitting = true;
        }

        ParticleSystem[] particles = kingTransform.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].gameObject.SetActive(true);
            particles[i].Play(true);
        }
    }

    private void AutoBind()
    {
        if (startButton == null)
        {
            startButton = GetComponentInChildren<Button>(true);
        }

        if (exitButton == null)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "ExitBtn" || t.hideFlags != HideFlags.None)
                {
                    continue;
                }

                exitButton = t.GetComponent<Button>();
                if (exitButton != null)
                {
                    break;
                }
            }
        }

        if (kingTransform == null)
        {
            GameObject kingObj = GameObject.Find("King");
            if (kingObj != null)
            {
                kingTransform = kingObj.transform;
            }
        }

        if (loadingTransition == null)
        {
            loadingTransition = LoadingTransitionManager.Instance;
        }
    }

    private bool IsHoveringKing()
    {
        Camera cam = Camera.main;
        if (cam == null || kingTransform == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            return false;
        }

        Transform t = hit.collider != null ? hit.collider.transform : null;
        if (t == null)
        {
            return false;
        }

        return t == kingTransform || t.IsChildOf(kingTransform) || kingTransform.IsChildOf(t);
    }

    private void TryAutoBindBestScoreBox()
    {
        if (bestScoreBox != null || string.IsNullOrWhiteSpace(bestScoreBoxName))
        {
            return;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != bestScoreBoxName)
            {
                continue;
            }

            if (t.hideFlags != HideFlags.None)
            {
                continue;
            }

            bestScoreBox = t.gameObject;
            return;
        }
    }

    private void TryAutoBindBestScoreText()
    {
        if (bestScoreTxt != null)
        {
            return;
        }

        if (bestScoreBox != null)
        {
            bestScoreTxt = bestScoreBox.GetComponentInChildren<TMP_Text>(true);
            if (bestScoreTxt != null)
            {
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(bestScoreTxtName))
        {
            return;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != bestScoreTxtName || t.hideFlags != HideFlags.None)
            {
                continue;
            }

            bestScoreTxt = t.GetComponent<TMP_Text>();
            if (bestScoreTxt != null)
            {
                return;
            }
        }
    }

    private void RefreshBestScoreText()
    {
        if (bestScoreTxt == null)
        {
            return;
        }

        int best = PlayerPrefs.GetInt(GameManager.BestKillPrefKey, 0);
        bestScoreTxt.text = $"<size=48>{best}</size> <color=#46D7EE>Kill</color>";
    }

    private void ResetBestKillData()
    {
        PlayerPrefs.SetInt(GameManager.BestKillPrefKey, 0);
        PlayerPrefs.Save();
        RefreshBestScoreText();
    }

    private static void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
