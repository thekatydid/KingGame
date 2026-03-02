using System;
using UnityEngine;

[DisallowMultipleComponent]
public class KingQueenSkillController : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField, Min(1)] private int durationTurns = 3;
    [SerializeField] private bool autoFindTurnManager = true;
    [Header("BGM")]
    [SerializeField] private string skillBgmKey = "Skill";
    [SerializeField] private bool resumeStageBgmOnSkillEnd = true;
    [SerializeField] private string fallbackResumeBgmKey = "MainBGM";
    [Header("Background FX While Skill Active")]
    [SerializeField] private bool applyBackgroundFxOnSkill = true;
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private bool autoUseSkyboxMaterial = true;
    [SerializeField] private Color skillColor = new(0.07843137f, 0.16470589f, 1f, 1f); // #142AFF
    [SerializeField] private float skillRippleSpeedValue = 2f;
    [SerializeField] private float skillHorizonGlowValue = 2f;
    [Header("King Material While Skill Active")]
    [SerializeField] private bool applyKingMaterialOnSkill = true;
    [SerializeField] private Material kingSkillMaterial;
    [SerializeField] private string kingIconRootName = "KingIcon";

    private int activeUntilTurnExclusive = -1;
    private bool lastActiveState;
    private bool hasSavedBgmState;
    private string savedBgmKey;
    private float savedBgmTime;
    private bool hasSavedBackgroundState;
    private bool savedHasColor;
    private bool savedHasRippleSpeed;
    private bool savedHasHorizonGlow;
    private Color savedColor;
    private float savedRippleSpeed;
    private float savedHorizonGlow;
    private Material runtimeBackgroundMaterial;
    private ChessPiece appliedKingPiece;
    private bool hasAppliedKingMaterial;
    private readonly System.Collections.Generic.List<RendererMaterialCache> kingRendererCaches = new();

    private static readonly int ColorCId = Shader.PropertyToID("_ColorC");
    private static readonly int RippleSpeedId = Shader.PropertyToID("_RippleSpeed");
    private static readonly int HorizonGlowId = Shader.PropertyToID("_HorizonGlow");

    private struct RendererMaterialCache
    {
        public Renderer Renderer;
        public Material[] OriginalSharedMaterials;
    }

    public bool IsActive
    {
        get
        {
            if (turnManager == null)
            {
                return false;
            }

            return activeUntilTurnExclusive > 0 && turnManager.TurnCount < activeUntilTurnExclusive;
        }
    }

    public PieceType CurrentKingRuleType => IsActive ? PieceType.Queen : PieceType.King;

    public int GetRemainingTurns(int currentTurn)
    {
        int now = Mathf.Max(1, currentTurn);
        if (activeUntilTurnExclusive <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, activeUntilTurnExclusive - now);
    }

    public void RestoreRuntimeState(int remainingTurns, int currentTurn)
    {
        bool wasActive = IsActive;
        int now = Mathf.Max(1, currentTurn);
        int remain = Mathf.Max(0, remainingTurns);
        activeUntilTurnExclusive = remain > 0 ? now + remain : -1;
        bool isActiveNow = IsActive;

        if (wasActive && !isActiveNow)
        {
            RestoreBackgroundFx();
            RestoreKingMaterialFx();
            TryResumeBgmAfterSkill();
        }
        else if (!wasActive && isActiveNow)
        {
            ApplyBackgroundFx();
            ApplyKingMaterialFx();
            TryPlaySkillBgm();
        }
        else if (isActiveNow)
        {
            ApplyKingMaterialFx();
        }

        lastActiveState = isActiveNow;
    }

    private void OnEnable()
    {
        EnsureReferences();
        BindTurnManager();
    }

    private void OnDisable()
    {
        UnbindTurnManager();
        RestoreBackgroundFx();
        RestoreKingMaterialFx();
    }

    private void Update()
    {
        if (turnManager == null && autoFindTurnManager)
        {
            EnsureReferences();
            BindTurnManager();
        }

        bool isActiveNow = IsActive;
        if (lastActiveState && !isActiveNow)
        {
            RestoreBackgroundFx();
            RestoreKingMaterialFx();
            TryResumeBgmAfterSkill();
        }
        else if (isActiveNow)
        {
            ApplyKingMaterialFx();
        }

        lastActiveState = isActiveNow;
    }

    private void EnsureReferences()
    {
        if (turnManager == null && autoFindTurnManager)
        {
            turnManager = FindFirstObjectByType<TurnManager>();
        }
    }

    private void BindTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.OnKingSkillActivated -= HandleKingSkillActivated;
        turnManager.OnKingSkillActivated += HandleKingSkillActivated;
    }

    private void UnbindTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.OnKingSkillActivated -= HandleKingSkillActivated;
    }

    private void HandleKingSkillActivated()
    {
        if (turnManager == null)
        {
            return;
        }

        CacheCurrentBgmState();
        int now = Mathf.Max(1, turnManager.TurnCount);
        activeUntilTurnExclusive = now + Mathf.Max(1, durationTurns);
        lastActiveState = IsActive;
        ApplyBackgroundFx();
        ApplyKingMaterialFx();
        TryPlaySkillBgm();
    }

    private void TryPlaySkillBgm()
    {
        if (SoundManager.Instance == null || string.IsNullOrWhiteSpace(skillBgmKey))
        {
            return;
        }

        SoundManager.Instance.PlayBgm(skillBgmKey, true);
    }

    private void TryResumeBgmAfterSkill()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            return;
        }

        if (SoundManager.Instance != null && hasSavedBgmState && !string.IsNullOrWhiteSpace(savedBgmKey))
        {
            SoundManager.Instance.PlayBgmAtTime(savedBgmKey, savedBgmTime, true);
            hasSavedBgmState = false;
            return;
        }

        if (resumeStageBgmOnSkillEnd)
        {
            StageManager stageManager = FindFirstObjectByType<StageManager>();
            if (stageManager != null)
            {
                stageManager.PlayStageBgmNow(true);
                return;
            }
        }

        if (SoundManager.Instance == null || string.IsNullOrWhiteSpace(fallbackResumeBgmKey))
        {
            return;
        }

        SoundManager.Instance.PlayBgm(fallbackResumeBgmKey, true);
    }

    private void CacheCurrentBgmState()
    {
        hasSavedBgmState = false;
        savedBgmKey = string.Empty;
        savedBgmTime = 0f;

        if (SoundManager.Instance == null)
        {
            return;
        }

        if (!SoundManager.Instance.TryGetCurrentBgmState(out string key, out float timeSeconds))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(skillBgmKey) && string.Equals(key, skillBgmKey, System.StringComparison.Ordinal))
        {
            return;
        }

        hasSavedBgmState = true;
        savedBgmKey = key;
        savedBgmTime = Mathf.Max(0f, timeSeconds);
    }

    private void ApplyBackgroundFx()
    {
        if (!applyBackgroundFxOnSkill)
        {
            return;
        }

        Material target = ResolveBackgroundMaterial();
        if (target == null)
        {
            return;
        }

        runtimeBackgroundMaterial = target;
        hasSavedBackgroundState = true;
        savedHasColor = target.HasProperty(ColorCId);
        savedHasRippleSpeed = target.HasProperty(RippleSpeedId);
        savedHasHorizonGlow = target.HasProperty(HorizonGlowId);
        if (savedHasColor)
        {
            savedColor = target.GetColor(ColorCId);
            target.SetColor(ColorCId, skillColor);
        }

        if (savedHasRippleSpeed)
        {
            savedRippleSpeed = target.GetFloat(RippleSpeedId);
            target.SetFloat(RippleSpeedId, skillRippleSpeedValue);
        }

        if (savedHasHorizonGlow)
        {
            savedHorizonGlow = target.GetFloat(HorizonGlowId);
            target.SetFloat(HorizonGlowId, skillHorizonGlowValue);
        }
    }

    private void RestoreBackgroundFx()
    {
        if (!hasSavedBackgroundState || runtimeBackgroundMaterial == null)
        {
            hasSavedBackgroundState = false;
            runtimeBackgroundMaterial = null;
            return;
        }

        if (savedHasColor && runtimeBackgroundMaterial.HasProperty(ColorCId))
        {
            runtimeBackgroundMaterial.SetColor(ColorCId, savedColor);
        }

        if (savedHasRippleSpeed && runtimeBackgroundMaterial.HasProperty(RippleSpeedId))
        {
            runtimeBackgroundMaterial.SetFloat(RippleSpeedId, savedRippleSpeed);
        }

        if (savedHasHorizonGlow && runtimeBackgroundMaterial.HasProperty(HorizonGlowId))
        {
            runtimeBackgroundMaterial.SetFloat(HorizonGlowId, savedHorizonGlow);
        }

        hasSavedBackgroundState = false;
        runtimeBackgroundMaterial = null;
    }

    private Material ResolveBackgroundMaterial()
    {
        if (backgroundMaterial != null)
        {
            return backgroundMaterial;
        }

        if (autoUseSkyboxMaterial)
        {
            return RenderSettings.skybox;
        }

        return null;
    }

    private void ApplyKingMaterialFx()
    {
        if (!applyKingMaterialOnSkill || kingSkillMaterial == null)
        {
            return;
        }

        ChessPiece king = ResolveKingPiece();
        if (king == null)
        {
            return;
        }

        if (hasAppliedKingMaterial && appliedKingPiece == king)
        {
            return;
        }

        if (hasAppliedKingMaterial && appliedKingPiece != king)
        {
            RestoreKingMaterialFx();
        }

        Renderer[] renderers = king.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        kingRendererCaches.Clear();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (ShouldSkipKingMaterialRenderer(king.transform, renderer))
            {
                continue;
            }

            Material[] original = renderer.sharedMaterials;
            Material[] copied = original != null ? (Material[])original.Clone() : new Material[0];
            kingRendererCaches.Add(new RendererMaterialCache
            {
                Renderer = renderer,
                OriginalSharedMaterials = copied
            });

            if (copied.Length <= 0)
            {
                continue;
            }

            Material[] overrideMats = new Material[copied.Length];
            for (int m = 0; m < overrideMats.Length; m++)
            {
                overrideMats[m] = kingSkillMaterial;
            }

            renderer.sharedMaterials = overrideMats;
        }

        appliedKingPiece = king;
        hasAppliedKingMaterial = true;
    }

    private bool ShouldSkipKingMaterialRenderer(Transform kingRoot, Renderer renderer)
    {
        if (kingRoot == null || renderer == null || string.IsNullOrWhiteSpace(kingIconRootName))
        {
            return false;
        }

        Transform t = renderer.transform;
        while (t != null && t != kingRoot)
        {
            if (string.Equals(t.name, kingIconRootName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    private void RestoreKingMaterialFx()
    {
        if (!hasAppliedKingMaterial)
        {
            return;
        }

        for (int i = 0; i < kingRendererCaches.Count; i++)
        {
            RendererMaterialCache cache = kingRendererCaches[i];
            if (cache.Renderer == null)
            {
                continue;
            }

            cache.Renderer.sharedMaterials = cache.OriginalSharedMaterials ?? new Material[0];
        }

        kingRendererCaches.Clear();
        appliedKingPiece = null;
        hasAppliedKingMaterial = false;
    }

    private ChessPiece ResolveKingPiece()
    {
        if (turnManager != null && turnManager.KingPiece != null)
        {
            return turnManager.KingPiece;
        }

        TurnManager tm = FindFirstObjectByType<TurnManager>();
        if (tm != null)
        {
            turnManager = tm;
            return tm.KingPiece;
        }

        return null;
    }
}
