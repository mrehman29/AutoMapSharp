using System;
using System.Collections.Generic;

namespace AutoMapSharp.Shared;

/// <summary>
/// Member name matching shared by the runtime engine and the Roslyn analyzer. The file is compiled into both
/// assemblies, so a member the analyzer considers mapped is a member the engine maps.
/// </summary>
internal static class MemberNameMatching
{
    /// <summary>
    /// Splits a PascalCase or camelCase name into words: "CustomerAddressCity" gives Customer, Address, City,
    /// and "HTTPServer" gives HTTP, Server.
    /// </summary>
    public static IReadOnlyList<string> SplitPascal(string name)
    {
        if (string.IsNullOrEmpty(name)) return Array.Empty<string>();

        var words = new List<string>();
        int start = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && !char.IsUpper(name[i + 1]))))
            {
                words.Add(name.Substring(start, i - start));
                start = i;
            }
        }

        words.Add(name.Substring(start));
        return words;
    }

    /// <summary>
    /// Splits a snake_case name into words.
    /// </summary>
    public static IReadOnlyList<string> SplitSnake(string name) =>
        string.IsNullOrEmpty(name) ? Array.Empty<string>() : name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Removes the first matching prefix and the first matching suffix, ignoring case, keeping at least one character.
    /// </summary>
    public static string StripPrefixesAndSuffixes(string name, IReadOnlyList<string> prefixes, IReadOnlyList<string> suffixes)
    {
        var result = name;
        foreach (var prefix in prefixes)
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && result.Length > prefix.Length)
            {
                result = result.Substring(prefix.Length);
                break;
            }
        }

        foreach (var suffix in suffixes)
        {
            if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && result.Length > suffix.Length)
            {
                result = result.Substring(0, result.Length - suffix.Length);
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// True when both word lists have the same words in the same order, ignoring case.
    /// </summary>
    public static bool WordsMatch(IReadOnlyList<string> words1, IReadOnlyList<string> words2)
    {
        if (words1.Count != words2.Count) return false;
        for (int i = 0; i < words1.Count; i++)
        {
            if (!string.Equals(words1[i], words2[i], StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }
}
