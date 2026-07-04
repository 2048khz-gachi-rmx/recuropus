using System.Text.RegularExpressions;

namespace RecurOpus.Models;

public class CommandConfig
{
    public const string DefaultOutputDir = "";
    public const string DefaultExtensions = "wav,mp3,flac,m4a";

    // `required` so you don't forget to set the field
    public required int Bitrate { get; init; }
    public required int MaxJobs { get; init; }
    public required string InputDir { get; init; }
    public required string OutputDir { get; init; } = DefaultOutputDir;
    public required string? FileRegex { get; init; }
    public required string? DirRegex { get; init; }
    public required string? FullRegex { get; init; }
    public required string Extensions { get; init; } = DefaultExtensions;
    public required bool DryRun { get; init; }
    public required bool Flatten { get; init; }
    public required bool NoCopy { get; init; }
    public required bool Verbose { get; init; }
    public required bool Quiet { get; init; }
    public required bool CaseInsensitive { get; init; }

    public HashSet<string> GetExtensions()
    {
        return new HashSet<string>(
            Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(e => "." + e.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase
        );
    }

    public Regex? CompiledFileRegex { get; private set; }
    public Regex? CompiledDirRegex { get; private set; }
    public Regex? CompiledFullRegex { get; private set; }

    public void CompileRegexes()
    {
        var options = (CaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.Compiled;
        
        if (!string.IsNullOrEmpty(FileRegex))
            CompiledFileRegex = new Regex(FileRegex, options, TimeSpan.FromSeconds(1));
        if (!string.IsNullOrEmpty(DirRegex))
            CompiledDirRegex = new Regex(DirRegex, options, TimeSpan.FromSeconds(1));
        if (!string.IsNullOrEmpty(FullRegex))
            CompiledFullRegex = new Regex(FullRegex, options, TimeSpan.FromSeconds(1));
    }
}
