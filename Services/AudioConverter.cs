using System.Diagnostics;
using System.Text;
using RecurOpus.Models;

namespace RecurOpus.Services;

public static class AudioConverter
{
    /// <summary>
    /// Spawns ffmpeg for decoding, pipes its' stdout to opusenc, and waits for completion.
    /// </summary>
    /// <returns>Information about the operation's status. If successful, there will be an opus file at outputPath</returns>
    public static async Task<(bool Success, string? FfmpegError, string? OpusencError)> Convert(
        ConversionParams convParams, IProgress<string>? logger = null)
    {
        var ffmpegErrorBuilder = new StringBuilder();
        var opusencErrorBuilder = new StringBuilder();

        var ffmpegArgs = new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-i", convParams.InputPath,
            "-map_metadata", "0",
            "-metadata", "comment=",
            "-metadata", "purl=",
            "-metadata", "synopsis=",
            "-metadata", "description=",
            "-metadata", "encoder=",
            "-vsync", "0",
            "-c:v", "mjpeg",
            "-vf", $"scale=-1:'min(iw,{convParams.AlbumArtSize})'",
            "-f", "flac",
            "-n",
            "pipe:1"
        };

        // Build opusenc args: read FLAC from stdin -> encode to opus
        var opusencArgs = new[]
        {
            "--music",
            "--framesize", "20",
            "--bitrate", convParams.Bitrate.ToString(),
            "--quiet",
            "-",
            convParams.OutputPath
        };

        logger?.Report($"ffmpeg args: {string.Join(" ", ffmpegArgs)}");
        logger?.Report($"opusenc args: {string.Join(" ", opusencArgs)}");

        var ffmpegStartInfo = new ProcessStartInfo
        {
            FileName = convParams.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        foreach (var arg in ffmpegArgs)
            ffmpegStartInfo.ArgumentList.Add(arg);
        
        using var ffmpegProcess = new Process { StartInfo = ffmpegStartInfo };
        
        var opusencStartInfo = new ProcessStartInfo
        {
            FileName = convParams.OpusencPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        foreach (var arg in opusencArgs)
            opusencStartInfo.ArgumentList.Add(arg);
        
        using var opusencProcess = new Process { StartInfo = opusencStartInfo };

        try
        {
            ffmpegProcess.Start();
            opusencProcess.Start();

            // Capture stderr from both processes asynchronously
            var ffmpegErrorTask = CaptureStdErrorAsync(ffmpegProcess.StandardError, ffmpegErrorBuilder);
            var opusencErrorTask = CaptureStdErrorAsync(opusencProcess.StandardError, opusencErrorBuilder);

            // Pipe ffmpeg stdout -> opusenc stdin
            await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(opusencProcess.StandardInput.BaseStream);
            opusencProcess.StandardInput.Close();

            // Wait for both processes to exit. Unsure if should await error tasks if i'm already awaiting exits,
            // but whatever i guess, might as well
            await Task.WhenAll(
                ffmpegErrorTask,
                opusencErrorTask,
                ffmpegProcess.WaitForExitAsync(),
                opusencProcess.WaitForExitAsync());
            

            var success = ffmpegProcess.ExitCode == 0 && opusencProcess.ExitCode == 0;

            return (
                success,
                ffmpegErrorBuilder.Length > 0 ? ffmpegErrorBuilder.ToString() : null,
                opusencErrorBuilder.Length > 0 ? opusencErrorBuilder.ToString() : null
            );
        }
        catch (Exception ex)
        {
            TryKillProcess(ffmpegProcess);
            TryKillProcess(opusencProcess);
            return (false, ex.Message, null);
        }
    }

    private static async Task CaptureStdErrorAsync(TextReader reader, StringBuilder builder)
    {
        var buffer = new char[4096];
        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            builder.Append(buffer, 0, charsRead);
        }
    }

    private static bool TryKillProcess(Process proc)
    {
        try
        {
            proc.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
