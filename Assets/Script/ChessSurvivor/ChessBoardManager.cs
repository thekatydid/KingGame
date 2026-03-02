using System.Collections.Generic;
using System;
using UnityEngine;

[System.Serializable]
public struct PiecePrefabEntry
{
    public PieceType pieceType;
    public GameObject prefab;
}

public enum PieceMoveStyle
{
    Slide,
    Jump,
    SkillDive
}

public enum EasePreset
{
    Linear,
    InOut,
    In,
    Out
}

public class ChessBoardManager : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int width = 40;
    [SerializeField] private int height = 40;
    [SerializeField] private float cellSize = 1.5f;
    [SerializeField] private bool autoCenterOrigin = true;
    [SerializeField] private Vector3 origin = new(-29.25f, 0f, -29.25f);

    [Header("Piece Materials")]
    [SerializeField] private Material allyMaterial;
    [SerializeField] private Material enemyMaterial;
    [SerializeField] private Material kingMaterial;
    [SerializeField] private Color allyFallbackColor = Color.black;
    [SerializeField] private Color enemyFallbackColor = Color.white;
    [SerializeField] private Color kingFallbackColor = Color.red;
    [Header("Fallback Shader")]
    [SerializeField] private bool useToonFallbackForPieces = true;
    [SerializeField] private Shader toonFallbackShader;

    [Header("Piece Prefabs")]
    [SerializeField] private List<PiecePrefabEntry> piecePrefabs = new();
    [SerializeField] private float prefabPieceYOffset = 0f;
    [SerializeField] private float fallbackPieceYOffset = 0.6f;
    [Header("FX")]
    [SerializeField] private GameObject kingKillImpactPrefab;
    [SerializeField] private Vector3 kingKillImpactOffset = new(0f, 1f, 0f);
    [SerializeField] private float kingKillImpactLifetime = 2f;
    [Header("Piece Move Motion")]
    [SerializeField] private bool animatePieceMove = true;
    [SerializeField] private float defaultMoveSlideDuration = 0.12f;
    [SerializeField] private AnimationCurve moveSlideEase = null;
    [SerializeField] private float moveJumpHeight = 1.8f;
    [SerializeField] private AnimationCurve moveArcProfile = null;
    [SerializeField] private float moveJumpTiltX = 30f;
    [SerializeField] private float moveJumpSpinDegrees = 360f;
    [SerializeField] private bool moveJumpSpinOnlyOnRise = true;
    [Header("Skill Dive Motion")]
    [SerializeField] private float skillDiveJumpHeight = 6f;
    [SerializeField] private float skillDiveApexHoldTime = 0.1f;
    [SerializeField, Range(0.1f, 0.9f)] private float skillDiveRisePortion = 0.48f;
    [SerializeField] private float skillDiveSpinTurns = 4f;
    [SerializeField] private float skillDiveStartXOffset = 10f;
    [SerializeField] private EasePreset skillDiveEasePreset = EasePreset.InOut;
    [SerializeField] private Vector3 skillDiveFlightScale = new(0.6f, 0.6f, 2f);
    [SerializeField] private Vector3 skillDiveHitScale = new(1.5f, 1.5f, 0.2f);
    [SerializeField] private float skillDiveHitScaleHold = 0.08f;
    [SerializeField] private float skillDiveRecoverDuration = 0.12f;
    [SerializeField] private Vector3 skillDiveLocalBottomAxis = new(0f, 0f, -1f);
    [Header("Move SFX")]
    [SerializeField] private bool playMoveSfx = true;
    [SerializeField] private string moveSfxKey = "chop";
    [Header("King Facing")]
    [SerializeField] private bool applyKingFacingOnSpawn = false;
    [SerializeField] private Vector3 kingFacingEuler = new(0f, 0f, 0f);
    [Header("Material Exclusions")]
    [SerializeField] private string kingIconRootName = "KingIcon";

    [Header("Tile Visual")]
    [SerializeField] private bool buildTileGridVisual = true;
    [SerializeField] private bool rebuildBoardVisualWhenCountMismatch = true;
    [SerializeField] private float tileHeight = 0.06f;
    [SerializeField] private bool buildBoardBasePlate = true;
    [SerializeField] private float basePlateThickness = 0.2f;
    [SerializeField] private float basePlateYOffset = -0.14f;
    [SerializeField] private Color basePlateColor = new(0.06f, 0.04f, 0.04f, 1f);
    [SerializeField] private Color tileColorA = new(0.16f, 0.16f, 0.18f, 1f);
    [SerializeField] private Color tileColorB = new(0.21f, 0.21f, 0.24f, 1f);
    [SerializeField] private Color tileHighlightColor = new(0.0f, 1.0f, 0.0f, 1f);
    [SerializeField] private Color captureBorderColor = new(1.0f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color enemyThreatPreviewColor = new(1.0f, 0.7f, 0.0f, 0.95f);

    [Header("Debug")]
    [SerializeField] private bool drawGridGizmos = true;

    private readonly Dictionary<BoardCoord, ChessPiece> pieces = new();
    private readonly Dictionary<BoardCoord, Renderer> tileRenderers = new();
    private readonly Dictionary<BoardCoord, GameObject> highlightOverlays = new();
    private readonly Dictionary<BoardCoord, GameObject> enemyThreatOverlays = new();
    private readonly Dictionary<BoardCoord, LineRenderer> captureBorders = new();
    private readonly HashSet<BoardCoord> highlightedCoords = new();
    private readonly HashSet<BoardCoord> enemyThreatCoords = new();
    private readonly HashSet<BoardCoord> captureBorderCoords = new();
    private readonly Dictionary<PieceType, GameObject> piecePrefabMap = new();
    private readonly Dictionary<ChessPiece, float> pieceYOffsets = new();
    private readonly Dictionary<ChessPiece, Coroutine> moveCoroutines = new();
    private Transform tileRoot;
    private Transform highlightRoot;
    private Transform basePlateRoot;
    private Material runtimeAllyMaterial;
    private Material runtimeEnemyMaterial;
    private Material runtimeKingMaterial;
    private Material runtimeHighlightMaterial;
    private Material runtimeThreatPreviewMaterial;
    private Material runtimeCaptureBorderMaterial;
    private Material runtimeBasePlateMaterial;

    public int Width => width;
    public int Height => height;
    public IEnumerable<ChessPiece> AllPieces => pieces.Values;

    private void Awake()
    {
        if (autoCenterOrigin)
        {
            origin = new Vector3(-((width - 1) * cellSize) * 0.5f, 0f, -((height - 1) * cellSize) * 0.5f);
        }

        EnsurePieceMaterials();
        RebuildPiecePrefabMap();
        BindExistingBoardVisuals();
        ValidateOrRebuildBoardVisuals();
        EnsureBoardBasePlate();
        if (moveSlideEase == null || moveSlideEase.length == 0)
        {
            moveSlideEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (moveArcProfile == null || moveArcProfile.length == 0)
        {
            moveArcProfile = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 4f),
                new Keyframe(0.35f, 1f, 0f, -2f),
                new Keyframe(1f, 0f, -6f, 0f));
        }

    }

    public bool IsInside(BoardCoord c)
    {
        return c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;
    }

    public bool IsOccupied(BoardCoord c)
    {
        return pieces.ContainsKey(c);
    }

    public ChessPiece GetPieceAt(BoardCoord c)
    {
        pieces.TryGetValue(c, out ChessPiece piece);
        return piece;
    }

    public ChessPiece SpawnPiece(PieceType type, Team team, BoardCoord coord)
    {
        if (!IsInside(coord) || IsOccupied(coord))
        {
            return null;
        }

        GameObject sourcePrefab = GetPiecePrefab(type);
        GameObject go = sourcePrefab != null ? Instantiate(sourcePrefab) : CreateFallbackPieceVisual(type);
        go.name = $"{team}_{type}_{coord.x}_{coord.y}";
        float yOffset = sourcePrefab != null ? prefabPieceYOffset : fallbackPieceYOffset;
        go.transform.position = CoordToWorld(coord) + new Vector3(0f, yOffset, 0f);
        ApplyDefaultFacing(type, go.transform);
        if (sourcePrefab == null)
        {
            go.transform.localScale = Vector3.one * 0.9f;
        }

        ChessPiece piece = go.GetComponent<ChessPiece>();
        if (piece == null)
        {
            piece = go.AddComponent<ChessPiece>();
        }

        if (go.GetComponent<Collider>() == null)
        {
            go.AddComponent<CapsuleCollider>();
        }

        piece.Initialize(team, type, coord);
        ApplyTeamMaterial(go, piece.PieceType, piece.Team);

        pieces[coord] = piece;
        pieceYOffsets[piece] = yOffset;
        return piece;
    }

    public void ApplyDefaultFacing(ChessPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        ApplyDefaultFacing(piece.PieceType, piece.transform);
    }

    private void ApplyDefaultFacing(PieceType pieceType, Transform tr)
    {
        if (tr == null || !applyKingFacingOnSpawn || pieceType != PieceType.King)
        {
            return;
        }

        Vector3 euler = tr.eulerAngles;
        euler.y = kingFacingEuler.y;
        tr.rotation = Quaternion.Euler(euler);
    }

    public GameObject SpawnPreviewPiece(PieceType type, Team team)
    {
        GameObject sourcePrefab = GetPiecePrefab(type);
        GameObject go = sourcePrefab != null ? Instantiate(sourcePrefab) : CreateFallbackPieceVisual(type);
        go.name = $"Preview_{team}_{type}";

        ChessPiece[] pieceComps = go.GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < pieceComps.Length; i++)
        {
            Destroy(pieceComps[i]);
        }

        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Destroy(rigidbodies[i]);
        }

        ApplyTeamMaterial(go, type, team);
        return go;
    }

    private void ApplyTeamMaterial(GameObject pieceGo, PieceType pieceType, Team team)
    {
        if (pieceGo == null)
        {
            return;
        }

        Renderer[] renderers = pieceGo.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            // Keep VFX renderer materials (trail/particles/lines) authored in prefab.
            if (renderers[i] is TrailRenderer || renderers[i] is ParticleSystemRenderer || renderers[i] is LineRenderer)
            {
                continue;
            }

            if (ShouldSkipTeamMaterialRenderer(pieceGo.transform, renderers[i]))
            {
                continue;
            }

            if (pieceType == PieceType.King)
            {
                renderers[i].sharedMaterial = kingMaterial != null ? kingMaterial : runtimeKingMaterial;
            }
            else if (team == Team.Ally)
            {
                renderers[i].sharedMaterial = allyMaterial != null ? allyMaterial : runtimeAllyMaterial;
            }
            else
            {
                renderers[i].sharedMaterial = enemyMaterial != null ? enemyMaterial : runtimeEnemyMaterial;
            }
        }
    }

    private bool ShouldSkipTeamMaterialRenderer(Transform pieceRoot, Renderer renderer)
    {
        if (pieceRoot == null || renderer == null)
        {
            return false;
        }

        // Keep KingIcon (and its children) authored material/color untouched.
        if (!string.IsNullOrWhiteSpace(kingIconRootName))
        {
            Transform t = renderer.transform;
            while (t != null && t != pieceRoot)
            {
                if (string.Equals(t.name, kingIconRootName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                t = t.parent;
            }
        }

        return false;
    }

    private GameObject GetPiecePrefab(PieceType type)
    {
        return piecePrefabMap.TryGetValue(type, out GameObject prefab) ? prefab : null;
    }

    private GameObject CreateFallbackPieceVisual(PieceType type)
    {
        PrimitiveType primitive = type == PieceType.Knight ? PrimitiveType.Cylinder : PrimitiveType.Capsule;
        return GameObject.CreatePrimitive(primitive);
    }

    private void RebuildPiecePrefabMap()
    {
        piecePrefabMap.Clear();
        for (int i = 0; i < piecePrefabs.Count; i++)
        {
            if (piecePrefabs[i].prefab == null)
            {
                continue;
            }

            piecePrefabMap[piecePrefabs[i].pieceType] = piecePrefabs[i].prefab;
        }
    }

    public bool MovePiece(
        ChessPiece piece,
        BoardCoord target,
        float durationOverride = -1f,
        Action onNearComplete = null,
        PieceMoveStyle moveStyle = PieceMoveStyle.Slide)
    {
        if (piece == null || !IsInside(target) || IsOccupied(target))
        {
            return false;
        }

        BoardCoord fromCoord = piece.Coord;
        float yOffset = pieceYOffsets.TryGetValue(piece, out float value) ? value : prefabPieceYOffset;
        Vector3 fromPos = CoordToWorld(fromCoord) + new Vector3(0f, yOffset, 0f);
        Vector3 toPos = CoordToWorld(target) + new Vector3(0f, yOffset, 0f);

        pieces.Remove(piece.Coord);
        piece.SetCoord(target);
        pieces[target] = piece;
        TryPlayMoveSfx();

        float duration = durationOverride >= 0f ? durationOverride : defaultMoveSlideDuration;
        if (!animatePieceMove || duration <= 0.0001f)
        {
            piece.transform.position = toPos;
            onNearComplete?.Invoke();
            return true;
        }

        if (moveCoroutines.TryGetValue(piece, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }

        Coroutine co = moveStyle switch
        {
            PieceMoveStyle.Jump => StartCoroutine(JumpPieceTo(piece, fromPos, toPos, duration, onNearComplete)),
            PieceMoveStyle.SkillDive => StartCoroutine(SkillDiveTo(piece, fromPos, toPos, duration, onNearComplete)),
            _ => StartCoroutine(SlidePieceTo(piece, fromPos, toPos, duration, onNearComplete))
        };
        moveCoroutines[piece] = co;
        return true;
    }

    public bool IsPieceMoving(ChessPiece piece)
    {
        return piece != null && moveCoroutines.TryGetValue(piece, out Coroutine running) && running != null;
    }

    private void TryPlayMoveSfx()
    {
        if (!playMoveSfx || string.IsNullOrWhiteSpace(moveSfxKey))
        {
            return;
        }

        SoundManager.Instance?.PlaySfx(moveSfxKey);
    }

    private System.Collections.IEnumerator JumpPieceTo(ChessPiece piece, Vector3 from, Vector3 to, float duration, Action onNearComplete)
    {
        if (piece == null)
        {
            yield break;
        }

        piece.transform.position = from;
        Quaternion baseRotation = piece.transform.rotation;
        bool invoked = false;
        float t = 0f;
        while (t < duration)
        {
            if (piece == null)
            {
                yield break;
            }

            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            float eased = moveSlideEase != null ? moveSlideEase.Evaluate(n) : n;
            Vector3 horizontal = Vector3.LerpUnclamped(from, to, eased);
            float arc01 = moveArcProfile != null ? Mathf.Max(0f, moveArcProfile.Evaluate(n)) : Mathf.Sin(n * Mathf.PI);
            horizontal.y += arc01 * moveJumpHeight;
            piece.transform.position = horizontal;
            float tiltT = Mathf.Clamp01(n / 0.5f);
            float spinT = moveJumpSpinOnlyOnRise ? Mathf.Clamp01(n / 0.5f) : n;
            piece.transform.rotation = ComposeMotionRotation(baseRotation, moveJumpTiltX * tiltT, moveJumpSpinDegrees * spinT);
            if (!invoked && n >= 0.85f)
            {
                invoked = true;
                onNearComplete?.Invoke();
            }

            yield return null;
        }

        if (piece != null)
        {
            piece.transform.position = to;
            piece.transform.rotation = baseRotation;
        }

        if (!invoked)
        {
            onNearComplete?.Invoke();
        }

        if (piece != null)
        {
            moveCoroutines.Remove(piece);
        }
    }

    private System.Collections.IEnumerator SlidePieceTo(ChessPiece piece, Vector3 from, Vector3 to, float duration, Action onNearComplete)
    {
        if (piece == null)
        {
            yield break;
        }

        piece.transform.position = from;
        bool invoked = false;
        float t = 0f;
        while (t < duration)
        {
            if (piece == null)
            {
                yield break;
            }

            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            float eased = moveSlideEase != null ? moveSlideEase.Evaluate(n) : n;
            piece.transform.position = Vector3.LerpUnclamped(from, to, eased);
            if (!invoked && n >= 0.85f)
            {
                invoked = true;
                onNearComplete?.Invoke();
            }

            yield return null;
        }

        if (piece != null)
        {
            piece.transform.position = to;
        }

        if (!invoked)
        {
            onNearComplete?.Invoke();
        }

        if (piece != null)
        {
            moveCoroutines.Remove(piece);
        }
    }

    private System.Collections.IEnumerator SkillDiveTo(ChessPiece piece, Vector3 from, Vector3 to, float duration, Action onNearComplete)
    {
        if (piece == null)
        {
            yield break;
        }

        piece.transform.position = from;
        Quaternion baseRotation = piece.transform.rotation;
        Vector3 baseScale = piece.transform.localScale;
        Vector3 baseEuler = piece.transform.eulerAngles;
        float baseX = NormalizeSignedAngle(baseEuler.x);
        float baseY = NormalizeSignedAngle(baseEuler.y);
        float baseZ = NormalizeSignedAngle(baseEuler.z);
        float spinDegrees = skillDiveSpinTurns * 360f;
        bool invoked = false;

        float totalDuration = Mathf.Max(0.01f, duration);
        float risePortion = Mathf.Clamp(skillDiveRisePortion, 0.1f, 0.9f);
        float riseDuration = Mathf.Max(0.01f, totalDuration * risePortion);
        float dashDuration = Mathf.Max(0.01f, totalDuration - riseDuration);
        Vector3 apex = Vector3.LerpUnclamped(from, to, 0.22f) + Vector3.up * skillDiveJumpHeight;

        float riseT = 0f;
        while (riseT < riseDuration)
        {
            if (piece == null)
            {
                yield break;
            }

            riseT += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(riseT / riseDuration);
            float eased = EvaluateEase(n, skillDiveEasePreset);
            Vector3 pos = Vector3.LerpUnclamped(from, apex, eased);
            piece.transform.position = pos;
            float rotX = Mathf.Lerp(baseX + skillDiveStartXOffset, baseX, n);
            float rotY = baseY + spinDegrees * n;
            piece.transform.rotation = Quaternion.Euler(rotX, rotY, baseZ);
            piece.transform.localScale = baseScale;
            yield return null;
        }

        if (piece == null)
        {
            yield break;
        }

        Vector3 apexPos = apex;

        if (skillDiveApexHoldTime > 0f)
        {
            float hold = 0f;
            while (hold < skillDiveApexHoldTime)
            {
                if (piece == null)
                {
                    yield break;
                }

                hold += Time.unscaledDeltaTime;
                piece.transform.position = apexPos;
                yield return null;
            }
        }

        float dashT = 0f;
        Vector3 localBottomAxis = skillDiveLocalBottomAxis.sqrMagnitude > 0.0001f
            ? skillDiveLocalBottomAxis.normalized
            : Vector3.down;
        Vector3 baseBottomDir = baseRotation * localBottomAxis;
        while (dashT < dashDuration)
        {
            if (piece == null)
            {
                yield break;
            }

            dashT += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(dashT / dashDuration);
            float eased = EvaluateEase(n, skillDiveEasePreset);
            Vector3 pos = Vector3.LerpUnclamped(apexPos, to, eased);
            piece.transform.position = pos;
            Vector3 toTarget = to - pos;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = to - apexPos;
            }

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion alignBottomToTarget = Quaternion.FromToRotation(baseBottomDir, toTarget.normalized);
                piece.transform.rotation = Quaternion.SlerpUnclamped(
                    piece.transform.rotation,
                    alignBottomToTarget * baseRotation,
                    1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            }

            float stretchAlpha = EvaluatePeak01(n, skillDiveEasePreset);
            piece.transform.localScale = new Vector3(
                baseScale.x * Mathf.LerpUnclamped(1f, skillDiveFlightScale.x, stretchAlpha),
                baseScale.y * Mathf.LerpUnclamped(1f, skillDiveFlightScale.y, stretchAlpha),
                baseScale.z * Mathf.LerpUnclamped(1f, skillDiveFlightScale.z, stretchAlpha));
            if (!invoked && n >= 0.92f)
            {
                invoked = true;
                onNearComplete?.Invoke();
            }

            yield return null;
        }

        if (piece != null)
        {
            piece.transform.position = to;
            piece.transform.localScale = new Vector3(
                baseScale.x * skillDiveHitScale.x,
                baseScale.y * skillDiveHitScale.y,
                baseScale.z * skillDiveHitScale.z);
        }

        if (piece != null && skillDiveHitScaleHold > 0f)
        {
            float hitHold = 0f;
            while (hitHold < skillDiveHitScaleHold)
            {
                if (piece == null)
                {
                    yield break;
                }

                hitHold += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (piece != null && skillDiveRecoverDuration > 0f)
        {
            float recoverT = 0f;
            Vector3 hitScale = new(
                baseScale.x * skillDiveHitScale.x,
                baseScale.y * skillDiveHitScale.y,
                baseScale.z * skillDiveHitScale.z);
            while (recoverT < skillDiveRecoverDuration)
            {
                if (piece == null)
                {
                    yield break;
                }

                recoverT += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(recoverT / skillDiveRecoverDuration);
                float eased = EvaluateEase(n, skillDiveEasePreset);
                piece.transform.localScale = Vector3.LerpUnclamped(hitScale, baseScale, eased);
                yield return null;
            }
        }

        if (piece != null)
        {
            piece.transform.rotation = baseRotation;
            piece.transform.localScale = baseScale;
        }

        if (!invoked)
        {
            onNearComplete?.Invoke();
        }

        if (piece != null)
        {
            moveCoroutines.Remove(piece);
        }
    }

    private static Quaternion ComposeMotionRotation(Quaternion baseRotation, float localTiltX, float localSpinY)
    {
        // Apply additive local rotation on top of authored prefab rotation (e.g. X=-90).
        return baseRotation
            * Quaternion.AngleAxis(localTiltX, Vector3.right)
            * Quaternion.AngleAxis(localSpinY, Vector3.up);
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static float EvaluateEase(float t, EasePreset preset)
    {
        t = Mathf.Clamp01(t);
        return preset switch
        {
            EasePreset.Linear => t,
            EasePreset.In => t * t,
            EasePreset.Out => 1f - ((1f - t) * (1f - t)),
            _ => t * t * (3f - (2f * t))
        };
    }

    private static float EvaluatePeak01(float t, EasePreset preset)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0.5f)
        {
            return EvaluateEase(t * 2f, preset);
        }

        return EvaluateEase((1f - t) * 2f, preset);
    }

    public void RemovePiece(ChessPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        pieces.Remove(piece.Coord);
        pieceYOffsets.Remove(piece);
        Destroy(piece.gameObject);
    }

    public void DetachPiece(ChessPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        pieces.Remove(piece.Coord);
        pieceYOffsets.Remove(piece);
    }

    public void SpawnKingKillImpactFx(Vector3 worldPos)
    {
        if (kingKillImpactPrefab == null)
        {
            return;
        }

        GameObject fx = Instantiate(kingKillImpactPrefab, worldPos + kingKillImpactOffset, Quaternion.identity);
        ParticleSystem[] particles = fx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.useUnscaledTime = true;
        }

        if (kingKillImpactLifetime > 0f)
        {
            Destroy(fx, kingKillImpactLifetime);
        }
    }

    public Vector3 CoordToWorld(BoardCoord c)
    {
        Vector3 local = origin + new Vector3(c.x * cellSize, 0f, c.y * cellSize);
        return transform.TransformPoint(local);
    }

    public Vector3 CoordToPieceWorld(BoardCoord c)
    {
        return CoordToWorld(c) + new Vector3(0f, prefabPieceYOffset, 0f);
    }

    public BoardCoord WorldToCoord(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world) - origin;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.z / cellSize);
        return new BoardCoord(x, y);
    }

    public bool TryGetCoordFromRay(Ray ray, out BoardCoord coord)
    {
        Plane plane = new(transform.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            coord = WorldToCoord(point);
            return IsInside(coord);
        }

        coord = default;
        return false;
    }

    public List<BoardCoord> BuildDangerMap(Team attackerTeam, ChessPiece ignoredAttacker = null)
    {
        HashSet<BoardCoord> danger = new();
        foreach (ChessPiece piece in pieces.Values)
        {
            if (piece.Team != attackerTeam || piece == ignoredAttacker)
            {
                continue;
            }

            danger.Add(piece.Coord);

            List<BoardCoord> threatened = ChessRules.GetThreatenedCoords(this, piece);
            for (int i = 0; i < threatened.Count; i++)
            {
                danger.Add(threatened[i]);
            }
        }

        return new List<BoardCoord>(danger);
    }

    public void ShowKingMoveHighlights(ChessPiece king, List<BoardCoord> enemyDanger)
    {
        ClearHighlights();
        if (king == null)
        {
            return;
        }

        List<BoardCoord> moves = ChessRules.GetMoveCandidates(this, king);
        ShowMoveHighlights(moves, enemyDanger);
    }

    public void ShowMoveHighlights(List<BoardCoord> moves, List<BoardCoord> enemyDanger)
    {
        ClearHighlights();
        if (moves == null)
        {
            return;
        }

        for (int i = 0; i < moves.Count; i++)
        {
            if (enemyDanger != null && enemyDanger.Contains(moves[i]))
            {
                continue;
            }

            if (!highlightOverlays.TryGetValue(moves[i], out GameObject overlay) || overlay == null)
            {
                continue;
            }

            overlay.SetActive(true);
            highlightedCoords.Add(moves[i]);
        }
    }

    public void ClearHighlights()
    {
        if (highlightedCoords.Count == 0)
        {
            return;
        }

        foreach (BoardCoord c in highlightedCoords)
        {
            if (!highlightOverlays.TryGetValue(c, out GameObject overlay) || overlay == null)
            {
                continue;
            }

            overlay.SetActive(false);
        }

        highlightedCoords.Clear();
    }

    public void ShowEnemyThreatPreview(List<BoardCoord> threatenedCoords)
    {
        ClearEnemyThreatPreview();
        if (threatenedCoords == null)
        {
            return;
        }

        for (int i = 0; i < threatenedCoords.Count; i++)
        {
            if (!enemyThreatOverlays.TryGetValue(threatenedCoords[i], out GameObject overlay) || overlay == null)
            {
                continue;
            }

            overlay.SetActive(true);
            enemyThreatCoords.Add(threatenedCoords[i]);
        }
    }

    public void ClearEnemyThreatPreview()
    {
        if (enemyThreatCoords.Count == 0)
        {
            return;
        }

        foreach (BoardCoord c in enemyThreatCoords)
        {
            if (enemyThreatOverlays.TryGetValue(c, out GameObject overlay) && overlay != null)
            {
                overlay.SetActive(false);
            }
        }

        enemyThreatCoords.Clear();
    }

    public void ShowCaptureBorders(List<BoardCoord> captureCoords)
    {
        ClearCaptureBorders();
        if (captureCoords == null)
        {
            return;
        }

        for (int i = 0; i < captureCoords.Count; i++)
        {
            if (!captureBorders.TryGetValue(captureCoords[i], out LineRenderer border) || border == null)
            {
                continue;
            }

            border.gameObject.SetActive(true);
            captureBorderCoords.Add(captureCoords[i]);
        }
    }

    public void ClearCaptureBorders()
    {
        if (captureBorderCoords.Count == 0)
        {
            return;
        }

        foreach (BoardCoord c in captureBorderCoords)
        {
            if (captureBorders.TryGetValue(c, out LineRenderer border) && border != null)
            {
                border.gameObject.SetActive(false);
            }
        }

        captureBorderCoords.Clear();
    }

    private void EnsurePieceMaterials()
    {
        if (allyMaterial == null)
        {
            runtimeAllyMaterial = CreateFallbackPieceMaterial(allyFallbackColor);
        }

        if (enemyMaterial == null)
        {
            runtimeEnemyMaterial = CreateFallbackPieceMaterial(enemyFallbackColor);
        }

        if (kingMaterial == null)
        {
            runtimeKingMaterial = CreateFallbackPieceMaterial(kingFallbackColor);
        }

        if (runtimeHighlightMaterial == null)
        {
            runtimeHighlightMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            runtimeHighlightMaterial.color = tileHighlightColor;
        }

        if (runtimeCaptureBorderMaterial == null)
        {
            runtimeCaptureBorderMaterial = new Material(Shader.Find("Sprites/Default"));
            runtimeCaptureBorderMaterial.color = captureBorderColor;
        }

        if (runtimeThreatPreviewMaterial == null)
        {
            runtimeThreatPreviewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            runtimeThreatPreviewMaterial.color = enemyThreatPreviewColor;
        }

        if (runtimeBasePlateMaterial == null)
        {
            runtimeBasePlateMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            runtimeBasePlateMaterial.color = basePlateColor;
        }
    }

    private Material CreateFallbackPieceMaterial(Color color)
    {
        Shader shader = null;
        if (useToonFallbackForPieces)
        {
            shader = toonFallbackShader != null ? toonFallbackShader : Shader.Find("Custom/URP/ToonSimple");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        Material mat = new(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        return mat;
    }

    private void BuildBoardTilesIfNeeded()
    {
        if (!buildTileGridVisual || tileRoot != null)
        {
            return;
        }

        tileRoot = new GameObject("BoardTiles").transform;
        tileRoot.SetParent(transform, false);
        highlightRoot = new GameObject("BoardHighlights").transform;
        highlightRoot.SetParent(transform, false);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BoardCoord c = new(x, y);
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.SetParent(tileRoot, false);
                tile.transform.position = CoordToWorld(c) + new Vector3(0f, -tileHeight * 0.5f, 0f);
                tile.transform.localScale = new Vector3(cellSize * 0.98f, tileHeight, cellSize * 0.98f);

                Renderer renderer = tile.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.sharedMaterial.color = GetBaseTileColor(c);

                tileRenderers[c] = renderer;

                GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
                overlay.name = $"Highlight_{x}_{y}";
                overlay.transform.SetParent(highlightRoot, false);
                overlay.transform.position = CoordToWorld(c) + new Vector3(0f, tileHeight + 0.01f, 0f);
                overlay.transform.localScale = new Vector3(cellSize * 0.85f, 0.02f, cellSize * 0.85f);

                Collider overlayCol = overlay.GetComponent<Collider>();
                if (overlayCol != null)
                {
                    overlayCol.enabled = false;
                }

                Renderer overlayRenderer = overlay.GetComponent<Renderer>();
                overlayRenderer.sharedMaterial = runtimeHighlightMaterial;
                overlay.SetActive(false);
                highlightOverlays[c] = overlay;

                GameObject threatOverlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
                threatOverlay.name = $"Threat_{x}_{y}";
                threatOverlay.transform.SetParent(highlightRoot, false);
                threatOverlay.transform.position = CoordToWorld(c) + new Vector3(0f, tileHeight + 0.035f, 0f);
                threatOverlay.transform.localScale = new Vector3(cellSize * 0.45f, 0.015f, cellSize * 0.45f);
                Collider threatCol = threatOverlay.GetComponent<Collider>();
                if (threatCol != null)
                {
                    threatCol.enabled = false;
                }

                Renderer threatRenderer = threatOverlay.GetComponent<Renderer>();
                threatRenderer.sharedMaterial = runtimeThreatPreviewMaterial;
                threatOverlay.SetActive(false);
                enemyThreatOverlays[c] = threatOverlay;

                GameObject borderObj = new($"CaptureBorder_{x}_{y}");
                borderObj.transform.SetParent(highlightRoot, false);
                borderObj.transform.position = CoordToWorld(c) + new Vector3(0f, tileHeight + 0.035f, 0f);
                LineRenderer border = borderObj.AddComponent<LineRenderer>();
                border.material = runtimeCaptureBorderMaterial;
                border.startColor = captureBorderColor;
                border.endColor = captureBorderColor;
                border.useWorldSpace = true;
                border.loop = false;
                border.positionCount = 5;
                border.startWidth = 0.06f;
                border.endWidth = 0.06f;

                float h = cellSize * 0.47f;
                Vector3 p0 = borderObj.transform.position + new Vector3(-h, 0f, -h);
                Vector3 p1 = borderObj.transform.position + new Vector3(h, 0f, -h);
                Vector3 p2 = borderObj.transform.position + new Vector3(h, 0f, h);
                Vector3 p3 = borderObj.transform.position + new Vector3(-h, 0f, h);
                border.SetPosition(0, p0);
                border.SetPosition(1, p1);
                border.SetPosition(2, p2);
                border.SetPosition(3, p3);
                border.SetPosition(4, p0);
                borderObj.SetActive(false);
                captureBorders[c] = border;
            }
        }
    }

    private void EnsureBoardBasePlate()
    {
        if (!buildBoardBasePlate)
        {
            if (basePlateRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(basePlateRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(basePlateRoot.gameObject);
                }
            }

            basePlateRoot = null;
            return;
        }

        if (basePlateRoot == null)
        {
            Transform existing = transform.Find("BoardBasePlate");
            if (existing != null)
            {
                basePlateRoot = existing;
            }
        }

        if (basePlateRoot == null)
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "BoardBasePlate";
            plate.transform.SetParent(transform, false);
            Collider col = plate.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            basePlateRoot = plate.transform;
        }

        // Extend one extra cell on each side: board(40x40) -> plate(42x42).
        float plateWidth = Mathf.Max(0.01f, (width + 2) * cellSize);
        float plateDepth = Mathf.Max(0.01f, (height + 2) * cellSize);
        const float plateThickness = 50f;
        const float platePosY = -25.25f;
        Vector3 center = origin + new Vector3((width - 1) * cellSize * 0.5f, 0f, (height - 1) * cellSize * 0.5f);

        basePlateRoot.localPosition = center + new Vector3(0f, platePosY, 0f);
        basePlateRoot.localRotation = Quaternion.identity;
        basePlateRoot.localScale = new Vector3(plateWidth, plateThickness, plateDepth);

        Renderer renderer = basePlateRoot.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = runtimeBasePlateMaterial;
        }
    }

    private void ValidateOrRebuildBoardVisuals()
    {
        if (!buildTileGridVisual || !rebuildBoardVisualWhenCountMismatch)
        {
            return;
        }

        int expected = Mathf.Max(0, width) * Mathf.Max(0, height);
        if (expected == 0)
        {
            return;
        }

        bool isValid = tileRenderers.Count == expected
            && highlightOverlays.Count == expected
            && enemyThreatOverlays.Count == expected
            && captureBorders.Count == expected;

        if (isValid)
        {
            return;
        }

        RebuildBoardVisualsFromDimensions();
    }

    private void RebuildBoardVisualsFromDimensions()
    {
        if (tileRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(tileRoot.gameObject);
            }
            else
            {
                DestroyImmediate(tileRoot.gameObject);
            }
        }

        if (highlightRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(highlightRoot.gameObject);
            }
            else
            {
                DestroyImmediate(highlightRoot.gameObject);
            }
        }

        tileRoot = null;
        highlightRoot = null;
        tileRenderers.Clear();
        highlightOverlays.Clear();
        enemyThreatOverlays.Clear();
        captureBorders.Clear();
        highlightedCoords.Clear();
        enemyThreatCoords.Clear();
        captureBorderCoords.Clear();

        BuildBoardTilesIfNeeded();
        EnsureBoardBasePlate();
    }

    private Color GetBaseTileColor(BoardCoord c)
    {
        return ((c.x + c.y) & 1) == 0 ? tileColorA : tileColorB;
    }

    private void BindExistingBoardVisuals()
    {
        tileRoot = transform.Find("BoardTiles");
        highlightRoot = transform.Find("BoardHighlights");
        basePlateRoot = transform.Find("BoardBasePlate");

        tileRenderers.Clear();
        highlightOverlays.Clear();
        enemyThreatOverlays.Clear();
        captureBorders.Clear();
        highlightedCoords.Clear();
        enemyThreatCoords.Clear();
        captureBorderCoords.Clear();

        if (!buildTileGridVisual)
        {
            return;
        }

        if (tileRoot != null)
        {
            for (int i = 0; i < tileRoot.childCount; i++)
            {
                Transform child = tileRoot.GetChild(i);
                if (!TryParseCoordFromName(child.name, "Tile_", out BoardCoord coord))
                {
                    continue;
                }

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    tileRenderers[coord] = renderer;
                }

                continue;
            }

            for (int i = 0; i < tileRoot.childCount; i++)
            {
                Transform cell = tileRoot.GetChild(i);
                if (!TryParseCoordFromName(cell.name, "Cell_", out BoardCoord cellCoord))
                {
                    continue;
                }

                Transform tile = cell.Find("Tile");
                if (tile != null)
                {
                    Renderer tileRenderer = tile.GetComponent<Renderer>();
                    if (tileRenderer != null)
                    {
                        tileRenderers[cellCoord] = tileRenderer;
                    }
                }

                Transform highlight = cell.Find("Highlight");
                if (highlight != null)
                {
                    highlight.gameObject.SetActive(false);
                    highlightOverlays[cellCoord] = highlight.gameObject;
                }

                Transform threat = cell.Find("Threat");
                if (threat != null)
                {
                    threat.gameObject.SetActive(false);
                    enemyThreatOverlays[cellCoord] = threat.gameObject;
                }

                Transform border = cell.Find("CaptureBorder");
                if (border != null)
                {
                    LineRenderer line = border.GetComponent<LineRenderer>();
                    if (line != null)
                    {
                        border.gameObject.SetActive(false);
                        captureBorders[cellCoord] = line;
                    }
                }
            }
        }

        if (highlightRoot == null)
        {
            return;
        }

        for (int i = 0; i < highlightRoot.childCount; i++)
        {
            Transform child = highlightRoot.GetChild(i);

            if (TryParseCoordFromName(child.name, "Highlight_", out BoardCoord highlightCoord))
            {
                child.gameObject.SetActive(false);
                highlightOverlays[highlightCoord] = child.gameObject;
                continue;
            }

            if (TryParseCoordFromName(child.name, "Threat_", out BoardCoord threatCoord))
            {
                child.gameObject.SetActive(false);
                enemyThreatOverlays[threatCoord] = child.gameObject;
                continue;
            }

            if (TryParseCoordFromName(child.name, "CaptureBorder_", out BoardCoord borderCoord))
            {
                LineRenderer border = child.GetComponent<LineRenderer>();
                if (border != null)
                {
                    child.gameObject.SetActive(false);
                    captureBorders[borderCoord] = border;
                }
            }
        }

        EnsureBoardBasePlate();
    }

    private static bool TryParseCoordFromName(string name, string prefix, out BoardCoord coord)
    {
        coord = default;
        if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix))
        {
            return false;
        }

        string[] parts = name.Substring(prefix.Length).Split('_');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
        {
            return false;
        }

        coord = new BoardCoord(x, y);
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawGridGizmos)
        {
            return;
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
        for (int x = 0; x <= width; x++)
        {
            Vector3 a = transform.TransformPoint(origin + new Vector3(x * cellSize, 0f, 0f));
            Vector3 b = transform.TransformPoint(origin + new Vector3(x * cellSize, 0f, height * cellSize));
            Gizmos.DrawLine(a, b);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 a = transform.TransformPoint(origin + new Vector3(0f, 0f, y * cellSize));
            Vector3 b = transform.TransformPoint(origin + new Vector3(width * cellSize, 0f, y * cellSize));
            Gizmos.DrawLine(a, b);
        }
    }
}
