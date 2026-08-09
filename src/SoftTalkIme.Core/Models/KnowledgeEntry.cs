namespace SoftTalkIme.Core.Models;

public sealed record KnowledgeEntry(
    string Id,
    string Question,
    string Answer,
    string CategoryId,
    string Scope,
    int PhraseSetNo,
    int SortOrder,
    string PinyinIndexText = "");
