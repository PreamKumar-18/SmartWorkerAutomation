using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// In-process replacement for the retired n8n workflow
/// "WF: Daily Refresh Automation Records (5AM)". SmartWorker is migrating
/// off n8n entirely (see NotificationsController/FileIngestionService doc
/// comments for the earlier controller-endpoint half of that migration) -
/// this one has no HTTP-triggered counterpart because nothing external
/// needs to call it: it's a pure schedule, so it lives entirely as a
/// .NET BackgroundService instead of an n8n Schedule Trigger calling into
/// the API.
///
/// Every day at 5:00 AM IST (Asia/Kolkata) it runs
/// Config/Queries.json's Automation:DailyRefresh
/// ("CALL public.refresh_automation_records_daily(NULL)") once, then sleeps
/// until the next 5:00 AM IST. IST is resolved defensively - Linux/Docker
/// images don't always ship the IANA tzdata package - by trying the IANA id
/// first, then the Windows id, then finally falling back to a fixed
/// UTC+5:30 offset (India doesn't observe DST, so a fixed offset is exactly
/// correct even without a real tzdata entry).
/// </summary>
public class DailyAutomationRefreshService : BackgroundService
{
    private static readonly TimeSpan RunTimeOfDayIst = new(5, 0, 0);

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly ILogger<DailyAutomationRefreshService> _logger;
    private readonly TimeZoneInfo _istZone;

    public DailyAutomationRefreshService(
        DbConnectionFactory connectionFactory,
        IQueryStore queryStore,
        ILogger<DailyAutomationRefreshService> logger)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _logger = logger;
        _istZone = ResolveIndiaTimeZone(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.UtcNow);
            _logger.LogInformation(
                "WF: Daily Refresh Automation Records (5AM) - next run in {Delay} (around {NextRunUtc:u} UTC).",
                delay,
                DateTimeOffset.UtcNow.Add(delay));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunRefreshAsync();
        }
    }

    private async Task RunRefreshAsync()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var sql = _queryStore.Get("Automation:DailyRefresh");
            await connection.ExecuteAsync(sql);
            _logger.LogInformation("WF: Daily Refresh Automation Records (5AM) completed successfully.");
        }
        catch (Exception ex)
        {
            // Best-effort daily job: log and let the loop pick it up again
            // tomorrow rather than crashing the whole API host over one
            // failed refresh.
            _logger.LogError(ex, "WF: Daily Refresh Automation Records (5AM) failed.");
        }
    }

    /// <summary>
    /// How long to sleep from <paramref name="nowUtc"/> until the next
    /// 5:00 AM IST. If it's already past 5:00 AM IST today, targets
    /// tomorrow's 5:00 AM IST instead.
    /// </summary>
    internal TimeSpan GetDelayUntilNextRun(DateTimeOffset nowUtc)
    {
        var nowIst = TimeZoneInfo.ConvertTime(nowUtc, _istZone);
        var todayRunIst = new DateTimeOffset(nowIst.Date, nowIst.Offset).Add(RunTimeOfDayIst);
        var nextRunIst = nowIst < todayRunIst ? todayRunIst : todayRunIst.AddDays(1);
        return nextRunIst - nowIst;
    }

    private static TimeZoneInfo ResolveIndiaTimeZone(ILogger logger)
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // try the next id
            }
            catch (InvalidTimeZoneException)
            {
                // try the next id
            }
        }

        logger.LogWarning(
            "Could not resolve an IST time zone from the OS tzdata (tried 'Asia/Kolkata' and 'India Standard Time'). " +
            "Falling back to a fixed UTC+05:30 offset - correct for India, which does not observe DST.");
        return TimeZoneInfo.CreateCustomTimeZone("IST_Fixed", TimeSpan.FromHours(5.5), "India Standard Time (fixed)", "IST");
    }
}
