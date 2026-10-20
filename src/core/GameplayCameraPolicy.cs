namespace VampireHuntersVR.Stage0;

// Pure identity policy, shared with fixtures. A scene name alone never proves gameplay.
public sealed record GameplayCameraCandidate(string Scene, string CameraName, bool IsMain,
    bool CameraActive, bool CameraEnabled, bool Perspective, bool HasTarget,
    bool PlayerActive, bool PlayerEnabled, bool DirectPlayerChild);

public static class GameplayCameraPolicy
{
    public static bool Accept(GameplayCameraCandidate candidate) =>
        !string.IsNullOrWhiteSpace(candidate.Scene) &&
        !candidate.Scene.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase) &&
        !candidate.Scene.Equals("Intro", StringComparison.OrdinalIgnoreCase) &&
        candidate.CameraName.Equals("Main Camera", StringComparison.Ordinal) &&
        candidate.IsMain && candidate.CameraActive && candidate.CameraEnabled &&
        candidate.Perspective && !candidate.HasTarget && candidate.PlayerActive &&
        candidate.PlayerEnabled && candidate.DirectPlayerChild;

    // Never choose the first of multiple players/cameras. Fail closed on ambiguity.
    public static int Select(IReadOnlyList<GameplayCameraCandidate> candidates)
    {
        int selected = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!Accept(candidates[i])) continue;
            if (selected >= 0) return -1;
            selected = i;
        }
        return selected;
    }
}