using RecurOpus.Commands;
using Serilog;
using Serilog.Core;

namespace RecurOpus;

public static class Program
{
    public static LoggingLevelSwitch LevelSwitch { get; } = new();

    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var command = ConvertCommand.BuildCommand();
            return await command.Parse(args).InvokeAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled error");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
