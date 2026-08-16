using Dapper;
using Npgsql;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;

namespace SmartWorkerAutomation.API.BackgroundServices;

/// <summary>
/// Native replacement for the retired n8n workflow
/// "WF: Reconcile WhatsApp Status (Schedule)" (id rDMqQ9kl3gEWnHuu) - the
/// entire workflow was one node: a Schedule Trigger firing every 2 minutes
/// into "SELECT public.fn_reconcile_whatsapp_status();". This is an
/// independent periodic safety net for the same reconcile call
/// ReminderSendBackgroundService/the WhatsApp reply-capture pipeline
/// already trigger inline whenever a status event arrives - it catches
/// anything those event-driven calls might miss.
/// </summary>
public class ReconcileWhatsAppStatusBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    private readonly IQueryStore _queryStore;
    private readonly ILogger<ReconcileWhatsAppStatusBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionStringEncryptor _encryptor;

    public ReconcileWhatsAppStatusBackgroundService(
        IQueryStore queryStore,
        ILogger<ReconcileWhatsAppStatusBackgroundService> logger, IServiceScopeFactory scopeFactory, ConnectionStringEncryptor encryptor)
    {
        _queryStore = queryStore;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _encryptor = encryptor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunReconcileCycleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Reconcile WhatsApp Status (Schedule) - cycle failed; will retry next cycle.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunReconcileCycleAsync()
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
                var sql = _queryStore.Get("WhatsAppReconcile:Reconcile");
                await connection.ExecuteAsync(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Reconcile WhatsApp Status (Schedule) - reconcile call failed for orgid {OrgId}; will retry next cycle.", tenant.OrgId);
            }
        }
    }
}
