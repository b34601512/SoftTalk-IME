namespace SoftTalkIme.Core.Models;

public sealed class KnowledgeUsageStatistics
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Dictionary<string, long> Counts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public long GetCount(string entryId)
    {
        var id = entryId?.Trim() ?? string.Empty;
        return id.Length > 0 && Counts.TryGetValue(id, out var count) && count > 0
            ? count
            : 0;
    }

    public void RecordUse(string entryId)
    {
        var id = entryId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            return;
        }

        var current = GetCount(id);
        Counts[id] = current == long.MaxValue ? current : current + 1;
    }
}
