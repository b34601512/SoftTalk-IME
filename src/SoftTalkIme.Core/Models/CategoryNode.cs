namespace SoftTalkIme.Core.Models;

public sealed record CategoryNode(
    string Id,
    string ParentId,
    int Level,
    string Scope,
    int PhraseSetNo,
    string Name,
    int SortOrder);
