using System;
using System.Collections.Generic;
using AutoMapSharp.Shared;

namespace AutoMapSharp.Conventions;

/// <summary>
/// Defines a contract for parsing and formatting member names according to casing conventions.
/// </summary>
public interface INamingConvention
{
    /// <summary>
    /// Splits a member name into constituent words.
    /// </summary>
    IReadOnlyList<string> SplitWords(string memberName);

    /// <summary>
    /// Formats constituent words into a single member name according to this convention.
    /// </summary>
    string Format(IEnumerable<string> words);
}

/// <summary>
/// PascalCase convention (e.g. "CustomerAddressCity").
/// </summary>
public sealed class PascalCaseNamingConvention : INamingConvention
{
    public static readonly PascalCaseNamingConvention Instance = new();

    public IReadOnlyList<string> SplitWords(string memberName) => MemberNameMatching.SplitPascal(memberName);

    public string Format(IEnumerable<string> words)
    {
        var result = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word)) continue;
            result.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
            {
                result.Append(word.Substring(1).ToLowerInvariant());
            }
        }
        return result.ToString();
    }
}

/// <summary>
/// camelCase convention (e.g. "customerAddressCity").
/// </summary>
public sealed class CamelCaseNamingConvention : INamingConvention
{
    public static readonly CamelCaseNamingConvention Instance = new();

    public IReadOnlyList<string> SplitWords(string memberName)
    {
        return PascalCaseNamingConvention.Instance.SplitWords(memberName);
    }

    public string Format(IEnumerable<string> words)
    {
        var pascal = PascalCaseNamingConvention.Instance.Format(words);
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        return char.ToLowerInvariant(pascal[0]) + (pascal.Length > 1 ? pascal.Substring(1) : string.Empty);
    }
}

/// <summary>
/// snake_case convention (e.g. "customer_address_city").
/// </summary>
public sealed class SnakeCaseNamingConvention : INamingConvention
{
    public static readonly SnakeCaseNamingConvention Instance = new();

    public IReadOnlyList<string> SplitWords(string memberName)
    {
        return MemberNameMatching.SplitSnake(memberName);
    }

    public string Format(IEnumerable<string> words)
    {
        var list = new List<string>();
        foreach (var word in words)
        {
            if (!string.IsNullOrEmpty(word))
            {
                list.Add(word.ToLowerInvariant());
            }
        }
        return string.Join("_", list);
    }
}
