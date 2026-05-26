using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rufus.Cli.Tui;

internal interface IRfsTuiRunCancellationKeySource
{
    bool IsInteractive { get; }

    bool KeyAvailable { get; }

    ConsoleKeyInfo ReadKey(bool intercept);
}

internal sealed class ConsoleRfsTuiRunCancellationKeySource : IRfsTuiRunCancellationKeySource
{
    public bool IsInteractive => RfsTuiTerminal.IsInteractive;

    public bool KeyAvailable => Console.KeyAvailable;

    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
}

internal static class RfsTuiHermesRunCancellationMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    internal static Task WatchAsync(
        CancellationTokenSource activeCancellationSource,
        IRfsTuiRunCancellationKeySource? keySource = null,
        CancellationToken cancellationToken = default)
    {
        keySource ??= new ConsoleRfsTuiRunCancellationKeySource();
        if (!keySource.IsInteractive)
        {
            return Task.CompletedTask;
        }

        return WatchInteractiveAsync(activeCancellationSource, keySource, cancellationToken);
    }

    private static async Task WatchInteractiveAsync(
        CancellationTokenSource activeCancellationSource,
        IRfsTuiRunCancellationKeySource keySource,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !activeCancellationSource.IsCancellationRequested)
        {
            try
            {
                if (!keySource.KeyAvailable)
                {
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var key = keySource.ReadKey(intercept: true);
                if (char.ToLowerInvariant(key.KeyChar) == 'q')
                {
                    activeCancellationSource.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
        }
    }
}
