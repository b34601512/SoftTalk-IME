using System.Text.Json;
using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Sync;

public sealed record SyncRunResult(
    KnowledgeSnapshot Snapshot,
    IReadOnlyList<string> CheckedScopes,
    IReadOnlyList<string> UpdatedScopes,
    int PulledRecords);

public sealed class KnowledgeSyncCoordinator
{
    private readonly IReadOnlyKnowledgeSyncTransport _transport;

    public KnowledgeSyncCoordinator(IReadOnlyKnowledgeSyncTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task<SyncRunResult> PollOnceAsync(
        KnowledgeSnapshot current,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        using var headDocument = await _transport.FetchHeadAsync(SyncConstants.FormalScopes, cancellationToken).ConfigureAwait(false);
        var head = SyncHead.Parse(headDocument.RootElement);
        var changedScopes = SyncDecision.FindChangedScopes(head, current);
        var next = current.Clone();
        var pulledRecords = 0;

        foreach (var scope in changedScopes)
        {
            var afterSequence = current.ScopeSequences.TryGetValue(scope, out var localSequence) ? localSequence : 0L;
            if (head.LatestByScope.TryGetValue(scope, out var remoteSequence) && remoteSequence < afterSequence)
            {
                next = KnowledgeSnapshotReducer.ResetScope(next, scope);
                afterSequence = 0;
            }
            var pageCursor = (string?)null;
            long? syncSequence = null;
            while (true)
            {
                using var pageDocument = await _transport.FetchCurrentStateAsync(
                    scope,
                    afterSequence,
                    pageCursor,
                    syncSequence,
                    cancellationToken).ConfigureAwait(false);
                var payload = pageDocument.RootElement;
                var pageSyncSequence = ReadNonNegativeLong(payload, "sync_seq");
                if (syncSequence.HasValue && syncSequence.Value != pageSyncSequence)
                {
                    throw new InvalidDataException("同一轮同步的多页数据 sync_seq 不一致。");
                }
                syncSequence ??= pageSyncSequence;
                pulledRecords += ReadObjectTotal(payload);
                next = KnowledgeSnapshotReducer.ApplyPage(next, scope, payload);

                if (!ReadBoolean(payload, "has_more"))
                {
                    break;
                }

                pageCursor = ReadRequiredString(payload, "next_page_cursor");
            }

            next = KnowledgeSnapshotReducer.CompleteScope(next, scope, syncSequence ?? afterSequence);
        }

        return new SyncRunResult(
            Snapshot: next,
            CheckedScopes: SyncConstants.FormalScopes.ToArray(),
            UpdatedScopes: changedScopes,
            PulledRecords: pulledRecords);
    }

    private static int ReadObjectTotal(JsonElement payload)
    {
        if (!payload.TryGetProperty("object_total", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }
        if (value.TryGetInt32(out var number) && number >= 0)
        {
            return number;
        }
        throw new InvalidDataException("同步当前状态 object_total 不合法。");
    }

    private static bool ReadBoolean(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }
        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException($"同步当前状态 {propertyName} 不合法。");
        }
        return value.GetBoolean();
    }

    private static long ReadNonNegativeLong(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidDataException($"同步当前状态缺少 {propertyName}。");
        }
        if (!value.TryGetInt64(out var number) || number < 0)
        {
            throw new InvalidDataException($"同步当前状态 {propertyName} 不合法。");
        }
        return number;
    }

    private static string ReadRequiredString(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"同步当前状态缺少 {propertyName}。");
        }
        var text = value.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException($"同步当前状态 {propertyName} 不能为空。");
        }
        return text;
    }
}
