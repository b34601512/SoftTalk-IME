namespace SoftTalkIme.Core.Models;

public sealed class KnowledgeSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Dictionary<string, long> ScopeSequences { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, CategoryNode> Categories { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, KnowledgeEntry> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public KnowledgeSnapshot Clone()
    {
        return new KnowledgeSnapshot
        {
            SchemaVersion = this.SchemaVersion,
            ScopeSequences = new Dictionary<string, long>(ScopeSequences, StringComparer.OrdinalIgnoreCase),
            Categories = new Dictionary<string, CategoryNode>(Categories, StringComparer.OrdinalIgnoreCase),
            Entries = new Dictionary<string, KnowledgeEntry>(Entries, StringComparer.OrdinalIgnoreCase),
        };
    }
}
