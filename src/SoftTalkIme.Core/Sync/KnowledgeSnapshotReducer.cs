using System.Text.Json;
using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Sync;

public static class KnowledgeSnapshotReducer
{
    public static KnowledgeSnapshot ApplyPage(
        KnowledgeSnapshot current,
        string scope,
        JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("同步当前状态必须返回 JSON 对象。");
        }

        var next = current.Clone();
        if (!payload.TryGetProperty("table_batches", out var batches) || batches.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("同步当前状态缺少 table_batches 数组。");
        }

        foreach (var batch in batches.EnumerateArray())
        {
            var tableName = ReadRequiredString(batch, "table_name");
            if (!batch.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"同步批次 {tableName} 缺少 records 数组。");
            }

            foreach (var record in records.EnumerateArray())
            {
                ApplyRecord(next, tableName, scope, record);
            }
        }

        return next;
    }

    public static KnowledgeSnapshot CompleteScope(
        KnowledgeSnapshot current,
        string scope,
        long nextSequence)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("同步域不能为空。", nameof(scope));
        }

        if (nextSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextSequence));
        }

        var next = current.Clone();
        next.ScopeSequences[scope] = nextSequence;
        return next;
    }

    public static KnowledgeSnapshot ResetScope(
        KnowledgeSnapshot current,
        string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("同步域不能为空。", nameof(scope));
        }

        var phraseScope = scope.Equals(SyncConstants.PrivateScope, StringComparison.OrdinalIgnoreCase)
            ? "personal"
            : "team";
        var next = current.Clone();
        foreach (var categoryId in next.Categories
                     .Where(pair => pair.Value.Scope.Equals(phraseScope, StringComparison.OrdinalIgnoreCase))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            next.Categories.Remove(categoryId);
        }
        foreach (var entryId in next.Entries
                     .Where(pair => pair.Value.Scope.Equals(phraseScope, StringComparison.OrdinalIgnoreCase))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            next.Entries.Remove(entryId);
        }

        next.ScopeSequences[scope] = 0;
        return next;
    }

    private static void ApplyRecord(
        KnowledgeSnapshot snapshot,
        string tableName,
        string scope,
        JsonElement record)
    {
        var id = ReadRequiredString(record, "uuid");
        if (HasValue(record, "deleted_at"))
        {
            RemoveRecord(snapshot, tableName, id);
            return;
        }

        switch (tableName)
        {
            case "st_category":
                snapshot.Categories[id] = new CategoryNode(
                    Id: id,
                    ParentId: ReadString(record, "parent_uuid"),
                    Level: ReadInt(record, "level"),
                    Scope: ReadString(record, "phrase_scope", scope),
                    PhraseSetNo: ReadInt(record, "phrase_set_no"),
                    Name: ReadString(record, "name"),
                    SortOrder: ReadInt(record, "sort_order"));
                break;
            case "st_faq":
                snapshot.Entries[id] = new KnowledgeEntry(
                    Id: id,
                    Question: ReadString(record, "question"),
                    Answer: ReadString(record, "answer"),
                    CategoryId: ReadString(record, "category_uuid"),
                    Scope: ReadString(record, "phrase_scope", scope),
                    PhraseSetNo: ReadInt(record, "phrase_set_no"),
                    SortOrder: ReadInt(record, "sort_order"),
                    PinyinIndexText: ReadString(record, "pinyin_index_text"));
                break;
        }
    }

    private static void RemoveRecord(KnowledgeSnapshot snapshot, string tableName, string id)
    {
        switch (tableName)
        {
            case "st_category":
                snapshot.Categories.Remove(id);
                break;
            case "st_faq":
                snapshot.Entries.Remove(id);
                break;
        }
    }

    private static bool HasValue(JsonElement record, string propertyName)
    {
        if (!record.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null => false,
            JsonValueKind.Undefined => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            _ => true,
        };
    }

    private static string ReadRequiredString(JsonElement record, string propertyName)
    {
        var value = ReadString(record, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"同步记录缺少 {propertyName}。");
        }

        return value;
    }

    private static string ReadString(JsonElement record, string propertyName, string fallback = "")
    {
        if (!record.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : value.ToString();
    }

    private static int ReadInt(JsonElement record, string propertyName)
    {
        if (!record.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var textNumber))
        {
            return textNumber;
        }

        throw new InvalidDataException($"同步记录字段 {propertyName} 不是整数。");
    }
}
