using System;
using UnityEngine;

public class ExperienceSystem : MonoBehaviour
{
    [SerializeField] private int level = 1;
    [SerializeField] private int currentExp;
    [SerializeField] private int baseExpToLevel = 10;
    [SerializeField] private int expGrowthPerLevel = 5;

    public int Level => level;
    public int CurrentExp => currentExp;
    public int RequiredExp => baseExpToLevel + (level - 1) * expGrowthPerLevel;

    public event Action<int> OnLevelUp;
    public event Action<int, int, int> OnExpChanged;

    public void AddExp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExp += amount;

        while (currentExp >= RequiredExp)
        {
            currentExp -= RequiredExp;
            level++;
            OnLevelUp?.Invoke(level);
        }

        OnExpChanged?.Invoke(level, currentExp, RequiredExp);
    }
}
