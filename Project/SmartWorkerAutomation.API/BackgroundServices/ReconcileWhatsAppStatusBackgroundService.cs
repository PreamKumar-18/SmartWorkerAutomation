using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

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

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly ILogger<ReconcileWhatsAppStatusBackgroundService> _logger;

    public ReconcileWhatsAppStatusBackgroundService(
        DbConnectionFactory connectionFactory,
        IQueryStore queryStore,
        ILogger<ReconcileWhatsAppStatusBackgroundService> logger)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var sql = _queryStore.Get("WhatsAppReconcile:Reconcile");
                await connection.ExecuteAsync(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WF: Reconcile WhatsApp Status (Schedule) - reconcile call failed; will retry next cycle.");
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
}
