using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChessPiece : MonoBehaviour
{
    [SerializeField] private Team team;
    [SerializeField] private PieceType pieceType;

    public Team Team => team;
    public PieceType PieceType => pieceType;

    public BoardCoord Coord { get; private set; }

    public void Initialize(Team newTeam, PieceType newPieceType, BoardCoord coord)
    {
        team = newTeam;
        pieceType = newPieceType;
        Coord = coord;
    }

    public void SetCoord(BoardCoord coord)
    {
        Coord = coord;
    }
}
