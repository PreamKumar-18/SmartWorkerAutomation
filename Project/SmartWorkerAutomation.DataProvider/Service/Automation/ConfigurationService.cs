using System;
using System.Data;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Dapper;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class ConfigurationService : IConfigurationService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public ConfigurationService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<bool> UpdateRuleAlertConfigAsync(UpdateRuleAlertConfigRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Configuration:UpdateRuleAlertConfig");
        return await connection.ExecuteScalarAsync<bool>(sql, new
        {
            p_id = request.Id,
            p_whatsapp_message_body_template = request.WhatsappMessageBodyTemplate
        });
    }

    public async Task<bool> UpdateEmailTemplateConfigAsync(UpdateEmailTemplateConfigRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Configuration:UpdateEmailTemplateConfig");
        return await connection.ExecuteScalarAsync<bool>(sql, new
        {
            p_id = request.Id,
            p_subject_template = request.SubjectTemplate,
            p_body_template = request.BodyTemplate
        });
    }
}
