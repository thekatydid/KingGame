using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class KingSkillRangeAttack : MonoBehaviour
{
    [SerializeField] private bool enableRangeAttack = true;
    [SerializeField] private bool onlyWhileKingSkillActive = true;
    [SerializeField, Min(1)] private int rangeRadius = 1;
    [SerializeField] private bool affectEnemiesOnly = true;
    [SerializeField, Min(0.01f)] private float splashPreLaunchTimingMultiplier = 1.5f;
    [SerializeField] private KingQueenSkillController kingSkill;
    [SerializeField] private bool autoFindKingSkill = true;

    public float SplashPreLaunchTimingMultiplier => Mathf.Max(0.01f, splashPreLaunchTimingMultiplier);

    public bool IsRangeAttackActive
    {
        get
        {
            if (!enableRangeAttack)
            {
                return false;
            }

            if (!onlyWhileKingSkillActive)
            {
                return true;
            }

            EnsureReferences();
            return kingSkill != null && kingSkill.IsActive;
        }
    }

    public void CollectVictims(ChessBoardManager board, BoardCoord center, HashSet<ChessPiece> result, ChessPiece exclude = null)
    {
        if (board == null || result == null || !IsRangeAttackActive)
        {
            return;
        }

        int radius = Mathf.Max(1, rangeRadius);
        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                BoardCoord c = new(x, y);
                if (!board.IsInside(c))
                {
                    continue;
                }

                ChessPiece piece = board.GetPieceAt(c);
                if (piece == null || piece == exclude)
                {
                    continue;
                }

                if (affectEnemiesOnly && piece.Team != Team.Enemy)
                {
                    continue;
                }

                result.Add(piece);
            }
        }
    }

    public void CollectRangeCoords(ChessBoardManager board, BoardCoord center, HashSet<BoardCoord> result)
    {
        if (board == null || result == null || !IsRangeAttackActive)
        {
            return;
        }

        int radius = Mathf.Max(1, rangeRadius);
        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                BoardCoord c = new(x, y);
                if (!board.IsInside(c))
                {
                    continue;
                }

                result.Add(c);
            }
        }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (kingSkill == null && autoFindKingSkill)
        {
            kingSkill = GetComponent<KingQueenSkillController>();
            if (kingSkill == null)
            {
                kingSkill = FindFirstObjectByType<KingQueenSkillController>();
            }
        }
    }
}
