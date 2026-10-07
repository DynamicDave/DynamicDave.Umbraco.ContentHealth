using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace DynamicDave.Umbraco.ContentHealth.Scanning;

/// <summary>
/// Checks every minute whether a full scan is due (first run, requested, nightly) and runs it. Compiled against the
/// Umbraco 17 interface; the members Umbraco 18 added have default implementations.
/// </summary>
internal sealed class FullScanJob(
    ScanStateStore store,
    FullScanner scanner,
    IOptionsMonitor<ContentHealthOptions> options,
    TimeProvider timeProvider,
    IRuntimeState runtimeState,
    ILogger<FullScanJob> logger) : IRecurringBackgroundJob
{
    private bool _reportedInvalidTime;

    public TimeSpan Period => TimeSpan.FromMinutes(1);

    public TimeSpan Delay => TimeSpan.FromMinutes(1);

    // One scanning server in a load-balanced setup.
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    public event EventHandler PeriodChanged { add { } remove { } }

    public Task RunJobAsync()
    {
        if (runtimeState.Level != RuntimeLevel.Run)
        {
            return Task.CompletedTask;
        }

        var settings = options.CurrentValue;
        if (settings.HasInvalidNightlyScanTime && !_reportedInvalidTime)
        {
            _reportedInvalidTime = true;
            logger.LogWarning("{Section}:NightlyScanTime '{Value}' is not a valid time (HH:mm); the nightly scan is disabled", Constants.OptionsSection, settings.NightlyScanTime);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (state, raw) = store.Read();
        if (!ScanScheduler.ShouldStart(state, now, settings.GetNightlyScanTime(), timeProvider.LocalTimeZone))
        {
            return Task.CompletedTask;
        }

        var running = store.TryStart(raw, state, now);
        if (running is null)
        {
            return Task.CompletedTask; // claimed by another process
        }

        try
        {
            scanner.Run(running);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Content health scan failed");
            var current = store.Read().State ?? running;
            store.Save(current with { Phase = ScanPhase.Idle, LastError = exception.Message });
        }

        return Task.CompletedTask;
    }
}
