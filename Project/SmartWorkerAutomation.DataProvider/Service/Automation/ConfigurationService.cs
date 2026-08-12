using System;
using System.Collections.Generic;
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

    /// <summary>
    /// See Queries.json's Configuration:GetManualRule comment. Reads via the
    /// dynamic-row helpers (this codebase doesn't configure a
    /// snake_case-to-PascalCase Dapper type map) since the query intentionally
    /// returns raw column names (id, rule_name), not aliased ones.
    /// </summary>
    public async Task<(int Id, string RuleName, string? TemplateName, string? LanguageCode)?> GetManualRuleAsync(string categoryName)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Configuration:GetManualRule");
        var row = await connection.QuerySingleOrDefaultAsync(sql, new { CategoryName = categoryName });
        if (row is null)
        {
            return null;
        }

        var fields = (IDictionary<string, object>)row;
        return (
            fields.GetInt("id"),
            fields.GetString("rule_name") ?? "Custom",
            fields.GetString("template_name"),
            fields.GetString("language_code"));
    }
}
