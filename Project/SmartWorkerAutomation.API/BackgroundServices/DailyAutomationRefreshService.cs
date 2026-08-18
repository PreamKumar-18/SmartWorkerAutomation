using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;

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
/// By default it runs at 5:00 AM IST (Asia/Kolkata), but the time of day is
/// configurable via appsettings' Automation:DailyRefreshTimeIst
/// ("HH:mm:ss", always interpreted in IST) - falls back to 05:00:00 if
/// missing or unparsable. It runs Config/Queries.json's
/// Automation:DailyRefresh ("CALL public.refresh_automation_records_daily(NULL)")
/// once, then sleeps until the next occurrence of that time. IST is resolved
/// defensively - Linux/Docker images don't always ship the IANA tzdata
/// package - by trying the IANA id first, then the Windows id, then finally
/// falling back to a fixed UTC+5:30 offset (India doesn't observe DST, so a
/// fixed offset is exactly correct even without a real tzdata entry).
/// </summary>
public class DailyAutomationRefreshService : BackgroundService
{
    private static readonly TimeSpan DefaultRunTimeOfDayIst = new(5, 0, 0);

    private readonly IQueryStore _queryStore;
    private readonly ILogger<DailyAutomationRefreshService> _logger;
    private readonly TimeZoneInfo _istZone;
    private readonly TimeSpan _runTimeOfDayIst;
    private readonly ConnectionStringEncryptor _encryptor;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyAutomationRefreshService(
        IQueryStore queryStore,
        IConfiguration configuration,
        ILogger<DailyAutomationRefreshService> logger,
        ConnectionStringEncryptor encryptor, IServiceScopeFactory scopeFactory)
    {
        _queryStore = queryStore;
        _logger = logger;
        _istZone = ResolveIndiaTimeZone(logger);
        _runTimeOfDayIst = ResolveRunTimeOfDay(configuration, logger);
        _encryptor = encryptor;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Reads Automation:DailyRefreshTimeIst ("HH:mm:ss") from config - a
    /// plain TimeSpan.TryParse accepts that format directly. Falls back to
    /// 05:00:00 (logged as a warning, not an error - a missing/blank value
    /// is the expected state until someone opts into overriding it) if the
    /// key is absent, blank, or not parseable, or if it resolves to
    /// something outside a single day (negative or >= 24h makes no sense
    /// for "time of day").
    /// </summary>
    private static TimeSpan ResolveRunTimeOfDay(IConfiguration configuration, ILogger logger)
    {
        var configured = configuration["Automation:DailyRefreshTimeIst"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultRunTimeOfDayIst;
        }

        if (TimeSpan.TryParse(configured, out var parsed) && parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1))
        {
            return parsed;
        }

        logger.LogWarning(
            "Automation:DailyRefreshTimeIst value '{Configured}' is not a valid time of day (expected HH:mm:ss) - falling back to the default {Default}.",
            configured,
            DefaultRunTimeOfDayIst);
        return DefaultRunTimeOfDayIst;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.UtcNow);
            _logger.LogInformation(
                "WF: Daily Refresh Automation Records (12.15 AM) - next run in {Delay} (around {NextRunUtc:u} UTC).",
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
        using var scope = _scopeFactory.CreateScope();
        var masterAuthRepository = scope.ServiceProvider.GetRequiredService<IMasterAuthRepository>();

        var tenants = await masterAuthRepository.GetAllActiveTenantConnectionsAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                var decrypted = _encryptor.Decrypt(tenant.EncryptedConnectionString);
                using var connection = new NpgsqlConnection(decrypted);
                var sql = _queryStore.Get("Automation:DailyRefresh");
                await connection.ExecuteAsync(sql);
                _logger.LogInformation("WF: Daily Refresh Automation Records (12.15 AM) completed for orgid {OrgId}.", tenant.OrgId);
            }
            catch (Exception ex)
            {
                // One tenant's failure (bad connection string, DB down,
                // etc.) must not stop the refresh for every other tenant.
                _logger.LogError(ex, "WF: Daily Refresh Automation Records (12.15 AM) failed for orgid {OrgId}.", tenant.OrgId);
            }
        }
    }

    /// <summary>
    /// How long to sleep from <paramref name="nowUtc"/> until the next
    /// occurrence of the configured run time (Automation:DailyRefreshTimeIst,
    /// default 5:00 AM IST). If it's already past that time today, targets
    /// tomorrow's occurrence instead.
    /// </summary>
    internal TimeSpan GetDelayUntilNextRun(DateTimeOffset nowUtc)
    {
        var nowIst = TimeZoneInfo.ConvertTime(nowUtc, _istZone);
        var todayRunIst = new DateTimeOffset(nowIst.Date, nowIst.Offset).Add(_runTimeOfDayIst);
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
