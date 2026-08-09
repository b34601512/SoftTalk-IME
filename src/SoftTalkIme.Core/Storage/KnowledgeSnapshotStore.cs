using System.Text;
using System.Text.Json;
using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Storage;

public sealed class KnowledgeSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public KnowledgeSnapshot LoadOrEmpty(string path)
    {
        if (!File.Exists(path))
        {
            return new KnowledgeSnapshot();
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        var snapshot = JsonSerializer.Deserialize<KnowledgeSnapshot>(json, JsonOptions);
        if (snapshot is null)
        {
            throw new InvalidDataException("本地话术快照为空。");
        }

        return snapshot;
    }

    public void SaveAtomic(string path, KnowledgeSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("快照路径不能为空。", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(snapshot);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidDataException("快照路径缺少目录。");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
