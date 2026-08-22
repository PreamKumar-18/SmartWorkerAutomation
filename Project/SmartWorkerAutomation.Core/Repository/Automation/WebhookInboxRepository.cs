using Dapper;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class WebhookInboxRepository : IWebhookInboxRepository
{
    private readonly MasterDbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public WebhookInboxRepository(MasterDbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<long> InsertPendingAsync(string channel, string rawPayloadJson)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("WebhookInbox:InsertPending");
        return await connection.ExecuteScalarAsync<long>(query, new { Channel = channel, RawPayload = rawPayloadJson });
    }

    public async Task<IEnumerable<WebhookInboxItem>> ClaimPendingBatchAsync(int batchSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("WebhookInbox:ClaimPendingBatch");
        return await connection.QueryAsync<WebhookInboxItem>(query, new { BatchSize = batchSize });
    }

    public async Task MarkProcessedAsync(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("WebhookInbox:MarkProcessed");
        await connection.ExecuteAsync(query, new { Id = id });
    }

    public async Task MarkFailedAsync(long id, string error, int maxAttempts)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("WebhookInbox:MarkFailed");
        await connection.ExecuteAsync(query, new { Id = id, Error = error, MaxAttempts = maxAttempts });
    }

    public async Task<int> DeleteExpiredAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("WebhookInbox:DeleteExpired");
        return await connection.ExecuteAsync(query);
    }
}
