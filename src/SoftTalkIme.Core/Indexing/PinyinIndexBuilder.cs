using System.Text;
using PinyinNet;

namespace SoftTalkIme.Core.Indexing;

public static class PinyinIndexBuilder
{
    public static string Build(params string?[] values)
    {
        var fullPinyin = new StringBuilder();
        var initials = new StringBuilder();
        foreach (var value in values)
        {
            var text = value ?? string.Empty;
            AppendAscii(fullPinyin, PinyinConvert.GetPinyin(text));
            AppendAscii(initials, PinyinConvert.GetPinyinFirstLetter(text));
        }

        return string.Join(' ', fullPinyin.ToString(), initials.ToString()).Trim();
    }

    private static void AppendAscii(StringBuilder target, string value)
    {
        foreach (var character in (value ?? string.Empty).ToLowerInvariant())
        {
            if ((character is >= 'a' and <= 'z') || (character is >= '0' and <= '9'))
            {
                target.Append(character);
            }
        }
    }
}
