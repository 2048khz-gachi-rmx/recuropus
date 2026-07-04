using RecurOpus.Models;
using Serilog;

namespace RecurOpus.Services;

public static class ConverterService
{
	public static async Task Run(CommandConfig config)
	{
		// Resolve binary paths: bundled copy first, fall back to PATH
		var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
		var opusencPath = Path.Combine(AppContext.BaseDirectory, "opusenc.exe");
		if (!File.Exists(ffmpegPath)) ffmpegPath = "ffmpeg";
		if (!File.Exists(opusencPath)) opusencPath = "opusenc";

		// Normalize input/output directories
		var inputRoot = Path.GetFullPath(config.InputDir).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;

		// If output dir was not specified, derive it from the input root's resolved name
		var outputDir = config.OutputDir;
		if (string.IsNullOrEmpty(outputDir))
		{
			var inputName = Path.GetFileName(inputRoot.TrimEnd('/', '\\'));
			outputDir = $"./{inputName}";
		}

		var outputRoot = Path.IsPathRooted(outputDir)
			? outputDir.TrimEnd('/', '\\') + Path.DirectorySeparatorChar
			: Path.GetFullPath(outputDir).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;

		// Validate input directory
		if (!Directory.Exists(inputRoot))
		{
			Log.Error("Input directory does not exist: {inputRoot}", inputRoot);
			return;
		}

		// Create output directory if missing
		if (!Directory.Exists(outputRoot))
		{
			Directory.CreateDirectory(outputRoot);
		}

		Log.Information("Converting from {inputRoot}", inputRoot);
		Log.Information("Converting to {bitrate}kbps ({maxJobs} simultaneous jobs)", config.Bitrate, config.MaxJobs);
		Log.Information("Output will be placed in {outputRoot}", outputRoot);

		var toConvert = config.GetExtensions();

		var convQueue = new List<(string Source, string Dest)>();

		RecurseDirectory(inputRoot, outputRoot, inputRoot, config, toConvert, convQueue);

		// Process the conversion queue with job limiting
		if (convQueue.Count > 0)
		{
			Log.Information("Converting {count} file(s)...", convQueue.Count);

			var semaphore = new SemaphoreSlim(config.MaxJobs, config.MaxJobs);
			var tasks = new List<Task>();

			for (var i = 0; i < convQueue.Count; i++)
			{
				var (source, dest) = convQueue[i];
				await semaphore.WaitAsync();
				var capturedIdx = i;

				var task = Task.Run(async () =>
				{
					try
					{
						var relSource = source.StartsWith(inputRoot, StringComparison.Ordinal)
							? source.Substring(inputRoot.Length)
							: source;
						var relDest = dest.StartsWith(outputRoot, StringComparison.Ordinal)
							? dest.Substring(outputRoot.Length)
							: dest;

						if (config.DryRun)
						{
							Log.Information("[DRY RUN] Encoding {relSource} -> {relDest}", relSource, relDest);
						}
						else
						{
							Log.Information("{idx}/{total}: {relSource}", capturedIdx + 1, convQueue.Count, relSource);

							// Ensure destination directory exists
							var destDir = Path.GetDirectoryName(dest)!;
							if (!Directory.Exists(destDir))
							{
								Directory.CreateDirectory(destDir);
							}

							var progress = new Progress<string>(Log.Debug);

							var convParams = new ConversionParams
							{
								FfmpegPath = ffmpegPath,
								OpusencPath = opusencPath,
								AlbumArtSize = 640, // TODO: this should be a config
								Bitrate = config.Bitrate,
								InputPath = source,
								OutputPath = dest
							};

							var (success, ffmpegErr, opusencErr) = await AudioConverter.Convert(convParams, progress);

							if (!success)
							{
								if (ffmpegErr != null) Log.Error("ffmpeg error: {error}", ffmpegErr);
								if (opusencErr != null) Log.Error("opusenc error: {error}", opusencErr);
							}
							else
							{
								Log.Debug("OK: {relDest}", relDest);
							}
						}
					}
					finally
					{
						semaphore.Release();
					}
				});

				tasks.Add(task);
			}

			await Task.WhenAll(tasks);
		}

		Log.Information("Done.");
	}

	/// <summary>
	/// Recursively scan the input directory, building a conversion queue and copying non-audio files.
	/// </summary>
	static void RecurseDirectory(string dirPath, string outputRoot, string inputRoot, CommandConfig config,
		HashSet<string> toConvert, List<(string Source, string Dest)> convQueue)
	{
		if (dirPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
			return;

		if (config.CompiledDirRegex != null && !(config.CompiledDirRegex?.IsMatch(dirPath) ?? true))
		{
			Log.Debug("[DIR SKIP] {dirPath}", dirPath);
			return;
		}

		try
		{
			foreach (var entry in Directory.EnumerateFileSystemEntries(dirPath))
			{
				var isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) == FileAttributes.Directory;

				if (isDirectory)
					RecurseDirectory(entry, outputRoot, inputRoot, config, toConvert, convQueue);
				else
					HandleFile(entry, outputRoot, inputRoot, config, toConvert, convQueue);
			}
		}
		catch (UnauthorizedAccessException)
		{
			// Skip directories we don't have access to
		}
		catch (Exception ex)
		{
			Log.Error(ex, $"Error thrown while traversing {dirPath}");
		}
	}

	/// <summary>
	/// Handle a single file: check filters, determine if it should be converted or copied.
	/// </summary>
	static void HandleFile(string filePath, string outputRoot, string inputRoot, CommandConfig config,
		HashSet<string> toConvert, List<(string Source, string Dest)> convQueue)
	{
		var fileName = Path.GetFileName(filePath);
		var ext = Path.GetExtension(filePath).ToLowerInvariant();

		if (config.CompiledFileRegex != null && !(config.CompiledFileRegex?.IsMatch(fileName) ?? true))
		{
			Log.Debug("[FILE SKIP] {fileName}", fileName);
			return;
		}

		if (config.CompiledFullRegex != null && !(config.CompiledFullRegex?.IsMatch(fileName) ?? true))
		{
			Log.Debug("[FULL SKIP] {filePath}", filePath);
			return;
		}

		// Determine destination path
		string destPath;
		if (config.Flatten)
		{
			destPath = Path.Combine(outputRoot, fileName);
		}
		else
		{
			var relative = filePath.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase)
				? filePath.Substring(inputRoot.Length)
				: filePath;
			destPath = Path.Combine(outputRoot, relative);
		}

		var shouldConvert = toConvert.Contains(ext);

		if (shouldConvert)
		{
			var newName = Path.GetFileNameWithoutExtension(filePath)
				.Replace("Topic - ", "")
				+ ".opus";

			var finalDest = Path.Combine(Path.GetDirectoryName(destPath)!, newName);
			convQueue.Add((filePath, finalDest));
		}
		else if (!config.NoCopy)
		{
			// Copy non-audio files
			if (!config.DryRun)
			{
				var destDir = Path.GetDirectoryName(destPath)!;
				if (!Directory.Exists(destDir))
					Directory.CreateDirectory(destDir);
				File.Copy(filePath, destPath, true);
				Log.Debug("[COPY] {fileName}", Path.GetFileName(filePath));
			}
			else
			{
				Log.Information("[DRY RUN] Copy {fileName} -> {destPath}", Path.GetFileName(filePath), destPath);
			}
		}
	}
}