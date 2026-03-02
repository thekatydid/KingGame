using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundData", menuName = "ChessSurvivor/Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    [Serializable]
    public class SoundEntry
    {
        public string key;
        public AudioClip clip;
        [Range(0f, 1.5f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0, 256)] public int priority = 128;
        public bool loop;
    }

    [Header("Background Music")]
    [SerializeField] private List<SoundEntry> bgmEntries = new();

    [Header("Sound Effects")]
    [SerializeField] private List<SoundEntry> sfxEntries = new();

    private readonly Dictionary<string, SoundEntry> bgmMap = new();
    private readonly Dictionary<string, SoundEntry> sfxMap = new();

    public bool TryGetBgm(string key, out SoundEntry entry)
    {
        EnsureCache();
        return bgmMap.TryGetValue(key, out entry);
    }

    public bool TryGetSfx(string key, out SoundEntry entry)
    {
        EnsureCache();
        return sfxMap.TryGetValue(key, out entry);
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    private void EnsureCache()
    {
        if (bgmMap.Count == 0 && sfxMap.Count == 0)
        {
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        bgmMap.Clear();
        sfxMap.Clear();
        BuildMap(bgmEntries, bgmMap);
        BuildMap(sfxEntries, sfxMap);
    }

    private static void BuildMap(List<SoundEntry> source, Dictionary<string, SoundEntry> target)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            SoundEntry entry = source[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            target[entry.key] = entry;
        }
    }
}
