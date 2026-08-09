using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Search;

public sealed record SearchHit(
    KnowledgeEntry Entry,
    string CategoryPath,
    double Score);
