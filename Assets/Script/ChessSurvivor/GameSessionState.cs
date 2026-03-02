using System.Collections.Generic;

public static class GameSessionState
{
    private static readonly HashSet<int> PlayedCinematics = new();

    public static bool EnteredMainFromTitle { get; private set; }

    public static void BeginMainRunFromTitle()
    {
        EnteredMainFromTitle = true;
        PlayedCinematics.Clear();
    }

    public static void MarkCinematicPlayed(int cinematicId)
    {
        PlayedCinematics.Add(cinematicId);
    }

    public static bool HasPlayedCinematic(int cinematicId)
    {
        return PlayedCinematics.Contains(cinematicId);
    }
}
