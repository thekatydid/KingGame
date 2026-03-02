using System.Collections.Generic;

public static class ChessRules
{
    private static readonly BoardCoord[] Orthogonal =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    };

    private static readonly BoardCoord[] Diagonal =
    {
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private static readonly BoardCoord[] KingOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private static readonly BoardCoord[] KnightOffsets =
    {
        new(1, 2), new(2, 1), new(-1, 2), new(-2, 1),
        new(1, -2), new(2, -1), new(-1, -2), new(-2, -1)
    };

    public static List<BoardCoord> GetMoveCandidates(ChessBoardManager board, ChessPiece piece)
    {
        return GetMoveCandidates(board, piece, piece.PieceType);
    }

    public static List<BoardCoord> GetMoveCandidates(ChessBoardManager board, ChessPiece piece, PieceType pieceTypeOverride)
    {
        List<BoardCoord> result = new();

        switch (pieceTypeOverride)
        {
            case PieceType.King:
                AddStepMoves(board, piece, KingOffsets, result);
                break;
            case PieceType.Pawn:
                AddStepMoves(board, piece, Orthogonal, result);
                break;
            case PieceType.Knight:
                AddStepMoves(board, piece, KnightOffsets, result);
                break;
            case PieceType.Rook:
                AddLineMoves(board, piece, Orthogonal, result);
                break;
            case PieceType.Bishop:
                AddLineMoves(board, piece, Diagonal, result);
                break;
            case PieceType.Queen:
                AddLineMoves(board, piece, Orthogonal, result);
                AddLineMoves(board, piece, Diagonal, result);
                break;
        }

        return result;
    }

    public static List<BoardCoord> GetAttackCandidates(ChessBoardManager board, ChessPiece piece)
    {
        return GetAttackCandidates(board, piece, piece.PieceType);
    }

    public static List<BoardCoord> GetAttackCandidates(ChessBoardManager board, ChessPiece piece, PieceType pieceTypeOverride)
    {
        List<BoardCoord> result = new();

        switch (pieceTypeOverride)
        {
            case PieceType.King:
                AddAttackSteps(board, piece, KingOffsets, result);
                break;
            case PieceType.Pawn:
                AddAttackSteps(board, piece, Diagonal, result);
                break;
            case PieceType.Knight:
                AddAttackSteps(board, piece, KnightOffsets, result);
                break;
            case PieceType.Rook:
                AddLineAttacks(board, piece, Orthogonal, result);
                break;
            case PieceType.Bishop:
                AddLineAttacks(board, piece, Diagonal, result);
                break;
            case PieceType.Queen:
                AddLineAttacks(board, piece, Orthogonal, result);
                AddLineAttacks(board, piece, Diagonal, result);
                break;
        }

        return result;
    }

    public static List<BoardCoord> GetThreatenedCoords(ChessBoardManager board, ChessPiece piece)
    {
        List<BoardCoord> result = new();

        switch (piece.PieceType)
        {
            case PieceType.King:
                AddThreatSteps(board, piece.Coord, KingOffsets, result);
                break;
            case PieceType.Pawn:
                AddThreatSteps(board, piece.Coord, Diagonal, result);
                break;
            case PieceType.Knight:
                AddThreatSteps(board, piece.Coord, KnightOffsets, result);
                break;
            case PieceType.Rook:
                AddThreatLines(board, piece.Coord, Orthogonal, result);
                break;
            case PieceType.Bishop:
                AddThreatLines(board, piece.Coord, Diagonal, result);
                break;
            case PieceType.Queen:
                AddThreatLines(board, piece.Coord, Orthogonal, result);
                AddThreatLines(board, piece.Coord, Diagonal, result);
                break;
        }

        return result;
    }

    private static void AddStepMoves(ChessBoardManager board, ChessPiece piece, BoardCoord[] offsets, List<BoardCoord> target)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            BoardCoord c = piece.Coord + offsets[i];
            if (!board.IsInside(c) || board.IsOccupied(c))
            {
                continue;
            }

            target.Add(c);
        }
    }

    private static void AddAttackSteps(ChessBoardManager board, ChessPiece piece, BoardCoord[] offsets, List<BoardCoord> target)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            BoardCoord c = piece.Coord + offsets[i];
            if (!board.IsInside(c))
            {
                continue;
            }

            ChessPiece victim = board.GetPieceAt(c);
            if (victim != null && victim.Team != piece.Team)
            {
                target.Add(c);
            }
        }
    }

    private static void AddLineMoves(ChessBoardManager board, ChessPiece piece, BoardCoord[] dirs, List<BoardCoord> target)
    {
        for (int d = 0; d < dirs.Length; d++)
        {
            BoardCoord cursor = piece.Coord + dirs[d];
            while (board.IsInside(cursor))
            {
                if (board.IsOccupied(cursor))
                {
                    break;
                }

                target.Add(cursor);
                cursor += dirs[d];
            }
        }
    }

    private static void AddLineAttacks(ChessBoardManager board, ChessPiece piece, BoardCoord[] dirs, List<BoardCoord> target)
    {
        for (int d = 0; d < dirs.Length; d++)
        {
            BoardCoord cursor = piece.Coord + dirs[d];
            while (board.IsInside(cursor))
            {
                ChessPiece occupier = board.GetPieceAt(cursor);
                if (occupier != null)
                {
                    if (occupier.Team != piece.Team)
                    {
                        target.Add(cursor);
                    }

                    break;
                }

                cursor += dirs[d];
            }
        }
    }

    private static void AddThreatSteps(ChessBoardManager board, BoardCoord origin, BoardCoord[] offsets, List<BoardCoord> target)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            BoardCoord c = origin + offsets[i];
            if (board.IsInside(c))
            {
                target.Add(c);
            }
        }
    }

    private static void AddThreatLines(ChessBoardManager board, BoardCoord origin, BoardCoord[] dirs, List<BoardCoord> target)
    {
        for (int d = 0; d < dirs.Length; d++)
        {
            BoardCoord cursor = origin + dirs[d];
            while (board.IsInside(cursor))
            {
                target.Add(cursor);
                if (board.IsOccupied(cursor))
                {
                    break;
                }

                cursor += dirs[d];
            }
        }
    }
}
