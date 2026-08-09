using System.Text;
using SoftTalkIme.Core.Indexing;
using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Search;

public static class KnowledgeSearchEngine
{
    public static IReadOnlyList<SearchHit> Search(
        KnowledgeSnapshot snapshot,
        string query,
        int limit = 9,
        IReadOnlyDictionary<string, long>? usageCounts = null)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery) || limit <= 0)
        {
            return Array.Empty<SearchHit>();
        }

        var tokens = SplitTokens(normalizedQuery);
        var hits = new List<SearchHit>();
        var categoryPinyinCache = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in snapshot.Entries.Values)
        {
            var categoryPath = BuildCategoryPath(snapshot, entry.CategoryId);
            if (!categoryPinyinCache.TryGetValue(categoryPath, out var categoryPinyin))
            {
                categoryPinyin = PinyinIndexBuilder.Build(categoryPath);
                categoryPinyinCache[categoryPath] = categoryPinyin;
            }
            var score = ScoreEntry(entry, categoryPath, categoryPinyin, tokens);
            if (score <= 0)
            {
                continue;
            }

            hits.Add(new SearchHit(entry, categoryPath, score));
        }

        return hits
            .OrderByDescending(hit => hit.Score)
            .ThenByDescending(hit => GetUsageCount(usageCounts, hit.Entry.Id))
            .ThenBy(hit => hit.Entry.SortOrder)
            .ThenBy(hit => hit.Entry.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static long GetUsageCount(
        IReadOnlyDictionary<string, long>? usageCounts,
        string entryId)
    {
        return usageCounts is not null
            && usageCounts.TryGetValue(entryId, out var count)
            && count > 0
            ? count
            : 0;
    }

    private static double ScoreEntry(
        KnowledgeEntry entry,
        string categoryPath,
        string categoryPinyin,
        IReadOnlyList<string> tokens)
    {
        var question = Normalize(entry.Question);
        var answer = Normalize(entry.Answer);
        var category = Normalize(categoryPath);
        var pinyin = Normalize(string.IsNullOrWhiteSpace(entry.PinyinIndexText)
            ? PinyinIndexBuilder.Build(entry.Question, entry.Answer)
            : entry.PinyinIndexText);
        var normalizedCategoryPinyin = Normalize(categoryPinyin);
        var score = 0d;

        foreach (var token in tokens)
        {
            var matched = false;
            if (question.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
                matched = true;
            }
            if (pinyin.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 80;
                matched = true;
            }
            if (answer.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 35;
                matched = true;
            }
            if (category.Contains(token, StringComparison.OrdinalIgnoreCase)
                || normalizedCategoryPinyin.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
                matched = true;
            }
            if (!matched)
            {
                return 0;
            }
        }

        return score;
    }

    private static IReadOnlyList<string> SplitTokens(string query)
    {
        var tokens = query
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '，', '.', '。', '!', '！', '?', '？', ':', '：', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return tokens.Length == 0 ? new[] { query } : tokens;
    }

    private static string BuildCategoryPath(KnowledgeSnapshot snapshot, string categoryId)
    {
        var names = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentId = categoryId;
        while (!string.IsNullOrWhiteSpace(currentId) && visited.Add(currentId))
        {
            if (!snapshot.Categories.TryGetValue(currentId, out var category))
            {
                break;
            }
            if (!string.IsNullOrWhiteSpace(category.Name))
            {
                names.Add(category.Name);
            }
            currentId = category.ParentId;
        }

        names.Reverse();
        return string.Join(" > ", names);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in (value ?? string.Empty).Trim().ToLowerInvariant())
        {
            if (!char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
