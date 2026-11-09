using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace AutoMapSharp.Sample.Web.Infrastructure;

/// <summary>
/// Reads code regions from the module source files embedded in this assembly.
/// A region starts with <c>// &lt;snippet:Name&gt;</c> and ends with <c>// &lt;/snippet:Name&gt;</c>.
/// Demo pages use this so the code they show is the code that actually ran.
/// </summary>
public static class SnippetReader
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    public static string Read(string fileName, string snippetName)
    {
        return Cache.GetOrAdd($"{fileName}#{snippetName}", _ => Load(fileName, snippetName));
    }

    private static string Load(string fileName, string snippetName)
    {
        using var stream = typeof(SnippetReader).Assembly.GetManifestResourceStream($"Snippets.{fileName}")
            ?? throw new InvalidOperationException($"Embedded source '{fileName}' was not found.");
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');

        var start = Array.FindIndex(lines, l => l.Trim() == $"// <snippet:{snippetName}>");
        var end = Array.FindIndex(lines, l => l.Trim() == $"// </snippet:{snippetName}>");
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException($"Snippet '{snippetName}' was not found in '{fileName}'.");
        }

        var body = lines.Skip(start + 1).Take(end - start - 1).ToList();
        var indent = body.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        var sb = new StringBuilder();
        foreach (var line in body)
        {
            sb.AppendLine(line.Length >= indent ? line.Substring(indent) : line.TrimStart());
        }

        return sb.ToString().TrimEnd();
    }
}
