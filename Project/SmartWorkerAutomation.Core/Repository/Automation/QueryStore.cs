using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.Core.Repository.Automation;

/// <inheritdoc cref="IQueryStore"/>
public class QueryStore : IQueryStore
{
    private readonly IConfiguration _configuration;

    public QueryStore(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Get(string key)
    {
        var value = _configuration[$"Queries:{key}"];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Missing SQL query 'Queries:{key}' - check Config/Queries.json.");
        }

        return value;
    }

    public string Render(string key, IReadOnlyDictionary<string, string> tokens)
    {
        var sql = Get(key);
        foreach (var (token, value) in tokens)
        {
            sql = sql.Replace("{" + token + "}", value);
        }

        return sql;
    }
}
