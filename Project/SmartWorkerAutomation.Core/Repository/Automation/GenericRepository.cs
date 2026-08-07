using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly DbConnectionFactory _connectionFactory;
    protected readonly IQueryStore _queryStore;
    protected readonly string _tableName;

    public GenericRepository(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _tableName = typeof(T).Name; // Assumes table name matches entity class name
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Render("GenericRepository:GetById", new Dictionary<string, string> { ["TableName"] = _tableName });
        return await connection.QuerySingleOrDefaultAsync<T>(query, new { Id = id });
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Render("GenericRepository:GetAll", new Dictionary<string, string> { ["TableName"] = _tableName });
        return await connection.QueryAsync<T>(query);
    }

    public virtual async Task<int> AddAsync(T entity)
    {
        using var connection = _connectionFactory.CreateConnection();
        var properties = GetProperties(excludeId: true);
        var columnNames = string.Join(", ", properties.Select(p => $"\"{p.Name}\""));
        var parameterNames = string.Join(", ", properties.Select(p => $"@{p.Name}"));

        var query = _queryStore.Render("GenericRepository:InsertReturningId", new Dictionary<string, string>
        {
            ["TableName"] = _tableName,
            ["ColumnNames"] = columnNames,
            ["ParameterNames"] = parameterNames
        });
        try
        {
            var id = await connection.ExecuteScalarAsync<int>(query, entity);
            var idProp = typeof(T).GetProperty("Id");
            if (idProp != null && idProp.CanWrite)
            {
                idProp.SetValue(entity, id);
            }
            return id;
        }
        catch (Exception)
        {
            // Fallback for tables without returning ID or custom primary key handling
            var nonReturningQuery = _queryStore.Render("GenericRepository:InsertNonReturning", new Dictionary<string, string>
            {
                ["TableName"] = _tableName,
                ["ColumnNames"] = columnNames,
                ["ParameterNames"] = parameterNames
            });
            return await connection.ExecuteAsync(nonReturningQuery, entity);
        }
    }

    public virtual async Task<int> UpdateAsync(T entity)
    {
        using var connection = _connectionFactory.CreateConnection();
        var properties = GetProperties(excludeId: true);
        var updateFields = string.Join(", ", properties.Select(p => $"\"{p.Name}\" = @{p.Name}"));

        var query = _queryStore.Render("GenericRepository:Update", new Dictionary<string, string>
        {
            ["TableName"] = _tableName,
            ["UpdateFields"] = updateFields
        });
        return await connection.ExecuteAsync(query, entity);
    }

    public virtual async Task<int> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Render("GenericRepository:Delete", new Dictionary<string, string> { ["TableName"] = _tableName });
        return await connection.ExecuteAsync(query, new { Id = id });
    }

    private IEnumerable<PropertyInfo> GetProperties(bool excludeId)
    {
        var properties = typeof(T).GetProperties();
        if (excludeId)
        {
            return properties.Where(p => !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
        }
        return properties;
    }
}
