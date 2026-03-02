using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private SoundData soundData;

    [Header("Players")]
    [SerializeField] private AudioSource bgmPlayer;
    [SerializeField] private AudioSource sfxPlayer;
    [SerializeField] private AudioSource sfxPlayerLayered;
    [Header("SFX Voices")]
    [SerializeField] [Min(1)] private int sfxVoiceCount = 10;
    [SerializeField] private Transform sfxVoiceRoot;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float masterBgmVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float masterSfxVolume = 1f;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [Header("BGM Transition")]
    [SerializeField] private bool smoothBgmTransition = true;
    [SerializeField] [Min(0f)] private float bgmFadeDuration = 0.35f;

    private string currentBgmKey;
    private float currentBgmEntryVolume = 1f;
    private Coroutine bgmTransitionCoroutine;
    private readonly List<AudioSource> sfxVoices = new();
    private int sfxVoiceCursor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsurePlayers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Reset()
    {
        EnsurePlayers();
    }

    public void SetSoundData(SoundData data)
    {
        soundData = data;
    }

    public void PlayBgm(string key, bool restartIfSame = false)
    {
        PlayBgmInternal(key, restartIfSame, -1f);
    }

    public void PlayBgmAtTime(string key, float timeSeconds, bool restartIfSame = true)
    {
        PlayBgmInternal(key, restartIfSame, Mathf.Max(0f, timeSeconds));
    }

    public bool TryGetCurrentBgmState(out string key, out float timeSeconds)
    {
        key = currentBgmKey;
        timeSeconds = 0f;
        if (bgmPlayer == null || bgmPlayer.clip == null || string.IsNullOrWhiteSpace(currentBgmKey))
        {
            return false;
        }

        if (!bgmPlayer.isPlaying)
        {
            return false;
        }

        timeSeconds = Mathf.Max(0f, bgmPlayer.time);
        return true;
    }

    private void PlayBgmInternal(string key, bool restartIfSame, float startTimeSeconds)
    {
        if (!TryGetBgmEntry(key, out SoundData.SoundEntry entry))
        {
            return;
        }

        if (!restartIfSame && bgmPlayer.isPlaying && currentBgmKey == key)
        {
            return;
        }

        if (bgmTransitionCoroutine != null)
        {
            StopCoroutine(bgmTransitionCoroutine);
            bgmTransitionCoroutine = null;
        }

        bool shouldFade = smoothBgmTransition && bgmFadeDuration > 0f && bgmPlayer.isPlaying && bgmPlayer.clip != null;
        if (!shouldFade)
        {
            ApplyBgmEntry(entry, key, true, startTimeSeconds);
            return;
        }

        bgmTransitionCoroutine = StartCoroutine(FadeToBgm(entry, key, startTimeSeconds));
    }

    public void StopBgm()
    {
        if (bgmTransitionCoroutine != null)
        {
            StopCoroutine(bgmTransitionCoroutine);
            bgmTransitionCoroutine = null;
        }

        currentBgmKey = string.Empty;
        currentBgmEntryVolume = 1f;
        bgmPlayer.Stop();
        bgmPlayer.clip = null;
    }

    public void PlaySfx(string key, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (!TryGetSfxEntry(key, out SoundData.SoundEntry entry))
        {
            return;
        }

        PlaySfxEntry(entry, volumeScale, pitchScale);
    }

    public void PlaySfxLayered(string key, float volumeScale = 1f, float pitchScale = 1f)
    {
        PlaySfx(key, volumeScale, pitchScale);
    }

    public void SetBgmVolume(float volume)
    {
        masterBgmVolume = Mathf.Clamp01(volume);
        if (bgmPlayer != null && bgmPlayer.clip != null)
        {
            bgmPlayer.volume = Mathf.Clamp01(currentBgmEntryVolume * masterBgmVolume);
        }
    }

    public void SetSfxVolume(float volume)
    {
        masterSfxVolume = Mathf.Clamp01(volume);
    }

    private bool TryGetBgmEntry(string key, out SoundData.SoundEntry entry)
    {
        if (!ValidateRequest(key))
        {
            entry = null;
            return false;
        }

        if (!soundData.TryGetBgm(key, out entry) || entry.clip == null)
        {
            Debug.LogWarning($"[SoundManager] Missing BGM key: {key}");
            return false;
        }

        return true;
    }

    private bool TryGetSfxEntry(string key, out SoundData.SoundEntry entry)
    {
        if (!ValidateRequest(key))
        {
            entry = null;
            return false;
        }

        if (!soundData.TryGetSfx(key, out entry) || entry.clip == null)
        {
            Debug.LogWarning($"[SoundManager] Missing SFX key: {key}");
            return false;
        }

        return true;
    }

    private bool ValidateRequest(string key)
    {
        EnsurePlayers();

        if (soundData == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return true;
    }

    private void EnsurePlayers()
    {
        if (bgmPlayer == null)
        {
            bgmPlayer = GetOrCreatePlayer("BgmPlayer");
        }
        ConfigureBgmSource(bgmPlayer);

        if (sfxPlayer == null)
        {
            sfxPlayer = GetOrCreatePlayer("SfxPlayer");
        }
        ConfigureSfxSource(sfxPlayer);

        if (sfxPlayerLayered == null)
        {
            sfxPlayerLayered = GetOrCreatePlayer("SfxPlayerLayered");
        }
        ConfigureSfxSource(sfxPlayerLayered);

        EnsureSfxVoicePool();
    }

    private void EnsureSfxVoicePool()
    {
        sfxVoices.Clear();
        if (sfxPlayer != null)
        {
            sfxVoices.Add(sfxPlayer);
        }

        if (sfxPlayerLayered != null && sfxPlayerLayered != sfxPlayer)
        {
            sfxVoices.Add(sfxPlayerLayered);
        }

        if (sfxVoiceRoot == null)
        {
            Transform found = transform.Find("SfxVoices");
            if (found == null)
            {
                GameObject go = new GameObject("SfxVoices");
                go.transform.SetParent(transform, false);
                sfxVoiceRoot = go.transform;
            }
            else
            {
                sfxVoiceRoot = found;
            }
        }

        int requiredCount = Mathf.Max(1, sfxVoiceCount);
        for (int i = sfxVoices.Count; i < requiredCount; i++)
        {
            GameObject go = new GameObject($"SfxVoice_{i:00}");
            go.transform.SetParent(sfxVoiceRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            ConfigureSfxSource(source);
            sfxVoices.Add(source);
        }

        if (sfxVoiceCursor >= sfxVoices.Count)
        {
            sfxVoiceCursor = 0;
        }
    }

    private static void ConfigureSfxSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    private static void ConfigureBgmSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    private void PlaySfxEntry(SoundData.SoundEntry entry, float volumeScale, float pitchScale)
    {
        if (entry == null || entry.clip == null)
        {
            return;
        }

        EnsurePlayers();
        AudioSource player = AcquireSfxVoice(entry.priority);
        if (player == null)
        {
            return;
        }

        float pitch = Mathf.Clamp(Mathf.Max(0.1f, entry.pitch) * pitchScale, 0.1f, 3f);
        float volume = Mathf.Clamp(entry.volume * masterSfxVolume * volumeScale, 0f, 1.5f);
        player.priority = Mathf.Clamp(entry.priority, 0, 256);
        player.pitch = pitch;
        player.loop = false;
        player.clip = entry.clip;
        player.volume = volume;
        player.Play();
    }

    private AudioSource AcquireSfxVoice(int requestedPriority)
    {
        if (sfxVoices.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < sfxVoices.Count; i++)
        {
            AudioSource voice = sfxVoices[i];
            if (voice != null && !voice.isPlaying)
            {
                return voice;
            }
        }

        int reqPriority = Mathf.Clamp(requestedPriority, 0, 256);
        int stealIndex = -1;
        int stealPriority = int.MinValue;
        for (int i = 0; i < sfxVoices.Count; i++)
        {
            AudioSource voice = sfxVoices[i];
            if (voice == null)
            {
                continue;
            }

            int p = voice.priority;
            if (p > stealPriority)
            {
                stealPriority = p;
                stealIndex = i;
            }
        }

        if (stealIndex >= 0 && reqPriority <= stealPriority)
        {
            AudioSource voice = sfxVoices[stealIndex];
            voice.Stop();
            sfxVoiceCursor = (stealIndex + 1) % sfxVoices.Count;
            return voice;
        }

        AudioSource fallback = sfxVoices[sfxVoiceCursor % sfxVoices.Count];
        sfxVoiceCursor = (sfxVoiceCursor + 1) % sfxVoices.Count;
        fallback?.Stop();
        return fallback;
    }

    private AudioSource GetOrCreatePlayer(string childName)
    {
        Transform child = transform.Find(childName);
        AudioSource source = null;
        if (child != null)
        {
            source = child.GetComponent<AudioSource>();
        }

        if (source == null)
        {
            GameObject go = child != null ? child.gameObject : new GameObject(childName);
            go.transform.SetParent(transform, false);
            source = go.GetComponent<AudioSource>();
            if (source == null)
            {
                source = go.AddComponent<AudioSource>();
            }
        }

        return source;
    }

    private void ApplyBgmEntry(SoundData.SoundEntry entry, string key, bool playFromStart, float startTimeSeconds)
    {
        currentBgmKey = key;
        currentBgmEntryVolume = Mathf.Clamp01(entry.volume);
        bgmPlayer.clip = entry.clip;
        bgmPlayer.loop = entry.loop;
        bgmPlayer.pitch = Mathf.Clamp(entry.pitch, 0.1f, 3f);
        bgmPlayer.volume = Mathf.Clamp01(currentBgmEntryVolume * masterBgmVolume);
        if (playFromStart)
        {
            bgmPlayer.Play();
            if (startTimeSeconds >= 0f && bgmPlayer.clip != null)
            {
                float targetTime = startTimeSeconds;
                if (bgmPlayer.clip.length > 0f)
                {
                    targetTime = bgmPlayer.loop
                        ? Mathf.Repeat(startTimeSeconds, bgmPlayer.clip.length)
                        : Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, bgmPlayer.clip.length - 0.01f));
                }

                bgmPlayer.time = targetTime;
            }
        }
    }

    private System.Collections.IEnumerator FadeToBgm(SoundData.SoundEntry nextEntry, string nextKey, float startTimeSeconds)
    {
        float initialVolume = bgmPlayer.volume;
        float elapsed = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / bgmFadeDuration);
            bgmPlayer.volume = Mathf.Lerp(initialVolume, 0f, t);
            yield return null;
        }

        ApplyBgmEntry(nextEntry, nextKey, true, startTimeSeconds);
        bgmPlayer.volume = 0f;

        float targetVolume = Mathf.Clamp01(currentBgmEntryVolume * masterBgmVolume);
        elapsed = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / bgmFadeDuration);
            bgmPlayer.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        bgmPlayer.volume = targetVolume;
        bgmTransitionCoroutine = null;
    }
}
