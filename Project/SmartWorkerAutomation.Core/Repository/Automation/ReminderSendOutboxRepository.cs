using Dapper;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class ReminderSendOutboxRepository : IReminderSendOutboxRepository
{
    private readonly MasterDbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public ReminderSendOutboxRepository(MasterDbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<long?> InsertPendingAsync(int orgId, int automationRecordId, string channel, string sendWindowKey, string payloadJson)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:InsertPending");
        return await connection.ExecuteScalarAsync<long?>(query, new
        {
            OrgId = orgId,
            AutomationRecordId = automationRecordId,
            Channel = channel,
            SendWindowKey = sendWindowKey,
            Payload = payloadJson
        });
    }

    public async Task<IEnumerable<ReminderSendOutboxItem>> ClaimPendingBatchAsync(int batchSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:ClaimPendingBatch");
        return await connection.QueryAsync<ReminderSendOutboxItem>(query, new { BatchSize = batchSize });
    }

    public async Task MarkSentAsync(long id, string? providerMessageId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:MarkSent");
        await connection.ExecuteAsync(query, new { Id = id, ProviderMessageId = providerMessageId });
    }

    public async Task MarkFailedAsync(long id, string error)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:MarkFailed");
        await connection.ExecuteAsync(query, new { Id = id, Error = error });
    }

    public async Task MarkUnknownAsync(long id, string error)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:MarkUnknown");
        await connection.ExecuteAsync(query, new { Id = id, Error = error });
    }

    public async Task<long?> ResolveUnknownWhatsAppAsync(int orgId, string recipient, string resolvedStatus, string providerMessageId, string? errorDetail)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:ResolveUnknownWhatsApp");
        return await connection.ExecuteScalarAsync<long?>(query, new
        {
            OrgId = orgId,
            Recipient = recipient,
            ResolvedStatus = resolvedStatus,
            ProviderMessageId = providerMessageId,
            ErrorDetail = errorDetail
        });
    }

    public async Task<IEnumerable<PresumedSentEmailTicket>> PresumeSentEmailTicketsAsync(int graceMinutes)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("ReminderSendOutbox:PresumeSentEmailTickets");
        return await connection.QueryAsync<PresumedSentEmailTicket>(query, new { GraceMinutes = graceMinutes });
    }
}
