using System.Text;
using System.Text.Json;
using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Storage;

public sealed class KnowledgeUsageStatisticsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public KnowledgeUsageStatistics LoadOrEmpty(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("使用统计路径不能为空。", nameof(path));
        }

        if (!File.Exists(path))
        {
            return new KnowledgeUsageStatistics();
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var statistics = JsonSerializer.Deserialize<KnowledgeUsageStatistics>(json, JsonOptions);
            return Normalize(statistics);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new KnowledgeUsageStatistics();
        }
    }

    public void SaveAtomic(string path, KnowledgeUsageStatistics statistics)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("使用统计路径不能为空。", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(statistics);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidDataException("使用统计路径缺少目录。");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(Normalize(statistics), JsonOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            throw;
        }
    }

    private static KnowledgeUsageStatistics Normalize(KnowledgeUsageStatistics? statistics)
    {
        var normalized = new KnowledgeUsageStatistics();
        if (statistics is null
            || statistics.SchemaVersion != KnowledgeUsageStatistics.CurrentSchemaVersion
            || statistics.Counts is null)
        {
            return normalized;
        }

        foreach (var pair in statistics.Counts)
        {
            var id = pair.Key?.Trim() ?? string.Empty;
            if (id.Length == 0 || pair.Value <= 0)
            {
                continue;
            }

            normalized.Counts[id] = normalized.Counts.TryGetValue(id, out var existing)
                ? Math.Max(existing, pair.Value)
                : pair.Value;
        }

        return normalized;
    }

    private static bool IsRecoverable(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException;
    }
}
