using NLog;

namespace SoTF.Editor.Services.Savegames;

public static class Polling
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static async Task EveryTwoSecondsAsync(Func<Task> tick, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var isFailing = false;
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                // A Failing Tick Never Ends The Loop, Logged Once Per Streak
                try
                {
                    await tick();
                    isFailing = false;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    if (!isFailing && !token.IsCancellationRequested)
                    {
                        s_logger.Error(exception, "Failed To Watch Savegames");
                    }

                    isFailing = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The Window Or Page Closed
        }
    }
}
