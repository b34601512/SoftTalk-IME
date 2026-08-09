namespace SoftTalkIme.Tsf;

internal static class TsfInputActivationPolicy
{
    public static bool ShouldAutoArm(int key, bool hasMatches)
    {
        return key is >= 'A' and <= 'Z' && hasMatches;
    }
}
