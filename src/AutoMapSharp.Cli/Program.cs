using System;
using System.IO;
using System.Linq;
using AutoMapSharp.MigrationAnalyzer;

namespace AutoMapSharp.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("   AutoMapSharp Migration CLI - Automated AutoMapper to AutoMapSharp Migrator   ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        bool dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
        bool showHelp = args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || a == "-h" || a == "-?");

        if (showHelp)
        {
            PrintHelp();
            return 0;
        }

        string targetDirectory = args.FirstOrDefault(a => !a.StartsWith("-")) ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(targetDirectory) && !File.Exists(targetDirectory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Target path '{targetDirectory}' does not exist.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Scanning target: {targetDirectory}");
        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[DRY RUN MODE] No files will be overwritten.");
            Console.ResetColor();
        }

        var filesToScan = File.Exists(targetDirectory)
            ? new[] { targetDirectory }
            : Directory.EnumerateFiles(targetDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
                .ToArray();

        int filesModified = 0;
        int totalChanges = 0;

        foreach (var file in filesToScan)
        {
            string originalText = File.ReadAllText(file);
            var report = AutoMapperMigrator.MigrateSourceCode(originalText);

            if (report.WasModified)
            {
                filesModified++;
                totalChanges += report.TotalReplacements;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[MODIFIED] {Path.GetFileName(file)} ({report.TotalReplacements} replacements)");
                Console.ResetColor();

                foreach (var change in report.Changes)
                {
                    Console.WriteLine($"   Line {change.LineNumber}: [{change.RuleId}] {change.Description}");
                }

                if (!dryRun)
                {
                    File.WriteAllText(file, report.MigratedCode);
                }
            }
        }

        Console.WriteLine("\n--------------------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Migration Complete! Scanned: {filesToScan.Length} files | Migrated: {filesModified} files | Total AST changes: {totalChanges}");
        Console.ResetColor();

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Usage:
  automapsharp-migrate [path] [options]

Arguments:
  [path]             Target directory or file path to migrate (defaults to current directory).

Options:
  --dry-run          Preview migrations without writing changes to disk.
  -h, --help         Show command-line help and usage instructions.

Rules Migrated:
  * AMSMIG001: 'using AutoMapper;' -> 'using AutoMapSharp;'
  * AMSMIG002: ': Profile' -> ': MappingModule'
  * AMSMIG003: 'CreateMap<T, U>()' -> 'DefineMap<T, U>()'
  * AMSMIG004: '.ForMember(...)' -> '.ForProperty(...)'
  * AMSMIG005: '.MapFrom(...)' -> '.MapSource(...)'
  * AMSMIG006: '.Ignore()' -> '.Skip()'
  * AMSMIG007: '.ProjectTo<T>()' -> '.ProjectOnto<T>()'
  * AMSMIG008: 'IMapper' -> 'IMapEngine'
  * AMSMIG009: '.Condition(...)' -> '.If(...)'
  * AMSMIG010: '.NullSubstitute(...)' -> '.Fallback(...)'
  * AMSMIG011: 'IValueResolver<...>' -> 'IPropertyResolver<...>'
  * AMSMIG005: '.MapFrom<TResolver>()' -> '.MapWith<TResolver>()'
  * AMSMIG007: 'ProjectTo<T>(mapper.ConfigurationProvider)' -> 'ProjectOnto<T>(mapper)'
");
    }
}
