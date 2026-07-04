using System.CommandLine;
using RecurOpus.Models;
using RecurOpus.Services;
using Serilog;
using Serilog.Events;

namespace RecurOpus.Commands;

public static class ConvertCommand
{
	public static RootCommand BuildCommand()
	{
		var rootCommand = new RootCommand("Recursively convert audio files to Opus format using ffmpeg and opusenc.");

		var bitrateOption = new Option<int>("--bitrate", "-b")
		{
			Description = "Target bitrate in kbps for Opus encoding.",
			DefaultValueFactory = _ => 160
		};

		var maxJobsOption = new Option<int>("--maxjobs", "-j")
		{
			Description = "Maximum number of simultaneous conversion jobs.",
			DefaultValueFactory = _ => Environment.ProcessorCount
		};

		var outDirOption = new Option<string>("--outdir", "-o", "--output")
		{
			Description = "Output directory for converted files.",
			DefaultValueFactory = _ => CommandConfig.DefaultOutputDir
		};

		var inDirOption = new Option<string>("--indir", "-i")
		{
			Description = "Input directory to recursively scan for audio files.",
			DefaultValueFactory = _ => Environment.CurrentDirectory
		};

		var fullRegexOption = new Option<string?>("--fullregex", "-p")
		{
			Description = "Full path regex pattern to filter files/directories. Overrides --fileregex and --dirregex if set."
		};

		var fileRegexOption = new Option<string?>("--fileregex", "-fr")
		{
			Description = "Regex pattern to filter filenames."
		};

		var dirRegexOption = new Option<string?>("--dirregex", "-dr")
		{
			Description = "Regex pattern to filter directory names."
		};

		var extensionsOption = new Option<string>("--extensions", "-e")
		{
			Description = "Comma-separated list of audio file extensions to convert.",
			DefaultValueFactory = _ => CommandConfig.DefaultExtensions
		};

		var dryRunOption = new Option<bool>("--dryrun", "-d")
		{
			Description = "Preview actions without performing any conversions or copies."
		};

		var flattenOption = new Option<bool>("--flatten", "-f")
		{
			Description = "Place all output files directly in the output root, ignoring directory structure."
		};

		var noCopyOption = new Option<bool>("--nocopy", "-nc", "--convertonly")
		{
			Description = "Skip copying non-audio files; only convert matching audio files."
		};

		var verboseOption = new Option<bool>("--verbose", "-v")
		{
			Description = "Enable verbose output."
		};

		var quietOption = new Option<bool>("--quiet", "-q")
		{
			Description = "Suppress non-error output."
		};

		var caseInsensitiveOption = new Option<bool>("--case-insensitive", "-ip")
		{
			Description = "Make regex pattern matching case-insensitive."
		};

		rootCommand.Options.Add(bitrateOption);
		rootCommand.Options.Add(maxJobsOption);
		rootCommand.Options.Add(outDirOption);
		rootCommand.Options.Add(inDirOption);
		rootCommand.Options.Add(fullRegexOption);
		rootCommand.Options.Add(fileRegexOption);
		rootCommand.Options.Add(dirRegexOption);
		rootCommand.Options.Add(extensionsOption);
		rootCommand.Options.Add(dryRunOption);
		rootCommand.Options.Add(flattenOption);
		rootCommand.Options.Add(noCopyOption);
		rootCommand.Options.Add(verboseOption);
		rootCommand.Options.Add(quietOption);
		rootCommand.Options.Add(caseInsensitiveOption);

		rootCommand.SetAction(async (parseResult, ct) =>
		{
			var verbose = parseResult.GetValue(verboseOption);
			var quiet = parseResult.GetValue(quietOption);

			Program.LevelSwitch.MinimumLevel = verbose
				? LogEventLevel.Debug
				: quiet
					? LogEventLevel.Warning
					: LogEventLevel.Information;

			var config = new CommandConfig
			{
				Bitrate = parseResult.GetRequiredValue(bitrateOption),
				MaxJobs = parseResult.GetRequiredValue(maxJobsOption),
				OutputDir = parseResult.GetRequiredValue(outDirOption),
				InputDir = parseResult.GetRequiredValue(inDirOption),
				FullRegex = parseResult.GetValue(fullRegexOption),
				FileRegex = parseResult.GetValue(fileRegexOption),
				DirRegex = parseResult.GetValue(dirRegexOption),
				Extensions = parseResult.GetRequiredValue(extensionsOption),
				DryRun = parseResult.GetValue(dryRunOption),
				Flatten = parseResult.GetValue(flattenOption),
				NoCopy = parseResult.GetValue(noCopyOption),
				Verbose = verbose,
				Quiet = quiet,
				CaseInsensitive = parseResult.GetValue(caseInsensitiveOption)
			};

			try
			{
				config.CompileRegexes();
			}
			catch (ArgumentException ex)
			{
				Log.Error(ex, "Invalid regex pattern");
				return 1;
			}

			await ConverterService.Run(config);
			return 0;
		});

		return rootCommand;
	}
}
