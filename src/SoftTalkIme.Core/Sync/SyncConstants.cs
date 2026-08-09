namespace SoftTalkIme.Core.Sync;

public static class SyncConstants
{
    public const string TeamScope = "team_phrases";
    public const string PrivateScope = "private_phrases";
    public const string HeadPath = "/api/phrase-sync/head";
    public const string CurrentStatePath = "/api/phrase-sync/current-state";
    public const string ProtocolVersion = "3";
    public const string SchemaVersion = "3";
    public const int DefaultPageLimit = 200;
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    public static readonly IReadOnlyList<string> FormalScopes = new[]
    {
        TeamScope,
        PrivateScope,
    };
}
