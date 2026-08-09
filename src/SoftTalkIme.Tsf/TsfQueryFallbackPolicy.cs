namespace SoftTalkIme.Tsf;

internal readonly record struct TsfQueryDecision(bool EatKey, string? FallbackText);

internal static class TsfQueryFallbackPolicy
{
    public static TsfQueryDecision Decide(
        string previousQuery,
        string nextQuery,
        bool hasMatches)
    {
        if (hasMatches)
        {
            return new TsfQueryDecision(EatKey: true, FallbackText: null);
        }

        return string.IsNullOrEmpty(previousQuery)
            ? new TsfQueryDecision(EatKey: false, FallbackText: null)
            : new TsfQueryDecision(EatKey: true, FallbackText: nextQuery);
    }
}
