using System.Text.Json;

namespace SoftTalkIme.Core.Sync;

public sealed record SyncHead(IReadOnlyDictionary<string, long> LatestByScope)
{
    public static SyncHead Parse(JsonElement payload)
    {
        if (!payload.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("同步 head 缺少 scopes 对象。");
        }

        var latestByScope = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in scopes.EnumerateObject())
        {
            var value = ReadNonNegativeLong(property.Value, $"scopes.{property.Name}");
            latestByScope[property.Name] = value;
        }

        return new SyncHead(latestByScope);
    }

    private static long ReadNonNegativeLong(JsonElement value, string fieldName)
    {
        long number;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numberValue))
        {
            number = numberValue;
        }
        else if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var textValue))
        {
            number = textValue;
        }
        else
        {
            throw new InvalidDataException($"同步 head 字段 {fieldName} 不是整数。");
        }

        if (number < 0)
        {
            throw new InvalidDataException($"同步 head 字段 {fieldName} 不能为负数。");
        }

        return number;
    }
}
