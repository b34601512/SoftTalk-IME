using System.Text.Json;

namespace SoftTalkIme.Core.Sync;

public interface IReadOnlyKnowledgeSyncTransport
{
    Task<JsonDocument> FetchHeadAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default);

    Task<JsonDocument> FetchCurrentStateAsync(
        string scope,
        long afterSequence,
        string? pageCursor,
        long? syncSequence,
        CancellationToken cancellationToken = default);
}
